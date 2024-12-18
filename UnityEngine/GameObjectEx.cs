using System.Collections.Generic;
using Facepunch;
using Rust;
using Rust.Registry;

namespace UnityEngine;

public static class GameObjectEx
{
	public static BaseEntity ToBaseEntity(this GameObject go, bool allowDestroyed = false)
	{
		return go.transform.ToBaseEntity(allowDestroyed);
	}

	public static BaseEntity ToBaseEntity(this Collider collider, bool allowDestroyed = false)
	{
		return collider.transform.ToBaseEntity(allowDestroyed);
	}

	public static BaseEntity ToBaseEntity(this Transform transform, bool allowDestroyed = false)
	{
		IEntity entity = GetEntityFromRegistry(transform, allowDestroyed);
		if (entity == null && !transform.gameObject.activeInHierarchy)
		{
			entity = GetEntityFromComponent(transform);
		}
		return entity as BaseEntity;
	}

	public static bool IsOnLayer(this GameObject go, Layer rustLayer)
	{
		return go.IsOnLayer((int)rustLayer);
	}

	public static bool IsOnLayer(this GameObject go, int layer)
	{
		if (go != null)
		{
			return go.layer == layer;
		}
		return false;
	}

	private static IEntity GetEntityFromRegistry(Transform transform, bool allowDestroyed = false)
	{
		Transform transform2 = transform;
		IEntity entity = Entity.Get(transform2);
		while (entity == null && transform2.parent != null)
		{
			transform2 = transform2.parent;
			entity = Entity.Get(transform2);
		}
		if (entity == null || (entity.IsDestroyed && !allowDestroyed))
		{
			return null;
		}
		return entity;
	}

	private static IEntity GetEntityFromComponent(Transform transform)
	{
		Transform transform2 = transform;
		IEntity component = transform2.GetComponent<IEntity>();
		while (component == null && transform2.parent != null)
		{
			transform2 = transform2.parent;
			component = transform2.GetComponent<IEntity>();
		}
		if (component != null && !component.IsDestroyed)
		{
			return component;
		}
		return null;
	}

	public static void SetHierarchyGroup(this GameObject obj, string strRoot, bool groupActive = true, bool persistant = false)
	{
		obj.transform.SetParent(HierarchyUtil.GetRoot(strRoot, groupActive, persistant).transform, worldPositionStays: true);
	}

	public static bool HasComponent<T>(this GameObject obj) where T : Component
	{
		return obj.GetComponent<T>() != null;
	}

	public static bool HasComponentInParent<T>(this GameObject obj) where T : Component
	{
		return obj.GetComponentInParent<T>() != null;
	}

	public static void SetChildComponentsEnabled<T>(this GameObject gameObject, bool enabled) where T : MonoBehaviour
	{
		List<T> obj = Facepunch.Pool.Get<List<T>>();
		gameObject.GetComponentsInChildren(includeInactive: true, obj);
		foreach (T item in obj)
		{
			item.enabled = enabled;
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	public static GameObject FindInChildren(this GameObject parent, string name)
	{
		if (parent.name == name)
		{
			return parent;
		}
		foreach (Transform item in parent.transform)
		{
			GameObject gameObject = item.gameObject.FindInChildren(name);
			if (gameObject != null)
			{
				return gameObject;
			}
		}
		return null;
	}
}
