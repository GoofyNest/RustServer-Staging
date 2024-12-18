using FIMSpace.Basics;
using UnityEngine;

namespace FIMSpace.GroundFitter;

public class FGroundRotator : FGroundFitter_Base
{
	[Tooltip("Root transform should be first object in the hierarchy of your movement game object")]
	public Transform RootTransform;

	public EFUpdateClock UpdateClock;

	private Quaternion initLocalRotation;

	private Vector3 mappingRight;

	private Vector3 mappingUp;

	private Vector3 mappingForward;

	protected override void Reset()
	{
		base.Reset();
		RelativeLookUp = false;
		RelativeLookUpBias = 0f;
		GlueToGround = false;
	}

	protected override void Start()
	{
		base.Start();
		initLocalRotation = TransformToRotate.localRotation;
		mappingForward = base.transform.InverseTransformDirection(RootTransform.forward);
		mappingUp = base.transform.InverseTransformDirection(RootTransform.up);
		mappingRight = base.transform.InverseTransformDirection(RootTransform.right);
	}

	private void Update()
	{
		if (UpdateClock != EFUpdateClock.FixedUpdate)
		{
			TransformToRotate.localRotation = initLocalRotation;
		}
	}

	private void FixedUpdate()
	{
		if (UpdateClock == EFUpdateClock.FixedUpdate)
		{
			TransformToRotate.localRotation = initLocalRotation;
		}
	}

	private void LateUpdate()
	{
		deltaTime = Time.deltaTime;
		FitToGround();
	}

	internal override void RotationCalculations()
	{
		targetRotationToApply = helperRotation;
		targetRotationToApply *= RootTransform.rotation;
		Vector3 eulerAngles = targetRotationToApply.eulerAngles;
		targetRotationToApply = Quaternion.Euler(Mathf.Clamp(FLogicMethods.WrapAngle(eulerAngles.x), 0f - MaxForwardRotation, MaxForwardRotation) * (1f - MildForwardValue), eulerAngles.y, Mathf.Clamp(FLogicMethods.WrapAngle(eulerAngles.z), 0f - MaxHorizontalRotation, MaxHorizontalRotation) * (1f - MildHorizontalValue));
		eulerAngles = targetRotationToApply.eulerAngles;
		eulerAngles = RootTransform.rotation.QToLocal(Quaternion.Euler(eulerAngles)).eulerAngles;
		Quaternion rotation = TransformToRotate.rotation;
		if (eulerAngles.x != 0f)
		{
			rotation *= Quaternion.AngleAxis(eulerAngles.x, mappingRight);
		}
		if (eulerAngles.y != 0f)
		{
			rotation *= Quaternion.AngleAxis(eulerAngles.y, mappingUp);
		}
		if (eulerAngles.z != 0f)
		{
			rotation *= Quaternion.AngleAxis(eulerAngles.z, mappingForward);
		}
		TransformToRotate.rotation = rotation;
	}
}
