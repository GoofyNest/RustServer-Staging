using System.Collections.Generic;
using ConVar;
using Facepunch;
using UnityEngine;

internal static class SimpleUpgrade
{
	public static bool CanUpgrade(BaseEntity entity, ItemDefinition upgradeItem, BasePlayer player)
	{
		if (player == null)
		{
			return false;
		}
		if (entity == null)
		{
			return false;
		}
		if (upgradeItem == null)
		{
			return false;
		}
		if (!player.CanInteract())
		{
			return false;
		}
		if (player.IsBuildingBlocked(entity.transform.position, entity.transform.rotation, entity.bounds))
		{
			return false;
		}
		if (upgradeItem.GetComponent<ItemModDeployable>() == null)
		{
			return false;
		}
		if (IsUpgradeBlocked(entity, upgradeItem, player))
		{
			return false;
		}
		if (!CanAffordUpgrade(entity, upgradeItem, player))
		{
			return false;
		}
		return true;
	}

	public static bool CanAffordUpgrade(BaseEntity entity, ItemDefinition upgradeItem, BasePlayer player)
	{
		if (player == null)
		{
			return false;
		}
		ISimpleUpgradable simpleUpgradable = entity as ISimpleUpgradable;
		if (entity == null)
		{
			return false;
		}
		if (player.IsInCreativeMode && Creative.freeBuild)
		{
			return true;
		}
		if (simpleUpgradable.CostIsItem())
		{
			return player.inventory.GetAmount(upgradeItem) > 0;
		}
		if (upgradeItem.Blueprint == null)
		{
			return false;
		}
		if (!ItemModStudyBlueprint.IsBlueprintUnlocked(upgradeItem, player))
		{
			return false;
		}
		foreach (ItemAmount ingredient in upgradeItem.Blueprint.ingredients)
		{
			if ((float)player.inventory.GetAmount(ingredient.itemid) < ingredient.amount)
			{
				return false;
			}
		}
		return true;
	}

	public static void PayForUpgrade(BaseEntity entity, ItemDefinition upgradeItem, BasePlayer player)
	{
		if (player == null || (player.IsInCreativeMode && Creative.freeBuild) || !(entity is ISimpleUpgradable simpleUpgradable))
		{
			return;
		}
		List<Item> list = new List<Item>();
		if (simpleUpgradable.CostIsItem())
		{
			player.inventory.Take(list, upgradeItem.itemid, 1);
			player.Command("note.inv " + upgradeItem.itemid + " " + -1);
		}
		else
		{
			foreach (ItemAmount ingredient in upgradeItem.Blueprint.ingredients)
			{
				player.inventory.Take(list, ingredient.itemid, (int)ingredient.amount);
				player.Command("note.inv " + ingredient.itemid + " " + ingredient.amount * -1f);
			}
		}
		foreach (Item item in list)
		{
			item.Remove();
		}
	}

	public static void DoUpgrade(BaseEntity entity, BasePlayer player)
	{
		if (!(entity is ISimpleUpgradable simpleUpgradable) || !simpleUpgradable.CanUpgrade(player))
		{
			return;
		}
		ItemDefinition upgradeItem = simpleUpgradable.GetUpgradeItem();
		PayForUpgrade(entity, upgradeItem, player);
		EntityRef[] slots = entity.GetSlots();
		BaseEntity parentEntity = entity.GetParentEntity();
		ItemModDeployable component = upgradeItem.GetComponent<ItemModDeployable>();
		BaseEntity baseEntity = GameManager.server.CreateEntity(component.entityPrefab.resourcePath, entity.transform.position, entity.transform.rotation);
		baseEntity.SetParent(parentEntity);
		baseEntity.OwnerID = player.userID;
		Deployable component2 = component.entityPrefab.Get().GetComponent<Deployable>();
		if (component2 != null && component2.placeEffect.isValid)
		{
			Effect.server.Run(component2.placeEffect.resourcePath, entity.transform.position, Vector3.up);
		}
		DecayEntity decayEntity = baseEntity as DecayEntity;
		if (decayEntity != null)
		{
			decayEntity.timePlaced = entity.GetNetworkTime();
		}
		List<SprayCan.ChildPreserveInfo> obj = Facepunch.Pool.Get<List<SprayCan.ChildPreserveInfo>>();
		foreach (BaseEntity child in entity.children)
		{
			obj.Add(new SprayCan.ChildPreserveInfo
			{
				TargetEntity = child,
				TargetBone = child.parentBone,
				LocalPosition = child.transform.localPosition,
				LocalRotation = child.transform.localRotation
			});
		}
		foreach (SprayCan.ChildPreserveInfo item in obj)
		{
			item.TargetEntity.SetParent(null, worldPositionStays: true);
		}
		entity.Kill();
		if (baseEntity is DecayEntity decayEntity2)
		{
			decayEntity2.AttachToBuilding(null);
		}
		baseEntity.Spawn();
		foreach (SprayCan.ChildPreserveInfo item2 in obj)
		{
			item2.TargetEntity.SetParent(baseEntity, item2.TargetBone, worldPositionStays: true);
			item2.TargetEntity.transform.localPosition = item2.LocalPosition;
			item2.TargetEntity.transform.localRotation = item2.LocalRotation;
			item2.TargetEntity.SendNetworkUpdate();
		}
		baseEntity.SetSlots(slots);
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	public static bool IsUpgradeBlocked(BaseEntity entity, ItemDefinition upgradeItem, BasePlayer player)
	{
		if (upgradeItem == null)
		{
			return true;
		}
		if (entity == null)
		{
			return true;
		}
		ItemModDeployable component = upgradeItem.GetComponent<ItemModDeployable>();
		DeployVolume[] volumes = PrefabAttribute.server.FindAll<DeployVolume>(component.entityPrefab.resourceID);
		if (DeployVolume.Check(entity.transform.position, entity.transform.rotation, volumes, ~((1 << entity.gameObject.layer) | 0x20000000)))
		{
			return true;
		}
		return false;
	}
}
