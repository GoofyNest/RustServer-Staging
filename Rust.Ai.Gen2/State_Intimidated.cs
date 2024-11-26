using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public class State_Intimidated : State_PlayAnimationRM
{
	private static readonly float facingAwayDotThreshold = Mathf.Cos(MathF.PI / 2f);

	public override EFSMStateStatus OnStateEnter()
	{
		FaceTarget = true;
		if (base.Senses.FindTargetPosition(out var targetPosition) && Vector3.Dot(Owner.transform.forward, (Owner.transform.position - targetPosition).normalized) > facingAwayDotThreshold)
		{
			return EFSMStateStatus.Success;
		}
		return base.OnStateEnter();
	}
}
