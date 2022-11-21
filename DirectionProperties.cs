using System;
using UnityEngine;

public class DirectionProperties : PrefabAttribute
{
	private const float radius = 200f;

	public Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

	public ProtectionProperties extraProtection;

	public Transform[] weakspots;

	protected override Type GetIndexedType()
	{
		return typeof(DirectionProperties);
	}

	public bool IsWeakspot(Transform tx, HitInfo info)
	{
		if (bounds.size == Vector3.zero)
		{
			return false;
		}
		BasePlayer initiatorPlayer = info.InitiatorPlayer;
		if (initiatorPlayer == null)
		{
			return false;
		}
		BaseEntity hitEntity = info.HitEntity;
		if (hitEntity == null)
		{
			return false;
		}
		Matrix4x4 worldToLocalMatrix = tx.worldToLocalMatrix;
		Vector3 b = worldToLocalMatrix.MultiplyPoint3x4(info.PointStart) - worldPosition;
		float num = worldForward.DotDegrees(b);
		Vector3 target = worldToLocalMatrix.MultiplyPoint3x4(info.HitPositionWorld);
		OBB oBB = new OBB(worldPosition, worldRotation, bounds);
		Vector3 position = initiatorPlayer.eyes.position;
		if (weakspots != null && weakspots.Length != 0)
		{
			Transform[] array = weakspots;
			foreach (Transform transform in array)
			{
				if (IsWeakspotVisible(hitEntity, position, tx.TransformPoint(transform.position)))
				{
					break;
				}
			}
		}
		else if (!IsWeakspotVisible(hitEntity, position, tx.TransformPoint(oBB.position)))
		{
			return false;
		}
		if (num > 100f)
		{
			return oBB.Contains(target);
		}
		return false;
	}

	private bool IsWeakspotVisible(BaseEntity hitEntity, Vector3 playerEyes, Vector3 weakspotPos)
	{
		if (!hitEntity.IsVisible(playerEyes, weakspotPos))
		{
			return false;
		}
		return true;
	}
}
