using System.Collections.Generic;
using Facepunch;
using UnityEngine;

[CreateAssetMenu(menuName = "Rust/Missions/OBJECTIVES/CookItem")]
public class MissionObjective_CookItem : MissionObjective
{
	[ItemSelector(ItemCategory.All)]
	[Tooltip("The cooked result that this objective is looking for (eg cooked chicken, not raw)")]
	public ItemDefinition targetItem;

	public int targetItemAmount;

	public BaseEntityRef[] pingEntitiesOnTutorialIsland;

	public BasePlayer.PingType pingType = BasePlayer.PingType.GoTo;

	public bool checkExistingInventory;

	private bool HasPings
	{
		get
		{
			if (pingEntitiesOnTutorialIsland != null)
			{
				return pingEntitiesOnTutorialIsland.Length != 0;
			}
			return false;
		}
	}

	public override void MissionStarted(int index, BaseMission.MissionInstance instance, BasePlayer forPlayer)
	{
		base.MissionStarted(index, instance, forPlayer);
		instance.objectiveStatuses[index].progressCurrent = 0f;
		instance.objectiveStatuses[index].progressTarget = targetItemAmount;
	}

	public override void ObjectiveStarted(BasePlayer playerFor, int index, BaseMission.MissionInstance instance)
	{
		base.ObjectiveStarted(playerFor, index, instance);
		if (HasPings)
		{
			TutorialIsland currentTutorialIsland = playerFor.GetCurrentTutorialIsland();
			if (currentTutorialIsland != null)
			{
				List<TutorialBuildTarget> obj = Pool.Get<List<TutorialBuildTarget>>();
				BaseEntityRef[] array = pingEntitiesOnTutorialIsland;
				foreach (BaseEntityRef baseEntityRef in array)
				{
					obj.Clear();
					currentTutorialIsland.GetBuildTargets(obj, baseEntityRef.Get().prefabID);
					if (obj.Count > 0)
					{
						List<BaseOven> obj2 = Pool.Get<List<BaseOven>>();
						Vis.Entities(obj[0].transform.position, 0.25f, obj2, 153092352);
						if (obj2.Count > 0)
						{
							playerFor.RegisterPingedEntity(obj2[0], pingType);
						}
						Pool.FreeUnmanaged(ref obj2);
						break;
					}
				}
				Pool.FreeUnmanaged(ref obj);
			}
		}
		if (checkExistingInventory)
		{
			int amount = playerFor.inventory.GetAmount(targetItem);
			if (amount > 0)
			{
				ProcessMissionEvent(playerFor, instance, index, BaseMission.MissionEventType.COOK, new BaseMission.MissionEventPayload
				{
					IntIdentifier = targetItem.itemid
				}, amount);
			}
		}
	}

	public override void ProcessMissionEvent(BasePlayer playerFor, BaseMission.MissionInstance instance, int index, BaseMission.MissionEventType type, BaseMission.MissionEventPayload payload, float amount)
	{
		base.ProcessMissionEvent(playerFor, instance, index, type, payload, amount);
		if (type != BaseMission.MissionEventType.COOK || IsCompleted(index, instance) || !CanProgress(index, instance) || targetItem.itemid != payload.IntIdentifier)
		{
			return;
		}
		instance.objectiveStatuses[index].progressCurrent += (int)amount;
		if (instance.objectiveStatuses[index].progressCurrent >= (float)targetItemAmount)
		{
			CompleteObjective(index, instance, playerFor);
			if (HasPings)
			{
				playerFor.DeregisterPingedEntity(payload.NetworkIdentifier, pingType);
			}
		}
		playerFor.MissionDirty();
	}
}
