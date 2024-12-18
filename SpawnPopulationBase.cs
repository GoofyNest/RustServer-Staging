using System.Collections.Generic;
using System.Text;
using ConVar;
using UnityEngine;

public abstract class SpawnPopulationBase : BaseScriptableObject
{
	public string ResourceFolder = string.Empty;

	public GameObjectRef[] ResourceList;

	public bool EnforcePopulationLimits = true;

	public float SpawnRate = 1f;

	public bool ScaleWithServerPopulation;

	protected Prefab<Spawnable>[] Prefabs;

	protected int[] numToSpawn;

	protected bool haveInitialized;

	protected virtual bool Initialize()
	{
		if (Prefabs == null || Prefabs.Length == 0)
		{
			if (!string.IsNullOrEmpty(ResourceFolder))
			{
				Prefabs = Prefab.Load<Spawnable>("assets/bundled/prefabs/autospawn/" + ResourceFolder, GameManager.server, PrefabAttribute.server, useProbabilities: false);
			}
			if (ResourceList != null && ResourceList.Length != 0)
			{
				List<string> list = new List<string>();
				GameObjectRef[] resourceList = ResourceList;
				foreach (GameObjectRef gameObjectRef in resourceList)
				{
					string resourcePath = gameObjectRef.resourcePath;
					if (string.IsNullOrEmpty(resourcePath))
					{
						Debug.LogWarning(base.name + " resource list contains invalid resource path for GUID " + gameObjectRef.guid, this);
					}
					else
					{
						list.Add(resourcePath);
					}
				}
				Prefabs = Prefab.Load<Spawnable>(list.ToArray(), GameManager.server, PrefabAttribute.server);
			}
			if (Prefabs == null || Prefabs.Length == 0)
			{
				return false;
			}
			numToSpawn = new int[Prefabs.Length];
		}
		return true;
	}

	public float GetCurrentSpawnRate()
	{
		if (ScaleWithServerPopulation)
		{
			return SpawnRate * SpawnHandler.PlayerLerp(Spawn.min_rate, Spawn.max_rate);
		}
		return SpawnRate * Spawn.max_rate;
	}

	public void Fill(SpawnHandler spawnHandler, SpawnDistribution distribution, int numToFill, bool initialSpawn)
	{
		if (GetTargetCount(distribution) == 0)
		{
			return;
		}
		if (!Initialize())
		{
			Debug.LogError("[Spawn] No prefabs to spawn: " + base.name, this);
			return;
		}
		if (Global.developer > 1)
		{
			Debug.Log("[Spawn] Population " + base.name + " needs to spawn " + numToFill);
		}
		SubFill(spawnHandler, distribution, numToFill, initialSpawn);
	}

	public abstract void SubFill(SpawnHandler spawnHandler, SpawnDistribution distribution, int numToFill, bool initialSpawn);

	public abstract byte[] GetBaseMapValues(int populationRes);

	public abstract int GetTargetCount(SpawnDistribution distribution);

	public abstract SpawnFilter GetSpawnFilter();

	public void GetReportString(StringBuilder sb, bool detailed)
	{
		if (!string.IsNullOrEmpty(ResourceFolder))
		{
			sb.AppendLine(base.name + " (autospawn/" + ResourceFolder + ")");
		}
		else
		{
			sb.AppendLine(base.name);
		}
		if (!detailed)
		{
			return;
		}
		sb.AppendLine("\tPrefabs:");
		if (Prefabs != null)
		{
			Prefab<Spawnable>[] prefabs = Prefabs;
			foreach (Prefab<Spawnable> prefab in prefabs)
			{
				sb.AppendLine("\t\t" + prefab.Name + " - " + prefab.Object);
			}
		}
		else
		{
			sb.AppendLine("\t\tN/A");
		}
	}
}
