#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Facepunch.Rust;
using Network;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

public class BaseMountable : BaseCombatEntity
{
	public enum ClippingCheckLocation
	{
		HeadOnly,
		WholeBody
	}

	public enum DismountConvarType
	{
		Misc,
		Boating,
		Flying,
		GroundVehicle,
		Horse
	}

	public enum MountStatType
	{
		None,
		Boating,
		Flying,
		Driving
	}

	public enum MountGestureType
	{
		None,
		UpperBody
	}

	public static Translate.Phrase dismountPhrase = new Translate.Phrase("dismount", "Dismount");

	[Header("View")]
	public Transform eyePositionOverride;

	public Transform eyeCenterOverride;

	public Vector2 pitchClamp = new Vector2(-80f, 50f);

	public Vector2 yawClamp = new Vector2(-80f, 80f);

	public bool canWieldItems = true;

	public bool relativeViewAngles = true;

	[Header("Mounting")]
	public bool AllowForceMountWhenRestrained;

	public Transform mountAnchor;

	public float mountLOSVertOffset = 0.5f;

	public PlayerModel.MountPoses mountPose;

	public float maxMountDistance = 1.5f;

	public Transform[] dismountPositions;

	public bool checkPlayerLosOnMount;

	public bool disableMeshCullingForPlayers;

	public bool allowHeadLook;

	public bool ignoreVehicleParent;

	public bool legacyDismount;

	public ItemModWearable wearWhileMounted;

	public bool modifiesPlayerCollider;

	public BasePlayer.CapsuleColliderInfo customPlayerCollider;

	public float clippingCheckRadius = 0.4f;

	public bool clippingAndVisChecks;

	public ClippingCheckLocation clippingChecksLocation;

	public SoundDefinition mountSoundDef;

	public SoundDefinition swapSoundDef;

	public SoundDefinition dismountSoundDef;

	public DismountConvarType dismountHoldType;

	public MountStatType mountTimeStatType;

	public MountGestureType allowedGestures;

	public bool canDrinkWhileMounted = true;

	public bool allowSleeperMounting;

	[Help("Set this to true if the mountable is enclosed so it doesn't move inside cars and such")]
	public bool animateClothInLocalSpace = true;

	[Header("Camera")]
	public BasePlayer.CameraMode MountedCameraMode;

	[Header("Rigidbody (Optional)")]
	public Rigidbody rigidBody;

	[FormerlySerializedAs("needsVehicleTick")]
	public bool isMobile;

	public float SideLeanAmount = 0.2f;

	public const float playerHeight = 1.8f;

	public const float playerRadius = 0.5f;

	private BasePlayer _mounted;

	public static ListHashSet<BaseMountable> AllMountables = new ListHashSet<BaseMountable>();

	public static ListHashSet<BaseMountable> Mounted = new ListHashSet<BaseMountable>();

	public const float MOUNTABLE_TICK_RATE = 0.05f;

	protected override float PositionTickRate => 0.05f;

	public virtual bool IsSummerDlcVehicle => false;

	protected virtual bool BypassClothingMountBlocks => false;

