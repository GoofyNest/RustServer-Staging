#define UNITY_ASSERTIONS
using System;
using ConVar;
using Facepunch;
using Network;
using ProtoBuf;
using UnityEngine;
using UnityEngine.Assertions;

public class ContainerIOEntity : IOEntity, IItemContainerEntity, IIdealSlotEntity, ILootableEntity, LootPanel.IHasLootPanel, IContainerSounds
{
	public ItemDefinition onlyAllowedItem;

	public ItemContainer.ContentsType allowedContents = ItemContainer.ContentsType.Generic;

	public int maxStackSize = 1;

	public int numSlots;

	public string lootPanelName = "generic";

	public Translate.Phrase panelTitle = new Translate.Phrase("loot", "Loot");

	public bool needsBuildingPrivilegeToUse;

	public bool isLootable = true;

	public bool dropsLoot;

	public bool dropFloats;

	public bool onlyOneUser;

	public SoundDefinition openSound;

	public SoundDefinition closeSound;

	private ItemContainer _inventory;

	public Translate.Phrase LootPanelTitle => panelTitle;

	public ItemContainer inventory => _inventory;

	public Transform Transform => base.transform;

	public bool DropsLoot => dropsLoot;

	public bool DropFloats => dropFloats;

	public float DestroyLootPercent => 0f;

	public ulong LastLootedBy { get; set; }

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("ContainerIOEntity.OnRpcMessage"))
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

	public override bool CanPickup(BasePlayer player)
	{
		if (!pickup.requireEmptyInv || _inventory == null || _inventory.itemList.Count == 0)
		{
			return base.CanPickup(player);
		}
		return false;
	}

	public override void ServerInit()
	{
		if (_inventory == null)
		{
			CreateInventory(giveUID: true);
			OnInventoryFirstCreated(_inventory);
		}
		base.ServerInit();
	}

	internal override void DoServerDestroy()
	{
		base.DoServerDestroy();
		Facepunch.Pool.Free(ref _inventory);
	}

	public override void PreServerLoad()
	{
		base.PreServerLoad();
		CreateInventory(giveUID: false);
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

	public void CreateInventory(bool giveUID)
	{
		Debug.Assert(_inventory == null, "Double init of inventory!");
		_inventory = Facepunch.Pool.Get<ItemContainer>();
		_inventory.entityOwner = this;
		_inventory.allowedContents = ((allowedContents == (ItemContainer.ContentsType)0) ? ItemContainer.ContentsType.Generic : allowedContents);
		_inventory.SetOnlyAllowedItem(onlyAllowedItem);
		_inventory.maxStackSize = maxStackSize;
		_inventory.ServerInitialize(null, numSlots);
		if (giveUID)
		{
			_inventory.GiveUID();
		}
		_inventory.onItemAddedRemoved = OnItemAddedOrRemoved;
		_inventory.onDirty += OnInventoryDirty;
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

	public virtual void OnInventoryFirstCreated(ItemContainer container)
	{
	}

	public virtual void OnItemAddedOrRemoved(Item item, bool added)
	{
	}

	protected virtual void OnInventoryDirty()
	{
	}

	public override void OnKilled(HitInfo info)
	{
		DropItems();
		base.OnKilled(info);
	}

	public void DropItems(BaseEntity initiator = null)
	{
		StorageContainer.DropItems(this, initiator);
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	private void RPC_OpenLoot(RPCMessage rpc)
	{
		if (_inventory != null)
		{
			BasePlayer player = rpc.player;
			if ((bool)player && player.CanInteract())
			{
				PlayerOpenLoot(player, lootPanelName);
			}
		}
	}

	public virtual bool PlayerOpenLoot(BasePlayer player, string panelToOpen = "", bool doPositionChecks = true)
	{
		if (needsBuildingPrivilegeToUse && !player.CanBuild())
		{
			return false;
		}
		if ((onlyOneUser && IsOpen()) || IsTransferring())
		{
			player.ShowToast(GameTip.Styles.Red_Normal, StorageContainer.LockedMessage, false);
			return false;
		}
		if (panelToOpen == "")
		{
			panelToOpen = lootPanelName;
		}
		if (player.inventory.loot.StartLootingEntity(this, doPositionChecks))
		{
			SetFlag(Flags.Open, b: true);
			player.inventory.loot.AddContainer(_inventory);
			player.inventory.loot.SendImmediate();
			player.ClientRPC(RpcTarget.Player("RPC_OpenLootPanel", player), lootPanelName);
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

	public bool ShouldDropItemsIndividually()
	{
		return false;
	}

	public virtual int GetIdealSlot(BasePlayer player, ItemContainer container, Item item)
	{
		return -1;
	}

	public virtual ItemContainerId GetIdealContainer(BasePlayer player, Item item, ItemMoveModifier modifier)
	{
		return default(ItemContainerId);
	}

	public virtual void DropBonusItems(BaseEntity initiator, ItemContainer container)
	{
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

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.fromDisk && info.msg.storageBox != null)
		{
			if (_inventory != null)
			{
				_inventory.Load(info.msg.storageBox.contents);
				_inventory.capacity = numSlots;
			}
			else
			{
				Debug.LogWarning("Storage container without inventory: " + ToString());
			}
		}
	}
}
