#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Facepunch;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

public class Item : Facepunch.Pool.IPooled
{
	[Flags]
	public enum Flag
	{
		None = 0,
		Placeholder = 1,
		IsOn = 2,
		OnFire = 4,
		IsLocked = 8,
		Cooking = 0x10,
		Radioactive = 0x20
	}

	private const string DefaultArmourBreakEffectPath = "assets/bundled/prefabs/fx/armor_break.prefab";

	private float _condition;

	private float _maxCondition = 100f;

	public ItemDefinition info;

	public ItemId uid;

	public bool dirty;

	public int amount = 1;

	public int position;

	public float busyTime;

	public float removeTime;

	public float fuel;

	public bool isServer;

	public ProtoBuf.Item.InstanceData instanceData;

	public ulong skin;

	public string name;

	public string streamerName;

	public string text;

	public float cookTimeLeft;

	public float radioactivity;

	public Flag flags;

	public ItemContainer contents;

	public ItemContainer parent;

	private EntityRef worldEnt;

	private EntityRef heldEntity;

	public float condition
	{
		get
		{
			return _condition;
		}
		set
		{
			float f = _condition;
			_condition = Mathf.Clamp(value, 0f, maxCondition);
			if (isServer && Mathf.Ceil(value) != Mathf.Ceil(f))
			{
				MarkDirty();
			}
		}
	}

	public float maxCondition
	{
		get
		{
			return _maxCondition;
		}
		set
		{
			_maxCondition = Mathf.Clamp(value, 0f, info.condition.max);
			if (isServer)
			{
				MarkDirty();
			}
		}
	}

	public float maxConditionNormalized => _maxCondition / info.condition.max;

	public float conditionNormalized
	{
		get
		{
			if (!hasCondition)
			{
				return 1f;
			}
			return condition / maxCondition;
		}
		set
		{
			if (hasCondition)
			{
				condition = value * maxCondition;
			}
		}
	}

	public bool hasCondition
	{
		get
		{
			if (info != null && info.condition.enabled)
			{
				return info.condition.max > 0f;
			}
			return false;
		}
	}

	public bool isBroken
	{
		get
		{
			if (hasCondition)
			{
				return condition <= 0f;
			}
			return false;
		}
	}

	public int? ammoCount { get; set; }

	public int despawnMultiplier
	{
		get
		{
			Rarity rarity = info.despawnRarity;
			if (rarity == Rarity.None)
			{
				rarity = info.rarity;
			}
			if (!(info != null))
			{
				return 1;
			}
			return Mathf.Clamp((int)(rarity - 1) * 4, 1, 100);
		}
	}

	public ItemDefinition blueprintTargetDef
	{
		get
		{
			if (!IsBlueprint())
			{
				return null;
			}
			return ItemManager.FindItemDefinition(blueprintTarget);
		}
	}

	public int blueprintTarget
	{
		get
		{
			if (instanceData == null)
			{
				return 0;
			}
			return instanceData.blueprintTarget;
		}
		set
		{
			if (instanceData == null)
			{
				instanceData = new ProtoBuf.Item.InstanceData();
			}
			instanceData.ShouldPool = false;
			instanceData.blueprintTarget = value;
		}
	}

	public int blueprintAmount
	{
		get
		{
			return amount;
		}
		set
		{
			amount = value;
		}
	}

	public Item parentItem
	{
		get
		{
			if (parent == null)
			{
				return null;
			}
			return parent.parent;
		}
	}

	public float temperature
	{
		get
		{
			if (parent != null)
			{
				return parent.GetTemperature(position);
			}
			return 15f;
		}
	}

	public BaseEntity.TraitFlag Traits => info.Traits;

	public event Action<Item> OnDirty;

	public event Action<Item, float> onCycle;

	public void LoseCondition(float amount)
	{
		if (hasCondition && !Debugging.disablecondition)
		{
			float num = condition;
			condition -= amount;
			if (ConVar.Global.developer > 0)
			{
				Debug.Log(info.shortname + " was damaged by: " + amount + "cond is: " + condition + "/" + maxCondition);
			}
			if (condition <= 0f && condition < num)
			{
				OnBroken();
			}
		}
	}

	public void RepairCondition(float amount)
	{
		if (hasCondition)
		{
			condition += amount;
		}
	}

	public void DoRepair(float maxLossFraction)
	{
		if (hasCondition)
		{
			if (info.condition.maintainMaxCondition)
			{
				maxLossFraction = 0f;
			}
			float num = 1f - condition / maxCondition;
			maxLossFraction = Mathf.Clamp(maxLossFraction, 0f, info.condition.max);
			maxCondition *= 1f - maxLossFraction * num;
			condition = maxCondition;
			BaseEntity baseEntity = GetHeldEntity();
			if (baseEntity != null)
			{
				baseEntity.SetFlag(BaseEntity.Flags.Broken, b: false);
			}
			if (ConVar.Global.developer > 0)
			{
				Debug.Log(info.shortname + " was repaired! new cond is: " + condition + "/" + maxCondition);
			}
		}
	}

