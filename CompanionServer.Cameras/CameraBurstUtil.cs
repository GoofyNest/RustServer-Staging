using System.Runtime.CompilerServices;
using UnityEngine;

namespace CompanionServer.Cameras;

internal static class CameraBurstUtil
{
	private struct RaycastHitPublic
	{
		public Vector3 m_Point;

		public Vector3 m_Normal;

		public uint m_FaceID;

		public float m_Distance;

		public Vector2 m_UV;

		public int m_Collider;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int GetColliderId(this RaycastHit hit)
	{
		return hit.colliderInstanceID;
	}

	public unsafe static Collider GetCollider(int colliderInstanceId)
	{
		RaycastHitPublic raycastHitPublic = default(RaycastHitPublic);
		raycastHitPublic.m_Collider = colliderInstanceId;
		RaycastHitPublic raycastHitPublic2 = raycastHitPublic;
		return ((RaycastHit*)(&raycastHitPublic2))->collider;
	}
}
