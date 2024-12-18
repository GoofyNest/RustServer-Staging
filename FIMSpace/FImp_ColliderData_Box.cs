using UnityEngine;

namespace FIMSpace;

public class FImp_ColliderData_Box : FImp_ColliderData_Base
{
	private Vector3 boxCenter;

	private Vector3 right;

	private Vector3 up;

	private Vector3 forward;

	private Vector3 rightN;

	private Vector3 upN;

	private Vector3 forwardN;

	private Vector3 scales;

	public BoxCollider Box { get; private set; }

	public BoxCollider2D Box2D { get; private set; }

	public FImp_ColliderData_Box(BoxCollider collider)
	{
		Is2D = false;
		base.Collider = collider;
		base.Transform = collider.transform;
		Box = collider;
		base.ColliderType = EFColliderType.Box;
		RefreshColliderData();
		previousPosition = base.Transform.position + Vector3.forward * Mathf.Epsilon;
	}

	public FImp_ColliderData_Box(BoxCollider2D collider2D)
	{
		Is2D = true;
		base.Collider2D = collider2D;
		base.Transform = collider2D.transform;
		Box2D = collider2D;
		base.ColliderType = EFColliderType.Box;
		RefreshColliderData();
		previousPosition = base.Transform.position + Vector3.forward * Mathf.Epsilon;
	}

	public override void RefreshColliderData()
	{
		if (base.IsStatic)
		{
			return;
		}
		if (base.Collider2D == null)
		{
			bool flag = false;
			if (!base.Transform.position.VIsSame(previousPosition))
			{
				flag = true;
			}
			else if (!base.Transform.rotation.QIsSame(previousRotation))
			{
				flag = true;
			}
			if (flag)
			{
				right = Box.transform.TransformVector(Vector3.right / 2f * Box.size.x);
				up = Box.transform.TransformVector(Vector3.up / 2f * Box.size.y);
				forward = Box.transform.TransformVector(Vector3.forward / 2f * Box.size.z);
				rightN = right.normalized;
				upN = up.normalized;
				forwardN = forward.normalized;
				boxCenter = GetBoxCenter(Box);
				scales = Vector3.Scale(Box.size, Box.transform.lossyScale);
				scales.Normalize();
			}
		}
		else
		{
			bool flag2 = false;
			if (Vector2.Distance(base.Transform.position, previousPosition) > Mathf.Epsilon)
			{
				flag2 = true;
			}
			else if (!base.Transform.rotation.QIsSame(previousRotation))
			{
				flag2 = true;
			}
			if (flag2)
			{
				right = Box2D.transform.TransformVector(Vector3.right / 2f * Box2D.size.x);
				up = Box2D.transform.TransformVector(Vector3.up / 2f * Box2D.size.y);
				rightN = right.normalized;
				upN = up.normalized;
				boxCenter = GetBoxCenter(Box2D);
				boxCenter.z = 0f;
				Vector3 lossyScale = base.Transform.lossyScale;
				lossyScale.z = 1f;
				scales = Vector3.Scale(Box2D.size, lossyScale);
				scales.Normalize();
			}
		}
		base.RefreshColliderData();
		previousPosition = base.Transform.position;
		previousRotation = base.Transform.rotation;
	}

