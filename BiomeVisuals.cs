using System;
using UnityEngine;

public class BiomeVisuals : MonoBehaviour
{
	[Serializable]
	public class EnvironmentVolumeOverride
	{
		public EnvironmentType Environment;

		public TerrainBiome.Enum Biome;
	}

	public GameObject Arid;

	public GameObject Temperate;

	public GameObject Tundra;

	public GameObject Arctic;

	public bool OverrideBiome;

	public TerrainBiome.Enum ToOverride;

	[Horizontal(2, -1)]
	public EnvironmentVolumeOverride[] EnvironmentVolumeOverrides;

	protected void Start()
	{
		int num = ((TerrainMeta.BiomeMap != null) ? TerrainMeta.BiomeMap.GetBiomeMaxType(base.transform.position) : 2);
		if (OverrideBiome)
		{
			num = (int)ToOverride;
		}
		else if (EnvironmentVolumeOverrides.Length != 0)
		{
			EnvironmentType environmentType = EnvironmentManager.Get(base.transform.position);
			EnvironmentVolumeOverride[] environmentVolumeOverrides = EnvironmentVolumeOverrides;
			foreach (EnvironmentVolumeOverride environmentVolumeOverride in environmentVolumeOverrides)
			{
				if ((environmentType & environmentVolumeOverride.Environment) != 0)
				{
					num = (int)environmentVolumeOverride.Biome;
					break;
				}
			}
		}
		switch (num)
		{
		case 1:
			SetChoice(Arid);
			break;
		case 2:
			SetChoice(Temperate);
			break;
		case 4:
			SetChoice(Tundra);
			break;
		case 8:
			SetChoice(Arctic);
			break;
		}
	}

	private void SetChoice(GameObject selection)
	{
		bool shouldDestroy = !base.gameObject.SupportsPoolingInParent();
		ApplyChoice(selection, Arid, shouldDestroy);
		ApplyChoice(selection, Temperate, shouldDestroy);
		ApplyChoice(selection, Tundra, shouldDestroy);
		ApplyChoice(selection, Arctic, shouldDestroy);
		if (selection != null)
		{
			selection.SetActive(value: true);
		}
		GameManager.Destroy(this);
	}

	private void ApplyChoice(GameObject selection, GameObject target, bool shouldDestroy)
	{
		if (target != null && target != selection)
		{
			if (shouldDestroy)
			{
				GameManager.Destroy(target);
			}
			else
			{
				target.SetActive(value: false);
			}
		}
	}
}
