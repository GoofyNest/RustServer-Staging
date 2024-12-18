using System;
using Facepunch;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
internal class Trans_InitialAlliesNotFighting : FSMTransitionBase
{
	[SerializeField]
	public float MinAllyHealthFraction = 0.3f;

	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_InitialAlliesNotFighting"))
		{
			using (PooledList<BaseEntity> pooledList = Pool.Get<PooledList<BaseEntity>>())
			{
				base.Senses.GetInitialAllies(pooledList);
				foreach (BaseEntity item in pooledList)
				{
					if (!item.GetComponent<SenseComponent>().FindTarget(out var _) && (!(item is BaseCombatEntity baseCombatEntity) || !(baseCombatEntity.healthFraction < MinAllyHealthFraction)))
					{
						return true;
					}
				}
			}
			return false;
		}
	}
}
