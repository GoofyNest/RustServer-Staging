using System.Collections.Generic;
using UnityEngine;

public class EntityFuelSystem
{
	private readonly bool isServer;

	private readonly bool editorGiveFreeFuel;

	private readonly uint fuelStorageID;

	public EntityRef<StorageContainer> fuelStorageInstance;

	private float nextFuelCheckTime;

	private bool cachedHasFuel;

	private float pendingFuel;

	public EntityFuelSystem(bool isServer, GameObjectRef fuelStoragePrefab, List<BaseEntity> children, bool editorGiveFreeFuel = true)
	{
		this.isServer = isServer;
		this.editorGiveFreeFuel = editorGiveFreeFuel;
		fuelStorageID = fuelStoragePrefab.GetEntity().prefabID;
		if (!isServer)
		{
			return;
		}
		foreach (BaseEntity child in children)
		{
			CheckNewChild(child);
		}
	}

	public bool IsInFuelInteractionRange(BasePlayer player)
	{
		StorageContainer fuelContainer = GetFuelContainer();
		if (fuelContainer != null)
		{
			float num = 0f;
			if (isServer)
			{
				num = 3f;
			}
			return fuelContainer.Distance(player.eyes.position) <= num;
		}
		return false;
	}

	private StorageContainer GetFuelContainer()
	{
		StorageContainer storageContainer = fuelStorageInstance.Get(isServer);
		if (storageContainer != null && storageContainer.IsValid())
		{
			return storageContainer;
		}
		return null;
	}

	public void CheckNewChild(BaseEntity child)
	{
		if (child.prefabID == fuelStorageID)
		{
			fuelStorageInstance.Set((StorageContainer)child);
		}
	}

	public Item GetFuelItem()
	{
		StorageContainer fuelContainer = GetFuelContainer();
		if (fuelContainer == null)
		{
			return null;
		}
		return fuelContainer.inventory.GetSlot(0);
	}

	public int GetFuelAmount()
	{
		Item fuelItem = GetFuelItem();
		if (fuelItem == null || fuelItem.amount < 1)
		{
			return 0;
		}
		return fuelItem.amount;
	}

	public float GetFuelFraction()
	{
		Item fuelItem = GetFuelItem();
		if (fuelItem == null || fuelItem.amount < 1)
		{
			return 0f;
		}
		return Mathf.Clamp01((float)fuelItem.amount / (float)fuelItem.MaxStackable());
	}

	public bool HasFuel(bool forceCheck = false)
	{
		if (Time.time > nextFuelCheckTime || forceCheck)
		{
			cachedHasFuel = (float)GetFuelAmount() > 0f;
			nextFuelCheckTime = Time.time + Random.Range(1f, 2f);
		}
		return cachedHasFuel;
	}

	public int TryUseFuel(float seconds, float fuelUsedPerSecond)
	{
		StorageContainer fuelContainer = GetFuelContainer();
		if (fuelContainer == null)
		{
			return 0;
		}
		Item slot = fuelContainer.inventory.GetSlot(0);
		if (slot == null || slot.amount < 1)
		{
			return 0;
		}
		pendingFuel += seconds * fuelUsedPerSecond;
		if (pendingFuel >= 1f)
		{
			int num = Mathf.FloorToInt(pendingFuel);
			slot.UseItem(num);
			pendingFuel -= num;
			return num;
		}
		return 0;
	}

	public void LootFuel(BasePlayer player)
	{
		if (IsInFuelInteractionRange(player))
		{
			GetFuelContainer().PlayerOpenLoot(player);
		}
	}

	public void AddStartingFuel(float amount = -1f)
	{
		amount = ((amount == -1f) ? ((float)GetFuelContainer().allowedItem.stackable * 0.2f) : amount);
		GetFuelContainer().inventory.AddItem(GetFuelContainer().allowedItem, Mathf.FloorToInt(amount), 0uL);
	}

	public void AdminAddFuel()
	{
		GetFuelContainer().inventory.AddItem(GetFuelContainer().allowedItem, GetFuelContainer().allowedItem.stackable, 0uL);
	}
}
