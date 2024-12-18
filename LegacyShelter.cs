using System.Collections.Generic;
using ConVar;
using Facepunch;
using ProtoBuf;
using Rust;
using UnityEngine;

public class LegacyShelter : DecayEntity
{
	public static readonly int FpShelterDefault = 1;

	[ReplicatedVar]
	public static int max_shelters = 1;

	private static Dictionary<ulong, List<LegacyShelter>> sheltersPerPlayer = new Dictionary<ulong, List<LegacyShelter>>();

	public static Translate.Phrase shelterLimitPhrase = new Translate.Phrase("shelter_limit_update", "You are now at {0}/{1} shelters");

	public static Translate.Phrase shelterLimitReachedPhrase = new Translate.Phrase("shelter_limit_reached", "You have reached your shelter limit!");

	[Header("Shelter References")]
	public GameObjectRef smallPrivilegePrefab;

	public GameObjectRef includedDoorPrefab;

	public GameObjectRef includedLockPrefab;

	public EntityRef<EntityPrivilege> entityPrivilege;

	private EntityRef<LegacyShelterDoor> childDoorInstance;

	private EntityRef<BaseLock> lockEntityInstance;

	private Decay decayReference;

	private float lastShelterDecayTick;

	private float lastInteractedWithDoor;

	private ulong shelterOwnerID;

	public static Dictionary<ulong, List<LegacyShelter>> SheltersPerPlayer => sheltersPerPlayer;

	public static Planner.CanBuildResult? CanBuildShelter(BasePlayer player, Construction construction)
	{
		if (GameManager.server.FindPrefab(construction.prefabID)?.GetComponent<BaseEntity>() is LegacyShelter)
		{
			int num = 1;
			Planner.CanBuildResult value2;
			if (sheltersPerPlayer.TryGetValue(player.userID, out var value))
			{
				num = value.Count + 1;
				if (value.Count >= max_shelters)
				{
					value2 = default(Planner.CanBuildResult);
					value2.Result = false;
					value2.Phrase = shelterLimitReachedPhrase;
					return value2;
				}
			}
			value2 = default(Planner.CanBuildResult);
			value2.Result = true;
			value2.Phrase = shelterLimitPhrase;
			value2.Arguments = new string[2]
			{
				num.ToString(),
				max_shelters.ToString()
			};
			return value2;
		}
		return null;
	}

	internal override void DoServerDestroy()
	{
		base.DoServerDestroy();
		if (sheltersPerPlayer.TryGetValue(shelterOwnerID, out var _))
		{
			sheltersPerPlayer[shelterOwnerID].Remove(this);
			BasePlayer basePlayer = BasePlayer.FindByID(shelterOwnerID);
			if (basePlayer != null)
			{
				basePlayer.SendRespawnOptions();
			}
		}
	}

	public static int GetShelterCount(ulong userId)
	{
		if (userId == 0L)
		{
			return 0;
		}
		if (!sheltersPerPlayer.TryGetValue(userId, out var value))
		{
			return 0;
		}
		return value.Count;
	}

	private void AddToShelterList(ulong id)
	{
		if (!sheltersPerPlayer.ContainsKey(id))
		{
			sheltersPerPlayer.Add(id, new List<LegacyShelter>());
		}
		if (!IsShelterInList(sheltersPerPlayer[id], out var _))
		{
			sheltersPerPlayer[id].Add(this);
		}
	}

	private bool IsShelterInList(List<LegacyShelter> shelters, out LegacyShelter thisShelter)
	{
		bool result = false;
		thisShelter = null;
		if (shelters.Count == 0)
		{
			return false;
		}
		if (thisShelter == null)
		{
			return false;
		}
		foreach (LegacyShelter shelter in shelters)
		{
			if (shelter.net.ID == net.ID)
			{
				result = true;
				thisShelter = shelter;
				break;
			}
		}
		return result;
	}

	public override EntityPrivilege GetEntityBuildingPrivilege()
	{
		return GetEntityPrivilege();
	}

	public EntityPrivilege GetEntityPrivilege()
	{
		EntityPrivilege entityPrivilege = this.entityPrivilege.Get(base.isServer);
		if (entityPrivilege.IsValid())
		{
			return entityPrivilege;
		}
		return null;
	}

