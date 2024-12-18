using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public class Trans_TargetInFront : FSMTransitionBase
{
	[SerializeField]
	public float Angle = 90f;

	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_TargetInFront"))
		{
			if (!base.Senses.FindTargetPosition(out var targetPosition))
			{
				return false;
			}
			Vector3 to = targetPosition - Owner.transform.position;
			return Vector3.Angle(Owner.transform.forward, to) < Angle;
		}
	}

	public override string GetName()
	{
		return string.Format("{0} {1}{2}°", base.GetName(), Inverted ? ">=" : "<", Angle);
	}
}
