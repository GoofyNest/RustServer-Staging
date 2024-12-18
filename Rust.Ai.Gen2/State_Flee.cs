using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public class State_Flee : FSMStateBase
{
	[SerializeField]
	public LimitedTurnNavAgent.Speeds speed = LimitedTurnNavAgent.Speeds.Sprint;

	[SerializeField]
	public float distance = 20f;

	[SerializeField]
	public float desiredDistance = 50f;

	[SerializeField]
	public int maxAttempts = 3;

	private int attempts;

	private float startDistance;

	public override EFSMStateStatus OnStateEnter()
	{
		base.Blackboard.Remove("HitByFire");
		if (!base.Senses.FindTargetPosition(out var targetPosition))
		{
			return EFSMStateStatus.Success;
		}
		attempts = 0;
		base.Agent.SetSpeed(speed);
		base.Agent.shouldStopAtDestination = false;
		startDistance = Vector3.Distance(Owner.transform.position, targetPosition);
		MoveAwayFromTarget();
		return base.OnStateEnter();
	}

	public override void OnStateExit()
	{
		base.Agent.ResetPath();
		base.OnStateExit();
	}

	public override EFSMStateStatus OnStateUpdate(float deltaTime)
	{
		if (base.Agent.IsFollowingPath)
		{
			return base.OnStateUpdate(deltaTime);
		}
		if (!base.Senses.FindTargetPosition(out var targetPosition))
		{
			return EFSMStateStatus.Success;
		}
		if (Vector3.Distance(targetPosition, Owner.transform.position) > desiredDistance + startDistance)
		{
			return EFSMStateStatus.Success;
		}
		attempts++;
		if (attempts >= maxAttempts)
		{
			return EFSMStateStatus.Success;
		}
		return MoveAwayFromTarget();
	}

	private EFSMStateStatus MoveAwayFromTarget()
	{
		if (!base.Senses.FindTargetPosition(out var targetPosition))
		{
			return EFSMStateStatus.Success;
		}
		Vector3 normalizedDirection = (Owner.transform.position - targetPosition).NormalizeXZ();
		if (!base.Agent.SetDestinationFromDirection(normalizedDirection, distance))
		{
			return EFSMStateStatus.Failure;
		}
		return EFSMStateStatus.None;
	}
}
