using System.Collections.Generic;
using ConVar;
using Facepunch;
using Facepunch.Rust;
using ProtoBuf;
using UnityEngine;

public class ZiplineMountable : BaseMountable
{
	public float MoveSpeed = 4f;

	public float ForwardAdditive = 5f;

	public CapsuleCollider ZipCollider;

	public Transform ZiplineGrabRoot;

	public Transform LeftHandIkPoint;

	public Transform RightHandIkPoint;

	public float SpeedUpTime = 0.6f;

	public bool EditorHoldInPlace;

	private List<Vector3> linePoints;

	private const Flags PushForward = Flags.Reserved1;

	public AnimationCurve MountPositionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

	public AnimationCurve MountRotationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

	public float MountEaseInTime = 0.5f;

	private const Flags ShowHandle = Flags.Reserved2;

	private float additiveValue;

	private float currentTravelDistance;

	private TimeSince mountTime;

	private bool hasEnded;

	private List<Collider> ignoreColliders = new List<Collider>();

	private Vector3 lastSafePosition;

	private Vector3 startPosition = Vector3.zero;

	private Vector3 endPosition = Vector3.zero;

	private Quaternion startRotation = Quaternion.identity;

	private Quaternion endRotation = Quaternion.identity;

	private float elapsedMoveTime;

	private bool isAnimatingIn;

	private Vector3 ProcessBezierMovement(float distanceToTravel)
	{
		if (linePoints == null)
		{
			return Vector3.zero;
		}
		float num = 0f;
		for (int i = 0; i < linePoints.Count - 1; i++)
		{
			float num2 = Vector3.Distance(linePoints[i], linePoints[i + 1]);
			if (num + num2 > distanceToTravel)
			{
				float t = Mathf.Clamp((distanceToTravel - num) / num2, 0f, 1f);
				return Vector3.Lerp(linePoints[i], linePoints[i + 1], t);
			}
			num += num2;
		}
		return linePoints[linePoints.Count - 1];
	}

	private Vector3 GetLineEndPoint(bool applyDismountOffset = false)
	{
		if (applyDismountOffset && linePoints != null)
		{
			Vector3 normalized = (linePoints[linePoints.Count - 2] - linePoints[linePoints.Count - 1]).normalized;
			return linePoints[linePoints.Count - 1] + normalized * 1.5f;
		}
		return linePoints?[linePoints.Count - 1] ?? Vector3.zero;
	}

	private Vector3 GetNextLinePoint(Transform forTransform)
	{
		Vector3 position = forTransform.position;
		Vector3 forward = forTransform.forward;
		for (int i = 1; i < linePoints.Count - 1; i++)
		{
			Vector3 normalized = (linePoints[i + 1] - position).normalized;
			Vector3 normalized2 = (linePoints[i - 1] - position).normalized;
			float num = Vector3.Dot(forward, normalized);
			float num2 = Vector3.Dot(forward, normalized2);
			if (num > 0f && num2 < 0f)
			{
				return linePoints[i + 1];
			}
		}
		return GetLineEndPoint();
	}

	public override void ResetState()
	{
		base.ResetState();
		additiveValue = 0f;
		currentTravelDistance = 0f;
		hasEnded = false;
		linePoints = null;
	}

	public override float MaxVelocity()
	{
		return MoveSpeed + ForwardAdditive;
	}

	public void SetDestination(List<Vector3> targetLinePoints, Vector3 lineStartPos, Quaternion lineStartRot)
	{
		linePoints = targetLinePoints;
		currentTravelDistance = 0f;
		mountTime = 0f;
		GamePhysics.OverlapSphere(base.transform.position, 6f, ignoreColliders, 1084293377);
		startPosition = base.transform.position;
		startRotation = base.transform.rotation;
		lastSafePosition = startPosition;
		endPosition = lineStartPos;
		endRotation = lineStartRot;
		elapsedMoveTime = 0f;
		isAnimatingIn = true;
		InvokeRepeating(MovePlayerToPosition, 0f, 0f);
		Analytics.Server.UsedZipline();
	}

