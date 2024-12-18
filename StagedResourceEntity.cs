using System;
using System.Collections.Generic;
using Facepunch;
using Network;
using ProtoBuf;
using UnityEngine;

public class StagedResourceEntity : ResourceEntity
{
	[Serializable]
	public class ResourceStage
	{
		public float health;

		public GameObject instance;
	}

	public List<ResourceStage> stages = new List<ResourceStage>();

	protected int stage;

	public GameObjectRef changeStageEffect;

	public MeshLOD ResourceMeshLod;

	public MeshCollider ResourceMeshCollider;

	public GameObject gibSourceTest;

	private StagedResourceEntityInfo cachedInfo;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("StagedResourceEntity.OnRpcMessage"))
		{
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.resource != null)
		{
			int num = info.msg.resource.stage;
			if (info.fromDisk && base.isServer)
			{
				health = startHealth;
				num = 0;
			}
			if (num != stage)
			{
				stage = num;
				UpdateStage();
			}
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		if (info.msg.resource == null)
		{
			info.msg.resource = Pool.Get<BaseResource>();
		}
		info.msg.resource.health = Health();
		info.msg.resource.stage = stage;
	}

	protected override void OnHealthChanged()
	{
		Invoke(UpdateNetworkStage, 0.1f);
	}

	protected virtual void UpdateNetworkStage()
	{
		if (FindBestStage() != stage)
		{
			stage = FindBestStage();
			SendNetworkUpdate();
			UpdateStage();
		}
	}

	private int FindBestStage()
	{
		float num = Mathf.InverseLerp(0f, MaxHealth(), Health());
		StagedResourceEntityInfo.ResourceStage[] array = GetInfo().Stages;
		for (int i = 0; i < array.Length; i++)
		{
			if (num >= array[i].Health)
			{
				return i;
			}
		}
		return array.Length - 1;
	}

	private StagedResourceEntityInfo GetInfo()
	{
		if (cachedInfo != null)
		{
			return cachedInfo;
		}
		if (base.isServer)
		{
			cachedInfo = PrefabAttribute.server.Find<StagedResourceEntityInfo>(prefabID);
		}
		return cachedInfo;
	}

	private void UpdateStage()
	{
		if (GetInfo().Stages.Length != 0)
		{
			ResourceMeshCollider.sharedMesh = cachedInfo.GetCollisionMesh(stage);
			GroundWatch.PhysicsChanged(base.gameObject);
		}
	}
}