	protected override void OnChildAdded(BaseEntity child)
	{
		base.OnChildAdded(child);
		if (base.isServer && child.prefabID == includedDoorPrefab.GetEntity().prefabID && !Rust.Application.isLoadingSave)
		{
			Setup(child);
		}
		if (child.prefabID == smallPrivilegePrefab.GetEntity().prefabID)
		{
			EntityPrivilege entity = (EntityPrivilege)child;
			entityPrivilege.Set(entity);
		}
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.legacyShelter == null || !base.isServer)
		{
			return;
		}
		shelterOwnerID = info.msg.legacyShelter.ownerId;
		childDoorInstance = new EntityRef<LegacyShelterDoor>(info.msg.legacyShelter.doorID);
		lastInteractedWithDoor = info.msg.legacyShelter.timeSinceInteracted;
		AddToShelterList(shelterOwnerID);
		if (max_shelters == FpShelterDefault)
		{
			BasePlayer basePlayer = BasePlayer.FindByID(shelterOwnerID);
			if (basePlayer != null)
			{
				basePlayer.SendRespawnOptions();
			}
		}
	}

	public override void DecayTick()
	{
		base.DecayTick();
		float num = UnityEngine.Time.time - lastShelterDecayTick;
		lastShelterDecayTick = UnityEngine.Time.time;
		float num2 = num * ConVar.Decay.scale;
		lastInteractedWithDoor += num2;
		UpdateDoorHp();
	}

	public void HasInteracted()
	{
		lastInteractedWithDoor = 0f;
	}

	public void SetupDecay()
	{
		decayReference = PrefabAttribute.server.Find<Decay>(prefabID);
	}

	public override float GetEntityDecayDuration()
	{
		if (lastInteractedWithDoor < 64800f)
		{
			return float.MaxValue;
		}
		if (decayReference == null)
		{
			SetupDecay();
		}
		if (decayReference != null)
		{
			return decayReference.GetDecayDuration(this);
		}
		return float.MaxValue;
	}

	public LegacyShelterDoor GetChildDoor()
	{
		LegacyShelterDoor legacyShelterDoor = childDoorInstance.Get(base.isServer);
		if (legacyShelterDoor.IsValid())
		{
			return legacyShelterDoor;
		}
		return null;
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.legacyShelter = Facepunch.Pool.Get<ProtoBuf.LegacyShelter>();
		info.msg.legacyShelter.doorID = childDoorInstance.uid;
		info.msg.legacyShelter.timeSinceInteracted = lastInteractedWithDoor;
		info.msg.legacyShelter.ownerId = shelterOwnerID;
	}

	public override void OnPlaced(BasePlayer player)
	{
		if (sheltersPerPlayer.TryGetValue(player.userID, out var value) && value.Count >= max_shelters)
		{
			value[0].Kill(DestroyMode.Gib);
		}
		shelterOwnerID = player.userID;
		AddToShelterList(shelterOwnerID);
		player.SendRespawnOptions();
	}

	public override void Hurt(HitInfo info)
	{
		base.Hurt(info);
		LegacyShelterDoor childDoor = GetChildDoor();
		if (childDoor != null)
		{
			childDoor.ProtectedHurt(info);
		}
	}

	public override void OnKilled(HitInfo info)
	{
		base.OnKilled(info);
		LegacyShelterDoor childDoor = GetChildDoor();
		if (childDoor != null && !childDoor.IsDead())
		{
			childDoor.Die();
		}
	}

	public override void OnRepair()
	{
		base.OnRepair();
		UpdateDoorHp();
	}

	public override void OnRepairFinished()
	{
		base.OnRepairFinished();
		UpdateDoorHp();
	}

	public void ProtectedHurt(HitInfo info)
	{
		info.HitEntity = this;
		base.Hurt(info);
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		LegacyShelterDoor childDoor = GetChildDoor();
		if ((bool)childDoor)
		{
			childDoor.SetupDoor(this);
			childDoor.SetMaxHealth(MaxHealth());
			UpdateDoorHp();
		}
		SetupDecay();
	}

	private void Setup(BaseEntity child)
	{
		LegacyShelterDoor legacyShelterDoor = (LegacyShelterDoor)child;
		childDoorInstance.Set(legacyShelterDoor);
		BasePlayer basePlayer = BasePlayer.FindByID(shelterOwnerID);
		GetComponentInChildren<EntityPrivilege>().AddPlayer(basePlayer);
		legacyShelterDoor.SetupDoor(this);
		legacyShelterDoor.SetMaxHealth(MaxHealth());
		UpdateDoorHp();
		BaseEntity baseEntity = GameManager.server.CreateEntity(includedLockPrefab.resourcePath);
		baseEntity.SetParent(legacyShelterDoor, legacyShelterDoor.GetSlotAnchorName(Slot.Lock));
		baseEntity.OwnerID = shelterOwnerID;
		baseEntity.OnDeployed(legacyShelterDoor, basePlayer, null);
		baseEntity.Spawn();
		BaseLock baseLock = (BaseLock)baseEntity;
		if (baseLock != null)
		{
			baseLock.CanRemove = false;
		}
		legacyShelterDoor.SetSlot(Slot.Lock, baseEntity);
	}

	private void UpdateDoorHp()
	{
		LegacyShelterDoor childDoor = GetChildDoor();
		if (childDoor != null)
		{
			childDoor.SetHealth(base.health);
		}
	}
}
