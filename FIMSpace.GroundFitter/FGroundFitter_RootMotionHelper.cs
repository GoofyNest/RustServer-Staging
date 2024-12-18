using UnityEngine;

namespace FIMSpace.GroundFitter;

public class FGroundFitter_RootMotionHelper : MonoBehaviour
{
	public FGroundFitter_Movement MovementController;

	public FGroundFitter_Base_RootMotion OptionalFitter;

	private void OnAnimatorMove()
	{
		if ((bool)MovementController)
		{
			MovementController.OnAnimatorMove();
		}
		else if ((bool)OptionalFitter)
		{
			OptionalFitter.OnAnimatorMove();
		}
		else
		{
			Object.Destroy(this);
		}
	}
}
