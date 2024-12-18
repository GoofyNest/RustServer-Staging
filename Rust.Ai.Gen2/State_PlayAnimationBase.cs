using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

public abstract class State_PlayAnimationBase : FSMStateBase
{
	[SerializeField]
	public bool FaceTarget;

	private EFSMStateStatus _status;

	private Action _succeedAction;

	protected Action SucceedAction => Succeed;

	public override EFSMStateStatus OnStateEnter()
	{
		if (FaceTarget && base.Senses.FindTargetPosition(out var targetPosition))
		{
			Vector3 forward = targetPosition - Owner.transform.position;
			forward.y = 0f;
			Owner.transform.rotation = Quaternion.LookRotation(forward);
		}
		return base.OnStateEnter();
	}

	public override EFSMStateStatus OnStateUpdate(float deltaTime)
	{
		return _status;
	}

	public override void OnStateExit()
	{
		base.AnimPlayer.StopServer();
		_status = EFSMStateStatus.None;
	}

	private void Succeed()
	{
		_status = EFSMStateStatus.Success;
	}
}
