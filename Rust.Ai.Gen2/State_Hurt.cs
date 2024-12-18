using System;
using Facepunch;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public class State_Hurt : State_PlayAnimationRM, IParametrized<HitInfo>
{
	[SerializeField]
	private RootMotionData StrongHitL;

	[SerializeField]
	private RootMotionData StrongHitR;

	[SerializeField]
	private RootMotionData WeakHit;

	[SerializeField]
	private float StaggerRatio = 0.5f;

	private HitInfo HitInfo;

	public void SetParameter(HitInfo parameter)
	{
		if (HitInfo != null)
		{
			Pool.Free(ref HitInfo);
		}
		if (parameter == null)
		{
			Debug.LogWarning("No parameter set for hurt state");
		}
		HitInfo = Pool.Get<HitInfo>();
		HitInfo.CopyFrom(parameter);
	}

	public override EFSMStateStatus OnStateEnter()
	{
		if (HitInfo == null)
		{
			Debug.LogWarning("No hitinfo set for hurt state");
			return base.OnStateEnter();
		}
		if ((bool)HitInfo.InitiatorPlayer && !HitInfo.damageTypes.IsMeleeType())
		{
			HitInfo.InitiatorPlayer.LifeStoryShotHit(HitInfo.Weapon);
		}
		if (HitInfo.damageTypes.Has(DamageType.Heat))
		{
			base.Blackboard.Add("HitByFire");
		}
		float num = Owner.Health() + HitInfo.damageTypes.Total();
		float num2 = Owner.MaxHealth() * StaggerRatio;
		if (num > num2 && Owner.Health() < num2)
		{
			bool flag = Vector3.Dot(HitInfo.attackNormal, Owner.transform.right) > 0f;
			Animation = (flag ? StrongHitL : StrongHitR);
		}
		else
		{
			Animation = WeakHit;
		}
		if (HitInfo.Initiator is BaseCombatEntity baseCombatEntity)
		{
			bool flag2 = true;
			if (base.Senses.FindTarget(out var target))
			{
				bool num3 = Owner.Distance(baseCombatEntity) < 16f;
				bool flag3 = !target.IsNonNpcPlayer() && baseCombatEntity.IsNonNpcPlayer();
				flag2 = num3 || flag3;
			}
			if (flag2)
			{
				base.Senses.TrySetTarget(baseCombatEntity);
			}
		}
		if (!base.Senses.FindTarget(out var target2))
		{
			Debug.LogWarning("Got attacked but couldn't find a target");
			return base.OnStateEnter();
		}
		using (PooledList<BaseEntity> pooledList = Pool.Get<PooledList<BaseEntity>>())
		{
			base.Senses.GetInitialAllies(pooledList);
			foreach (BaseEntity item in pooledList)
			{
				item.GetComponent<Wolf2FSM>().Intimidate(target2);
			}
		}
		return base.OnStateEnter();
	}

	public override void OnStateExit()
	{
		Pool.Free(ref HitInfo);
		base.OnStateExit();
	}
}
