using UnityEngine;

namespace Rust.Ai.Gen2;

internal abstract class FSMSlowTransitionBase : FSMTransitionBase
{
	private bool cachedEvalResult;

	private double? lastEvalTime;

	private double cacheLifeTime = 1.0;

	protected sealed override bool EvaluateInternal()
	{
		double timeAsDouble = Time.timeAsDouble;
		if (!lastEvalTime.HasValue || timeAsDouble - lastEvalTime.Value > cacheLifeTime)
		{
			cachedEvalResult = EvaluateAtInterval();
			lastEvalTime = timeAsDouble;
		}
		return cachedEvalResult;
	}

	protected abstract bool EvaluateAtInterval();
}
