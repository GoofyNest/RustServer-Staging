using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public class Trans_TargetInRange : FSMTransitionBase
{
	[SerializeField]
	public float Range = 4f;

	private float rangeSq;

	public override void Init(BaseEntity owner)
	{
		base.Init(owner);
		rangeSq = Range * Range;
	}

	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_TargetInRange"))
		{
			return base.Senses.IsTargetInRangeSq(rangeSq);
		}
	}

	public override string GetName()
	{
		return string.Format("{0} {1}{2}m", base.GetName(), Inverted ? ">=" : "<", Range);
	}
}
