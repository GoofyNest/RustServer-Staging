using System.Collections.Generic;
using UnityEngine;

namespace FIMSpace.FEditor;

public static class FGUI_Finders
{
	public static Component FoundAnimator;

	private static bool checkForAnim = true;

	private static int clicks = 0;

	public static void ResetFinders(bool resetClicks = true)
	{
		checkForAnim = true;
		FoundAnimator = null;
		if (resetClicks)
		{
			clicks = 0;
		}
	}

	public static bool CheckForAnimator(GameObject root, bool needAnimatorBox = true, bool drawInactiveWarning = true, int clicksTohide = 1)
	{
		bool flag = false;
		if (checkForAnim)
		{
			FoundAnimator = SearchForParentWithAnimator(root);
		}
		if ((bool)FoundAnimator)
		{
			Animation animation = FoundAnimator as Animation;
			Animator animator = FoundAnimator as Animator;
			if ((bool)animation && animation.enabled)
			{
				flag = true;
			}
			if ((bool)animator)
			{
				if (animator.enabled)
				{
					flag = true;
				}
				if (animator.runtimeAnimatorController == null)
				{
					drawInactiveWarning = false;
					flag = false;
				}
			}
			if (needAnimatorBox && drawInactiveWarning && flag)
			{
			}
		}
		else if (needAnimatorBox)
		{
			_ = clicks;
		}
		checkForAnim = false;
		return flag;
	}

	public static Component SearchForParentWithAnimator(GameObject root)
	{
		Animation componentInChildren = root.GetComponentInChildren<Animation>();
		if ((bool)componentInChildren)
		{
			return componentInChildren;
		}
		Animator componentInChildren2 = root.GetComponentInChildren<Animator>();
		if ((bool)componentInChildren2)
		{
			return componentInChildren2;
		}
		if (root.transform.parent != null)
		{
			Transform parent = root.transform.parent;
			while (parent != null)
			{
				componentInChildren = parent.GetComponent<Animation>();
				if ((bool)componentInChildren)
				{
					return componentInChildren;
				}
				componentInChildren2 = parent.GetComponent<Animator>();
				if ((bool)componentInChildren2)
				{
					return componentInChildren2;
				}
				parent = parent.parent;
			}
		}
		return null;
	}

	public static SkinnedMeshRenderer GetBoneSearchArray(Transform root)
	{
		List<SkinnedMeshRenderer> list = new List<SkinnedMeshRenderer>();
		SkinnedMeshRenderer skinnedMeshRenderer = null;
		Transform[] componentsInChildren = root.GetComponentsInChildren<Transform>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			SkinnedMeshRenderer component = componentsInChildren[i].GetComponent<SkinnedMeshRenderer>();
			if ((bool)component)
			{
				list.Add(component);
			}
		}
		if (list.Count == 0)
		{
			Transform transform = root;
			while (transform != null && !(transform.parent == null))
			{
				transform = transform.parent;
			}
			componentsInChildren = transform.GetComponentsInChildren<Transform>();
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				SkinnedMeshRenderer component2 = componentsInChildren[i].GetComponent<SkinnedMeshRenderer>();
				if (!list.Contains(component2) && (bool)component2)
				{
					list.Add(component2);
				}
			}
		}
		if (list.Count > 1)
		{
			skinnedMeshRenderer = list[0];
			for (int j = 1; j < list.Count; j++)
			{
				if (list[j].bones.Length > skinnedMeshRenderer.bones.Length)
				{
					skinnedMeshRenderer = list[j];
				}
			}
		}
		else if (list.Count > 0)
		{
			skinnedMeshRenderer = list[0];
		}
		if (skinnedMeshRenderer == null)
		{
			return null;
		}
		return skinnedMeshRenderer;
	}

	public static bool IsChildOf(Transform child, Transform rootParent)
	{
		Transform transform = child;
		while (transform != null && transform != rootParent)
		{
			transform = transform.parent;
		}
		if (transform == null)
		{
			return false;
		}
		return true;
	}

	public static Transform GetLastChild(Transform rootParent)
	{
		Transform transform = rootParent;
		while (transform.childCount > 0)
		{
			transform = transform.GetChild(0);
		}
		return transform;
	}

	public static bool? IsRightOrLeft(string name, bool includeNotSure = false)
	{
		string text = name.ToLower();
		if (text.Contains("right"))
		{
			return true;
		}
		if (text.Contains("left"))
		{
			return false;
		}
		if (text.StartsWith("r_"))
		{
			return true;
		}
		if (text.StartsWith("l_"))
		{
			return false;
		}
		if (text.EndsWith("_r"))
		{
			return true;
		}
		if (text.EndsWith("_l"))
		{
			return false;
		}
		if (text.StartsWith("r."))
		{
			return true;
		}
		if (text.StartsWith("l."))
		{
			return false;
		}
		if (text.EndsWith(".r"))
		{
			return true;
		}
		if (text.EndsWith(".l"))
		{
			return false;
		}
		if (includeNotSure)
		{
			if (text.Contains("r_"))
			{
				return true;
			}
			if (text.Contains("l_"))
			{
				return false;
			}
			if (text.Contains("_r"))
			{
				return true;
			}
			if (text.Contains("_l"))
			{
				return false;
			}
			if (text.Contains("r."))
			{
				return true;
			}
			if (text.Contains("l."))
			{
				return false;
			}
			if (text.Contains(".r"))
			{
				return true;
			}
			if (text.Contains(".l"))
			{
				return false;
			}
		}
		return null;
	}

	public static bool? IsRightOrLeft(Transform child, Transform itsRoot)
	{
		Vector3 vector = itsRoot.InverseTransformPoint(child.position);
		if (vector.x < 0f)
		{
			return false;
		}
		if (vector.x > 0f)
		{
			return true;
		}
		return null;
	}

	public static bool HaveKey(string text, string[] keys)
	{
		for (int i = 0; i < keys.Length; i++)
		{
			if (text.Contains(keys[i]))
			{
				return true;
			}
		}
		return false;
	}
}
