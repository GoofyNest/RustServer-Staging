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
using UnityEngine.Serialization;

public class TrainCar : BaseVehicle, ITrainCollidable, IPrefabPreProcess, TriggerHurtNotChild.IHurtTriggerUser, TrainTrackSpline.ITrainTrackUser
{
	public enum TrainCarType
	{
		Wagon,
		Engine,
		Other
	}

	[Header("Train Car")]
	[SerializeField]
	private float corpseSeconds = 60f;

	[SerializeField]
	private TriggerTrainCollisions frontCollisionTrigger;

	[SerializeField]
	private TriggerTrainCollisions rearCollisionTrigger;

	[SerializeField]
	private float collisionDamageDivide = 100000f;

	[SerializeField]
	private float derailCollisionForce = 130000f;

	[SerializeField]
	private TriggerHurtNotChild hurtTriggerFront;

	[SerializeField]
	private TriggerHurtNotChild hurtTriggerRear;

	[SerializeField]
	private GameObject[] hurtOrRepelTriggersInternal;

	[SerializeField]
	private float hurtTriggerMinSpeed = 1f;

	[SerializeField]
	private Transform centreOfMassTransform;

	[SerializeField]
	private Transform frontBogiePivot;

	[SerializeField]
	private bool frontBogieCanRotate = true;

	[SerializeField]
	private Transform rearBogiePivot;

	[SerializeField]
	private bool rearBogieCanRotate = true;

	[SerializeField]
	private Transform[] wheelVisuals;

	[SerializeField]
	private float wheelRadius = 0.615f;

	[FormerlySerializedAs("fxFinalExplosion")]
	[SerializeField]
	private GameObjectRef fxDestroyed;

	[SerializeField]
	protected TriggerParent platformParentTrigger;

	public GameObjectRef collisionEffect;

	public Transform frontCoupling;

	public Transform frontCouplingPivot;

	public Transform rearCoupling;

	public Transform rearCouplingPivot;

	[SerializeField]
	private SoundDefinition coupleSound;

	[SerializeField]
	private SoundDefinition uncoupleSound;

	[SerializeField]
	private TrainCarAudio trainCarAudio;

	[FormerlySerializedAs("frontCoupleFx")]
	[SerializeField]
	private ParticleSystem frontCouplingChangedFx;

	[FormerlySerializedAs("rearCoupleFx")]
	[SerializeField]
	private ParticleSystem rearCouplingChangedFx;

	[FormerlySerializedAs("fxCoupling")]
	[SerializeField]
	private ParticleSystem newCouplingFX;

	[SerializeField]
	private float decayTimeMultiplier = 1f;

	[SerializeField]
	[ReadOnly]
	private Vector3 frontBogieLocalOffset;

	[SerializeField]
	[ReadOnly]
	private Vector3 rearBogieLocalOffset;

	[SerializeField]
	private GameObjectRef itemStoragePrefab;

	private EntityRef<StorageContainer> itemStorageInstance;

	[ServerVar(Help = "Population active on the server", ShowInAdminUI = true)]
	public static float population = 2.3f;

	[ServerVar(Help = "Ratio of wagons to train engines that spawn")]
	public static int wagons_per_engine = 2;

	[ServerVar(Help = "How long before a train car despawns")]
	public static float decayminutes = 30f;

	[ReadOnly]
	public float DistFrontWheelToFrontCoupling;

	[ReadOnly]
	public float DistFrontWheelToBackCoupling;

	public TrainCouplingController coupling;

	[NonSerialized]
	public TrainTrackSpline.TrackSelection localTrackSelection;

	public const Flags Flag_LinedUpToUnload = Flags.Reserved4;

	public static float TRAINCAR_MAX_SPEED = 25f;

	protected bool trainDebug;

	public CompleteTrain completeTrain;

	[NonSerialized]
	public float savedTrackSpeed;

	private bool frontAtEndOfLine;

	private bool rearAtEndOfLine;

	private float frontBogieYRot;

	private float rearBogieYRot;

