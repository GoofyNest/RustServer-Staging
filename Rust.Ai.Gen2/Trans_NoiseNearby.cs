using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
internal class Trans_NoiseNearby : FSMTransitionBase
{
	[SerializeField]
	public float distance = 7f;

	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_NoiseNearby"))
		{
			NpcNoiseEvent currentNoise = base.Senses.currentNoise;
			return currentNoise != null && Vector3.Distance(Owner.transform.position, currentNoise.Position) < distance;
		}
	}

	public override void OnTransitionTaken(FSMStateBase from, FSMStateBase to)
	{
		base.OnTransitionTaken(from, to);
		if (base.Senses.currentNoise != null)
		{
			if (to is IParametrized<NpcNoiseEvent> parametrized)
			{
				parametrized.SetParameter(base.Senses.currentNoise);
			}
			base.Senses.ConsumeCurrentNoise();
		}
	}

	public override string GetName()
	{
		return $"{base.GetName()} <{distance}m";
	}
}
