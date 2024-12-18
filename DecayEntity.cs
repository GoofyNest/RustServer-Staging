using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using ProtoBuf;
using Rust;
using UnityEngine;

public class DecayEntity : BaseCombatEntity
{
	[Serializable]
	public struct DebrisPosition
	{
		public Vector3 Position;

		public Vector3 Rotation;

		public bool dropToTerrain;
	}

	public GameObjectRef debrisPrefab;

	public Vector3 debrisRotationOffset = Vector3.zero;

	public DebrisPosition[] DebrisPositions;

	[NonSerialized]
	public uint buildingID;

	public float timePlaced;

	private float decayTimer;

	private float upkeepTimer;

	private Upkeep upkeep;

	private Decay decay;

	private DecayPoint[] decayPoints;

	private float lastDecayTick;

	private float decayVariance = 1f;

	public Upkeep Upkeep => upkeep;

	public virtual bool BypassInsideDecayMultiplier => false;

	public virtual bool AllowOnCargoShip => false;

	public override void ResetState()
	{
		base.ResetState();
		buildingID = 0u;
		if (base.isServer)
		{
			decayTimer = 0f;
		}
	}

	public void AttachToBuilding(uint id)
	{
		if (base.isServer)
		{
			BuildingManager.server.Remove(this);
			buildingID = id;
			BuildingManager.server.Add(this);
			SendNetworkUpdate();
		}
	}

	public BuildingManager.Building GetBuilding()
	{
		if (base.isServer)
		{
			return BuildingManager.server.GetBuilding(buildingID);
		}
		return null;
	}

	public override BuildingPrivlidge GetBuildingPrivilege()
	{
		BuildingManager.Building building = GetBuilding();
		if (building != null)
		{
			BuildingPrivlidge dominatingBuildingPrivilege = building.GetDominatingBuildingPrivilege();
			if (dominatingBuildingPrivilege != null || CanReturnEmptyBuildingPrivilege())
			{
				return dominatingBuildingPrivilege;
			}
		}
		return base.GetBuildingPrivilege();
	}

	public virtual bool CanReturnEmptyBuildingPrivilege()
	{
		return false;
	}

