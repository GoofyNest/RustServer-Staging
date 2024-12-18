using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;

namespace UnityEngine;

public static class TransformEx
{
	public static string GetRecursiveName(this Transform transform, string strEndName = "")
	{
		string text = transform.name;
		if (!string.IsNullOrEmpty(strEndName))
		{
			text = text + "/" + strEndName;
		}
		if (transform.parent != null)
		{
			text = transform.parent.GetRecursiveName(text);
		}
		return text;
	}

	public static void RemoveComponent<T>(this Transform transform) where T : Component
	{
		T component = transform.GetComponent<T>();
		if (!(component == null))
		{
			GameManager.Destroy(component);
		}
	}

	public static void RetireAllChildren(this Transform transform, GameManager gameManager)
	{
		List<GameObject> obj = Facepunch.Pool.Get<List<GameObject>>();
		foreach (Transform item in transform)
		{
			if (!item.CompareTag("persist"))
			{
				obj.Add(item.gameObject);
			}
		}
		foreach (GameObject item2 in obj)
		{
			gameManager.Retire(item2);
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	public static List<Transform> GetChildren(this Transform transform)
	{
		return transform.Cast<Transform>().ToList();
	}

	public static void OrderChildren(this Transform tx, Func<Transform, object> selector)
	{
		foreach (Transform item in tx.Cast<Transform>().OrderBy(selector))
		{
			item.SetAsLastSibling();
		}
	}

	public static List<Transform> GetAllChildren(this Transform transform)
	{
		List<Transform> list = new List<Transform>();
		if (transform != null)
		{
			transform.AddAllChildren(list);
		}
		return list;
	}

	public static void AddAllChildren(this Transform transform, List<Transform> list)
	{
		list.Add(transform);
		for (int i = 0; i < transform.childCount; i++)
		{
			Transform child = transform.GetChild(i);
			if (!(child == null))
			{
				child.AddAllChildren(list);
			}
		}
	}

	public static Transform[] GetChildrenWithTag(this Transform transform, string strTag)
	{
		return (from x in transform.GetAllChildren()
			where x.CompareTag(strTag)
			select x).ToArray();
	}

	public static Matrix4x4 LocalToPrefabRoot(this Transform transform)
	{
		Matrix4x4 identity = Matrix4x4.identity;
		while (transform.parent != null)
		{
			identity *= Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);
			transform = transform.parent;
		}
		return identity;
	}

	public static void Identity(this GameObject go)
	{
		go.transform.localPosition = Vector3.zero;
		go.transform.localRotation = Quaternion.identity;
		go.transform.localScale = Vector3.one;
	}

	public static GameObject CreateChild(this GameObject go)
	{
		GameObject gameObject = new GameObject();
		gameObject.transform.parent = go.transform;
		gameObject.Identity();
		return gameObject;
	}

	public static GameObject InstantiateChild(this GameObject go, GameObject prefab)
	{
		GameObject gameObject = Instantiate.GameObject(prefab);
		gameObject.transform.SetParent(go.transform, worldPositionStays: false);
		gameObject.Identity();
		return gameObject;
	}

	public static void SetLayerRecursive(this GameObject go, int Layer)
	{
		if (go.layer != Layer)
		{
			go.layer = Layer;
		}
		for (int i = 0; i < go.transform.childCount; i++)
		{
			go.transform.GetChild(i).gameObject.SetLayerRecursive(Layer);
		}
	}

	public static bool DropToGround(this Transform transform, bool alignToNormal = false, float fRange = 100f)
	{
		if (transform.GetGroundInfo(out var pos, out var normal, fRange))
		{
			transform.position = pos;
			if (alignToNormal)
			{
				transform.rotation = Quaternion.LookRotation(transform.forward, normal);
			}
			return true;
		}
		return false;
	}

	public static bool GetGroundInfo(this Transform transform, out Vector3 pos, out Vector3 normal, float range = 100f)
	{
		return TransformUtil.GetGroundInfo(transform.position, out pos, out normal, range, transform);
	}

	public static bool GetGroundInfoTerrainOnly(this Transform transform, out Vector3 pos, out Vector3 normal, float range = 100f)
	{
		return TransformUtil.GetGroundInfoTerrainOnly(transform.position, out pos, out normal, range);
	}

	public static Bounds WorkoutRenderBounds(this Transform tx)
	{
		Bounds result = new Bounds(Vector3.zero, Vector3.zero);
		Renderer[] componentsInChildren = tx.GetComponentsInChildren<Renderer>();
		foreach (Renderer renderer in componentsInChildren)
		{
			if (!(renderer is ParticleSystemRenderer))
			{
				if (result.center == Vector3.zero)
				{
					result = renderer.bounds;
				}
				else
				{
					result.Encapsulate(renderer.bounds);
				}
			}
		}
		return result;
	}

	public static List<T> GetSiblings<T>(this Transform transform, bool includeSelf = false)
	{
		List<T> list = new List<T>();
		if (transform.parent == null)
		{
			return list;
		}
		for (int i = 0; i < transform.parent.childCount; i++)
		{
			Transform child = transform.parent.GetChild(i);
			if (includeSelf || !(child == transform))
			{
				T component = child.GetComponent<T>();
				if (component != null)
				{
					list.Add(component);
				}
			}
		}
		return list;
	}

	public static void DestroyChildren(this Transform transform)
	{
		for (int i = 0; i < transform.childCount; i++)
		{
			GameManager.Destroy(transform.GetChild(i).gameObject);
		}
	}

	public static void SetChildrenActive(this Transform transform, bool b)
	{
		for (int i = 0; i < transform.childCount; i++)
		{
			transform.GetChild(i).gameObject.SetActive(b);
		}
	}

	public static Transform ActiveChild(this Transform transform, string name, bool bDisableOthers)
	{
		Transform result = null;
		for (int i = 0; i < transform.childCount; i++)
		{
			Transform child = transform.GetChild(i);
			if (child.name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
			{
				result = child;
				child.gameObject.SetActive(value: true);
			}
			else if (bDisableOthers)
			{
				child.gameObject.SetActive(value: false);
			}
		}
		return result;
	}

	public static T GetComponentInChildrenIncludeDisabled<T>(this Transform transform) where T : Component
	{
		List<T> obj = Facepunch.Pool.Get<List<T>>();
		transform.GetComponentsInChildren(includeInactive: true, obj);
		T result = ((obj.Count > 0) ? obj[0] : null);
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return result;
	}

	public static bool HasComponentInChildrenIncludeDisabled<T>(this Transform transform) where T : Component
	{
		List<T> obj = Facepunch.Pool.Get<List<T>>();
		transform.GetComponentsInChildren(includeInactive: true, obj);
		bool result = obj.Count > 0;
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return result;
	}

	public static void SetHierarchyGroup(this Transform transform, string strRoot, bool groupActive = true, bool persistant = false)
	{
		transform.SetParent(HierarchyUtil.GetRoot(strRoot, groupActive, persistant).transform, worldPositionStays: true);
	}

	public static Bounds GetBounds(this Transform transform, bool includeRenderers = true, bool includeColliders = true, bool includeInactive = true)
	{
		Bounds result = new Bounds(Vector3.zero, Vector3.zero);
		if (includeRenderers)
		{
			MeshLOD[] componentsInChildren = transform.GetComponentsInChildren<MeshLOD>(includeInactive);
			foreach (MeshLOD meshLOD in componentsInChildren)
			{
				Mesh highestDetailMesh = meshLOD.GetHighestDetailMesh();
				if (highestDetailMesh != null)
				{
					Matrix4x4 matrix = transform.worldToLocalMatrix * meshLOD.transform.localToWorldMatrix;
					Bounds bounds = highestDetailMesh.bounds;
					result.Encapsulate(bounds.Transform(matrix));
				}
			}
			MeshFilter[] componentsInChildren2 = transform.GetComponentsInChildren<MeshFilter>(includeInactive);
			foreach (MeshFilter meshFilter in componentsInChildren2)
			{
				if ((bool)meshFilter.sharedMesh)
				{
					Matrix4x4 matrix2 = transform.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
					Bounds bounds2 = meshFilter.sharedMesh.bounds;
					result.Encapsulate(bounds2.Transform(matrix2));
				}
			}
			SkinnedMeshRenderer[] componentsInChildren3 = transform.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive);
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren3)
			{
				if ((bool)skinnedMeshRenderer.sharedMesh)
				{
					Matrix4x4 matrix3 = transform.worldToLocalMatrix * skinnedMeshRenderer.transform.localToWorldMatrix;
					Bounds bounds3 = skinnedMeshRenderer.sharedMesh.bounds;
					result.Encapsulate(bounds3.Transform(matrix3));
				}
			}
		}
		if (includeColliders)
		{
			MeshCollider[] componentsInChildren4 = transform.GetComponentsInChildren<MeshCollider>(includeInactive);
			foreach (MeshCollider meshCollider in componentsInChildren4)
			{
				if ((bool)meshCollider.sharedMesh && !meshCollider.isTrigger)
				{
					Matrix4x4 matrix4 = transform.worldToLocalMatrix * meshCollider.transform.localToWorldMatrix;
					Bounds bounds4 = meshCollider.sharedMesh.bounds;
					result.Encapsulate(bounds4.Transform(matrix4));
				}
			}
		}
		return result;
	}
}
