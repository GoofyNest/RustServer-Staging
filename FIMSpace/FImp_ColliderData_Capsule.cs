using UnityEngine;

namespace FIMSpace;

public class FImp_ColliderData_Capsule : FImp_ColliderData_Base
{
	private Vector3 Top;

	private Vector3 Bottom;

	private Vector3 Direction;

	private float radius;

	private float scaleFactor;

	private float preRadius;

	public CapsuleCollider Capsule { get; private set; }

	public CapsuleCollider2D Capsule2D { get; private set; }

	public FImp_ColliderData_Capsule(CapsuleCollider collider)
	{
		Is2D = false;
		base.Transform = collider.transform;
		base.Collider = collider;
		base.Transform = collider.transform;
		Capsule = collider;
		base.ColliderType = EFColliderType.Capsule;
		CalculateCapsuleParameters(Capsule, ref Direction, ref radius, ref scaleFactor);
		RefreshColliderData();
	}

	public FImp_ColliderData_Capsule(CapsuleCollider2D collider)
	{
		Is2D = true;
		base.Transform = collider.transform;
		base.Collider2D = collider;
		base.Transform = collider.transform;
		Capsule2D = collider;
		base.ColliderType = EFColliderType.Capsule;
		CalculateCapsuleParameters(Capsule2D, ref Direction, ref radius, ref scaleFactor);
		RefreshColliderData();
	}

	public override void RefreshColliderData()
	{
		if (base.IsStatic)
		{
			return;
		}
		bool flag = false;
		if (!previousPosition.VIsSame(base.Transform.position))
		{
			flag = true;
		}
		else if (!base.Transform.rotation.QIsSame(previousRotation))
		{
			flag = true;
		}
		else if (!Is2D)
		{
			if (preRadius != Capsule.radius || !previousScale.VIsSame(base.Transform.lossyScale))
			{
				CalculateCapsuleParameters(Capsule, ref Direction, ref radius, ref scaleFactor);
			}
		}
		else if (preRadius != GetCapsule2DRadius(Capsule2D) || !previousScale.VIsSame(base.Transform.lossyScale))
		{
			CalculateCapsuleParameters(Capsule2D, ref Direction, ref radius, ref scaleFactor);
		}
		if (flag)
		{
			if (!Is2D)
			{
				GetCapsuleHeadsPositions(Capsule, ref Top, ref Bottom, Direction, radius, scaleFactor);
			}
			else
			{
				GetCapsuleHeadsPositions(Capsule2D, ref Top, ref Bottom, Direction, radius, scaleFactor);
			}
		}
		base.RefreshColliderData();
		previousPosition = base.Transform.position;
		previousRotation = base.Transform.rotation;
		previousScale = base.Transform.lossyScale;
		if (!Is2D)
		{
			preRadius = Capsule.radius;
		}
		else
		{
			preRadius = GetCapsule2DRadius(Capsule2D);
		}
	}

	public override bool PushIfInside(ref Vector3 point, float pointRadius, Vector3 pointOffset)
	{
		return PushOutFromCapsuleCollider(pointRadius, ref point, Top, Bottom, radius, pointOffset, Is2D);
	}

	public static bool PushOutFromCapsuleCollider(CapsuleCollider capsule, float segmentColliderRadius, ref Vector3 pos, Vector3 segmentOffset)
	{
		Vector3 direction = Vector3.zero;
		float trueRadius = capsule.radius;
		float scalerFactor = 1f;
		CalculateCapsuleParameters(capsule, ref direction, ref trueRadius, ref scalerFactor);
		Vector3 upper = Vector3.zero;
		Vector3 bottom = Vector3.zero;
		GetCapsuleHeadsPositions(capsule, ref upper, ref bottom, direction, trueRadius, scalerFactor);
		return PushOutFromCapsuleCollider(segmentColliderRadius, ref pos, upper, bottom, trueRadius, segmentOffset);
	}

