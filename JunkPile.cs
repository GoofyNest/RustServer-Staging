using Network;
using UnityEngine;

public class JunkPile : BaseEntity
{
	public GameObjectRef sinkEffect;

	public SpawnGroup[] spawngroups;

	public NPCSpawner NPCSpawn;

	private const float lifetimeMinutes = 30f;

	protected bool isSinking;

	public virtual bool DespawnIfAnyLootTaken => true;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("JunkPile.OnRpcMessage"))
		{
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void ServerInit()
	{
		base.ServerInit();
		Invoke(TimeOut, 1800f);
		InvokeRepeating(CheckEmpty, 10f, 30f);
		Invoke(SpawnInitial, 1f);
		isSinking = false;
	}

	internal override void DoServerDestroy()
	{
		base.DoServerDestroy();
		StabilityEntity.updateSurroundingsQueue.Add(WorldSpaceBounds().ToBounds());
	}

	private void SpawnInitial()
	{
		SpawnGroup[] array = spawngroups;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].SpawnInitial();
		}
	}

	public bool SpawnGroupsEmpty()
	{
		SpawnGroup[] array = spawngroups;
		foreach (SpawnGroup spawnGroup in array)
		{
			if (spawnGroup.DoesGroupContainNPCs())
			{
				continue;
			}
			if (DespawnIfAnyLootTaken)
			{
				if (spawnGroup.ObjectsRemoved > 0)
				{
					return true;
				}
				foreach (SpawnPointInstance spawnInstance in spawnGroup.SpawnInstances)
				{
					if (spawnInstance.Entity is LootContainer { HasBeenLooted: not false })
					{
						return true;
					}
				}
			}
			else if (spawnGroup.currentPopulation > 0)
			{
				return false;
			}
		}
		if (NPCSpawn != null && NPCSpawn.currentPopulation > 0)
		{
			return false;
		}
		if (DespawnIfAnyLootTaken)
		{
			return false;
		}
		return true;
	}

	public void CheckEmpty()
	{
		if (SpawnGroupsEmpty() && !BaseNetworkable.HasCloseConnections(base.transform.position, TimeoutPlayerCheckRadius()))
		{
			CancelInvoke(CheckEmpty);
			SinkAndDestroy();
		}
	}

	public virtual float TimeoutPlayerCheckRadius()
	{
		return 15f;
	}

	public void TimeOut()
	{
		if (BaseNetworkable.HasCloseConnections(base.transform.position, TimeoutPlayerCheckRadius()))
		{
			Invoke(TimeOut, 30f);
			return;
		}
		SpawnGroupsEmpty();
		SinkAndDestroy();
	}

	public void SinkAndDestroy()
	{
		if (base.isServer)
		{
			CancelInvoke(SinkAndDestroy);
			SpawnGroup[] array = spawngroups;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].Clear();
			}
			SetFlag(Flags.Reserved8, b: true, recursive: true);
			if (NPCSpawn != null)
			{
				NPCSpawn.Clear();
			}
			ClientRPC(RpcTarget.NetworkGroup("CLIENT_StartSink"));
			base.transform.position -= new Vector3(0f, 5f, 0f);
			isSinking = true;
			Invoke(KillMe, 22f);
		}
	}

	public void KillMe()
	{
		Kill();
	}

	public static void NotifyLootContainerLooted(BaseEntity entity)
	{
	}
}
