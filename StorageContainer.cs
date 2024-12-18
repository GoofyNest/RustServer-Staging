#define UNITY_ASSERTIONS
using System;
using ConVar;
using Facepunch;
using Network;
using ProtoBuf;
using UnityEngine;
using UnityEngine.Assertions;

public class StorageContainer : DecayEntity, IItemContainerEntity, IIdealSlotEntity, ILootableEntity, LootPanel.IHasLootPanel, IContainerSounds, PlayerInventory.ICanMoveFrom
{
	[Header("Storage Container")]
	public static readonly Translate.Phrase LockedMessage = new Translate.Phrase("storage.locked", "Can't loot right now");

	public static readonly Translate.Phrase InUseMessage = new Translate.Phrase("storage.in_use", "Already in use");

	public int inventorySlots = 6;

	public bool dropsLoot = true;

	public float dropLootDestroyPercent;

	public bool dropFloats;

	public bool isLootable = true;

	public bool isLockable = true;

	public bool isMonitorable;

	public string panelName = "generic";

	public Translate.Phrase panelTitle = new Translate.Phrase("loot", "Loot");

	public ItemContainer.ContentsType allowedContents;

	public ItemDefinition allowedItem;

	public ItemDefinition allowedItem2;

	public ItemDefinition[] blockedItems;

	public int maxStackSize;

	public bool needsBuildingPrivilegeToUse;

	public bool mustBeMountedToUse;

	public SoundDefinition openSound;

	public SoundDefinition closeSound;

	[Header("Item Dropping")]
	public Vector3 dropPosition;

	public Vector3 dropVelocity = Vector3.forward;

	public ItemCategory onlyAcceptCategory = ItemCategory.All;

	public bool onlyOneUser;

	private ItemContainer _inventory;

	public Translate.Phrase LootPanelTitle => panelTitle;

	public ItemContainer inventory => _inventory;

	public Transform Transform => base.transform;

	public bool DropsLoot => dropsLoot;

	public bool DropFloats => dropFloats;

	public float DestroyLootPercent => dropLootDestroyPercent;

