using FIMSpace.Basics;
using UnityEngine;
using UnityEngine.AI;

namespace FIMSpace.GroundFitter;

[RequireComponent(typeof(NavMeshAgent))]
public class FGroundFitter_Demo_NavMesh : MonoBehaviour
{
	public FGroundFitter_Base TargetGroundFitter;

	[Range(0.5f, 50f)]
	public float RotationSpeed = 3f;

	[Range(0f, 1f)]
	[Tooltip("Moving Accordingly to rotation after acceleration")]
	public float DirectMovement = 0.8f;

	public float AnimationSpeedScale = 1f;

	private NavMeshAgent agent;

	private FAnimationClips animationClips;

	private bool reachedDestination;

	private Vector3 lastAgentPosition;

	private string movementClip;

	private float dirMov;

	private float sd_dirMov;

	public bool moving { get; private set; }

	private void Reset()
	{
		TargetGroundFitter = GetComponent<FGroundFitter_Base>();
		if ((bool)TargetGroundFitter)
		{
			TargetGroundFitter.GlueToGround = false;
		}
		agent = GetComponent<NavMeshAgent>();
		if ((bool)agent)
		{
			agent.acceleration = 1000f;
			agent.angularSpeed = 100f;
		}
	}

	protected virtual void Start()
	{
		if (TargetGroundFitter == null)
		{
			TargetGroundFitter = GetComponent<FGroundFitter_Base>();
		}
		if ((bool)TargetGroundFitter)
		{
			TargetGroundFitter.GlueToGround = false;
		}
		agent = GetComponent<NavMeshAgent>();
		agent.Warp(base.transform.position);
		agent.SetDestination(base.transform.position);
		moving = false;
		lastAgentPosition = base.transform.position;
		reachedDestination = true;
		animationClips = new FAnimationClips(GetComponentInChildren<Animator>());
		animationClips.AddClip("Idle");
		if (animationClips.Animator.StateExists("Move") || animationClips.Animator.StateExists("move"))
		{
			movementClip = "Move";
		}
		else
		{
			movementClip = "Walk";
		}
		animationClips.AddClip(movementClip);
	}

	protected virtual void Update()
	{
		animationClips.SetFloat("AnimationSpeed", agent.desiredVelocity.magnitude * AnimationSpeedScale, 8f);
		IsMovingCheck();
		Vector3 vector = agent.nextPosition - lastAgentPosition;
		float magnitude = agent.velocity.magnitude;
		_ = vector.normalized;
		Vector3 vector2 = agent.nextPosition;
		if (DirectMovement > 0f)
		{
			if (magnitude > 0f)
			{
				Vector3 b = lastAgentPosition + base.transform.forward * magnitude * Time.deltaTime;
				float smoothTime = 0.25f;
				float target = 1f;
				if (agent.remainingDistance <= agent.stoppingDistance * 1.1f + 0.1f)
				{
					smoothTime = 0.1f;
					target = 0f;
				}
				dirMov = Mathf.SmoothDamp(dirMov, target, ref sd_dirMov, smoothTime, 1000f, Time.deltaTime);
				vector2 = Vector3.LerpUnclamped(vector2, b, dirMov);
			}
			else
			{
				dirMov = Mathf.SmoothDamp(dirMov, 0f, ref sd_dirMov, 0.1f, 1000f, Time.deltaTime);
			}
		}
		vector2.y = agent.nextPosition.y;
		base.transform.position = vector2;
		if (moving)
		{
			Vector3 vector3 = agent.nextPosition + agent.desiredVelocity;
			float y = Quaternion.LookRotation(new Vector3(vector3.x, 0f, vector3.z) - base.transform.position).eulerAngles.y;
			TargetGroundFitter.UpAxisRotation = Mathf.LerpAngle(TargetGroundFitter.UpAxisRotation, y, Time.deltaTime * RotationSpeed);
		}
		lastAgentPosition = vector2;
	}

	private bool IsMovingCheck()
	{
		bool num = moving;
		moving = true;
		if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance && (!agent.hasPath || agent.velocity.sqrMagnitude == 0f))
		{
			if (!reachedDestination)
			{
				OnReachDestination();
			}
			moving = false;
		}
		if (num != moving)
		{
			OnStartMoving();
		}
		return moving;
	}

	protected virtual void OnReachDestination()
	{
		reachedDestination = true;
		animationClips.CrossFadeInFixedTime("Idle");
	}

	protected virtual void OnStartMoving()
	{
		reachedDestination = false;
		animationClips.CrossFadeInFixedTime(movementClip);
	}
}
