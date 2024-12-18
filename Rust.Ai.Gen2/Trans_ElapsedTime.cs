using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public class Trans_ElapsedTime : FSMTransitionBase
{
	[SerializeField]
	public double Duration = 6.0;

	private double startTime;

	public override void OnStateEnter()
	{
		startTime = Time.timeAsDouble;
	}

	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_ElapsedTime"))
		{
			return Time.timeAsDouble >= startTime + Duration;
		}
	}

	public override string GetName()
	{
		return $"{base.GetName()} {Duration}s";
	}
}