	public override bool PushIfInside(ref Vector3 segmentPosition, float segmentRadius, Vector3 segmentOffset)
	{
		int num = 0;
		Vector3 vector = Vector3.zero;
		Vector3 vector2 = segmentPosition + segmentOffset;
		float planeDistance = PlaneDistance(boxCenter + up, upN, vector2);
		if (SphereInsidePlane(planeDistance, segmentRadius))
		{
			num++;
		}
		else if (SphereIntersectsPlane(planeDistance, segmentRadius))
		{
			num++;
			vector = up;
		}
		planeDistance = PlaneDistance(boxCenter - up, -upN, vector2);
		if (SphereInsidePlane(planeDistance, segmentRadius))
		{
			num++;
		}
		else if (SphereIntersectsPlane(planeDistance, segmentRadius))
		{
			num++;
			vector = -up;
		}
		planeDistance = PlaneDistance(boxCenter - right, -rightN, vector2);
		if (SphereInsidePlane(planeDistance, segmentRadius))
		{
			num++;
		}
		else if (SphereIntersectsPlane(planeDistance, segmentRadius))
		{
			num++;
			vector = -right;
		}
		planeDistance = PlaneDistance(boxCenter + right, rightN, vector2);
		if (SphereInsidePlane(planeDistance, segmentRadius))
		{
			num++;
		}
		else if (SphereIntersectsPlane(planeDistance, segmentRadius))
		{
			num++;
			vector = right;
		}
		bool flag = false;
		if (base.Collider2D == null)
		{
			planeDistance = PlaneDistance(boxCenter + forward, forwardN, vector2);
			if (SphereInsidePlane(planeDistance, segmentRadius))
			{
				num++;
			}
			else if (SphereIntersectsPlane(planeDistance, segmentRadius))
			{
				num++;
				vector = forward;
			}
			planeDistance = PlaneDistance(boxCenter - forward, -forwardN, vector2);
			if (SphereInsidePlane(planeDistance, segmentRadius))
			{
				num++;
			}
			else if (SphereIntersectsPlane(planeDistance, segmentRadius))
			{
				num++;
				vector = -forward;
			}
			if (num == 6)
			{
				flag = true;
			}
		}
		else if (num == 4)
		{
			flag = true;
		}
		if (flag)
		{
			bool flag2 = false;
			if (vector.sqrMagnitude == 0f)
			{
				flag2 = true;
			}
			else if (base.Collider2D == null)
			{
				if (IsInsideBoxCollider(Box, vector2))
				{
					flag2 = true;
				}
			}
			else if (IsInsideBoxCollider(Box2D, vector2))
			{
				flag2 = true;
			}
			Vector3 vector3 = GetNearestPoint(vector2) - vector2;
			if (flag2)
			{
				vector3 += vector3.normalized * segmentRadius;
			}
			else
			{
				vector3 -= vector3.normalized * segmentRadius;
			}
			if (flag2)
			{
				segmentPosition += vector3;
			}
			else if (vector3.sqrMagnitude > 0f)
			{
				segmentPosition += vector3;
			}
			return true;
		}
		return false;
	}

	public static void PushOutFromBoxCollider(BoxCollider box, Collision collision, float segmentColliderRadius, ref Vector3 segmentPosition, bool is2D = false)
	{
		Vector3 vector = box.transform.TransformVector(Vector3.right / 2f * box.size.x + box.center.x * Vector3.right);
		Vector3 vector2 = box.transform.TransformVector(Vector3.up / 2f * box.size.y + box.center.y * Vector3.up);
		Vector3 vector3 = box.transform.TransformVector(Vector3.forward / 2f * box.size.z + box.center.z * Vector3.forward);
		Vector3 vector4 = Vector3.Scale(box.size, box.transform.lossyScale);
		vector4.Normalize();
		PushOutFromBoxCollider(box, collision, segmentColliderRadius, ref segmentPosition, vector, vector2, vector3, vector4, is2D);
	}

