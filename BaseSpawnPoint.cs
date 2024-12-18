using System.Collections.Generic;
using UnityEngine;

public abstract class BaseSpawnPoint : MonoBehaviour, IServerComponent
{
	public enum SpawnPointType
	{
		Normal,
		Tugboat,
		Motorbike,
		Bicycle
	}

	public SpawnPointType spawnPointType;

	[SerializeField]
	[Range(1f, 25f)]
	[Tooltip("Min distance between the spawn area and any human players")]
	protected float playerCheckMargin = 2f;

	public static Dictionary<SpawnPointType, List<BaseSpawnPoint>> spawnPoints = new Dictionary<SpawnPointType, List<BaseSpawnPoint>>();

	public abstract void GetLocation(out Vector3 pos, out Quaternion rot);

	public abstract void ObjectSpawned(SpawnPointInstance instance);

	public abstract void ObjectRetired(SpawnPointInstance instance);

	protected void OnEnable()
	{
		if (spawnPointType != 0)
		{
			if (spawnPoints.TryGetValue(spawnPointType, out var value))
			{
				value.Add(this);
				return;
			}
			spawnPoints[spawnPointType] = new List<BaseSpawnPoint> { this };
		}
	}

	protected void OnDisable()
	{
		if (spawnPointType != 0 && spawnPoints.TryGetValue(spawnPointType, out var value))
		{
			value.Remove(this);
		}
	}

	public virtual bool IsAvailableTo(GameObject prefab)
	{
		return base.gameObject.activeSelf;
	}

	public virtual bool HasPlayersIntersecting()
	{
		return BaseNetworkable.HasCloseConnections(base.transform.position, playerCheckMargin);
	}

	protected void DropToGround(ref Vector3 pos, ref Quaternion rot)
	{
		if ((bool)TerrainMeta.HeightMap && (bool)TerrainMeta.Collision && !TerrainMeta.Collision.GetIgnore(pos))
		{
			float height = TerrainMeta.HeightMap.GetHeight(pos);
			pos.y = Mathf.Max(pos.y, height);
		}
		if (TransformUtil.GetGroundInfo(pos, out var hitOut, 20f, 1235288065))
		{
			pos = hitOut.point;
			rot = Quaternion.LookRotation(rot * Vector3.forward, hitOut.normal);
		}
	}
}
