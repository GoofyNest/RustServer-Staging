#define UNITY_ASSERTIONS
using System;
using ConVar;
using Facepunch;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

public class Bike : GroundVehicle, VehicleChassisVisuals<Bike>.IClientWheelUser, IPrefabPreProcess, CarPhysics<Bike>.ICar, TriggerHurtNotChild.IHurtTriggerUser
{
	public enum PoweredBy
	{
		Fuel,
		Human
	}

	public static Translate.Phrase sprintPhrase = new Translate.Phrase("sprint", "Sprint");

	public static Translate.Phrase boostPhrase = new Translate.Phrase("boost", "Boost");

	[Header("Bike")]
	[SerializeField]
	private Transform centreOfMassTransform;

	[SerializeField]
	private VisualCarWheel wheelFront;

	[SerializeField]
	private VisualCarWheel wheelRear;

	[SerializeField]
	private VisualCarWheel wheelExtra;

	[SerializeField]
	private bool snowmobileDrivingStyle;

	[SerializeField]
	private CarSettings carSettings;

	[SerializeField]
	private int engineKW = 59;

	[SerializeField]
	private float idleFuelPerSec = 0.03f;

	[SerializeField]
	private float maxFuelPerSec = 0.15f;

	[SerializeField]
	[Range(0f, 1f)]
	private float pitchStabP = 0.01f;

	[SerializeField]
	[Range(0f, 1f)]
	private float pitchStabD = 0.005f;

	[SerializeField]
	[Range(0f, 1f)]
	private float twoWheelRollStabP = 100f;

	[SerializeField]
	[Range(0f, 1f)]
	private float twoWheelRollStabD = 10f;

	[SerializeField]
	[Range(1f, 500f)]
	private float manyWheelStabP = 40f;

	[SerializeField]
	[Range(1f, 100f)]
	private float manyWheelStabD = 10f;

	[SerializeField]
	[Range(0f, 1f)]
	private float airControlTorquePower = 0.04f;

	public float sprintTime = 5f;

	[SerializeField]
	private float sprintRegenTime = 10f;

	[SerializeField]
	private float sprintBoostPercent = 0.3f;

	[SerializeField]
	private ProtectionProperties riderProtection;

	[SerializeField]
	private float hurtTriggerMinSpeed = 1f;

	[SerializeField]
	private TriggerHurtNotChild hurtTriggerFront;

	[SerializeField]
	private TriggerHurtNotChild hurtTriggerRear;

	[SerializeField]
	private float maxLeanSpeed = 20f;

	[SerializeField]
	private float leftMaxLean = 60f;

	[SerializeField]
	private float rightMaxLean = 60f;

	[SerializeField]
	private float midairRotationForce = 1f;

	[SerializeField]
	private Vector3 customInertiaTensor = new Vector3(85f, 60f, 40f);

	public PoweredBy poweredBy;

	[SerializeField]
	[Range(0f, 1f)]
	private float percentFood = 0.5f;

	[SerializeField]
	private float playerDamageThreshold = 40f;

	[SerializeField]
	private float playerDeathThreshold = 75f;

	[SerializeField]
	private bool hasBell;

	[Header("Bike Visuals")]
	public float minGroundFXSpeed;

	[SerializeField]
	private BikeChassisVisuals chassisVisuals;

	[SerializeField]
	private VehicleLight[] lights;

	[SerializeField]
	private ParticleSystemContainer exhaustFX;

	[SerializeField]
	private Transform steeringLeftIK;

	[SerializeField]
	private Transform steeringRightIK;

	[SerializeField]
	private Transform steeringRightIKAcclerating;

	[SerializeField]
	private Transform leftFootIK;

	[SerializeField]
	private Transform rightFootIK;

	[SerializeField]
	private Transform passengerLeftHandIK;

	[SerializeField]
	private Transform passengerRightHandIK;

	[SerializeField]
	private Transform passengerLeftFootIK;

