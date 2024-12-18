using UnityEngine;

namespace FIMSpace.GroundFitter;

public class FGroundFitter_MovementLook : FGroundFitter_Movement
{
	[Header("Movement Look Options")]
	public Transform targetOfLook;

	[Range(0f, 1f)]
	public float FollowSpeed = 1f;

	public bool localOffset;

	private Vector3 targetPos;

	protected override void HandleTransforming()
	{
		base.HandleTransforming();
		if (MoveVector != Vector3.zero)
		{
			SetLookAtPosition(base.transform.position + Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y + RotationOffset, 0f) * Vector3.forward * 10f);
		}
		if ((bool)targetOfLook)
		{
			Vector3 vector = targetPos;
			if (localOffset)
			{
				vector = base.transform.TransformPoint(targetPos);
			}
			if (FollowSpeed >= 1f)
			{
				targetOfLook.position = vector;
			}
			else
			{
				targetOfLook.position = Vector3.Lerp(targetOfLook.position, vector, Mathf.Lerp(1f, 30f, FollowSpeed) * Time.deltaTime);
			}
		}
	}

	private void SetLookAtPosition(Vector3 tPos)
	{
		if (!localOffset)
		{
			targetPos = tPos + Vector3.up;
		}
		else
		{
			targetPos = base.transform.InverseTransformPoint(tPos + Vector3.up);
		}
	}
}
