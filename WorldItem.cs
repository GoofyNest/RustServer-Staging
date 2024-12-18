#define UNITY_ASSERTIONS
using System;
using ConVar;
using Facepunch;
using Facepunch.Rust;
using Network;
using ProtoBuf;
using UnityEngine;
using UnityEngine.Assertions;

public class WorldItem : BaseEntity, PlayerInventory.ICanMoveFrom
{
	public static readonly Translate.Phrase OpenLootTitle = new Translate.Phrase("open_loot", "Open");

	public static readonly Translate.Phrase PickUpTitle = new Translate.Phrase("pick_up", "Pick Up");

	public static readonly Translate.Phrase HoldToPickupPhrase = new Translate.Phrase("hold_use_to_pickup", "Hold [USE] to pickup");

	[Header("WorldItem")]
	public bool allowPickup = true;

	[NonSerialized]
	public Item item;

	private bool _isInvokingSendItemUpdate;

	protected float eatSeconds = 10f;

	protected float caloriesPerSecond = 1f;

	public override TraitFlag Traits
	{
		get
		{
			if (item != null)
			{
				return item.Traits;
			}
			return base.Traits;
		}
	}

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("WorldItem.OnRpcMessage"))
		{
			if (rpc == 2778075470u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Pickup ");
				}
				using (TimeWarning.New("Pickup"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(2778075470u, "Pickup", this, player, 3f))
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
							RPCMessage msg2 = rPCMessage;
							Pickup(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in Pickup");
					}
				}
				return true;
			}
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
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in RPC_OpenLoot");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override Item GetItem()
	{
		return item;
	}

	public void InitializeItem(Item in_item)
	{
		if (item != null)
		{
			RemoveItem();
		}
		item = in_item;
		if (item != null)
		{
			item.OnDirty += OnItemDirty;
			base.name = item.info.shortname + " (world)";
			item.SetWorldEntity(this);
			OnItemDirty(item);
			if (base.isServer)
			{
				SingletonComponent<NpcFoodManager>.Instance.Add(this);
			}
		}
	}

	public void RemoveItem()
	{
		if (item != null)
		{
			if (base.isServer)
			{
				SingletonComponent<NpcFoodManager>.Instance.Remove(this);
			}
			item.OnDirty -= OnItemDirty;
			item = null;
		}
	}

	public void DestroyItem()
	{
		if (item != null)
		{
			if (base.isServer)
			{
				SingletonComponent<NpcFoodManager>.Instance.Remove(this);
			}
			item.OnDirty -= OnItemDirty;
			item.Remove();
			item = null;
		}
	}

	protected virtual void OnItemDirty(Item in_item)
	{
		Assert.IsTrue(item == in_item, "WorldItem:OnItemDirty - dirty item isn't ours!");
		if (item != null)
		{
			BroadcastMessage("OnItemChanged", item, SendMessageOptions.DontRequireReceiver);
		}
		DoItemNetworking();
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.worldItem != null && info.msg.worldItem.item != null)
		{
			Item item = ItemManager.Load(info.msg.worldItem.item, this.item, base.isServer);
			if (item != null)
			{
				InitializeItem(item);
			}
		}
	}

	public override string ToString()
	{
		if (_name == null)
		{
			if (base.isServer)
			{
				_name = string.Format("{1}[{0}] {2}", net?.ID ?? default(NetworkableId), base.ShortPrefabName, this.IsUnityNull() ? "NULL" : base.name);
			}
			else
			{
				_name = base.ShortPrefabName;
			}
		}
		return _name;
	}

	public bool CanMoveFrom(BasePlayer player, Item item)
	{
		if (item?.info.GetComponent<ItemModBackpack>() == null)
		{
			return true;
		}
		return item.parentItem?.parent == player.inventory.containerWear;
	}

	public override void ServerInit()
	{
		base.ServerInit();
		if (item != null)
		{
			BroadcastMessage("OnItemChanged", item, SendMessageOptions.DontRequireReceiver);
		}
	}

	private void DoItemNetworking()
	{
		if (!_isInvokingSendItemUpdate)
		{
			_isInvokingSendItemUpdate = true;
			Invoke(SendItemUpdate, 0.1f);
		}
	}

	private void SendItemUpdate()
	{
		_isInvokingSendItemUpdate = false;
		if (item == null)
		{
			return;
		}
		using UpdateItem updateItem = Facepunch.Pool.Get<UpdateItem>();
		updateItem.item = item.Save(bIncludeContainer: false, bIncludeOwners: false);
		ClientRPC(RpcTarget.NetworkGroup("UpdateItem"), updateItem);
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void Pickup(RPCMessage msg)
	{
		if (msg.player.CanInteract() && this.item != null && allowPickup && CanOpenInSafeZone(msg.player))
		{
			ClientRPC(RpcTarget.NetworkGroup("PickupSound"));
			Item item = this.item;
			Analytics.Azure.OnItemPickup(msg.player, this);
			RemoveItem();
			msg.player.GiveItem(item, GiveItemReason.PickedUp);
			msg.player.SignalBroadcast(Signal.Gesture, "pickup_item");
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		if (item != null)
		{
			bool forDisk = info.forDisk;
			info.msg.worldItem = Facepunch.Pool.Get<ProtoBuf.WorldItem>();
			info.msg.worldItem.item = item.Save(forDisk, bIncludeOwners: false);
		}
	}

	internal override void DoServerDestroy()
	{
		base.DoServerDestroy();
		DestroyItem();
	}

	public override void SwitchParent(BaseEntity ent)
	{
		SetParent(ent, parentBone);
	}

	public override void Eat(BaseNpc baseNpc, float timeSpent)
	{
		if (!(eatSeconds <= 0f))
		{
			eatSeconds -= timeSpent;
			baseNpc.AddCalories(caloriesPerSecond * timeSpent);
			if (eatSeconds < 0f)
			{
				DestroyItem();
				Kill();
			}
		}
	}

	private bool CanOpenInSafeZone(BasePlayer looter)
	{
		if (item == null || !item.info.blockStealingInSafeZone)
		{
			return true;
		}
		if (!(this is DroppedItem droppedItem))
		{
			return true;
		}
		if (looter.InSafeZone() && droppedItem.DroppedBy != (ulong)looter.userID && droppedItem.DroppedBy != 0L)
		{
			return false;
		}
		return true;
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	private void RPC_OpenLoot(RPCMessage rpc)
	{
		if (item == null || item.contents == null)
		{
			return;
		}
		ItemModContainer component = item.info.GetComponent<ItemModContainer>();
		if (!(component == null) && component.canLootInWorld)
		{
			BasePlayer player = rpc.player;
			if ((bool)player && player.CanInteract() && CanOpenInSafeZone(player) && player.inventory.loot.StartLootingEntity(this))
			{
				SetFlag(Flags.Open, b: true);
				player.inventory.loot.AddContainer(item.contents);
				player.inventory.loot.SendImmediate();
				player.ClientRPC(RpcTarget.Player("RPC_OpenLootPanel", player), "generic_resizable");
				SendNetworkUpdate();
			}
		}
	}
}
