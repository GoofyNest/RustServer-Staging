using Facepunch;
using ProtoBuf;
using Rust;
using UnityEngine;

public class PlayerCorpse : LootableCorpse
{
	public Buoyancy buoyancy;

	public const Flags Flag_Buoyant = Flags.Reserved6;

	public uint underwearSkin;

	public PlayerBonePosData bonePosData;

	public const Flags BlockClothingRebuild = Flags.Reserved2;

	private Vector3 prevLocalPos;

	private const float SLEEP_CHECK_FREQUENCY = 10f;

	public Ragdoll CorpseRagdollScript { get; private set; }

	public override bool CorpseIsRagdoll => CorpseRagdollScript != null;

	protected override float PositionTickRate => 0.05f;

	protected override bool PositionTickFixedTime => true;

	public bool IsBuoyant()
	{
		return HasFlag(Flags.Reserved6);
	}

	public override bool OnStartBeingLooted(BasePlayer baseEntity)
	{
		if ((baseEntity.InSafeZone() || InSafeZone()) && (ulong)baseEntity.userID != playerSteamID)
		{
			return false;
		}
		return base.OnStartBeingLooted(baseEntity);
	}

	public override void ServerInit()
	{
		base.ServerInit();
		if (buoyancy == null)
		{
			Debug.LogWarning("Player corpse has no buoyancy assigned, searching at runtime :" + base.name);
			buoyancy = GetComponent<Buoyancy>();
		}
		if (buoyancy != null)
		{
			buoyancy.SubmergedChanged = BuoyancyChanged;
			buoyancy.forEntity = this;
		}
		if (Rust.Application.isLoadingSave)
		{
			CorpseRagdollScript = GetComponent<Ragdoll>();
		}
		if (CorpseIsRagdoll)
		{
			CorpseRagdollScript.simOnServer = true;
			CorpseRagdollScript.ServerInit();
			InvokeRandomized(SleepCheck, 5f, 10f, Random.Range(-1f, 1f));
		}
	}

	public override void ServerInitCorpse(BaseEntity pr, Vector3 posOnDeah, Quaternion rotOnDeath, BasePlayer.PlayerFlags playerFlagsOnDeath, ModelState modelState)
	{
		parentEnt = pr;
		BasePlayer basePlayer = (BasePlayer)pr;
		CorpseRagdollScript = GetComponent<Ragdoll>();
		SpawnPointInstance component = GetComponent<SpawnPointInstance>();
		if (component != null)
		{
			spawnGroup = component.parentSpawnPointUser as SpawnGroup;
		}
		Skeleton component2 = GetComponent<Skeleton>();
		if (component2 != null)
		{
			PlayerBonePosData.BonePosData bonePositionData = GetBonePositionData(playerFlagsOnDeath, modelState);
			if (bonePositionData != null)
			{
				component2.CopyFrom(bonePositionData.bonePositions, bonePositionData.boneRotations, localSpace: true);
				component2.Bones[0].transform.localEulerAngles += bonePositionData.rootRotationOffset;
			}
		}
		if (CorpseIsRagdoll)
		{
			Quaternion rotation = (((playerFlagsOnDeath & BasePlayer.PlayerFlags.Sleeping) != 0) ? Quaternion.identity : rotOnDeath);
			base.transform.SetPositionAndRotation(posOnDeah, rotation);
		}
		else
		{
			base.transform.SetPositionAndRotation(parentEnt.CenterPoint(), basePlayer.eyes.bodyRotation);
		}
	}

	private PlayerBonePosData.BonePosData GetBonePositionData(BasePlayer.PlayerFlags flagsOnDeath, ModelState modelState)
	{
		if (flagsOnDeath.HasFlag(BasePlayer.PlayerFlags.Sleeping))
		{
			return bonePosData.sleeping;
		}
		if (flagsOnDeath.HasFlag(BasePlayer.PlayerFlags.Incapacitated))
		{
			return bonePosData.incapacitated;
		}
		if (flagsOnDeath.HasFlag(BasePlayer.PlayerFlags.Wounded))
		{
			return bonePosData.crawling;
		}
		if (modelState.onLadder)
		{
			return bonePosData.onladder;
		}
		if (modelState.ducked)
		{
			return bonePosData.ducking;
		}
		if (modelState.waterLevel >= 0.75f)
		{
			return bonePosData.swimming;
		}
		if (modelState.mounted)
		{
			if (modelState.poseType < bonePosData.mountedPoses.Length)
			{
				return bonePosData.mountedPoses[modelState.poseType];
			}
			if (modelState.poseType == 128)
			{
				return bonePosData.standing;
			}
			Debug.LogWarning($"PlayerCorpse GetBonePositionData: No saved bone position data for mount pose {modelState.poseType}. Falling back to SitGeneric. Please update the 'Server Side Ragdoll Bone Pos Data' file with the new mount pose.");
			return bonePosData.mountedPoses[7];
		}
		return bonePosData.standing;
	}

	public void BuoyancyChanged(bool isSubmerged)
	{
		if (!IsBuoyant())
		{
			SetFlag(Flags.Reserved6, isSubmerged, recursive: false, networkupdate: false);
			SendNetworkUpdate_Flags();
		}
	}

	public void BecomeActive()
	{
		if (CorpseIsRagdoll)
		{
			CorpseRagdollScript.BecomeActive();
			prevLocalPos = base.transform.localPosition;
		}
	}

	public void BecomeInactive()
	{
		if (CorpseIsRagdoll)
		{
			CorpseRagdollScript.BecomeInactive();
		}
	}

	protected override void PushRagdoll(HitInfo info)
	{
		if (CorpseIsRagdoll)
		{
			BecomeActive();
			PushRigidbodies(CorpseRagdollScript.rigidbodies, info.HitPositionWorld, info.attackNormal);
		}
		else
		{
			base.PushRagdoll(info);
		}
	}

	private void SleepCheck()
	{
		if (!CorpseIsRagdoll)
		{
			return;
		}
		if (CorpseRagdollScript.IsKinematic)
		{
			if (!GamePhysics.Trace(new Ray(CenterPoint(), Vector3.down), 0f, out var _, 0.25f, -928830701, QueryTriggerInteraction.Ignore, this))
			{
				BecomeActive();
			}
		}
		else if (!rigidBody.IsSleeping() && !buoyancy.ShouldWake() && Vector3.SqrMagnitude(base.transform.localPosition - prevLocalPos) < 0.1f)
		{
			BecomeInactive();
		}
		prevLocalPos = base.transform.localPosition;
	}

	public override bool BuoyancySleep(bool inWater)
	{
		if (CorpseIsRagdoll)
		{
			if (!rigidBody.IsSleeping())
			{
				BecomeInactive();
			}
			return true;
		}
		return base.BuoyancySleep(inWater);
	}

	public override bool BuoyancyWake()
	{
		if (CorpseIsRagdoll)
		{
			BecomeActive();
			return true;
		}
		return base.BuoyancyWake();
	}

	private void OnPhysicsNeighbourChanged()
	{
		BecomeActive();
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		if (info.msg.lootableCorpse != null)
		{
			info.msg.lootableCorpse.underwearSkin = underwearSkin;
		}
		if (base.isServer && containers != null && containers.Length > 1 && !info.forDisk)
		{
			info.msg.storageBox = Pool.Get<StorageBox>();
			info.msg.storageBox.contents = containers[1].Save();
		}
	}

	public override string Categorize()
	{
		return "playercorpse";
	}
}
