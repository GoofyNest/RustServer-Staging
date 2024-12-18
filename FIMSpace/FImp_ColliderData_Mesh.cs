using UnityEngine;

namespace FIMSpace;

public class FImp_ColliderData_Mesh : FImp_ColliderData_Base
{
	private ContactFilter2D filter;

	private RaycastHit2D[] r;

	public MeshCollider Mesh { get; private set; }

	public PolygonCollider2D Poly2D { get; private set; }

	public FImp_ColliderData_Mesh(MeshCollider collider)
	{
		Is2D = false;
		base.Transform = collider.transform;
		base.Collider = collider;
		Mesh = collider;
		base.ColliderType = EFColliderType.Mesh;
	}

	public FImp_ColliderData_Mesh(PolygonCollider2D collider)
	{
		Is2D = true;
		base.Transform = collider.transform;
		Poly2D = collider;
		base.Collider2D = collider;
		base.ColliderType = EFColliderType.Mesh;
		filter = default(ContactFilter2D);
		filter.useTriggers = false;
		filter.useDepth = false;
		r = new RaycastHit2D[1];
	}

	public override bool PushIfInside(ref Vector3 segmentPosition, float segmentRadius, Vector3 segmentOffset)
	{
		if (!Is2D)
		{
			if (!Mesh.convex)
			{
				float num = 0f;
				Vector3 vector = segmentPosition + segmentOffset;
				Vector3 vector2 = Mesh.ClosestPointOnBounds(vector);
				num = (vector2 - Mesh.transform.position).magnitude;
				bool flag = false;
				float num2 = 1f;
				if (vector2 == vector)
				{
					flag = true;
					num2 = 7f;
					vector2 = Mesh.transform.position;
				}
				Vector3 vector3 = vector2 - vector;
				Vector3 normalized = vector3.normalized;
				Vector3 origin = vector - normalized * (segmentRadius * 2f + Mesh.bounds.extents.magnitude);
				float maxDistance = vector3.magnitude + segmentRadius * 2f + num + Mesh.bounds.extents.magnitude;
				if ((vector - vector2).magnitude < segmentRadius * num2)
				{
					Ray ray = new Ray(origin, normalized);
					if (Mesh.Raycast(ray, out var hitInfo, maxDistance) && (vector - hitInfo.point).magnitude < segmentRadius * num2)
					{
						Vector3 vector4 = hitInfo.point - vector;
						Vector3 vector5 = ((!flag) ? (vector4 - vector4.normalized * segmentRadius) : (vector4 + vector4.normalized * segmentRadius));
						float num3 = Vector3.Dot((hitInfo.point - vector).normalized, normalized);
						if (flag && num3 > 0f)
						{
							vector5 = vector4 - vector4.normalized * segmentRadius;
						}
						segmentPosition += vector5;
						return true;
					}
				}
				return false;
			}
			Vector3 vector6 = segmentPosition + segmentOffset;
			float num4 = 1f;
			Vector3 vector7 = Physics.ClosestPoint(vector6, Mesh, Mesh.transform.position, Mesh.transform.rotation);
			if (Vector3.Distance(vector7, vector6) > segmentRadius * 1.01f)
			{
				return false;
			}
			Vector3 vector8 = vector7 - vector6;
			if (vector8 == Vector3.zero)
			{
				return false;
			}
			Mesh.Raycast(new Ray(vector6, vector8.normalized), out var hitInfo2, segmentRadius * num4);
			if ((bool)hitInfo2.transform)
			{
				segmentPosition = hitInfo2.point + hitInfo2.normal * segmentRadius;
				return true;
			}
		}
		else
		{
			Vector2 vector9 = segmentPosition + segmentOffset;
			Vector2 vector11;
			if (Poly2D.OverlapPoint(vector9))
			{
				Vector3 vector10 = Poly2D.bounds.center - (Vector3)vector9;
				vector10.z = 0f;
				Ray ray2 = new Ray(Poly2D.bounds.center - vector10 * Poly2D.bounds.max.magnitude, vector10);
				float distance = 0f;
				Poly2D.bounds.IntersectRay(ray2, out distance);
				vector11 = ((!(distance > 0f)) ? Poly2D.ClosestPoint(vector9) : Poly2D.ClosestPoint(ray2.GetPoint(distance)));
			}
			else
			{
				vector11 = Poly2D.ClosestPoint(vector9);
			}
			Vector2 normalized2 = (vector11 - vector9).normalized;
			if (Physics2D.Raycast(vector9, normalized2, filter, r, segmentRadius) > 0 && r[0].transform == base.Transform)
			{
				segmentPosition = vector11 + r[0].normal * segmentRadius;
				return true;
			}
		}
		return false;
	}

	public static void PushOutFromMeshCollider(MeshCollider mesh, Collision collision, float segmentColliderRadius, ref Vector3 pos)
	{
		Vector3 point = collision.contacts[0].point;
		Vector3 normal = collision.contacts[0].normal;
		if (mesh.Raycast(new Ray(pos + normal * segmentColliderRadius * 2f, -normal), out var hitInfo, segmentColliderRadius * 5f))
		{
			normal = hitInfo.point - pos;
			float sqrMagnitude = normal.sqrMagnitude;
			if (sqrMagnitude > 0f && sqrMagnitude < segmentColliderRadius * segmentColliderRadius)
			{
				pos = hitInfo.point - normal * (segmentColliderRadius / Mathf.Sqrt(sqrMagnitude)) * 0.9f;
			}
		}
		else
		{
			normal = point - pos;
			float sqrMagnitude2 = normal.sqrMagnitude;
			if (sqrMagnitude2 > 0f && sqrMagnitude2 < segmentColliderRadius * segmentColliderRadius)
			{
				pos = point - normal * (segmentColliderRadius / Mathf.Sqrt(sqrMagnitude2)) * 0.9f;
			}
		}
	}

	public static void PushOutFromMesh(MeshCollider mesh, Collision collision, float pointRadius, ref Vector3 point)
	{
		float num = 0f;
		Vector3 vector = mesh.ClosestPointOnBounds(point);
		num = (vector - mesh.transform.position).magnitude;
		bool flag = false;
		float num2 = 1f;
		if (vector == point)
		{
			flag = true;
			num2 = 7f;
			vector = mesh.transform.position;
		}
		Vector3 vector2 = vector - point;
		Vector3 normalized = vector2.normalized;
		Vector3 origin = point - normalized * (pointRadius * 2f + mesh.bounds.extents.magnitude);
		float maxDistance = vector2.magnitude + pointRadius * 2f + num + mesh.bounds.extents.magnitude;
		if (!((point - vector).magnitude < pointRadius * num2))
		{
			return;
		}
		Vector3 vector3;
		if (!flag)
		{
			vector3 = collision.contacts[0].point;
		}
		else
		{
			Ray ray = new Ray(origin, normalized);
			vector3 = ((!mesh.Raycast(ray, out var hitInfo, maxDistance)) ? collision.contacts[0].point : hitInfo.point);
		}
		if ((point - vector3).magnitude < pointRadius * num2)
		{
			Vector3 vector4 = vector3 - point;
			Vector3 vector5 = ((!flag) ? (vector4 - vector4.normalized * pointRadius) : (vector4 + vector4.normalized * pointRadius));
			float num3 = Vector3.Dot((vector3 - point).normalized, normalized);
			if (flag && num3 > 0f)
			{
				vector5 = vector4 - vector4.normalized * pointRadius;
			}
			point += vector5;
		}
	}
}
