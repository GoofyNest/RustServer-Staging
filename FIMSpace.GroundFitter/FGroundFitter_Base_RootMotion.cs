using System;
using UnityEngine;

namespace FIMSpace.GroundFitter;

public abstract class FGroundFitter_Base_RootMotion : FGroundFitter_Base
{
	[Tooltip("Making ground fitter translate with root motion")]
	[HideInInspector]
	public bool HandleRootMotion;

	[SerializeField]
	[HideInInspector]
	protected Transform parentTransform;

	[SerializeField]
	[HideInInspector]
	protected CharacterController optionalCharContr;

	[SerializeField]
	[HideInInspector]
	protected bool rootMotionRotation = true;

	protected Animator rootMAnimator;

	protected override void Reset()
	{
		base.Reset();
		parentTransform = base.transform;
	}

	protected override void Start()
	{
		base.Start();
	}

	protected virtual void HandleRootMotionSupport()
	{
		if (HandleRootMotion)
		{
			if (!rootMAnimator)
			{
				rootMAnimator = GetComponentInChildren<Animator>();
			}
			if (rootMAnimator.gameObject != base.gameObject && !rootMAnimator.applyRootMotion && !rootMAnimator.GetComponent<FGroundFitter_RootMotionHelper>())
			{
				rootMAnimator.gameObject.AddComponent<FGroundFitter_RootMotionHelper>().OptionalFitter = this;
			}
			rootMAnimator.applyRootMotion = true;
		}
	}

	internal virtual void OnAnimatorMove()
	{
		if (!rootMAnimator)
		{
			return;
		}
		if ((bool)optionalCharContr)
		{
			if (rootMAnimator.deltaPosition != Vector3.zero)
			{
				if (TransformToRotate != base.transform)
				{
					optionalCharContr.Move(TransformToRotate.rotation * rootMAnimator.deltaPosition);
				}
				else
				{
					optionalCharContr.Move(rootMAnimator.deltaPosition);
				}
			}
			rootMAnimator.rootPosition = TransformToRotate.position;
		}
		else if (TransformToRotate != base.transform)
		{
			parentTransform.position += TransformToRotate.rotation * rootMAnimator.deltaPosition;
		}
		else
		{
			parentTransform.position += rootMAnimator.deltaPosition;
		}
		rootMAnimator.rootPosition = TransformToRotate.position;
		rootMAnimator.rootRotation = base.LastRotation;
		if (rootMotionRotation)
		{
			rootMAnimator.rootRotation = base.LastRotation;
			rootMAnimator.deltaRotation.ToAngleAxis(out var angle, out var axis);
			float y = (axis * angle * (MathF.PI / 180f)).y;
			UpAxisRotation += y * 57.290154f;
		}
	}
}
