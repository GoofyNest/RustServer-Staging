using UnityEngine;

public class GenerateErosion : ProceduralComponent
{
	public override void Process(uint seed)
	{
		if (World.Networked)
		{
			return;
		}
		TerrainTopologyMap topologyMap = TerrainMeta.TopologyMap;
		TerrainHeightMap heightmap = TerrainMeta.HeightMap;
		TerrainSplatMap splatmap = TerrainMeta.SplatMap;
		int erosion_res = heightmap.res;
		float[] erosion = new float[erosion_res * erosion_res];
		int deposit_res = splatmap.res;
		float[] deposit = new float[deposit_res * deposit_res];
		for (float num = TerrainMeta.Position.z; num < TerrainMeta.Position.z + TerrainMeta.Size.z; num += 10f)
		{
			for (float num2 = TerrainMeta.Position.x; num2 < TerrainMeta.Position.x + TerrainMeta.Size.x; num2 += 10f)
			{
				Vector3 worldPos = new Vector3(num2, 0f, num);
				float num3 = (worldPos.y = heightmap.GetHeight(worldPos));
				if (worldPos.y <= 15f)
				{
					continue;
				}
				Vector3 normal = heightmap.GetNormal(worldPos);
				if (normal.y <= 0.01f || normal.y >= 0.99f)
				{
					continue;
				}
				Vector2 normalized = normal.XZ2D().normalized;
				Vector2 vector = normalized;
				float num4 = 0f;
				float num5 = 0f;
				for (int i = 0; i < 300; i++)
				{
					worldPos.x += normalized.x;
					worldPos.z += normalized.y;
					if (Vector3.Angle(normalized, vector) > 90f)
					{
						break;
					}
					float num6 = TerrainMeta.NormalizeX(worldPos.x);
					float num7 = TerrainMeta.NormalizeZ(worldPos.z);
					int topology = topologyMap.GetTopology(num6, num7);
					if (((uint)topology & 0xB4990u) != 0)
					{
						break;
					}
					float height = heightmap.GetHeight(num6, num7);
					if (height > num3 + 8f)
					{
						break;
					}
					float num8 = Mathf.Min(height, num3);
					worldPos.y = Mathf.Lerp(worldPos.y, num8, 0.5f);
					normal = heightmap.GetNormal(worldPos);
					normalized = Vector2.Lerp(normalized, normal.XZ2D().normalized, 0.5f).normalized;
					num3 = num8;
					float num9 = 0f;
					float target = 0f;
					if ((topology & 0x800400) == 0)
					{
						float value = Vector3.Angle(Vector3.up, normal);
						num9 = Mathf.InverseLerp(5f, 15f, value);
						target = 1f;
						if ((topology & 0x8000) == 0)
						{
							target = num9;
						}
					}
					num4 = Mathf.MoveTowards(num4, num9, 0.05f);
					num5 = Mathf.MoveTowards(num5, target, 0.05f);
					if ((topologyMap.GetTopology(num6, num7, 10f) & 2) == 0)
					{
						int num10 = Mathf.Clamp((int)(num6 * (float)erosion_res), 0, erosion_res - 1);
						int num11 = Mathf.Clamp((int)(num7 * (float)erosion_res), 0, erosion_res - 1);
						int num12 = Mathf.Clamp((int)(num6 * (float)deposit_res), 0, deposit_res - 1);
						int num13 = Mathf.Clamp((int)(num7 * (float)deposit_res), 0, deposit_res - 1);
						erosion[num11 * erosion_res + num10] += num4;
						deposit[num13 * deposit_res + num12] += num5;
					}
				}
			}
		}
		Parallel.For(1, erosion_res - 1, delegate(int z)
		{
			for (int k = 1; k < erosion_res - 1; k++)
			{
				float t = CalculateDelta(erosion, erosion_res, k, z, 1f, 0.8f, 0.6f);
				float delta = (0f - Mathf.Lerp(0f, 0.25f, t)) * TerrainMeta.OneOverSize.y;
				heightmap.AddHeight(k, z, delta);
			}
		});
		Parallel.For(1, deposit_res - 1, delegate(int z)
		{
			for (int j = 1; j < deposit_res - 1; j++)
			{
				float splat = splatmap.GetSplat(j, z, 2);
				float splat2 = splatmap.GetSplat(j, z, 4);
				if (splat > 0.1f || splat2 > 0.1f)
				{
					float value2 = CalculateDelta(deposit, deposit_res, j, z, 1f, 0.4f, 0.2f);
					value2 = Mathf.InverseLerp(1f, 3f, value2);
					value2 = Mathf.Lerp(0f, 0.5f, value2);
					splatmap.AddSplat(j, z, 128, value2);
				}
				else
				{
					float value3 = CalculateDelta(deposit, deposit_res, j, z, 1f, 0.2f, 0.1f);
					float value4 = CalculateDelta(deposit, deposit_res, j, z, 1f, 0.8f, 0.4f);
					value3 = Mathf.InverseLerp(1f, 3f, value3);
					value4 = Mathf.InverseLerp(1f, 3f, value4);
					value3 = Mathf.Lerp(0f, 1f, value3);
					value4 = Mathf.Lerp(0f, 1f, value4);
					splatmap.AddSplat(j, z, 1, value4 * 0.5f);
					splatmap.AddSplat(j, z, 64, value3 * 0.7f);
					splatmap.AddSplat(j, z, 128, value3 * 0.5f);
				}
			}
		});
		static float CalculateDelta(float[] data, int res, int x, int z, float cntr, float side, float diag)
		{
			int num14 = x - 1;
			int num15 = x + 1;
			int num16 = z - 1;
			int num17 = z + 1;
			side /= 4f;
			diag /= 4f;
			float num18 = data[z * res + x];
			float num19 = data[z * res + num14] + data[z * res + num15] + data[num17 * res + x] + data[num17 * res + x];
			float num20 = data[num16 * res + num14] + data[num16 * res + num15] + data[num17 * res + num14] + data[num17 * res + num15];
			return cntr * num18 + side * num19 + diag * num20;
		}
	}
}
