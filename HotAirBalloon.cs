#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

public class HotAirBalloon : BaseCombatEntity, VehicleSpawner.IVehicleSpawnUser, SamSite.ISamSiteTarget, SeekerTarget.ISeekerTargetOwner
{
	[Serializable]
	public struct UpgradeOption
	{
		public ItemDefinition TokenItem;

		public Translate.Phrase Title;

		public Translate.Phrase Description;

		public Sprite Icon;

		public int order;
	}

	protected const Flags Flag_HasFuel = Flags.Reserved6;

	protected const Flags Flag_Grounded = Flags.Reserved7;

	protected const Flags Flag_CanModifyEquipment = Flags.Reserved8;

	protected const Flags Flag_HalfInflated = Flags.Reserved1;

	protected const Flags Flag_FullInflated = Flags.Reserved2;

	public const Flags Flag_OnlyOwnerEntry = Flags.Locked;

	public Transform centerOfMass;

	public Rigidbody myRigidbody;

	public Transform buoyancyPoint;

	public float liftAmount = 10f;

	public Transform windSock;

	public Transform[] windFlags;

	public GameObject staticBalloonDeflated;

	public GameObject staticBalloon;

	public GameObject animatedBalloon;

	public Animator balloonAnimator;

	public Transform groundSample;

	public float inflationLevel;

	[Header("Fuel")]
	public GameObjectRef fuelStoragePrefab;

	public float fuelPerSec = 0.25f;

	[Header("Storage")]
	public GameObjectRef storageUnitPrefab;

	public EntityRef<StorageContainer> storageUnitInstance;

	[Header("Damage")]
	public DamageRenderer damageRenderer;

	public Transform engineHeight;

	public GameObject[] killTriggers;

	[Header("Upgrades")]
	public List<UpgradeOption> UpgradeOptions;

	private EntityFuelSystem fuelSystem;

	[ServerVar(Help = "Population active on the server", ShowInAdminUI = true)]
	public static float population = 1f;

	[ServerVar(Help = "How long before a HAB loses all its health while outside")]
	public static float outsidedecayminutes = 180f;

	public float NextUpgradeTime;

	public float windForce = 30000f;

	public Vector3 currentWindVec = Vector3.zero;

	public Bounds collapsedBounds;

	public Bounds raisedBounds;

	public GameObject[] balloonColliders;

	[ServerVar]
	public static float serviceCeiling = 175f;

	[ServerVar]
	public static float minimumAltitudeTerrain = 25f;

	private Vector3 lastFailedDecayPosition = Vector3.zero;

	private float currentBuoyancy;

	private TimeSince sinceLastBlast;

	private float avgTerrainHeight;

	protected bool grounded;

	private float spawnTime = -1f;

	private float safeAreaRadius;

	private Vector3 safeAreaOrigin;

	public bool IsFullyInflated => inflationLevel >= 1f;

	public bool Grounded => HasFlag(Flags.Reserved7);

	public SamSite.SamTargetType SAMTargetType => SamSite.targetTypeVehicle;

