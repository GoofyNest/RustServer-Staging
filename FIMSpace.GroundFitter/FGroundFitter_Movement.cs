using System;
using FIMSpace.Basics;
using UnityEngine;

namespace FIMSpace.GroundFitter;

[RequireComponent(typeof(FGroundFitter))]
public class FGroundFitter_Movement : MonoBehaviour
{
	[Header("> Main Tweak Variables <")]
	public float BaseSpeed = 3f;

	public float RotateToTargetSpeed = 6f;

	public float SprintingSpeed = 10f;

	protected float ActiveSpeed;

	public float AccelerationSpeed = 10f;

	public float DecelerationSpeed = 10f;

	[Header("> Additional Options <")]
	public float JumpPower = 7f;

	public float gravity = 15f;

	public bool MultiplySprintAnimation;

	[Range(0f, 20f)]
	public float RotateBackInAir;

	[Tooltip("Protecting from going through walls when slope is very big and ground fitter is jumping into it")]
	public bool NotFallingThrough;

	[Tooltip("You need collider and rigidbody on object to make it work right - ALSO CHANGE YOUR CAMERA UPDATE CLOCK TO FIXED UPDATE AND USE TIME.fixedDeltaTime - ! For now it can cause errors when jumping, character can go through floor sometimes ! - Will be upgraded in future versions")]
	[Header("(experimental)")]
	public bool UsePhysics;

	[Tooltip("Disabling translating object from code and running animation without need to hold minimum movement speed")]
	public bool UseRootMotionTranslation;

	public bool UseRootMotionRotation;

	internal float YVelocity;

	protected bool inAir;

	protected float gravitUpOffset;

	internal Vector3 lastNotZeroMoveVector = Vector3.zero;

	internal Vector3 MoveVector = Vector3.zero;

	internal bool Sprint;

	internal float RotationOffset;

	protected string lastAnim = "";

	protected Animator animator;

	protected FGroundFitter fitter;

	protected Rigidbody rigb;

	protected bool animatorHaveAnimationSpeedProp;

	protected float initialUpOffset;

	protected Vector3 holdJumpPosition;

	protected float freezeJumpYPosition;

	protected float delta;

	protected Vector3 lastVelocity;

	protected Collider itsCollider;

	protected FAnimationClips clips;

	internal static int _hash_animSp = Animator.StringToHash("AnimationSpeed");

	private int _hash_IsGrounded = -1;

	private int _hash_IsMov = -1;

	private bool slidingAssigned;

	private float? yAdjustPos;

	[Tooltip("If you want to set some animator parameter during being grounded")]
	[HideInInspector]
	public string SetIsGroundedParam = "";

	[Tooltip("If you want to set some animator parameter during accelerating moving")]
	[HideInInspector]
	public string SetIsMovingParam = "";

	[Tooltip("If using physical move with collider, assigning to the collider sliding material")]
	[HideInInspector]
	public bool UseSlidingMat = true;

	private static PhysicMaterial pm_Sliding = null;

	private void Reset()
	{
		if (!base.gameObject.GetComponent<FGroundFitter_Input>())
		{
			base.gameObject.AddComponent<FGroundFitter_Input>();
		}
	}

	protected virtual void Start()
	{
		fitter = GetComponent<FGroundFitter>();
		animator = GetComponentInChildren<Animator>();
		rigb = GetComponent<Rigidbody>();
		itsCollider = GetComponentInChildren<Collider>();
		if (!string.IsNullOrEmpty(SetIsGroundedParam))
		{
			_hash_IsGrounded = Animator.StringToHash(SetIsGroundedParam);
		}
		if (!string.IsNullOrEmpty(SetIsMovingParam))
		{
			_hash_IsMov = Animator.StringToHash(SetIsMovingParam);
		}
		if ((bool)animator)
		{
			if (HasParameter(animator, "AnimationSpeed"))
			{
				animatorHaveAnimationSpeedProp = true;
			}
			animator.applyRootMotion = false;
		}
		fitter.UpAxisRotation = base.transform.rotation.eulerAngles.y;
		initialUpOffset = fitter.UpOffset;
		fitter.RefreshLastRaycast();
		clips = new FAnimationClips(animator);
		clips.AddClip("Idle");
		clips.AddClip("Walk");
		clips.AddClip("Run");
	}

	protected virtual void Update()
	{
		HandleBaseVariables();
		HandleGravity();
		HandleAnimations();
		HandleTransforming();
		if (!UsePhysics)
		{
			ApplyTransforming();
		}
	}

