using System;

namespace Rust.Ai.Gen2;

[Serializable]
internal class Trans_SeesFood : FSMTransitionBase
{
	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_SeesFood"))
		{
			BaseEntity food;
			return base.Senses.FindFood(out food);
		}
	}
}