	public static void PushOutFromBoxCollider(BoxCollider box, float segmentColliderRadius, ref Vector3 segmentPosition, bool is2D = false)
	{
		Vector3 vector = box.transform.TransformVector(Vector3.right / 2f * box.size.x + box.center.x * Vector3.right);
		Vector3 vector2 = box.transform.TransformVector(Vector3.up / 2f * box.size.y + box.center.y * Vector3.up);
		Vector3 vector3 = box.transform.TransformVector(Vector3.forward / 2f * box.size.z + box.center.z * Vector3.forward);
		Vector3.Scale(box.size, box.transform.lossyScale).Normalize();
		Vector3 vector4 = GetBoxCenter(box);
		Vector3 normalized = vector2.normalized;
		Vector3 normalized2 = vector.normalized;
		Vector3 normalized3 = vector3.normalized;
		int num = 0;
		Vector3 vector5 = Vector3.zero;
		float planeDistance = PlaneDistance(vector4 + vector2, normalized, segmentPosition);
		if (SphereInsidePlane(planeDistance, segmentColliderRadius))
		{
			num++;
		}
		else if (SphereIntersectsPlane(planeDistance, segmentColliderRadius))
		{
			num++;
			vector5 = vector2;
		}
		planeDistance = PlaneDistance(vector4 - vector2, -normalized, segmentPosition);
		if (SphereInsidePlane(planeDistance, segmentColliderRadius))
		{
			num++;
		}
		else if (SphereIntersectsPlane(planeDistance, segmentColliderRadius))
		{
			num++;
			vector5 = -vector2;
		}
		planeDistance = PlaneDistance(vector4 - vector, -normalized2, segmentPosition);
		if (SphereInsidePlane(planeDistance, segmentColliderRadius))
		{
			num++;
		}
		else if (SphereIntersectsPlane(planeDistance, segmentColliderRadius))
		{
			num++;
			vector5 = -vector;
		}
		planeDistance = PlaneDistance(vector4 + vector, normalized2, segmentPosition);
		if (SphereInsidePlane(planeDistance, segmentColliderRadius))
		{
			num++;
		}
		else if (SphereIntersectsPlane(planeDistance, segmentColliderRadius))
		{
			num++;
			vector5 = vector;
		}
		planeDistance = PlaneDistance(vector4 + vector3, normalized3, segmentPosition);
		if (SphereInsidePlane(planeDistance, segmentColliderRadius))
		{
			num++;
		}
		else if (SphereIntersectsPlane(planeDistance, segmentColliderRadius))
		{
			num++;
			vector5 = vector3;
		}
		planeDistance = PlaneDistance(vector4 - vector3, -normalized3, segmentPosition);
		if (SphereInsidePlane(planeDistance, segmentColliderRadius))
		{
			num++;
		}
		else if (SphereIntersectsPlane(planeDistance, segmentColliderRadius))
		{
			num++;
			vector5 = -vector3;
		}
		if (num == 6)
		{
			bool flag = false;
			if (vector5.sqrMagnitude == 0f)
			{
				flag = true;
			}
			else if (IsInsideBoxCollider(box, segmentPosition))
			{
				flag = true;
			}
			Vector3 vector6 = GetNearestPoint(segmentPosition, vector4, vector, vector2, vector3, is2D) - segmentPosition;
			if (flag)
			{
				vector6 += vector6.normalized * segmentColliderRadius * 1.01f;
			}
			else
			{
				vector6 -= vector6.normalized * segmentColliderRadius * 1.01f;
			}
			if (flag)
			{
				segmentPosition += vector6;
			}
			else if (vector6.sqrMagnitude > 0f)
			{
				segmentPosition += vector6;
			}
		}
	}

	public static void PushOutFromBoxCollider(BoxCollider box, Collision collision, float segmentColliderRadius, ref Vector3 pos, Vector3 right, Vector3 up, Vector3 forward, Vector3 scales, bool is2D = false)
	{
		Vector3 vector = collision.contacts[0].point;
		Vector3 vector2 = pos - vector;
		Vector3 vector3 = GetBoxCenter(box);
		if (vector2.sqrMagnitude == 0f)
		{
			vector2 = pos - vector3;
		}
		float num = 1f;
		if (IsInsideBoxCollider(box, pos))
		{
			float boxAverageScale = GetBoxAverageScale(box);
			Vector3 targetPlaneNormal = GetTargetPlaneNormal(box, pos, right, up, forward, scales);
			Vector3 normalized = targetPlaneNormal.normalized;
			vector = ((!box.Raycast(new Ray(pos - normalized * boxAverageScale * 3f, normalized), out var hitInfo, boxAverageScale * 4f)) ? GetIntersectOnBoxFromInside(box, vector3, pos, targetPlaneNormal) : hitInfo.point);
			vector2 = vector - pos;
			num = 100f;
		}
		Vector3 vector4 = pos - (vector2 / num + vector2.normalized * 1.15f) / 2f * segmentColliderRadius;
		vector4 = vector - vector4;
		float sqrMagnitude = vector4.sqrMagnitude;
		if (sqrMagnitude > 0f && sqrMagnitude < segmentColliderRadius * segmentColliderRadius * num)
		{
			pos += vector4;
		}
	}

