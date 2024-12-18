#define UNITY_ASSERTIONS
using System;
using ConVar;
using Facepunch;
using Network;
using ProtoBuf;
using UnityEngine;
using UnityEngine.Assertions;

public class Mailbox : StorageContainer
{
	public string ownerPanel;

	public GameObjectRef mailDropSound;

	public ItemDefinition[] allowedItems;

	public bool autoSubmitWhenClosed;

	public bool shouldMarkAsFull;

	public int InputSlotCount = 1;

	[NonSerialized]
	public ItemContainer InputContainer;

	public int mailInputSlot => 0;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("Mailbox.OnRpcMessage"))
		{
			if (rpc == 131727457 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_Submit ");
				}
				using (TimeWarning.New("RPC_Submit"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg2 = rPCMessage;
							RPC_Submit(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_Submit");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public virtual bool PlayerIsOwner(BasePlayer player)
	{
		return player.CanBuild();
	}

	public bool IsFull()
	{
		if (shouldMarkAsFull)
		{
			return HasFlag(Flags.Reserved1);
		}
		return false;
	}

	public void MarkFull(bool full)
	{
		SetFlag(Flags.Reserved1, shouldMarkAsFull && full);
	}

	public override bool PlayerOpenLoot(BasePlayer player, string panelToOpen = "", bool doPositionChecks = true)
	{
		return base.PlayerOpenLoot(player, PlayerIsOwner(player) ? ownerPanel : panelToOpen);
	}

	public override void AddContainers(PlayerLoot loot)
	{
		if (PlayerIsOwner(loot.GetCastedEntity()))
		{
			loot.AddContainer(base.inventory);
		}
		else
		{
			loot.AddContainer(InputContainer);
		}
	}

	public override bool CanOpenLootPanel(BasePlayer player, string panelName)
	{
		if (panelName == ownerPanel)
		{
			if (PlayerIsOwner(player))
			{
				return base.CanOpenLootPanel(player, panelName);
			}
			return false;
		}
		if (!HasFreeSpace())
		{
			return !shouldMarkAsFull;
		}
		return true;
	}

	private bool HasFreeSpace()
	{
		return !base.inventory.IsFull();
	}

	public override void PlayerStoppedLooting(BasePlayer player)
	{
		if (autoSubmitWhenClosed)
		{
			SubmitInputItems(player);
		}
		if (IsFull())
		{
			base.inventory.GetSlot(mailInputSlot)?.Drop(GetDropPosition(), GetDropVelocity());
		}
		base.PlayerStoppedLooting(player);
		if (PlayerIsOwner(player))
		{
			SetFlag(Flags.On, b: false);
		}
	}

	[RPC_Server]
	public void RPC_Submit(RPCMessage msg)
	{
		if (!IsFull())
		{
			BasePlayer player = msg.player;
			SubmitInputItems(player);
		}
	}

	public void SubmitInputItems(BasePlayer fromPlayer)
	{
		for (int i = 0; i < InputContainer.capacity; i++)
		{
			Item slot = InputContainer.GetSlot(i);
			if (slot != null && slot.MoveToContainer(base.inventory))
			{
				Effect.server.Run(mailDropSound.resourcePath, GetDropPosition());
				if (fromPlayer != null && !PlayerIsOwner(fromPlayer))
				{
					SetFlag(Flags.On, b: true);
				}
			}
		}
	}

	public override void OnItemAddedOrRemoved(Item item, bool added)
	{
		MarkFull(!HasFreeSpace());
		base.OnItemAddedOrRemoved(item, added);
	}

	public override bool ItemFilter(Item item, int targetSlot)
	{
		if (allowedItems == null || allowedItems.Length == 0)
		{
			return base.ItemFilter(item, targetSlot);
		}
		ItemDefinition[] array = allowedItems;
		foreach (ItemDefinition itemDefinition in array)
		{
			if (item.info == itemDefinition)
			{
				return true;
			}
		}
		return false;
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		ProtoBuf.Mailbox mailbox = Facepunch.Pool.Get<ProtoBuf.Mailbox>();
		mailbox.inventory = InputContainer.Save();
		info.msg.mailbox = mailbox;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (base.isServer && info.msg.mailbox != null && info.msg.mailbox.inventory != null)
		{
			InputContainer.Load(info.msg.mailbox.inventory);
		}
	}

	public override void ServerInit()
	{
		base.ServerInit();
		if (InputContainer == null)
		{
			InputContainer = Facepunch.Pool.Get<ItemContainer>();
			InputContainer.allowedContents = ((allowedContents == (ItemContainer.ContentsType)0) ? ItemContainer.ContentsType.Generic : allowedContents);
			InputContainer.SetOnlyAllowedItem(allowedItem);
			InputContainer.entityOwner = this;
			InputContainer.maxStackSize = maxStackSize;
			InputContainer.ServerInitialize(null, InputSlotCount);
			InputContainer.GiveUID();
			InputContainer.onDirty += OnInventoryDirty;
			InputContainer.onItemAddedRemoved = OnItemAddedOrRemoved;
			ItemContainer inputContainer = InputContainer;
			inputContainer.canAcceptItem = (Func<Item, int, bool>)Delegate.Combine(inputContainer.canAcceptItem, new Func<Item, int, bool>(ItemFilter));
			OnInventoryFirstCreated(InputContainer);
		}
	}
}
