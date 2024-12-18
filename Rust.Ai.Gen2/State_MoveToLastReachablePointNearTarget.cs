using UnityEngine;

namespace Rust.Ai.Gen2;

public class State_MoveToLastReachablePointNearTarget : State_MoveToTarget
{
	private const float maxHorizontalDist = 7f;

	private const float maxVerticalDist = 2.7f;

	private const float traceVerticalOffset = 1f;

	private Vector3 reachableDestination;

	private LockState.LockHandle targetLock;

	public static bool CanJumpFromPosToPos(BaseEntity owner, Vector3 ownerLocation, Vector3 targetPos)
	{
		if (Mathf.Abs(targetPos.y - ownerLocation.y) > 2.7f)
		{
			return false;
		}
		if (Vector3.Distance(ownerLocation, targetPos) > 7f)
		{
			return false;
		}
		if (!owner.CanSee(ownerLocation + 1f * Vector3.up, targetPos + 1f * Vector3.up))
		{
			return false;
		}
		return true;
	}

	public override EFSMStateStatus OnStateEnter()
	{
		if (!FindReachableLocation(out reachableDestination))
		{
			return EFSMStateStatus.Failure;
		}
		targetLock = base.Senses.LockCurrentTarget();
		base.Agent.deceleration.Value = 6f;
		return base.OnStateEnter();
	}

	private bool FindReachableLocation(out Vector3 location)
	{
		location = default(Vector3);
		if (!base.Senses.FindTarget(out var target) || !(target is BasePlayer basePlayer))
		{
			return false;
		}
		if (basePlayer.isMounted)
		{
			return false;
		}
		Vector3 position = target.transform.position;
		if (Vector3.Distance(Owner.transform.position, position) > 50f)
		{
			return false;
		}
		Vector3? vector = null;
		if (base.Agent.lastValidDestination.HasValue && Vector3.Distance(base.Agent.lastValidDestination.Value, position) <= 7f && base.Agent.SamplePosition(base.Agent.lastValidDestination.Value, out var sample, 7f) && CanJumpFromPosToPos(Owner, sample, position))
		{
			vector = sample;
		}
		if (!vector.HasValue && base.Agent.SamplePosition(position, out var sample2, 7f) && CanJumpFromPosToPos(Owner, sample2, position))
		{
			vector = sample2;
		}
		if (!vector.HasValue)
		{
			return false;
		}
		location = vector.Value;
		return true;
	}

	protected override bool GetMoveDestination(out Vector3 destination)
	{
		destination = reachableDestination;
		return true;
	}

	public override EFSMStateStatus OnStateUpdate(float deltaTime)
	{
		float ratio = Mathx.RemapValClamped(Vector3.Distance(Owner.transform.position, reachableDestination), 4f, 16f, 0f, 1f);
		if (Trans_TargetIsNearFire.Test(Owner, base.Senses))
		{
			base.Agent.SetSpeed(ratio, LimitedTurnNavAgent.Speeds.Sneak, LimitedTurnNavAgent.Speeds.Jog);
		}
		else
		{
			base.Agent.SetSpeed(ratio, LimitedTurnNavAgent.Speeds.Run);
		}
		return base.OnStateUpdate(deltaTime);
	}

	public override void OnStateExit()
	{
		base.OnStateExit();
		base.Senses.UnlockTarget(ref targetLock);
		base.Agent.deceleration.Reset();
	}
}
