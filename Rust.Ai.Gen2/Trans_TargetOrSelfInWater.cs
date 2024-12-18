using System;

namespace Rust.Ai.Gen2;

[Serializable]
internal class Trans_TargetOrSelfInWater : FSMTransitionBase
{
	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_TargetOrSelfInWater"))
		{
			if (base.Senses.FindTarget(out var target) && target.ToNonNpcPlayer(out var _) && !LimitedTurnNavAgent.IsAcceptableWaterDepth(Owner, target.transform.position, Owner.bounds.extents.y))
			{
				return true;
			}
			return !LimitedTurnNavAgent.IsAcceptableWaterDepth(Owner, Owner.transform.position, Owner.bounds.extents.y);
		}
	}
}