	public static void PushOutFromBoxCollider(BoxCollider2D box2D, float segmentColliderRadius, ref Vector3 segmentPosition)
	{
		Vector2 vector = box2D.transform.TransformVector(Vector3.right / 2f * box2D.size.x + box2D.offset.x * Vector3.right);
		Vector2 vector2 = box2D.transform.TransformVector(Vector3.up / 2f * box2D.size.y + box2D.offset.y * Vector3.up);
		Vector3 lossyScale = box2D.transform.lossyScale;
		lossyScale.z = 1f;
		((Vector2)Vector3.Scale(box2D.size, lossyScale)).Normalize();
		Vector2 vector3 = GetBoxCenter(box2D);
		Vector2 normalized = vector2.normalized;
		Vector2 normalized2 = vector.normalized;
		int num = 0;
		Vector3 vector4 = Vector3.zero;
		float planeDistance = PlaneDistance(vector3 + vector2, normalized, segmentPosition);
		if (SphereInsidePlane(planeDistance, segmentColliderRadius))
		{
			num++;
		}
		else if (SphereIntersectsPlane(planeDistance, segmentColliderRadius))
		{
			num++;
			vector4 = vector2;
		}
		planeDistance = PlaneDistance(vector3 - vector2, -normalized, segmentPosition);
		if (SphereInsidePlane(planeDistance, segmentColliderRadius))
		{
			num++;
		}
		else if (SphereIntersectsPlane(planeDistance, segmentColliderRadius))
		{
			num++;
			vector4 = -vector2;
		}
		planeDistance = PlaneDistance(vector3 - vector, -normalized2, segmentPosition);
		if (SphereInsidePlane(planeDistance, segmentColliderRadius))
		{
			num++;
		}
		else if (SphereIntersectsPlane(planeDistance, segmentColliderRadius))
		{
			num++;
			vector4 = -vector;
		}
		planeDistance = PlaneDistance(vector3 + vector, normalized2, segmentPosition);
		if (SphereInsidePlane(planeDistance, segmentColliderRadius))
		{
			num++;
		}
		else if (SphereIntersectsPlane(planeDistance, segmentColliderRadius))
		{
			num++;
			vector4 = vector;
		}
		if (num == 4)
		{
			bool flag = false;
			if (vector4.sqrMagnitude == 0f)
			{
				flag = true;
			}
			else if (IsInsideBoxCollider(box2D, segmentPosition))
			{
				flag = true;
			}
			Vector3 vector5 = GetNearestPoint2D(segmentPosition, vector3, vector, vector2) - segmentPosition;
			if (flag)
			{
				vector5 += vector5.normalized * segmentColliderRadius * 1.01f;
			}
			else
			{
				vector5 -= vector5.normalized * segmentColliderRadius * 1.01f;
			}
			if (flag)
			{
				segmentPosition += vector5;
			}
			else if (vector5.sqrMagnitude > 0f)
			{
				segmentPosition += vector5;
			}
		}
	}

	private Vector3 GetNearestPoint(Vector3 point)
	{
		Vector3 vector = point;
		Vector3 one = Vector3.one;
		one.x = PlaneDistance(boxCenter + right, rightN, point);
		one.y = PlaneDistance(boxCenter + up, upN, point);
		if (base.Collider2D == null)
		{
			one.z = PlaneDistance(boxCenter + forward, forwardN, point);
		}
		Vector3 one2 = Vector3.one;
		one2.x = PlaneDistance(boxCenter - right, -rightN, point);
		one2.y = PlaneDistance(boxCenter - up, -upN, point);
		if (base.Collider2D == null)
		{
			one2.z = PlaneDistance(boxCenter - forward, -forwardN, point);
		}
		float num = 1f;
		float num2 = 1f;
		float num3 = 1f;
		float x;
		if (one.x > one2.x)
		{
			x = one.x;
			num = -1f;
		}
		else
		{
			x = one2.x;
			num = 1f;
		}
		float y;
		if (one.y > one2.y)
		{
			y = one.y;
			num2 = -1f;
		}
		else
		{
			y = one2.y;
			num2 = 1f;
		}
		if (base.Collider2D == null)
		{
			float z;
			if (one.z > one2.z)
			{
				z = one.z;
				num3 = -1f;
			}
			else
			{
				z = one2.z;
				num3 = 1f;
			}
			if (x > z)
			{
				if (x > y)
				{
					return ProjectPointOnPlane(right * num, point, x);
				}
				return ProjectPointOnPlane(up * num2, point, y);
			}
			if (z > y)
			{
				return ProjectPointOnPlane(forward * num3, point, z);
			}
			return ProjectPointOnPlane(up * num2, point, y);
		}
		if (x > y)
		{
			return ProjectPointOnPlane(right * num, point, x);
		}
		return ProjectPointOnPlane(up * num2, point, y);
	}

