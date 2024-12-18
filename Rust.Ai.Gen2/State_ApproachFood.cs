using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
[SoftRequireComponent(typeof(LimitedTurnNavAgent), typeof(SenseComponent), typeof(BlackboardComponent))]
public class State_ApproachFood : State_MoveToTarget
{
	public const string TriedToApproachUnreachableFood = "TriedToApproachUnreachableFood";

	public override EFSMStateStatus OnStateEnter()
	{
		if (!base.Senses.FindFood(out var food))
		{
			return EFSMStateStatus.Failure;
		}
		if (!base.Agent.CanReach(food.transform.position))
		{
			base.Blackboard.Add("TriedToApproachUnreachableFood");
			SingletonComponent<NpcFoodManager>.Instance.Remove(food);
			return EFSMStateStatus.Failure;
		}
		return base.OnStateEnter();
	}

	protected override bool GetMoveDestination(out Vector3 destination)
	{
		if (!base.Senses.FindFood(out var food))
		{
			destination = Vector3.zero;
			return false;
		}
		destination = food.transform.position;
		return true;
	}
}
