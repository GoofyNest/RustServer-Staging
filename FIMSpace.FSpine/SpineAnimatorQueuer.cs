using System.Collections.Generic;
using UnityEngine;

namespace FIMSpace.FSpine;

[DefaultExecutionOrder(-12)]
[AddComponentMenu("FImpossible Creations/Spine Animator Utilities/Spine Animator Queuer")]
public class SpineAnimatorQueuer : MonoBehaviour
{
	[Tooltip("Can be used to fade out all spine animators")]
	[FPD_Suffix(0f, 1f, FPD_SuffixAttribute.SuffixMode.From0to100, "%", true, 0)]
	public float SpineAnimatorsAmount = 1f;

	[SerializeField]
	internal List<FSpineAnimator> updateOrder;

	private void Update()
	{
		for (int num = updateOrder.Count - 1; num >= 0; num--)
		{
			if (updateOrder[num] == null)
			{
				updateOrder.RemoveAt(num);
			}
			else
			{
				if (updateOrder[num].enabled)
				{
					updateOrder[num].enabled = false;
				}
				updateOrder[num].Update();
			}
		}
	}

	private void FixedUpdate()
	{
		for (int num = updateOrder.Count - 1; num >= 0; num--)
		{
			if (updateOrder[num] == null)
			{
				updateOrder.RemoveAt(num);
			}
			else
			{
				if (updateOrder[num].enabled)
				{
					updateOrder[num].enabled = false;
				}
				updateOrder[num].FixedUpdate();
			}
		}
	}

	private void LateUpdate()
	{
		for (int i = 0; i < updateOrder.Count; i++)
		{
			if (SpineAnimatorsAmount < 1f)
			{
				updateOrder[i].SpineAnimatorAmount = SpineAnimatorsAmount;
			}
			updateOrder[i].LateUpdate();
		}
	}
}
