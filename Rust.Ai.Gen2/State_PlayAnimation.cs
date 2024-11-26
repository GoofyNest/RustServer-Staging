using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public class State_PlayAnimation : State_PlayAnimationBase
{
	[SerializeField]
	public AnimationClip Animation;

	public override EFSMStateStatus OnStateEnter()
	{
		EFSMStateStatus result = base.OnStateEnter();
		base.AnimPlayer.PlayServer(Animation, base.SucceedAction);
		return result;
	}
}
