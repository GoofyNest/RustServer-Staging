using System;
using UnityEngine;
using UnityEngine.AI;

namespace Rust.Ai.Gen2;

[Serializable]
public class State_Circle : FSMStateBase, IParametrized<BaseEntity>
{
	[SerializeField]
	public float radius = 16f;

	[SerializeField]
	public LimitedTurnNavAgent.Speeds speed = LimitedTurnNavAgent.Speeds.Sprint;

	private bool clockWise = true;

	private float radiusOffset;

	public void SetParameter(BaseEntity target)
	{
		base.Senses.TrySetTarget(target);
	}

	public override EFSMStateStatus OnStateEnter()
	{
		base.Agent.SetSpeed(speed);
		radiusOffset = UnityEngine.Random.Range(-1f, 1f);
		clockWise = UnityEngine.Random.value > 0.5f;
		base.Agent.shouldStopAtDestination = false;
		return base.OnStateEnter();
	}

	public override void OnStateExit()
	{
		base.Agent.ResetPath();
		base.OnStateExit();
	}

	protected virtual bool GetCircleOrigin(out Vector3 origin)
	{
		return base.Senses.FindTargetPosition(out origin);
	}

	public override EFSMStateStatus OnStateUpdate(float deltaTime)
	{
		if (!GetCircleOrigin(out var origin))
		{
			return EFSMStateStatus.Failure;
		}
		float num = radius + radiusOffset;
		float f = (Quaternion.LookRotation(Owner.transform.position - origin).eulerAngles.y + 5f * (float)(clockWise ? 1 : (-1))) * (MathF.PI / 180f);
		Vector3 vector = origin + new Vector3(Mathf.Sin(f), 0f, Mathf.Cos(f)) * num;
		vector.y = Mathf.Lerp(origin.y, Owner.transform.position.y, Mathf.InverseLerp(0f, Vector3.Distance(origin, Owner.transform.position), num));
		if (NavMesh.Raycast(Owner.transform.position, vector, out var _, -1))
		{
			return EFSMStateStatus.Failure;
		}
		if (!base.Agent.SetDestination(vector))
		{
			return EFSMStateStatus.Failure;
		}
		return base.OnStateUpdate(deltaTime);
	}
}
