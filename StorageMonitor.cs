using System;
using System.Collections.Generic;
using Facepunch;
using Network;
using ProtoBuf;
using UnityEngine;

public class StorageMonitor : AppIOEntity
{
	private readonly Action<Item, bool> _onItemAddedRemoved;

	private readonly Action<Item, int> _onItemAddedToStack;

	private readonly Action<Item, int> _onItemRemovedFromStack;

	private readonly Action _resetSwitchHandler;

	private double _lastPowerOnUpdate;

	public override AppEntityType Type => AppEntityType.StorageMonitor;

	public override bool Value
	{
		get
		{
			return IsOn();
		}
		set
		{
		}
	}

	public StorageMonitor()
	{
		_onItemAddedRemoved = OnItemAddedRemoved;
		_onItemAddedToStack = OnItemAddedToStack;
		_onItemRemovedFromStack = OnItemRemovedFromStack;
		_resetSwitchHandler = ResetSwitch;
	}

	internal override void FillEntityPayload(AppEntityPayload payload)
	{
		base.FillEntityPayload(payload);
		StorageContainer storageContainer = GetStorageContainer();
		if (storageContainer == null || !HasFlag(Flags.Reserved8))
		{
			return;
		}
		payload.items = Pool.Get<List<AppEntityPayload.Item>>();
		foreach (Item item2 in storageContainer.inventory.itemList)
		{
			AppEntityPayload.Item item = Pool.Get<AppEntityPayload.Item>();
			item.itemId = (item2.IsBlueprint() ? item2.blueprintTargetDef.itemid : item2.info.itemid);
			item.quantity = item2.amount;
			item.itemIsBlueprint = item2.IsBlueprint();
			payload.items.Add(item);
		}
		payload.capacity = storageContainer.inventory.capacity;
		if (storageContainer is BuildingPrivlidge buildingPrivlidge)
		{
			payload.hasProtection = true;
			float protectedMinutes = buildingPrivlidge.GetProtectedMinutes();
			if (protectedMinutes > 0f)
			{
				payload.protectionExpiry = (uint)DateTimeOffset.UtcNow.AddMinutes(protectedMinutes).ToUnixTimeSeconds();
			}
		}
	}

	public override void Init()
	{
		base.Init();
		StorageContainer storageContainer = GetStorageContainer();
		if (storageContainer != null && storageContainer.inventory != null)
		{
			ItemContainer inventory = storageContainer.inventory;
			inventory.onItemAddedRemoved = (Action<Item, bool>)Delegate.Combine(inventory.onItemAddedRemoved, _onItemAddedRemoved);
			ItemContainer inventory2 = storageContainer.inventory;
			inventory2.onItemAddedToStack = (Action<Item, int>)Delegate.Combine(inventory2.onItemAddedToStack, _onItemAddedToStack);
			ItemContainer inventory3 = storageContainer.inventory;
			inventory3.onItemRemovedFromStack = (Action<Item, int>)Delegate.Combine(inventory3.onItemRemovedFromStack, _onItemRemovedFromStack);
		}
	}

	public override void DestroyShared()
	{
		base.DestroyShared();
		StorageContainer storageContainer = GetStorageContainer();
		if (storageContainer != null && storageContainer.inventory != null)
		{
			ItemContainer inventory = storageContainer.inventory;
			inventory.onItemAddedRemoved = (Action<Item, bool>)Delegate.Remove(inventory.onItemAddedRemoved, _onItemAddedRemoved);
			ItemContainer inventory2 = storageContainer.inventory;
			inventory2.onItemAddedToStack = (Action<Item, int>)Delegate.Remove(inventory2.onItemAddedToStack, _onItemAddedToStack);
			ItemContainer inventory3 = storageContainer.inventory;
			inventory3.onItemRemovedFromStack = (Action<Item, int>)Delegate.Remove(inventory3.onItemRemovedFromStack, _onItemRemovedFromStack);
		}
	}

	private StorageContainer GetStorageContainer()
	{
		return GetParentEntity() as StorageContainer;
	}

	public override int GetPassthroughAmount(int outputSlot = 0)
	{
		switch (outputSlot)
		{
		case 0:
			if (!IsOn())
			{
				return 0;
			}
			return Mathf.Min(1, GetCurrentEnergy());
		case 1:
		{
			int num = GetCurrentEnergy();
			if (!IsOn())
			{
				return num;
			}
			return num - 1;
		}
		default:
			return 0;
		}
	}

	public override void UpdateHasPower(int inputAmount, int inputSlot)
	{
		bool flag = HasFlag(Flags.Reserved8);
		base.UpdateHasPower(inputAmount, inputSlot);
		if (inputSlot == 0)
		{
			bool num = inputAmount >= ConsumptionAmount();
			double realtimeSinceStartup = TimeEx.realtimeSinceStartup;
			if (num && !flag && _lastPowerOnUpdate < realtimeSinceStartup - 1.0)
			{
				_lastPowerOnUpdate = realtimeSinceStartup;
				BroadcastValueChange();
			}
		}
	}

	private void OnItemAddedRemoved(Item item, bool added)
	{
		OnContainerChanged();
	}

	private void OnItemAddedToStack(Item item, int amount)
	{
		OnContainerChanged();
	}

	private void OnItemRemovedFromStack(Item item, int amount)
	{
		OnContainerChanged();
	}

	private void OnContainerChanged()
	{
		if (HasFlag(Flags.Reserved8))
		{
			Invoke(_resetSwitchHandler, 0.5f);
			if (!IsOn())
			{
				SetFlag(Flags.On, b: true);
				SendNetworkUpdateImmediate();
				MarkDirty();
				BroadcastValueChange();
			}
		}
	}

	private void ResetSwitch()
	{
		SetFlag(Flags.On, b: false);
		SendNetworkUpdateImmediate();
		MarkDirty();
		BroadcastValueChange();
	}
}