	public void CalculateUpkeepCostAmounts(List<ItemAmount> itemAmounts, float multiplier)
	{
		if (upkeep == null)
		{
			return;
		}
		float num = upkeep.upkeepMultiplier * multiplier;
		if (num == 0f)
		{
			return;
		}
		List<ItemAmount> list = BuildCost();
		if (list == null)
		{
			return;
		}
		foreach (ItemAmount item in list)
		{
			if (item.itemDef.category != ItemCategory.Resources)
			{
				continue;
			}
			float num2 = item.amount * num;
			bool flag = false;
			foreach (ItemAmount itemAmount in itemAmounts)
			{
				if (itemAmount.itemDef == item.itemDef)
				{
					itemAmount.amount += num2;
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				itemAmounts.Add(new ItemAmount(item.itemDef, num2));
			}
		}
	}

	public override void ServerInit()
	{
		base.ServerInit();
		decayVariance = UnityEngine.Random.Range(0.95f, 1f);
		decay = PrefabAttribute.server.Find<Decay>(prefabID);
		decayPoints = PrefabAttribute.server.FindAll<DecayPoint>(prefabID);
		upkeep = PrefabAttribute.server.Find<Upkeep>(prefabID);
		BuildingManager.server.Add(this);
		if (!Rust.Application.isLoadingSave)
		{
			BuildingManager.server.CheckMerge(this);
		}
		lastDecayTick = UnityEngine.Time.time;
	}

	internal override void DoServerDestroy()
	{
		base.DoServerDestroy();
		BuildingManager.server.Remove(this);
		BuildingManager.server.CheckSplit(this);
	}

	public override bool ShouldUseCastNoClipChecks()
	{
		return UnityEngine.Time.time - timePlaced <= 5f;
	}

	public virtual void AttachToBuilding(DecayEntity other)
	{
		if (other != null)
		{
			AttachToBuilding(other.buildingID);
			BuildingManager.server.CheckMerge(this);
			return;
		}
		BuildingBlock nearbyBuildingBlock = GetNearbyBuildingBlock();
		if ((bool)nearbyBuildingBlock)
		{
			AttachToBuilding(nearbyBuildingBlock.buildingID);
		}
	}

	public BuildingBlock GetNearbyBuildingBlock()
	{
		float num = float.MaxValue;
		BuildingBlock result = null;
		Vector3 position = PivotPoint();
		List<BuildingBlock> obj = Facepunch.Pool.Get<List<BuildingBlock>>();
		Vis.Entities(position, 1.5f, obj, 2097152);
		for (int i = 0; i < obj.Count; i++)
		{
			BuildingBlock buildingBlock = obj[i];
			if (buildingBlock.isServer == base.isServer)
			{
				float num2 = buildingBlock.SqrDistance(position);
				if (!buildingBlock.grounded)
				{
					num2 += 1f;
				}
				if (num2 < num)
				{
					num = num2;
					result = buildingBlock;
				}
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return result;
	}

	public void ResetUpkeepTime()
	{
		upkeepTimer = 0f;
	}

	public void DecayTouch()
	{
		decayTimer = 0f;
	}

	public void AddUpkeepTime(float time)
	{
		upkeepTimer -= time;
	}

	public float GetProtectedSeconds()
	{
		return Mathf.Max(0f, 0f - upkeepTimer);
	}

	public virtual float GetEntityDecayDuration()
	{
		return decay.GetDecayDuration(this);
	}

	public virtual float GetEntityHealScale()
	{
		return decay.GetHealScale(this);
	}

	public virtual float GetEntityDecayDelay()
	{
		return decay.GetDecayDelay(this);
	}

	public virtual void DecayTick()
	{
		if (!(decay == null))
		{
			float num = decay.GetDecayTickOverride();
			if (num == 0f)
			{
				num = ConVar.Decay.tick;
			}
			float num2 = UnityEngine.Time.time - lastDecayTick;
			if (!(num2 < num))
			{
				OnDecay(decay, num2);
			}
		}
	}

	public virtual void OnDecay(Decay decay, float decayDeltaTime)
	{
		lastDecayTick = UnityEngine.Time.time;
		if (HasParent() || !decay.ShouldDecay(this))
		{
			return;
		}
		float num = decayDeltaTime * ConVar.Decay.scale;
		if (ConVar.Decay.upkeep)
		{
			upkeepTimer += num;
			if (upkeepTimer > 0f)
			{
				BuildingPrivlidge buildingPrivilege = GetBuildingPrivilege();
				if (buildingPrivilege != null)
				{
					upkeepTimer -= buildingPrivilege.PurchaseUpkeepTime(this, Mathf.Max(upkeepTimer, 600f));
				}
			}
			if (upkeepTimer < 1f)
			{
				if (base.healthFraction < 1f && GetEntityHealScale() > 0f && base.SecondsSinceAttacked > 600f)
				{
					float num2 = decayDeltaTime / GetEntityDecayDuration() * GetEntityHealScale();
					Heal(MaxHealth() * num2);
				}
				return;
			}
			upkeepTimer = 1f;
		}
		decayTimer += num;
		if (decayTimer < GetEntityDecayDelay())
		{
			return;
		}
		using (TimeWarning.New("DecayTick"))
		{
			float num3 = 1f;
			if (ConVar.Decay.upkeep)
			{
				if (!BypassInsideDecayMultiplier && !IsOutside())
				{
					num3 *= ConVar.Decay.upkeep_inside_decay_scale;
				}
			}
			else
			{
				for (int i = 0; i < decayPoints.Length; i++)
				{
					DecayPoint decayPoint = decayPoints[i];
					if (decayPoint.IsOccupied(this))
					{
						num3 -= decayPoint.protection;
					}
				}
			}
			if (num3 > 0f)
			{
				float num4 = num / GetEntityDecayDuration() * MaxHealth();
				Hurt(num4 * num3 * decayVariance, DamageType.Decay);
			}
		}
	}

	public override void OnRepairFinished()
	{
		base.OnRepairFinished();
		DecayTouch();
	}

	public override void OnKilled(HitInfo info)
	{
		if (debrisPrefab.isValid)
		{
			if (DebrisPositions != null && DebrisPositions.Length != 0)
			{
				DebrisPosition[] debrisPositions = DebrisPositions;
				for (int i = 0; i < debrisPositions.Length; i++)
				{
					DebrisPosition debrisPosition = debrisPositions[i];
					SpawnDebris(debrisPosition.Position, Quaternion.Euler(debrisPosition.Rotation), debrisPosition.dropToTerrain);
				}
			}
			else
			{
				SpawnDebris(Vector3.zero, Quaternion.Euler(debrisRotationOffset), dropToTerrain: false);
			}
		}
		base.OnKilled(info);
	}

	private void SpawnDebris(Vector3 localPos, Quaternion rot, bool dropToTerrain)
	{
		Vector3 vector = base.transform.TransformPoint(localPos);
		if (dropToTerrain && UnityEngine.Physics.Raycast(vector, Vector3.down, out var hitInfo, 6f, 8388608))
		{
			float num = vector.y - hitInfo.point.y;
			vector.y = hitInfo.point.y;
			localPos.y -= num;
		}
		List<DebrisEntity> obj = Facepunch.Pool.Get<List<DebrisEntity>>();
		Vis.Entities(vector, 0.1f, obj, 256);
		if (obj.Count > 0)
		{
			Facepunch.Pool.FreeUnmanaged(ref obj);
			return;
		}
		BaseEntity baseEntity = GameManager.server.CreateEntity(debrisPrefab.resourcePath, base.transform.TransformPoint(localPos), base.transform.rotation * rot);
		if ((bool)baseEntity)
		{
			baseEntity.SetParent(parentEntity.Get(serverside: true), worldPositionStays: true);
			baseEntity.Spawn();
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	public override bool SupportsChildDeployables()
	{
		BaseEntity baseEntity = GetParentEntity();
		if (!(baseEntity != null))
		{
			return false;
		}
		return baseEntity.ForceDeployableSetParent();
	}

	public override bool ForceDeployableSetParent()
	{
		BaseEntity baseEntity = GetParentEntity();
		if (!(baseEntity != null))
		{
			return false;
		}
		return baseEntity.ForceDeployableSetParent();
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.decayEntity = Facepunch.Pool.Get<ProtoBuf.DecayEntity>();
		info.msg.decayEntity.buildingID = buildingID;
		if (info.forDisk)
		{
			info.msg.decayEntity.decayTimer = decayTimer;
			info.msg.decayEntity.upkeepTimer = upkeepTimer;
		}
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.decayEntity == null)
		{
			return;
		}
		decayTimer = info.msg.decayEntity.decayTimer;
		upkeepTimer = info.msg.decayEntity.upkeepTimer;
		if (buildingID != info.msg.decayEntity.buildingID)
		{
			AttachToBuilding(info.msg.decayEntity.buildingID);
			if (info.fromDisk)
			{
				BuildingManager.server.LoadBuildingID(buildingID);
			}
		}
	}
}