	public bool IsClient => base.isClient;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("HotAirBalloon.OnRpcMessage"))
		{
			if (rpc == 578721460 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - EngineSwitch ");
				}
				using (TimeWarning.New("EngineSwitch"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(578721460u, "EngineSwitch", this, player, 3f))
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
							EngineSwitch(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in EngineSwitch");
					}
				}
				return true;
			}
			if (rpc == 1851540757 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_OpenFuel ");
				}
				using (TimeWarning.New("RPC_OpenFuel"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg3 = rPCMessage;
							RPC_OpenFuel(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in RPC_OpenFuel");
					}
				}
				return true;
			}
			if (rpc == 2441951484u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_ReqEquipItem ");
				}
				using (TimeWarning.New("RPC_ReqEquipItem"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(2441951484u, "RPC_ReqEquipItem", this, player, 3f))
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
							RPCMessage msg4 = rPCMessage;
							RPC_ReqEquipItem(msg4);
						}
					}
					catch (Exception exception3)
					{
						Debug.LogException(exception3);
						player.Kick("RPC Error in RPC_ReqEquipItem");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void InitShared()
	{
		fuelSystem = new EntityFuelSystem(base.isServer, fuelStoragePrefab, children);
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.hotAirBalloon != null)
		{
			inflationLevel = info.msg.hotAirBalloon.inflationAmount;
			sinceLastBlast = info.msg.hotAirBalloon.sinceLastBlast;
			if (info.fromDisk && (bool)myRigidbody)
			{
				myRigidbody.velocity = info.msg.hotAirBalloon.velocity;
			}
		}
		if (info.msg.motorBoat != null)
		{
			fuelSystem.SetInstanceID(info.msg.motorBoat.fuelStorageID);
			storageUnitInstance.uid = info.msg.motorBoat.storageid;
		}
	}

	public bool CanModifyEquipment()
	{
		if (base.isServer && UnityEngine.Time.time < NextUpgradeTime)
		{
			return false;
		}
		return true;
	}

	public void DelayNextUpgrade(float delay)
	{
		if (UnityEngine.Time.time + delay > NextUpgradeTime)
		{
			NextUpgradeTime = UnityEngine.Time.time + delay;
		}
	}

	public int GetEquipmentCount(ItemModHABEquipment item)
	{
		int num = 0;
		for (int num2 = children.Count - 1; num2 >= 0; num2--)
		{
			BaseEntity baseEntity = children[num2];
			if (!(baseEntity == null) && baseEntity.prefabID == item.Prefab.resourceID)
			{
				num++;
			}
		}
		return num;
	}

	public void RemoveItemsOfType(ItemModHABEquipment item)
	{
		for (int num = children.Count - 1; num >= 0; num--)
		{
			BaseEntity baseEntity = children[num];
			if (!(baseEntity == null) && baseEntity.prefabID == item.Prefab.resourceID)
			{
				baseEntity.Kill();
			}
		}
	}

	public bool WaterLogged()
	{
		return WaterLevel.Test(engineHeight.position, waves: true, volumes: true, this);
	}

	public bool OnlyOwnerAccessible()
	{
		return HasFlag(Flags.Locked);
	}

	public override void OnAttacked(HitInfo info)
	{
		if (IsSafe() && !info.damageTypes.Has(DamageType.Decay))
		{
			info.damageTypes.ScaleAll(0f);
		}
		base.OnAttacked(info);
	}

	protected override void OnChildAdded(BaseEntity child)
	{
		base.OnChildAdded(child);
		if (base.isServer)
		{
			if (isSpawned)
			{
				fuelSystem.CheckNewChild(child);
			}
			if (child.prefabID == storageUnitPrefab.GetEntity().prefabID)
			{
				storageUnitInstance.Set((StorageContainer)child);
				_ = storageUnitInstance.Get(serverside: true).inventory;
			}
			bool isLoadingSave = Rust.Application.isLoadingSave;
			HotAirBalloonEquipment hotAirBalloonEquipment = child as HotAirBalloonEquipment;
			if (hotAirBalloonEquipment != null)
			{
				hotAirBalloonEquipment.Added(this, isLoadingSave);
			}
		}
	}

	protected override void OnChildRemoved(BaseEntity child)
	{
		base.OnChildRemoved(child);
		if (base.isServer)
		{
			HotAirBalloonEquipment hotAirBalloonEquipment = child as HotAirBalloonEquipment;
			if (hotAirBalloonEquipment != null)
			{
				hotAirBalloonEquipment.Removed(this);
			}
		}
	}

	internal override void DoServerDestroy()
	{
		if (vehicle.vehiclesdroploot && storageUnitInstance.IsValid(base.isServer))
		{
			storageUnitInstance.Get(base.isServer).DropItems();
		}
		SeekerTarget.SetSeekerTarget(this, SeekerTarget.SeekerStrength.OFF);
		base.DoServerDestroy();
	}

	public bool IsValidSAMTarget(bool staticRespawn)
	{
		if (myRigidbody.IsSleeping() || myRigidbody.isKinematic)
		{
			return false;
		}
		if (staticRespawn)
		{
			return IsFullyInflated;
		}
		if (IsFullyInflated)
		{
			return !InSafeZone();
		}
		return false;
	}

	public override float GetNetworkTime()
	{
		return UnityEngine.Time.fixedTime;
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		ClearOwnerEntry();
		SetFlag(Flags.On, b: false);
	}

	[RPC_Server]
	public void RPC_OpenFuel(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (!(player == null) && (!OnlyOwnerAccessible() || !(msg.player != creatorEntity)))
		{
			fuelSystem.LootFuel(player);
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.hotAirBalloon = Facepunch.Pool.Get<ProtoBuf.HotAirBalloon>();
		info.msg.hotAirBalloon.inflationAmount = inflationLevel;
		info.msg.hotAirBalloon.sinceLastBlast = sinceLastBlast;
		if (info.forDisk && (bool)myRigidbody)
		{
			info.msg.hotAirBalloon.velocity = myRigidbody.velocity;
		}
		info.msg.motorBoat = Facepunch.Pool.Get<Motorboat>();
		info.msg.motorBoat.storageid = storageUnitInstance.uid;
		info.msg.motorBoat.fuelStorageID = fuelSystem.GetInstanceID();
	}

	public override void ServerInit()
	{
		myRigidbody.centerOfMass = centerOfMass.localPosition;
		myRigidbody.isKinematic = false;
		avgTerrainHeight = TerrainMeta.HeightMap.GetHeight(base.transform.position);
		base.ServerInit();
		bounds = collapsedBounds;
		InvokeRandomized(DecayTick, UnityEngine.Random.Range(30f, 60f), 60f, 6f);
		InvokeRandomized(UpdateIsGrounded, 0f, 3f, 0.2f);
		SeekerTarget.SetSeekerTarget(this, SeekerTarget.SeekerStrength.MEDIUM);
	}

	public void DecayTick()
	{
		if (base.healthFraction == 0f)
		{
			return;
		}
		if (IsFullyInflated)
		{
			bool flag = true;
			if (lastFailedDecayPosition != Vector3.zero && Distance(lastFailedDecayPosition) < 2f)
			{
				flag = false;
			}
			lastFailedDecayPosition = base.transform.position;
			if (flag)
			{
				return;
			}
			myRigidbody.AddForceAtPosition(Vector3.up * (0f - UnityEngine.Physics.gravity.y) * myRigidbody.mass * 20f, buoyancyPoint.position, ForceMode.Force);
			myRigidbody.AddForceAtPosition(UnityEngine.Random.onUnitSphere.WithY(0f) * 20f, buoyancyPoint.position, ForceMode.Force);
		}
		if (!((float)sinceLastBlast < 600f))
		{
			float num = 1f / outsidedecayminutes;
			if (IsOutside() || IsFullyInflated)
			{
				Hurt(MaxHealth() * num, DamageType.Decay, this, useProtection: false);
			}
		}
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void EngineSwitch(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (!(player == null) && (!OnlyOwnerAccessible() || !(player != creatorEntity)))
		{
			bool b = msg.read.Bit();
			SetFlag(Flags.On, b);
			if (IsOn())
			{
				Invoke(ScheduleOff, 60f);
			}
			else
			{
				CancelInvoke(ScheduleOff);
			}
		}
	}

	public void ScheduleOff()
	{
		SetFlag(Flags.On, b: false);
	}

	public void UpdateIsGrounded()
	{
		List<Collider> obj = Facepunch.Pool.Get<List<Collider>>();
		GamePhysics.OverlapSphere(groundSample.transform.position, 1.25f, obj, 1218511105);
		grounded = obj.Count > 0;
		CheckGlobal(flags);
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	public override void OnFlagsChanged(Flags old, Flags next)
	{
		base.OnFlagsChanged(old, next);
		if (base.isServer)
		{
			CheckGlobal(next);
			if (myRigidbody != null)
			{
				myRigidbody.isKinematic = IsTransferProtected();
			}
		}
	}

	private void CheckGlobal(Flags flags)
	{
		bool wants = flags.HasFlag(Flags.On) || flags.HasFlag(Flags.Reserved2) || flags.HasFlag(Flags.Reserved1) || !grounded;
		EnableGlobalBroadcast(wants);
	}

	protected void FixedUpdate()
	{
		if (!isSpawned || base.isClient || IsTransferProtected())
		{
			return;
		}
		if (!fuelSystem.HasFuel() || WaterLogged())
		{
			SetFlag(Flags.On, b: false);
		}
		if (IsOn())
		{
			fuelSystem.TryUseFuel(UnityEngine.Time.fixedDeltaTime, fuelPerSec);
		}
		SetFlag(Flags.Reserved6, fuelSystem.HasFuel());
		SetFlag(Flags.Reserved7, grounded);
		SetFlag(Flags.Reserved8, CanModifyEquipment());
		bool flag = (IsFullyInflated && myRigidbody.velocity.y < 0f) || myRigidbody.velocity.y < 0.75f;
		GameObject[] array = killTriggers;
		foreach (GameObject gameObject in array)
		{
			if (gameObject.activeSelf != flag)
			{
				gameObject.SetActive(flag);
			}
		}
		float num = inflationLevel;
		if (IsOn() && !IsFullyInflated)
		{
			inflationLevel = Mathf.Clamp01(inflationLevel + UnityEngine.Time.fixedDeltaTime / 10f);
		}
		else if (grounded && inflationLevel > 0f && !IsOn() && ((float)sinceLastBlast > 30f || WaterLogged()))
		{
			inflationLevel = Mathf.Clamp01(inflationLevel - UnityEngine.Time.fixedDeltaTime / 10f);
		}
		if (num != inflationLevel)
		{
			if (IsFullyInflated)
			{
				bounds = raisedBounds;
			}
			else if (inflationLevel == 0f)
			{
				bounds = collapsedBounds;
			}
			SetFlag(Flags.Reserved1, inflationLevel > 0.3f);
			SetFlag(Flags.Reserved2, inflationLevel >= 1f);
			SendNetworkUpdate();
			_ = inflationLevel;
		}
		bool flag2 = !myRigidbody.IsSleeping() || inflationLevel > 0f;
		array = balloonColliders;
		foreach (GameObject gameObject2 in array)
		{
			if (gameObject2.activeSelf != flag2)
			{
				gameObject2.SetActive(flag2);
			}
		}
		if (IsOn())
		{
			if (IsFullyInflated)
			{
				currentBuoyancy += UnityEngine.Time.fixedDeltaTime * 0.2f;
				sinceLastBlast = 0f;
			}
		}
		else
		{
			currentBuoyancy -= UnityEngine.Time.fixedDeltaTime * 0.1f;
		}
		currentBuoyancy = Mathf.Clamp(currentBuoyancy, 0f, 0.8f + 0.2f * base.healthFraction);
		if (inflationLevel > 0f)
		{
			float b = Mathf.Max(minimumAltitudeTerrain, TerrainMeta.HeightMap.GetHeight(base.transform.position));
			avgTerrainHeight = Mathf.Lerp(avgTerrainHeight, b, UnityEngine.Time.deltaTime);
			float num2 = 1f - Mathf.InverseLerp(avgTerrainHeight + serviceCeiling - 20f, avgTerrainHeight + serviceCeiling, buoyancyPoint.position.y);
			myRigidbody.AddForceAtPosition(Vector3.up * (0f - UnityEngine.Physics.gravity.y) * myRigidbody.mass * 0.5f * inflationLevel, buoyancyPoint.position, ForceMode.Force);
			myRigidbody.AddForceAtPosition(Vector3.up * liftAmount * currentBuoyancy * num2, buoyancyPoint.position, ForceMode.Force);
			Vector3 windAtPos = GetWindAtPos(buoyancyPoint.position);
			_ = windAtPos.magnitude;
			float num3 = 1f;
			float waterOrTerrainSurface = WaterLevel.GetWaterOrTerrainSurface(buoyancyPoint.position, waves: false, volumes: false);
			float num4 = Mathf.InverseLerp(waterOrTerrainSurface + 20f, waterOrTerrainSurface + 60f, buoyancyPoint.position.y);
			float num5 = 1f;
			if (UnityEngine.Physics.SphereCast(new Ray(base.transform.position + Vector3.up * 2f, Vector3.down), 1.5f, out var hitInfo, 5f, 1218511105))
			{
				num5 = Mathf.Clamp01(hitInfo.distance / 5f);
			}
			num3 *= num4 * num2 * num5;
			num3 *= 0.2f + 0.8f * base.healthFraction;
			Vector3 vector = windAtPos.normalized * num3 * windForce;
			currentWindVec = Vector3.Lerp(currentWindVec, vector, UnityEngine.Time.fixedDeltaTime * 0.25f);
			myRigidbody.AddForceAtPosition(vector * 0.1f, buoyancyPoint.position, ForceMode.Force);
			myRigidbody.AddForce(vector * 0.9f, ForceMode.Force);
		}
		if (OnlyOwnerAccessible() && safeAreaRadius != -1f && Vector3.Distance(base.transform.position, safeAreaOrigin) > safeAreaRadius)
		{
			ClearOwnerEntry();
		}
	}

	public override Vector3 GetLocalVelocityServer()
	{
		if (myRigidbody == null)
		{
			return Vector3.zero;
		}
		return myRigidbody.velocity;
	}

	public override Quaternion GetAngularVelocityServer()
	{
		if (myRigidbody == null)
		{
			return Quaternion.identity;
		}
		return Quaternion.Euler(myRigidbody.angularVelocity * 57.29578f);
	}

	public void ClearOwnerEntry()
	{
		creatorEntity = null;
		SetFlag(Flags.Locked, b: false);
		safeAreaRadius = -1f;
		safeAreaOrigin = Vector3.zero;
	}

	public bool IsSafe()
	{
		if (OnlyOwnerAccessible())
		{
			return Vector3.Distance(safeAreaOrigin, base.transform.position) <= safeAreaRadius;
		}
		return false;
	}

	public void SetupOwner(BasePlayer owner, Vector3 newSafeAreaOrigin, float newSafeAreaRadius)
	{
		if (owner != null)
		{
			creatorEntity = owner;
			SetFlag(Flags.Locked, b: true);
			safeAreaRadius = newSafeAreaRadius;
			safeAreaOrigin = newSafeAreaOrigin;
			spawnTime = UnityEngine.Time.realtimeSinceStartup;
		}
	}

	public bool IsDespawnEligable()
	{
		if (spawnTime != -1f)
		{
			return spawnTime + 300f < UnityEngine.Time.realtimeSinceStartup;
		}
		return true;
	}

	public IFuelSystem GetFuelSystem()
	{
		return fuelSystem;
	}

	public int StartingFuelUnits()
	{
		return 75;
	}

	public Vector3 GetWindAtPos(Vector3 pos)
	{
		float num = pos.y * 6f;
		return new Vector3(Mathf.Sin(num * (MathF.PI / 180f)), 0f, Mathf.Cos(num * (MathF.PI / 180f))).normalized * 1f;
	}

	public bool PlayerHasEquipmentItem(BasePlayer player, int tokenItemID)
	{
		return GetEquipmentItem(player, tokenItemID) != null;
	}

	public Item GetEquipmentItem(BasePlayer player, int tokenItemID)
	{
		return player.inventory.FindItemByItemID(tokenItemID);
	}

	public override float MaxHealth()
	{
		if (base.isServer)
		{
			return base.MaxHealth();
		}
		float num = base.MaxHealth();
		float num2 = 0f;
		foreach (BaseEntity child in children)
		{
			if (child is HotAirBalloonArmor hotAirBalloonArmor)
			{
				num2 += hotAirBalloonArmor.AdditionalHealth;
			}
		}
		return num + num2;
	}

	public override List<ItemAmount> BuildCost()
	{
		List<ItemAmount> list = new List<ItemAmount>(base.BuildCost());
		foreach (BaseEntity child in children)
		{
			if (child is HotAirBalloonEquipment hotAirBalloonEquipment)
			{
				list.AddRange(hotAirBalloonEquipment.BuildCost());
			}
		}
		return list;
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void RPC_ReqEquipItem(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (player == null)
		{
			return;
		}
		int tokenItemID = msg.read.Int32();
		Item equipmentItem = GetEquipmentItem(player, tokenItemID);
		if (equipmentItem != null)
		{
			ItemModHABEquipment component = equipmentItem.info.GetComponent<ItemModHABEquipment>();
			if (!(component == null) && component.CanEquipToHAB(this))
			{
				component.ApplyToHAB(this);
				equipmentItem.UseItem();
				SendNetworkUpdateImmediate();
			}
		}
	}

	public bool IsValidHomingTarget()
	{
		if (ConVar.Server.homingMissileTargetsHab)
		{
			return flags.HasFlag(Flags.Reserved2);
		}
		return false;
	}
}
