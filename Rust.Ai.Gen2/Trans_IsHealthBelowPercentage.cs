using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
internal class Trans_IsHealthBelowPercentage : FSMTransitionBase
{
	[SerializeField]
	public float percentage = 0.25f;

	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_IsHealthBelowPercentage"))
		{
			return Owner is BaseCombatEntity baseCombatEntity && baseCombatEntity.healthFraction < percentage;
		}
	}

	public override string GetName()
	{
		return $"{base.GetName()} <{percentage * 100f}%";
	}
}
