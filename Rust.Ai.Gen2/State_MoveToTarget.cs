using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public class State_MoveToTarget : FSMStateBase, IParametrized<BaseEntity>
{
	[SerializeField]
	public LimitedTurnNavAgent.Speeds speed = LimitedTurnNavAgent.Speeds.FullSprint;

	[SerializeField]
	public bool succeedWhenDestinationIsReached = true;

	[SerializeField]
	public bool stopAtDestination = true;

	[SerializeField]
	public float accelerationOverride;

	[SerializeField]
	public float decelerationOverride;

	public void SetParameter(BaseEntity target)
	{
		base.Senses.TrySetTarget(target);
	}

	public override EFSMStateStatus OnStateEnter()
	{
		base.Agent.ResetPath();
		base.Agent.shouldStopAtDestination = stopAtDestination;
		base.Agent.SetSpeed(speed);
		if (accelerationOverride > 0f)
		{
			base.Agent.acceleration.Value = accelerationOverride;
		}
		if (decelerationOverride > 0f)
		{
			base.Agent.deceleration.Value = decelerationOverride;
		}
		if (!GetMoveDestination(out var destination))
		{
			return EFSMStateStatus.Failure;
		}
		base.Agent.SetDestinationAsync(destination);
		return base.OnStateEnter();
	}

	public override EFSMStateStatus OnStateUpdate(float deltaTime)
	{
		if (!base.Agent.IsFollowingPath && succeedWhenDestinationIsReached)
		{
			return EFSMStateStatus.Success;
		}
		if (!GetMoveDestination(out var destination))
		{
			return EFSMStateStatus.Failure;
		}
		base.Agent.SetDestinationAsync(destination);
		return base.OnStateUpdate(deltaTime);
	}

	public override void OnStateExit()
	{
		base.Agent.ResetPath();
		base.OnStateExit();
	}

	protected virtual bool GetMoveDestination(out Vector3 destination)
	{
		if (!base.Senses.FindTargetPosition(out var targetPosition))
		{
			destination = Vector3.zero;
			return false;
		}
		destination = targetPosition;
		return true;
	}
}
