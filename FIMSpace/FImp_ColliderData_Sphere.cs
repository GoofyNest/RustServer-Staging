using UnityEngine;

namespace FIMSpace;

public class FImp_ColliderData_Sphere : FImp_ColliderData_Base
{
	private float SphereRadius;

	public SphereCollider Sphere { get; private set; }

	public CircleCollider2D Sphere2D { get; private set; }

	public FImp_ColliderData_Sphere(SphereCollider collider)
	{
		Is2D = false;
		base.Transform = collider.transform;
		base.Collider = collider;
		Sphere = collider;
		base.ColliderType = EFColliderType.Sphere;
		RefreshColliderData();
	}

	public FImp_ColliderData_Sphere(CircleCollider2D collider)
	{
		Is2D = true;
		base.Transform = collider.transform;
		base.Collider2D = collider;
		Sphere2D = collider;
		base.ColliderType = EFColliderType.Sphere;
		RefreshColliderData();
	}

	public override void RefreshColliderData()
	{
		if (!base.IsStatic)
		{
			if (Sphere2D == null)
			{
				SphereRadius = CalculateTrueRadiusOfSphereCollider(Sphere.transform, Sphere.radius);
				base.RefreshColliderData();
			}
			else
			{
				SphereRadius = CalculateTrueRadiusOfSphereCollider(Sphere2D.transform, Sphere2D.radius);
				base.RefreshColliderData();
			}
		}
	}

	public override bool PushIfInside(ref Vector3 point, float pointRadius, Vector3 pointOffset)
	{
		if (!Is2D)
		{
			return PushOutFromSphereCollider(Sphere, pointRadius, ref point, SphereRadius, pointOffset);
		}
		return PushOutFromSphereCollider(Sphere2D, pointRadius, ref point, SphereRadius, pointOffset);
	}

	public static bool PushOutFromSphereCollider(SphereCollider sphere, float segmentColliderRadius, ref Vector3 segmentPos, Vector3 segmentOffset)
	{
		return PushOutFromSphereCollider(sphere, segmentColliderRadius, ref segmentPos, CalculateTrueRadiusOfSphereCollider(sphere), segmentOffset);
	}

	public static bool PushOutFromSphereCollider(SphereCollider sphere, float segmentColliderRadius, ref Vector3 segmentPos, float collidingSphereRadius, Vector3 segmentOffset)
	{
		Vector3 vector = sphere.transform.position + sphere.transform.TransformVector(sphere.center);
		float num = collidingSphereRadius + segmentColliderRadius;
		Vector3 vector2 = segmentPos + segmentOffset - vector;
		float sqrMagnitude = vector2.sqrMagnitude;
		if (sqrMagnitude > 0f && sqrMagnitude < num * num)
		{
			segmentPos = vector - segmentOffset + vector2 * (num / Mathf.Sqrt(sqrMagnitude));
			return true;
		}
		return false;
	}

	public static bool PushOutFromSphereCollider(CircleCollider2D sphere, float segmentColliderRadius, ref Vector3 segmentPos, float collidingSphereRadius, Vector3 segmentOffset)
	{
		Vector3 vector = sphere.transform.position + sphere.transform.TransformVector(sphere.offset);
		vector.z = 0f;
		float num = collidingSphereRadius + segmentColliderRadius;
		Vector3 vector2 = segmentPos;
		vector2.z = 0f;
		Vector3 vector3 = vector2 + segmentOffset - vector;
		float sqrMagnitude = vector3.sqrMagnitude;
		if (sqrMagnitude > 0f && sqrMagnitude < num * num)
		{
			segmentPos = vector - segmentOffset + vector3 * (num / Mathf.Sqrt(sqrMagnitude));
			return true;
		}
		return false;
	}

	public static float CalculateTrueRadiusOfSphereCollider(SphereCollider sphere)
	{
		return CalculateTrueRadiusOfSphereCollider(sphere.transform, sphere.radius);
	}

	public static float CalculateTrueRadiusOfSphereCollider(CircleCollider2D sphere)
	{
		return CalculateTrueRadiusOfSphereCollider(sphere.transform, sphere.radius);
	}

	public static float CalculateTrueRadiusOfSphereCollider(Transform transform, float componentRadius)
	{
		float num = componentRadius;
		if (transform.lossyScale.x > transform.lossyScale.y)
		{
			if (transform.lossyScale.x > transform.lossyScale.z)
			{
				return num * transform.lossyScale.x;
			}
			return num * transform.lossyScale.z;
		}
		if (transform.lossyScale.y > transform.lossyScale.z)
		{
			return num * transform.lossyScale.y;
		}
		return num * transform.lossyScale.z;
	}
}