	public ItemContainer GetRootContainer()
	{
		ItemContainer itemContainer = parent;
		int num = 0;
		while (itemContainer != null && num <= 8 && itemContainer.parent != null && itemContainer.parent.parent != null)
		{
			itemContainer = itemContainer.parent.parent;
			num++;
		}
		if (num == 8)
		{
			Debug.LogWarning("GetRootContainer failed with 8 iterations");
		}
		return itemContainer;
	}

	public virtual void OnBroken()
	{
		if (!hasCondition)
		{
			return;
		}
		BaseEntity baseEntity = GetHeldEntity();
		if (baseEntity != null)
		{
			baseEntity.SetFlag(BaseEntity.Flags.Broken, b: true);
		}
		BasePlayer ownerPlayer = GetOwnerPlayer();
		if ((bool)ownerPlayer)
		{
			if (ownerPlayer.GetActiveItem() == this)
			{
				Effect.server.Run("assets/bundled/prefabs/fx/item_break.prefab", ownerPlayer, 0u, Vector3.zero, Vector3.zero);
				ownerPlayer.ShowToast(GameTip.Styles.Error, PlayerInventoryErrors.ActiveItemBroken, false);
			}
			if (info.TryGetComponent<ItemModWearable>(out var component) && ownerPlayer.inventory.containerWear.itemList.Contains(this))
			{
				if (component.breakEffect.isValid)
				{
					Effect.server.Run(component.breakEffect.resourcePath, ownerPlayer, 0u, Vector3.zero, Vector3.zero);
				}
				else
				{
					Effect.server.Run("assets/bundled/prefabs/fx/armor_break.prefab", ownerPlayer, 0u, Vector3.zero, Vector3.zero);
				}
			}
		}
		if ((!info.condition.repairable && !info.GetComponent<ItemModRepair>()) || maxCondition <= 5f)
		{
			UnloadAmmo();
			Remove();
		}
		else if (parent != null && parent.HasFlag(ItemContainer.Flag.NoBrokenItems))
		{
			ItemContainer rootContainer = GetRootContainer();
			if (rootContainer.HasFlag(ItemContainer.Flag.NoBrokenItems))
			{
				Remove();
			}
			else
			{
				BasePlayer playerOwner = rootContainer.playerOwner;
				if (playerOwner != null && !MoveToContainer(playerOwner.inventory.containerMain))
				{
					Drop(playerOwner.transform.position, playerOwner.eyes.BodyForward() * 1.5f);
				}
			}
		}
		MarkDirty();
	}

	void Facepunch.Pool.IPooled.EnterPool()
	{
		info = null;
		uid = default(ItemId);
		dirty = false;
		amount = 1;
		position = 0;
		busyTime = 0f;
		removeTime = 0f;
		fuel = 0f;
		isServer = false;
		instanceData = null;
		skin = 0uL;
		name = null;
		streamerName = null;
		text = null;
		ammoCount = null;
		cookTimeLeft = 0f;
		radioactivity = 0f;
		flags = Flag.None;
		if (contents != null)
		{
			Facepunch.Pool.Free(ref contents);
		}
		parent = null;
		heldEntity = default(EntityRef);
		worldEnt = default(EntityRef);
		this.onCycle = null;
		this.OnDirty = null;
		_condition = 0f;
		_maxCondition = 100f;
	}

	void Facepunch.Pool.IPooled.LeavePool()
	{
	}

	public string GetName(bool? streamerModeOverride = null)
	{
		if (streamerModeOverride.HasValue)
		{
			if (!streamerModeOverride.Value)
			{
				return name;
			}
			return streamerName ?? name;
		}
		return name;
	}

	public bool IsBlueprint()
	{
		return blueprintTarget != 0;
	}

	public bool HasFlag(Flag f)
	{
		return (flags & f) == f;
	}

	public void SetFlag(Flag f, bool b)
	{
		if (b)
		{
			flags |= f;
		}
		else
		{
			flags &= ~f;
		}
	}

	public bool IsOn()
	{
		return HasFlag(Flag.IsOn);
	}

	public bool IsOnFire()
	{
		return HasFlag(Flag.OnFire);
	}

	public bool IsCooking()
	{
		return HasFlag(Flag.Cooking);
	}

	public bool IsLocked()
	{
		if (!HasFlag(Flag.IsLocked))
		{
			if (parent != null)
			{
				return parent.IsLocked();
			}
			return false;
		}
		return true;
	}

	public bool IsRadioactive()
	{
		return HasFlag(Flag.Radioactive);
	}

	public void MarkDirty()
	{
		OnChanged();
		dirty = true;
		if (parent != null)
		{
			parent.MarkDirty();
		}
		if (this.OnDirty != null)
		{
			this.OnDirty(this);
		}
	}

	public void OnChanged()
	{
		ItemMod[] itemMods = info.itemMods;
		for (int i = 0; i < itemMods.Length; i++)
		{
			itemMods[i].OnChanged(this);
		}
		if (contents != null)
		{
			contents.OnChanged();
		}
	}

	public void CollectedForCrafting(BasePlayer crafter)
	{
		ItemMod[] itemMods = info.itemMods;
		for (int i = 0; i < itemMods.Length; i++)
		{
			itemMods[i].CollectedForCrafting(this, crafter);
		}
	}