	private Vector3 spawnOrigin;

	private bool coupledBackwards;

	private TrainTrackSpline _frontTrackSection;

	private float distFrontToBackWheel;

	private float initialSpawnTime;

	protected float decayingFor;

	private float decayTickSpacing = 60f;

	private float lastDecayTick;

	public TriggerTrainCollisions FrontCollisionTrigger => frontCollisionTrigger;

	public TriggerTrainCollisions RearCollisionTrigger => rearCollisionTrigger;

	public virtual TrainCarType CarType => TrainCarType.Wagon;

	public bool LinedUpToUnload => HasFlag(Flags.Reserved4);

	public Vector3 Position => base.transform.position;

	public float FrontWheelSplineDist { get; private set; }

	public bool FrontAtEndOfLine => frontAtEndOfLine;

	public bool RearAtEndOfLine => rearAtEndOfLine;

	protected virtual bool networkUpdateOnCompleteTrainChange => false;

	public TrainTrackSpline FrontTrackSection
	{
		get
		{
			return _frontTrackSection;
		}
		private set
		{
			if (_frontTrackSection != value)
			{
				if (_frontTrackSection != null)
				{
					_frontTrackSection.DeregisterTrackUser(this);
				}
				_frontTrackSection = value;
				if (_frontTrackSection != null)
				{
					_frontTrackSection.RegisterTrackUser(this);
				}
			}
		}
	}

	public TrainTrackSpline RearTrackSection { get; private set; }

	protected bool IsAtAStation
	{
		get
		{
			if (FrontTrackSection != null)
			{
				return FrontTrackSection.isStation;
			}
			return false;
		}
	}

	protected bool IsOnAboveGroundSpawnRail
	{
		get
		{
			if (FrontTrackSection != null)
			{
				return FrontTrackSection.aboveGroundSpawn;
			}
			return false;
		}
	}

