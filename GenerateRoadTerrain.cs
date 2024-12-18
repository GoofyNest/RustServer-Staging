using System.Linq;
using UnityEngine;

public class GenerateRoadTerrain : ProceduralComponent
{
	public const int SmoothenLoops = 2;

	public const int SmoothenIterations = 8;

	public const int SmoothenY = 16;

	public const int SmoothenXZ = 4;

	private float SmoothenFilter(PathList path, int index)
	{
		int topology = TerrainMeta.TopologyMap.GetTopology(path.Path.Points[index]);
		if (((uint)topology & 0x80400u) != 0)
		{
			return 0f;
		}
		if (((uint)topology & 0x100000u) != 0)
		{
			return 0.5f;
		}
		if (((uint)topology & 0x4000u) != 0)
		{
			return 0.1f;
		}
		if (((uint)topology & 0x8000u) != 0)
		{
			return 0.3f;
		}
		return 1f;
	}

	public override void Process(uint seed)
	{
		TerrainHeightMap heightMap = TerrainMeta.HeightMap;
		TerrainTopologyMap topologyMap = TerrainMeta.TopologyMap;
		for (int j = 0; j < 2; j++)
		{
			foreach (PathList road in TerrainMeta.Path.Roads.AsEnumerable().Reverse())
			{
				PathInterpolator path = road.Path;
				Vector3[] points = path.Points;
				for (int k = 0; k < points.Length; k++)
				{
					Vector3 vector = points[k];
					float num = heightMap.GetHeight(vector);
					int topology = topologyMap.GetTopology(vector);
					if (((uint)topology & 0x4000u) != 0)
					{
						num += 0.75f;
					}
					if (((uint)topology & 0x8000u) != 0)
					{
						num += 0.5f;
					}
					vector.y = num;
					points[k] = vector;
				}
				path.Smoothen(8, Vector3.up, (int i) => SmoothenFilter(road, i));
				path.RecalculateTangents();
			}
			foreach (PathList item in TerrainMeta.Path.Roads.AsEnumerable().Reverse())
			{
				heightMap.Push();
				float intensity = 1f;
				float fade = Mathf.InverseLerp(2f, 0f, j);
				item.AdjustTerrainHeight(intensity, fade);
				heightMap.Pop();
			}
			foreach (PathList item2 in TerrainMeta.Path.Rails.AsEnumerable().Reverse())
			{
				heightMap.Push();
				float intensity2 = 1f;
				float num2 = Mathf.InverseLerp(2f, 0f, j);
				item2.AdjustTerrainHeight(intensity2, num2 / 4f);
				heightMap.Pop();
			}
		}
		foreach (PathList road2 in TerrainMeta.Path.Roads)
		{
			PathInterpolator path2 = road2.Path;
			Vector3[] points2 = path2.Points;
			for (int l = 0; l < points2.Length; l++)
			{
				Vector3 vector2 = points2[l];
				vector2.y = heightMap.GetHeight(vector2);
				points2[l] = vector2;
			}
			path2.RecalculateTangents();
		}
	}
}
