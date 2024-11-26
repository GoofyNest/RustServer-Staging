using UnityEngine;

namespace Rust.Ai.Gen2;

public static class NavHelpers
{
	public static bool IsPositionAtTopologyRequirement(BaseEntity baseEntity, Vector3 position, TerrainTopology.Enum topologyRequirement)
	{
		using (TimeWarning.New("IsPositionAtTopologyRequirement"))
		{
			if (TerrainMeta.TopologyMap == null)
			{
				return false;
			}
			TerrainTopology.Enum topology = (TerrainTopology.Enum)TerrainMeta.TopologyMap.GetTopology(position);
			if ((topologyRequirement & topology) == 0)
			{
				return false;
			}
			return true;
		}
	}

	public static bool IsPositionABiomeRequirement(BaseEntity baseEntity, Vector3 position, TerrainBiome.Enum biomeRequirement)
	{
		using (TimeWarning.New("IsPositionABiomeRequirement"))
		{
			if (biomeRequirement == (TerrainBiome.Enum)0)
			{
				return true;
			}
			if (TerrainMeta.BiomeMap == null)
			{
				return false;
			}
			TerrainBiome.Enum biomeMaxType = (TerrainBiome.Enum)TerrainMeta.BiomeMap.GetBiomeMaxType(position);
			if ((biomeRequirement & biomeMaxType) == 0)
			{
				return false;
			}
			return true;
		}
	}

	public static bool IsAcceptableWaterDepth(BaseEntity baseEntity, Vector3 position, float maxDepth = 0.1f)
	{
		using (TimeWarning.New("IsAcceptableWaterDepth"))
		{
			if (WaterLevel.GetOverallWaterDepth(position, waves: false, volumes: false) > maxDepth)
			{
				return false;
			}
			return true;
		}
	}
}
