using System;
using System.Collections.Generic;
using UnityEngine;

namespace FIMSpace.GroundFitter;

public abstract class FGroundFitter_Base : MonoBehaviour
{
	[Header("> Main Variables <", order = 0)]
	[Space(4f, order = 1)]
	[Tooltip("How quick rotation should be corrected to target")]
	[Range(1f, 30f)]
	public float FittingSpeed = 6f;

	[Tooltip("Smoothing whole rotation motion")]
	[Range(0f, 1f)]
	public float TotalSmoother;

	[Space(3f)]
	[Tooltip("Transform which will be rotated by script, usually it can be the same transform as component's")]
	[HideInInspector]
	public Transform TransformToRotate;

	[Space(3f)]
	[Tooltip("If you want this script only to change your object's rotation and do nothing with position, untoggle this")]
	public bool GlueToGround;

	[Header("> Tweaking Settings <", order = 0)]
	[Space(4f, order = 1)]
	[Range(0f, 1f)]
	[Tooltip("If forward/pitch rotation value should go in lighter value than real normal hit direction")]
	public float MildForwardValue;

	[Tooltip("Maximum rotation angle in rotation of x/pitch axis, so rotating forward - degrees value of maximum rotation")]
	[Range(0f, 90f)]
	public float MaxForwardRotation = 90f;

	[Space(5f)]
	[Range(0f, 1f)]
	[Tooltip("If side rotation value/roll should go in lighter value than real normal hit direction")]
	public float MildHorizontalValue;

	[Tooltip("Max roll rotation. If rotation should work on also on x axis - good for spiders, can look wrong on quadropeds etc.")]
	[Range(0f, 90f)]
	public float MaxHorizontalRotation = 90f;

	[Header("> Advanced settings <", order = 0)]
	[Space(4f, order = 1)]
	[Tooltip("We should cast raycast from position little higher than foots of your game object")]
	public float RaycastHeightOffset = 0.5f;

	[Tooltip("How far ray should cast to check if ground is under feet")]
	public float RaycastCheckRange = 5f;

	[Tooltip("If value is not equal 0 there will be casted second ray in front or back of gameObject")]
	public float LookAheadRaycast;

	[Tooltip("Blending with predicted forward raycast rotation")]
	public float AheadBlend = 0.5f;

	[Tooltip("Offset over ground")]
	[HideInInspector]
	public float UpOffset;

	[Space(8f)]
	[Tooltip("What collision layers should be included by algorithm")]
	public LayerMask GroundLayerMask = 1;

	[Tooltip("When casting down vector should adjust with transform's rotation")]
	public bool RelativeLookUp;

	[Range(0f, 1f)]
	public float RelativeLookUpBias;

	internal Vector3 WorldUp = Vector3.up;

	[Space(8f)]
	[Tooltip("Casting more raycsts under object to detect ground more precisely, then we use average from all casts to set new rotation")]
	public bool ZoneCast;

	public Vector2 ZoneCastDimensions = new Vector2(0.3f, 0.5f);

	public Vector3 ZoneCastOffset = Vector3.zero;

	[Range(0f, 10f)]
	public float ZoneCastBias;

	[Range(0f, 1f)]
	[Tooltip("More precision = more raycasts = lower performance")]
	public float ZoneCastPrecision = 0.25f;

	[NonSerialized]
	public float UpAxisRotation;

	protected Quaternion helperRotation = Quaternion.identity;

	protected Collider selfCollider;

	protected Vector3 castOffset = Vector3.zero;

	protected float deltaTime;

	internal bool ApplyRotation = true;

	internal Quaternion targetRotationToApply = Quaternion.identity;

	public RaycastHit LastRaycast { get; protected set; }

	public Vector3 LastRaycastOrigin { get; protected set; }

	public RaycastHit LastTransformRaycast { get; protected set; }

	public Quaternion LastRotation { get; protected set; }

	protected virtual void Start()
	{
		selfCollider = GetComponent<Collider>();
		if (TransformToRotate == null)
		{
			TransformToRotate = base.transform;
		}
		UpAxisRotation = base.transform.localEulerAngles.y;
	}

	protected virtual void Reset()
	{
		TransformToRotate = base.transform;
	}

	protected virtual void FitToGround()
	{
		if ((bool)selfCollider)
		{
			selfCollider.enabled = false;
		}
		RaycastHit hitInfo = default(RaycastHit);
		if (LookAheadRaycast != 0f)
		{
			Physics.Raycast(TransformToRotate.position + GetUpVector(RaycastHeightOffset) + TransformToRotate.forward * LookAheadRaycast, -GetUpVector(), out hitInfo, RaycastCheckRange, GroundLayerMask, QueryTriggerInteraction.Ignore);
		}
		RefreshLastRaycast();
		if ((bool)LastRaycast.transform)
		{
			Quaternion quaternion = Quaternion.FromToRotation(Vector3.up, LastRaycast.normal);
			if ((bool)hitInfo.transform)
			{
				Quaternion b = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
				quaternion = Quaternion.Lerp(quaternion, b, AheadBlend);
			}
			helperRotation = Quaternion.Slerp(helperRotation, quaternion, deltaTime * FittingSpeed);
		}
		else
		{
			helperRotation = Quaternion.Slerp(helperRotation, Quaternion.identity, deltaTime * FittingSpeed);
		}
		RotationCalculations();
		if (GlueToGround && (bool)LastRaycast.transform)
		{
			TransformToRotate.position = LastRaycast.point + Vector3.up * UpOffset;
		}
		if ((bool)selfCollider)
		{
			selfCollider.enabled = true;
		}
	}

