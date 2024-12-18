using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
[SoftRequireComponent(typeof(RootMotionPlayer))]
public class State_EatFood : FSMStateBase
{
	[SerializeField]
	protected AnimationClip Animation;

	private const float damageToCorpsesPerLoop = 2.5f;

	private const float timeToForgetSightingWhileEating = 5f;

	private bool isAnimationPlaying;

	public override EFSMStateStatus OnStateEnter()
	{
		if (!base.Senses.FindFood(out var food))
		{
			return EFSMStateStatus.Failure;
		}
		Vector3 forward = food.transform.position - Owner.transform.position;
		forward.y = 0f;
		Owner.transform.rotation = Quaternion.LookRotation(forward);
		base.Senses.timeToForgetSightings.Value = 5f;
		PlayAnimation();
		return base.OnStateEnter();
	}

	private void PlayAnimation()
	{
		isAnimationPlaying = true;
		base.AnimPlayer.PlayServer(Animation, OnAnimationEnd);
	}

	private void OnAnimationEnd()
	{
		isAnimationPlaying = false;
	}

	public override EFSMStateStatus OnStateUpdate(float deltaTime)
	{
		if (!base.Senses.FindFood(out var food))
		{
			return EFSMStateStatus.Failure;
		}
		if (isAnimationPlaying)
		{
			return base.OnStateUpdate(deltaTime);
		}
		if (food is BaseCorpse baseCorpse)
		{
			baseCorpse.Hurt(2.5f);
			if (baseCorpse.IsDead())
			{
				base.Senses.ClearTarget();
				return EFSMStateStatus.Success;
			}
			PlayAnimation();
		}
		else if (food is DroppedItem droppedItem)
		{
			droppedItem.item.amount = Mathf.FloorToInt((float)droppedItem.item.amount * 0.5f);
			if (droppedItem.item.amount <= 0)
			{
				droppedItem.DestroyItem();
				droppedItem.Kill();
				base.Senses.ClearTarget();
				return EFSMStateStatus.Success;
			}
			droppedItem.item.MarkDirty();
			PlayAnimation();
		}
		return base.OnStateUpdate(deltaTime);
	}

	public override void OnStateExit()
	{
		base.Senses.timeToForgetSightings.Reset();
		base.AnimPlayer.StopServer();
	}
}
