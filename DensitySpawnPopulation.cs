using ConVar;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Rust/Density Spawn Population")]
public class DensitySpawnPopulation : SpawnPopulationBase
{
	[Header("Spawn Info")]
	[Tooltip("Usually per square km")]
	[SerializeField]
	[FormerlySerializedAs("TargetDensity")]
	private float _targetDensity = 1f;

	public int ClusterSizeMin = 1;

	public int ClusterSizeMax = 1;

	public int ClusterDithering;

	public int SpawnAttemptsInitial = 20;

	public int SpawnAttemptsRepeating = 10;

	public bool ScaleWithLargeMaps = true;

	public bool ScaleWithSpawnFilter = true;

	public bool AlignToNormal;

	public SpawnFilter Filter = new SpawnFilter();

	public float FilterCutoff;

	public float FilterRadius;

	public bool FilterOutTutorialIslands;

	public MonumentType[] FilterOutMonuments;

	private int sumToSpawn;

	public virtual float TargetDensity => _targetDensity;

	public override void SubFill(SpawnHandler spawnHandler, SpawnDistribution distribution, int numToFill, bool initialSpawn)
	{
		float num = Mathf.Max(ClusterSizeMax, distribution.GetGridCellArea() * GetMaximumSpawnDensity());
		UpdateWeights(distribution, GetTargetCount(distribution));
		int num2 = (initialSpawn ? (numToFill * SpawnAttemptsInitial) : (numToFill * SpawnAttemptsRepeating));
		while (numToFill >= ClusterSizeMax && num2 > 0)
		{
			ByteQuadtree.Element node = distribution.SampleNode();
			int f = Random.Range(ClusterSizeMin, ClusterSizeMax + 1);
			f = Mathx.Min(num2, numToFill, f);
			for (int i = 0; i < f; i++)
			{
				Vector3 spawnPos;
				Quaternion spawnRot;
				bool flag = distribution.Sample(out spawnPos, out spawnRot, node, AlignToNormal, ClusterDithering) && Filter.GetFactor(spawnPos) > 0f;
				if (flag && FilterOutTutorialIslands && TutorialIsland.WorldBoundsMinusTutorialIslands.size != Vector3.zero)
				{
					flag = TutorialIsland.WorldBoundsMinusTutorialIslands.Contains(spawnPos);
				}
				if (flag && FilterRadius > 0f)
				{
					flag = Filter.GetFactor(spawnPos + Vector3.forward * FilterRadius) > 0f && Filter.GetFactor(spawnPos - Vector3.forward * FilterRadius) > 0f && Filter.GetFactor(spawnPos + Vector3.right * FilterRadius) > 0f && Filter.GetFactor(spawnPos - Vector3.right * FilterRadius) > 0f;
				}
				if (flag && FilterOutMonuments != null && FilterOutMonuments.Length != 0)
				{
					flag = TerrainMeta.Path.FindMonumentWithBoundsOverlap(spawnPos, FilterOutMonuments) == null;
				}
				if (flag && TryTakeRandomPrefab(out var result))
				{
					if (GetSpawnPosOverride(result, ref spawnPos, ref spawnRot) && (float)distribution.GetCount(spawnPos) < num)
					{
						spawnHandler.Spawn(this, result, spawnPos, spawnRot);
						numToFill--;
					}
					else
					{
						ReturnPrefab(result);
					}
				}
				num2--;
			}
		}
	}

	public void UpdateWeights(SpawnDistribution distribution, int targetCount)
	{
		int num = 0;
		for (int i = 0; i < Prefabs.Length; i++)
		{
			Prefab<Spawnable> prefab = Prefabs[i];
			int prefabWeight = GetPrefabWeight(prefab);
			num += prefabWeight;
		}
		int num2 = Mathf.CeilToInt((float)targetCount / (float)num);
		sumToSpawn = 0;
		for (int j = 0; j < Prefabs.Length; j++)
		{
			Prefab<Spawnable> prefab2 = Prefabs[j];
			int prefabWeight2 = GetPrefabWeight(prefab2);
			int count = distribution.GetCount(prefab2.ID);
			int num3 = Mathf.Max(prefabWeight2 * num2 - count, 0);
			numToSpawn[j] = num3;
			sumToSpawn += num3;
		}
	}

	protected virtual int GetPrefabWeight(Prefab<Spawnable> prefab)
	{
		if (!prefab.Parameters)
		{
			return 1;
		}
		return prefab.Parameters.Count;
	}

	public bool TryTakeRandomPrefab(out Prefab<Spawnable> result)
	{
		int num = Random.Range(0, sumToSpawn);
		for (int i = 0; i < Prefabs.Length; i++)
		{
			if ((num -= numToSpawn[i]) < 0)
			{
				numToSpawn[i]--;
				sumToSpawn--;
				result = Prefabs[i];
				return true;
			}
		}
		result = null;
		return false;
	}

	public void ReturnPrefab(Prefab<Spawnable> prefab)
	{
		if (prefab == null)
		{
			return;
		}
		for (int i = 0; i < Prefabs.Length; i++)
		{
			if (Prefabs[i] == prefab)
			{
				numToSpawn[i]++;
				sumToSpawn++;
			}
		}
	}

	public float GetCurrentSpawnDensity()
	{
		if (ScaleWithServerPopulation)
		{
			return TargetDensity * SpawnHandler.PlayerLerp(Spawn.min_density, Spawn.max_density) * 1E-06f;
		}
		return TargetDensity * Spawn.max_density * 1E-06f;
	}

	public float GetMaximumSpawnDensity()
	{
		if (ScaleWithServerPopulation)
		{
			return 2f * TargetDensity * SpawnHandler.PlayerLerp(Spawn.min_density, Spawn.max_density) * 1E-06f;
		}
		return 2f * TargetDensity * Spawn.max_density * 1E-06f;
	}

	public virtual bool GetSpawnPosOverride(Prefab<Spawnable> prefab, ref Vector3 newPos, ref Quaternion newRot)
	{
		return true;
	}

	public override byte[] GetBaseMapValues(int populationRes)
	{
		byte[] baseValues = new byte[populationRes * populationRes];
		SpawnFilter filter = Filter;
		float cutoff = FilterCutoff;
		Parallel.For(0, populationRes, delegate(int z)
		{
			for (int i = 0; i < populationRes; i++)
			{
				float normX = ((float)i + 0.5f) / (float)populationRes;
				float normZ = ((float)z + 0.5f) / (float)populationRes;
				float factor = filter.GetFactor(normX, normZ);
				baseValues[z * populationRes + i] = (byte)((factor >= cutoff) ? (255f * factor) : 0f);
			}
		});
		return baseValues;
	}

	public override int GetTargetCount(SpawnDistribution distribution)
	{
		float num = TerrainMeta.Size.x * TerrainMeta.Size.z;
		float num2 = GetCurrentSpawnDensity();
		if (!ScaleWithLargeMaps)
		{
			num = Mathf.Min(num, 16000000f);
		}
		if (ScaleWithSpawnFilter)
		{
			num2 *= distribution.Density;
		}
		return Mathf.RoundToInt(num * num2);
	}

	public override SpawnFilter GetSpawnFilter()
	{
		return Filter;
	}
}
