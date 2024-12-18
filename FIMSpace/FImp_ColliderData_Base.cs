using UnityEngine;

namespace FIMSpace;

public abstract class FImp_ColliderData_Base
{
	public enum EFColliderType
	{
		Box,
		Sphere,
		Capsule,
		Mesh,
		Terrain
	}

	public bool Is2D;

	protected Vector3 previousPosition = Vector3.zero;

	protected Quaternion previousRotation = Quaternion.identity;

	protected Vector3 previousScale = Vector3.one;

	public Transform Transform { get; protected set; }

	public Collider Collider { get; protected set; }

	public Collider2D Collider2D { get; protected set; }

	public bool IsStatic { get; private set; }

	public EFColliderType ColliderType { get; protected set; }

	public static FImp_ColliderData_Base GetColliderDataFor(Collider collider)
	{
		SphereCollider sphereCollider = collider as SphereCollider;
		if ((bool)sphereCollider)
		{
			return new FImp_ColliderData_Sphere(sphereCollider);
		}
		CapsuleCollider capsuleCollider = collider as CapsuleCollider;
		if ((bool)capsuleCollider)
		{
			return new FImp_ColliderData_Capsule(capsuleCollider);
		}
		BoxCollider boxCollider = collider as BoxCollider;
		if ((bool)boxCollider)
		{
			return new FImp_ColliderData_Box(boxCollider);
		}
		MeshCollider meshCollider = collider as MeshCollider;
		if ((bool)meshCollider)
		{
			return new FImp_ColliderData_Mesh(meshCollider);
		}
		TerrainCollider terrainCollider = collider as TerrainCollider;
		if ((bool)terrainCollider)
		{
			return new FImp_ColliderData_Terrain(terrainCollider);
		}
		return null;
	}

	public static FImp_ColliderData_Base GetColliderDataFor(Collider2D collider)
	{
		CircleCollider2D circleCollider2D = collider as CircleCollider2D;
		if ((bool)circleCollider2D)
		{
			return new FImp_ColliderData_Sphere(circleCollider2D);
		}
		CapsuleCollider2D capsuleCollider2D = collider as CapsuleCollider2D;
		if ((bool)capsuleCollider2D)
		{
			return new FImp_ColliderData_Capsule(capsuleCollider2D);
		}
		BoxCollider2D boxCollider2D = collider as BoxCollider2D;
		if ((bool)boxCollider2D)
		{
			return new FImp_ColliderData_Box(boxCollider2D);
		}
		PolygonCollider2D polygonCollider2D = collider as PolygonCollider2D;
		if ((bool)polygonCollider2D)
		{
			return new FImp_ColliderData_Mesh(polygonCollider2D);
		}
		return null;
	}

	public virtual void RefreshColliderData()
	{
		if (Transform.gameObject.isStatic)
		{
			IsStatic = true;
		}
		else
		{
			IsStatic = false;
		}
	}

	public virtual bool PushIfInside(ref Vector3 point, float pointRadius, Vector3 pointOffset)
	{
		if ((bool)(Collider as SphereCollider))
		{
			Debug.Log("It shouldn't appear");
		}
		return false;
	}

	public virtual bool PushIfInside2D(ref Vector3 point, float pointRadius, Vector3 pointOffset)
	{
		return PushIfInside(ref point, pointRadius, pointOffset);
	}

	public static bool VIsSame(Vector3 vec1, Vector3 vec2)
	{
		if (vec1.x != vec2.x)
		{
			return false;
		}
		if (vec1.y != vec2.y)
		{
			return false;
		}
		if (vec1.z != vec2.z)
		{
			return false;
		}
		return true;
	}
}
