#define UNITY_ASSERTIONS
using System;
using ConVar;
using Facepunch.Rust;
using Network;
using UnityEngine;
using UnityEngine.Assertions;

public class CollectibleEntity : BaseEntity, IPrefabPreProcess
{
	public static readonly Translate.Phrase EatTitle = new Translate.Phrase("eat", "Eat");

	public Translate.Phrase itemName;

	public ItemAmount[] itemList;

	public GameObjectRef pickupEffect;

	public float xpScale = 1f;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("CollectibleEntity.OnRpcMessage"))
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
						if (!RPC_Server.MaxDistance.Test(2778075470u, "Pickup", this, player, 3f))
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
			if (rpc == 3528769075u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - PickupEat ");
				}
				using (TimeWarning.New("PickupEat"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(3528769075u, "PickupEat", this, player, 3f))
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
							RPCMessage msg3 = rPCMessage;
							PickupEat(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in PickupEat");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public bool IsFood(bool checkConsumeMod = false)
	{
		for (int i = 0; i < itemList.Length; i++)
		{
			if (itemList[i].itemDef.category == ItemCategory.Food && (!checkConsumeMod || itemList[i].itemDef.GetComponent<ItemModConsume>() != null))
			{
				return true;
			}
		}
		return false;
	}

	public void DoPickup(BasePlayer reciever, bool eat = false)
	{
		if (itemList == null)
		{
			return;
		}
		ItemAmount[] array = itemList;
		foreach (ItemAmount itemAmount in array)
		{
			if (reciever != null && reciever.IsInTutorial && itemAmount.ignoreInTutorial)
			{
				continue;
			}
			Item item = ItemManager.Create(itemAmount.itemDef, (int)itemAmount.amount, 0uL);
			if (item == null)
			{
				continue;
			}
			if (eat && item.info.category == ItemCategory.Food && reciever != null)
			{
				ItemModConsume component = item.info.GetComponent<ItemModConsume>();
				if (component != null)
				{
					component.DoAction(item, reciever);
					continue;
				}
			}
			if ((bool)reciever)
			{
				Analytics.Azure.OnGatherItem(item.info.shortname, item.amount, this, reciever);
				reciever.GiveItem(item, GiveItemReason.ResourceHarvested);
			}
			else
			{
				item.Drop(base.transform.position + Vector3.up * 0.5f, Vector3.up);
			}
		}
		itemList = null;
		if (pickupEffect.isValid)
		{
			Effect.server.Run(pickupEffect.resourcePath, base.transform.position, base.transform.up);
		}
		RandomItemDispenser randomItemDispenser = PrefabAttribute.server.Find<RandomItemDispenser>(prefabID);
		if (randomItemDispenser != null)
		{
			randomItemDispenser.DistributeItems(reciever, base.transform.position);
		}
		Kill();
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	public void Pickup(RPCMessage msg)
	{
		if (msg.player.CanInteract())
		{
			DoPickup(msg.player);
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	public void PickupEat(RPCMessage msg)
	{
		if (msg.player.CanInteract())
		{
			DoPickup(msg.player, eat: true);
		}
	}

	public bool HasItem(ItemDefinition def)
	{
		ItemAmount[] array = itemList;
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].itemDef == def)
			{
				return true;
			}
		}
		return false;
	}

	public override void PreProcess(IPrefabProcessor preProcess, GameObject rootObj, string name, bool serverside, bool clientside, bool bundling)
	{
		base.PreProcess(preProcess, rootObj, name, serverside, clientside, bundling);
		if (serverside)
		{
			preProcess.RemoveComponent(GetComponent<Collider>());
		}
	}
}
