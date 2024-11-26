using UnityEngine;

namespace FIMSpace.GroundFitter;

public class FGroundFitter_Demo_RootMotionExample : FGroundFitter_Movement
{
	protected override void Start()
	{
		base.Start();
		clips.AddClip("RotateL");
		clips.AddClip("RotateR");
	}

	protected override void HandleAnimations()
	{
		if (Input.GetKey(KeyCode.A))
		{
			CrossfadeTo("RotateL");
			MoveVector = Vector3.zero;
		}
		else if (Input.GetKey(KeyCode.D))
		{
			CrossfadeTo("RotateR");
			MoveVector = Vector3.zero;
		}
		else if (ActiveSpeed > 0.15f)
		{
			if (Sprint)
			{
				CrossfadeTo("Run");
			}
			else
			{
				CrossfadeTo("Walk");
			}
		}
		else
		{
			CrossfadeTo("Idle");
		}
		if (animatorHaveAnimationSpeedProp)
		{
			if (inAir)
			{
				animator.LerpFloatValue("AnimationSpeed");
			}
			else
			{
				animator.LerpFloatValue("AnimationSpeed", MultiplySprintAnimation ? (ActiveSpeed / BaseSpeed) : Mathf.Min(1f, ActiveSpeed / BaseSpeed));
			}
		}
	}
}