	private void Update()
	{
		if (linePoints == null || base.isClient || isAnimatingIn || hasEnded)
		{
			return;
		}
		float num = (MoveSpeed + additiveValue * ForwardAdditive) * Mathf.Clamp((float)mountTime / SpeedUpTime, 0f, 1f) * UnityEngine.Time.smoothDeltaTime;
		currentTravelDistance += num;
		Vector3 vector = ProcessBezierMovement(currentTravelDistance);
		List<RaycastHit> obj = Facepunch.Pool.Get<List<RaycastHit>>();
		Vector3 position = vector.WithY(vector.y - ZipCollider.height * 0.6f);
		Vector3 position2 = vector;
		GamePhysics.CapsuleSweep(position, position2, ZipCollider.radius, base.transform.forward, num, obj, 1084293377);
		foreach (RaycastHit item in obj)
		{
			if (!(item.collider == ZipCollider) && !ignoreColliders.Contains(item.collider) && !(item.collider.GetComponentInParent<PowerlineNode>() != null) && !(item.collider.GetComponent<TreeEntity>() != null) && !(item.collider.GetComponentInParent<TreeEntity>() != null))
			{
				ZiplineMountable componentInParent = item.collider.GetComponentInParent<ZiplineMountable>();
				if (componentInParent != null)
				{
					componentInParent.EndZipline();
				}
				if (!GetDismountPosition(GetMounted(), out var _))
				{
					base.transform.position = lastSafePosition;
				}
				EndZipline();
				Facepunch.Pool.FreeUnmanaged(ref obj);
				return;
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		if (Vector3.Distance(vector, GetLineEndPoint()) < 0.1f)
		{
			base.transform.position = GetLineEndPoint(applyDismountOffset: true);
			hasEnded = true;
			return;
		}
		if (Vector3.Distance(lastSafePosition, base.transform.position) > 0.75f)
		{
			lastSafePosition = base.transform.position;
		}
		Vector3 normalized = (vector - base.transform.position.WithY(vector.y)).normalized;
		base.transform.position = Vector3.Lerp(base.transform.position, vector, UnityEngine.Time.deltaTime * 12f);
		base.transform.forward = normalized;
	}

	public override void PlayerServerInput(InputState inputState, BasePlayer player)
	{
		base.PlayerServerInput(inputState, player);
		if (linePoints != null)
		{
			if (hasEnded)
			{
				EndZipline();
				return;
			}
			Vector3 position = base.transform.position;
			float num = ((GetNextLinePoint(base.transform).y < position.y + 0.1f && inputState.IsDown(BUTTON.FORWARD)) ? 1f : 0f);
			additiveValue = Mathf.MoveTowards(additiveValue, num, (float)Server.tickrate * ((num > 0f) ? 4f : 2f));
			SetFlag(Flags.Reserved1, additiveValue > 0.5f);
		}
	}

	private void EndZipline()
	{
		DismountAllPlayers();
	}

	public override void OnPlayerDismounted(BasePlayer player)
	{
		base.OnPlayerDismounted(player);
		if (!base.IsDestroyed)
		{
			Kill();
		}
	}

	public override bool ValidDismountPosition(BasePlayer player, Vector3 disPos)
	{
		ZipCollider.enabled = false;
		bool result = base.ValidDismountPosition(player, disPos);
		ZipCollider.enabled = true;
		return result;
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		if (linePoints == null)
		{
			return;
		}
		if (info.msg.ziplineMountable == null)
		{
			info.msg.ziplineMountable = Facepunch.Pool.Get<ProtoBuf.ZiplineMountable>();
		}
		info.msg.ziplineMountable.linePoints = Facepunch.Pool.Get<List<VectorData>>();
		foreach (Vector3 linePoint in linePoints)
		{
			info.msg.ziplineMountable.linePoints.Add(linePoint);
		}
	}

	private void MovePlayerToPosition()
	{
		elapsedMoveTime += UnityEngine.Time.deltaTime;
		float num = Mathf.Clamp(elapsedMoveTime / MountEaseInTime, 0f, 1f);
		Vector3 localPosition = Vector3.Lerp(startPosition, endPosition, MountPositionCurve.Evaluate(num));
		Quaternion localRotation = Quaternion.Lerp(startRotation, endRotation, MountRotationCurve.Evaluate(num));
		base.transform.localPosition = localPosition;
		base.transform.localRotation = localRotation;
		if (num >= 1f)
		{
			isAnimatingIn = false;
			SetFlag(Flags.Reserved2, b: true);
			mountTime = 0f;
			CancelInvoke(MovePlayerToPosition);
		}
	}

	public override void OnFlagsChanged(Flags old, Flags next)
	{
		base.OnFlagsChanged(old, next);
		if (base.isServer && old.HasFlag(Flags.Busy) && !next.HasFlag(Flags.Busy) && !base.IsDestroyed)
		{
			Kill();
		}
	}
}