	[SerializeField]
	private Transform passengerRightFootIK;

	[SerializeField]
	private ParticleSystemContainer fxMediumDamage;

	[SerializeField]
	private GameObject fxMediumDamageInstLight;

	[SerializeField]
	private ParticleSystemContainer fxHeavyDamage;

	[SerializeField]
	private GameObject fxHeavyDamageInstLight;

	[Header("Sidecar")]
	[SerializeField]
	private Rigidbody sidecarRigidBody;

	[SerializeField]
	private Transform sidecarPhysicsHinge;

	[ServerVar(Help = "How long before a bike loses all its health while outside")]
	public static float outsideDecayMinutes = 1440f;

	[ServerVar(Help = "Pedal bike population active on the server (roadside spawns)", ShowInAdminUI = true)]
	public static float pedalRoadsidePopulation = 1f;

	[SerializeField]
	private Transform realSidecarCapsule;

	[ServerVar(Help = "Pedal bike population in monuments", ShowInAdminUI = true)]
	public static float pedalMonumentPopulation = 1f;

	[SerializeField]
	private Transform duplicateSidecarCapsule;

	[ServerVar(Help = "Motorbike population in monuments", ShowInAdminUI = true)]
	public static float motorbikeMonumentPopulation = 1f;

	[ServerVar(Help = "Can bike crashes cause damage or death to the rider?")]
	public static bool doPlayerDamage = true;

	private bool hasExtraWheel;

	private bool hasSidecar;

	private bool hasDamageFX;

	private float _throttle;

	private float _brake;

	public const Flags Flag_SprintInput = Flags.Reserved6;

	public const Flags Flag_DuckInput = Flags.Reserved8;

	public const Flags Flag_IsSprinting = Flags.Reserved9;

	private float _mass = -1f;

	private float cachedFuelFraction;

	private const float FORCE_MULTIPLIER = 10f;

	private float _steer;

	private CarPhysics<Bike> carPhysics;

	private VehicleTerrainHandler serverTerrainHandler;

	private CarWheel[] wheels;

	private TimeSince timeSinceLastUsed;

	private const float DECAY_TICK_TIME = 60f;

	private float prevPitchStabError;

	private float prevRollStabError;

	private float prevRollStabRoll;

	private float lastCrashDamage;

	private TimeSince timeSinceBellDing;

	private bool wasWantingSlopeSprint;

	private bool inBurnoutMode;

	private bool shouldBypassClippingChecks;

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

	public float BrakeInput
	{
		get
		{
			return _brake;
		}
		protected set
		{
			_brake = Mathf.Clamp(value, 0f, 1f);
		}
	}

	public bool IsBraking => BrakeInput > 0f;

	public bool SprintInput
	{
		get
		{
			return HasFlag(Flags.Reserved6);
		}
		private set
		{
			if (SprintInput != value)
			{
				SetFlag(Flags.Reserved6, value);
			}
		}
	}

	public bool DuckInput
	{
		get
		{
			return HasFlag(Flags.Reserved8);
		}
		private set
		{
			if (DuckInput != value)
			{
				SetFlag(Flags.Reserved8, value);
			}
		}
	}

	public bool CanSprint => poweredBy == PoweredBy.Human;

	public bool IsSprinting
	{
		get
		{
			return HasFlag(Flags.Reserved9);
		}
		private set
		{
			if (IsSprinting != value)
			{
				SetFlag(Flags.Reserved9, value);
			}
		}
	}

	public float SprintPercentRemaining { get; protected set; }

	public float SteerAngle
	{
		get
		{
			if (base.isServer)
			{
				return carPhysics.SteerAngle;
			}
			return 0f;
		}
	}

	public override float DriveWheelVelocity
	{
		get
		{
			if (base.isServer)
			{
				float num = carPhysics.DriveWheelVelocity;
				if (inBurnoutMode && ThrottleInput > 0.1f)
				{
					num += ThrottleInput * 20f;
				}
				return num;
			}
			return 0f;
		}
	}

