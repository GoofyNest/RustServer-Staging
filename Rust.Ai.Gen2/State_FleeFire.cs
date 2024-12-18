using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public class State_FleeFire : State_Flee
{
	private int numExecutions;

	private int maxExecutionsBeforeMinDist = 2;

	private float minDistance = 8f;

	private float maxDistance = 20f;

	private double timeOfLastExecution;

	public override EFSMStateStatus OnStateEnter()
	{
		if (Time.timeAsDouble - timeOfLastExecution > 30.0)
		{
			numExecutions = 0;
		}
		timeOfLastExecution = Time.timeAsDouble;
		distance = 7f;
		desiredDistance = Mathx.RemapValClamped(numExecutions, 0f, maxExecutionsBeforeMinDist, maxDistance, minDistance);
		numExecutions++;
		return base.OnStateEnter();
	}

	public override EFSMStateStatus OnStateUpdate(float deltaTime)
	{
		if (base.Senses.FindTargetPosition(out var targetPosition) && Time.timeAsDouble - timeOfLastExecution > 1.0 && Vector3.Distance(Owner.transform.position, targetPosition) > desiredDistance)
		{
			return EFSMStateStatus.Success;
		}
		return base.OnStateUpdate(deltaTime);
	}
}
