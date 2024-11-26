using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public class State_Roam : FSMStateBase
{
	[SerializeField]
	private Vector2 distanceRange = new Vector2(10f, 20f);

	[SerializeField]
	private float homeRadius = 50f;

	private Vector3? homePosition;

	public override EFSMStateStatus OnStateEnter()
	{
		Reset();
		if (!homePosition.HasValue)
		{
			homePosition = Owner.transform.position;
		}
		float num = UnityEngine.Random.Range(distanceRange.x, distanceRange.y);
		Vector3 v = ((Vector3.Distance(homePosition.Value, Owner.transform.position) > homeRadius) ? (homePosition.Value - Owner.transform.position).normalized : UnityEngine.Random.insideUnitSphere);
		float ratio = Mathf.InverseLerp(0f, distanceRange.y, num);
		base.Agent.SetSpeed(ratio);
		base.Agent.SetDestinationFromDirectionAsync(v.XZ3D(), num, 0f, restrictTerrain: true);
		return base.OnStateEnter();
	}

	public override EFSMStateStatus OnStateUpdate(float deltaTime)
	{
		if (!base.Agent.IsFollowingPath)
		{
			return EFSMStateStatus.Success;
		}
		return base.OnStateUpdate(deltaTime);
	}

	public override void OnStateExit()
	{
		base.Agent.ResetPath();
		base.OnStateExit();
	}

	private void Reset()
	{
		base.Senses.ClearTarget();
		base.Blackboard.Clear();
		if (Owner is BaseCombatEntity { healthFraction: <1f, SecondsSinceAttacked: >120f } baseCombatEntity)
		{
			baseCombatEntity.SetHealth(Owner.MaxHealth());
		}
	}
}
