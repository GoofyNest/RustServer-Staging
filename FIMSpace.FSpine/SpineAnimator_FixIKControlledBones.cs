using UnityEngine;

namespace FIMSpace.FSpine;

[DefaultExecutionOrder(-12)]
[AddComponentMenu("FImpossible Creations/Spine Animator Utilities/Spine Animator IK Controlled Bone Fixer")]
public class SpineAnimator_FixIKControlledBones : MonoBehaviour
{
	public Transform SkeletonParentBone;

	[Tooltip("If bones are twisting with this option off you should turn it on (calibrating bone if it's animation don't use keyframes)")]
	public bool Calibrate = true;

	private Quaternion initLocalRot;

	private Vector3 initLocalPos;

	private Quaternion localRotation;

	private Vector3 localPosition;

	private void Start()
	{
		initLocalPos = base.transform.localPosition;
		initLocalRot = base.transform.localRotation;
	}

	public void Calibration()
	{
		if (Calibrate)
		{
			base.transform.localPosition = initLocalPos;
			base.transform.localRotation = initLocalRot;
		}
	}

	public void UpdateOnAnimator()
	{
		if (base.enabled)
		{
			localRotation = SkeletonParentBone.rotation.QToLocal(base.transform.rotation);
			localPosition = SkeletonParentBone.InverseTransformPoint(base.transform.position);
		}
	}

	public void UpdateAfterProcedural()
	{
		if (base.enabled)
		{
			base.transform.rotation = SkeletonParentBone.rotation.QToWorld(localRotation);
			base.transform.position = SkeletonParentBone.TransformPoint(localPosition);
		}
	}
}