	public void ReturnedFromCancelledCraft(BasePlayer crafter)
	{
		ItemMod[] itemMods = info.itemMods;
		for (int i = 0; i < itemMods.Length; i++)
		{
			itemMods[i].ReturnedFromCancelledCraft(this, crafter);
		}
	}

	public void Initialize(ItemDefinition template)
	{
		uid = new ItemId(Network.Net.sv.TakeUID());
		float num = (maxCondition = info.condition.max);
		condition = num;
		SetRadioactivity(template);
		OnItemCreated();
	}

	public void OnItemCreated()
	{
		this.onCycle = null;
		ItemMod[] itemMods = info.itemMods;
		for (int i = 0; i < itemMods.Length; i++)
		{
			itemMods[i].OnItemCreated(this);
		}
	}

	public void OnVirginSpawn()
	{
		ItemMod[] itemMods = info.itemMods;
		for (int i = 0; i < itemMods.Length; i++)
		{
			itemMods[i].OnVirginItem(this);
		}
	}

	public float GetDespawnDuration()
	{
		if (info.quickDespawn)
		{
			return ConVar.Server.itemdespawn_quick;
		}
		int num = 0;
		if (contents != null && contents.itemList != null)
		{
			foreach (Item item in contents.itemList)
			{
				num += item.despawnMultiplier;
			}
		}
		return (float)Mathf.Min(Mathf.Max(despawnMultiplier, num), ConVar.Server.itemdespawn_container_max_multiplier) * ConVar.Server.itemdespawn;
	}

	protected void RemoveFromWorld()
	{
		BaseEntity worldEntity = GetWorldEntity();
		if (worldEntity == null)
		{
			return;
		}
		SetWorldEntity(null);
		OnRemovedFromWorld();
		if (contents != null)
		{
			contents.OnRemovedFromWorld();
		}
		if (worldEntity.IsValid())
		{
			if (worldEntity is WorldItem worldItem)
			{
				worldItem.RemoveItem();
			}
			worldEntity.Kill();
		}
	}

	public void OnRemovedFromWorld()
	{
		ItemMod[] itemMods = info.itemMods;
		for (int i = 0; i < itemMods.Length; i++)
		{
			itemMods[i].OnRemovedFromWorld(this);
		}
	}

	public void RemoveFromContainer()
	{
		if (parent != null)
		{
			SetParent(null);
		}
	}

	public bool DoItemSlotsConflict(Item other)
	{
		return (info.occupySlots & other.info.occupySlots) != 0;
	}

	public void SetParent(ItemContainer target)
	{
		if (target == parent)
		{
			return;
		}
		if (parent != null)
		{
			parent.Remove(this);
			parent = null;
		}
		if (target == null)
		{
			position = 0;
		}
		else
		{
			parent = target;
			if (!parent.Insert(this))
			{
				Remove();
				Debug.LogError("Item.SetParent caused remove - this shouldn't ever happen");
			}
		}
		MarkDirty();
		RecalulateParentEntity(children: false);
		if (parent != null)
		{
			ItemContainer itemContainer = parent;
			BasePlayer basePlayer = null;
			do
			{
				basePlayer = itemContainer.GetOwnerPlayer();
				itemContainer = itemContainer?.parent?.parent;
			}
			while (itemContainer != null && basePlayer == null);
			if (basePlayer != null)
			{
				if (!basePlayer.enableSaving)
				{
					ForbidHeldEntitySaving();
				}
				else
				{
					RestoreHeldEntitySaving();
				}
			}
		}
		else
		{
			RestoreHeldEntitySaving();
		}
	}

	public void RecalulateParentEntity(bool children)
	{
		ItemMod[] itemMods = info.itemMods;
		for (int i = 0; i < itemMods.Length; i++)
		{
			itemMods[i].OnParentChanged(this);
		}
		if (!children || contents == null)
		{
			return;
		}
		foreach (Item item in contents.itemList)
		{
			item.RecalulateParentEntity(children: false);
		}
	}

	private void ForbidHeldEntitySaving()
	{
		if (heldEntity.IsValid(serverside: true))
		{
			heldEntity.Get(serverside: true).EnableSaving(wants: false);
		}
		if (contents == null)
		{
			return;
		}
		foreach (Item item in contents.itemList)
		{
			item.ForbidHeldEntitySaving();
		}
	}

	private void RestoreHeldEntitySaving()
	{
		if (heldEntity.IsValid(serverside: true))
		{
			heldEntity.Get(serverside: true).RestoreCanSave();
		}
		if (contents == null)
		{
			return;
		}
		foreach (Item item in contents.itemList)
		{
			item.RestoreHeldEntitySaving();
		}
	}

	public void OnAttacked(HitInfo hitInfo)
	{
		ItemMod[] itemMods = info.itemMods;
		for (int i = 0; i < itemMods.Length; i++)
		{
			itemMods[i].OnAttacked(this, hitInfo);
		}
	}

	public BaseEntity GetEntityOwner()
	{
		return parent?.GetEntityOwner();
	}

	public bool IsChildContainer(ItemContainer c)
	{
		if (contents == null)
		{
			return false;
		}
		if (contents == c)
		{
			return true;
		}
		foreach (Item item in contents.itemList)
		{
			if (item.IsChildContainer(c))
			{
				return true;
			}
		}
		return false;
	}

