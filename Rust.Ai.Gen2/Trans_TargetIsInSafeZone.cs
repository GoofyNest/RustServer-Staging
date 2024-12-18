using System;

namespace Rust.Ai.Gen2;

[Serializable]
internal class Trans_TargetIsInSafeZone : FSMTransitionBase
{
	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_TargetIsInSafeZone"))
		{
			BaseEntity target;
			return base.Senses.FindTarget(out target) && target.InSafeZone();
		}
	}
}
