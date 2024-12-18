#define UNITY_ASSERTIONS
using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace Rust.Ai.Gen2;

[Serializable]
public class State_Attack : State_PlayAnimationRM
{
	[SerializeField]
	public float Damage = 20f;

	[SerializeField]
	public float Delay = 0.5f;

	[SerializeField]
	public DamageType DamageType = DamageType.Bite;

	private Action _doDamageAction;

	private Action DoDamageAction => DoDamage;

	public override EFSMStateStatus OnStateEnter()
	{
		Assert.IsTrue(Delay < Animation.inPlaceAnimation.length);
		if (!base.Senses.FindTargetPosition(out var targetPosition))
		{
			return EFSMStateStatus.Failure;
		}
		FaceTarget = false;
		Vector3 rhs = (Owner.transform.position - targetPosition).NormalizeXZ();
		Vector3 vector = Vector3.Cross(Vector3.up, rhs);
		targetPosition += ((UnityEngine.Random.value > 0.5f) ? 1f : (-1f)) * vector;
		Vector3 forward = (targetPosition - Owner.transform.position).NormalizeXZ();
		Owner.transform.rotation = Quaternion.LookRotation(forward);
		Owner.Invoke(DoDamageAction, Delay);
		return base.OnStateEnter();
	}

	public override void OnStateExit()
	{
		Owner.CancelInvoke(DoDamageAction);
		base.OnStateExit();
	}

	private void DoDamage()
	{
		if (base.Senses.FindTarget(out var target) && target is BaseCombatEntity baseCombatEntity)
		{
			baseCombatEntity.Hurt(Damage, DamageType, Owner);
		}
	}
}