	public bool CanMoveTo(ItemContainer newcontainer, int iTargetPos = -1)
	{
		if (IsChildContainer(newcontainer))
		{
			return false;
		}
		if (newcontainer.CanAcceptItem(this, iTargetPos) != 0)
		{
			return false;
		}
		if (iTargetPos >= newcontainer.capacity)
		{
			return false;
		}
		if (parent != null && newcontainer == parent && iTargetPos == position)
		{
			return false;
		}
		return true;
	}

	public bool MoveToContainer(ItemContainer newcontainer, int iTargetPos = -1, bool allowStack = true, bool ignoreStackLimit = false, BasePlayer sourcePlayer = null, bool allowSwap = true)
	{
		using (TimeWarning.New("MoveToContainer"))
		{
			bool flag = iTargetPos == -1;
			ItemContainer itemContainer = parent;
			if (iTargetPos == -1)
			{
				if (allowStack && info.stackable > 1)
				{
					foreach (Item item3 in from x in newcontainer.FindItemsByItemID(info.itemid)
						orderby x.position
						select x)
					{
						if (item3.CanStack(this) && (ignoreStackLimit || item3.amount < item3.MaxStackable()))
						{
							iTargetPos = item3.position;
						}
					}
				}
				if (iTargetPos == -1 && newcontainer.GetEntityOwner(returnHeldEntity: true) is IIdealSlotEntity idealSlotEntity)
				{
					iTargetPos = idealSlotEntity.GetIdealSlot(sourcePlayer, newcontainer, this);
					if (iTargetPos == int.MinValue)
					{
						return false;
					}
				}
				if (iTargetPos == -1)
				{
					if (newcontainer == parent)
					{
						return false;
					}
					bool flag2 = newcontainer.HasFlag(ItemContainer.Flag.Clothing) && info.isWearable;
					ItemModWearable itemModWearable = info.ItemModWearable;
					for (int i = 0; i < newcontainer.capacity; i++)
					{
						Item slot = newcontainer.GetSlot(i);
						if (slot == null)
						{
							if (CanMoveTo(newcontainer, i))
							{
								iTargetPos = i;
								break;
							}
							continue;
						}
						if (flag2 && slot != null && !slot.info.ItemModWearable.CanExistWith(itemModWearable))
						{
							iTargetPos = i;
							break;
						}
						if (newcontainer.HasAvailableSlotsDefined && DoItemSlotsConflict(slot))
						{
							iTargetPos = i;
							break;
						}
					}
					if (flag2 && iTargetPos == -1)
					{
						iTargetPos = newcontainer.capacity - 1;
					}
				}
			}
			if (iTargetPos == -1)
			{
				return false;
			}
			if (!CanMoveTo(newcontainer, iTargetPos))
			{
				return false;
			}
			if (iTargetPos >= 0 && newcontainer.SlotTaken(this, iTargetPos))
			{
				Item slot2 = newcontainer.GetSlot(iTargetPos);
				if (slot2 == this)
				{
					return false;
				}
				if (allowStack && slot2 != null)
				{
					int num = slot2.MaxStackable();
					if (slot2.CanStack(this))
					{
						if (ignoreStackLimit)
						{
							num = int.MaxValue;
						}
						if (slot2.amount >= num)
						{
							return false;
						}
						int num2 = Mathf.Min(num - slot2.amount, amount);
						slot2.amount += num2;
						newcontainer.onItemAddedToStack?.Invoke(slot2, num2);
						amount -= num2;
						slot2.MarkDirty();
						MarkDirty();
						if (amount <= 0)
						{
							RemoveFromWorld();
							RemoveFromContainer();
							Remove();
							return true;
						}
						if (flag)
						{
							return MoveToContainer(newcontainer, -1, allowStack, ignoreStackLimit, sourcePlayer);
						}
						return false;
					}
				}
				if (parent != null && allowSwap && slot2 != null)
				{
					ItemContainer itemContainer2 = parent;
					int iTargetPos2 = position;
					ItemContainer newcontainer2 = slot2.parent;
					int num3 = slot2.position;
					if (!slot2.CanMoveTo(itemContainer2, iTargetPos2))
					{
						return false;
					}
					if (itemContainer2.maxStackSize > 0 && slot2.amount > itemContainer2.maxStackSize)
					{
						Item item = slot2.SplitItem(slot2.amount - itemContainer2.maxStackSize);
						if (item == null || !item.MoveToContainer(newcontainer2, -1, allowStack: false, ignoreStackLimit: false, sourcePlayer, allowSwap: false))
						{
							slot2.amount += item.amount;
							item.Remove();
							return false;
						}
					}
					BaseEntity entityOwner = GetEntityOwner();
					BaseEntity entityOwner2 = slot2.GetEntityOwner();
					RemoveFromContainer();
					slot2.RemoveFromContainer();
					RemoveConflictingSlots(newcontainer, entityOwner, sourcePlayer);
					slot2.RemoveConflictingSlots(itemContainer2, entityOwner2, sourcePlayer);
					if (!slot2.MoveToContainer(itemContainer2, iTargetPos2, allowStack: true, ignoreStackLimit: false, sourcePlayer) || !MoveToContainer(newcontainer, iTargetPos, allowStack: true, ignoreStackLimit: false, sourcePlayer))
					{
						RemoveFromContainer();
						slot2.RemoveFromContainer();
						SetParent(itemContainer2);
						position = iTargetPos2;
						slot2.SetParent(newcontainer2);
						slot2.position = num3;
						return true;
					}
					return true;
				}
				return false;
			}
			if (parent == newcontainer)
			{
				if (iTargetPos >= 0 && iTargetPos != position && !parent.SlotTaken(this, iTargetPos))
				{
					position = iTargetPos;
					MarkDirty();
					return true;
				}
				return false;
			}
			if (newcontainer.maxStackSize > 0 && newcontainer.maxStackSize < amount)
			{
				Item item2 = SplitItem(newcontainer.maxStackSize);
				if (item2 != null && !item2.MoveToContainer(newcontainer, iTargetPos, allowStack: false, ignoreStackLimit: false, sourcePlayer) && (itemContainer == null || !item2.MoveToContainer(itemContainer, -1, allowStack: true, ignoreStackLimit: false, sourcePlayer)))
				{
					DroppedItem droppedItem = item2.Drop(newcontainer.dropPosition, newcontainer.dropVelocity) as DroppedItem;
					if (droppedItem != null)
					{
						droppedItem.DroppedBy = sourcePlayer?.userID ?? ((BasePlayer.EncryptedValue<ulong>)0uL);
					}
				}
				return true;
			}
			if (!newcontainer.CanAccept(this))
			{
				return false;
			}
			BaseEntity entityOwner3 = GetEntityOwner();
			RemoveFromContainer();
			RemoveFromWorld();
			RemoveConflictingSlots(newcontainer, entityOwner3, sourcePlayer);
			position = iTargetPos;
			SetParent(newcontainer);
			return true;
		}
	}

