using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public class Trans_Cooldown : FSMTransitionBase
{
	[SerializeField]
	public float cooldown = 20f;

	private double? lastTakenTime;

	public override void OnTransitionTaken(FSMStateBase from, FSMStateBase to)
	{
		base.OnTransitionTaken(from, to);
		lastTakenTime = Time.timeAsDouble;
	}

	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_Cooldown"))
		{
			return !lastTakenTime.HasValue || Time.timeAsDouble - lastTakenTime.Value >= (double)cooldown;
		}
	}

	public override string GetName()
	{
		return $"{base.GetName()} {cooldown}s";
	}
}
