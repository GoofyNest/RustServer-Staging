using UnityEngine;

public static class LODUtil
{
	public const float DefaultDistance = 1000f;

	public const float FarCameraDistance = 30f;

	public static float GetDistance(Transform transform, LODDistanceMode mode = LODDistanceMode.XYZ)
	{
		return GetDistance(transform.position, mode);
	}

	public static float GetDistance(Vector3 meshPos, LODDistanceMode mode = LODDistanceMode.XYZ)
	{
		return GetDistanceInternal(MainCamera.position, meshPos, mode);
	}

	private static float GetDistanceInternal(Vector3 cameraPos, Vector3 worldPos, LODDistanceMode mode)
	{
		if (MainCamera.isValid)
		{
			switch (mode)
			{
			case LODDistanceMode.XYZ:
				return Vector3.Distance(cameraPos, worldPos);
			case LODDistanceMode.XZ:
				return Vector3Ex.Distance2D(cameraPos, worldPos);
			case LODDistanceMode.Y:
				return Mathf.Abs(cameraPos.y - worldPos.y);
			}
		}
		return 1000f;
	}

	public static float VerifyDistance(float distance)
	{
		return Mathf.Min(500f, distance);
	}

	public static LODEnvironmentMode DetermineEnvironmentMode(Transform transform)
	{
		if (transform.CompareTag("OnlyVisibleUnderground") || transform.root.CompareTag("OnlyVisibleUnderground"))
		{
			return LODEnvironmentMode.Underground;
		}
		return LODEnvironmentMode.Default;
	}
}