	private void RemoveConflictingSlots(ItemContainer container, BaseEntity entityOwner, BasePlayer sourcePlayer)
	{
		if (!isServer || !container.HasAvailableSlotsDefined)
		{
			return;
		}
		List<Item> obj = Facepunch.Pool.Get<List<Item>>();
		obj.AddRange(container.itemList);
		foreach (Item item in obj)
		{
			if (item.DoItemSlotsConflict(this))
			{
				item.RemoveFromContainer();
				if (entityOwner is BasePlayer basePlayer)
				{
					basePlayer.GiveItem(item);
				}
				else if (entityOwner is IItemContainerEntity itemContainerEntity)
				{
					item.MoveToContainer(itemContainerEntity.inventory, -1, allowStack: true, ignoreStackLimit: false, sourcePlayer);
				}
			}
		}
		Facepunch.Pool.Free(ref obj, freeElements: false);
	}

	public BaseEntity CreateWorldObject(Vector3 pos, Quaternion rotation = default(Quaternion), BaseEntity parentEnt = null, uint parentBone = 0u)
	{
		BaseEntity worldEntity = GetWorldEntity();
		if (worldEntity != null)
		{
			return worldEntity;
		}
		worldEntity = GameManager.server.CreateEntity("assets/prefabs/misc/burlap sack/generic_world.prefab", pos, rotation);
		if (worldEntity == null)
		{
			Debug.LogWarning("Couldn't create world object for prefab: items/generic_world");
			return null;
		}
		WorldItem worldItem = worldEntity as WorldItem;
		if (worldItem != null)
		{
			worldItem.InitializeItem(this);
		}
		if (parentEnt != null)
		{
			worldEntity.SetParent(parentEnt, parentBone);
		}
		worldEntity.Spawn();
		SetWorldEntity(worldEntity);
		return GetWorldEntity();
	}

	public BaseEntity Drop(Vector3 vPos, Vector3 vVelocity, Quaternion rotation = default(Quaternion))
	{
		ulong droppedBy = GetRootContainer()?.playerOwner?.userID ?? ((BasePlayer.EncryptedValue<ulong>)0uL);
		RemoveFromWorld();
		if (info.AlignWorldModelOnDrop)
		{
			rotation = Quaternion.Euler(0f, Quaternion.LookRotation(vVelocity.normalized, Vector3.up).eulerAngles.y, 0f);
			rotation = Quaternion.Euler(info.WorldModelDropOffset) * rotation;
		}
		BaseEntity baseEntity = null;
		if (vPos != Vector3.zero && !info.HasFlag(ItemDefinition.Flag.NoDropping))
		{
			baseEntity = CreateWorldObject(vPos, rotation);
			if ((bool)baseEntity)
			{
				baseEntity.SetVelocity(vVelocity);
			}
			if (baseEntity is DroppedItem droppedItem)
			{
				droppedItem.DroppedBy = droppedBy;
				if (info.AdjustCenterOfMassOnDrop)
				{
					droppedItem.Rigidbody.centerOfMass = info.DropCenterOfMass;
				}
			}
		}
		else
		{
			Remove();
		}
		RemoveFromContainer();
		return baseEntity;
	}

	public BaseEntity DropAndTossUpwards(Vector3 vPos, float force = 2f)
	{
		float f = UnityEngine.Random.value * MathF.PI * 2f;
		return Drop(vVelocity: new Vector3(Mathf.Sin(f), 1f, Mathf.Cos(f)) * force, vPos: vPos + Vector3.up * 0.1f);
	}

