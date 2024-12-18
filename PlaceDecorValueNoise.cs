using UnityEngine;

public class PlaceDecorValueNoise : ProceduralComponent
{
	public SpawnFilter Filter;

	public string ResourceFolder = string.Empty;

	public NoiseParameters Cluster = new NoiseParameters(2, 0.5f, 1f, 0f);

	public float ObjectDensity = 100f;

	public TerrainAnchorMode AnchorMode;

	[InspectorFlags]
	public SpawnFilterMode FilterMode = (SpawnFilterMode)(-1);

	public override void Process(uint seed)
	{
		if (World.Networked)
		{
			World.Spawn("Decor", "assets/bundled/prefabs/autospawn/" + ResourceFolder + "/");
			return;
		}
		TerrainHeightMap heightMap = TerrainMeta.HeightMap;
		Prefab[] array = Prefab.Load("assets/bundled/prefabs/autospawn/" + ResourceFolder);
		if (array == null || array.Length == 0)
		{
			return;
		}
		Vector3 position = TerrainMeta.Position;
		Vector3 size = TerrainMeta.Size;
		int num = Mathf.RoundToInt(ObjectDensity * size.x * size.z * 1E-06f);
		float x = position.x;
		float z = position.z;
		float max = position.x + size.x;
		float max2 = position.z + size.z;
		float num2 = SeedRandom.Range(ref seed, -1000000f, 1000000f);
		float num3 = SeedRandom.Range(ref seed, -1000000f, 1000000f);
		int octaves = Cluster.Octaves;
		float offset = Cluster.Offset;
		float frequency = Cluster.Frequency * 0.01f;
		float amplitude = Cluster.Amplitude;
		for (int i = 0; i < num; i++)
		{
			float num4 = SeedRandom.Range(ref seed, x, max);
			float num5 = SeedRandom.Range(ref seed, z, max2);
			float normX = TerrainMeta.NormalizeX(num4);
			float normZ = TerrainMeta.NormalizeZ(num5);
			float num6 = SeedRandom.Value(ref seed);
			float factor = Filter.GetFactor(normX, normZ);
			Prefab random = array.GetRandom(ref seed);
			if (((FilterMode & SpawnFilterMode.PivotPoint) == 0 || !(factor <= 0f)) && !((offset + Noise.Turbulence(num2 + num4, num3 + num5, octaves, frequency, amplitude)) * factor * factor < num6))
			{
				float height = heightMap.GetHeight(normX, normZ);
				Vector3 pos = new Vector3(num4, height, num5);
				Quaternion rot = random.Object.transform.localRotation;
				Vector3 scale = random.Object.transform.localScale;
				random.ApplyDecorComponents(ref pos, ref rot, ref scale);
				if (random.ApplyTerrainFilters(pos, rot, scale) && random.ApplyTerrainAnchors(ref pos, rot, scale, AnchorMode, ((FilterMode & SpawnFilterMode.TerrainAnchorPoints) != 0) ? Filter : null) && random.ApplyTerrainChecks(pos, rot, scale, ((FilterMode & SpawnFilterMode.TerrainCheckPoints) != 0) ? Filter : null) && random.ApplyWaterChecks(pos, rot, scale))
				{
					World.AddPrefab("Decor", random, pos, rot, scale);
				}
			}
		}
	}
}
