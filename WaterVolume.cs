using UnityEngine;

public class WaterVolume : TriggerBase
{
	public Bounds WaterBounds = new Bounds(Vector3.zero, Vector3.one);

	private OBB cachedBounds;

	private Transform cachedTransform;

	public Transform[] cutOffPlanes = new Transform[0];

	[Tooltip("Allows filling bota bags, jugs, etc. Don't turn this on if the player is responsible for filling this water volume as that will allow water duplication")]
	public bool naturalSource;

	public bool waterEnabled = true;

	private void OnEnable()
	{
		cachedTransform = base.transform;
		cachedBounds = new OBB(cachedTransform, WaterBounds);
	}

	private Plane GetWaterPlane()
	{
		return new Plane(cachedBounds.up, cachedBounds.position);
	}

	public bool Test(Vector3 pos, out WaterLevel.WaterInfo info)
	{
		if (!waterEnabled)
		{
			info = default(WaterLevel.WaterInfo);
			return false;
		}
		UpdateCachedTransform();
		if (cachedBounds.Contains(pos))
		{
			if (!CheckCutOffPlanes(pos, out var bottomCutY))
			{
				info = default(WaterLevel.WaterInfo);
				return false;
			}
			Vector3 vector = GetWaterPlane().ClosestPointOnPlane(pos);
			float y = (vector + cachedBounds.up * cachedBounds.extents.y).y;
			float y2 = (vector + -cachedBounds.up * cachedBounds.extents.y).y;
			y2 = Mathf.Max(y2, bottomCutY);
			info = default(WaterLevel.WaterInfo);
			info.isValid = true;
			info.currentDepth = Mathf.Max(0f, y - pos.y);
			info.overallDepth = Mathf.Max(0f, y - y2);
			info.surfaceLevel = y;
			return true;
		}
		info = default(WaterLevel.WaterInfo);
		return false;
	}

	public bool Test(Bounds bounds, out WaterLevel.WaterInfo info)
	{
		if (!waterEnabled)
		{
			info = default(WaterLevel.WaterInfo);
			return false;
		}
		UpdateCachedTransform();
		if (cachedBounds.Contains(bounds.ClosestPoint(cachedBounds.position)))
		{
			if (!CheckCutOffPlanes(bounds.center, out var bottomCutY))
			{
				info = default(WaterLevel.WaterInfo);
				return false;
			}
			Vector3 vector = GetWaterPlane().ClosestPointOnPlane(bounds.center);
			float y = (vector + cachedBounds.up * cachedBounds.extents.y).y;
			float y2 = (vector + -cachedBounds.up * cachedBounds.extents.y).y;
			y2 = Mathf.Max(y2, bottomCutY);
			info = default(WaterLevel.WaterInfo);
			info.isValid = true;
			info.currentDepth = Mathf.Max(0f, y - bounds.min.y);
			info.overallDepth = Mathf.Max(0f, y - y2);
			info.surfaceLevel = y;
			return true;
		}
		info = default(WaterLevel.WaterInfo);
		return false;
	}

	public bool Test(Vector3 start, Vector3 end, float radius, out WaterLevel.WaterInfo info)
	{
		if (!waterEnabled)
		{
			info = default(WaterLevel.WaterInfo);
			return false;
		}
		UpdateCachedTransform();
		Vector3 vector = (start + end) * 0.5f;
		float num = Mathf.Min(start.y, end.y) - radius;
		if (cachedBounds.Distance(start) < radius || cachedBounds.Distance(end) < radius)
		{
			if (!CheckCutOffPlanes(vector, out var bottomCutY))
			{
				info = default(WaterLevel.WaterInfo);
				return false;
			}
			Vector3 vector2 = GetWaterPlane().ClosestPointOnPlane(vector);
			float y = (vector2 + cachedBounds.up * cachedBounds.extents.y).y;
			float y2 = (vector2 + -cachedBounds.up * cachedBounds.extents.y).y;
			y2 = Mathf.Max(y2, bottomCutY);
			info = default(WaterLevel.WaterInfo);
			info.isValid = true;
			info.currentDepth = Mathf.Max(0f, y - num);
			info.overallDepth = Mathf.Max(0f, y - y2);
			info.surfaceLevel = y;
			return true;
		}
		info = default(WaterLevel.WaterInfo);
		return false;
	}

	private bool CheckCutOffPlanes(Vector3 pos, out float bottomCutY)
	{
		int num = cutOffPlanes.Length;
		bottomCutY = float.MaxValue;
		bool flag = true;
		for (int i = 0; i < num; i++)
		{
			if (cutOffPlanes[i] != null)
			{
				Vector3 vector = cutOffPlanes[i].InverseTransformPoint(pos);
				Vector3 position = cutOffPlanes[i].position;
				if (Vector3.Dot(cutOffPlanes[i].up, cachedBounds.up) < -0.1f)
				{
					bottomCutY = Mathf.Min(bottomCutY, position.y);
				}
				if (vector.y > 0f)
				{
					flag = false;
					break;
				}
			}
		}
		if (!flag)
		{
			return false;
		}
		return true;
	}

	private void UpdateCachedTransform()
	{
		if (cachedTransform != null && cachedTransform.hasChanged)
		{
			cachedBounds = new OBB(cachedTransform, WaterBounds);
			cachedTransform.hasChanged = false;
		}
	}

	internal override GameObject InterestedInObject(GameObject obj)
	{
		obj = base.InterestedInObject(obj);
		if (obj == null)
		{
			return null;
		}
		BaseEntity baseEntity = obj.ToBaseEntity();
		if (baseEntity == null)
		{
			return null;
		}
		return baseEntity.gameObject;
	}
}
