using UnityEngine;

namespace FIMSpace;

public static class FAnimatorMethods
{
	public static void LerpFloatValue(this Animator animator, string name = "RunWalk", float value = 0f, float deltaSpeed = 8f)
	{
		float @float = animator.GetFloat(name);
		@float = Mathf.Lerp(@float, value, Time.deltaTime * deltaSpeed);
		animator.SetFloat(name, @float);
	}

	public static bool CheckAnimationEnd(this Animator animator, int layer = 0, bool reverse = false, bool checkAnimLoop = true)
	{
		AnimatorStateInfo currentAnimatorStateInfo = animator.GetCurrentAnimatorStateInfo(layer);
		if (!animator.IsInTransition(layer))
		{
			if (checkAnimLoop)
			{
				if (!currentAnimatorStateInfo.loop && !reverse)
				{
					if (currentAnimatorStateInfo.normalizedTime > 0.98f)
					{
						return true;
					}
					if (currentAnimatorStateInfo.normalizedTime < 0.02f)
					{
						return true;
					}
				}
			}
			else if (!reverse)
			{
				if (currentAnimatorStateInfo.normalizedTime > 0.98f)
				{
					return true;
				}
				if (currentAnimatorStateInfo.normalizedTime < 0.02f)
				{
					return true;
				}
			}
		}
		return false;
	}

	public static void ResetLayersWeights(this Animator animator, float speed = 10f)
	{
		for (int i = 1; i < animator.layerCount; i++)
		{
			animator.SetLayerWeight(i, animator.GetLayerWeight(i).Lerp(0f, Time.deltaTime * speed));
		}
	}

	public static void LerpLayerWeight(this Animator animator, int layer = 0, float newValue = 1f, float speed = 8f)
	{
		float num = animator.GetLayerWeight(layer);
		num.Lerp(newValue, Time.deltaTime * speed);
		if (newValue == 1f && num > 0.999f)
		{
			num = 1f;
		}
		if (newValue == 0f && num < 0.01f)
		{
			num = 0f;
		}
		animator.SetLayerWeight(layer, num);
	}

	public static bool StateExists(this Animator animator, string clipName, int layer = 0)
	{
		int stateID = Animator.StringToHash(clipName);
		return animator.HasState(layer, stateID);
	}
}
