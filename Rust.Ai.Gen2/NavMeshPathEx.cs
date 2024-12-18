using UnityEngine;
using UnityEngine.AI;

namespace Rust.Ai.Gen2;

public static class NavMeshPathEx
{
	private static Vector3[] cornersBuffer = new Vector3[128];

	public static float GetPathLength(this NavMeshPath path)
	{
		using (TimeWarning.New("GetPathLength"))
		{
			float num = 0f;
			int cornersNonAlloc = path.GetCornersNonAlloc(cornersBuffer);
			if (cornersNonAlloc < 2)
			{
				return num;
			}
			for (int i = 0; i < cornersNonAlloc - 1; i++)
			{
				num += Vector3.Distance(cornersBuffer[i], cornersBuffer[i + 1]);
			}
			return num;
		}
	}

	public static Vector3 GetOrigin(this NavMeshPath path)
	{
		using (TimeWarning.New("GetOrigin"))
		{
			if (path.GetCornersNonAlloc(cornersBuffer) < 1)
			{
				return Vector3.zero;
			}
			return cornersBuffer[0];
		}
	}

	public static Vector3 GetDestination(this NavMeshPath path)
	{
		using (TimeWarning.New("GetDestination"))
		{
			int cornersNonAlloc = path.GetCornersNonAlloc(cornersBuffer);
			if (cornersNonAlloc < 1)
			{
				return Vector3.zero;
			}
			return cornersBuffer[cornersNonAlloc - 1];
		}
	}
}
