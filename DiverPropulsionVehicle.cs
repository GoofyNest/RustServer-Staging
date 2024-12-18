#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Facepunch.Utility;
using Network;
using ProtoBuf;
using Rust;
using Rust.UI;
using UnityEngine;
using UnityEngine.Assertions;

public class DiverPropulsionVehicle : BaseMountable, IEngineControllerUser, IEntity
{
	[Header("DPV")]
	[SerializeField]
	private Buoyancy buoyancy;

	[SerializeField]
	private GameObjectRef fuelStoragePrefab;

	[SerializeField]
	private Transform propellerTransform;

	[SerializeField]
	private float engineKW = 25f;

	[SerializeField]
	private float turnPower = 0.1f;

	[SerializeField]
	private float depthChangeTargetSpeed = 1f;

	[SerializeField]
	private float engineStartupTime = 0.5f;

	[SerializeField]
	private float idleFuelPerSec = 0.03f;

	[SerializeField]
	private float maxFuelPerSec = 0.15f;

	[SerializeField]
	private GameObject characterWorldCollider;

	[SerializeField]
	private float timeUntilAutoSurface = 600f;

	[SerializeField]
	private float minWaterDepth = 0.75f;

	[Header("DPV - Control stability")]
	[SerializeField]
	private float rotStability = 0.05f;

	[SerializeField]
	private float rotPower = 1f;

	[SerializeField]
	private float rotTargetChangeRate = 1f;

	[SerializeField]
	private float vertStability = 0.1f;

	[SerializeField]
	private float maxPitchDegrees = 20f;

	[SerializeField]
	private float maxRollDegrees = 30f;

	[Header("DPV - UI")]
	[SerializeField]
	private Canvas dashboardCanvas;

	[SerializeField]
	private RustText fuelBarsText;

	[SerializeField]
	private RustText speedometerText;

	[SerializeField]
	private float fuelAmountWarning;

	[SerializeField]
	private RustText batteryWarningText;

	[SerializeField]
	private float healthWarningFraction;

	[SerializeField]
	private RustText healthWarningText;

	[Header("DPV - FX")]
	[SerializeField]
	private Transform leftHandGrip;

	[SerializeField]
	private Transform rightHandGrip;

	[SerializeField]
	private GameObject lightsToggleGroup;

	[SerializeField]
	private DiverPropulsionVehicleAudio dpvAudio;

	[SerializeField]
	private ParticleSystem fxUnderWaterEngineThrustForward;

	[SerializeField]
	private ParticleSystem[] fxUnderWaterEngineThrustForwardSubs;

	[SerializeField]
	private ParticleSystem fxUnderWaterEngineThrustReverse;

	[SerializeField]
	private ParticleSystem[] fxUnderWaterEngineThrustReverseSubs;

	private float waterLevelY;

	private float waterDepthHere;

	private float ourDepthInWaterY;

	public const Flags Flag_Headlights = Flags.Reserved5;

	public const Flags Flag_Stationary = Flags.Reserved6;

	protected VehicleEngineController<DiverPropulsionVehicle> engineController;

	private float _throttle;

	private float _steer;

	private float _upDown;

	private float normalDrag;

	private float highDrag;

	private float targetClimbSpeed;

	private TimeSince timeSinceLastUsed;

	private const float DECAY_TICK_TIME = 60f;

	private float targetPitch;

	private float targetRoll;

	private BoxCollider characterBoxCollider;

	private bool IsInWater => ourDepthInWaterY > 0.1f;

	public VehicleEngineController<DiverPropulsionVehicle>.EngineState EngineState => engineController.CurEngineState;

	public bool LightsOn => HasFlag(Flags.Reserved5);

	public bool IsActive => !HasFlag(Flags.Reserved6);

	public float ThrottleInput
	{
		get
		{
			if (!engineController.IsOn)
			{
				return 0f;
			}
			return _throttle;
		}
		protected set
		{
			_throttle = Mathf.Clamp(value, -1f, 1f);
		}
	}

	public float SteerInput
	{
		get
		{
			return _steer;
		}
		set
		{
			_steer = Mathf.Clamp(value, -1f, 1f);
		}
	}

	public float UpDownInput
	{
		get
		{
			if (base.isServer)
			{
				if ((float)timeSinceLastUsed >= timeUntilAutoSurface)
				{
					return 0.15f;
				}
				if (!engineController.IsOn)
				{
					return Mathf.Max(0f, _upDown);
				}
				return _upDown;
			}
			return _upDown;
		}
		protected set
		{
			_upDown = Mathf.Clamp(value, -1f, 1f);
		}
	}

