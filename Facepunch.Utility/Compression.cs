using System;
using Ionic.Zlib;
using UnityEngine;

namespace Facepunch.Utility;

public class Compression
{
	public static byte[] Compress(byte[] data)
	{
		try
		{
			return GZipStream.CompressBuffer(data);
		}
		catch (Exception)
		{
			return null;
		}
	}

	public static byte[] Uncompress(byte[] data)
	{
		return GZipStream.UncompressBuffer(data);
	}

	public static int PackVector3ToInt(Vector3 vector, float minValue, float maxValue)
	{
		Vector3 vector2 = new Vector3(Mathf.InverseLerp(minValue, maxValue, vector.x), Mathf.InverseLerp(minValue, maxValue, vector.y), Mathf.InverseLerp(minValue, maxValue, vector.z));
		return 0 | ((int)(vector2.x * 1023f) << 20) | ((int)(vector2.y * 1023f) << 10) | (int)(vector2.z * 1023f);
	}

	public static Vector3 UnpackVector3FromInt(int packed, float minValue, float maxValue)
	{
		Vector3 vector = new Vector3((packed >> 20) & 0x3FF, (packed >> 10) & 0x3FF, packed & 0x3FF) / 1023f;
		return new Vector3(Mathf.Lerp(minValue, maxValue, vector.x), Mathf.Lerp(minValue, maxValue, vector.y), Mathf.Lerp(minValue, maxValue, vector.z));
	}
}