	public float DriveWheelSlip
	{
		get
		{
			if (base.isServer)
			{
				return carPhysics.DriveWheelSlip;
			}
			return 0f;
		}
	}

	public float SidecarAngle
	{
		get
		{
			if (base.isServer)
			{
				return sidecarPhysicsHinge.localEulerAngles.z;
			}
			return 0f;
		}
	}

	public float MaxSteerAngle => carSettings.maxSteerAngle;

	private float Mass
	{
		get
		{
			if (base.isServer)
			{
				return rigidBody.mass;
			}
			return _mass;
		}
	}

	public float SteerInput
	{
		get
		{
			return _steer;
		}
		protected set
		{
			_steer = Mathf.Clamp(value, -1f, 1f);
		}
	}

	public VehicleTerrainHandler.Surface OnSurface
	{
		get
		{
			if (serverTerrainHandler == null)
			{
				return VehicleTerrainHandler.Surface.Default;
			}
			return serverTerrainHandler.OnSurface;
		}
	}

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("Bike.OnRpcMessage"))
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

	public override void PreInitShared()
	{
		hasExtraWheel = wheelExtra.wheelCollider != null;
		hasSidecar = sidecarPhysicsHinge != null;
		hasDamageFX = fxMediumDamage != null;
		base.PreInitShared();
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.bike != null)
		{
			engineController.FuelSystem.SetInstanceID(info.msg.bike.fuelStorageID);
			cachedFuelFraction = info.msg.bike.fuelFraction;
		}
	}

	public float GetMaxDriveForce()
	{
		float num = (float)engineKW * 10f * GetPerformanceFraction();
		if (IsSprinting)
		{
			num *= 1f + sprintBoostPercent;
		}
		return num;
	}

	public override float GetMaxForwardSpeed()
	{
		float num = GetMaxDriveForce() / Mass * 15f;
		if (IsSprinting)
		{
			num *= 1f + sprintBoostPercent;
		}
		return num;
	}

	public override float GetThrottleInput()
	{
		return ThrottleInput;
	}

	public override float GetBrakeInput()
	{
		return BrakeInput;
	}

	public float GetPerformanceFraction()
	{
		float t = Mathf.InverseLerp(0.25f, 0.5f, base.healthFraction);
		return Mathf.Lerp(0.5f, 1f, t);
	}

	public float GetFuelFraction()
	{
		if (base.isServer)
		{
			return Mathf.Clamp01((float)engineController.FuelSystem.GetFuelAmount() / 100f);
		}
		return cachedFuelFraction;
	}

	public override bool CanBeLooted(BasePlayer player)
	{
		if (!base.CanBeLooted(player))
		{
			return false;
		}
		if (AnyMounted())
		{
			if (PlayerIsMounted(player))
			{
				return player.modelState.poseType == 26;
			}
			return false;
		}
		return true;
	}

	protected override IFuelSystem CreateFuelSystem()
	{
		if (poweredBy == PoweredBy.Fuel)
		{
			return base.CreateFuelSystem();
		}
		return new HumanFuelSystem(base.isServer, this, percentFood);
	}

	public float GetSteerInput()
	{
		return SteerInput;
	}

	public override void ServerInit()
	{
		base.ServerInit();
		timeSinceLastUsed = 0f;
		rigidBody.centerOfMass = centreOfMassTransform.localPosition;
		rigidBody.inertiaTensor = customInertiaTensor;
		carPhysics = new CarPhysics<Bike>(this, base.transform, rigidBody, carSettings);
		serverTerrainHandler = new VehicleTerrainHandler(this);
		SprintPercentRemaining = 1f;
		InvokeRandomized(UpdateClients, 0f, 0.1f, 0.01f);
		InvokeRandomized(BikeDecay, UnityEngine.Random.Range(30f, 60f), 60f, 6f);
	}

	public override void OnCollision(Collision collision, BaseEntity hitEntity)
	{
		if (base.isServer)
		{
			ProcessCollision(collision, sidecarRigidBody);
		}
	}

	public override void VehicleFixedUpdate()
	{
		using (TimeWarning.New("Bike.VehicleFixedUpdate"))
		{
			base.VehicleFixedUpdate();
			float speed = GetSpeed();
			carPhysics.FixedUpdate(UnityEngine.Time.fixedDeltaTime, speed);
			serverTerrainHandler.FixedUpdate();
			bool flag = false;
			if (IsOn())
			{
				inBurnoutMode = false;
				float fuelPerSecond = Mathf.Lerp(idleFuelPerSec, maxFuelPerSec, Mathf.Abs(ThrottleInput));
				engineController.TickFuel(fuelPerSecond);
				if (CanSprint && carPhysics.IsGrounded() && WantsSprint(speed))
				{
					SprintPercentRemaining -= UnityEngine.Time.deltaTime / sprintTime;
					SprintPercentRemaining = Mathf.Clamp01(SprintPercentRemaining);
					flag = SprintPercentRemaining > 0f;
				}
				bool flag2 = DuckInput || (ThrottleInput > 0f && BrakeInput > 0f);
				if (poweredBy == PoweredBy.Fuel && carPhysics.IsGrounded() && flag2)
				{
					inBurnoutMode = true;
				}
			}
			engineController.CheckEngineState();
			if (CanSprint && !flag && SprintPercentRemaining < 1f)
			{
				SprintPercentRemaining += UnityEngine.Time.deltaTime / sprintRegenTime;
				SprintPercentRemaining = Mathf.Clamp01(SprintPercentRemaining);
			}
			IsSprinting = flag;
			bool num = rigidBody.IsSleeping();
			if (!num)
			{
				AwakeBikePhysicsTick(speed);
			}
			RigidbodyConstraints rigidbodyConstraints = (num ? RigidbodyConstraints.FreezeRotationZ : RigidbodyConstraints.None);
			if (rigidBody.constraints != rigidbodyConstraints)
			{
				rigidBody.constraints = rigidbodyConstraints;
				if (rigidBody.constraints == RigidbodyConstraints.None)
				{
					rigidBody.inertiaTensor = customInertiaTensor;
				}
			}
			hurtTriggerFront.gameObject.SetActive(speed > hurtTriggerMinSpeed);
			hurtTriggerRear.gameObject.SetActive(speed < 0f - hurtTriggerMinSpeed);
			if (!hasSidecar)
			{
				return;
			}
			if (rigidBody.isKinematic != sidecarRigidBody.isKinematic)
			{
				sidecarRigidBody.isKinematic = rigidBody.isKinematic;
			}
			if (rigidBody.IsSleeping() != sidecarRigidBody.IsSleeping())
			{
				if (rigidBody.IsSleeping())
				{
					sidecarRigidBody.Sleep();
				}
				else
				{
					sidecarRigidBody.WakeUp();
				}
			}
		}
	}

	protected virtual void AwakeBikePhysicsTick(float speed)
	{
		if (rigidBody.isKinematic)
		{
			return;
		}
		bool num = carPhysics.IsGrounded();
		if (snowmobileDrivingStyle)
		{
			if (!carPhysics.IsGrounded())
			{
				StabiliseSnowmobileStyle();
				PDPitchStab();
			}
		}
		else
		{
			PDPitchStab();
			PDDirectionStab();
			PDRollStab(speed);
		}
		float num2 = 0f;
		if (!num)
		{
			if (SprintInput && !DuckInput)
			{
				num2 = 0f - airControlTorquePower;
			}
			else if (DuckInput && !SprintInput)
			{
				num2 = airControlTorquePower;
			}
		}
		if (num2 != 0f)
		{
			rigidBody.AddRelativeTorque(num2, 0f, 0f, ForceMode.VelocityChange);
		}
		if (hasSidecar)
		{
			duplicateSidecarCapsule.SetPositionAndRotation(realSidecarCapsule.position, realSidecarCapsule.rotation);
		}
	}

	private void PDPitchStab()
	{
		float num = base.transform.localEulerAngles.x;
		if (num > 180f)
		{
			num -= 360f;
		}
		float num2 = 0f - num;
		float num3 = num2;
		float num4 = (num2 - prevPitchStabError) / UnityEngine.Time.fixedDeltaTime;
		float x = pitchStabP * num3 + pitchStabD * num4;
		rigidBody.AddRelativeTorque(x, 0f, 0f, ForceMode.VelocityChange);
		prevPitchStabError = num2;
	}

	private void PDDirectionStab()
	{
		Vector3 angularVelocity = rigidBody.angularVelocity;
		float num = (carPhysics.IsGrounded() ? (0.05f + Mathf.Abs(SteerAngle) * 0.15f) : 0.05f);
		angularVelocity.y = Mathf.Clamp(angularVelocity.y, 0f - num, num);
		rigidBody.angularVelocity = angularVelocity;
	}

	private void PDRollStab(float speed)
	{
		float num = ((speed >= 0f) ? speed : ((0f - speed) * 0.33f));
		float num2 = 0f - SteerAngle / MaxSteerAngle * Mathf.Clamp01(num / maxLeanSpeed);
		num2 = ((!(num2 < 0f)) ? (num2 * leftMaxLean) : (num2 * rightMaxLean));
		float num3 = base.transform.localEulerAngles.z;
		if (num3 > 180f)
		{
			num3 -= 360f;
		}
		float num4 = num2 - num3;
		float num5 = num4;
		float num6 = 0f - AngleDifference(num3, prevRollStabRoll) / UnityEngine.Time.fixedDeltaTime;
		float z = twoWheelRollStabP * num5 + twoWheelRollStabD * num6;
		rigidBody.AddRelativeTorque(0f, 0f, z, ForceMode.VelocityChange);
		prevRollStabError = num4;
		prevRollStabRoll = num3;
	}

	private float AngleDifference(float a, float b)
	{
		return (a - b + 540f) % 360f - 180f;
	}

	private void StabiliseSnowmobileStyle()
	{
		if (UnityEngine.Physics.Raycast(base.transform.position, Vector3.down, out var hitInfo, 10f, 1218511105, QueryTriggerInteraction.Ignore))
		{
			Vector3 normal = hitInfo.normal;
			Vector3 right = base.transform.right;
			right.y = 0f;
			normal = Vector3.ProjectOnPlane(normal, right);
			float num = Vector3.Angle(normal, Vector3.up);
			float angle = rigidBody.angularVelocity.magnitude * 57.29578f * manyWheelStabD / manyWheelStabP;
			if (num <= 45f)
			{
				Vector3 direction = Vector3.Cross(Quaternion.AngleAxis(angle, rigidBody.angularVelocity) * base.transform.up, normal) * manyWheelStabP * manyWheelStabP;
				Vector3 torque = rigidBody.transform.InverseTransformDirection(direction);
				rigidBody.AddRelativeTorque(torque);
			}
		}
	}

	public override void PlayerServerInput(InputState inputState, BasePlayer player)
	{
		if (!IsDriver(player))
		{
			return;
		}
		timeSinceLastUsed = 0f;
		if (inputState.IsDown(BUTTON.FIRE_THIRD))
		{
			SteerInput += inputState.MouseDelta().x * 0.1f;
		}
		else
		{
			SteerInput = 0f;
			if (inputState.IsDown(BUTTON.LEFT))
			{
				SteerInput = -1f;
			}
			else if (inputState.IsDown(BUTTON.RIGHT))
			{
				SteerInput = 1f;
			}
		}
		bool flag = inputState.IsDown(BUTTON.FORWARD);
		bool flag2 = inputState.IsDown(BUTTON.BACKWARD);
		BrakeInput = 0f;
		if (GetSpeed() > 3f)
		{
			ThrottleInput = (flag ? 1f : 0f);
			BrakeInput = (flag2 ? 1f : 0f);
		}
		else
		{
			ThrottleInput = (flag ? 1f : (flag2 ? (-1f) : 0f));
		}
		SprintInput = inputState.IsDown(BUTTON.SPRINT);
		DuckInput = inputState.IsDown(BUTTON.DUCK);
		if (engineController.IsOff && ((inputState.IsDown(BUTTON.FORWARD) && !inputState.WasDown(BUTTON.FORWARD)) || (inputState.IsDown(BUTTON.BACKWARD) && !inputState.WasDown(BUTTON.BACKWARD))))
		{
			engineController.TryStartEngine(player);
		}
		if (hasBell && inputState.IsDown(BUTTON.FIRE_PRIMARY) && !inputState.WasDown(BUTTON.FIRE_PRIMARY) && (float)timeSinceBellDing > 1f)
		{
			ClientRPC(RpcTarget.NetworkGroup("RingBell"));
			timeSinceBellDing = 0f;
		}
	}

	public float GetAdjustedDriveForce(float absSpeed, float topSpeed)
	{
		float maxDriveForce = GetMaxDriveForce();
		float num = MathEx.BiasedLerp(bias: Mathf.Lerp(0.3f, 0.75f, GetPerformanceFraction()), x: 1f - absSpeed / topSpeed);
		return maxDriveForce * num;
	}

	public bool GetSteerSpeedMod(float speed)
	{
		return inBurnoutMode;
	}

	public virtual float GetSteerMaxMult(float speed)
	{
		if (speed < 0f)
		{
			return 0.5f;
		}
		if (!inBurnoutMode)
		{
			return 1f;
		}
		return 1.35f;
	}

	public override float MaxVelocity()
	{
		return Mathf.Max(GetMaxForwardSpeed() * 1.3f, 30f);
	}

	public CarWheel[] GetWheels()
	{
		if (wheels == null)
		{
			if (hasExtraWheel)
			{
				wheels = new CarWheel[3] { wheelFront, wheelRear, wheelExtra };
			}
			else
			{
				wheels = new CarWheel[2] { wheelFront, wheelRear };
			}
		}
		return wheels;
	}

	public float GetWheelsMidPos()
	{
		return (wheelFront.wheelCollider.transform.localPosition.z - wheelRear.wheelCollider.transform.localPosition.z) * 0.5f;
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.bike = Facepunch.Pool.Get<ProtoBuf.Bike>();
		info.msg.bike.steerInput = SteerAngle;
		info.msg.bike.driveWheelVel = DriveWheelVelocity;
		info.msg.bike.throttleInput = ThrottleInput;
		info.msg.bike.brakeInput = BrakeInput;
		info.msg.bike.fuelStorageID = GetFuelSystem().GetInstanceID();
		info.msg.bike.fuelFraction = GetFuelFraction();
		if (hasSidecar)
		{
			info.msg.bike.sidecarAngle = SidecarAngle;
			info.msg.bike.time = GetNetworkTime();
		}
	}

	public override void OnParentChanging(BaseEntity oldParent, BaseEntity newParent)
	{
		base.OnParentChanging(oldParent, newParent);
		shouldBypassClippingChecks = false;
		if (newParent != null && HasDriver() && newParent.GetComponentInChildren<TriggerParentEnclosed>() != null)
		{
			shouldBypassClippingChecks = true;
		}
	}

	public override void SeatClippedWorld(BaseMountable mountable)
	{
		if (!shouldBypassClippingChecks)
		{
			base.SeatClippedWorld(mountable);
		}
	}

	protected override void DoCollisionDamage(BaseEntity hitEntity, float damage)
	{
		lastCrashDamage = damage;
		if (doPlayerDamage && damage > playerDamageThreshold)
		{
			float num = ((damage > playerDeathThreshold) ? 9999f : ((damage - playerDamageThreshold) / 2f));
			float num2 = ((damage > playerDeathThreshold) ? 9999f : (num * 0.5f));
			foreach (MountPointInfo mountPoint in mountPoints)
			{
				if (mountPoint.mountable != null)
				{
					BasePlayer mounted = mountPoint.mountable.GetMounted();
					if (mounted != null)
					{
						float amount = (mountPoint.isDriver ? num : num2);
						mounted.Hurt(amount, DamageType.Collision, this, useProtection: false);
					}
				}
			}
		}
		base.DoCollisionDamage(hitEntity, damage);
	}

	public override Vector3 GetMountRagdollVelocity(BasePlayer player)
	{
		float num = Mathf.Clamp(lastCrashDamage, 0f, 75f);
		return base.transform.forward * num * 0.5f;
	}

	public override int StartingFuelUnits()
	{
		return 0;
	}

	public override bool MeetsEngineRequirements()
	{
		return HasDriver();
	}

	public void BikeDecay()
	{
		if (!IsDead() && !((float)timeSinceLastUsed < 2700f))
		{
			float num = (IsOutside() ? outsideDecayMinutes : float.PositiveInfinity);
			if (!float.IsPositiveInfinity(num))
			{
				float num2 = 1f / num;
				Hurt(MaxHealth() * num2, DamageType.Decay, this, useProtection: false);
			}
		}
	}

	public override float GetModifiedDrag()
	{
		float num = base.GetModifiedDrag();
		if (!IsOn() && !HasDriver())
		{
			num = Mathf.Max(num, 0.5f);
		}
		return num;
	}

	private void UpdateClients()
	{
		if (HasDriver())
		{
			byte num = (byte)((ThrottleInput + 1f) * 7f);
			byte b = (byte)(BrakeInput * 15f);
			byte throttleAndBrake = (byte)(num + (b << 4));
			SendClientRPC(throttleAndBrake);
		}
	}

	public virtual void SendClientRPC(byte throttleAndBrake)
	{
		if (hasSidecar)
		{
			ClientRPC(RpcTarget.NetworkGroup("BikeUpdateSC"), GetNetworkTime(), SteerAngle, throttleAndBrake, DriveWheelVelocity, GetFuelFraction(), SidecarAngle);
		}
		else if (CanSprint)
		{
			ClientRPC(RpcTarget.NetworkGroup("BikeUpdateSP"), GetNetworkTime(), SteerAngle, throttleAndBrake, DriveWheelVelocity, GetFuelFraction(), SprintPercentRemaining);
		}
		else
		{
			ClientRPC(RpcTarget.NetworkGroup("BikeUpdate"), GetNetworkTime(), SteerAngle, throttleAndBrake, DriveWheelVelocity, GetFuelFraction());
		}
	}

	public override void OnEngineStartFailed()
	{
		ClientRPC(RpcTarget.NetworkGroup("EngineStartFailed"));
	}

	public override void ScaleDamageForPlayer(BasePlayer player, HitInfo info)
	{
		base.ScaleDamageForPlayer(player, info);
		if (info.UseProtection)
		{
			riderProtection.Scale(info.damageTypes);
		}
	}

	private bool WantsSprint(float speed)
	{
		if (SprintInput)
		{
			return true;
		}
		if (speed > 5f || ThrottleInput <= 0.5f || BrakeInput > 0f)
		{
			return false;
		}
		float num = base.transform.localEulerAngles.x;
		if (num > 180f)
		{
			num -= 360f;
		}
		return wasWantingSlopeSprint = (wasWantingSlopeSprint ? (num <= -18f) : (num <= -23f));
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
}
