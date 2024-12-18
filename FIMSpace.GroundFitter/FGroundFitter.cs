using FIMSpace.Basics;
using UnityEngine;

namespace FIMSpace.GroundFitter;

public class FGroundFitter : FGroundFitter_Base_RootMotion
{
	[Header("< Specific Parameters >")]
	public EFUpdateClock UpdateClock;

	protected override void Reset()
	{
		base.Reset();
		RelativeLookUp = true;
		RelativeLookUpBias = 0.25f;
	}

	private void Update()
	{
		if (UpdateClock == EFUpdateClock.Update)
		{
			deltaTime = Time.deltaTime;
			FitToGround();
		}
	}

	private void FixedUpdate()
	{
		if (UpdateClock == EFUpdateClock.FixedUpdate)
		{
			deltaTime = Time.fixedDeltaTime;
			FitToGround();
		}
	}

	private void LateUpdate()
	{
		if (UpdateClock == EFUpdateClock.LateUpdate)
		{
			deltaTime = Time.deltaTime;
			FitToGround();
		}
	}

	public void RefreshDelta()
	{
		switch (UpdateClock)
		{
		case EFUpdateClock.Update:
			deltaTime = Time.deltaTime;
			break;
		case EFUpdateClock.LateUpdate:
			deltaTime = Time.deltaTime;
			break;
		case EFUpdateClock.FixedUpdate:
			deltaTime = Time.fixedDeltaTime;
			break;
		}
	}

	protected override void FitToGround()
	{
		HandleRootMotionSupport();
		base.FitToGround();
	}

	protected override void HandleRootMotionSupport()
	{
		base.HandleRootMotionSupport();
		if (HandleRootMotion)
		{
			UpdateClock = EFUpdateClock.LateUpdate;
		}
	}
}
