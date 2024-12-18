#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Facepunch.Rust;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

public class StabilityEntity : DecayEntity
{
	public class StabilityCheckWorkQueue : ObjectWorkQueue<StabilityEntity>
	{
		protected override void RunJob(StabilityEntity entity)
		{
			if (ShouldAdd(entity))
			{
				entity.StabilityCheck();
			}
		}

		protected override bool ShouldAdd(StabilityEntity entity)
		{
			if (!ConVar.Server.stability)
			{
				return false;
			}
			if (!entity.IsValid())
			{
				return false;
			}
			if (!entity.isServer)
			{
				return false;
			}
			return true;
		}
	}

	public class UpdateSurroundingsQueue : ObjectWorkQueue<Bounds>
	{
		protected override void RunJob(Bounds bounds)
		{
			NotifyNeighbours(bounds);
		}

		public static void NotifyNeighbours(Bounds bounds)
		{
			if (!ConVar.Server.stability)
			{
				return;
			}
			List<BaseEntity> obj = Facepunch.Pool.Get<List<BaseEntity>>();
			Vis.Entities(bounds.center, bounds.extents.magnitude + 1f, obj, -2144696062);
			foreach (BaseEntity item in obj)
			{
				if (!item.IsDestroyed && !item.isClient)
				{
					if (item is StabilityEntity stabilityEntity)
					{
						stabilityEntity.OnPhysicsNeighbourChanged();
					}
					else
					{
						item.BroadcastMessage("OnPhysicsNeighbourChanged", SendMessageOptions.DontRequireReceiver);
					}
				}
			}
			Facepunch.Pool.FreeUnmanaged(ref obj);
		}
	}

	private class Support
	{
		public StabilityEntity parent;

		public EntityLink link;

		public float factor = 1f;

		public Support(StabilityEntity parent, EntityLink link, float factor)
		{
			this.parent = parent;
			this.link = link;
			this.factor = factor;
		}

		public StabilityEntity SupportEntity(StabilityEntity ignoreEntity = null)
		{
			StabilityEntity stabilityEntity = null;
			for (int i = 0; i < link.connections.Count; i++)
			{
				StabilityEntity stabilityEntity2 = link.connections[i].owner as StabilityEntity;
				Socket_Base socket = link.connections[i].socket;
				if (!(stabilityEntity2 == null) && !(stabilityEntity2 == parent) && !(stabilityEntity2 == ignoreEntity) && !stabilityEntity2.isClient && !stabilityEntity2.IsDestroyed && !(socket is ConstructionSocket { femaleNoStability: not false }) && (stabilityEntity == null || stabilityEntity2.cachedDistanceFromGround < stabilityEntity.cachedDistanceFromGround))
				{
					stabilityEntity = stabilityEntity2;
				}
			}
			return stabilityEntity;
		}
	}

	public static readonly Translate.Phrase CancelTitle = new Translate.Phrase("cancel", "Cancel");

	public static readonly Translate.Phrase CancelDesc = new Translate.Phrase("cancel_desc");

	public bool grounded;

	[NonSerialized]
	public float cachedStability;

	[NonSerialized]
	public int cachedDistanceFromGround = int.MaxValue;

	private List<Support> supports;

	private int stabilityStrikes;

	private bool dirty;

	public static readonly Translate.Phrase DemolishTitle = new Translate.Phrase("demolish", "Demolish");

	public static readonly Translate.Phrase DemolishDesc = new Translate.Phrase("demolish_desc", "Slowly and automatically dismantle this block");

	[ServerVar]
	public static int demolish_seconds = 600;

	public const Flags DemolishFlag = Flags.Reserved2;

	public bool canBeDemolished;

	public static StabilityCheckWorkQueue stabilityCheckQueue = new StabilityCheckWorkQueue();

	public static UpdateSurroundingsQueue updateSurroundingsQueue = new UpdateSurroundingsQueue();

