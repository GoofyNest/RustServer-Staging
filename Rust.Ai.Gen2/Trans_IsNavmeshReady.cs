using System;
using ConVar;

namespace Rust.Ai.Gen2;

[Serializable]
internal class Trans_IsNavmeshReady : FSMTransitionBase
{
	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_IsNavmeshReady"))
		{
			return AI.move && base.Agent.IsNavmeshReady;
		}
	}
}
