#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Facepunch;
using Facepunch.Rust;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

public class PlayerInventory : EntityComponent<BasePlayer>, IAmmoContainer
{
	public enum Type
	{
		Main,
		Belt,
		Wear,
		BackpackContents
	}

	public interface ICanMoveFrom
	{
		bool CanMoveFrom(BasePlayer player, Item item);
	}

	public enum NetworkInventoryMode
	{
		LocalPlayer,
		Everyone,
		EveryoneButLocal
	}

	private struct WearCheckResult
	{
		public bool Result;

		public List<Item> ChangedItem;
	}

	public ItemContainer containerMain;

	public ItemContainer containerBelt;

	public ItemContainer containerWear;

	public ItemCrafter crafting;

	public PlayerLoot loot;

	public static Translate.Phrase BackpackGroundedError = new Translate.Phrase("error.backpackGrounded", "You must be on a solid surface to equip a backpack");

	public float inventoryRadioactivity;

	public bool containsRadioactiveItems;

	private Action _deferredServerUpdateAction;

	private List<Item> returnItems;

	[ServerVar]
	public static bool forceBirthday = false;

	private static float nextCheckTime = 0f;

	private static bool wasBirthday = false;

	private Action DeferredServerUpdateAction => DeferredServerUpdate;

	public event Action<float, bool> onRadioactivityChanged;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("PlayerInventory.OnRpcMessage"))
		{
			if (rpc == 3482449460u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - ItemCmd ");
				}
				using (TimeWarning.New("ItemCmd"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!BaseEntity.RPC_Server.FromOwner.Test(3482449460u, "ItemCmd", GetBaseEntity(), player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							BaseEntity.RPCMessage rPCMessage = default(BaseEntity.RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							BaseEntity.RPCMessage msg2 = rPCMessage;
							ItemCmd(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in ItemCmd");
					}
				}
				return true;
			}
			if (rpc == 3041092525u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - MoveItem ");
				}
				using (TimeWarning.New("MoveItem"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!BaseEntity.RPC_Server.FromOwner.Test(3041092525u, "MoveItem", GetBaseEntity(), player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							BaseEntity.RPCMessage rPCMessage = default(BaseEntity.RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							BaseEntity.RPCMessage msg3 = rPCMessage;
							MoveItem(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in MoveItem");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	protected void Initialize(BasePlayer owner)
	{
		Debug.Assert(containerMain == null, "Double init of main container!");
		containerMain = Facepunch.Pool.Get<ItemContainer>();
		containerMain.SetFlag(ItemContainer.Flag.IsPlayer, b: true);
		Debug.Assert(containerBelt == null, "Double init of belt container!");
		containerBelt = Facepunch.Pool.Get<ItemContainer>();
		containerBelt.SetFlag(ItemContainer.Flag.IsPlayer, b: true);
		containerBelt.SetFlag(ItemContainer.Flag.Belt, b: true);
		Debug.Assert(containerWear == null, "Double init of wear container!");
		containerWear = Facepunch.Pool.Get<ItemContainer>();
		containerWear.SetFlag(ItemContainer.Flag.IsPlayer, b: true);
		containerWear.SetFlag(ItemContainer.Flag.Clothing, b: true);
		containerWear.containerVolume = 2;
		crafting = GetComponent<ItemCrafter>();
		if (crafting != null)
		{
			crafting.owner = owner;
			crafting.AddContainer(containerMain);
			crafting.AddContainer(containerBelt);
		}
		loot = GetComponent<PlayerLoot>();
		if (!loot)
		{
			loot = base.baseEntity.AddComponent<PlayerLoot>();
		}
	}

	public void DoDestroy()
	{
		if (containerMain != null)
		{
			Facepunch.Pool.Free(ref containerMain);
		}
		if (containerBelt != null)
		{
			Facepunch.Pool.Free(ref containerBelt);
		}
		if (containerWear != null)
		{
			Facepunch.Pool.Free(ref containerWear);
		}
	}

	public void SetLockedByRestraint(bool flag)
	{
		containerMain.SetLocked(flag);
		containerWear.SetLocked(flag);
		containerBelt.SetLocked(flag);
		GetContainer(Type.BackpackContents)?.SetLocked(flag);
	}

	public void ServerInit(BasePlayer owner)
	{
		Initialize(owner);
		containerMain.ServerInitialize(null, 24);
		if (!containerMain.uid.IsValid)
		{
			containerMain.GiveUID();
		}
		containerBelt.ServerInitialize(null, 6);
		if (!containerBelt.uid.IsValid)
		{
			containerBelt.GiveUID();
		}
		containerWear.ServerInitialize(null, 8);
		if (!containerWear.uid.IsValid)
		{
			containerWear.GiveUID();
		}
		containerMain.playerOwner = owner;
		containerBelt.playerOwner = owner;
		containerWear.playerOwner = owner;
		containerWear.onItemAddedRemoved = OnClothingChanged;
		containerWear.canAcceptItem = CanWearItem;
		containerBelt.canAcceptItem = CanEquipItem;
		containerMain.canAcceptItem = CanStoreInInventory;
		containerMain.onPreItemRemove = OnItemRemoved;
		containerWear.onPreItemRemove = OnItemRemoved;
		containerBelt.onPreItemRemove = OnItemRemoved;
		containerMain.onDirty += OnContentsDirty;
		containerBelt.onDirty += OnContentsDirty;
		containerWear.onDirty += OnContentsDirty;
		containerBelt.onItemAddedRemoved = OnItemAddedOrRemoved;
		containerMain.onItemAddedRemoved = OnItemAddedOrRemoved;
		ItemContainer itemContainer = containerWear;
		itemContainer.onItemAddedRemoved = (Action<Item, bool>)Delegate.Combine(itemContainer.onItemAddedRemoved, new Action<Item, bool>(OnItemAddedOrRemoved));
		containerWear.onItemRadiationChanged = OnItemRadiationChanged;
		containerBelt.onItemRadiationChanged = OnItemRadiationChanged;
		containerMain.onItemRadiationChanged = OnItemRadiationChanged;
		onRadioactivityChanged += owner.PlayerInventoryRadioactivityChange;
		CalculateInventoryRadioactivity();
	}

	public void OnItemAddedOrRemoved(Item item, bool bAdded)
	{
		if (item != null && (item.radioactivity > 0f || item.contents != null))
		{
			CalculateInventoryRadioactivity();
		}
		if (item.info.isHoldable)
		{
			Invoke(UpdatedVisibleHolsteredItems, 0.1f);
		}
		if (item.parent == containerBelt)
		{
			OnBeltItemAddedOrRemoved(item, bAdded);
		}
		if (bAdded)
		{
			BasePlayer basePlayer = base.baseEntity;
			if (!basePlayer.HasPlayerFlag(BasePlayer.PlayerFlags.DisplaySash) && basePlayer.IsHostileItem(item))
			{
				base.baseEntity.SetPlayerFlag(BasePlayer.PlayerFlags.DisplaySash, b: true);
			}
			if (bAdded)
			{
				basePlayer.ProcessMissionEvent(BaseMission.MissionEventType.ACQUIRE_ITEM, item.info.itemid, item.amount);
			}
		}
	}

	private void OnBeltItemAddedOrRemoved(Item item, bool added)
	{
		if (!added)
		{
			return;
		}
		ItemModForceWearFromBelt component = item.info.GetComponent<ItemModForceWearFromBelt>();
		if (!(component == null) && (!component.IfPlayerRestrained || base.baseEntity.IsRestrained))
		{
			bool num = containerWear.IsLocked();
			if (num)
			{
				containerWear.SetLocked(isLocked: false);
			}
			if (!item.MoveToContainer(containerWear))
			{
				item.MoveToContainer(containerWear, 0, allowStack: false);
			}
			if (num)
			{
				containerWear.SetLocked(isLocked: true);
			}
		}
	}

	public void UpdatedVisibleHolsteredItems()
	{
		List<HeldEntity> obj = Facepunch.Pool.Get<List<HeldEntity>>();
		List<Item> obj2 = Facepunch.Pool.Get<List<Item>>();
		GetAllItems(obj2);
		AddBackpackContentsToList(obj2);
		foreach (Item item in obj2)
		{
			if (item.info.isHoldable && !(item.GetHeldEntity() == null))
			{
				HeldEntity component = item.GetHeldEntity().GetComponent<HeldEntity>();
				if (!(component == null))
				{
					obj.Add(component);
				}
			}
		}
		Facepunch.Pool.Free(ref obj2, freeElements: false);
		IOrderedEnumerable<HeldEntity> orderedEnumerable = obj.OrderByDescending((HeldEntity x) => x.hostileScore);
		bool flag = true;
		bool flag2 = true;
		bool flag3 = true;
		foreach (HeldEntity item2 in orderedEnumerable)
		{
			if (!(item2 == null) && item2.holsterInfo.displayWhenHolstered)
			{
				if (flag3 && !item2.IsDeployed() && item2.holsterInfo.slot == HeldEntity.HolsterInfo.HolsterSlot.BACK)
				{
					item2.SetVisibleWhileHolstered(visible: true);
					flag3 = false;
				}
				else if (flag2 && !item2.IsDeployed() && item2.holsterInfo.slot == HeldEntity.HolsterInfo.HolsterSlot.RIGHT_THIGH)
				{
					item2.SetVisibleWhileHolstered(visible: true);
					flag2 = false;
				}
				else if (flag && !item2.IsDeployed() && item2.holsterInfo.slot == HeldEntity.HolsterInfo.HolsterSlot.LEFT_THIGH)
				{
					item2.SetVisibleWhileHolstered(visible: true);
					flag = false;
				}
				else
				{
					item2.SetVisibleWhileHolstered(visible: false);
				}
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	private void AddBackpackContentsToList(List<Item> items)
	{
		Item backpackWithInventory = GetBackpackWithInventory();
		if (backpackWithInventory != null && backpackWithInventory.contents != null)
		{
			items.AddRange(backpackWithInventory.contents.itemList);
		}
	}

	private void OnContentsDirty()
	{
		if (base.baseEntity != null)
		{
			base.baseEntity.InvalidateNetworkCache();
		}
	}

	private bool CanMoveItemsFrom(BaseEntity entity, Item item)
	{
		if (entity is ICanMoveFrom canMoveFrom && !canMoveFrom.CanMoveFrom(base.baseEntity, item))
		{
			return false;
		}
		if ((bool)BaseGameMode.GetActiveGameMode(serverside: true))
		{
			return BaseGameMode.GetActiveGameMode(serverside: true).CanMoveItemsFrom(this, entity, item);
		}
		return true;
	}

	[BaseEntity.RPC_Server]
	[BaseEntity.RPC_Server.FromOwner]
	private void ItemCmd(BaseEntity.RPCMessage msg)
	{
		if ((msg.player != null && msg.player.IsWounded()) || base.baseEntity.IsTransferring())
		{
			return;
		}
		ItemId id = msg.read.ItemID();
		string text = msg.read.String();
		Item item = FindItemByUID(id);
		if (item == null)
		{
			return;
		}
		BaseEntity entityOwner = item.GetEntityOwner();
		if ((entityOwner != null && entityOwner == msg.player && msg.player.IsRestrainedOrSurrendering) || item.IsLocked() || (item.parent != null && item.parent.IsLocked()) || !CanMoveItemsFrom(item.GetEntityOwner(), item))
		{
			return;
		}
		if (text == "drop")
		{
			int num = item.amount;
			if (msg.read.Unread >= 4)
			{
				num = msg.read.Int32();
			}
			if (!msg.player.isMounted && !msg.player.HasParent() && !GamePhysics.LineOfSight(msg.player.transform.position, msg.player.eyes.position, 1218519041))
			{
				return;
			}
			base.baseEntity.stats.Add("item_drop", 1, (Stats)5);
			if (num < item.amount)
			{
				Item item2 = item.SplitItem(num);
				ItemContainer parent = item.parent;
				if (item2 != null)
				{
					DroppedItem droppedItem = item2.Drop(base.baseEntity.GetDropPosition(), base.baseEntity.GetDropVelocity()) as DroppedItem;
					if (droppedItem != null)
					{
						droppedItem.DropReason = DroppedItem.DropReasonEnum.Player;
						droppedItem.DroppedBy = base.baseEntity.userID;
						droppedItem.DroppedTime = DateTime.UtcNow;
						Analytics.Azure.OnItemDropped(base.baseEntity, droppedItem, DroppedItem.DropReasonEnum.Player);
					}
				}
				parent?.onItemRemovedFromStack?.Invoke(item, num);
			}
			else
			{
				ItemContainer parent2 = item.parent;
				DroppedItem droppedItem2 = item.Drop(base.baseEntity.GetDropPosition(), base.baseEntity.GetDropVelocity()) as DroppedItem;
				if (droppedItem2 != null)
				{
					droppedItem2.DropReason = DroppedItem.DropReasonEnum.Player;
					droppedItem2.DroppedBy = base.baseEntity.userID;
					droppedItem2.DroppedTime = DateTime.UtcNow;
					Analytics.Azure.OnItemDropped(base.baseEntity, droppedItem2, DroppedItem.DropReasonEnum.Player);
				}
				parent2?.onItemAddedRemoved?.Invoke(item, arg2: false);
			}
			base.baseEntity.SignalBroadcast(BaseEntity.Signal.Gesture, "drop_item");
		}
		else
		{
			item.ServerCommand(text, base.baseEntity);
			ItemManager.DoRemoves();
			ServerUpdate(0f);
		}
	}

	[BaseEntity.RPC_Server]
	[BaseEntity.RPC_Server.FromOwner]
	public void MoveItem(BaseEntity.RPCMessage msg)
	{
		if (base.baseEntity.IsTransferring())
		{
			return;
		}
		ItemId id = msg.read.ItemID();
		ItemContainerId itemContainerId = msg.read.ItemContainerID();
		int num = msg.read.Int8();
		int num2 = (int)msg.read.UInt32();
		ItemMoveModifier itemMoveModifier = (ItemMoveModifier)msg.read.Int32();
		Item item = FindItemByUID(id);
		if (item == null)
		{
			msg.player.ShowToast(GameTip.Styles.Error, PlayerInventoryErrors.InvalidItem, false);
			ConstructionErrors.Log(msg.player, id.ToString());
			return;
		}
		BaseEntity entityOwner = item.GetEntityOwner();
		if (entityOwner != null && entityOwner == msg.player && msg.player.IsRestrainedOrSurrendering)
		{
			return;
		}
		if (!CanMoveItemsFrom(entityOwner, item))
		{
			msg.player.ShowToast(GameTip.Styles.Error, PlayerInventoryErrors.CannotMoveItem, true);
			return;
		}
		if (num2 <= 0)
		{
			num2 = item.amount;
		}
		num2 = Mathf.Clamp(num2, 1, item.MaxStackable());
		if (msg.player.GetActiveItem() == item)
		{
			msg.player.UpdateActiveItem(default(ItemId));
		}
		if (!itemContainerId.IsValid)
		{
			BaseEntity baseEntity = entityOwner;
			if (loot.containers.Count > 0)
			{
				if (entityOwner == base.baseEntity)
				{
					if (!itemMoveModifier.HasFlag(ItemMoveModifier.Alt))
					{
						baseEntity = loot.entitySource;
					}
				}
				else
				{
					baseEntity = base.baseEntity;
				}
			}
			if (baseEntity is IIdealSlotEntity idealSlotEntity)
			{
				itemContainerId = idealSlotEntity.GetIdealContainer(base.baseEntity, item, itemMoveModifier);
				if (itemContainerId == ItemContainerId.Invalid)
				{
					return;
				}
			}
			ItemContainer parent = item.parent;
			if (parent != null && parent.IsLocked())
			{
				msg.player.ShowToast(GameTip.Styles.Error, PlayerInventoryErrors.ContainerLocked, false);
				return;
			}
			if (!itemContainerId.IsValid)
			{
				if (baseEntity == loot.entitySource)
				{
					foreach (ItemContainer container in loot.containers)
					{
						if (!container.PlayerItemInputBlocked() && !container.IsLocked() && item.MoveToContainer(container, -1, allowStack: true, ignoreStackLimit: false, base.baseEntity))
						{
							break;
						}
					}
					return;
				}
				if (!GiveItem(item, itemMoveModifier))
				{
					msg.player.ShowToast(GameTip.Styles.Error, "GiveItem failed!", false);
				}
				return;
			}
		}
		ItemContainer itemContainer = FindContainer(itemContainerId);
		if (itemContainer == null)
		{
			msg.player.ShowToast(GameTip.Styles.Error, PlayerInventoryErrors.InvalidContainer, false);
			ConstructionErrors.Log(msg.player, itemContainerId.ToString());
			return;
		}
		if (itemContainer.IsLocked())
		{
			msg.player.ShowToast(GameTip.Styles.Error, PlayerInventoryErrors.ContainerLocked, false);
			return;
		}
		if (itemContainer.PlayerItemInputBlocked())
		{
			msg.player.ShowToast(GameTip.Styles.Error, PlayerInventoryErrors.DoesntAcceptPlayerItems, false);
			return;
		}
		if (itemContainer.maxStackSize > 0)
		{
			num2 = Mathf.Clamp(num2, 1, itemContainer.maxStackSize);
		}
		using (TimeWarning.New("Split"))
		{
			if (item.amount > num2)
			{
				int split_Amount = num2;
				Item item2 = item.SplitItem(split_Amount);
				Item slot = itemContainer.GetSlot(num);
				if (slot != null && !item.CanStack(slot) && item.parent != null && !item2.MoveToContainer(item.parent, -1, allowStack: false, ignoreStackLimit: false, base.baseEntity, allowSwap: false))
				{
					item.amount += item2.amount;
					item2.Remove();
					ItemManager.DoRemoves();
					ServerUpdate(0f);
					return;
				}
				if (!item2.MoveToContainer(itemContainer, num, allowStack: true, ignoreStackLimit: false, base.baseEntity))
				{
					item.amount += item2.amount;
					item2.Remove();
				}
				else
				{
					item.parent.onItemRemovedFromStack?.Invoke(item, num2);
				}
				ItemManager.DoRemoves();
				ServerUpdate(0f);
				return;
			}
		}
		if (item.MoveToContainer(itemContainer, num, allowStack: true, ignoreStackLimit: false, base.baseEntity))
		{
			ItemManager.DoRemoves();
			ServerUpdate(0f);
		}
	}

	private void OnClothingChanged(Item item, bool bAdded)
	{
		base.baseEntity.SV_ClothingChanged();
		if (ItemManager.EnablePooling)
		{
			if (!IsInvoking(DeferredServerUpdateAction))
			{
				Invoke(DeferredServerUpdateAction, 0f);
			}
		}
		else
		{
			ItemManager.DoRemoves();
			ServerUpdate(0f);
		}
		if (item.position == 7)
		{
			item.RecalulateParentEntity(children: true);
			Invoke(UpdatedVisibleHolsteredItems, 0.1f);
			item?.contents?.onItemAddedRemoved?.Invoke(item, bAdded);
		}
		base.baseEntity.ProcessMissionEvent(BaseMission.MissionEventType.CLOTHINGCHANGED, 0, 0f);
	}

	private void DeferredServerUpdate()
	{
		ServerUpdate(0f);
	}

	private void OnItemRemoved(Item item)
	{
		base.baseEntity.InvalidateNetworkCache();
	}

	private bool CanStoreInInventory(Item item, int targetSlot)
	{
		return true;
	}

	private bool CanEquipItem(Item item, int targetSlot)
	{
		if ((item.info.flags & ItemDefinition.Flag.NotAllowedInBelt) != 0)
		{
			return false;
		}
		if (base.baseEntity != null && base.baseEntity.IsRestrained)
		{
			Handcuffs restraintItem = base.baseEntity.Belt.GetRestraintItem();
			if (restraintItem != null && restraintItem.GetItem().position == targetSlot)
			{
				return false;
			}
		}
		ItemModContainerRestriction component = item.info.GetComponent<ItemModContainerRestriction>();
		if (component == null)
		{
			return true;
		}
		Item[] array = containerBelt.itemList.ToArray();
		foreach (Item item2 in array)
		{
			if (item2 != item)
			{
				ItemModContainerRestriction component2 = item2.info.GetComponent<ItemModContainerRestriction>();
				if (!(component2 == null) && !component.CanExistWith(component2) && !item2.MoveToContainer(containerMain))
				{
					item2.Drop(base.baseEntity.GetDropPosition(), base.baseEntity.GetDropVelocity());
				}
			}
		}
		return true;
	}

	private bool CanWearItem(Item item, int targetSlot)
	{
		return CanWearItem(item, canAdjustClothing: true, targetSlot);
	}

	private bool CanWearItem(Item item, bool canAdjustClothing, int targetSlot)
	{
		return WearItemCheck(item, canAdjustClothing, targetSlot).Result;
	}

	private bool CanReplaceBackpack(Item itemToWear)
	{
		Item slot = containerWear.GetSlot(7);
		if (slot == null)
		{
			return true;
		}
		ItemContainer contents = slot.contents;
		if (contents != null && contents.itemList?.Count > 0)
		{
			if (base.baseEntity.InSafeZone())
			{
				return false;
			}
			if (!itemToWear.IsDroppedInWorld(serverside: true))
			{
				return false;
			}
		}
		return true;
	}

	public void ServerUpdate(float delta)
	{
		loot.Check();
		if (delta > 0f && !base.baseEntity.IsSleeping() && !base.baseEntity.IsTransferring())
		{
			crafting.ServerUpdate(delta);
		}
		float currentTemperature = base.baseEntity.currentTemperature;
		UpdateContainer(delta, Type.Main, containerMain, bSendInventoryToEveryone: false, currentTemperature);
		UpdateContainer(delta, Type.Belt, containerBelt, bSendInventoryToEveryone: true, currentTemperature);
		UpdateContainer(delta, Type.Wear, containerWear, bSendInventoryToEveryone: true, currentTemperature);
	}

	public void UpdateContainer(float delta, Type type, ItemContainer container, bool bSendInventoryToEveryone, float temperature)
	{
		if (container != null)
		{
			container.temperature = temperature;
			if (delta > 0f)
			{
				container.OnCycle(delta);
			}
			if (container.dirty)
			{
				SendUpdatedInventory(type, container, bSendInventoryToEveryone);
				base.baseEntity.InvalidateNetworkCache();
			}
		}
	}

	public void SendSnapshot()
	{
		using (TimeWarning.New("PlayerInventory.SendSnapshot"))
		{
			SendUpdatedInventory(Type.Main, containerMain);
			SendUpdatedInventory(Type.Belt, containerBelt, bSendInventoryToEveryone: true);
			SendUpdatedInventory(Type.Wear, containerWear, bSendInventoryToEveryone: true);
		}
	}

	public void SendUpdatedInventory(Type type, ItemContainer container, bool bSendInventoryToEveryone = false)
	{
		if (type == Type.Wear)
		{
			if (bSendInventoryToEveryone)
			{
				SendUpdatedInventoryInternal(type, container, NetworkInventoryMode.LocalPlayer);
				SendUpdatedInventoryInternal(type, container, NetworkInventoryMode.EveryoneButLocal);
			}
			else
			{
				SendUpdatedInventoryInternal(type, container, NetworkInventoryMode.LocalPlayer);
			}
		}
		else
		{
			SendUpdatedInventoryInternal(type, container, bSendInventoryToEveryone ? NetworkInventoryMode.Everyone : NetworkInventoryMode.LocalPlayer);
		}
	}

	public void SendUpdatedInventoryInternal(Type type, ItemContainer container, NetworkInventoryMode mode)
	{
		using UpdateItemContainer updateItemContainer = Facepunch.Pool.Get<UpdateItemContainer>();
		updateItemContainer.type = (int)type;
		if (container != null)
		{
			container.dirty = false;
			updateItemContainer.container = Facepunch.Pool.Get<List<ProtoBuf.ItemContainer>>();
			bool bIncludeContainer = type != Type.Wear || mode == NetworkInventoryMode.LocalPlayer;
			updateItemContainer.container.Add(container.Save(bIncludeContainer));
		}
		switch (mode)
		{
		case NetworkInventoryMode.Everyone:
			base.baseEntity.ClientRPC(RpcTarget.NetworkGroup("UpdatedItemContainer"), updateItemContainer);
			break;
		case NetworkInventoryMode.LocalPlayer:
			base.baseEntity.ClientRPC(RpcTarget.Player("UpdatedItemContainer", base.baseEntity), updateItemContainer);
			break;
		case NetworkInventoryMode.EveryoneButLocal:
			if (base.baseEntity.net?.group?.subscribers == null)
			{
				break;
			}
			{
				foreach (Connection subscriber in base.baseEntity.net.group.subscribers)
				{
					if (subscriber.player is BasePlayer basePlayer && basePlayer != base.baseEntity)
					{
						base.baseEntity.ClientRPC(RpcTarget.Player("UpdatedItemContainer", basePlayer), updateItemContainer);
					}
				}
				break;
			}
		}
	}

	private WearCheckResult WearItemCheck(Item item, bool canAdjustClothing, int targetSlot, bool dontMove = false)
	{
		ItemModWearable component = item.info.GetComponent<ItemModWearable>();
		WearCheckResult result;
		if (component == null)
		{
			result = default(WearCheckResult);
			result.Result = false;
			result.ChangedItem = null;
			return result;
		}
		if (component.npcOnly && !Inventory.disableAttireLimitations)
		{
			BasePlayer basePlayer = base.baseEntity;
			if (basePlayer != null && !basePlayer.IsNpc)
			{
				result = default(WearCheckResult);
				result.Result = false;
				result.ChangedItem = null;
				return result;
			}
		}
		bool flag = item.IsBackpack();
		if (flag)
		{
			if (targetSlot != 7)
			{
				result = default(WearCheckResult);
				result.Result = false;
				result.ChangedItem = null;
				return result;
			}
			if (!CanReplaceBackpack(item))
			{
				result = default(WearCheckResult);
				result.Result = false;
				result.ChangedItem = null;
				return result;
			}
		}
		else if (!flag && targetSlot == 7)
		{
			result = default(WearCheckResult);
			result.Result = false;
			result.ChangedItem = null;
			return result;
		}
		if (item.info.GetComponent<ItemModParachute>() != null && !CanEquipParachute())
		{
			base.baseEntity.ShowToast(GameTip.Styles.Red_Normal, BackpackGroundedError, false);
			result = default(WearCheckResult);
			result.Result = false;
			result.ChangedItem = null;
			return result;
		}
		if (component.preventsMounting && base.baseEntity.isMounted)
		{
			result = default(WearCheckResult);
			result.Result = false;
			result.ChangedItem = null;
			return result;
		}
		Item[] array = containerWear.itemList.ToArray();
		foreach (Item item2 in array)
		{
			if (item2 == item)
			{
				continue;
			}
			ItemModWearable component2 = item2.info.GetComponent<ItemModWearable>();
			if (!(component2 == null) && !Inventory.disableAttireLimitations && !component.CanExistWith(component2))
			{
				if (!canAdjustClothing)
				{
					result = default(WearCheckResult);
					result.Result = false;
					result.ChangedItem = null;
					return result;
				}
				if (!dontMove && (targetSlot != item2.position || targetSlot == 7) && !item2.MoveToContainer(containerMain) && !item2.MoveToContainer(containerBelt))
				{
					item2.Drop(base.baseEntity.GetDropPosition(), base.baseEntity.GetDropVelocity());
				}
			}
		}
		result = default(WearCheckResult);
		result.Result = true;
		result.ChangedItem = returnItems;
		return result;
	}

	public Item FindItemByUID(ItemId id)
	{
		if (!id.IsValid)
		{
			return null;
		}
		if (containerMain != null)
		{
			Item item = containerMain.FindItemByUID(id);
			if (item != null && item.IsValid())
			{
				return item;
			}
		}
		if (containerBelt != null)
		{
			Item item2 = containerBelt.FindItemByUID(id);
			if (item2 != null && item2.IsValid())
			{
				return item2;
			}
		}
		if (containerWear != null)
		{
			Item item3 = containerWear.FindItemByUID(id);
			if (item3 != null && item3.IsValid())
			{
				return item3;
			}
		}
		return loot.FindItem(id);
	}

	public Item FindItemByItemID(string itemName)
	{
		ItemDefinition itemDefinition = ItemManager.FindItemDefinition(itemName);
		if (itemDefinition == null)
		{
			return null;
		}
		return FindItemByItemID(itemDefinition.itemid);
	}

	public Item FindItemByItemID(int id)
	{
		if (containerMain != null)
		{
			Item item = containerMain.FindItemByItemID(id);
			if (item != null && item.IsValid())
			{
				return item;
			}
		}
		if (containerBelt != null)
		{
			Item item2 = containerBelt.FindItemByItemID(id);
			if (item2 != null && item2.IsValid())
			{
				return item2;
			}
		}
		if (containerWear != null)
		{
			Item item3 = containerWear.FindItemByItemID(id);
			if (item3 != null && item3.IsValid())
			{
				return item3;
			}
		}
		return null;
	}

	public Item FindItemByItemName(string name)
	{
		if (containerMain != null)
		{
			Item item = containerMain.FindItemByItemName(name);
			if (item != null && item.IsValid())
			{
				return item;
			}
		}
		if (containerBelt != null)
		{
			Item item2 = containerBelt.FindItemByItemName(name);
			if (item2 != null && item2.IsValid())
			{
				return item2;
			}
		}
		if (containerWear != null)
		{
			Item item3 = containerWear.FindItemByItemName(name);
			if (item3 != null && item3.IsValid())
			{
				return item3;
			}
		}
		return null;
	}

	public Item FindBySubEntityID(NetworkableId subEntityID)
	{
		if (containerMain != null)
		{
			Item item = containerMain.FindBySubEntityID(subEntityID);
			if (item != null && item.IsValid())
			{
				return item;
			}
		}
		if (containerBelt != null)
		{
			Item item2 = containerBelt.FindBySubEntityID(subEntityID);
			if (item2 != null && item2.IsValid())
			{
				return item2;
			}
		}
		if (containerWear != null)
		{
			Item item3 = containerWear.FindBySubEntityID(subEntityID);
			if (item3 != null && item3.IsValid())
			{
				return item3;
			}
		}
		return null;
	}

	public List<Item> FindItemsByItemID(int id)
	{
		List<Item> list = new List<Item>();
		if (containerMain != null)
		{
			list.AddRange(containerMain.FindItemsByItemID(id));
		}
		if (containerBelt != null)
		{
			list.AddRange(containerBelt.FindItemsByItemID(id));
		}
		if (containerWear != null)
		{
			list.AddRange(containerWear.FindItemsByItemID(id));
		}
		return list;
	}

	public ItemContainer FindContainer(ItemContainerId id)
	{
		using (TimeWarning.New("FindContainer"))
		{
			ItemContainer itemContainer = containerMain.FindContainer(id);
			if (itemContainer != null)
			{
				return itemContainer;
			}
			itemContainer = containerBelt.FindContainer(id);
			if (itemContainer != null)
			{
				return itemContainer;
			}
			itemContainer = containerWear.FindContainer(id);
			if (itemContainer != null)
			{
				return itemContainer;
			}
			return loot.FindContainer(id);
		}
	}

	public ItemContainer GetContainer(Type id)
	{
		if (id == Type.Main)
		{
			return containerMain;
		}
		if (Type.Belt == id)
		{
			return containerBelt;
		}
		if (Type.Wear == id)
		{
			return containerWear;
		}
		if (Type.BackpackContents == id)
		{
			return GetBackpackWithInventory()?.contents;
		}
		return null;
	}

	public Item GetAnyBackpack()
	{
		return containerWear?.GetSlot(7);
	}

	public bool HasBackpackItem()
	{
		return GetAnyBackpack() != null;
	}

	public Item GetBackpackWithInventory()
	{
		Item anyBackpack = GetAnyBackpack();
		if (anyBackpack == null || anyBackpack.contents == null)
		{
			return null;
		}
		return anyBackpack;
	}

	public void DropBackpackOnDeath()
	{
		if (base.baseEntity.InSafeZone())
		{
			return;
		}
		Item anyBackpack = GetAnyBackpack();
		if (anyBackpack != null)
		{
			ItemModBackpack component = anyBackpack.info.GetComponent<ItemModBackpack>();
			if (!(component == null) && component.DropWhenDowned)
			{
				TryDropBackpack();
			}
		}
	}

	public Item GetEquippedPrisonerHoodItem()
	{
		return containerWear.FindItemByItemID(Handcuffs.PrisonerHoodItemID);
	}

	public Item GetUsableHoodItem()
	{
		return FindItemByItemID(Handcuffs.PrisonerHoodItemID);
	}

	public bool GiveItem(Item item, ItemContainer container = null)
	{
		return GiveItem(item, ItemMoveModifier.None, container);
	}

	public bool GiveItem(Item item, ItemMoveModifier modifiers, ItemContainer container = null)
	{
		bool tryWearClothing = modifiers.HasFlag(ItemMoveModifier.Alt);
		bool flag = modifiers.HasFlag(ItemMoveModifier.BackpackOpen);
		if (item == null)
		{
			return false;
		}
		if (container == null)
		{
			container = GetIdealPickupContainer(item, tryWearClothing);
		}
		if (container != null && item.MoveToContainer(container))
		{
			return true;
		}
		if (item.MoveToContainer(containerMain))
		{
			return true;
		}
		if (flag)
		{
			Item backpackWithInventory = GetBackpackWithInventory();
			if (backpackWithInventory != null && item.MoveToContainer(backpackWithInventory.contents))
			{
				return true;
			}
		}
		if (item.MoveToContainer(containerBelt))
		{
			return true;
		}
		return false;
	}

	public ItemContainer GetIdealPickupContainer(Item item, bool tryWearClothing)
	{
		if (item.MaxStackable() > 1)
		{
			if (containerBelt != null && containerBelt.FindItemByItemID(item.info.itemid) != null)
			{
				return containerBelt;
			}
			if (containerMain != null && containerMain.FindItemByItemID(item.info.itemid) != null)
			{
				return containerMain;
			}
		}
		if (item.info.isWearable && item.info.ItemModWearable.equipOnPickup && item.IsDroppedInWorld(serverside: true))
		{
			Item anyBackpack = GetAnyBackpack();
			if (anyBackpack != null && anyBackpack.GetItemVolume() > containerMain.containerVolume && item.GetItemVolume() <= containerMain.containerVolume)
			{
				if (!containerMain.IsFull())
				{
					return containerMain;
				}
				return containerBelt;
			}
			return containerWear;
		}
		if (tryWearClothing && item.info.isWearable && CanWearItem(item, canAdjustClothing: false, item.IsBackpack() ? 7 : (-1)))
		{
			return containerWear;
		}
		if (item.info.isUsable && !item.info.HasFlag(ItemDefinition.Flag.NotStraightToBelt))
		{
			return containerBelt;
		}
		return null;
	}

	public void Strip()
	{
		containerMain.Clear();
		containerBelt.Clear();
		containerWear.Clear();
		ItemManager.DoRemoves();
	}

	public static bool IsBirthday()
	{
		if (forceBirthday)
		{
			return true;
		}
		if (UnityEngine.Time.time < nextCheckTime)
		{
			return wasBirthday;
		}
		nextCheckTime = UnityEngine.Time.time + 60f;
		DateTime now = DateTime.Now;
		wasBirthday = now.Month == 12 && now.Day >= 7 && now.Day <= 16;
		return wasBirthday;
	}

	public static bool IsChristmas()
	{
		return XMas.enabled;
	}

	public void GiveDefaultItems()
	{
		Strip();
		BaseGameMode activeGameMode = BaseGameMode.GetActiveGameMode(serverside: true);
		if (activeGameMode != null && activeGameMode.HasLoadouts())
		{
			BaseGameMode.GetActiveGameMode(serverside: true).LoadoutPlayer(base.baseEntity);
			return;
		}
		GiveDefaultItemWithSkin("client.rockskin", "rock");
		GiveDefaultItemWithSkin("client.torchskin", "torch");
		if (IsBirthday() && !base.baseEntity.IsInTutorial)
		{
			GiveItem(ItemManager.CreateByName("cakefiveyear", 1, 0uL), containerBelt);
			GiveItem(ItemManager.CreateByName("partyhat", 1, 0uL), containerWear);
		}
		if (IsChristmas() && !base.baseEntity.IsInTutorial)
		{
			GiveItem(ItemManager.CreateByName("snowball", 1, 0uL), containerBelt);
			GiveItem(ItemManager.CreateByName("snowball", 1, 0uL), containerBelt);
			GiveItem(ItemManager.CreateByName("snowball", 1, 0uL), containerBelt);
		}
		void GiveDefaultItemWithSkin(string convarSkinName, string itemShortName)
		{
			ulong num = 0uL;
			int infoInt = base.baseEntity.GetInfoInt(convarSkinName, 0);
			bool flag = false;
			bool flag2 = false;
			flag2 = base.baseEntity?.UnlockAllSkins ?? false;
			if (infoInt > 0 && (base.baseEntity.blueprints.CheckSkinOwnership(infoInt, base.baseEntity.userID) || flag2))
			{
				ItemDefinition itemDefinition = ItemManager.FindItemDefinition(itemShortName);
				if (itemDefinition != null && ItemDefinition.FindSkin(itemDefinition.itemid, infoInt) != 0L)
				{
					IPlayerItemDefinition itemDefinition2 = PlatformService.Instance.GetItemDefinition(infoInt);
					if (itemDefinition2 != null)
					{
						num = itemDefinition2.WorkshopDownload;
					}
					if (num == 0L && itemDefinition.skins != null)
					{
						ItemSkinDirectory.Skin[] skins = itemDefinition.skins;
						for (int i = 0; i < skins.Length; i++)
						{
							ItemSkinDirectory.Skin skin = skins[i];
							if (skin.id == infoInt && skin.invItem != null && skin.invItem is ItemSkin itemSkin && itemSkin.Redirect != null)
							{
								GiveItem(ItemManager.CreateByName(itemSkin.Redirect.shortname, 1, 0uL), containerBelt);
								flag = true;
								break;
							}
						}
					}
				}
			}
			if (!flag)
			{
				GiveItem(ItemManager.CreateByName(itemShortName, 1, num), containerBelt);
			}
		}
	}

	private bool CanEquipParachute()
	{
		if (ConVar.Server.canEquipBackpacksInAir || Parachute.BypassRepack)
		{
			return true;
		}
		if (base.baseEntity.WaterFactor() > 0.5f)
		{
			return true;
		}
		if (!base.baseEntity.IsOnGround())
		{
			return false;
		}
		if (base.baseEntity.isMounted && (bool)base.baseEntity.GetMounted() && base.baseEntity.GetMounted().VehicleParent() is Parachute)
		{
			return false;
		}
		return true;
	}

	public ProtoBuf.PlayerInventory Save(bool bForDisk)
	{
		ProtoBuf.PlayerInventory playerInventory = Facepunch.Pool.Get<ProtoBuf.PlayerInventory>();
		if (bForDisk)
		{
			playerInventory.invMain = containerMain.Save();
		}
		playerInventory.invBelt = containerBelt.Save();
		playerInventory.invWear = containerWear.Save();
		return playerInventory;
	}

	public void Load(ProtoBuf.PlayerInventory msg)
	{
		if (msg.invMain != null)
		{
			containerMain.Load(msg.invMain);
		}
		if (msg.invBelt != null)
		{
			containerBelt.Load(msg.invBelt);
		}
		if (msg.invWear != null)
		{
			containerWear.Load(msg.invWear);
		}
		if ((bool)base.baseEntity && base.baseEntity.isServer && containerWear.capacity == 7)
		{
			containerWear.capacity = 8;
		}
	}

	public void TryDropBackpack()
	{
		Item anyBackpack = GetAnyBackpack();
		if (anyBackpack != null && base.baseEntity.isServer)
		{
			anyBackpack.Drop(base.baseEntity.GetDropPosition(), base.baseEntity.GetDropVelocity());
		}
	}

	public int Take(List<Item> collect, int itemid, int amount)
	{
		int num = 0;
		if (containerMain != null)
		{
			int num2 = containerMain.Take(collect, itemid, amount);
			num += num2;
			amount -= num2;
		}
		if (amount <= 0)
		{
			return num;
		}
		if (containerBelt != null)
		{
			int num3 = containerBelt.Take(collect, itemid, amount);
			num += num3;
			amount -= num3;
		}
		if (amount <= 0)
		{
			return num;
		}
		if (containerWear != null)
		{
			int num4 = containerWear.Take(collect, itemid, amount);
			num += num4;
			amount -= num4;
		}
		return num;
	}

	public bool HasEmptySlotInBeltOrMain()
	{
		if (containerMain != null && containerMain.capacity > containerMain.itemList.Count)
		{
			return true;
		}
		if (containerBelt != null && containerBelt.capacity > containerBelt.itemList.Count)
		{
			return true;
		}
		return false;
	}

	public bool HasEmptySlots(int requiredSlots)
	{
		int num = 0;
		if (containerMain != null)
		{
			num += containerMain.capacity - containerMain.itemList.Count;
		}
		if (containerBelt != null)
		{
			num += containerBelt.capacity - containerBelt.itemList.Count;
		}
		return num >= requiredSlots;
	}

	public int GetAmount(ItemDefinition definition)
	{
		if (!(definition != null))
		{
			return 0;
		}
		return GetAmount(definition.itemid);
	}

	public int GetAmount(int itemid)
	{
		if (itemid == 0)
		{
			return 0;
		}
		int num = 0;
		if (containerMain != null)
		{
			num += containerMain.GetAmount(itemid, onlyUsableAmounts: true);
		}
		if (containerBelt != null)
		{
			num += containerBelt.GetAmount(itemid, onlyUsableAmounts: true);
		}
		if (containerWear != null)
		{
			num += containerWear.GetAmount(itemid, onlyUsableAmounts: true);
		}
		return num;
	}

	public int GetOkConditionAmount(int itemid)
	{
		if (itemid == 0)
		{
			return 0;
		}
		int num = 0;
		if (containerMain != null)
		{
			num += containerMain.GetOkConditionAmount(itemid, onlyUsableAmounts: true);
		}
		if (containerBelt != null)
		{
			num += containerBelt.GetOkConditionAmount(itemid, onlyUsableAmounts: true);
		}
		if (containerWear != null)
		{
			num += containerWear.GetOkConditionAmount(itemid, onlyUsableAmounts: true);
		}
		return num;
	}

	public bool Contains(Item item)
	{
		bool flag = containerMain?.itemList.Contains(item) ?? false;
		if (!flag)
		{
			flag = containerBelt?.itemList.Contains(item) ?? false;
		}
		if (!flag)
		{
			flag = containerWear?.itemList.Contains(item) ?? false;
		}
		return flag;
	}

	public int GetAllItems(List<Item> items)
	{
		items.Clear();
		if (containerMain != null)
		{
			items.AddRange(containerMain.itemList);
		}
		if (containerBelt != null)
		{
			items.AddRange(containerBelt.itemList);
		}
		if (containerWear != null)
		{
			items.AddRange(containerWear.itemList);
		}
		return items.Count;
	}

	public Item FindAmmo(AmmoTypes ammoType)
	{
		Item item = containerMain?.FindAmmo(ammoType);
		if (item == null)
		{
			item = containerBelt?.FindAmmo(ammoType);
		}
		return item;
	}

	public void FindAmmo(List<Item> list, AmmoTypes ammoType)
	{
		containerMain?.FindAmmo(list, ammoType);
		containerBelt?.FindAmmo(list, ammoType);
	}

	public bool HasAmmo(AmmoTypes ammoType)
	{
		if (!containerMain.HasAmmo(ammoType))
		{
			return containerBelt.HasAmmo(ammoType);
		}
		return true;
	}

	private void OnItemRadiationChanged(Item item, float rads)
	{
		CalculateInventoryRadioactivity();
	}

	private void CalculateInventoryRadioactivity()
	{
		float num = 0f;
		if (containerMain != null)
		{
			num += containerMain.GetRadioactiveMaterialInContainer();
		}
		if (containerBelt != null)
		{
			num += containerBelt.GetRadioactiveMaterialInContainer();
		}
		if (containerWear != null)
		{
			num += containerWear.GetRadioactiveMaterialInContainer();
		}
		inventoryRadioactivity = num;
		bool arg = (containsRadioactiveItems = num > 0f);
		this.onRadioactivityChanged?.Invoke(num, arg);
	}

	private void CalculateInventoryRadioactivityCheckFast()
	{
		if (containsRadioactiveItems)
		{
			CalculateInventoryRadioactivity();
		}
	}
}
