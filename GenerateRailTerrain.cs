using System.Linq;
using UnityEngine;

public class GenerateRailTerrain : ProceduralComponent
{
	public const int SmoothenLoops = 8;

	public const int SmoothenIterations = 8;

	public const int SmoothenY = 64;

	public const int SmoothenXZ = 32;

	public const int TransitionSteps = 8;

	private float AdjustTerrainFade(float xn, float zn)
	{
		int topology = TerrainMeta.TopologyMap.GetTopology(xn, zn);
		if (((uint)topology & 0x4000u) != 0)
		{
			return 0f;
		}
		if (((uint)topology & 0x8000u) != 0)
		{
			return 0.5f;
		}
		return 1f;
	}

	private float SmoothenFilter(PathList path, int index)
	{
		float num = (path.Start ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 8f, index)) : 1f);
		int topology = TerrainMeta.TopologyMap.GetTopology(path.Path.Points[index]);
		if (((uint)topology & 0x4000u) != 0)
		{
			return 0.1f * num;
		}
		if (((uint)topology & 0x8000u) != 0)
		{
			return 0.3f * num;
		}
		return num;
	}

	public override void Process(uint seed)
	{
		TerrainHeightMap heightMap = TerrainMeta.HeightMap;
		TerrainTopologyMap topologyMap = TerrainMeta.TopologyMap;
		for (int j = 0; j < 8; j++)
		{
			foreach (PathList rail in TerrainMeta.Path.Rails.AsEnumerable().Reverse())
			{
				PathInterpolator path = rail.Path;
				Vector3[] points = path.Points;
				for (int k = 0; k < points.Length; k++)
				{
					Vector3 vector = points[k];
					float num = heightMap.GetHeight(vector);
					int topology = topologyMap.GetTopology(vector);
					if (((uint)topology & 0x4000u) != 0)
					{
						num += 0.1875f;
					}
					if (((uint)topology & 0x8000u) != 0)
					{
						num += 0.125f;
					}
					if (rail.Start)
					{
						vector.y = Mathf.SmoothStep(vector.y, num, SmoothenFilter(rail, k));
					}
					else
					{
						vector.y = num;
					}
					points[k] = vector;
				}
				path.Smoothen(8, Vector3.up, (int i) => SmoothenFilter(rail, i));
				path.RecalculateTangents();
			}
			foreach (PathList item in TerrainMeta.Path.Rails.AsEnumerable().Reverse())
			{
				heightMap.Push();
				float intensity = 1f;
				float fademin = 0.125f;
				float fademax = Mathf.InverseLerp(8f, 0f, j);
				item.AdjustTerrainHeight((float xn, float zn) => intensity, (float xn, float zn) => Mathf.Lerp(fademin, fademax, AdjustTerrainFade(xn, zn)));
				heightMap.Pop();
			}
		}
		foreach (PathList rail2 in TerrainMeta.Path.Rails)
		{
			PathInterpolator path2 = rail2.Path;
			Vector3[] points2 = path2.Points;
			for (int l = 0; l < points2.Length; l++)
			{
				Vector3 vector2 = points2[l];
				float height = heightMap.GetHeight(vector2);
				if (rail2.Start)
				{
					vector2.y = Mathf.SmoothStep(vector2.y, height, SmoothenFilter(rail2, l));
				}
				else
				{
					vector2.y = height;
				}
				points2[l] = vector2;
			}
			path2.RecalculateTangents();
		}
	}
}
