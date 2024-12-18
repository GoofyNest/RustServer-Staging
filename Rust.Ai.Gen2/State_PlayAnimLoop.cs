using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public class State_PlayAnimLoop : State_PlayAnimationBase
{
	[SerializeField]
	public AnimationClip Start;

	[SerializeField]
	public AnimationClip Loop;

	[SerializeField]
	public AnimationClip Stop;

	[SerializeField]
	public float MinDuration = 7f;

	[SerializeField]
	public float MaxDuration = 14f;

	private float duration;

	private Action _playLoopAction;

	private Action PlayLoopAction => PlayLoop;

	public override EFSMStateStatus OnStateEnter()
	{
		EFSMStateStatus result = base.OnStateEnter();
		duration = UnityEngine.Random.Range(MinDuration, MaxDuration);
		base.AnimPlayer.PlayServer(Start, PlayLoopAction);
		return result;
	}

	private void PlayLoop()
	{
		if (!(duration <= 0f))
		{
			base.AnimPlayer.PlayServer(Loop, PlayLoopAction);
		}
	}

	public override EFSMStateStatus OnStateUpdate(float deltaTime)
	{
		if (duration > 0f)
		{
			duration -= deltaTime;
			if (duration <= 0f)
			{
				base.AnimPlayer.PlayServer(Stop, base.SucceedAction);
			}
		}
		return base.OnStateUpdate(deltaTime);
	}
}
