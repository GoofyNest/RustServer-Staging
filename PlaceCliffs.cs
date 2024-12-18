using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public class PlaceCliffs : ProceduralComponent
{
	private class CliffPlacement
	{
		public int count;

		public int score;

		public Prefab prefab;

		public Vector3 pos = Vector3.zero;

		public Quaternion rot = Quaternion.identity;

		public Vector3 scale = Vector3.one;

		public CliffPlacement next;
	}

	public SpawnFilter Filter;

	public string ResourceFolder = string.Empty;

	public int RetryMultiplier = 1;

	[FormerlySerializedAs("CutoffSlope")]
	public int CutoffSlopeInitial = 10;

	public int CutoffSlopeRepeat = 10;

	[FormerlySerializedAs("MinHeight")]
	public int MinTerrainHeight;

	[FormerlySerializedAs("MaxHeight")]
	public int MaxTerrainHeight = 500;

	public int MinCliffHeight;

	public int MaxCliffHeight = 500;

	[FormerlySerializedAs("MinScale")]
	public float MinCliffScale = 1f;

	[FormerlySerializedAs("MaxScale")]
	public float MaxCliffScale = 2f;

	public int TargetCount = 8;

	public int TargetLength;

	public TerrainAnchorMode AnchorModeInitial = TerrainAnchorMode.MaximizeHeight;

	public TerrainAnchorMode AnchorModeRepeat = TerrainAnchorMode.MinimizeMovement;

	[InspectorFlags]
	public SpawnFilterMode FilterModeInitial = SpawnFilterMode.PivotPoint;

	[InspectorFlags]
	public SpawnFilterMode FilterModeRepeat = SpawnFilterMode.PivotPoint;

	private static float min_scale_delta = 0.1f;

	private static int max_scale_attempts = 10;

	private static int min_rotation = rotation_delta;

	private static int max_rotation = 60;

	private static int rotation_delta = 10;

	private static float offset_c = 0f;

	private static float offset_l = -0.75f;

	private static float offset_r = 0.75f;

	private static Vector3[] offsets = new Vector3[5]
	{
		new Vector3(offset_c, offset_c, offset_c),
		new Vector3(offset_l, offset_c, offset_c),
		new Vector3(offset_r, offset_c, offset_c),
		new Vector3(offset_c, offset_c, offset_l),
		new Vector3(offset_c, offset_c, offset_r)
	};

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
		Prefab[] array2 = array.Where((Prefab prefab) => (bool)prefab.Attribute.Find<DecorSocketMale>(prefab.ID) && (bool)prefab.Attribute.Find<DecorSocketFemale>(prefab.ID)).ToArray();
		if (array2 == null || array2.Length == 0)
		{
			return;
		}
		Prefab[] array3 = array.Where((Prefab prefab) => prefab.Attribute.Find<DecorSocketMale>(prefab.ID)).ToArray();
		if (array3 == null || array3.Length == 0)
		{
			return;
		}
		Prefab[] array4 = array.Where((Prefab prefab) => prefab.Attribute.Find<DecorSocketFemale>(prefab.ID)).ToArray();
		if (array4 == null || array4.Length == 0)
		{
			return;
		}
		Vector3 position = TerrainMeta.Position;
		Vector3 size = TerrainMeta.Size;
		float x = position.x;
		float z = position.z;
		float max = position.x + size.x;
		float max2 = position.z + size.z;
		int num = Mathf.RoundToInt(size.x * size.z * 0.001f * (float)RetryMultiplier);
		for (int i = 0; i < num; i++)
		{
			float x2 = SeedRandom.Range(ref seed, x, max);
			float z2 = SeedRandom.Range(ref seed, z, max2);
			float normX = TerrainMeta.NormalizeX(x2);
			float normZ = TerrainMeta.NormalizeZ(z2);
			float num2 = SeedRandom.Value(ref seed);
			Prefab random = array2.GetRandom(ref seed);
			PlaceCliffParameters placeCliffParameters = random.Attribute.Find<PlaceCliffParameters>(random.ID);
			int num3 = (placeCliffParameters ? placeCliffParameters.CutoffSlopeInitial : CutoffSlopeInitial);
			int num4 = (placeCliffParameters ? placeCliffParameters.MinTerrainHeight : MinTerrainHeight);
			int num5 = (placeCliffParameters ? placeCliffParameters.MaxTerrainHeight : MaxTerrainHeight);
			int num6 = (placeCliffParameters ? placeCliffParameters.MinCliffHeight : MinCliffHeight);
			int num7 = (placeCliffParameters ? placeCliffParameters.MaxCliffHeight : MaxCliffHeight);
			float num8 = (placeCliffParameters ? placeCliffParameters.MinCliffScale : MinCliffScale);
			float num9 = (placeCliffParameters ? placeCliffParameters.MaxCliffScale : MaxCliffScale);
			if ((FilterModeInitial & SpawnFilterMode.PivotPoint) != 0)
			{
				float factor = Filter.GetFactor(normX, normZ);
				if (factor * factor < num2)
				{
					continue;
				}
			}
			float height = heightMap.GetHeight(normX, normZ);
			if (height < (float)num4 || height > (float)num5)
			{
				continue;
			}
			Vector3 normal = heightMap.GetNormal(normX, normZ);
			if (Vector3.Angle(Vector3.up, normal) < (float)num3)
			{
				continue;
			}
			Vector3 vector = new Vector3(x2, height, z2);
			Quaternion quaternion = QuaternionEx.LookRotationForcedUp(normal, Vector3.up);
			float num10 = Mathf.Max((num9 - num8) / (float)max_scale_attempts, min_scale_delta);
			for (float num11 = num9; num11 >= num8; num11 -= num10)
			{
				Vector3 pos = vector;
				Quaternion rot = quaternion * random.Object.transform.localRotation;
				Vector3 scale = num11 * random.Object.transform.localScale;
				random.ApplyDecorComponents(ref pos, ref rot, ref scale);
				if (random.ApplyTerrainFilters(pos, rot, scale) && random.ApplyTerrainAnchors(ref pos, rot, scale, AnchorModeInitial, ((FilterModeInitial & SpawnFilterMode.TerrainAnchorPoints) != 0) ? Filter : null) && !(pos.y < (float)num6) && !(pos.y > (float)num7) && random.ApplyTerrainChecks(pos, rot, scale, ((FilterModeInitial & SpawnFilterMode.TerrainCheckPoints) != 0) ? Filter : null) && random.ApplyWaterChecks(pos, rot, scale))
				{
					CliffPlacement cliffPlacement = PlaceMale(array3, ref seed, random, pos, rot, scale);
					CliffPlacement cliffPlacement2 = PlaceFemale(array4, ref seed, random, pos, rot, scale);
					World.AddPrefab("Decor", random, pos, rot, scale);
					while (cliffPlacement != null && cliffPlacement.prefab != null)
					{
						World.AddPrefab("Decor", cliffPlacement.prefab, cliffPlacement.pos, cliffPlacement.rot, cliffPlacement.scale);
						cliffPlacement = cliffPlacement.next;
						i++;
					}
					while (cliffPlacement2 != null && cliffPlacement2.prefab != null)
					{
						World.AddPrefab("Decor", cliffPlacement2.prefab, cliffPlacement2.pos, cliffPlacement2.rot, cliffPlacement2.scale);
						cliffPlacement2 = cliffPlacement2.next;
						i++;
					}
					break;
				}
			}
		}
	}

	private CliffPlacement PlaceMale(Prefab[] prefabs, ref uint seed, Prefab parentPrefab, Vector3 parentPos, Quaternion parentRot, Vector3 parentScale)
	{
		return Place<DecorSocketFemale, DecorSocketMale>(prefabs, ref seed, parentPrefab, parentPos, parentRot, parentScale);
	}

	private CliffPlacement PlaceFemale(Prefab[] prefabs, ref uint seed, Prefab parentPrefab, Vector3 parentPos, Quaternion parentRot, Vector3 parentScale)
	{
		return Place<DecorSocketMale, DecorSocketFemale>(prefabs, ref seed, parentPrefab, parentPos, parentRot, parentScale);
	}

	private CliffPlacement Place<ParentSocketType, ChildSocketType>(Prefab[] prefabs, ref uint seed, Prefab parentPrefab, Vector3 parentPos, Quaternion parentRot, Vector3 parentScale, int parentAngle = 0, int parentCount = 0, int parentScore = 0) where ParentSocketType : PrefabAttribute where ChildSocketType : PrefabAttribute
	{
		CliffPlacement cliffPlacement = null;
		if (parentAngle > 160 || parentAngle < -160)
		{
			return cliffPlacement;
		}
		int num = SeedRandom.Range(ref seed, 0, prefabs.Length);
		ParentSocketType val = parentPrefab.Attribute.Find<ParentSocketType>(parentPrefab.ID);
		Vector3 vector = parentPos + parentRot * Vector3.Scale(val.worldPosition, parentScale);
		TerrainHeightMap heightMap = TerrainMeta.HeightMap;
		for (int i = 0; i < prefabs.Length; i++)
		{
			Prefab prefab = prefabs[(num + i) % prefabs.Length];
			if (prefab == parentPrefab)
			{
				continue;
			}
			ParentSocketType val2 = prefab.Attribute.Find<ParentSocketType>(prefab.ID);
			ChildSocketType val3 = prefab.Attribute.Find<ChildSocketType>(prefab.ID);
			bool flag = val2 != null;
			if (cliffPlacement != null && cliffPlacement.count > TargetCount && cliffPlacement.score > TargetLength && flag)
			{
				continue;
			}
			PlaceCliffParameters placeCliffParameters = prefab.Attribute.Find<PlaceCliffParameters>(prefab.ID);
			int num2 = (placeCliffParameters ? placeCliffParameters.CutoffSlopeRepeat : CutoffSlopeRepeat);
			int num3 = (placeCliffParameters ? placeCliffParameters.MinTerrainHeight : MinTerrainHeight);
			int num4 = (placeCliffParameters ? placeCliffParameters.MaxTerrainHeight : MaxTerrainHeight);
			int num5 = (placeCliffParameters ? placeCliffParameters.MinCliffHeight : MinCliffHeight);
			int num6 = (placeCliffParameters ? placeCliffParameters.MaxCliffHeight : MaxCliffHeight);
			float num7 = (placeCliffParameters ? placeCliffParameters.MinCliffScale : MinCliffScale);
			float num8 = (placeCliffParameters ? placeCliffParameters.MaxCliffScale : MaxCliffScale);
			float num9 = Mathf.Max((num8 - num7) / (float)max_scale_attempts, min_scale_delta);
			float num10 = num8;
			while (num10 >= num7)
			{
				int j;
				Vector3 scale;
				Quaternion rot;
				Vector3 pos;
				for (j = min_rotation; j <= max_rotation; j += rotation_delta)
				{
					for (int k = -1; k <= 1; k += 2)
					{
						Vector3[] array = offsets;
						foreach (Vector3 vector2 in array)
						{
							scale = prefab.Object.transform.localScale * num10;
							rot = Quaternion.Euler(0f, k * j, 0f) * parentRot;
							pos = vector - rot * (Vector3.Scale(val3.worldPosition, scale) + vector2);
							float normX = TerrainMeta.NormalizeX(pos.x);
							float normZ = TerrainMeta.NormalizeZ(pos.z);
							if ((FilterModeRepeat & SpawnFilterMode.PivotPoint) != 0)
							{
								float factor = Filter.GetFactor(normX, normZ);
								if (factor * factor < 0.5f)
								{
									continue;
								}
							}
							float height = heightMap.GetHeight(normX, normZ);
							if (height < (float)num3 || height > (float)num4)
							{
								continue;
							}
							Vector3 normal = heightMap.GetNormal(normX, normZ);
							if (Vector3.Angle(Vector3.up, normal) < (float)num2)
							{
								continue;
							}
							prefab.ApplyDecorComponents(ref pos, ref rot, ref scale);
							if (!prefab.ApplyTerrainAnchors(ref pos, rot, scale, AnchorModeRepeat, ((FilterModeRepeat & SpawnFilterMode.TerrainAnchorPoints) != 0) ? Filter : null) || pos.y < (float)num5 || pos.y > (float)num6 || !prefab.ApplyTerrainChecks(pos, rot, scale, ((FilterModeRepeat & SpawnFilterMode.TerrainCheckPoints) != 0) ? Filter : null) || !prefab.ApplyTerrainFilters(pos, rot, scale) || !prefab.ApplyWaterChecks(pos, rot, scale))
							{
								continue;
							}
							goto IL_0375;
						}
					}
				}
				num10 -= num9;
				continue;
				IL_0375:
				int parentAngle2 = parentAngle + j;
				int num11 = parentCount + 1;
				int num12 = parentScore + Mathf.CeilToInt(Vector3Ex.Distance2D(parentPos, pos));
				CliffPlacement cliffPlacement2 = null;
				if (flag)
				{
					cliffPlacement2 = Place<ParentSocketType, ChildSocketType>(prefabs, ref seed, prefab, pos, rot, scale, parentAngle2, num11, num12);
					if (cliffPlacement2 != null)
					{
						num11 = cliffPlacement2.count;
						num12 = cliffPlacement2.score;
					}
				}
				else
				{
					num12 *= 2;
				}
				if (cliffPlacement == null)
				{
					cliffPlacement = new CliffPlacement();
				}
				if (cliffPlacement.score < num12)
				{
					cliffPlacement.next = cliffPlacement2;
					cliffPlacement.count = num11;
					cliffPlacement.score = num12;
					cliffPlacement.prefab = prefab;
					cliffPlacement.pos = pos;
					cliffPlacement.rot = rot;
					cliffPlacement.scale = scale;
				}
				break;
			}
		}
		return cliffPlacement;
	}
}