	public bool IsBusy()
	{
		if (busyTime > UnityEngine.Time.time)
		{
			return true;
		}
		return false;
	}

	public void BusyFor(float fTime)
	{
		busyTime = UnityEngine.Time.time + fTime;
	}

	public bool IsRemoved()
	{
		return removeTime > 0f;
	}

	public void Remove(float fTime = 0f)
	{
		if (removeTime > 0f)
		{
			return;
		}
		if (isServer)
		{
			ItemMod[] itemMods = info.itemMods;
			for (int i = 0; i < itemMods.Length; i++)
			{
				itemMods[i].OnRemove(this);
			}
		}
		this.onCycle = null;
		removeTime = UnityEngine.Time.time + fTime;
		this.OnDirty = null;
		position = -1;
		ItemManager.RemoveItem(this, fTime);
	}

	internal void DoRemove()
	{
		this.OnDirty = null;
		this.onCycle = null;
		if (isServer && uid.IsValid && Network.Net.sv != null)
		{
			Network.Net.sv.ReturnUID(uid.Value);
			uid = default(ItemId);
		}
		if (contents != null)
		{
			Facepunch.Pool.Free(ref contents);
		}
		if (isServer)
		{
			RemoveFromWorld();
			RemoveFromContainer();
		}
		BaseEntity baseEntity = GetHeldEntity();
		if (baseEntity.IsValid())
		{
			Debug.LogWarning("Item's Held Entity not removed!" + info.displayName.english + " -> " + baseEntity, baseEntity);
		}
	}

	public void SwitchOnOff(bool bNewState)
	{
		if (HasFlag(Flag.IsOn) != bNewState)
		{
			SetFlag(Flag.IsOn, bNewState);
			MarkDirty();
		}
	}

	public void LockUnlock(bool bNewState)
	{
		if (HasFlag(Flag.IsLocked) != bNewState)
		{
			SetFlag(Flag.IsLocked, bNewState);
			MarkDirty();
		}
	}

	public BasePlayer GetOwnerPlayer()
	{
		if (parent == null)
		{
			return null;
		}
		return parent.GetOwnerPlayer();
	}

	public bool IsBackpack()
	{
		if (info != null)
		{
			return (info.flags & ItemDefinition.Flag.Backpack) != 0;
		}
		return false;
	}

	public int GetChildItemCount()
	{
		return (contents?.itemList?.Count).GetValueOrDefault();
	}

	public int GetItemVolume()
	{
		if (IsBackpack() && (contents?.itemList?.Count).GetValueOrDefault() > 0)
		{
			ItemModBackpack component = info.GetComponent<ItemModBackpack>();
			if (component != null)
			{
				return component.containerVolumeWhenFilled;
			}
		}
		return info.volume;
	}

	public Item SplitItem(int split_Amount)
	{
		Assert.IsTrue(split_Amount > 0, "split_Amount <= 0");
		if (split_Amount <= 0)
		{
			return null;
		}
		if (split_Amount >= amount)
		{
			return null;
		}
		amount -= split_Amount;
		Item item = ItemManager.CreateByItemID(info.itemid, 1, 0uL);
		item.amount = split_Amount;
		item.skin = skin;
		if (IsBlueprint())
		{
			item.blueprintTarget = blueprintTarget;
		}
		if (info.amountType == ItemDefinition.AmountType.Genetics && instanceData != null && instanceData.dataInt != 0)
		{
			item.instanceData = new ProtoBuf.Item.InstanceData();
			item.instanceData.dataInt = instanceData.dataInt;
			item.instanceData.ShouldPool = false;
		}
		if (instanceData != null && instanceData.dataInt > 0 && info != null && info.Blueprint != null && info.Blueprint.workbenchLevelRequired == 3)
		{
			item.instanceData = new ProtoBuf.Item.InstanceData();
			item.instanceData.dataInt = instanceData.dataInt;
			item.instanceData.ShouldPool = false;
			item.SetFlag(Flag.IsOn, IsOn());
		}
		MarkDirty();
		return item;
	}

	public void UnloadAmmo()
	{
		BaseProjectile baseProjectile = GetHeldEntity() as BaseProjectile;
		if (baseProjectile == null)
		{
			return;
		}
		while (baseProjectile.primaryMagazine.contents > 0)
		{
			int num = Mathf.Min(baseProjectile.primaryMagazine.contents, baseProjectile.primaryMagazine.ammoType.stackable);
			baseProjectile.primaryMagazine.contents -= num;
			Item item = ItemManager.Create(baseProjectile.primaryMagazine.ammoType, num, 0uL);
			BasePlayer basePlayer = GetRootContainer()?.playerOwner;
			if (basePlayer != null)
			{
				basePlayer.GiveItem(item);
			}
			else if (!item.MoveToContainer(parent) && item.Drop(parent.dropPosition, parent.dropVelocity) == null)
			{
				item.Remove();
			}
		}
	}

	public bool CanBeHeld()
	{
		if (isBroken)
		{
			return false;
		}
		return true;
	}

