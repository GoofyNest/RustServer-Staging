using UnityEngine;

namespace FIMSpace.FTools;

public class UniRotateBone
{
	private Vector3 dynamicUpReference = Vector3.up;

	public Transform transform { get; protected set; }

	public Vector3 initialLocalPosition { get; protected set; }

	public Quaternion initialLocalRotation { get; protected set; }

	public Vector3 initialLocalPositionInRootSpace { get; protected set; }

	public Quaternion initialLocalRotationInRootSpace { get; protected set; }

	public Vector3 right { get; protected set; }

	public Vector3 up { get; protected set; }

	public Vector3 forward { get; protected set; }

	public Vector3 dright { get; protected set; }

	public Vector3 dup { get; protected set; }

	public Vector3 dforward { get; protected set; }

	public Vector3 fromParentForward { get; protected set; }

	public Vector3 fromParentCross { get; protected set; }

	public Vector3 keyframedPosition { get; protected set; }

	public Quaternion keyframedRotation { get; protected set; }

	public Quaternion mapping { get; protected set; }

	public Quaternion dmapping { get; protected set; }

	public Transform root { get; protected set; }

	public Vector3 forwardReference { get; private set; }

	public Vector3 upReference { get; private set; }

	public Vector3 rightCrossReference { get; private set; }

	public UniRotateBone(Transform t, Transform root)
	{
		transform = t;
		initialLocalPosition = transform.localPosition;
		initialLocalRotation = transform.localRotation;
		if ((bool)root)
		{
			initialLocalPositionInRootSpace = root.InverseTransformPoint(t.position);
			initialLocalRotationInRootSpace = root.rotation.QToLocal(t.rotation);
		}
		forward = transform.InverseTransformDirection(root.forward);
		up = transform.InverseTransformDirection(root.up);
		right = transform.InverseTransformDirection(root.right);
		dforward = Quaternion.FromToRotation(forward, Vector3.forward) * Vector3.forward;
		dup = Quaternion.FromToRotation(up, Vector3.up) * Vector3.up;
		dright = Quaternion.FromToRotation(right, Vector3.right) * Vector3.right;
		if ((bool)t.parent)
		{
			fromParentForward = GetFromParentForward().normalized;
		}
		else
		{
			fromParentForward = forward;
		}
		fromParentCross = -Vector3.Cross(fromParentForward, forward);
		mapping = Quaternion.FromToRotation(right, Vector3.right);
		mapping *= Quaternion.FromToRotation(up, Vector3.up);
		dmapping = Quaternion.FromToRotation(fromParentForward, Vector3.right);
		dmapping *= Quaternion.FromToRotation(up, Vector3.up);
		this.root = root;
	}

	public Vector3 GetFromParentForward()
	{
		return transform.InverseTransformDirection(transform.position - transform.parent.position);
	}

	public Quaternion GetRootCompensateRotation(Quaternion initPelvisInWorld, Quaternion currInWorld, float armsRootCompensate)
	{
		Quaternion localRotation;
		if (armsRootCompensate > 0f)
		{
			localRotation = currInWorld.QToLocal(transform.parent.rotation);
			localRotation = initPelvisInWorld.QToWorld(localRotation);
			if (armsRootCompensate < 1f)
			{
				localRotation = Quaternion.Lerp(transform.parent.rotation, localRotation, armsRootCompensate);
			}
		}
		else
		{
			localRotation = transform.parent.rotation;
		}
		return localRotation;
	}

	public void RefreshCustomAxis(Vector3 up, Vector3 forward)
	{
		if (!(transform == null))
		{
			forwardReference = Quaternion.Inverse(transform.parent.rotation) * root.rotation * forward;
			upReference = Quaternion.Inverse(transform.parent.rotation) * root.rotation * up;
			rightCrossReference = Vector3.Cross(upReference, forwardReference);
		}
	}

	public void RefreshCustomAxis(Vector3 up, Vector3 forward, Quaternion customParentRot)
	{
		forwardReference = Quaternion.Inverse(customParentRot) * root.rotation * forward;
		upReference = Quaternion.Inverse(customParentRot) * root.rotation * up;
		rightCrossReference = Vector3.Cross(upReference, forwardReference);
	}

	public Quaternion RotateCustomAxis(float x, float y, UniRotateBone oRef)
	{
		Vector3 normal = Quaternion.AngleAxis(y, oRef.upReference) * Quaternion.AngleAxis(x, rightCrossReference) * oRef.forwardReference;
		Vector3 tangent = oRef.upReference;
		Vector3.OrthoNormalize(ref normal, ref tangent);
		Vector3 normal2 = normal;
		dynamicUpReference = tangent;
		Vector3.OrthoNormalize(ref normal2, ref dynamicUpReference);
		return transform.parent.rotation * Quaternion.LookRotation(normal2, dynamicUpReference) * Quaternion.Inverse(transform.parent.rotation * Quaternion.LookRotation(oRef.forwardReference, oRef.upReference));
	}

	internal Quaternion GetSourcePoseRotation()
	{
		return root.rotation.QToWorld(initialLocalRotationInRootSpace);
	}

	public Vector2 GetCustomLookAngles(Vector3 direction, UniRotateBone orientationsReference)
	{
		Vector3 vector = Quaternion.Inverse(transform.parent.rotation) * direction.normalized;
		Vector2 zero = Vector2.zero;
		zero.y = AngleAroundAxis(orientationsReference.forwardReference, vector, orientationsReference.upReference);
		Vector3 axis = Vector3.Cross(orientationsReference.upReference, vector);
		Vector3 firstDirection = vector - Vector3.Project(vector, orientationsReference.upReference);
		zero.x = AngleAroundAxis(firstDirection, vector, axis);
		return zero;
	}