	private static Vector3 GetNearestPoint(Vector3 point, Vector3 boxCenter, Vector3 right, Vector3 up, Vector3 forward, bool is2D = false)
	{
		Vector3 vector = point;
		Vector3 one = Vector3.one;
		one.x = PlaneDistance(boxCenter + right, right.normalized, point);
		one.y = PlaneDistance(boxCenter + up, up.normalized, point);
		if (!is2D)
		{
			one.z = PlaneDistance(boxCenter + forward, forward.normalized, point);
		}
		Vector3 one2 = Vector3.one;
		one2.x = PlaneDistance(boxCenter - right, -right.normalized, point);
		one2.y = PlaneDistance(boxCenter - up, -up.normalized, point);
		if (!is2D)
		{
			one2.z = PlaneDistance(boxCenter - forward, -forward.normalized, point);
		}
		float num = 1f;
		float num2 = 1f;
		float num3 = 1f;
		float x;
		if (one.x > one2.x)
		{
			x = one.x;
			num = -1f;
		}
		else
		{
			x = one2.x;
			num = 1f;
		}
		float y;
		if (one.y > one2.y)
		{
			y = one.y;
			num2 = -1f;
		}
		else
		{
			y = one2.y;
			num2 = 1f;
		}
		if (!is2D)
		{
			float z;
			if (one.z > one2.z)
			{
				z = one.z;
				num3 = -1f;
			}
			else
			{
				z = one2.z;
				num3 = 1f;
			}
			if (x > z)
			{
				if (x > y)
				{
					return ProjectPointOnPlane(right * num, point, x);
				}
				return ProjectPointOnPlane(up * num2, point, y);
			}
			if (z > y)
			{
				return ProjectPointOnPlane(forward * num3, point, z);
			}
			return ProjectPointOnPlane(up * num2, point, y);
		}
		if (x > y)
		{
			return ProjectPointOnPlane(right * num, point, x);
		}
		return ProjectPointOnPlane(up * num2, point, y);
	}

	private static Vector3 GetNearestPoint2D(Vector2 point, Vector2 boxCenter, Vector2 right, Vector2 up)
	{
		Vector3 vector = point;
		Vector3 one = Vector3.one;
		one.x = PlaneDistance(boxCenter + right, right.normalized, point);
		one.y = PlaneDistance(boxCenter + up, up.normalized, point);
		Vector3 one2 = Vector3.one;
		one2.x = PlaneDistance(boxCenter - right, -right.normalized, point);
		one2.y = PlaneDistance(boxCenter - up, -up.normalized, point);
		float num = 1f;
		float num2 = 1f;
		float x;
		if (one.x > one2.x)
		{
			x = one.x;
			num = -1f;
		}
		else
		{
			x = one2.x;
			num = 1f;
		}
		float y;
		if (one.y > one2.y)
		{
			y = one.y;
			num2 = -1f;
		}
		else
		{
			y = one2.y;
			num2 = 1f;
		}
		if (x > y)
		{
			return ProjectPointOnPlane(right * num, point, x);
		}
		return ProjectPointOnPlane(up * num2, point, y);
	}

