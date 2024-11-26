using System;

namespace Rust.Ai.Gen2;

[Serializable]
public class Trans_AlwaysValid : FSMTransitionBase
{
	protected override bool EvaluateInternal()
	{
		return true;
	}
}
