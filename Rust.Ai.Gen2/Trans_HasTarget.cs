using System;

namespace Rust.Ai.Gen2;

[Serializable]
internal class Trans_HasTarget : FSMTransitionBase
{
	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_HasTarget"))
		{
			BaseEntity target;
			return base.Senses.FindTarget(out target);
		}
	}
}
