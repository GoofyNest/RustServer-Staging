using System.Collections.Generic;
using UnityEngine;

namespace FIMSpace;

public static class FTransformMethods
{
	public static Transform FindChildByNameInDepth(string name, Transform transform, bool findInDeactivated = true, string[] additionalContains = null)
	{
		if (transform.name == name)
		{
			return transform;
		}
		Transform[] componentsInChildren = transform.GetComponentsInChildren<Transform>(findInDeactivated);
		foreach (Transform transform2 in componentsInChildren)
		{
			if (!transform2.name.ToLower().Contains(name.ToLower()))
			{
				continue;
			}
			bool flag = false;
			if (additionalContains == null || additionalContains.Length == 0)
			{
				flag = true;
			}
			else
			{
				for (int j = 0; j < additionalContains.Length; j++)
				{
					if (transform2.name.ToLower().Contains(additionalContains[j].ToLower()))
					{
						flag = true;
						break;
					}
				}
			}
			if (flag)
			{
				return transform2;
			}
		}
		return null;
	}

	public static List<T> FindComponentsInAllChildren<T>(Transform transformToSearchIn, bool includeInactive = false, bool tryGetMultipleOutOfSingleObject = false) where T : Component
	{
		List<T> list = new List<T>();
		T[] components = transformToSearchIn.GetComponents<T>();
		foreach (T val in components)
		{
			if ((bool)val)
			{
				list.Add(val);
			}
		}
		Transform[] componentsInChildren = transformToSearchIn.GetComponentsInChildren<Transform>(includeInactive);
		foreach (Transform transform in componentsInChildren)
		{
			if (!tryGetMultipleOutOfSingleObject)
			{
				T component = transform.GetComponent<T>();
				if ((bool)component && !list.Contains(component))
				{
					list.Add(component);
				}
				continue;
			}
			components = transform.GetComponents<T>();
			foreach (T val2 in components)
			{
				if ((bool)val2 && !list.Contains(val2))
				{
					list.Add(val2);
				}
			}
		}
		return list;
	}

	public static T FindComponentInAllChildren<T>(Transform transformToSearchIn) where T : Component
	{
		Transform[] componentsInChildren = transformToSearchIn.GetComponentsInChildren<Transform>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			T component = componentsInChildren[i].GetComponent<T>();
			if ((bool)component)
			{
				return component;
			}
		}
		return null;
	}

	public static T FindComponentInAllParents<T>(Transform transformToSearchIn) where T : Component
	{
		Transform parent = transformToSearchIn.parent;
		for (int i = 0; i < 100; i++)
		{
			T component = parent.GetComponent<T>();
			if ((bool)component)
			{
				return component;
			}
			parent = parent.parent;
			if (parent == null)
			{
				return null;
			}
		}
		return null;
	}

	public static void ChangeActiveChildrenInside(Transform parentOfThem, bool active)
	{
		for (int i = 0; i < parentOfThem.childCount; i++)
		{
			parentOfThem.GetChild(i).gameObject.SetActive(active);
		}
	}

	public static void ChangeActiveThroughParentTo(Transform start, Transform end, bool active, bool changeParentsChildrenActivation = false)
	{
		start.gameObject.SetActive(active);
		Transform parent = start.parent;
		for (int i = 0; i < 100; i++)
		{
			if (parent == end)
			{
				break;
			}
			if (parent == null)
			{
				break;
			}
			if (changeParentsChildrenActivation)
			{
				ChangeActiveChildrenInside(parent, active);
			}
			parent = parent.parent;
		}
	}

	public static Transform GetObjectByPath(Transform root, string path)
	{
		if (root == null)
		{
			return null;
		}
		string[] array = path.Split('/');
		Transform transform = root;
		for (int i = 0; i < array.Length; i++)
		{
			Transform transform2 = transform.Find(array[i]);
			if (transform2 == null)
			{
				return null;
			}
			transform = transform2;
		}
		return transform;
	}

	public static string CalculateTransformPath(Transform child, Transform root)
	{
		if (child.parent == null)
		{
			return "";
		}
		string text = "";
		bool flag = true;
		while (child != root)
		{
			if (child == null)
			{
				return "";
			}
			text = ((!flag) ? (child.name + "/" + text) : child.name);
			flag = false;
			child = child.parent;
		}
		return text;
	}
}
