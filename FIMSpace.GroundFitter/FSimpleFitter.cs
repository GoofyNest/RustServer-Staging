using UnityEngine;

namespace FIMSpace.GroundFitter;

public class FSimpleFitter : FGroundFitter_Base_RootMotion
{
	protected override void Reset()
	{
		base.Reset();
		RelativeLookUp = false;
		RelativeLookUpBias = 0f;
	}

	private void LateUpdate()
	{
		deltaTime = Time.deltaTime;
		FitToGround();
	}

	protected override void FitToGround()
	{
		HandleRootMotionSupport();
		base.FitToGround();
	}

	protected override void HandleRootMotionSupport()
	{
		base.HandleRootMotionSupport();
	}
}
