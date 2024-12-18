using System;
using UnityEngine;
using UnityEngine.AI;

namespace Rust.Ai.Gen2;

[Serializable]
public class State_AttackUnreachable : FSMStateBase
{
	private enum Phase
	{
		PreJump,
		Jump,
		Attack,
		JumpBack,
		PostJumpBack
	}

	private const float preJumpEnd = 0.29f;

	private const float jumpEnd = 0.395f;

	private const float attackEnd = 0.67f;

	private const float jumpBackEnd = 0.765f;

	private const float postJumpBackEnd = 0.95f;

	private const float groundCheckDistance = 2f;

	private const float damage = 35f;

	private const float meleeAttackRange = 1.7f;

	private const DamageType damageType = DamageType.Bite;

	public RootMotionData animClip;

	private Vector3 startLocation;

	private Quaternion startRotation;

	private Vector3 destination;

	private float elapsedTime;

	private LockState.LockHandle targetLock;

	private LockState.LockHandle movementLock;

	private Phase phase;

	private float previousOffsetZ;

	public static bool SampleGroundPositionUnderTarget(LimitedTurnNavAgent agent, BasePlayer targetAsPlayer, out Vector3 projectedLocation)
	{
		float radius = targetAsPlayer.GetRadius();
		return agent.SampleGroundPositionWithPhysics(targetAsPlayer.transform.position, out projectedLocation, 2f, radius);
	}

	public override EFSMStateStatus OnStateEnter()
	{
		if (!base.Senses.FindTarget(out var target) || !(target is BasePlayer basePlayer))
		{
			return EFSMStateStatus.Failure;
		}
		destination = target.transform.position;
		if (!basePlayer.IsOnGround() && !SampleGroundPositionUnderTarget(base.Agent, basePlayer, out destination))
		{
			return EFSMStateStatus.Failure;
		}
		if (!State_MoveToLastReachablePointNearTarget.CanJumpFromPosToPos(Owner, Owner.transform.position, destination))
		{
			return EFSMStateStatus.Failure;
		}
		movementLock = base.Agent.Pause();
		elapsedTime = 0f;
		targetLock = base.Senses.LockCurrentTarget();
		base.AnimPlayer.PlayServer(animClip.inPlaceAnimation);
		Owner.GetComponent<NavMeshAgent>().enabled = false;
		SetPhase(Phase.PreJump);
		return base.OnStateEnter();
	}

	private void SetPhase(Phase newPhase)
	{
		phase = newPhase;
		previousOffsetZ = animClip.zMotionCurve.Evaluate(elapsedTime);
		if (phase == Phase.Jump)
		{
			if (base.Senses.FindTarget(out var target) && target is BasePlayer targetAsPlayer)
			{
				SampleGroundPositionUnderTarget(base.Agent, targetAsPlayer, out destination);
			}
			startLocation = Owner.transform.position;
			Owner.transform.rotation = Quaternion.LookRotation((destination - Owner.transform.position).WithY(0f));
			Owner.ClientRPC(RpcTarget.NetworkGroup("CL_SetFloorSnappingEnabled"), arg1: false);
		}
		else if (phase == Phase.Attack)
		{
			startRotation = Owner.transform.rotation;
			if (base.Senses.FindTarget(out var target2))
			{
				if (target2 is BaseCombatEntity baseCombatEntity && Vector3.Distance(Owner.transform.position, baseCombatEntity.transform.position) <= 1.7f)
				{
					baseCombatEntity.Hurt(35f, DamageType.Bite, Owner);
				}
				if (target2 is BasePlayer basePlayer && Vector3.Distance(Owner.transform.position, basePlayer.transform.position) <= 1f)
				{
					basePlayer.ClientRPC(RpcTarget.Player("RPC_DoPush", basePlayer), Owner.transform.forward * 10f + Vector3.up * 3f);
				}
			}
		}
		else if (phase == Phase.PostJumpBack)
		{
			Owner.ClientRPC(RpcTarget.NetworkGroup("CL_SetFloorSnappingEnabled"), arg1: true);
		}
	}

	private Vector3 ThreePointLerp(Vector3 a, Vector3 b, Vector3 c, float t)
	{
		return Vector3.Lerp(Vector3.Lerp(a, b, t), Vector3.Lerp(b, c, t), t);
	}

	public override EFSMStateStatus OnStateUpdate(float deltaTime)
	{
		elapsedTime += deltaTime;
		float num = elapsedTime / Mathf.Max(animClip.inPlaceAnimation.length, 0.001f);
		if (phase == Phase.PreJump)
		{
			Quaternion b = Quaternion.LookRotation((destination - Owner.transform.position).WithY(0f));
			Owner.transform.rotation = Quaternion.Slerp(Owner.transform.rotation, b, 2f * deltaTime);
			float num2 = animClip.zMotionCurve.Evaluate(elapsedTime);
			Vector3 vector = Owner.transform.forward * (num2 - previousOffsetZ);
			previousOffsetZ = num2;
			Owner.transform.position += vector;
			if (num >= 0.29f)
			{
				SetPhase(Phase.Jump);
			}
		}
		if (phase == Phase.Jump)
		{
			Vector3 b2 = (startLocation + destination) * 0.5f;
			b2.y = Mathf.Max(startLocation.y, destination.y);
			float t = Mathx.RemapValClamped(num, 0.29f, 0.395f, 0f, 1f);
			Vector3 position = ThreePointLerp(startLocation, b2, destination, t);
			Owner.transform.position = position;
			if (num >= 0.395f)
			{
				SetPhase(Phase.Attack);
			}
		}
		if (phase == Phase.Attack)
		{
			Owner.transform.rotation = startRotation * Quaternion.AngleAxis(animClip.yRotationCurve.Evaluate(elapsedTime), Vector3.up);
			if (num > 0.67f)
			{
				SetPhase(Phase.JumpBack);
			}
		}
		if (phase == Phase.JumpBack)
		{
			Vector3 b3 = (startLocation + destination) * 0.5f;
			b3.y = Mathf.Max(startLocation.y, destination.y);
			float t2 = Mathx.RemapValClamped(num, 0.67f, 0.765f, 0f, 1f);
			Vector3 position2 = ThreePointLerp(destination, b3, startLocation, t2);
			Owner.transform.position = position2;
			Owner.transform.rotation = Quaternion.LookRotation((startLocation - destination).WithY(0f));
			if (num >= 0.765f)
			{
				SetPhase(Phase.PostJumpBack);
			}
		}
		if (phase == Phase.PostJumpBack)
		{
			float num3 = animClip.zMotionCurve.Evaluate(elapsedTime);
			Vector3 vector2 = Owner.transform.forward * (num3 - previousOffsetZ);
			previousOffsetZ = num3;
			Owner.transform.position -= vector2;
		}
		if (num >= 0.95f)
		{
			return EFSMStateStatus.Success;
		}
		return base.OnStateUpdate(deltaTime);
	}

	public override void OnStateExit()
	{
		Owner.GetComponent<NavMeshAgent>().enabled = true;
		base.Senses.UnlockTarget(ref targetLock);
		base.Agent.Unpause(ref movementLock);
		if (phase != Phase.PostJumpBack)
		{
			Owner.ClientRPC(RpcTarget.NetworkGroup("CL_SetFloorSnappingEnabled"), arg1: true);
		}
		base.OnStateExit();
	}
}