	protected override bool PositionTickFixedTime => true;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("DiverPropulsionVehicle.OnRpcMessage"))
		{
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
							RPCMessage msg2 = rPCMessage;
							RPC_OpenFuel(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_OpenFuel");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void InitShared()
	{
		base.InitShared();
		EntityFuelSystem entityFuelSystem = new EntityFuelSystem(base.isServer, fuelStoragePrefab, children);
		if (base.isServer)
		{
			StorageContainer fuelContainer = entityFuelSystem.GetFuelContainer();
			if (fuelContainer != null)
			{
				SetFuelUpdateInventoryCallback(fuelContainer);
			}
		}
		engineController = new VehicleEngineController<DiverPropulsionVehicle>(this, entityFuelSystem, base.isServer, engineStartupTime);
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.diverPropulsionVehicle != null)
		{
			engineController.FuelSystem.SetInstanceID(info.msg.diverPropulsionVehicle.fuelStorageID);
		}
	}

	public override void OnFlagsChanged(Flags old, Flags next)
	{
		base.OnFlagsChanged(old, next);
		if (old != next && base.isServer)
		{
			characterWorldCollider.SetActive(next.HasFlag(Flags.Busy));
		}
	}

	public override float WaterFactorForPlayer(BasePlayer player)
	{
		if (!WaterLevel.Test(player.eyes.position, waves: true, volumes: true))
		{
			return 0f;
		}
		return 1f;
	}

	private void UpdateWaterInfo()
	{
		GetWaterInfo(this, base.transform, out waterLevelY, out waterDepthHere);
		ourDepthInWaterY = waterLevelY - base.transform.position.y;
	}

	private static void GetWaterInfo(BaseEntity forEntity, Transform referencePoint, out float surfaceY, out float depth)
	{
		WaterLevel.WaterInfo waterInfo = WaterLevel.GetWaterInfo(referencePoint.position, waves: true, volumes: true, forEntity);
		if (waterInfo.isValid)
		{
			depth = waterInfo.overallDepth;
			surfaceY = waterInfo.surfaceLevel;
		}
		else
		{
			depth = 0f;
			surfaceY = referencePoint.position.y - 1f;
		}
	}

	private bool WaterIsDeepEnough(bool updateWaterInfo)
	{
		if (updateWaterInfo)
		{
			UpdateWaterInfo();
		}
		return waterDepthHere >= minWaterDepth;
	}

	public override void ServerInit()
	{
		base.ServerInit();
		timeSinceLastUsed = 0f;
		normalDrag = rigidBody.drag;
		highDrag = normalDrag * 2.5f;
		characterWorldCollider.SetActive(HasFlag(Flags.Busy));
		characterBoxCollider = characterWorldCollider.GetComponent<BoxCollider>();
		InvokeRandomized(SendClientUpdate, 0f, 0.15f, 0.02f);
		InvokeRandomized(DPVDecay, UnityEngine.Random.Range(30f, 60f), 60f, 6f);
	}

	private void SendClientUpdate()
	{
		if (IsMounted())
		{
			int arg = Compression.PackVector3ToInt(new Vector3(SteerInput, UpDownInput, ThrottleInput), -1f, 1f);
			ClientRPC(RpcTarget.NetworkGroup("CL_UpdateCosmetics"), arg);
		}
	}

	public override void LightToggle(BasePlayer player)
	{
		SetFlag(Flags.Reserved5, !LightsOn);
	}

	protected override void OnChildAdded(BaseEntity child)
	{
		base.OnChildAdded(child);
		if (base.isServer && isSpawned && GetFuelSystem().CheckNewChild(child))
		{
			SetFuelUpdateInventoryCallback(child as StorageContainer);
		}
	}

	private void SetFuelUpdateInventoryCallback(StorageContainer sc)
	{
		ItemContainer inventory = sc.inventory;
		inventory.onItemAddedRemoved = (Action<Item, bool>)Delegate.Combine(inventory.onItemAddedRemoved, (Action<Item, bool>)delegate
		{
			SendClientFuelInfo();
		});
	}

	private void UpdateMovementState()
	{
		SetFlag(Flags.Reserved6, rigidBody.IsSleeping() && !AnyMounted());
	}

	public override float GetNetworkTime()
	{
		return UnityEngine.Time.fixedTime;
	}

	public override void VehicleFixedUpdate()
	{
		using (TimeWarning.New("DPV.VehicleFixedUpdate"))
		{
			base.VehicleFixedUpdate();
			UpdateMovementState();
			if (!IsActive)
			{
				return;
			}
			UpdateWaterInfo();
			rigidBody.drag = (IsMounted() ? normalDrag : highDrag);
			engineController.CheckEngineState();
			if (engineController.IsOn)
			{
				float fuelPerSecond = Mathf.Lerp(idleFuelPerSec, maxFuelPerSec, Mathf.Abs(ThrottleInput));
				if (engineController.TickFuel(fuelPerSecond) > 0)
				{
					SendClientFuelInfo();
				}
			}
			if (!IsInWater)
			{
				return;
			}
			if (WaterIsDeepEnough(updateWaterInfo: false))
			{
				Vector3 localVelocity = GetLocalVelocity();
				float num = Vector3.Dot(localVelocity, base.transform.forward);
				float num2 = depthChangeTargetSpeed * UpDownInput;
				targetClimbSpeed = Mathf.MoveTowards(maxDelta: (((!(UpDownInput > 0f) || !(num2 > targetClimbSpeed) || !(targetClimbSpeed > 0f)) && (!(UpDownInput < 0f) || !(num2 < targetClimbSpeed) || !(targetClimbSpeed < 0f))) ? 4f : 0.7f) * UnityEngine.Time.fixedDeltaTime, current: targetClimbSpeed, target: num2);
				float num3 = rigidBody.velocity.y - targetClimbSpeed;
				float value = buoyancy.buoyancyScale - num3 * 50f * UnityEngine.Time.fixedDeltaTime;
				buoyancy.buoyancyScale = Mathf.Clamp(value, 0.01f, 1f);
				targetPitch = Mathf.Lerp(targetPitch, (0f - UpDownInput) * maxPitchDegrees, UnityEngine.Time.fixedDeltaTime * rotTargetChangeRate);
				targetRoll = Mathf.Lerp(targetRoll, (0f - SteerInput) * maxRollDegrees, UnityEngine.Time.fixedDeltaTime * rotTargetChangeRate);
				Vector3 right = base.transform.right;
				Vector3 forward = base.transform.forward;
				Quaternion quaternion = Quaternion.AngleAxis(targetPitch, right);
				Vector3 rhs = Quaternion.AngleAxis(targetRoll, forward) * quaternion * Vector3.up;
				Vector3 torque = Vector3.Cross(Quaternion.AngleAxis(rigidBody.angularVelocity.magnitude * 57.29578f * rotStability / rotPower, rigidBody.angularVelocity) * base.transform.up, rhs) * rotPower * rotPower;
				rigidBody.AddTorque(torque);
				rigidBody.AddForce(Vector3.up * (0f - num3) * vertStability, ForceMode.VelocityChange);
				if (IsOn())
				{
					rigidBody.AddForce(base.transform.forward * (engineKW * ThrottleInput), ForceMode.Force);
					if (Mathf.Abs(num) > 1f)
					{
						Vector3 normalized = localVelocity.normalized;
						float num4 = Mathf.Abs(Vector3.Dot(normalized, base.transform.right));
						rigidBody.AddForce(-normalized * (num4 * (0.08f * engineKW) * rigidBody.mass * rigidBody.drag));
					}
					float num5 = turnPower * rigidBody.mass * rigidBody.angularDrag;
					float num6 = Mathf.Min(Mathf.Abs(num) * 0.6f, 1f);
					float num7 = num5 * SteerInput * num6;
					if (num < -1f)
					{
						num7 *= -1f;
					}
					rigidBody.AddTorque(Vector3.up * num7, ForceMode.Force);
				}
			}
			else
			{
				DismountAllPlayers();
			}
		}
	}

	public override Vector3 GetLocalVelocityServer()
	{
		if (rigidBody == null)
		{
			return Vector3.zero;
		}
		return rigidBody.velocity;
	}

	public override void PlayerServerInput(InputState inputState, BasePlayer player)
	{
		timeSinceLastUsed = 0f;
		if (inputState.IsDown(BUTTON.FORWARD))
		{
			ThrottleInput = 1f;
		}
		else if (inputState.IsDown(BUTTON.BACKWARD))
		{
			ThrottleInput = -1f;
		}
		else
		{
			ThrottleInput = 0f;
		}
		if (inputState.IsDown(BUTTON.LEFT))
		{
			SteerInput = -1f;
		}
		else if (inputState.IsDown(BUTTON.RIGHT))
		{
			SteerInput = 1f;
		}
		else
		{
			SteerInput = 0f;
		}
		if (inputState.IsDown(BUTTON.SPRINT))
		{
			UpDownInput = 1f;
		}
		else if (inputState.IsDown(BUTTON.DUCK))
		{
			UpDownInput = -1f;
		}
		else
		{
			UpDownInput = 0f;
		}
		if (engineController.IsOff && ((inputState.IsDown(BUTTON.FORWARD) && !inputState.WasDown(BUTTON.FORWARD)) || (inputState.IsDown(BUTTON.BACKWARD) && !inputState.WasDown(BUTTON.BACKWARD))))
		{
			engineController.TryStartEngine(player);
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.diverPropulsionVehicle = Facepunch.Pool.Get<ProtoBuf.DiverPropulsionVehicle>();
		info.msg.diverPropulsionVehicle.fuelStorageID = GetFuelSystem().GetInstanceID();
		info.msg.diverPropulsionVehicle.fuelAmount = GetFuelSystem().GetFuelAmount();
		info.msg.diverPropulsionVehicle.fuelTicks = Mathf.RoundToInt(GetFuelSystem().GetFuelFraction() * 12f);
	}

	public IFuelSystem GetFuelSystem()
	{
		return engineController.FuelSystem;
	}

	public bool AdminFixUp()
	{
		if (IsDead())
		{
			return false;
		}
		GetFuelSystem()?.FillFuel();
		SetHealth(MaxHealth());
		SendNetworkUpdate();
		return true;
	}

	public void OnEngineStartFailed()
	{
		ClientRPC(RpcTarget.NetworkGroup("CL_EngineStartFailed"));
	}

	public bool MeetsEngineRequirements()
	{
		return AnyMounted();
	}

	public override bool CanBeLooted(BasePlayer player)
	{
		if (!base.CanBeLooted(player))
		{
			return false;
		}
		if (!PlayerIsMounted(player))
		{
			return !IsOn();
		}
		return true;
	}

	[RPC_Server]
	public void RPC_OpenFuel(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (CanBeLooted(player))
		{
			GetFuelSystem().LootFuel(player);
		}
	}

	public override void OnPickedUp(Item createdItem, BasePlayer player)
	{
		base.OnPickedUp(createdItem, player);
		if (GetFuelSystem().GetFuelAmount() > 0)
		{
			EntityFuelSystem entityFuelSystem = GetFuelSystem() as EntityFuelSystem;
			player.GiveItem(entityFuelSystem.GetFuelItem(), GiveItemReason.PickedUp);
		}
	}

	public override void OnPlayerMounted()
	{
		base.OnPlayerMounted();
		SendClientFuelInfo();
	}

	private void SendClientFuelInfo()
	{
		IFuelSystem fuelSystem = GetFuelSystem();
		byte arg = (byte)Mathf.RoundToInt(GetFuelSystem().GetFuelFraction() * 12f);
		ClientRPC(RpcTarget.NetworkGroup("CL_SetFuel"), (ushort)fuelSystem.GetFuelAmount(), arg);
	}

	private void DPVDecay()
	{
		BaseBoat.WaterVehicleDecay(this, 60f, timeSinceLastUsed, BaseSubmarine.outsidedecayminutes, BaseSubmarine.deepwaterdecayminutes, MotorRowboat.decaystartdelayminutes, preventDecayIndoors: true);
	}

	public override void AttemptMount(BasePlayer player, bool doMountChecks = true)
	{
		if (!WaterIsDeepEnough(updateWaterInfo: true))
		{
			ClientRPC(RpcTarget.Player("CL_TooShallowToMount", player));
			return;
		}
		List<Collider> obj = Facepunch.Pool.Get<List<Collider>>();
		characterBoxCollider.transform.GetPositionAndRotation(out var position, out var rotation);
		GamePhysics.OverlapOBB(new OBB(position + rotation * characterBoxCollider.center, characterBoxCollider.size, rotation), obj, 1218652417);
		foreach (Collider item in obj)
		{
			BaseEntity baseEntity = item.ToBaseEntity();
			if (!(baseEntity != null) || (!(baseEntity == this) && !(baseEntity == player)))
			{
				Facepunch.Pool.FreeUnmanaged(ref obj);
				ClientRPC(RpcTarget.Player("CL_MountingBlocked", player));
				return;
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		base.AttemptMount(player, doMountChecks);
	}

	void IEngineControllerUser.Invoke(Action action, float time)
	{
		Invoke(action, time);
	}

	void IEngineControllerUser.CancelInvoke(Action action)
	{
		CancelInvoke(action);
	}
}
