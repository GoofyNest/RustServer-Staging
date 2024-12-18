using System;

namespace Rust.Ai.Gen2;

[Serializable]
internal class Trans_CanReachTarget_Slow : FSMSlowTransitionBase
{
	protected override bool EvaluateAtInterval()
	{
		using (TimeWarning.New("Trans_CanReachTarget_Slow"))
		{
			if (!base.Senses.FindTargetPosition(out var targetPosition))
			{
				return false;
			}
			LimitedTurnNavAgent component = Owner.GetComponent<LimitedTurnNavAgent>();
			if (component == null)
			{
				return false;
			}
			return component.CanReach(targetPosition);
		}
	}
}