	public bool CanStack(Item item)
	{
		if (item == this)
		{
			return false;
		}
		if (MaxStackable() <= 1)
		{
			return false;
		}
		if (item.info.stackable <= 1)
		{
			return false;
		}
		if (item.info.itemid != info.itemid)
		{
			return false;
		}
		if (hasCondition && condition != item.info.condition.max)
		{
			return false;
		}
		if (item.hasCondition && item.condition != item.info.condition.max)
		{
			return false;
		}
		if (!IsValid())
		{
			return false;
		}
		if (IsBlueprint() && blueprintTarget != item.blueprintTarget)
		{
			return false;
		}
		if (item.skin != skin)
		{
			return false;
		}
		if (item.info.amountType == ItemDefinition.AmountType.Genetics || info.amountType == ItemDefinition.AmountType.Genetics)
		{
			int num = ((item.instanceData != null) ? item.instanceData.dataInt : (-1));
			int num2 = ((instanceData != null) ? instanceData.dataInt : (-1));
			if (num != num2)
			{
				return false;
			}
		}
		if (item.instanceData != null && instanceData != null && (item.IsOn() != IsOn() || (item.instanceData.dataInt != instanceData.dataInt && item.info.Blueprint != null && item.info.Blueprint.workbenchLevelRequired == 3)))
		{
			return false;
		}
		if (instanceData != null && instanceData.subEntity.IsValid && (bool)info.GetComponent<ItemModSign>())
		{
			return false;
		}
		if (item.instanceData != null && item.instanceData.subEntity.IsValid && (bool)item.info.GetComponent<ItemModSign>())
		{
			return false;
		}
		return true;
	}

	public bool IsValid()
	{
		if (removeTime > 0f)
		{
			return false;
		}
		return true;
	}

	public bool IsDroppedInWorld(bool serverside)
	{
		return worldEnt.IsValid(serverside);
	}

	public void SetWorldEntity(BaseEntity ent)
	{
		if (!ent.IsValid())
		{
			worldEnt.Set(null);
			MarkDirty();
		}
		else if (!(worldEnt.uid == ent.net.ID))
		{
			worldEnt.Set(ent);
			MarkDirty();
			OnMovedToWorld();
			if (contents != null)
			{
				contents.OnMovedToWorld();
			}
		}
	}

	public void OnMovedToWorld()
	{
		ItemMod[] itemMods = info.itemMods;
		for (int i = 0; i < itemMods.Length; i++)
		{
			itemMods[i].OnMovedToWorld(this);
		}
	}

	public BaseEntity GetWorldEntity()
	{
		return worldEnt.Get(isServer);
	}

	public void SetHeldEntity(BaseEntity ent)
	{
		if (!ent.IsValid())
		{
			this.heldEntity.Set(null);
			MarkDirty();
		}
		else
		{
			if (this.heldEntity.uid == ent.net.ID)
			{
				return;
			}
			this.heldEntity.Set(ent);
			MarkDirty();
			if (ent.IsValid())
			{
				HeldEntity heldEntity = ent as HeldEntity;
				if (heldEntity != null)
				{
					heldEntity.SetupHeldEntity(this);
				}
			}
		}
	}

	public BaseEntity GetHeldEntity()
	{
		return heldEntity.Get(isServer);
	}

	public void OnCycle(float delta)
	{
		if (this.onCycle != null)
		{
			this.onCycle(this, delta);
		}
	}

	public void ServerCommand(string command, BasePlayer player)
	{
		HeldEntity heldEntity = GetHeldEntity() as HeldEntity;
		if (heldEntity != null)
		{
			heldEntity.ServerCommand(this, command, player);
		}
		ItemMod[] itemMods = info.itemMods;
		for (int i = 0; i < itemMods.Length; i++)
		{
			itemMods[i].ServerCommand(this, command, player);
		}
	}

	public void UseItem(int amountToConsume = 1)
	{
		if (amountToConsume > 0)
		{
			amount -= amountToConsume;
			if (amount <= 0)
			{
				amount = 0;
				Remove();
			}
			else
			{
				MarkDirty();
			}
		}
	}

	public bool HasAmmo(AmmoTypes ammoType)
	{
		if (info.TryGetComponent<ItemModProjectile>(out var component) && component.IsAmmo(ammoType))
		{
			return true;
		}
		if (contents != null)
		{
			if (info != null && info.TryGetComponent<ItemModContainer>(out var component2) && component2.blockAmmoSource)
			{
				return false;
			}
			return contents.HasAmmo(ammoType);
		}
		return false;
	}

	public Item FindAmmo(AmmoTypes ammoType)
	{
		if (info.TryGetComponent<ItemModProjectile>(out var component) && component.IsAmmo(ammoType))
		{
			return this;
		}
		if (contents != null)
		{
			return contents.FindAmmo(ammoType);
		}
		return null;
	}

	public void FindAmmo(List<Item> list, AmmoTypes ammoType)
	{
		if (info.TryGetComponent<ItemModProjectile>(out var component) && component.IsAmmo(ammoType))
		{
			list.Add(this);
		}
		else if (contents != null)
		{
			contents.FindAmmo(list, ammoType);
		}
	}

