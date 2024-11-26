using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public class Trans_RandomChance : FSMTransitionBase
{
	[SerializeField]
	public float chance = 0.5f;

	private bool Triggered;

	public override void OnStateEnter()
	{
		Triggered = UnityEngine.Random.value <= chance;
	}

	public override void OnStateExit()
	{
		Triggered = false;
	}

	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_RandomChance"))
		{
			return Triggered;
		}
	}
}