	public static Vector3 GetNearestPointOnBox(BoxCollider boxCollider, Vector3 point, bool is2D = false)
	{
		Vector3 vector = boxCollider.transform.TransformVector(Vector3.right / 2f);
		Vector3 vector2 = boxCollider.transform.TransformVector(Vector3.up / 2f);
		Vector3 vector3 = Vector3.forward;
		if (!is2D)
		{
			vector3 = boxCollider.transform.TransformVector(Vector3.forward / 2f);
		}
		Vector3 vector4 = point;
		Vector3 vector5 = GetBoxCenter(boxCollider);
		Vector3 normalized = vector.normalized;
		Vector3 normalized2 = vector2.normalized;
		Vector3 normalized3 = vector3.normalized;
		Vector3 one = Vector3.one;
		one.x = PlaneDistance(vector5 + vector, normalized, point);
		one.y = PlaneDistance(vector5 + vector2, normalized2, point);
		if (!is2D)
		{
			one.z = PlaneDistance(vector5 + vector3, normalized3, point);
		}
		Vector3 one2 = Vector3.one;
		one2.x = PlaneDistance(vector5 - vector, -normalized, point);
		one2.y = PlaneDistance(vector5 - vector2, -normalized2, point);
		if (!is2D)
		{
			one2.z = PlaneDistance(vector5 - vector3, -normalized3, point);
		}
		float num = 1f;
		float num2 = 1f;
		float num3 = 1f;
		float x;
		if (one.x > one2.x)
		{
			x = one.x;
			num = -1f;
		}
		else
		{
			x = one2.x;
			num = 1f;
		}
		float y;
		if (one.y > one2.y)
		{
			y = one.y;
			num2 = -1f;
		}
		else
		{
			y = one2.y;
			num2 = 1f;
		}
		if (!is2D)
		{
			float z;
			if (one.z > one2.z)
			{
				z = one.z;
				num3 = -1f;
			}
			else
			{
				z = one2.z;
				num3 = 1f;
			}
			if (x > z)
			{
				if (x > y)
				{
					return ProjectPointOnPlane(vector * num, point, x);
				}
				return ProjectPointOnPlane(vector2 * num2, point, y);
			}
			if (z > y)
			{
				return ProjectPointOnPlane(vector3 * num3, point, z);
			}
			return ProjectPointOnPlane(vector2 * num2, point, y);
		}
		if (x > y)
		{
			return ProjectPointOnPlane(vector * num, point, x);
		}
		return ProjectPointOnPlane(vector2 * num2, point, y);
	}

	private static float PlaneDistance(Vector3 planeCenter, Vector3 planeNormal, Vector3 point)
	{
		return Vector3.Dot(point - planeCenter, planeNormal);
	}

	private static Vector3 ProjectPointOnPlane(Vector3 planeNormal, Vector3 point, float distance)
	{
		Vector3 vector = planeNormal.normalized * distance;
		return point + vector;
	}

	private static bool SphereInsidePlane(float planeDistance, float pointRadius)
	{
		return 0f - planeDistance > pointRadius;
	}

	private static bool SphereOutsidePlane(float planeDistance, float pointRadius)
	{
		return planeDistance > pointRadius;
	}

	private static bool SphereIntersectsPlane(float planeDistance, float pointRadius)
	{
		return Mathf.Abs(planeDistance) <= pointRadius;
	}

	public static bool IsInsideBoxCollider(BoxCollider collider, Vector3 point, bool is2D = false)
	{
		point = collider.transform.InverseTransformPoint(point) - collider.center;
		float num = collider.size.x * 0.5f;
		float num2 = collider.size.y * 0.5f;
		float num3 = collider.size.z * 0.5f;
		if (point.x < num && point.x > 0f - num && point.y < num2 && point.y > 0f - num2 && point.z < num3)
		{
			return point.z > 0f - num3;
		}
		return false;
	}

	public static bool IsInsideBoxCollider(BoxCollider2D collider, Vector3 point)
	{
		point = (Vector2)collider.transform.InverseTransformPoint(point) - collider.offset;
		float num = collider.size.x * 0.5f;
		float num2 = collider.size.y * 0.5f;
		if (point.x < num && point.x > 0f - num && point.y < num2)
		{
			return point.y > 0f - num2;
		}
		return false;
	}

	protected static float GetBoxAverageScale(BoxCollider box)
	{
		Vector3 lossyScale = box.transform.lossyScale;
		lossyScale = Vector3.Scale(lossyScale, box.size);
		return (lossyScale.x + lossyScale.y + lossyScale.z) / 3f;
	}

