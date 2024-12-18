using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public class State_CircleDynamic : FSMStateBase, IParametrized<BaseEntity>
{
	[SerializeField]
	private LimitedTurnNavAgent.Speeds minSpeed;

	[SerializeField]
	private LimitedTurnNavAgent.Speeds maxSpeed = LimitedTurnNavAgent.Speeds.Sprint;

	[SerializeField]
	protected Vector2 distanceSpeedRange = new Vector2(10f, 50f);

	[SerializeField]
	private Vector2 angleRange = new Vector3(20f, 80f);

	[SerializeField]
	private Vector2 angleDurationRange = new Vector2(1f, 3f);

	[SerializeField]
	private Vector2 burstDurationRange = new Vector2(1f, 3f);

	[SerializeField]
	private Vector2 burstCooldownRange = new Vector2(1f, 10f);

	private Action _updateBurstAction;

	private Action _endBurstAction;

	private Action _updateAngleAction;

	private bool clockWise = true;

	private int burstSpeedIndexOffset;

	private float randomAngle;

	private Action UpdateBurstAction => UpdateBurst;

	private Action EndBurstAction => EndBurst;

	private Action UpdateAngleAction => UpdateAngle;

	public void SetParameter(BaseEntity target)
	{
		base.Senses.TrySetTarget(target);
	}

	public override EFSMStateStatus OnStateEnter()
	{
		clockWise = UnityEngine.Random.value > 0.5f;
		EndBurst();
		UpdateAngle();
		return base.OnStateEnter();
	}

	public override void OnStateExit()
	{
		base.Agent.ResetPath();
		Owner.CancelInvoke(UpdateBurstAction);
		Owner.CancelInvoke(EndBurstAction);
		Owner.CancelInvoke(UpdateAngleAction);
		base.OnStateExit();
	}

	private void UpdateAngle()
	{
		randomAngle = UnityEngine.Random.Range(angleRange.x, angleRange.y) * (float)(clockWise ? 1 : (-1));
		Owner.Invoke(UpdateAngleAction, UnityEngine.Random.Range(angleDurationRange.x, angleDurationRange.y));
	}

	private void UpdateBurst()
	{
		burstSpeedIndexOffset = 2;
		clockWise = UnityEngine.Random.value > 0.5f;
		float time = UnityEngine.Random.Range(burstDurationRange.x, burstDurationRange.y);
		Owner.Invoke(EndBurstAction, time);
	}

	private void EndBurst()
	{
		burstSpeedIndexOffset = 0;
		float time = UnityEngine.Random.Range(burstCooldownRange.x, burstCooldownRange.y);
		Owner.Invoke(UpdateBurstAction, time);
	}

	public override EFSMStateStatus OnStateUpdate(float deltaTime)
	{
		if (!base.Senses.FindTargetPosition(out var targetPosition))
		{
			return EFSMStateStatus.Failure;
		}
		float num = Vector3.Distance(Owner.transform.position, targetPosition);
		float ratio = Mathf.InverseLerp(distanceSpeedRange.x, distanceSpeedRange.y, num);
		base.Agent.SetSpeed(ratio, minSpeed, maxSpeed, burstSpeedIndexOffset);
		float currentDeviation = Mathx.RemapValClamped(num, distanceSpeedRange.x, distanceSpeedRange.y, randomAngle, 0f);
		base.Agent.currentDeviation = currentDeviation;
		Vector3 newDestination = targetPosition;
		if (!base.Agent.SetDestination(newDestination))
		{
			return EFSMStateStatus.Failure;
		}
		return base.OnStateUpdate(deltaTime);
	}
}
