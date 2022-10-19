using Facepunch;
using ProtoBuf;
using Rust;
using UnityEngine;

public class HalloweenDungeon : BasePortal
{
	public GameObjectRef dungeonPrefab;

	public EntityRef<ProceduralDynamicDungeon> dungeonInstance;

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.fromDisk && info.msg.ioEntity != null)
		{
			dungeonInstance.uid = info.msg.ioEntity.genericEntRef3;
		}
	}

	public override void Spawn()
	{
		base.Spawn();
		if (!Rust.Application.isLoadingSave)
		{
			SpawnSubEntities();
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		if (info.msg.ioEntity == null)
		{
			info.msg.ioEntity = Pool.Get<ProtoBuf.IOEntity>();
		}
		info.msg.ioEntity.genericEntRef3 = dungeonInstance.uid;
	}

	public static Vector3 GetDungeonSpawnPoint()
	{
		float num = 200f;
		float num2 = 200f;
		float num3 = Mathf.Floor(TerrainMeta.Size.x / 200f);
		float num4 = 1000f;
		Vector3 zero = Vector3.zero;
		zero.x = 0f - Mathf.Min(TerrainMeta.Size.x, 4000f) + num;
		zero.y = 1000f;
		zero.z = 0f - Mathf.Min(TerrainMeta.Size.z, 4000f) + num;
		_ = Vector3.zero;
		for (int i = 0; (float)i < num4; i++)
		{
			for (int j = 0; (float)j < num3; j++)
			{
				Vector3 vector = zero + new Vector3((float)j * num, (float)i * num2, 0f);
				bool flag = false;
				foreach (ProceduralDynamicDungeon dungeon in ProceduralDynamicDungeon.dungeons)
				{
					if (Vector3.Distance(dungeon.transform.position, vector) < 10f)
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					return vector;
				}
			}
		}
		return Vector3.zero;
	}

	internal override void DoServerDestroy()
	{
		base.DoServerDestroy();
		if (dungeonInstance.IsValid(serverside: true))
		{
			dungeonInstance.Get(serverside: true).Kill();
		}
	}

	public void DelayedDestroy()
	{
		Kill();
	}

	public void SpawnSubEntities()
	{
		Vector3 dungeonSpawnPoint = GetDungeonSpawnPoint();
		if (dungeonSpawnPoint == Vector3.zero)
		{
			Debug.LogError("No dungeon spawn point");
			Invoke(DelayedDestroy, 5f);
			return;
		}
		BaseEntity baseEntity = GameManager.server.CreateEntity(dungeonPrefab.resourcePath, dungeonSpawnPoint, Quaternion.identity);
		baseEntity.Spawn();
		ProceduralDynamicDungeon component = baseEntity.GetComponent<ProceduralDynamicDungeon>();
		dungeonInstance.Set(component);
		BasePortal basePortal = (targetPortal = component.GetExitPortal());
		basePortal.targetPortal = this;
		LinkPortal();
		basePortal.LinkPortal();
	}
}
