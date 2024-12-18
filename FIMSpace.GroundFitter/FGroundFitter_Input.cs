using UnityEngine;

namespace FIMSpace.GroundFitter;

public class FGroundFitter_Input : FGroundFitter_InputBase
{
	protected virtual void Update()
	{
		if (Input.GetKeyDown(KeyCode.Space))
		{
			TriggerJump();
		}
		Vector3 zero = Vector3.zero;
		if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D))
		{
			if (Input.GetKey(KeyCode.LeftShift))
			{
				base.Sprint = true;
			}
			else
			{
				base.Sprint = false;
			}
			if (Input.GetKey(KeyCode.W))
			{
				zero.z += 1f;
			}
			if (Input.GetKey(KeyCode.A))
			{
				zero.x -= 1f;
			}
			if (Input.GetKey(KeyCode.D))
			{
				zero.x += 1f;
			}
			if (Input.GetKey(KeyCode.S))
			{
				zero.z -= 1f;
			}
			zero.Normalize();
			base.RotationOffset = Quaternion.LookRotation(zero).eulerAngles.y;
			base.MoveVector = Vector3.forward;
		}
		else
		{
			base.Sprint = false;
			base.MoveVector = Vector3.zero;
		}
		if (Input.GetKey(KeyCode.X))
		{
			base.MoveVector -= Vector3.forward;
		}
		if (Input.GetKey(KeyCode.Q))
		{
			base.MoveVector += Vector3.left;
		}
		if (Input.GetKey(KeyCode.E))
		{
			base.MoveVector += Vector3.right;
		}
		base.MoveVector.Normalize();
		controller.Sprint = base.Sprint;
		controller.MoveVector = base.MoveVector;
		controller.RotationOffset = base.RotationOffset;
	}
}