	public static bool PushOutFromCapsuleCollider(float segmentColliderRadius, ref Vector3 segmentPos, Vector3 capSphereCenter1, Vector3 capSphereCenter2, float capsuleRadius, Vector3 segmentOffset, bool is2D = false)
	{
		float num = capsuleRadius + segmentColliderRadius;
		Vector3 vector = capSphereCenter2 - capSphereCenter1;
		Vector3 vector2 = segmentPos + segmentOffset - capSphereCenter1;
		if (is2D)
		{
			vector.z = 0f;
			vector2.z = 0f;
		}
		float num2 = Vector3.Dot(vector2, vector);
		if (num2 <= 0f)
		{
			float sqrMagnitude = vector2.sqrMagnitude;
			if (sqrMagnitude > 0f && sqrMagnitude < num * num)
			{
				segmentPos = capSphereCenter1 - segmentOffset + vector2 * (num / Mathf.Sqrt(sqrMagnitude));
				return true;
			}
		}
		else
		{
			float sqrMagnitude2 = vector.sqrMagnitude;
			if (num2 >= sqrMagnitude2)
			{
				vector2 = segmentPos + segmentOffset - capSphereCenter2;
				float sqrMagnitude3 = vector2.sqrMagnitude;
				if (sqrMagnitude3 > 0f && sqrMagnitude3 < num * num)
				{
					segmentPos = capSphereCenter2 - segmentOffset + vector2 * (num / Mathf.Sqrt(sqrMagnitude3));
					return true;
				}
			}
			else if (sqrMagnitude2 > 0f)
			{
				vector2 -= vector * (num2 / sqrMagnitude2);
				float sqrMagnitude4 = vector2.sqrMagnitude;
				if (sqrMagnitude4 > 0f && sqrMagnitude4 < num * num)
				{
					float num3 = Mathf.Sqrt(sqrMagnitude4);
					segmentPos += vector2 * ((num - num3) / num3);
					return true;
				}
			}
		}
		return false;
	}

	protected static void CalculateCapsuleParameters(CapsuleCollider capsule, ref Vector3 direction, ref float trueRadius, ref float scalerFactor)
	{
		Transform transform = capsule.transform;
		float num;
		if (capsule.direction == 1)
		{
			direction = Vector3.up;
			scalerFactor = transform.lossyScale.y;
			num = ((transform.lossyScale.x > transform.lossyScale.z) ? transform.lossyScale.x : transform.lossyScale.z);
		}
		else if (capsule.direction == 0)
		{
			direction = Vector3.right;
			scalerFactor = transform.lossyScale.x;
			num = ((transform.lossyScale.y > transform.lossyScale.z) ? transform.lossyScale.y : transform.lossyScale.z);
		}
		else
		{
			direction = Vector3.forward;
			scalerFactor = transform.lossyScale.z;
			num = ((transform.lossyScale.y > transform.lossyScale.x) ? transform.lossyScale.y : transform.lossyScale.x);
		}
		trueRadius = capsule.radius * num;
	}

	private static float GetCapsule2DRadius(CapsuleCollider2D capsule)
	{
		if (capsule.direction == CapsuleDirection2D.Vertical)
		{
			return capsule.size.x / 2f;
		}
		return capsule.size.y / 2f;
	}

	private static float GetCapsule2DHeight(CapsuleCollider2D capsule)
	{
		if (capsule.direction == CapsuleDirection2D.Vertical)
		{
			return capsule.size.y / 2f;
		}
		return capsule.size.x / 2f;
	}

	protected static void CalculateCapsuleParameters(CapsuleCollider2D capsule, ref Vector3 direction, ref float trueRadius, ref float scalerFactor)
	{
		Transform transform = capsule.transform;
		if (capsule.direction == CapsuleDirection2D.Vertical)
		{
			direction = Vector3.up;
			scalerFactor = transform.lossyScale.y;
			float num = ((transform.lossyScale.x > transform.lossyScale.z) ? transform.lossyScale.x : transform.lossyScale.z);
			trueRadius = capsule.size.x / 2f * num;
		}
		else if (capsule.direction == CapsuleDirection2D.Horizontal)
		{
			direction = Vector3.right;
			scalerFactor = transform.lossyScale.x;
			float num = ((transform.lossyScale.y > transform.lossyScale.z) ? transform.lossyScale.y : transform.lossyScale.z);
			trueRadius = capsule.size.y / 2f * num;
		}
	}

	protected static void GetCapsuleHeadsPositions(CapsuleCollider capsule, ref Vector3 upper, ref Vector3 bottom, Vector3 direction, float radius, float scalerFactor)
	{
		Vector3 direction2 = direction * (capsule.height / 2f * scalerFactor - radius);
		upper = capsule.transform.position + capsule.transform.TransformDirection(direction2) + capsule.transform.TransformVector(capsule.center);
		Vector3 direction3 = -direction * (capsule.height / 2f * scalerFactor - radius);
		bottom = capsule.transform.position + capsule.transform.TransformDirection(direction3) + capsule.transform.TransformVector(capsule.center);
	}

	protected static void GetCapsuleHeadsPositions(CapsuleCollider2D capsule, ref Vector3 upper, ref Vector3 bottom, Vector3 direction, float radius, float scalerFactor)
	{
		Vector3 direction2 = direction * (GetCapsule2DHeight(capsule) * scalerFactor - radius);
		upper = capsule.transform.position + capsule.transform.TransformDirection(direction2) + capsule.transform.TransformVector(capsule.offset);
		upper.z = 0f;
		Vector3 direction3 = -direction * (GetCapsule2DHeight(capsule) * scalerFactor - radius);
		bottom = capsule.transform.position + capsule.transform.TransformDirection(direction3) + capsule.transform.TransformVector(capsule.offset);
		bottom.z = 0f;
	}
}
