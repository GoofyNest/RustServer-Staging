using System;
using Facepunch;
using UnityEngine;

namespace Rust.Modular;

public class ModularVehicleInventory : IDisposable
{
	private ItemContainer moduleContainer;

	private ItemContainer chassisContainer;

	private readonly BaseModularVehicle vehicle;

	public ItemContainer ModuleContainer => moduleContainer;

	public ItemContainer ChassisContainer => chassisContainer;

	public ItemContainerId UID => moduleContainer.uid;

	private int TotalSockets => vehicle.TotalSockets;

	public ModularVehicleInventory(BaseModularVehicle vehicle, ItemDefinition chassisItemDef, bool giveUID)
	{
		this.vehicle = vehicle;
		moduleContainer = CreateModuleInventory(vehicle, giveUID);
		chassisContainer = CreateChassisInventory(vehicle, giveUID);
		vehicle.AssociatedItemInstance = ItemManager.Create(chassisItemDef, 1, 0uL);
		if (!Application.isLoadingSave)
		{
			vehicle.AssociatedItemInstance.MoveToContainer(chassisContainer, 0, allowStack: false);
		}
	}

	public void Dispose()
	{
		foreach (Item item in moduleContainer.itemList)
		{
			item.OnDirty -= OnModuleItemChanged;
		}
		Pool.Free(ref moduleContainer);
		Pool.Free(ref chassisContainer);
	}

	public void GiveUIDs()
	{
		moduleContainer.GiveUID();
		chassisContainer.GiveUID();
	}

	public bool SocketIsFree(int socketIndex, Item moduleItem = null)
	{
		Item item = null;
		int num = socketIndex;
		while (item == null && num >= 0)
		{
			item = moduleContainer.GetSlot(num);
			if (item != null)
			{
				if (item == moduleItem)
				{
					return true;
				}
				ItemModVehicleModule component = item.info.GetComponent<ItemModVehicleModule>();
				return num + component.socketsTaken - 1 < socketIndex;
			}
			num--;
		}
		return true;
	}

	public bool SocketIsTaken(int socketIndex)
	{
		return !SocketIsFree(socketIndex);
	}

	public bool TryAddModuleItem(Item moduleItem, int socketIndex)
	{
		if (moduleItem == null)
		{
			Debug.LogError(GetType().Name + ": Can't add null item.");
			return false;
		}
		return moduleItem.MoveToContainer(moduleContainer, socketIndex, allowStack: false);
	}

	public bool RemoveAndDestroy(Item itemToRemove)
	{
		bool result = moduleContainer.Remove(itemToRemove);
		itemToRemove.Remove();
		return result;
	}

	public int TryGetFreeSocket(int socketsTaken)
	{
		return TryGetFreeSocket(null, socketsTaken);
	}

	public int TryGetFreeSocket(Item moduleItem, int socketsTaken)
	{
		for (int i = 0; i <= TotalSockets - socketsTaken; i++)
		{
			if (SocketsAreFree(i, socketsTaken, moduleItem))
			{
				return i;
			}
		}
		return -1;
	}

	public bool SocketsAreFree(int firstIndex, int socketsTaken, Item moduleItem = null)
	{
		if (firstIndex < 0 || firstIndex + socketsTaken > TotalSockets)
		{
			return false;
		}
		for (int i = firstIndex; i < firstIndex + socketsTaken; i++)
		{
			if (!SocketIsFree(i, moduleItem))
			{
				return false;
			}
		}
		return true;
	}