	public int GetAmmoAmount(AmmoTypes ammoType)
	{
		int num = 0;
		if (info.TryGetComponent<ItemModProjectile>(out var component) && component.IsAmmo(ammoType))
		{
			num += amount;
		}
		if (contents != null)
		{
			num += contents.GetAmmoAmount(ammoType);
		}
		return num;
	}

	public int GetAmmoAmount(List<AmmoTypes> ammoTypes)
	{
		int num = 0;
		if (info.TryGetComponent<ItemModProjectile>(out var component))
		{
			foreach (AmmoTypes ammoType in ammoTypes)
			{
				if (component.IsAmmo(ammoType))
				{
					num += amount;
				}
			}
		}
		if (contents != null)
		{
			foreach (AmmoTypes ammoType2 in ammoTypes)
			{
				num += contents.GetAmmoAmount(ammoType2);
			}
		}
		return num;
	}

	public override string ToString()
	{
		string[] obj = new string[6]
		{
			"Item.",
			info.shortname,
			"x",
			amount.ToString(),
			".",
			null
		};
		ItemId itemId = uid;
		obj[5] = itemId.ToString();
		return string.Concat(obj);
	}

	public Item FindItem(ItemId iUID)
	{
		if (uid == iUID)
		{
			return this;
		}
		if (contents == null)
		{
			return null;
		}
		return contents.FindItemByUID(iUID);
	}

	public int MaxStackable()
	{
		int num = info.stackable;
		if (parent != null && parent.maxStackSize > 0)
		{
			num = Mathf.Min(parent.maxStackSize, num);
		}
		return num;
	}

	private void SetRadioactivity(ItemDefinition template)
	{
		if (!(template == null))
		{
			radioactivity = template.baseRadioactivity;
			if (radioactivity > 0f)
			{
				SetFlag(Flag.Radioactive, b: true);
			}
		}
	}

	public GameObjectRef GetWorldModel()
	{
		return info.GetWorldModel(amount);
	}

	public virtual ProtoBuf.Item Save(bool bIncludeContainer = false, bool bIncludeOwners = true)
	{
		dirty = false;
		ProtoBuf.Item item = Facepunch.Pool.Get<ProtoBuf.Item>();
		item.UID = uid;
		item.itemid = info.itemid;
		item.slot = position;
		item.amount = amount;
		item.flags = (int)flags;
		item.removetime = removeTime;
		item.locktime = busyTime;
		item.instanceData = instanceData;
		item.worldEntity = worldEnt.uid;
		item.heldEntity = heldEntity.uid;
		item.skinid = skin;
		item.name = name;
		item.streamerName = streamerName;
		item.text = text;
		item.cooktime = cookTimeLeft;
		item.ammoCount = 0;
		if (heldEntity.uid.IsValid)
		{
			BaseProjectile baseProjectile = GetHeldEntity() as BaseProjectile;
			if (baseProjectile != null)
			{
				item.ammoCount = baseProjectile.primaryMagazine.contents + 1;
			}
		}
		if (hasCondition)
		{
			item.conditionData = Facepunch.Pool.Get<ProtoBuf.Item.ConditionData>();
			item.conditionData.maxCondition = _maxCondition;
			item.conditionData.condition = _condition;
		}
		if (contents != null && bIncludeContainer)
		{
			item.contents = contents.Save(bIncludeContainer);
		}
		return item;
	}

	public virtual void Load(ProtoBuf.Item load)
	{
		if (info == null || info.itemid != load.itemid)
		{
			info = ItemManager.FindItemDefinition(load.itemid);
		}
		uid = load.UID;
		name = load.name;
		streamerName = load.streamerName;
		text = load.text;
		cookTimeLeft = load.cooktime;
		amount = load.amount;
		position = load.slot;
		busyTime = load.locktime;
		removeTime = load.removetime;
		flags = (Flag)load.flags;
		worldEnt.uid = load.worldEntity;
		heldEntity.uid = load.heldEntity;
		if (load.ammoCount == 0)
		{
			ammoCount = null;
		}
		else
		{
			ammoCount = load.ammoCount - 1;
		}
		SetRadioactivity(info);
		if (isServer)
		{
			Network.Net.sv.RegisterUID(uid.Value);
		}
		if (instanceData != null)
		{
			instanceData.ShouldPool = true;
			instanceData.ResetToPool();
			instanceData = null;
		}
		instanceData = load.instanceData;
		if (instanceData != null)
		{
			instanceData.ShouldPool = false;
		}
		skin = load.skinid;
		if (info == null || info.itemid != load.itemid)
		{
			info = ItemManager.FindItemDefinition(load.itemid);
		}
		if (info == null)
		{
			return;
		}
		_condition = 0f;
		_maxCondition = 0f;
		if (load.conditionData != null)
		{
			_condition = load.conditionData.condition;
			_maxCondition = load.conditionData.maxCondition;
		}
		else if (info.condition.enabled)
		{
			_condition = info.condition.max;
			_maxCondition = info.condition.max;
		}
		if (load.contents != null)
		{
			if (contents == null)
			{
				contents = Facepunch.Pool.Get<ItemContainer>();
				if (isServer)
				{
					contents.ServerInitialize(this, load.contents.slots);
				}
			}
			contents.Load(load.contents);
		}
		if (isServer)
		{
			removeTime = 0f;
			OnItemCreated();
		}
	}
}