	public ulong LastLootedBy { get; set; }

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("StorageContainer.OnRpcMessage"))
		{
			if (rpc == 331989034 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_OpenLoot ");
				}
				using (TimeWarning.New("RPC_OpenLoot"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(331989034u, "RPC_OpenLoot", this, player, 3f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage rpc2 = rPCMessage;
							RPC_OpenLoot(rpc2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_OpenLoot");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public virtual void OnDrawGizmos()
	{
		Gizmos.matrix = base.transform.localToWorldMatrix;
		Gizmos.color = Color.yellow;
		Gizmos.DrawCube(dropPosition, Vector3.one * 0.1f);
		Gizmos.color = Color.white;
		Gizmos.DrawRay(dropPosition, dropVelocity);
	}

	public bool MoveAllInventoryItems(ItemContainer from)
	{
		return MoveAllInventoryItems(from, _inventory);
	}

	public static bool MoveAllInventoryItems(ItemContainer source, ItemContainer dest)
	{
		bool flag = true;
		for (int i = 0; i < Mathf.Min(source.capacity, dest.capacity); i++)
		{
			Item slot = source.GetSlot(i);
			if (slot != null)
			{
				bool flag2 = slot.MoveToContainer(dest);
				if (flag && !flag2)
				{
					flag = false;
				}
			}
		}
		return flag;
	}

	public virtual void ReceiveInventoryFromItem(Item item)
	{
		if (item.contents != null)
		{
			MoveAllInventoryItems(item.contents, _inventory);
		}
	}

	public override bool CanPickup(BasePlayer player)
	{
		bool flag = GetSlot(Slot.Lock) != null;
		if (base.isClient)
		{
			if (base.CanPickup(player))
			{
				return !flag;
			}
			return false;
		}
		if ((!pickup.requireEmptyInv || _inventory == null || _inventory.itemList.Count == 0) && base.CanPickup(player))
		{
			return !flag;
		}
		return false;
	}

	public override void OnPickedUp(Item createdItem, BasePlayer player)
	{
		base.OnPickedUp(createdItem, player);
		if (createdItem != null && createdItem.contents != null)
		{
			MoveAllInventoryItems(_inventory, createdItem.contents);
			return;
		}
		for (int i = 0; i < _inventory.capacity; i++)
		{
			Item slot = _inventory.GetSlot(i);
			if (slot != null)
			{
				slot.RemoveFromContainer();
				player.GiveItem(slot, GiveItemReason.PickedUp);
			}
		}
	}

	public override void ServerInit()
	{
		if (_inventory == null)
		{
			CreateInventory(giveUID: true);
			OnInventoryFirstCreated(_inventory);
		}
		else
		{
			_inventory.SetBlacklist(blockedItems);
		}
		base.ServerInit();
	}

	public virtual void OnInventoryFirstCreated(ItemContainer container)
	{
	}

	public virtual void OnItemAddedOrRemoved(Item item, bool added)
	{
	}

	public virtual bool ItemFilter(Item item, int targetSlot)
	{
		if (onlyAcceptCategory == ItemCategory.All)
		{
			return true;
		}
		return item.info.category == onlyAcceptCategory;
	}

	public void CreateInventory(bool giveUID)
	{
		Debug.Assert(_inventory == null, "Double init of inventory!");
		_inventory = Facepunch.Pool.Get<ItemContainer>();
		_inventory.entityOwner = this;
		_inventory.allowedContents = ((allowedContents == (ItemContainer.ContentsType)0) ? ItemContainer.ContentsType.Generic : allowedContents);
		_inventory.SetOnlyAllowedItems(allowedItem, allowedItem2);
		_inventory.SetBlacklist(blockedItems);
		_inventory.maxStackSize = maxStackSize;
		_inventory.ServerInitialize(null, inventorySlots);
		if (giveUID)
		{
			_inventory.GiveUID();
		}
		_inventory.onDirty += OnInventoryDirty;
		_inventory.onItemAddedRemoved = OnItemAddedOrRemoved;
		_inventory.canAcceptItem = ItemFilter;
	}

	public override void PreServerLoad()
	{
		base.PreServerLoad();
		CreateInventory(giveUID: false);
	}

	protected virtual void OnInventoryDirty()
	{
		InvalidateNetworkCache();
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		if (_inventory != null && !_inventory.uid.IsValid)
		{
			_inventory.GiveUID();
		}
		SetFlag(Flags.Open, b: false);
	}

	internal override void DoServerDestroy()
	{
		base.DoServerDestroy();
		if (_inventory != null)
		{
			Facepunch.Pool.Free(ref _inventory);
		}
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	private void RPC_OpenLoot(RPCMessage rpc)
	{
		if (isLootable)
		{
			BasePlayer player = rpc.player;
			if ((bool)player && player.CanInteract())
			{
				PlayerOpenLoot(player);
			}
		}
	}

	public virtual string GetPanelName()
	{
		return panelName;
	}

	public virtual bool CanMoveFrom(BasePlayer player, Item item)
	{
		return !_inventory.IsLocked();
	}

	public virtual bool CanOpenLootPanel(BasePlayer player, string panelName)
	{
		if (!CanBeLooted(player))
		{
			return false;
		}
		BaseLock baseLock = GetSlot(Slot.Lock) as BaseLock;
		if (baseLock != null && !baseLock.OnTryToOpen(player))
		{
			player.ShowToast(GameTip.Styles.Error, PlayerInventoryErrors.ContainerLocked, false);
			return false;
		}
		return true;
	}

	public override bool CanBeLooted(BasePlayer player)
	{
		if (needsBuildingPrivilegeToUse && !player.CanBuild())
		{
			return false;
		}
		if (mustBeMountedToUse && !player.isMounted)
		{
			return false;
		}
		return base.CanBeLooted(player);
	}

	public virtual void AddContainers(PlayerLoot loot)
	{
		loot.AddContainer(_inventory);
	}

	public virtual bool PlayerOpenLoot(BasePlayer player, string panelToOpen = "", bool doPositionChecks = true)
	{
		if (IsLocked() || IsTransferring())
		{
			player.ShowToast(GameTip.Styles.Red_Normal, LockedMessage, false);
			return false;
		}
		if (onlyOneUser && IsOpen())
		{
			player.ShowToast(GameTip.Styles.Red_Normal, InUseMessage, false);
			return false;
		}
		if (panelToOpen == "")
		{
			panelToOpen = panelName;
		}
		if (!CanOpenLootPanel(player, panelToOpen))
		{
			return false;
		}
		if (player.inventory.loot.StartLootingEntity(this, doPositionChecks))
		{
			SetFlag(Flags.Open, b: true);
			AddContainers(player.inventory.loot);
			player.inventory.loot.SendImmediate();
			player.ClientRPC(RpcTarget.Player("RPC_OpenLootPanel", player), panelToOpen);
			SendNetworkUpdate();
			return true;
		}
		return false;
	}

	public virtual void PlayerStoppedLooting(BasePlayer player)
	{
		SetFlag(Flags.Open, b: false);
		SendNetworkUpdate();
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		if (info.forDisk)
		{
			if (_inventory != null)
			{
				info.msg.storageBox = Facepunch.Pool.Get<StorageBox>();
				info.msg.storageBox.contents = _inventory.Save();
			}
			else
			{
				Debug.LogWarning("Storage container without inventory: " + ToString());
			}
		}
	}

	public override void OnKilled(HitInfo info)
	{
		DropItems(info?.Initiator);
		base.OnKilled(info);
	}

	public void DropItems(BaseEntity initiator = null)
	{
		DropItems(this, initiator);
	}

	public static void DropItems(IItemContainerEntity containerEntity, BaseEntity initiator = null)
	{
		ItemContainer itemContainer = containerEntity.inventory;
		if (itemContainer == null || itemContainer.itemList == null || itemContainer.itemList.Count == 0 || !containerEntity.DropsLoot)
		{
			return;
		}
		if (containerEntity.ShouldDropItemsIndividually() || (itemContainer.itemList.Count == 1 && !containerEntity.DropFloats))
		{
			if (initiator != null)
			{
				containerEntity.DropBonusItems(initiator, itemContainer);
			}
			DropUtil.DropItems(itemContainer, containerEntity.GetDropPosition());
		}
		else
		{
			string prefab = (containerEntity.DropFloats ? "assets/prefabs/misc/item drop/item_drop_buoyant.prefab" : "assets/prefabs/misc/item drop/item_drop.prefab");
			_ = itemContainer.Drop(prefab, containerEntity.GetDropPosition(), containerEntity.Transform.rotation, containerEntity.DestroyLootPercent) != null;
		}
	}

	public virtual void DropBonusItems(BaseEntity initiator, ItemContainer container)
	{
	}

	public override Vector3 GetDropPosition()
	{
		return base.transform.localToWorldMatrix.MultiplyPoint(dropPosition);
	}

	public override Vector3 GetDropVelocity()
	{
		return GetInheritedDropVelocity() + base.transform.localToWorldMatrix.MultiplyVector(dropPosition);
	}

	public virtual bool ShouldDropItemsIndividually()
	{
		return false;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.storageBox != null)
		{
			if (_inventory != null)
			{
				_inventory.Load(info.msg.storageBox.contents);
				_inventory.capacity = inventorySlots;
			}
			else
			{
				Debug.LogWarning("Storage container without inventory: " + ToString());
			}
		}
	}

	public virtual int GetIdealSlot(BasePlayer player, ItemContainer container, Item item)
	{
		return -1;
	}

	public virtual ItemContainerId GetIdealContainer(BasePlayer player, Item item, ItemMoveModifier modifier)
	{
		return default(ItemContainerId);
	}

	public override bool HasSlot(Slot slot)
	{
		if (isLockable && slot == Slot.Lock)
		{
			return true;
		}
		if (isMonitorable && slot == Slot.StorageMonitor)
		{
			return true;
		}
		return base.HasSlot(slot);
	}

	public bool OccupiedCheck(BasePlayer player = null)
	{
		if (player != null && player.inventory.loot.entitySource == this)
		{
			return true;
		}
		if (onlyOneUser)
		{
			return !IsOpen();
		}
		return true;
	}

	protected bool HasAttachedStorageAdaptor()
	{
		if (children == null)
		{
			return false;
		}
		foreach (BaseEntity child in children)
		{
			if (child is IndustrialStorageAdaptor)
			{
				return true;
			}
		}
		return false;
	}
}