	public bool TrySyncModuleInventory(BaseVehicleModule moduleEntity, int firstSocketIndex)
	{
		if (firstSocketIndex < 0)
		{
			Debug.LogError($"{GetType().Name}: Invalid socket index ({firstSocketIndex}) for new module entity.", vehicle.gameObject);
			return false;
		}
		Item slot = moduleContainer.GetSlot(firstSocketIndex);
		int numSocketsTaken = moduleEntity.GetNumSocketsTaken();
		if (!SocketsAreFree(firstSocketIndex, numSocketsTaken) && (slot == null || moduleEntity.AssociatedItemInstance != slot))
		{
			Debug.LogError($"{GetType().Name}: Sockets are not free for new module entity. First: {firstSocketIndex} Taken: {numSocketsTaken}", vehicle.gameObject);
			return false;
		}
		if (slot == null)
		{
			Item item = ItemManager.Create(moduleEntity.AssociatedItemDef, 1, 0uL);
			item.condition = moduleEntity.health;
			moduleEntity.AssociatedItemInstance = item;
			bool num = TryAddModuleItem(item, firstSocketIndex);
			if (num)
			{
				vehicle.SetUpModule(moduleEntity, item);
				return num;
			}
			item.Remove();
			return num;
		}
		return true;
	}

	private bool SocketIsUsed(Item item, int slotIndex)
	{
		return !SocketIsFree(slotIndex, item);
	}

	private ItemContainer CreateModuleInventory(BaseModularVehicle vehicle, bool giveUID)
	{
		ItemContainer itemContainer = Pool.Get<ItemContainer>();
		itemContainer.entityOwner = vehicle;
		itemContainer.allowedContents = ItemContainer.ContentsType.Generic;
		itemContainer.maxStackSize = 1;
		itemContainer.ServerInitialize(null, TotalSockets);
		if (giveUID)
		{
			itemContainer.GiveUID();
		}
		itemContainer.onItemAddedRemoved = OnSocketInventoryAddRemove;
		itemContainer.canAcceptItem = ItemFilter;
		itemContainer.slotIsReserved = SocketIsUsed;
		return itemContainer;
	}

	private ItemContainer CreateChassisInventory(BaseModularVehicle vehicle, bool giveUID)
	{
		ItemContainer itemContainer = Pool.Get<ItemContainer>();
		itemContainer.entityOwner = vehicle;
		itemContainer.allowedContents = ItemContainer.ContentsType.Generic;
		itemContainer.maxStackSize = 1;
		itemContainer.ServerInitialize(null, 1);
		if (giveUID)
		{
			itemContainer.GiveUID();
		}
		return itemContainer;
	}

	private void OnSocketInventoryAddRemove(Item moduleItem, bool added)
	{
		if (added)
		{
			ModuleItemAdded(moduleItem, moduleItem.position);
		}
		else
		{
			ModuleItemRemoved(moduleItem);
		}
	}

	private void ModuleItemAdded(Item moduleItem, int socketIndex)
	{
		ItemModVehicleModule component = moduleItem.info.GetComponent<ItemModVehicleModule>();
		if (!Application.isLoadingSave && vehicle.GetModuleForItem(moduleItem) == null)
		{
			vehicle.CreatePhysicalModuleEntity(moduleItem, component, socketIndex);
		}
		moduleItem.OnDirty += OnModuleItemChanged;
	}

	private void ModuleItemRemoved(Item moduleItem)
	{
		if (moduleItem == null)
		{
			Debug.LogError("Null module item removed.", vehicle.gameObject);
			return;
		}
		moduleItem.OnDirty -= OnModuleItemChanged;
		BaseVehicleModule moduleForItem = vehicle.GetModuleForItem(moduleItem);
		if (moduleForItem != null)
		{
			if (!moduleForItem.IsFullySpawned())
			{
				Debug.LogError("Module entity being removed before it's fully spawned. This could cause errors.", vehicle.gameObject);
			}
			moduleForItem.Kill();
		}
		else
		{
			Debug.Log("Couldn't find entity for this item.");
		}
	}

	private void OnModuleItemChanged(Item moduleItem)
	{
		BaseVehicleModule moduleForItem = vehicle.GetModuleForItem(moduleItem);
		if (moduleForItem != null)
		{
			moduleForItem.SetHealth(moduleItem.condition);
			if (moduleForItem.FirstSocketIndex != moduleItem.position)
			{
				ModuleItemRemoved(moduleItem);
				ModuleItemAdded(moduleItem, moduleItem.position);
			}
		}
	}

	private bool ItemFilter(Item item, int targetSlot)
	{
		string failureReason;
		return vehicle.ModuleCanBeAdded(item, targetSlot, out failureReason);
	}
}