	protected static Vector3 GetBoxCenter(BoxCollider box)
	{
		return box.transform.position + box.transform.TransformVector(box.center);
	}

	protected static Vector3 GetBoxCenter(BoxCollider2D box)
	{
		return box.transform.position + box.transform.TransformVector(box.offset);
	}

	protected static Vector3 GetTargetPlaneNormal(BoxCollider boxCollider, Vector3 point, bool is2D = false)
	{
		Vector3 vector = boxCollider.transform.TransformVector(Vector3.right / 2f * boxCollider.size.x);
		Vector3 vector2 = boxCollider.transform.TransformVector(Vector3.up / 2f * boxCollider.size.y);
		Vector3 vector3 = Vector3.forward;
		if (!is2D)
		{
			vector3 = boxCollider.transform.TransformVector(Vector3.forward / 2f * boxCollider.size.z);
		}
		Vector3 vector4 = Vector3.Scale(boxCollider.size, boxCollider.transform.lossyScale);
		vector4.Normalize();
		return GetTargetPlaneNormal(boxCollider, point, vector, vector2, vector3, vector4, is2D);
	}

	protected static Vector3 GetTargetPlaneNormal(BoxCollider boxCollider, Vector3 point, Vector3 right, Vector3 up, Vector3 forward, Vector3 scales, bool is2D = false)
	{
		Vector3 normalized = (GetBoxCenter(boxCollider) - point).normalized;
		Vector3 vector = default(Vector3);
		vector.x = Vector3.Dot(normalized, right.normalized);
		vector.y = Vector3.Dot(normalized, up.normalized);
		vector.x = vector.x * scales.y * scales.z;
		vector.y = vector.y * scales.x * scales.z;
		if (!is2D)
		{
			vector.z = Vector3.Dot(normalized, forward.normalized);
			vector.z = vector.z * scales.y * scales.x;
		}
		else
		{
			vector.z = 0f;
		}
		vector.Normalize();
		Vector3 vector2 = vector;
		if (vector.x < 0f)
		{
			vector2.x = 0f - vector.x;
		}
		if (vector.y < 0f)
		{
			vector2.y = 0f - vector.y;
		}
		if (vector.z < 0f)
		{
			vector2.z = 0f - vector.z;
		}
		if (vector2.x > vector2.y)
		{
			if (vector2.x > vector2.z || is2D)
			{
				return right * Mathf.Sign(vector.x);
			}
			return forward * Mathf.Sign(vector.z);
		}
		if (vector2.y > vector2.z || is2D)
		{
			return up * Mathf.Sign(vector.y);
		}
		return forward * Mathf.Sign(vector.z);
	}

	protected static Vector3 GetTargetPlaneNormal(BoxCollider2D boxCollider, Vector2 point, Vector2 right, Vector2 up, Vector2 scales)
	{
		Vector2 normalized = ((Vector2)GetBoxCenter(boxCollider) - point).normalized;
		Vector2 vector = default(Vector2);
		vector.x = Vector3.Dot(normalized, right.normalized);
		vector.y = Vector3.Dot(normalized, up.normalized);
		vector.x *= scales.y;
		vector.y *= scales.x;
		vector.Normalize();
		Vector2 vector2 = vector;
		if (vector.x < 0f)
		{
			vector2.x = 0f - vector.x;
		}
		if (vector.y < 0f)
		{
			vector2.y = 0f - vector.y;
		}
		if (vector2.x > vector2.y)
		{
			return right * Mathf.Sign(vector.x);
		}
		return up * Mathf.Sign(vector.y);
	}

	protected static Vector3 GetIntersectOnBoxFromInside(BoxCollider boxCollider, Vector3 from, Vector3 to, Vector3 planeNormal)
	{
		Vector3 direction = to - from;
		Plane plane = new Plane(-planeNormal, GetBoxCenter(boxCollider) + planeNormal);
		Vector3 result = to;
		float enter = 0f;
		Ray ray = new Ray(from, direction);
		if (plane.Raycast(ray, out enter))
		{
			result = ray.GetPoint(enter);
		}
		return result;
	}
}
