using UnityEngine;

namespace FIMSpace;

public class FImp_ColliderData_Terrain : FImp_ColliderData_Base
{
	public TerrainCollider TerrCollider { get; private set; }

	public Terrain TerrainComponent { get; private set; }

	public FImp_ColliderData_Terrain(TerrainCollider collider)
	{
		base.Collider = collider;
		base.Transform = collider.transform;
		TerrCollider = collider;
		base.ColliderType = EFColliderType.Terrain;
		TerrainComponent = collider.GetComponent<Terrain>();
	}

	public override bool PushIfInside(ref Vector3 segmentPosition, float segmentRadius, Vector3 segmentOffset)
	{
		if (segmentPosition.x + segmentRadius < TerrainComponent.GetPosition().x - segmentRadius || segmentPosition.x > TerrainComponent.GetPosition().x + TerrainComponent.terrainData.size.x || segmentPosition.z + segmentRadius < TerrainComponent.GetPosition().z - segmentRadius || segmentPosition.z > TerrainComponent.GetPosition().z + TerrainComponent.terrainData.size.z)
		{
			return false;
		}
		Vector3 vector = segmentPosition + segmentOffset;
		Vector3 vector2 = vector;
		vector2.y = TerrCollider.transform.position.y + TerrainComponent.SampleHeight(vector);
		float magnitude = (vector - vector2).magnitude;
		float num = 1f;
		if (vector.y < vector2.y)
		{
			num = 4f;
		}
		else if (vector.y + segmentRadius * 2f < vector2.y)
		{
			num = 8f;
		}
		if (magnitude < segmentRadius * num)
		{
			Vector3 vector3 = vector2 - vector;
			Vector3 vector4 = ((!(num > 1f)) ? (vector3 - vector3.normalized * segmentRadius) : (vector3 + vector3.normalized * segmentRadius));
			segmentPosition += vector4;
			return true;
		}
		return false;
	}

	public static void PushOutFromTerrain(TerrainCollider terrainCollider, float segmentRadius, ref Vector3 point)
	{
		Terrain component = terrainCollider.GetComponent<Terrain>();
		Vector3 origin = point;
		origin.y = terrainCollider.transform.position.y + component.SampleHeight(point) + segmentRadius;
		Ray ray = new Ray(origin, Vector3.down);
		if (terrainCollider.Raycast(ray, out var hitInfo, segmentRadius * 2f))
		{
			float magnitude = (point - hitInfo.point).magnitude;
			float num = 1f;
			if (hitInfo.point.y > point.y + segmentRadius * 0.9f)
			{
				num = 8f;
			}
			else if (hitInfo.point.y > point.y)
			{
				num = 4f;
			}
			if (magnitude < segmentRadius * num)
			{
				Vector3 vector = hitInfo.point - point;
				Vector3 vector2 = ((!(num > 1f)) ? (vector - vector.normalized * segmentRadius) : (vector + vector.normalized * segmentRadius));
				point += vector2;
			}
		}
	}
}