	protected virtual void FixedUpdate()
	{
		if ((bool)rigb)
		{
			if (UsePhysics)
			{
				rigb.useGravity = false;
				rigb.isKinematic = false;
				if (!slidingAssigned)
				{
					Collider componentInChildren = GetComponentInChildren<Collider>();
					if ((bool)componentInChildren)
					{
						if (pm_Sliding == null)
						{
							pm_Sliding = new PhysicMaterial("Sliding");
							pm_Sliding.bounciness = 0f;
							pm_Sliding.frictionCombine = PhysicMaterialCombine.Minimum;
							pm_Sliding.dynamicFriction = 0f;
							pm_Sliding.staticFriction = 0f;
						}
						componentInChildren.material = pm_Sliding;
						slidingAssigned = true;
					}
				}
			}
			else
			{
				rigb.isKinematic = true;
			}
		}
		if (!UsePhysics)
		{
			fitter.ApplyRotation = true;
			return;
		}
		ApplyTransforming();
		rigb.angularVelocity = Vector3.zero;
		rigb.freezeRotation = true;
		fitter.ApplyRotation = false;
		rigb.rotation = fitter.targetRotationToApply;
	}

	protected virtual void HandleBaseVariables()
	{
		delta = Time.deltaTime;
		if (UseRootMotionTranslation)
		{
			fitter.HandleRootMotion = false;
			if (animator.gameObject != base.gameObject && !animator.applyRootMotion && !animator.GetComponent<FGroundFitter_RootMotionHelper>())
			{
				animator.gameObject.AddComponent<FGroundFitter_RootMotionHelper>().MovementController = this;
			}
			fitter.UpdateClock = EFUpdateClock.LateUpdate;
			animator.applyRootMotion = true;
		}
		else
		{
			animator.applyRootMotion = false;
		}
	}

	protected virtual void HandleGravity()
	{
		if (fitter.enabled)
		{
			if (fitter.UpOffset > initialUpOffset)
			{
				fitter.UpOffset += YVelocity * delta;
			}
			else
			{
				fitter.UpOffset = initialUpOffset;
			}
		}
		else
		{
			fitter.UpOffset += YVelocity * delta;
		}
		if (inAir)
		{
			YVelocity -= gravity * delta;
			fitter.RefreshDelta();
			fitter.RotateBack(RotateBackInAir);
		}
		if (fitter.enabled)
		{
			if (!fitter.LastRaycast.transform)
			{
				if (!inAir)
				{
					inAir = true;
					holdJumpPosition = base.transform.position;
					freezeJumpYPosition = holdJumpPosition.y;
					YVelocity = -1f;
					fitter.enabled = false;
				}
			}
			else if (YVelocity > 0f)
			{
				inAir = true;
			}
		}
		if (!inAir)
		{
			return;
		}
		if (fitter.enabled)
		{
			fitter.enabled = false;
		}
		if (YVelocity < 0f)
		{
			RaycastHit raycastHit = fitter.CastRay();
			if ((bool)raycastHit.transform && base.transform.position.y + YVelocity * delta <= raycastHit.point.y + initialUpOffset + 0.05f)
			{
				fitter.UpOffset -= raycastHit.point.y - freezeJumpYPosition;
				HitGround();
			}
		}
		else
		{
			RaycastHit raycastHit2 = fitter.CastRay();
			if ((bool)raycastHit2.transform && raycastHit2.point.y - 0.1f > base.transform.position.y)
			{
				fitter.UpOffset = initialUpOffset;
				YVelocity = -1f;
				HitGround();
			}
		}
		if (NotFallingThrough && inAir)
		{
			Vector3 forward = fitter.transform.forward;
			float raycastCheckRange = fitter.RaycastCheckRange;
			if (Physics.Raycast(fitter.GetRaycastOrigin() - forward * raycastCheckRange * 0.1f, forward, raycastCheckRange * 1.11f, fitter.GroundLayerMask, QueryTriggerInteraction.Ignore))
			{
				float raycastCheckRange2 = fitter.RaycastCheckRange;
				fitter.RaycastCheckRange *= 100f;
				fitter.UpOffset = initialUpOffset;
				YVelocity = -1f;
				HitGround();
				fitter.RaycastCheckRange = raycastCheckRange2;
			}
		}
	}

	protected virtual void HandleAnimations()
	{
		float value = 1f;
		if (ActiveSpeed > 0.15f)
		{
			if (ActiveSpeed > (BaseSpeed + SprintingSpeed) * 0.25f)
			{
				value = ActiveSpeed / SprintingSpeed;
				CrossfadeTo("Run");
			}
			else
			{
				value = ActiveSpeed / BaseSpeed;
				CrossfadeTo("Walk");
			}
		}
		else
		{
			CrossfadeTo("Idle");
		}
		if (animatorHaveAnimationSpeedProp)
		{
			if (inAir)
			{
				animator.LerpFloatValue("AnimationSpeed");
			}
			else
			{
				animator.LerpFloatValue("AnimationSpeed", value);
			}
		}
		if ((bool)animator)
		{
			if (_hash_IsGrounded != -1)
			{
				animator.SetBool(_hash_IsGrounded, !inAir);
			}
			if (_hash_IsMov != -1)
			{
				animator.SetBool(_hash_IsMov, MoveVector != Vector3.zero);
			}
		}
	}

