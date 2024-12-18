using System;
using System.Collections.Generic;
using Facepunch;
using Rust;
using VLB;

namespace UnityEngine;

public static class ColliderEx
{
	public static PhysicMaterial GetMaterialAt(this Collider obj, Vector3 pos)
	{
		if (obj == null)
		{
			return TerrainMeta.Config.WaterMaterial;
		}
		if (obj is TerrainCollider)
		{
			return TerrainMeta.Physics.GetMaterial(pos);
		}
		return obj.sharedMaterial;
	}

	public static float EstimateVolume(this Collider collider)
	{
		Vector3 lossyScale = collider.transform.lossyScale;
		if (collider is SphereCollider sphereCollider)
		{
			return sphereCollider.radius * lossyScale.x * sphereCollider.radius * lossyScale.y * sphereCollider.radius * lossyScale.z * 4.1887903f;
		}
		if (collider is BoxCollider boxCollider)
		{
			return boxCollider.size.x * lossyScale.x * boxCollider.size.y * lossyScale.y * boxCollider.size.z * lossyScale.z;
		}
		if (collider is MeshCollider { bounds: { size: var size } })
		{
			return size.x * lossyScale.x * size.y * lossyScale.y * size.z * lossyScale.z;
		}
		if (collider is CapsuleCollider capsuleCollider)
		{
			float num = capsuleCollider.radius * Mathf.Max(lossyScale.x, lossyScale.z);
			float num2 = (capsuleCollider.height - num * 2f) * lossyScale.y;
			return MathF.PI * num * num * num2 + 4.1887903f * num * num * num;
		}
		return 0f;
	}

	public static bool IsOnLayer(this Collider col, Layer rustLayer)
	{
		if (col != null)
		{
			return col.gameObject.IsOnLayer(rustLayer);
		}
		return false;
	}

	public static bool IsOnLayer(this Collider col, int layer)
	{
		if (col != null)
		{
			return col.gameObject.IsOnLayer(layer);
		}
		return false;
	}

	public static float GetRadius(this Collider col, Vector3 transformScale)
	{
		float result = 1f;
		if (col is SphereCollider sphereCollider)
		{
			result = sphereCollider.radius * transformScale.Max();
		}
		else if (col is BoxCollider boxCollider)
		{
			result = Vector3.Scale(boxCollider.size, transformScale).Max() * 0.5f;
		}
		else if (col is CapsuleCollider { direction: var direction } capsuleCollider)
		{
			float num = direction switch
			{
				0 => transformScale.y, 
				1 => transformScale.x, 
				_ => transformScale.x, 
			};
			result = capsuleCollider.radius * num;
		}
		else if (col is MeshCollider { bounds: var bounds })
		{
			result = Vector3.Scale(bounds.size, transformScale).Max() * 0.5f;
		}
		return result;
	}

	public static MonumentInfo GetMonument(this Collider collider, bool ignoreEntity = true)
	{
		if (collider == null)
		{
			return null;
		}
		if (ignoreEntity && collider.ToBaseEntity() != null)
		{
			return null;
		}
		CachedMonumentComponent cachedMonumentComponent = collider.GetComponent<CachedMonumentComponent>();
		if (cachedMonumentComponent == null || cachedMonumentComponent.LastPosition != collider.transform.position)
		{
			cachedMonumentComponent = collider.gameObject.GetOrAddComponent<CachedMonumentComponent>();
			PreventBuildingMonumentTag component = collider.GetComponent<PreventBuildingMonumentTag>();
			if (component != null)
			{
				cachedMonumentComponent.UpdateMonument(component.GetAttachedMonument(), collider);
				return cachedMonumentComponent.Monument;
			}
			List<Collider> obj = Facepunch.Pool.Get<List<Collider>>();
			GamePhysics.OverlapBounds(collider.bounds, obj, 536870912, QueryTriggerInteraction.Collide);
			foreach (Collider item in obj)
			{
				component = item.GetComponent<PreventBuildingMonumentTag>();
				if (component != null)
				{
					cachedMonumentComponent.UpdateMonument(component.GetAttachedMonument(), collider);
				}
			}
			Facepunch.Pool.FreeUnmanaged(ref obj);
		}
		return cachedMonumentComponent?.Monument;
	}
}