	private bool RecentlySpawned => UnityEngine.Time.time < initialSpawnTime + 2f;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("TrainCar.OnRpcMessage"))
		{
			if (rpc == 924237371 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_OpenItemStorage ");
				}
				using (TimeWarning.New("RPC_OpenItemStorage"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(924237371u, "RPC_OpenItemStorage", this, player, 3f))
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
							RPC_OpenItemStorage(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_OpenItemStorage");
					}
				}
				return true;
			}
			if (rpc == 3930273067u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_WantsUncouple ");
				}
				using (TimeWarning.New("RPC_WantsUncouple"))
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
							RPC_WantsUncouple(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in RPC_WantsUncouple");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void PreProcess(IPrefabProcessor process, GameObject rootObj, string name, bool serverside, bool clientside, bool bundling)
	{
		base.PreProcess(process, rootObj, name, serverside, clientside, bundling);
		frontBogieLocalOffset = base.transform.InverseTransformPoint(frontBogiePivot.position);
		float num = ((!(frontCoupling != null)) ? (bounds.extents.z + bounds.center.z) : base.transform.InverseTransformPoint(frontCoupling.position).z);
		float num2 = ((!(rearCoupling != null)) ? (0f - bounds.extents.z + bounds.center.z) : base.transform.InverseTransformPoint(rearCoupling.position).z);
		DistFrontWheelToFrontCoupling = num - frontBogieLocalOffset.z;
		DistFrontWheelToBackCoupling = 0f - num2 + frontBogieLocalOffset.z;
		rearBogieLocalOffset = base.transform.InverseTransformPoint(rearBogiePivot.position);
	}

	public override void InitShared()
	{
		base.InitShared();
		coupling = new TrainCouplingController(this);
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.baseTrain != null && base.isServer)
		{
			frontBogieYRot = info.msg.baseTrain.frontBogieYRot;
			rearBogieYRot = info.msg.baseTrain.rearBogieYRot;
			itemStorageInstance.uid = info.msg.baseTrain.itemStorageID;
		}
	}

	public override void OnFlagsChanged(Flags old, Flags next)
	{
		base.OnFlagsChanged(old, next);
		if (old != next && base.isServer)
		{
			ServerFlagsChanged(old, next);
		}
	}

	public bool CustomCollision(TrainCar train, TriggerTrainCollisions trainTrigger)
	{
		return false;
	}

	public override float InheritedVelocityScale()
	{
		return 0.5f;
	}

	protected virtual void SetTrackSelection(TrainTrackSpline.TrackSelection trackSelection)
	{
		if (localTrackSelection != trackSelection)
		{
			localTrackSelection = trackSelection;
			if (base.isServer)
			{
				ClientRPC(RpcTarget.NetworkGroup("SetTrackSelection"), (sbyte)localTrackSelection);
			}
		}
	}

	protected bool PlayerIsOnPlatform(BasePlayer player)
	{
		return player.GetParentEntity() == this;
	}

	public override void ServerInit()
	{
		base.ServerInit();
		spawnOrigin = base.transform.position;
		distFrontToBackWheel = Vector3.Distance(GetFrontWheelPos(), GetRearWheelPos());
		rigidBody.centerOfMass = centreOfMassTransform.localPosition;
		UpdateCompleteTrain();
		lastDecayTick = UnityEngine.Time.time;
		InvokeRandomized(UpdateClients, 0f, 0.15f, 0.02f);
		InvokeRandomized(DecayTick, UnityEngine.Random.Range(20f, 40f), decayTickSpacing, decayTickSpacing * 0.1f);
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		if (base.health <= 0f)
		{
			ActualDeath();
			return;
		}
		SetFlag(Flags.Reserved2, b: false);
		SetFlag(Flags.Reserved3, b: false);
	}

	public override void Spawn()
	{
		base.Spawn();
		initialSpawnTime = UnityEngine.Time.time;
		if (TrainTrackSpline.TryFindTrackNear(GetFrontWheelPos(), 15f, out var splineResult, out var distResult))
		{
			FrontWheelSplineDist = distResult;
			Vector3 tangent;
			Vector3 positionAndTangent = splineResult.GetPositionAndTangent(FrontWheelSplineDist, base.transform.forward, out tangent);
			SetTheRestFromFrontWheelData(ref splineResult, positionAndTangent, tangent, localTrackSelection, null, instantMove: true);
			FrontTrackSection = splineResult;
			if (!Rust.Application.isLoadingSave && !SpaceIsClear())
			{
				Invoke(base.KillMessage, 0f);
			}
		}
		else
		{
			Invoke(base.KillMessage, 0f);
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.baseTrain = Facepunch.Pool.Get<BaseTrain>();
		info.msg.baseTrain.time = GetNetworkTime();
		info.msg.baseTrain.frontBogieYRot = frontBogieYRot;
		info.msg.baseTrain.rearBogieYRot = rearBogieYRot;
		info.msg.baseTrain.itemStorageID = itemStorageInstance.uid;
		if (coupling.frontCoupling.TryGetCoupledToID(out var id))
		{
			info.msg.baseTrain.frontCouplingID = id;
			info.msg.baseTrain.frontCouplingToFront = coupling.frontCoupling.CoupledTo.isFrontCoupling;
		}
		if (coupling.rearCoupling.TryGetCoupledToID(out id))
		{
			info.msg.baseTrain.rearCouplingID = id;
			info.msg.baseTrain.rearCouplingToFront = coupling.rearCoupling.CoupledTo.isFrontCoupling;
		}
	}

	protected virtual void ServerFlagsChanged(Flags old, Flags next)
	{
		if (isSpawned && (next.HasFlag(Flags.Reserved2) != old.HasFlag(Flags.Reserved2) || next.HasFlag(Flags.Reserved3) != old.HasFlag(Flags.Reserved3)))
		{
			UpdateCompleteTrain();
		}
	}

	private void UpdateCompleteTrain()
	{
		List<TrainCar> result = Facepunch.Pool.Get<List<TrainCar>>();
		coupling.GetAll(ref result);
		SetNewCompleteTrain(new CompleteTrain(result));
	}

	public void SetNewCompleteTrain(CompleteTrain ct)
	{
		if (completeTrain != ct)
		{
			RemoveFromCompleteTrain();
			completeTrain = ct;
			coupledBackwards = ct.GetIsCoupledBackwards(this);
			if (networkUpdateOnCompleteTrainChange)
			{
				SendNetworkUpdate();
			}
		}
	}

	public override void Hurt(HitInfo info)
	{
		if (!RecentlySpawned)
		{
			base.Hurt(info);
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	public void RPC_OpenItemStorage(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (CanBeLooted(player))
		{
			StorageContainer itemContainer = GetItemContainer();
			if (itemContainer != null)
			{
				itemContainer.PlayerOpenLoot(player);
			}
		}
	}

	protected override void OnChildAdded(BaseEntity child)
	{
		base.OnChildAdded(child);
		if (base.isServer && itemStoragePrefab != null && itemStoragePrefab.isValid)
		{
			BaseEntity entity = itemStoragePrefab.GetEntity();
			if (entity != null && child.prefabID == entity.prefabID)
			{
				itemStorageInstance.Set((StorageContainer)child);
			}
		}
	}

	public StorageContainer GetItemContainer()
	{
		BaseEntity baseEntity = itemStorageInstance.Get(base.isServer);
		if (baseEntity != null && baseEntity.IsValid())
		{
			return baseEntity as StorageContainer;
		}
		return null;
	}

	public override void OnKilled(HitInfo info)
	{
		float num = info?.damageTypes.Get(DamageType.AntiVehicle) ?? 0f;
		float num2 = info?.damageTypes.Get(DamageType.Explosion) ?? 0f;
		float num3 = info?.damageTypes.Total() ?? 0f;
		if ((num + num2) / num3 > 0.5f || vehicle.cinematictrains || corpseSeconds == 0f)
		{
			if (HasDriver())
			{
				GetDriver().Hurt(float.MaxValue);
			}
			base.OnKilled(info);
		}
		else
		{
			Invoke(ActualDeath, corpseSeconds);
		}
		if (base.IsDestroyed && fxDestroyed.isValid)
		{
			Effect.server.Run(fxDestroyed.resourcePath, GetExplosionPos(), Vector3.up, null, broadcast: true);
		}
	}

	protected virtual Vector3 GetExplosionPos()
	{
		return GetCentreOfTrainPos();
	}

	public void ActualDeath()
	{
		Kill(DestroyMode.Gib);
	}

	public override void DoRepair(BasePlayer player)
	{
		base.DoRepair(player);
		if (IsDead() && Health() > 0f)
		{
			CancelInvoke(ActualDeath);
			lifestate = LifeState.Alive;
		}
	}

	public float GetDamageMultiplier(BaseEntity ent)
	{
		return Mathf.Abs(GetTrackSpeed()) * 1f;
	}

	public void OnHurtTriggerOccupant(BaseEntity hurtEntity, DamageType damageType, float damageTotal)
	{
	}

	internal override void DoServerDestroy()
	{
		if (FrontTrackSection != null)
		{
			FrontTrackSection.DeregisterTrackUser(this);
		}
		coupling.Uncouple(front: true);
		coupling.Uncouple(front: false);
		RemoveFromCompleteTrain();
		if (vehicle.vehiclesdroploot)
		{
			StorageContainer storageContainer = itemStorageInstance.Get(base.isServer);
			if (storageContainer != null && storageContainer.IsValid())
			{
				storageContainer.DropItems();
			}
		}
		base.DoServerDestroy();
	}

	private void RemoveFromCompleteTrain()
	{
		if (completeTrain != null)
		{
			if (completeTrain.ContainsOnly(this))
			{
				completeTrain.Dispose();
				completeTrain = null;
			}
			else
			{
				completeTrain.RemoveTrainCar(this);
			}
		}
	}

	public override bool MountEligable(BasePlayer player)
	{
		if (IsDead())
		{
			return false;
		}
		return base.MountEligable(player);
	}

	public override float MaxVelocity()
	{
		return TRAINCAR_MAX_SPEED;
	}

	public float GetTrackSpeed()
	{
		if (completeTrain == null)
		{
			return 0f;
		}
		return completeTrain.GetTrackSpeedFor(this);
	}

	public bool IsCoupledBackwards()
	{
		if (completeTrain == null)
		{
			return false;
		}
		return coupledBackwards;
	}

	public float GetPrevTrackSpeed()
	{
		if (completeTrain == null)
		{
			return 0f;
		}
		return completeTrain.GetPrevTrackSpeedFor(this);
	}

	public override Vector3 GetLocalVelocityServer()
	{
		return base.transform.forward * GetTrackSpeed();
	}

	public bool AnyPlayersOnTrainCar()
	{
		if (AnyMounted())
		{
			return true;
		}
		if (platformParentTrigger != null && platformParentTrigger.HasAnyEntityContents)
		{
			foreach (BaseEntity entityContent in platformParentTrigger.entityContents)
			{
				if (entityContent.ToPlayer() != null)
				{
					return true;
				}
			}
		}
		return false;
	}

	public override void VehicleFixedUpdate()
	{
		using (TimeWarning.New("TrainCar.VehicleFixedUpdate"))
		{
			base.VehicleFixedUpdate();
			if (completeTrain != null)
			{
				completeTrain.UpdateTick(UnityEngine.Time.fixedDeltaTime);
				float trackSpeed = GetTrackSpeed();
				hurtTriggerFront.gameObject.SetActive(!coupling.IsFrontCoupled && trackSpeed > hurtTriggerMinSpeed);
				hurtTriggerRear.gameObject.SetActive(!coupling.IsRearCoupled && trackSpeed < 0f - hurtTriggerMinSpeed);
				GameObject[] array = hurtOrRepelTriggersInternal;
				for (int i = 0; i < array.Length; i++)
				{
					array[i].SetActive(Mathf.Abs(trackSpeed) > hurtTriggerMinSpeed);
				}
			}
		}
	}

	public override void PostVehicleFixedUpdate()
	{
		base.PostVehicleFixedUpdate();
		if (completeTrain != null)
		{
			completeTrain.ResetUpdateTick();
		}
	}

	public Vector3 GetCentreOfTrainPos()
	{
		return base.transform.position + base.transform.rotation * bounds.center;
	}

	public Vector3 GetFrontOfTrainPos()
	{
		return base.transform.position + base.transform.rotation * (bounds.center + Vector3.forward * bounds.extents.z);
	}

	public Vector3 GetRearOfTrainPos()
	{
		return base.transform.position + base.transform.rotation * (bounds.center - Vector3.forward * bounds.extents.z);
	}

	public void FrontTrainCarTick(TrainTrackSpline.TrackSelection trackSelection, float dt)
	{
		float distToMove = GetTrackSpeed() * dt;
		TrainTrackSpline preferredAltTrack = ((RearTrackSection != FrontTrackSection) ? RearTrackSection : null);
		MoveFrontWheelsAlongTrackSpline(FrontTrackSection, FrontWheelSplineDist, distToMove, preferredAltTrack, trackSelection);
	}

	public void OtherTrainCarTick(TrainTrackSpline theirTrackSpline, float prevSplineDist, float distanceOffset)
	{
		MoveFrontWheelsAlongTrackSpline(theirTrackSpline, prevSplineDist, distanceOffset, FrontTrackSection, TrainTrackSpline.TrackSelection.Default);
	}

	public bool TryGetNextTrainCar(Vector3 forwardDir, out TrainCar result)
	{
		return TryGetTrainCar(next: true, forwardDir, out result);
	}

	public bool TryGetPrevTrainCar(Vector3 forwardDir, out TrainCar result)
	{
		return TryGetTrainCar(next: false, forwardDir, out result);
	}

	public bool TryGetTrainCar(bool next, Vector3 forwardDir, out TrainCar result)
	{
		result = null;
		if (completeTrain == null)
		{
			return false;
		}
		return completeTrain.TryGetAdjacentTrainCar(this, next, forwardDir, out result);
	}

	private void MoveFrontWheelsAlongTrackSpline(TrainTrackSpline trackSpline, float prevSplineDist, float distToMove, TrainTrackSpline preferredAltTrack, TrainTrackSpline.TrackSelection trackSelection)
	{
		TrainTrackSpline.MoveResult moveResult = trackSpline.MoveAlongSpline(tReq: new TrainTrackSpline.TrackRequest(trackSelection, preferredAltTrack, null), prevSplineDist: prevSplineDist, askerForward: base.transform.forward, distMoved: distToMove);
		TrainTrackSpline frontTS = moveResult.spline;
		FrontWheelSplineDist = moveResult.distAlongSpline;
		frontAtEndOfLine = moveResult.atEndOfLine;
		Vector3 tangent;
		Vector3 positionAndTangent = frontTS.GetPositionAndTangent(FrontWheelSplineDist, base.transform.forward, out tangent);
		SetTheRestFromFrontWheelData(ref frontTS, positionAndTangent, tangent, trackSelection, trackSpline, instantMove: false);
		FrontTrackSection = frontTS;
	}

	private Vector3 GetFrontWheelPos()
	{
		return base.transform.position + base.transform.rotation * frontBogieLocalOffset;
	}

	private Vector3 GetRearWheelPos()
	{
		return base.transform.position + base.transform.rotation * rearBogieLocalOffset;
	}

	private void SetTheRestFromFrontWheelData(ref TrainTrackSpline frontTS, Vector3 targetFrontWheelPos, Vector3 targetFrontWheelTangent, TrainTrackSpline.TrackSelection trackSelection, TrainTrackSpline additionalAlt, bool instantMove)
	{
		TrainTrackSpline.TrackRequest tReq = new TrainTrackSpline.TrackRequest(trackSelection, RearTrackSection, additionalAlt);
		TrainTrackSpline.MoveResult moveResult = frontTS.MoveAlongSpline(FrontWheelSplineDist, base.transform.forward, 0f - distFrontToBackWheel, tReq);
		TrainTrackSpline spline = moveResult.spline;
		float distAlongSpline = moveResult.distAlongSpline;
		rearAtEndOfLine = moveResult.atEndOfLine;
		Vector3 tangent;
		Vector3 positionAndTangent = spline.GetPositionAndTangent(distAlongSpline, base.transform.forward, out tangent);
		if (rearAtEndOfLine)
		{
			moveResult = spline.MoveAlongSpline(tReq: new TrainTrackSpline.TrackRequest(trackSelection, spline, additionalAlt), prevSplineDist: distAlongSpline, askerForward: base.transform.forward, distMoved: distFrontToBackWheel);
			frontTS = moveResult.spline;
			FrontWheelSplineDist = moveResult.distAlongSpline;
			frontAtEndOfLine = moveResult.atEndOfLine;
			targetFrontWheelPos = frontTS.GetPositionAndTangent(FrontWheelSplineDist, base.transform.forward, out targetFrontWheelTangent);
		}
		RearTrackSection = spline;
		Vector3 normalized = (targetFrontWheelPos - positionAndTangent).normalized;
		Vector3 vector = targetFrontWheelPos - Quaternion.LookRotation(normalized) * frontBogieLocalOffset;
		if (instantMove)
		{
			base.transform.position = vector;
			if (normalized.magnitude == 0f)
			{
				base.transform.rotation = Quaternion.identity;
			}
			else
			{
				base.transform.rotation = Quaternion.LookRotation(normalized);
			}
		}
		else
		{
			base.transform.position = vector;
			if (normalized.magnitude == 0f)
			{
				base.transform.rotation = Quaternion.identity;
			}
			else
			{
				base.transform.rotation = Quaternion.LookRotation(normalized);
			}
		}
		frontBogieYRot = Vector3.SignedAngle(base.transform.forward, targetFrontWheelTangent, base.transform.up);
		rearBogieYRot = Vector3.SignedAngle(base.transform.forward, tangent, base.transform.up);
		if (UnityEngine.Application.isEditor)
		{
			Debug.DrawLine(targetFrontWheelPos, positionAndTangent, Color.magenta, 0.2f);
			Debug.DrawLine(rigidBody.position, vector, Color.yellow, 0.2f);
			Debug.DrawRay(vector, Vector3.up, Color.yellow, 0.2f);
		}
	}

	public float GetForces()
	{
		float num = base.transform.localEulerAngles.x;
		if (num > 180f)
		{
			num -= 360f;
		}
		return 0f + num / 90f * (0f - UnityEngine.Physics.gravity.y) * RealisticMass * 0.33f + GetThrottleForce();
	}

	protected virtual float GetThrottleForce()
	{
		return 0f;
	}

	public virtual bool HasThrottleInput()
	{
		return false;
	}

	public float ApplyCollisionDamage(float forceMagnitude)
	{
		float num = ((!(forceMagnitude > derailCollisionForce)) ? (Mathf.Pow(forceMagnitude, 1.3f) / collisionDamageDivide) : float.MaxValue);
		Hurt(num, DamageType.Collision, this, useProtection: false);
		return num;
	}

	public bool SpaceIsClear()
	{
		List<Collider> obj = Facepunch.Pool.Get<List<Collider>>();
		GamePhysics.OverlapOBB(WorldSpaceBounds(), obj, 32768);
		foreach (Collider item in obj)
		{
			if (!ColliderIsPartOfTrain(item))
			{
				Facepunch.Pool.FreeUnmanaged(ref obj);
				return false;
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return true;
	}

	public bool ColliderIsPartOfTrain(Collider collider)
	{
		BaseEntity baseEntity = collider.ToBaseEntity();
		if (baseEntity == null)
		{
			return false;
		}
		if (baseEntity == this)
		{
			return true;
		}
		BaseEntity baseEntity2 = baseEntity.parentEntity.Get(base.isServer);
		if (baseEntity2.IsValid())
		{
			return baseEntity2 == this;
		}
		return false;
	}

	private void UpdateClients()
	{
		if (IsMoving())
		{
			ClientRPC(RpcTarget.NetworkGroup("BaseTrainUpdate"), GetNetworkTime(), frontBogieYRot, rearBogieYRot);
		}
	}

	private void DecayTick()
	{
		if (completeTrain == null)
		{
			return;
		}
		bool flag = HasDriver() || completeTrain.AnyPlayersOnTrain();
		if (flag)
		{
			decayingFor = 0f;
		}
		float num = GetDecayMinutes(flag) * 60f;
		float time = UnityEngine.Time.time;
		float num2 = time - lastDecayTick;
		lastDecayTick = time;
		if (num != float.PositiveInfinity)
		{
			decayingFor += num2;
			if (decayingFor >= num && CanDieFromDecayNow())
			{
				ActualDeath();
			}
		}
	}

	protected virtual float GetDecayMinutes(bool hasPassengers)
	{
		bool flag = IsAtAStation && Vector3.Distance(spawnOrigin, base.transform.position) < 50f;
		if (hasPassengers || AnyPlayersNearby(30f) || flag || IsOnAboveGroundSpawnRail)
		{
			return float.PositiveInfinity;
		}
		return decayminutes * decayTimeMultiplier;
	}

	protected virtual bool CanDieFromDecayNow()
	{
		if (CarType != TrainCarType.Engine)
		{
			return !completeTrain.IncludesAnEngine();
		}
		return true;
	}

	private bool AnyPlayersNearby(float maxDist)
	{
		return BaseNetworkable.HasCloseConnections(base.transform.position, maxDist);
	}

	[RPC_Server]
	public void RPC_WantsUncouple(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (!(player == null) && !(Vector3.SqrMagnitude(base.transform.position - player.transform.position) > 200f))
		{
			bool front = msg.read.Bit();
			coupling.Uncouple(front);
		}
	}
}