	protected void RefreshHitGroundVars(RaycastHit hit)
	{
		holdJumpPosition = hit.point;
		freezeJumpYPosition = hit.point.y;
		fitter.UpOffset = Mathf.Abs(hit.point.y - base.transform.position.y);
	}

	protected virtual void HandleTransforming()
	{
		if (!UseRootMotionTranslation)
		{
			lastVelocity = base.transform.TransformDirection(lastNotZeroMoveVector) * ActiveSpeed;
		}
		if (fitter.enabled)
		{
			if ((bool)fitter.LastRaycast.transform)
			{
				Vector3 position = fitter.LastRaycast.point + fitter.UpOffset * Vector3.up;
				if (!UsePhysics)
				{
					base.transform.position = position;
				}
				else if ((bool)rigb)
				{
					yAdjustPos = position.y;
				}
				holdJumpPosition = base.transform.position;
				freezeJumpYPosition = holdJumpPosition.y;
			}
			else
			{
				inAir = true;
			}
		}
		else
		{
			holdJumpPosition.y = freezeJumpYPosition + fitter.UpOffset;
		}
		if (MoveVector != Vector3.zero)
		{
			if (!UseRootMotionRotation)
			{
				if (!fitter.enabled)
				{
					fitter.UpAxisRotation = Mathf.LerpAngle(fitter.UpAxisRotation, Camera.main.transform.eulerAngles.y + RotationOffset, delta * RotateToTargetSpeed * 0.15f);
					fitter.RotationCalculations();
				}
				else
				{
					fitter.UpAxisRotation = Mathf.LerpAngle(fitter.UpAxisRotation, Camera.main.transform.eulerAngles.y + RotationOffset, delta * RotateToTargetSpeed);
				}
			}
			if (!Sprint)
			{
				ActiveSpeed = Mathf.Lerp(ActiveSpeed, BaseSpeed, delta * AccelerationSpeed);
			}
			else
			{
				ActiveSpeed = Mathf.Lerp(ActiveSpeed, SprintingSpeed, delta * AccelerationSpeed);
			}
		}
		else if (ActiveSpeed > 0f)
		{
			ActiveSpeed = Mathf.Lerp(ActiveSpeed, -0.01f, delta * DecelerationSpeed);
		}
		else
		{
			ActiveSpeed = 0f;
		}
		holdJumpPosition += lastVelocity * delta;
		if (MoveVector != Vector3.zero)
		{
			lastNotZeroMoveVector = MoveVector;
		}
	}

	private void ApplyTransforming()
	{
		if (UsePhysics && (bool)rigb)
		{
			float y = YVelocity;
			if (!inAir && yAdjustPos.HasValue)
			{
				y = (yAdjustPos.Value - rigb.position.y) / Time.fixedDeltaTime;
			}
			rigb.velocity = new Vector3(lastVelocity.x, y, lastVelocity.z);
		}
		else
		{
			base.transform.position = holdJumpPosition;
		}
	}

	internal virtual void OnAnimatorMove()
	{
		if (UseRootMotionTranslation)
		{
			if (!inAir)
			{
				lastVelocity = animator.velocity;
			}
			animator.rootPosition = base.transform.position;
			animator.rootRotation = fitter.LastRotation;
		}
		if (UseRootMotionRotation)
		{
			animator.rootRotation = fitter.LastRotation;
			animator.deltaRotation.ToAngleAxis(out var angle, out var axis);
			float y = (axis * angle * (MathF.PI / 180f)).y;
			fitter.UpAxisRotation += y / Time.deltaTime;
		}
	}

	protected virtual void HitGround()
	{
		fitter.RefreshLastRaycast();
		fitter.enabled = true;
		inAir = false;
		freezeJumpYPosition = 0f;
	}

	public virtual void Jump()
	{
		YVelocity = JumpPower;
		fitter.UpOffset += JumpPower * Time.deltaTime / 2f;
	}

	protected virtual void CrossfadeTo(string animation, float transitionTime = 0.25f)
	{
		if (!clips.ContainsKey(animation))
		{
			if (!(animation == "Run"))
			{
				return;
			}
			animation = "Walk";
		}
		if (lastAnim != animation)
		{
			animator.CrossFadeInFixedTime(clips[animation], transitionTime);
			lastAnim = animation;
		}
	}

	public static bool HasParameter(Animator animator, string paramName)
	{
		AnimatorControllerParameter[] parameters = animator.parameters;
		for (int i = 0; i < parameters.Length; i++)
		{
			if (parameters[i].name == paramName)
			{
				return true;
			}
		}
		return false;
	}
}