	public static float AngleAroundAxis(Vector3 firstDirection, Vector3 secondDirection, Vector3 axis)
	{
		firstDirection -= Vector3.Project(firstDirection, axis);
		secondDirection -= Vector3.Project(secondDirection, axis);
		return Vector3.Angle(firstDirection, secondDirection) * (float)((!(Vector3.Dot(axis, Vector3.Cross(firstDirection, secondDirection)) < 0f)) ? 1 : (-1));
	}

	public Quaternion DynamicMapping()
	{
		return Quaternion.FromToRotation(right, transform.InverseTransformDirection(root.right)) * Quaternion.FromToRotation(up, transform.InverseTransformDirection(root.up));
	}

	public void CaptureKeyframeAnimation()
	{
		keyframedPosition = transform.position;
		keyframedRotation = transform.rotation;
	}

	public void RotateBy(float x, float y, float z)
	{
		Quaternion rotation = transform.rotation;
		if (x != 0f)
		{
			rotation *= Quaternion.AngleAxis(x, right);
		}
		if (y != 0f)
		{
			rotation *= Quaternion.AngleAxis(y, up);
		}
		if (z != 0f)
		{
			rotation *= Quaternion.AngleAxis(z, forward);
		}
		transform.rotation = rotation;
	}

	public void RotateBy(Vector3 angles)
	{
		RotateBy(angles.x, angles.y, angles.z);
	}

	public void RotateBy(Vector3 angles, float blend)
	{
		RotateBy(BlendAngle(angles.x, blend), BlendAngle(angles.y, blend), BlendAngle(angles.z, blend));
	}

	public void RotateByDynamic(Vector3 angles)
	{
		RotateByDynamic(angles.x, angles.y, angles.z);
	}

	public void RotateByDynamic(float x, float y, float z)
	{
		Quaternion rotation = transform.rotation;
		if (x != 0f)
		{
			rotation *= Quaternion.AngleAxis(x, transform.InverseTransformDirection(root.right));
		}
		if (y != 0f)
		{
			rotation *= Quaternion.AngleAxis(y, transform.InverseTransformDirection(root.up));
		}
		if (z != 0f)
		{
			rotation *= Quaternion.AngleAxis(z, transform.InverseTransformDirection(root.forward));
		}
		transform.rotation = rotation;
	}

	public Quaternion GetAngleRotation(float x, float y, float z)
	{
		Quaternion identity = Quaternion.identity;
		if (x != 0f)
		{
			identity *= Quaternion.AngleAxis(x, right);
		}
		if (y != 0f)
		{
			identity *= Quaternion.AngleAxis(y, up);
		}
		if (z != 0f)
		{
			identity *= Quaternion.AngleAxis(z, forward);
		}
		return identity;
	}

	public Quaternion GetAngleRotationDynamic(float x, float y, float z)
	{
		Quaternion identity = Quaternion.identity;
		if (x != 0f)
		{
			identity *= Quaternion.AngleAxis(x, transform.InverseTransformDirection(root.right));
		}
		if (y != 0f)
		{
			identity *= Quaternion.AngleAxis(y, transform.InverseTransformDirection(root.up));
		}
		if (z != 0f)
		{
			identity *= Quaternion.AngleAxis(z, transform.InverseTransformDirection(root.forward));
		}
		return identity;
	}

	public Quaternion GetAngleRotationDynamic(Vector3 angles)
	{
		return GetAngleRotationDynamic(angles.x, angles.y, angles.z);
	}

	public void RotateByDynamic(Vector3 angles, float blend)
	{
		RotateByDynamic(BlendAngle(angles.x, blend), BlendAngle(angles.y, blend), BlendAngle(angles.z, blend));
	}

	public void RotateByDynamic(float x, float y, float z, float blend)
	{
		RotateByDynamic(BlendAngle(x, blend), BlendAngle(y, blend), BlendAngle(z, blend));
	}

	public void RotateByDynamic(float x, float y, float z, Quaternion orientation)
	{
		Quaternion rotation = transform.rotation;
		if (x != 0f)
		{
			rotation *= Quaternion.AngleAxis(x, transform.InverseTransformDirection(orientation * Vector3.right));
		}
		if (y != 0f)
		{
			rotation *= Quaternion.AngleAxis(y, transform.InverseTransformDirection(orientation * Vector3.up));
		}
		if (z != 0f)
		{
			rotation *= Quaternion.AngleAxis(z, transform.InverseTransformDirection(orientation * Vector3.forward));
		}
		transform.rotation = rotation;
	}

	public void RotateXBy(float angle)
	{
		transform.rotation *= Quaternion.AngleAxis(angle, right);
	}

	public void RotateYBy(float angle)
	{
		transform.rotation *= Quaternion.AngleAxis(angle, up);
	}

	public void RotateZBy(float angle)
	{
		transform.rotation *= Quaternion.AngleAxis(angle, forward);
	}

	public void PreCalibrate()
	{
		transform.localPosition = initialLocalPosition;
		transform.localRotation = initialLocalRotation;
	}

	public Quaternion RotationTowards(Vector3 toDir)
	{
		return Quaternion.FromToRotation(transform.TransformDirection(fromParentForward).normalized, toDir.normalized) * transform.rotation;
	}

	public Quaternion RotationTowardsDynamic(Vector3 toDir)
	{
		return Quaternion.FromToRotation((transform.position - transform.parent.position).normalized, toDir.normalized) * transform.rotation;
	}

	public static float BlendAngle(float angle, float blend)
	{
		return Mathf.LerpAngle(0f, angle, blend);
	}

	public Vector3 Dir(Vector3 forward)
	{
		return transform.TransformDirection(forward);
	}

	public Vector3 IDir(Vector3 forward)
	{
		return transform.InverseTransformDirection(forward);
	}
}
