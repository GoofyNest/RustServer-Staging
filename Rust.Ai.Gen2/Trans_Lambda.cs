using System;

namespace Rust.Ai.Gen2;

public class Trans_Lambda : FSMTransitionBase
{
	private Func<BaseEntity, bool> EvaluateFunc;

	public Trans_Lambda(Func<BaseEntity, bool> evaluateFunc)
	{
		EvaluateFunc = evaluateFunc;
	}

	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_Lambda"))
		{
			if (Owner == null)
			{
				return false;
			}
			return EvaluateFunc(Owner);
		}
	}
}