	internal virtual void RotationCalculations()
	{
		Quaternion quaternion = helperRotation;
		quaternion = Quaternion.Euler(Mathf.Clamp(FLogicMethods.WrapAngle(quaternion.eulerAngles.x), 0f - MaxForwardRotation, MaxForwardRotation) * (1f - MildForwardValue), quaternion.eulerAngles.y, Mathf.Clamp(FLogicMethods.WrapAngle(quaternion.eulerAngles.z), 0f - MaxHorizontalRotation, MaxHorizontalRotation) * (1f - MildHorizontalValue));
		if (TotalSmoother == 0f)
		{
			targetRotationToApply = quaternion * Quaternion.AngleAxis(UpAxisRotation, Vector3.up);
		}
		else
		{
			Quaternion quaternion2 = Quaternion.AngleAxis(UpAxisRotation, Vector3.up);
			targetRotationToApply *= Quaternion.Inverse(quaternion2);
			targetRotationToApply = Quaternion.Slerp(targetRotationToApply, quaternion, deltaTime * Mathf.Lerp(50f, 1f, TotalSmoother));
			targetRotationToApply *= quaternion2;
		}
		if (ApplyRotation)
		{
			TransformToRotate.rotation = targetRotationToApply;
		}
		LastRotation = TransformToRotate.rotation;
	}

	internal virtual RaycastHit CastRay()
	{
		LastRaycastOrigin = GetRaycastOrigin() + castOffset;
		Physics.Raycast(LastRaycastOrigin, -GetUpVector(), out var hitInfo, RaycastCheckRange + Mathf.Abs(UpOffset), GroundLayerMask, QueryTriggerInteraction.Ignore);
		if (ZoneCast)
		{
			Vector3 vector = TransformToRotate.position + GetRotation() * ZoneCastOffset + GetUpVector(RaycastHeightOffset);
			Vector3 vector2 = TransformToRotate.right * ZoneCastDimensions.x;
			Vector3 vector3 = TransformToRotate.forward * ZoneCastDimensions.y;
			List<RaycastHit> list = new List<RaycastHit>();
			list.Add(hitInfo);
			int num = 0;
			float num2 = 1f;
			for (int i = 0; (float)i < Mathf.Lerp(4f, 24f, ZoneCastPrecision); i++)
			{
				Vector3 vector4 = Vector3.zero;
				switch (num)
				{
				case 0:
					vector4 = vector2 - vector3;
					break;
				case 1:
					vector4 = vector2 + vector3;
					break;
				case 2:
					vector4 = -vector2 + vector3;
					break;
				case 3:
					vector4 = -vector2 - vector3;
					num2 += 0.75f;
					num = -1;
					break;
				}
				Physics.Raycast(vector + vector4 / num2, -GetUpVector() + vector4 * ZoneCastBias + castOffset, out var hitInfo2, RaycastCheckRange + Mathf.Abs(UpOffset), GroundLayerMask, QueryTriggerInteraction.Ignore);
				if ((bool)hitInfo2.transform)
				{
					list.Add(hitInfo2);
				}
				num++;
			}
			Vector3 zero = Vector3.zero;
			Vector3 zero2 = Vector3.zero;
			for (int j = 0; j < list.Count; j++)
			{
				zero2 += list[j].normal;
				zero += list[j].point;
			}
			zero /= (float)list.Count;
			zero2 /= (float)list.Count;
			hitInfo.normal = zero2;
			if (!hitInfo.transform)
			{
				hitInfo.point = new Vector3(zero.x, TransformToRotate.position.y, zero.z);
			}
		}
		return hitInfo;
	}

	internal Vector3 GetRaycastOrigin()
	{
		return TransformToRotate.position + GetUpVector() * RaycastHeightOffset;
	}

	protected virtual Quaternion GetRotation()
	{
		return TransformToRotate.rotation;
	}

	protected virtual Vector3 GetUpVector(float mulRange = 1f)
	{
		if (RelativeLookUp)
		{
			return Vector3.Lerp(WorldUp, TransformToRotate.TransformDirection(Vector3.up).normalized, RelativeLookUpBias) * mulRange;
		}
		return WorldUp * mulRange;
	}

	internal void RotateBack(float speed = 5f)
	{
		if (!(speed <= 0f))
		{
			helperRotation = Quaternion.Slerp(helperRotation, Quaternion.identity, deltaTime * speed);
		}
	}

	internal void RefreshLastRaycast()
	{
		LastRaycast = CastRay();
		if ((bool)LastRaycast.transform)
		{
			LastTransformRaycast = LastRaycast;
		}
	}

	internal void BackRaycast()
	{
		LastRaycast = LastTransformRaycast;
	}
}