	public virtual bool CanBeDemolished => canBeDemolished;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("StabilityEntity.OnRpcMessage"))
		{
			if (rpc == 2858062413u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - DoDemolish ");
				}
				using (TimeWarning.New("DoDemolish"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(2858062413u, "DoDemolish", this, player, 3f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg2 = rPCMessage;
							DoDemolish(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in DoDemolish");
					}
				}
				return true;
			}
			if (rpc == 216608990 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - DoImmediateDemolish ");
				}
				using (TimeWarning.New("DoImmediateDemolish"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(216608990u, "DoImmediateDemolish", this, player, 3f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg3 = rPCMessage;
							DoImmediateDemolish(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in DoImmediateDemolish");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void ResetState()
	{
		base.ResetState();
		cachedStability = 0f;
		cachedDistanceFromGround = int.MaxValue;
		if (base.isServer)
		{
			supports = null;
			stabilityStrikes = 0;
			dirty = false;
		}
	}

	public void InitializeSupports()
	{
		supports = new List<Support>();
		if (grounded || HasParent())
		{
			return;
		}
		List<EntityLink> entityLinks = GetEntityLinks();
		for (int i = 0; i < entityLinks.Count; i++)
		{
			EntityLink entityLink = entityLinks[i];
			if (entityLink.IsMale())
			{
				if (entityLink.socket is StabilitySocket)
				{
					supports.Add(new Support(this, entityLink, (entityLink.socket as StabilitySocket).support));
				}
				if (entityLink.socket is ConstructionSocket)
				{
					supports.Add(new Support(this, entityLink, (entityLink.socket as ConstructionSocket).support));
				}
			}
		}
	}

	public int DistanceFromGround(StabilityEntity ignoreEntity = null)
	{
		if (grounded || HasParent())
		{
			return 1;
		}
		if (supports == null)
		{
			return 1;
		}
		if (ignoreEntity == null)
		{
			ignoreEntity = this;
		}
		int num = int.MaxValue;
		for (int i = 0; i < supports.Count; i++)
		{
			StabilityEntity stabilityEntity = supports[i].SupportEntity(ignoreEntity);
			if (!(stabilityEntity == null))
			{
				int num2 = stabilityEntity.CachedDistanceFromGround(ignoreEntity);
				if (num2 != int.MaxValue)
				{
					num = Mathf.Min(num, num2 + 1);
				}
			}
		}
		return num;
	}

	public float SupportValue(StabilityEntity ignoreEntity = null)
	{
		if (grounded || HasParent())
		{
			return 1f;
		}
		if (supports == null)
		{
			return 1f;
		}
		if (ignoreEntity == null)
		{
			ignoreEntity = this;
		}
		float num = 0f;
		for (int i = 0; i < supports.Count; i++)
		{
			Support support = supports[i];
			StabilityEntity stabilityEntity = support.SupportEntity(ignoreEntity);
			if (!(stabilityEntity == null))
			{
				float num2 = stabilityEntity.CachedSupportValue(ignoreEntity);
				if (num2 != 0f)
				{
					num += num2 * support.factor;
				}
			}
		}
		return Mathf.Clamp01(num);
	}

	public int CachedDistanceFromGround(StabilityEntity ignoreEntity = null)
	{
		if (grounded || HasParent())
		{
			return 1;
		}
		if (supports == null)
		{
			return 1;
		}
		if (ignoreEntity == null)
		{
			ignoreEntity = this;
		}
		int num = int.MaxValue;
		for (int i = 0; i < supports.Count; i++)
		{
			StabilityEntity stabilityEntity = supports[i].SupportEntity(ignoreEntity);
			if (!(stabilityEntity == null))
			{
				int num2 = stabilityEntity.cachedDistanceFromGround;
				if (num2 != int.MaxValue)
				{
					num = Mathf.Min(num, num2 + 1);
				}
			}
		}
		return num;
	}

	public float CachedSupportValue(StabilityEntity ignoreEntity = null)
	{
		if (grounded || HasParent())
		{
			return 1f;
		}
		if (supports == null)
		{
			return 1f;
		}
		if (ignoreEntity == null)
		{
			ignoreEntity = this;
		}
		float num = 0f;
		for (int i = 0; i < supports.Count; i++)
		{
			Support support = supports[i];
			StabilityEntity stabilityEntity = support.SupportEntity(ignoreEntity);
			if (!(stabilityEntity == null))
			{
				float num2 = stabilityEntity.cachedStability;
				if (num2 != 0f)
				{
					num += num2 * support.factor;
				}
			}
		}
		return Mathf.Clamp01(num);
	}

	public virtual void StabilityCheck()
	{
		if (base.IsDestroyed)
		{
			return;
		}
		if (supports == null)
		{
			InitializeSupports();
		}
		bool flag = false;
		int num = DistanceFromGround();
		if (num != cachedDistanceFromGround)
		{
			cachedDistanceFromGround = num;
			flag = true;
		}
		float num2 = SupportValue();
		if (Mathf.Abs(cachedStability - num2) > Stability.accuracy)
		{
			cachedStability = num2;
			flag = true;
		}
		if (flag)
		{
			dirty = true;
			UpdateConnectedEntities();
			UpdateStability();
		}
		else if (dirty)
		{
			dirty = false;
			SendNetworkUpdate();
		}
		if (num2 < Stability.collapse)
		{
			if (stabilityStrikes < Stability.strikes)
			{
				UpdateStability();
				stabilityStrikes++;
			}
			else
			{
				Kill(DestroyMode.Gib);
			}
		}
		else
		{
			stabilityStrikes = 0;
		}
	}

	public void UpdateStability()
	{
		stabilityCheckQueue.Add(this);
	}

	public void UpdateSurroundingEntities()
	{
		updateSurroundingsQueue.Add(WorldSpaceBounds().ToBounds());
	}

	public void UpdateConnectedEntities()
	{
		List<EntityLink> entityLinks = GetEntityLinks();
		for (int i = 0; i < entityLinks.Count; i++)
		{
			EntityLink entityLink = entityLinks[i];
			if (!entityLink.IsFemale())
			{
				continue;
			}
			for (int j = 0; j < entityLink.connections.Count; j++)
			{
				StabilityEntity stabilityEntity = entityLink.connections[j].owner as StabilityEntity;
				if (!(stabilityEntity == null) && !stabilityEntity.isClient && !stabilityEntity.IsDestroyed)
				{
					stabilityEntity.UpdateStability();
				}
			}
		}
	}

	protected void OnPhysicsNeighbourChanged()
	{
		if (!base.IsDestroyed)
		{
			StabilityCheck();
		}
	}

	protected void DebugNudge()
	{
		StabilityCheck();
	}

	public override void ServerInit()
	{
		base.ServerInit();
		if (HasFlag(Flags.Reserved2) || !Rust.Application.isLoadingSave)
		{
			StartBeingDemolishable();
		}
		if (!Rust.Application.isLoadingSave)
		{
			UpdateStability();
		}
	}

	internal override void DoServerDestroy()
	{
		base.DoServerDestroy();
		UpdateSurroundingEntities();
	}

	private bool CanDemolish(BasePlayer player)
	{
		if (CanBeDemolished && IsDemolishable())
		{
			return HasDemolishPrivilege(player);
		}
		return false;
	}

	private bool IsDemolishable()
	{
		if (!ConVar.Server.pve && !HasFlag(Flags.Reserved2))
		{
			return false;
		}
		return true;
	}

	private bool HasDemolishPrivilege(BasePlayer player)
	{
		return player.IsBuildingAuthed(base.transform.position, base.transform.rotation, bounds);
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	private void DoDemolish(RPCMessage msg)
	{
		if (msg.player.CanInteract() && CanDemolish(msg.player))
		{
			Analytics.Azure.OnBuildingBlockDemolished(msg.player, this);
			Kill(DestroyMode.Gib);
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	private void DoImmediateDemolish(RPCMessage msg)
	{
		if (msg.player.CanInteract() && msg.player.IsAdmin)
		{
			Analytics.Azure.OnBuildingBlockDemolished(msg.player, this);
			Kill(DestroyMode.Gib);
		}
	}

	private void StopBeingDemolishable()
	{
		SetFlag(Flags.Reserved2, b: false);
		SendNetworkUpdate();
	}

	private void StartBeingDemolishable()
	{
		SetFlag(Flags.Reserved2, b: true);
		Invoke(StopBeingDemolishable, demolish_seconds);
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.stabilityEntity = Facepunch.Pool.Get<ProtoBuf.StabilityEntity>();
		info.msg.stabilityEntity.stability = cachedStability;
		info.msg.stabilityEntity.distanceFromGround = cachedDistanceFromGround;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.stabilityEntity != null)
		{
			cachedStability = info.msg.stabilityEntity.stability;
			cachedDistanceFromGround = info.msg.stabilityEntity.distanceFromGround;
			if (cachedStability <= 0f)
			{
				cachedStability = 0f;
			}
			if (cachedDistanceFromGround <= 0)
			{
				cachedDistanceFromGround = int.MaxValue;
			}
		}
		if (info.fromDisk)
		{
			SetFlag(Flags.Reserved2, b: false);
		}
	}
}
