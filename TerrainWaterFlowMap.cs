using System;
using Facepunch;
using UnityEngine;

public class TerrainWaterFlowMap : TerrainMap<byte>
{
	private const float TwoPi = MathF.PI * 2f;

	public override void Setup()
	{
		res = terrain.terrainData.heightmapResolution;
		src = (dst = new byte[res * res]);
	}

	public override void PostSetup()
	{
		using (TimeWarning.New("TerrainWaterFlowMap.PostSetup"))
		{
			WriteWaterFlowFromShoreVectors();
			WriteWaterFlowFromRivers();
		}
	}

	private void WriteWaterFlowFromShoreVectors()
	{
		Parallel.For(0, res * res, delegate(int i)
		{
			int index = i % res;
			int index2 = i / res;
			float num = Coordinate(index);
			float num2 = Coordinate(index2);
			int topology = TerrainMeta.TopologyMap.GetTopology(num, num2, 16f);
			Vector4 rawShoreVector = TerrainTexturing.Instance.GetRawShoreVector(new Vector2(num, num2));
			Vector3 flow = new Vector3(rawShoreVector.x, 0f, rawShoreVector.y);
			if (((uint)topology & 0x14080u) != 0)
			{
				SetFlowDirection(num, num2, flow);
			}
		});
	}

	private void WriteWaterFlowFromRivers()
	{
		foreach (PathList river in TerrainMeta.Path.Rivers)
		{
			river.AdjustTerrainWaterFlow(scaleWidthWithLength: true);
		}
	}

	public Vector3 GetFlowDirection(Vector3 worldPos)
	{
		float normX = TerrainMeta.NormalizeX(worldPos.x);
		float normZ = TerrainMeta.NormalizeZ(worldPos.z);
		return GetFlowDirection(normX, normZ);
	}

	public Vector3 GetFlowDirection(Vector2 worldPos2D)
	{
		float normX = TerrainMeta.NormalizeX(worldPos2D.x);
		float normZ = TerrainMeta.NormalizeZ(worldPos2D.y);
		return GetFlowDirection(normX, normZ);
	}

	public Vector3 GetFlowDirection(float normX, float normZ)
	{
		int num = Index(normX);
		int num2 = Index(normZ);
		float f = ByteToAngle(src[num2 * res + num]);
		return new Vector3(Mathf.Sin(f), 0f, Mathf.Cos(f));
	}

	public void SetFlowDirection(Vector3 worldPos, Vector3 flow)
	{
		float normX = TerrainMeta.NormalizeX(worldPos.x);
		float normZ = TerrainMeta.NormalizeZ(worldPos.z);
		SetFlowDirection(normX, normZ, flow);
	}

	public void SetFlowDirection(float normX, float normZ, Vector3 flow)
	{
		int num = Index(normX);
		int num2 = Index(normZ);
		Vector3 normalized = flow.XZ().normalized;
		byte b = AngleToByte(Mathf.Atan2(normalized.x, normalized.z));
		src[num2 * res + num] = b;
	}

	private static float ByteToAngle(byte b)
	{
		return (float)(int)b / 255f * (MathF.PI * 2f) - MathF.PI;
	}

	private static byte AngleToByte(float a)
	{
		a = Mathf.Clamp(a, -MathF.PI, MathF.PI);
		return (byte)Mathf.RoundToInt((a + MathF.PI) / (MathF.PI * 2f) * 255f);
	}
}