	public virtual bool BlocksDoors => true;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("BaseMountable.OnRpcMessage"))
		{
			if (rpc == 1735799362 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_WantsDismount ");
				}
				using (TimeWarning.New("RPC_WantsDismount"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg2 = rPCMessage;
							RPC_WantsDismount(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_WantsDismount");
					}
				}
				return true;
			}
			if (rpc == 4014300952u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_WantsMount ");
				}
				using (TimeWarning.New("RPC_WantsMount"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(4014300952u, "RPC_WantsMount", this, player, 3f))
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
							RPC_WantsMount(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in RPC_WantsMount");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public virtual bool CanHoldItems()
	{
		return canWieldItems;
	}

	public virtual BasePlayer.CameraMode GetMountedCameraMode()
	{
		return MountedCameraMode;
	}

	public virtual bool DirectlyMountable()
	{
		return true;
	}

	public virtual Transform GetEyeOverride()
	{
		if (eyePositionOverride != null)
		{
			return eyePositionOverride;
		}
		return base.transform;
	}

	public virtual bool ModifiesThirdPersonCamera()
	{
		return false;
	}

	public virtual Vector2 GetPitchClamp()
	{
		return pitchClamp;
	}

	public virtual Vector2 GetYawClamp()
	{
		return yawClamp;
	}

	public virtual bool AnyMounted()
	{
		return IsBusy();
	}

	public bool IsMounted()
	{
		return AnyMounted();
	}

	public virtual Vector3 EyePositionForPlayer(BasePlayer player, Quaternion lookRot)
	{
		if (player.GetMounted() != this)
		{
			return Vector3.zero;
		}
		return GetEyeOverride().position;
	}

	public virtual Vector3 EyeCenterForPlayer(BasePlayer player, Quaternion lookRot)
	{
		if (player.GetMounted() != this)
		{
			return Vector3.zero;
		}
		return eyeCenterOverride.transform.position;
	}

	public virtual float WaterFactorForPlayer(BasePlayer player)
	{
		return WaterLevel.Factor(player.WorldSpaceBounds().ToBounds(), waves: true, volumes: true, this);
	}

	public override float MaxVelocity()
	{
		BaseEntity baseEntity = GetParentEntity();
		if ((bool)baseEntity)
		{
			return baseEntity.MaxVelocity();
		}
		return base.MaxVelocity();
	}

	public virtual bool PlayerIsMounted(BasePlayer player)
	{
		if (player.IsValid())
		{
			return player.GetMounted() == this;
		}
		return false;
	}

	public virtual BaseVehicle VehicleParent()
	{
		if (ignoreVehicleParent)
		{
			return null;
		}
		return GetParentEntity() as BaseVehicle;
	}

	public virtual bool HasValidDismountPosition(BasePlayer player)
	{
		BaseVehicle baseVehicle = VehicleParent();
		if (baseVehicle != null)
		{
			return baseVehicle.HasValidDismountPosition(player);
		}
		Transform[] array = dismountPositions;
		foreach (Transform transform in array)
		{
			if (ValidDismountPosition(player, transform.transform.position))
			{
				return true;
			}
		}
		return false;
	}

	public virtual bool ValidDismountPosition(BasePlayer player, Vector3 disPos)
	{
		bool debugDismounts = Debugging.DebugDismounts;
		Vector3 dismountCheckStart = GetDismountCheckStart(player);
		if (debugDismounts)
		{
			Debug.Log($"ValidDismountPosition debug: Checking dismount point {disPos} from {dismountCheckStart}.");
		}
		Vector3 start = disPos + new Vector3(0f, 0.5f, 0f);
		Vector3 end = disPos + new Vector3(0f, 1.3f, 0f);
		if (!UnityEngine.Physics.CheckCapsule(start, end, 0.5f, 1537286401))
		{
			Vector3 position = disPos + base.transform.up * 0.5f;
			if (debugDismounts)
			{
				Debug.Log($"ValidDismountPosition debug: Dismount point {disPos} capsule check is OK.");
			}
			if (IsVisibleAndCanSee(position))
			{
				Vector3 vector = disPos + player.NoClipOffset();
				if (debugDismounts)
				{
					Debug.Log($"ValidDismountPosition debug: Dismount point {disPos} is visible.");
				}
				if (legacyDismount || !AntiHack.TestNoClipping(player, dismountCheckStart, vector, player.NoClipRadius(ConVar.AntiHack.noclip_margin_dismount), ConVar.AntiHack.noclip_backtracking, out var _, vehicleLayer: false, this))
				{
					if (debugDismounts)
					{
						Debug.Log($"<color=green>ValidDismountPosition debug: Dismount point {disPos} is valid</color>.");
						Debug.DrawLine(dismountCheckStart, vector, Color.green, 10f);
					}
					return true;
				}
			}
		}
		if (debugDismounts)
		{
			Debug.DrawLine(dismountCheckStart, disPos, Color.red, 10f);
			if (debugDismounts)
			{
				Debug.Log($"<color=red>ValidDismountPosition debug: Dismount point {disPos} is invalid</color>.");
			}
		}
		return false;
	}

	public BasePlayer GetMounted()
	{
		return _mounted;
	}

	public virtual void MounteeTookDamage(BasePlayer mountee, HitInfo info)
	{
	}

	public virtual void LightToggle(BasePlayer player)
	{
	}

	public virtual void OnWeaponFired(BaseProjectile weapon)
	{
	}

	public virtual bool CanSwapToThis(BasePlayer player)
	{
		return true;
	}

	public override bool CanPickup(BasePlayer player)
	{
		if (base.CanPickup(player))
		{
			return !AnyMounted();
		}
		return false;
	}

	public override void OnKilled(HitInfo info)
	{
		DismountAllPlayers();
		base.OnKilled(info);
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void RPC_WantsMount(RPCMessage msg)
	{
		WantsMount(msg.player);
	}

	public void WantsMount(BasePlayer player)
	{
		if (!player.IsValid() || !player.CanInteract())
		{
			return;
		}
		if (!DirectlyMountable())
		{
			BaseVehicle baseVehicle = VehicleParent();
			if (baseVehicle != null)
			{
				baseVehicle.WantsMount(player);
				return;
			}
		}
		AttemptMount(player);
	}

	public virtual void AttemptMount(BasePlayer player, bool doMountChecks = true)
	{
		if (_mounted != null || IsDead() || !player.CanMountMountablesNow() || IsTransferring() || IsSeatClipping(this) || ClothingBlocksMounting(player))
		{
			return;
		}
		if (doMountChecks)
		{
			if (checkPlayerLosOnMount && UnityEngine.Physics.Linecast(player.eyes.position, mountAnchor.position + base.transform.up * mountLOSVertOffset, out var hitInfo, 1218652417))
			{
				bool flag = false;
				BaseEntity entity = hitInfo.GetEntity();
				if (entity != null && (entity == this || entity == VehicleParent()))
				{
					flag = true;
				}
				if (!flag)
				{
					Debug.Log("No line of sight to mount pos");
					return;
				}
			}
			if (!HasValidDismountPosition(player))
			{
				Debug.Log("no valid dismount");
				return;
			}
		}
		MountPlayer(player);
	}

	public virtual bool AttemptDismount(BasePlayer player)
	{
		if (player != _mounted)
		{
			return false;
		}
		if (IsTransferring())
		{
			return false;
		}
		if (VehicleParent() != null && !VehicleParent().AllowPlayerInstigatedDismount(player))
		{
			return false;
		}
		DismountPlayer(player);
		return true;
	}

	[RPC_Server]
	public void RPC_WantsDismount(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (HasValidDismountPosition(player) && (!(player != null) || !player.IsRestrained))
		{
			AttemptDismount(player);
		}
	}

	public void MountPlayer(BasePlayer player)
	{
		if (!(_mounted != null) && !(mountAnchor == null))
		{
			player.EnsureDismounted();
			_mounted = player;
			Transform transform = mountAnchor;
			player.SetMounted(this);
			player.MovePosition(transform.position);
			player.transform.rotation = transform.rotation;
			player.ServerRotation = transform.rotation;
			player.OverrideViewAngles(transform.rotation.eulerAngles);
			_mounted.eyes.NetworkUpdate(transform.rotation);
			player.SendNetworkUpdateImmediate();
			Analytics.Azure.OnMountEntity(player, this, VehicleParent());
			OnPlayerMounted();
			if (this.IsValid() && player.IsValid())
			{
				player.ProcessMissionEvent(BaseMission.MissionEventType.MOUNT_ENTITY, net.ID, 1f);
			}
		}
	}

	public virtual void OnPlayerMounted()
	{
		if (_mounted != null)
		{
			Mounted.TryAdd(this);
		}
		UpdateMountFlags();
	}

	public virtual void OnPlayerDismounted(BasePlayer player)
	{
		Mounted.Remove(this);
		UpdateMountFlags();
	}

	public virtual void UpdateMountFlags()
	{
		SetFlag(Flags.Busy, _mounted != null);
		BaseVehicle baseVehicle = VehicleParent();
		if (baseVehicle != null)
		{
			baseVehicle.UpdateMountFlags();
		}
	}

	public virtual void DismountAllPlayers()
	{
		if ((bool)_mounted)
		{
			DismountPlayer(_mounted);
		}
	}

	public void DismountPlayer(BasePlayer player, bool lite = false)
	{
		if (_mounted == null || _mounted != player)
		{
			return;
		}
		BaseVehicle baseVehicle = VehicleParent();
		Vector3 res;
		if (lite)
		{
			if (baseVehicle != null)
			{
				baseVehicle.PrePlayerDismount(player, this);
			}
			_mounted.DismountObject();
			_mounted = null;
			if (baseVehicle != null)
			{
				baseVehicle.PlayerDismounted(player, this);
			}
			OnPlayerDismounted(player);
		}
		else if (!GetDismountPosition(player, out res) || Distance(res) > 10f)
		{
			if (baseVehicle != null)
			{
				baseVehicle.PrePlayerDismount(player, this);
			}
			res = player.transform.position;
			_mounted.DismountObject();
			_mounted.MovePosition(res);
			_mounted.transform.rotation = Quaternion.identity;
			_mounted.ClientRPC(RpcTarget.Player("ForcePositionTo", _mounted), res);
			BasePlayer mounted = _mounted;
			_mounted = null;
			Debug.LogWarning("Killing player due to invalid dismount point :" + player.displayName + " / " + player.userID.Get() + " on obj : " + base.gameObject.name);
			mounted.Hurt(1000f, DamageType.Suicide, mounted, useProtection: false);
			if (baseVehicle != null)
			{
				baseVehicle.PlayerDismounted(player, this);
			}
			OnPlayerDismounted(player);
		}
		else
		{
			if (baseVehicle != null)
			{
				baseVehicle.PrePlayerDismount(player, this);
			}
			if (AntiHack.TestNoClipping(_mounted, res, res, _mounted.NoClipRadius(ConVar.AntiHack.noclip_margin), ConVar.AntiHack.noclip_backtracking, out var _, vehicleLayer: true))
			{
				_mounted.PauseVehicleNoClipDetection(5f);
			}
			_mounted.DismountObject();
			_mounted.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
			_mounted.MovePosition(res);
			_mounted.SendNetworkUpdateImmediate();
			_mounted.SendModelState(force: true);
			_mounted = null;
			if (baseVehicle != null)
			{
				baseVehicle.PlayerDismounted(player, this);
			}
			player.ForceUpdateTriggers();
			if ((bool)player.GetParentEntity())
			{
				BaseEntity baseEntity = player.GetParentEntity();
				player.ClientRPC(RpcTarget.Player("ForcePositionToParentOffset", player), baseEntity.transform.InverseTransformPoint(res), baseEntity.net.ID);
			}
			else
			{
				player.ClientRPC(RpcTarget.Player("ForcePositionTo", player), res);
			}
			Analytics.Azure.OnDismountEntity(player, this, baseVehicle);
			OnPlayerDismounted(player);
		}
	}

	public virtual bool GetDismountPosition(BasePlayer player, out Vector3 res)
	{
		BaseVehicle baseVehicle = VehicleParent();
		if (baseVehicle != null && baseVehicle.IsVehicleMountPoint(this))
		{
			return baseVehicle.GetDismountPosition(player, out res);
		}
		int num = 0;
		Transform[] array = dismountPositions;
		foreach (Transform transform in array)
		{
			if (ValidDismountPosition(player, transform.transform.position))
			{
				res = transform.transform.position;
				return true;
			}
			num++;
		}
		Debug.LogWarning("Failed to find dismount position for player :" + player.displayName + " / " + player.userID.Get() + " on obj : " + base.gameObject.name);
		res = player.transform.position;
		return false;
	}

	public override void ServerInit()
	{
		base.ServerInit();
		if (isMobile)
		{
			AllMountables.Add(this);
		}
	}

	internal override void DoServerDestroy()
	{
		DismountAllPlayers();
		AllMountables.Remove(this);
		base.DoServerDestroy();
	}

	public static void FixedUpdateCycle()
	{
		for (int num = AllMountables.Count - 1; num >= 0; num--)
		{
			BaseMountable baseMountable = AllMountables[num];
			if (baseMountable == null)
			{
				AllMountables.RemoveAt(num);
			}
			else if (baseMountable.isSpawned)
			{
				baseMountable.VehicleFixedUpdate();
			}
		}
		for (int num2 = AllMountables.Count - 1; num2 >= 0; num2--)
		{
			BaseMountable baseMountable2 = AllMountables[num2];
			if (baseMountable2 == null)
			{
				AllMountables.RemoveAt(num2);
			}
			else if (baseMountable2.isSpawned)
			{
				baseMountable2.PostVehicleFixedUpdate();
			}
		}
	}

	public static void PlayerSyncCycle()
	{
		for (int num = Mounted.Count - 1; num >= 0; num--)
		{
			BaseMountable baseMountable = Mounted[num];
			if (baseMountable == null || baseMountable.GetMounted() == null)
			{
				Mounted.RemoveAt(num);
			}
			else if (baseMountable.isSpawned)
			{
				baseMountable.MountedPlayerSync();
			}
		}
	}

	public virtual void VehicleFixedUpdate()
	{
		using (TimeWarning.New("BaseMountable.VehicleFixedUpdate"))
		{
			if (!(rigidBody != null) || rigidBody.IsSleeping() || rigidBody.isKinematic)
			{
				return;
			}
			float num = ValidBounds.TestDist(this, base.transform.position) - 25f;
			if (num < 0f)
			{
				num = 0f;
			}
			if (!(num < 100f))
			{
				return;
			}
			Vector3 normalized = base.transform.position.normalized;
			float num2 = Vector3.Dot(rigidBody.velocity, normalized);
			if (num2 > 0f)
			{
				float num3 = 1f - num / 100f;
				rigidBody.velocity -= normalized * num2 * (num3 * num3);
				if (num < 25f)
				{
					float num4 = 1f - num / 25f;
					rigidBody.AddForce(-normalized * 20f * num4, ForceMode.Acceleration);
				}
			}
		}
	}

	public virtual void PostVehicleFixedUpdate()
	{
	}

	protected virtual void MountedPlayerSync()
	{
		_mounted.transform.rotation = mountAnchor.transform.rotation;
		_mounted.ServerRotation = mountAnchor.transform.rotation;
		_mounted.MovePosition(mountAnchor.transform.position);
	}

	public virtual void PlayerServerInput(InputState inputState, BasePlayer player)
	{
	}

	public virtual float GetComfort()
	{
		return 0f;
	}

	public virtual void ScaleDamageForPlayer(BasePlayer player, HitInfo info)
	{
	}

	public bool TryFireProjectile(StorageContainer ammoStorage, AmmoTypes ammoType, Vector3 firingPos, Vector3 firingDir, BasePlayer shooter, float launchOffset, float minSpeed, out ServerProjectile projectile)
	{
		projectile = null;
		if (ammoStorage == null)
		{
			return false;
		}
		bool result = false;
		List<Item> obj = Facepunch.Pool.Get<List<Item>>();
		ammoStorage.inventory.FindAmmo(obj, ammoType);
		for (int num = obj.Count - 1; num >= 0; num--)
		{
			if (obj[num].amount <= 0)
			{
				obj.RemoveAt(num);
			}
		}
		if (obj.Count > 0)
		{
			if (UnityEngine.Physics.Raycast(firingPos, firingDir, out var hitInfo, launchOffset, 1237003025))
			{
				launchOffset = hitInfo.distance - 0.1f;
			}
			Item item = obj[obj.Count - 1];
			ItemModProjectile component = item.info.GetComponent<ItemModProjectile>();
			BaseEntity baseEntity = GameManager.server.CreateEntity(component.projectileObject.resourcePath, firingPos + firingDir * launchOffset);
			projectile = baseEntity.GetComponent<ServerProjectile>();
			Vector3 vector = projectile.initialVelocity + firingDir * projectile.speed;
			if (minSpeed > 0f)
			{
				float num2 = Vector3.Dot(vector, firingDir) - minSpeed;
				if (num2 < 0f)
				{
					vector += firingDir * (0f - num2);
				}
			}
			projectile.InitializeVelocity(vector);
			if (shooter.IsValid())
			{
				baseEntity.creatorEntity = shooter;
				baseEntity.OwnerID = shooter.userID;
			}
			baseEntity.Spawn();
			Analytics.Azure.OnExplosiveLaunched(shooter, baseEntity, this);
			item.UseItem();
			result = true;
		}
		Facepunch.Pool.Free(ref obj, freeElements: false);
		return result;
	}

	public override void DisableTransferProtection()
	{
		base.DisableTransferProtection();
		BasePlayer mounted = GetMounted();
		if (mounted != null && mounted.IsTransferProtected())
		{
			mounted.DisableTransferProtection();
		}
	}

	protected virtual int GetClipCheckMask()
	{
		return 1210122497;
	}

	public virtual bool IsSeatClipping(BaseMountable mountable)
	{
		if (!clippingAndVisChecks)
		{
			return false;
		}
		if (mountable == null)
		{
			return false;
		}
		int clipCheckMask = GetClipCheckMask();
		Vector3 position = mountable.eyePositionOverride.transform.position;
		Vector3 position2 = mountable.transform.position;
		Vector3 normalized = (position - position2).normalized;
		float num = clippingCheckRadius;
		if (mountable.modifiesPlayerCollider)
		{
			num = Mathf.Min(num, mountable.customPlayerCollider.radius);
		}
		Vector3 startPos = position - normalized * (num - 0.2f);
		return IsSeatClipping(mountable, startPos, num, clipCheckMask, position2, normalized);
	}

	public virtual Vector3 GetMountRagdollVelocity(BasePlayer player)
	{
		return Vector3.zero;
	}

	protected virtual bool IsSeatClipping(BaseMountable mountable, Vector3 startPos, float radius, int mask, Vector3 seatPos, Vector3 direction)
	{
		if (clippingChecksLocation == ClippingCheckLocation.HeadOnly)
		{
			return GamePhysics.CheckSphere(startPos, radius, mask, QueryTriggerInteraction.Ignore);
		}
		Vector3 end = seatPos + direction * (radius + 0.05f);
		return GamePhysics.CheckCapsule(startPos, end, radius, mask, QueryTriggerInteraction.Ignore);
	}

	public virtual bool IsInstrument()
	{
		return false;
	}

	public Vector3 GetDismountCheckStart(BasePlayer player)
	{
		Vector3 result = GetMountedPosition() + player.NoClipOffset();
		Vector3 vector = ((mountAnchor == null) ? base.transform.forward : mountAnchor.transform.forward);
		Vector3 vector2 = ((mountAnchor == null) ? base.transform.up : mountAnchor.transform.up);
		if (mountPose == PlayerModel.MountPoses.Chair)
		{
			result += -vector * 0.32f;
			result += vector2 * 0.25f;
		}
		else if (mountPose == PlayerModel.MountPoses.SitGeneric)
		{
			result += -vector * 0.26f;
			result += vector2 * 0.25f;
		}
		else if (mountPose == PlayerModel.MountPoses.SitGeneric)
		{
			result += -vector * 0.26f;
		}
		return result;
	}

	public Vector3 GetMountedPosition()
	{
		if (mountAnchor == null)
		{
			return base.transform.position;
		}
		return mountAnchor.transform.position;
	}

	public virtual float GetSpeed()
	{
		if (!isMobile)
		{
			return 0f;
		}
		return Vector3.Dot(GetLocalVelocity(), base.transform.forward);
	}

	public bool CanPlayerSeeMountPoint(Ray ray, BasePlayer player, float maxDistance)
	{
		if (player == null)
		{
			return false;
		}
		if (mountAnchor == null)
		{
			return false;
		}
		if (UnityEngine.Physics.SphereCast(ray, 0.25f, out var hitInfo, maxDistance, 1218652417))
		{
			BaseEntity entity = hitInfo.GetEntity();
			if (entity != null)
			{
				if (entity == this || EqualNetID(entity))
				{
					return true;
				}
				if (entity is BasePlayer basePlayer)
				{
					BaseMountable mounted = basePlayer.GetMounted();
					if (mounted == this)
					{
						return true;
					}
					if (mounted != null && mounted.VehicleParent() == this)
					{
						return true;
					}
				}
				BaseEntity baseEntity = entity.GetParentEntity();
				if (hitInfo.IsOnLayer(Rust.Layer.Vehicle_Detailed) && (baseEntity == this || EqualNetID(baseEntity)))
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool NearMountPoint(BasePlayer player)
	{
		if (player == null)
		{
			return false;
		}
		if (mountAnchor == null)
		{
			return false;
		}
		if (Vector3.Distance(player.transform.position, mountAnchor.position) <= maxMountDistance)
		{
			return CanPlayerSeeMountPoint(player.eyes.HeadRay(), player, 2f);
		}
		return false;
	}

	protected bool ClothingBlocksMounting(BasePlayer player)
	{
		if (BypassClothingMountBlocks)
		{
			return false;
		}
		foreach (Item item in player.inventory.containerWear.itemList)
		{
			if (item.info.ItemModWearable != null && item.info.ItemModWearable.preventsMounting)
			{
				return true;
			}
		}
		return false;
	}

	public static Vector3 ConvertVector(Vector3 vec)
	{
		for (int i = 0; i < 3; i++)
		{
			if (vec[i] > 180f)
			{
				vec[i] -= 360f;
			}
			else if (vec[i] < -180f)
			{
				vec[i] += 360f;
			}
		}
		return vec;
	}
}
