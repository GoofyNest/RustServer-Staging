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

public class TravellingVendor : BaseEntity, VehicleChassisVisuals<TravellingVendor>.IClientWheelUser, IPathListener
{
	private enum TravellingVendorState
	{
		Stopped,
		Travelling,
		Waiting,
		Aligning
	}

	public static class TravellingVendorFlags
	{
		public const Flags Braking = Flags.Reserved1;

		public const Flags IndicateLeft = Flags.Reserved2;

		public const Flags IndicateRight = Flags.Reserved4;

		public const Flags Lights = Flags.Reserved5;

		public const Flags Hazards = Flags.Reserved6;
	}

	[Flags]
	private enum WheelIsGroundedFlags
	{
		RearLeft = 1,
		RearRight = 2,
		FrontLeft = 4,
		FrontRight = 8
	}

	[Serializable]
	private struct VendorTargetInfo
	{
		public float lastSeenTime;

		public float lastBlockingTime;

		public float blockingAccumulator;

		public float ignoredUntil;

		public bool IsIgnored => ignoredUntil > UnityEngine.Time.time;
	}

	[Header("Visuals")]
	public TravellingVendorVisuals visuals;

	[Header("Sounds")]
	public TravellingVendorSounds sounds;

	public SoundPlayer BuySound;

	[Header("References")]
	[SerializeField]
	private VisualCarWheel wheelFL;

	[SerializeField]
	private VisualCarWheel wheelFR;

	[SerializeField]
	private VisualCarWheel wheelRL;

	[SerializeField]
	private VisualCarWheel wheelRR;

	public float client_steering_left;

	public float client_steering_right;

	public Vector3 client_velocity = Vector3.zero;

	private WheelIsGroundedFlags client_wheel_flags;

	public TimeSince timeSinceLastUpdate;

	public VehicleLight headlight;

	public VehicleLight rearLights;

	public VehicleLight rearLeftIndicator;

	public VehicleLight rearRightIndicator;

	private static Collider[] spawncheckColliders = new Collider[2];

	private const string prefabPath = "assets/prefabs/npc/travelling vendor/travellingvendor.prefab";

	[Header("General")]
	public bool DoAI = true;

	public float ObstacleCheckTime = 0.33f;

	public float MarkerUpdateTime = 0.05f;

	public float TimeBetweenPullovers = 120f;

	[Header("Engine Config")]
	public float motorForceConstant = 300f;

	public float brakeForceConstant = 500f;

	public float acceleration = 2f;

	[Header("Steer Config")]
	public float wheelbase = 3.3f;

	public float rearTrack = 1.6f;

	public float steeringSmoothing = 0.1f;

	public float downforceCoefficient = 10f;

	public float maxSteerAngle = 80f;

	[Header("Trade")]
	public GameObjectRef vendingMachineRef;

	public GameObjectRef vendingMachineFrontRef;

	[Header("Pullover")]
	public float maxPulloverAngleDifference = 15f;

	[Header("Other")]
	public static int obstacleMask = 196608;

	[Header("References")]
	public GameObjectRef mapMarkerEntityPrefab;

	public GameObjectRef preventBuildingPrefab;

	public GameObjectRef backfireEffect;

	public Transform backfirePosition;

	private TriggerVehiclePush pusher;

	private TriggerPlayerForce forcer;

	private NPCVendingMachine vendingMachine;

	[Header("Spline")]
	public float splineMovementSpeed = 2f;

	public Vector3 splineOffset;

	[ServerVar]
	public static bool should_spawn = true;

	[ServerVar]
	public static bool attempt_pullovers = true;

	[ServerVar]
	public static float alive_time_seconds = 1800f;

	[ServerVar]
	public static bool should_destroy_buildings = false;

	[ReplicatedVar(Saved = true)]
	public static float max_speed = 5f;

	private float smoothedSteering;

	private float brakes;

	private float throttle;

	private float targetThrottle = 3f;

	private bool handbrake = true;

	private float steeringAngle;

	private float currentMaxSpeed;

	private Rigidbody myRigidbody;

	private List<RaycastHit> obstacleHits;

	private List<RaycastHit> pulloverHits;

	private Vector3 destination;

	private bool instantLeave;

	private float waitTimeAccumulator;

	private float aliveTimer;

	private TimeSince timeSinceBackfire;

	private bool pullingOver;

	private Vector3 pulloverPosition = Vector3.zero;

	private float pullOverTimer;

	private Vector3 pulloverTangent = Vector3.zero;

	private bool overrideSteering;

	private BaseEntity preventBuildingInstance;

	private RaycastHit hit;

	private TravellingVendorState internalState;

	private WheelIsGroundedFlags wheelFlags;

	private SimpleSplineTranslator splineTranslator;

	private MapMarker mapMarkerInstance;

	private bool globaIndicatorLeft;

	private TimeSince timeSincePlayerDetected;

	private float slowdownStartSpeed;

	private List<Vector3> currentPath;

	private int currentPathIndex;

	private float atDestinationDistance = 8f;

	private bool followingSpine;

	private int splineId = -1;

	private WorldSpline spline;

	private ListDictionary<BasePlayer, VendorTargetInfo> playerRecords;

	private List<BasePlayer> localPlayers;

	private int searchRange = 10;

	private float allowedVendorBlockTime = 1f;

	public Vector3 Velocity => client_velocity;

	public float DriveWheelVelocity => client_velocity.magnitude;

	public float SteerAngle => (client_steering_left + client_steering_right) / 2f;

	public float MaxSteerAngle => maxSteerAngle;

	protected override bool PositionTickFixedTime => true;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("TravellingVendor.OnRpcMessage"))
		{
			if (rpc == 831304742 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SV_OpenMenu ");
				}
				using (TimeWarning.New("SV_OpenMenu"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(831304742u, "SV_OpenMenu", this, player, 3f))
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
							SV_OpenMenu(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in SV_OpenMenu");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public float GetThrottleInput()
	{
		return 1f;
	}

	[ServerVar(Name = "spawn")]
	public static string svspawntravellingvendor(ConsoleSystem.Arg args)
	{
		if (!(SpawnTravellingVendor(args.Player().transform.position) != null))
		{
			return "Failed to spawn Travelling Vendor. Is there a ring road present?";
		}
		return "Spawned Travelling Vendor.";
	}

	[ServerVar(Name = "startevent")]
	public static string svspawntravellingvendorevent(ConsoleSystem.Arg args)
	{
		if (!(SpawnTravellingVendorForEvent() != null))
		{
			return "Failed to spawn Travelling Vendor.";
		}
		return "Spawned Travelling Vendor.";
	}

	public static TravellingVendor SpawnTravellingVendor(Vector3 position)
	{
		RuntimePath runtimePath = new RuntimePath();
		PathList pathList = null;
		float num = float.PositiveInfinity;
		foreach (PathList mainRoad in TerrainMeta.Path.MainRoads)
		{
			_ = Vector3.zero;
			float num2 = float.PositiveInfinity;
			Vector3[] points = mainRoad.Path.Points;
			foreach (Vector3 a in points)
			{
				float num3 = Vector3.Distance(a, position);
				if (num3 < num2)
				{
					num2 = num3;
				}
			}
			if (num2 < num)
			{
				pathList = mainRoad;
				num = num2;
			}
		}
		if (pathList == null)
		{
			Debug.Log("Couldn't find road to spawn on.");
			return null;
		}
		Vector3 startPoint = pathList.Path.GetStartPoint();
		pathList.Path.GetEndPoint();
		int num4 = pathList.Path.Points.Length - 1;
		IAIPathNode[] nodes = new RuntimePathNode[num4];
		runtimePath.Nodes = nodes;
		IAIPathNode iAIPathNode = null;
		int num5 = 0;
		int num6 = pathList.Path.MaxIndex - 1;
		for (int j = pathList.Path.MinIndex; j <= num6; j++)
		{
			IAIPathNode iAIPathNode2 = new RuntimePathNode(pathList.Path.Points[j] + Vector3.up * 1f);
			if (iAIPathNode != null)
			{
				iAIPathNode2.AddLink(iAIPathNode);
				iAIPathNode.AddLink(iAIPathNode2);
			}
			runtimePath.Nodes[num5] = iAIPathNode2;
			iAIPathNode = iAIPathNode2;
			num5++;
		}
		runtimePath.Nodes[0].AddLink(runtimePath.Nodes[runtimePath.Nodes.Length - 1]);
		runtimePath.Nodes[runtimePath.Nodes.Length - 1].AddLink(runtimePath.Nodes[0]);
		int value = Mathf.CeilToInt(pathList.Path.Length / 500f);
		value = Mathf.Clamp(value, 1, 3);
		value++;
		for (int k = 0; k < value; k++)
		{
			int num7 = UnityEngine.Random.Range(0, pathList.Path.Points.Length);
			RuntimeInterestNode interestNode = new RuntimeInterestNode(pathList.Path.Points[num7] + Vector3.up * 1f);
			runtimePath.AddInterestNode(interestNode);
		}
		Vector3 normalized = (runtimePath.Nodes[1].Position - startPoint).normalized;
		BaseEntity baseEntity = GameManager.server.CreateEntity("assets/prefabs/npc/travelling vendor/travellingvendor.prefab", startPoint + Vector3.up * 2f, Quaternion.LookRotation(normalized));
		TravellingVendor travellingVendor = null;
		if ((bool)baseEntity)
		{
			travellingVendor = baseEntity.GetComponent<TravellingVendor>();
			if ((bool)travellingVendor)
			{
				travellingVendor.Spawn();
				travellingVendor.InstallPath(runtimePath, 1);
			}
			else
			{
				baseEntity.Kill();
			}
		}
		return travellingVendor;
	}

	private static (bool Valid, int Index) GetSpawnPoint(Vector3[] points)
	{
		int num = UnityEngine.Random.Range(0, points.Length);
		for (int i = 0; i < 15; i++)
		{
			if (CheckSpawnPosition(points[num]))
			{
				return (Valid: true, Index: num);
			}
			num = UnityEngine.Random.Range(0, points.Length);
		}
		Debug.Log("Failed to spawn a travelling vendor after " + 15 + " attempts.");
		return (Valid: false, Index: 0);
	}

	public static TravellingVendor SpawnTravellingVendorForEvent()
	{
		RuntimePath runtimePath = new RuntimePath();
		PathList pathList = null;
		if (TerrainMeta.Path.MainRoads.Count == 0)
		{
			Debug.Log("Can't spawn Travelling Vendor: No roads available to spawn on.");
			return null;
		}
		foreach (PathList mainRoad in TerrainMeta.Path.MainRoads)
		{
			if (mainRoad.Path.GetStartPoint() == mainRoad.Path.GetEndPoint())
			{
				pathList = mainRoad;
				break;
			}
		}
		if (pathList == null)
		{
			Debug.Log("Can't spawn Travelling Vendor: can't find Ring Road.");
			return null;
		}
		if (pathList.Path.Points.Length == 0)
		{
			Debug.Log("Can't spawn Travelling Vendor: Road has no points.");
			return null;
		}
		int num = pathList.Path.Points.Length - 1;
		IAIPathNode[] nodes = new RuntimePathNode[num];
		runtimePath.Nodes = nodes;
		IAIPathNode iAIPathNode = null;
		int num2 = 0;
		int num3 = pathList.Path.MaxIndex - 1;
		for (int i = pathList.Path.MinIndex; i <= num3; i++)
		{
			IAIPathNode iAIPathNode2 = new RuntimePathNode(pathList.Path.Points[i] + Vector3.up * 1f);
			if (iAIPathNode != null)
			{
				iAIPathNode2.AddLink(iAIPathNode);
				iAIPathNode.AddLink(iAIPathNode2);
			}
			runtimePath.Nodes[num2] = iAIPathNode2;
			iAIPathNode = iAIPathNode2;
			num2++;
		}
		runtimePath.Nodes[0].AddLink(runtimePath.Nodes[runtimePath.Nodes.Length - 1]);
		runtimePath.Nodes[runtimePath.Nodes.Length - 1].AddLink(runtimePath.Nodes[0]);
		int value = Mathf.CeilToInt(pathList.Path.Length / 500f);
		value = Mathf.Clamp(value, 1, 3);
		value++;
		for (int j = 0; j < value; j++)
		{
			int num4 = UnityEngine.Random.Range(0, pathList.Path.Points.Length);
			RuntimeInterestNode interestNode = new RuntimeInterestNode(pathList.Path.Points[num4] + Vector3.up * 1f);
			runtimePath.AddInterestNode(interestNode);
		}
		(bool, int) spawnPoint = GetSpawnPoint(pathList.Path.Points);
		if (spawnPoint.Item1)
		{
			int item = spawnPoint.Item2;
			Vector3 normalized = (pathList.Path.Points[(item + 1) % pathList.Path.Points.Length] - pathList.Path.Points[item]).normalized;
			BaseEntity baseEntity = GameManager.server.CreateEntity("assets/prefabs/npc/travelling vendor/travellingvendor.prefab", pathList.Path.Points[item] + Vector3.up * 2f, Quaternion.LookRotation(normalized));
			TravellingVendor travellingVendor = null;
			if ((bool)baseEntity)
			{
				travellingVendor = baseEntity.GetComponent<TravellingVendor>();
				if ((bool)travellingVendor)
				{
					travellingVendor.Spawn();
					travellingVendor.InstallPath(runtimePath, (item + 1) % pathList.Path.Points.Length);
				}
				else
				{
					baseEntity.Kill();
				}
			}
			return travellingVendor;
		}
		return null;
	}

	private static bool CheckSpawnPosition(Vector3 testPosition)
	{
		if (TerrainMeta.TopologyMap.GetTopology(testPosition, 1024))
		{
			return false;
		}
		int num = UnityEngine.Physics.OverlapSphereNonAlloc(testPosition, 0.3f, spawncheckColliders, obstacleMask | 0x8000000);
		_ = 0;
		return num == 0;
	}

	protected override void OnChildAdded(BaseEntity child)
	{
		base.OnChildAdded(child);
		if (child.prefabID == vendingMachineRef.GetEntity().prefabID && !Rust.Application.isLoadingSave)
		{
			vendingMachine = child as NPCVendingMachine;
			if (base.isServer && vendingMachine != null)
			{
				vendingMachine.SetFlag(Flags.Reserved4, b: false);
				vendingMachine.UpdateMapMarker();
				vendingMachine.ChangeRefillTime(alive_time_seconds * 0.334f);
			}
		}
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void SV_OpenMenu(RPCMessage msg)
	{
		if (vendingMachine == null)
		{
			vendingMachine = GetComponentInChildren<NPCVendingMachine>();
		}
		vendingMachine.OpenShop(msg.player);
	}

	public override float GetNetworkTime()
	{
		return UnityEngine.Time.fixedTime;
	}

	public override bool IsDebugging()
	{
		return false;
	}

	public override void OnAttacked(HitInfo info)
	{
		base.OnAttacked(info);
		BaseCombatEntity baseCombatEntity = info.Initiator as BaseCombatEntity;
		if (baseCombatEntity != null)
		{
			baseCombatEntity.MarkHostileFor();
		}
	}

	public void CreateMapMarker()
	{
		if (mapMarkerInstance != null)
		{
			mapMarkerInstance.Kill();
		}
		BaseEntity baseEntity = GameManager.server.CreateEntity(mapMarkerEntityPrefab.resourcePath, Vector3.zero, Quaternion.identity);
		baseEntity.Spawn();
		mapMarkerInstance = baseEntity as MapMarker;
	}

	public void CreatePreventBuilding()
	{
		if (preventBuildingInstance != null)
		{
			preventBuildingInstance.Kill();
		}
		BaseEntity baseEntity = GameManager.server.CreateEntity(preventBuildingPrefab.resourcePath, Vector3.zero, Quaternion.identity);
		baseEntity.Spawn();
		baseEntity.SetParent(this);
		preventBuildingInstance = baseEntity;
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		UpdateWheelFlags();
		info.msg.travellingVendor = Facepunch.Pool.Get<ProtoBuf.TravellingVendor>();
		info.msg.travellingVendor.steeringAngle = wheelFL.wheelCollider.steerAngle;
		info.msg.travellingVendor.velocity = (IsFollowingSpline() ? (base.transform.forward * splineTranslator.Speed) : myRigidbody.velocity);
		info.msg.travellingVendor.wheelFlags = (int)wheelFlags;
	}

	public override void ServerInit()
	{
		base.ServerInit();
		if (!base.isClient)
		{
			myRigidbody = GetComponent<Rigidbody>();
			obstacleHits = Facepunch.Pool.Get<List<RaycastHit>>();
			pulloverHits = Facepunch.Pool.Get<List<RaycastHit>>();
			currentMaxSpeed = max_speed;
			timeSinceBackfire = 0f;
			SetFlag(Flags.On, b: true);
			SetFlag(Flags.Reserved1, b: false);
			SetFlag(Flags.Reserved5, b: false);
			NightCheck();
			pusher = GetComponentInChildren<TriggerVehiclePush>();
			forcer = GetComponentInChildren<TriggerPlayerForce>();
			CreateMapMarker();
			CreatePreventBuilding();
			InvokeRepeating(UpdateObstacles, 0f, ObstacleCheckTime);
			InvokeRepeating(UpdateMarker, 0f, MarkerUpdateTime);
			InvokeRepeating(BuildingCheck, 1f, 3f);
			InvokeRepeating(NightCheck, 0f, 120f);
		}
	}

	internal override void DoServerDestroy()
	{
		base.DoServerDestroy();
		if (preventBuildingInstance != null && preventBuildingInstance.IsValid())
		{
			preventBuildingInstance.Kill();
		}
		if (mapMarkerInstance != null && mapMarkerInstance.IsValid())
		{
			mapMarkerInstance.Kill();
		}
		if (localPlayers != null)
		{
			Facepunch.Pool.FreeUnmanaged(ref localPlayers);
		}
		if (obstacleHits != null)
		{
			Facepunch.Pool.FreeUnmanaged(ref obstacleHits);
		}
		if (pulloverHits != null)
		{
			Facepunch.Pool.FreeUnmanaged(ref pulloverHits);
		}
		if (currentPath != null)
		{
			Facepunch.Pool.FreeUnmanaged(ref currentPath);
		}
	}

	private void StartBackfire()
	{
		Effect.server.Run(backfireEffect.resourcePath, this, 0u, backfirePosition.localPosition, Vector3.zero);
	}

	private void StartHorn()
	{
		ClientRPC(RpcTarget.NetworkGroup("CL_PlayerDetected"));
	}

	private void FixedUpdate()
	{
		if (!base.isClient && DoAI && HasPath())
		{
			ProcessLifetime();
			ProcessHandbrake();
			if (!IsFollowingSpline())
			{
				DoSteering();
				ApplyDownforce();
			}
			ProcessState();
			FetchTargets();
			SendNetworkUpdate();
		}
	}

	private void ProcessHandbrake()
	{
		if (handbrake && !(aliveTimer <= 5f) && wheelFL.wheelCollider.isGrounded && wheelFR.wheelCollider.isGrounded)
		{
			handbrake = false;
			wheelFL.wheelCollider.brakeTorque = 0f;
			wheelFR.wheelCollider.brakeTorque = 0f;
			wheelRL.wheelCollider.brakeTorque = 0f;
			wheelRR.wheelCollider.brakeTorque = 0f;
		}
	}

	private void SetGlobalIndicator()
	{
		if (HasFlag(Flags.Reserved6))
		{
			SetFlag(Flags.Reserved6, b: false);
		}
		if (globaIndicatorLeft)
		{
			SetFlag(Flags.Reserved2, b: true);
		}
		else
		{
			SetFlag(Flags.Reserved4, b: true);
		}
	}

	private void TurnOffIndicators()
	{
		if (HasFlag(Flags.Reserved2) || HasFlag(Flags.Reserved4))
		{
			SetFlag(Flags.Reserved2, b: false);
			SetFlag(Flags.Reserved4, b: false);
		}
	}

	private void UpdateMarker()
	{
		if (!(mapMarkerInstance == null))
		{
			mapMarkerInstance.transform.SetPositionAndRotation(base.transform.position, base.transform.rotation);
			mapMarkerInstance.SendNetworkUpdate();
		}
	}

	private void NightCheck()
	{
		bool flag = TOD_Sky.Instance != null && (TOD_Sky.Instance.Cycle.Hour > 19f || TOD_Sky.Instance.Cycle.Hour < 8f);
		if (HasFlag(Flags.Reserved5) != flag)
		{
			SetFlag(Flags.Reserved5, flag);
		}
	}

	private void ProcessLifetime()
	{
		aliveTimer += UnityEngine.Time.deltaTime;
		if (!(aliveTimer >= alive_time_seconds))
		{
			return;
		}
		if (localPlayers.Count > 0)
		{
			aliveTimer += 120f;
			return;
		}
		if (mapMarkerInstance != null)
		{
			mapMarkerInstance.Kill();
		}
		if (preventBuildingInstance != null)
		{
			preventBuildingInstance.Kill();
		}
		TravellingVendorEvent.currentVendor = null;
		Kill();
	}

	private void ProcessState()
	{
		if (!HasPath())
		{
			return;
		}
		if (internalState == TravellingVendorState.Stopped)
		{
			targetThrottle = 0f;
			if (HasPath())
			{
				internalState = TravellingVendorState.Travelling;
			}
		}
		if (internalState == TravellingVendorState.Travelling)
		{
			targetThrottle = 2f;
			if (instantLeave)
			{
				instantLeave = false;
			}
			if (overrideSteering)
			{
				overrideSteering = false;
			}
			if (!IsFollowingSpline())
			{
				if (!pullingOver)
				{
					AdvancePath();
					pullOverTimer += UnityEngine.Time.deltaTime;
					if (pullOverTimer > TimeBetweenPullovers && attempt_pullovers)
					{
						pullingOver = true;
					}
				}
				else
				{
					HandlePullover();
				}
			}
			if (CheckForObstacle())
			{
				instantLeave = true;
				SetWaiting();
				return;
			}
			if (!IsFollowingSpline())
			{
				ApplyForceAtWheels();
			}
			else
			{
				TravelOnSpline();
			}
			if (IsValidPatrons())
			{
				if (pulloverPosition != Vector3.zero)
				{
					return;
				}
				SetWaiting();
			}
		}
		if (internalState == TravellingVendorState.Aligning)
		{
			targetThrottle = 0.2f;
			Vector3 normalized = (currentPath[GetPathIndexAhead(3)] - currentPath[GetPathIndexAhead(2)]).normalized;
			float steerAngle = ((Vector3.Dot(base.transform.right, normalized) <= 0f) ? (0f - MaxSteerAngle) : MaxSteerAngle);
			if (Vector3.Angle(base.transform.forward, pulloverTangent) > 5f)
			{
				wheelFL.wheelCollider.steerAngle = steerAngle;
				wheelFR.wheelCollider.steerAngle = steerAngle;
				ApplyForceAtWheels();
			}
			else
			{
				overrideSteering = false;
				SetPulloverWaiting();
			}
		}
		if (internalState != TravellingVendorState.Waiting)
		{
			return;
		}
		targetThrottle = 0f;
		if (!IsFollowingSpline())
		{
			ApplyBrakesAtWheels();
		}
		else
		{
			SlowOnSpline();
		}
		if (CheckForObstacle())
		{
			return;
		}
		if (!IsValidPatrons() || instantLeave)
		{
			if (!IsInvoking(SetTravelling))
			{
				float num = 0f;
				if (waitTimeAccumulator > 0f)
				{
					num = GetWaitAccumulator();
				}
				float num2 = 10f + num;
				Invoke(SetTravelling, instantLeave ? 0f : num2);
				if (!instantLeave)
				{
					Invoke(SetGlobalIndicator, num2 - 5f);
				}
			}
		}
		else if (IsInvoking(SetTravelling))
		{
			CancelInvoke(SetTravelling);
		}
	}

	private void HandlePullover()
	{
		if (pulloverPosition == Vector3.zero && !FindPullingOverSpot())
		{
			ResetPullover();
			pulloverPosition = Vector3.zero;
			currentMaxSpeed = 1f;
		}
		else
		{
			if (!AtDestination())
			{
				return;
			}
			if (Vector3.Angle(base.transform.forward, pulloverTangent) > 5f && pulloverPosition != Vector3.zero)
			{
				Vector3 position = pulloverPosition + pulloverTangent * 2f;
				if (!IsPositionClear(position, 2f))
				{
					SetPulloverWaiting();
					return;
				}
				overrideSteering = true;
				internalState = TravellingVendorState.Aligning;
				SetFlag(Flags.Reserved1, b: false);
			}
			else
			{
				SetPulloverWaiting();
			}
		}
	}

	private void SetPulloverWaiting()
	{
		currentPathIndex = GetPathIndexAhead(4);
		SetDestination(currentPath[currentPathIndex]);
		SetWaiting();
		TurnOffIndicators();
		SetFlag(Flags.Reserved6, b: true);
		waitTimeAccumulator += 60f;
		ResetPullover();
	}

	private bool FindPullingOverSpot()
	{
		pulloverHits.Clear();
		bool flag = UnityEngine.Random.value > 0.5f;
		pulloverTangent = (currentPath[GetPathIndexAhead(3)] - currentPath[GetPathIndexAhead(2)]).normalized;
		Vector3 vector = Vector3.Cross(base.transform.up, pulloverTangent);
		Vector3 vector2 = Vector3.Cross(pulloverTangent, base.transform.up);
		if (!TryFindClearPulloverPoint(flag, out var testedPosition))
		{
			flag = !flag;
			if (!TryFindClearPulloverPoint(flag, out testedPosition))
			{
				return false;
			}
		}
		globaIndicatorLeft = !flag;
		if (flag)
		{
			SetFlag(Flags.Reserved2, b: true);
		}
		else
		{
			SetFlag(Flags.Reserved4, b: true);
		}
		if (preventBuildingInstance != null)
		{
			preventBuildingInstance.SetParent(null);
			preventBuildingInstance.transform.position = pulloverPosition;
		}
		pulloverPosition = testedPosition;
		SetDestination(pulloverPosition, 2f);
		return true;
	}

	private Vector3 GetAdjustedPulloverPoint(bool onLeft)
	{
		Vector3 side = (onLeft ? Vector3.Cross(pulloverTangent, base.transform.up) : Vector3.Cross(base.transform.up, pulloverTangent));
		Vector3 pulloverPointFromSide = GetPulloverPointFromSide(side);
		float height = TerrainMeta.HeightMap.GetHeight(pulloverPointFromSide);
		pulloverPointFromSide.y = height + 1f;
		return pulloverPointFromSide;
	}

	private bool TryFindClearPulloverPoint(bool onLeft, out Vector3 testedPosition)
	{
		Vector3 adjustedPulloverPoint = GetAdjustedPulloverPoint(onLeft);
		Vector3 normalized = (adjustedPulloverPoint - base.transform.position).normalized;
		testedPosition = adjustedPulloverPoint;
		bool num = IsDirectionClear(normalized, adjustedPulloverPoint);
		bool flag = IsPositionClear(adjustedPulloverPoint);
		return num && flag;
	}

	private Vector3 GetPulloverPointFromSide(Vector3 side, bool inFront = true)
	{
		if (inFront)
		{
			return currentPath[GetPathIndexAhead(2)] + side * 3.2f + pulloverTangent * 3f;
		}
		return currentPath[GetPathIndexAhead(-2)] + side * 3.2f + pulloverTangent * 3f;
	}

	private bool IsPositionClear(Vector3 position, float radiusCheck = 4.5f)
	{
		List<Collider> obj = Facepunch.Pool.Get<List<Collider>>();
		Vis.Colliders(position, radiusCheck, obj, obstacleMask);
		bool result = true;
		if (obj == null)
		{
			return false;
		}
		if (obj.Count > 0)
		{
			foreach (Collider item in obj)
			{
				if (!(item == null) && !(item.gameObject == null) && !(item.transform == null) && !item.transform.IsChildOf(base.transform) && !(item.transform == base.transform) && !item.CompareTag("IgnoreCollider"))
				{
					result = false;
				}
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return result;
	}

	private bool IsDirectionClear(Vector3 direction, Vector3 point)
	{
		UpdateObstacleList(pulloverHits, direction, 3.5f);
		foreach (RaycastHit pulloverHit in pulloverHits)
		{
			if (!pulloverHit.collider.CompareTag("IgnoreCollider") && !pulloverHit.collider.CompareTag("Main Terrain") && !pulloverHit.collider.transform.IsChildOf(base.transform) && !(pulloverHit.collider.transform == base.transform))
			{
				return false;
			}
		}
		float height = TerrainMeta.HeightMap.GetHeight(point);
		Vector3 testPos = point;
		testPos.y = height + 1f;
		if (base.transform.up.DotDegrees(GetTerrainNormal(testPos)) >= maxPulloverAngleDifference)
		{
			return false;
		}
		return true;
	}

	private Vector3 GetTerrainNormal(Vector3 testPos)
	{
		if (TransformUtil.GetGroundInfo(testPos, out hit, 100f, 8388608))
		{
			return hit.normal;
		}
		return Vector3.zero;
	}

	private void SetWaiting()
	{
		internalState = TravellingVendorState.Waiting;
		forcer.enabled = false;
		pusher.enabled = false;
		SetFlag(Flags.Reserved1, b: true);
		targetThrottle = 0f;
		brakes = 1f;
	}

	private void SetTravelling()
	{
		if (preventBuildingInstance != null && preventBuildingInstance.parentEntity.uid != net.ID)
		{
			preventBuildingInstance.SetParent(this);
			preventBuildingInstance.transform.localPosition = Vector3.zero;
		}
		forcer.enabled = true;
		pusher.enabled = true;
		if ((float)timeSinceBackfire > 30f && UnityEngine.Random.value < 0.6f)
		{
			timeSinceBackfire = 0f;
			Invoke(StartBackfire, UnityEngine.Random.Range(1f, 4f));
		}
		if (HasFlag(Flags.Reserved6))
		{
			SetFlag(Flags.Reserved6, b: false);
		}
		if (!IsInvoking(TurnOffIndicators))
		{
			Invoke(TurnOffIndicators, 3f, 0f);
		}
		SetFlag(Flags.Reserved1, b: false);
		internalState = TravellingVendorState.Travelling;
		brakes = 0f;
	}

	private void AdvancePath()
	{
		bool flag = false;
		if (currentPath != null)
		{
			if (PathComplete())
			{
				currentPathIndex = 0;
				flag = true;
			}
			else if (AtDestination())
			{
				currentPathIndex = GetPathIndexAhead(2);
				flag = true;
			}
			if (flag)
			{
				SetDestination(currentPath[currentPathIndex]);
			}
		}
	}

	private int GetPathIndexAhead(int ahead)
	{
		if (currentPath == null)
		{
			return 0;
		}
		return (currentPathIndex + ahead) % currentPath.Count;
	}

	private void ResetPullover()
	{
		pullingOver = false;
		pullOverTimer = 0f;
		pulloverPosition = Vector3.zero;
		pulloverTangent = Vector3.zero;
	}

	private float GetWaitAccumulator()
	{
		float result = waitTimeAccumulator;
		waitTimeAccumulator = 0f;
		return result;
	}

	public void ScaleSidewaysFriction(float scale)
	{
		float stiffness = 0.75f + 0.75f * scale;
		WheelFrictionCurve sidewaysFriction = wheelFL.wheelCollider.sidewaysFriction;
		sidewaysFriction.stiffness = stiffness;
		wheelFL.wheelCollider.sidewaysFriction = sidewaysFriction;
		wheelFR.wheelCollider.sidewaysFriction = sidewaysFriction;
		wheelRL.wheelCollider.sidewaysFriction = sidewaysFriction;
		wheelRR.wheelCollider.sidewaysFriction = sidewaysFriction;
	}

	private void ApplyDownforce()
	{
		myRigidbody.AddForce(-base.transform.up * downforceCoefficient);
	}

	private void UpdateWheelFlags()
	{
		if (wheelFL.wheelCollider.isGrounded)
		{
			wheelFlags |= WheelIsGroundedFlags.FrontLeft;
		}
		else
		{
			wheelFlags &= ~WheelIsGroundedFlags.FrontLeft;
		}
		if (wheelFR.wheelCollider.isGrounded)
		{
			wheelFlags |= WheelIsGroundedFlags.FrontRight;
		}
		else
		{
			wheelFlags &= ~WheelIsGroundedFlags.FrontRight;
		}
		if (wheelRL.wheelCollider.isGrounded)
		{
			wheelFlags |= WheelIsGroundedFlags.RearLeft;
		}
		else
		{
			wheelFlags &= ~WheelIsGroundedFlags.RearLeft;
		}
		if (wheelRR.wheelCollider.isGrounded)
		{
			wheelFlags |= WheelIsGroundedFlags.RearRight;
		}
		else
		{
			wheelFlags &= ~WheelIsGroundedFlags.RearRight;
		}
	}

	private void BuildingCheck()
	{
		List<BaseEntity> obj = Facepunch.Pool.Get<List<BaseEntity>>();
		Vis.Entities(WorldSpaceBounds(), obj, 1075937536);
		foreach (BaseEntity item in obj)
		{
			if (!(item is Barricade barricade))
			{
				if (!(item is LootContainer lootContainer))
				{
					if (!(item is TreeEntity treeEntity))
					{
						if (!(item is DecayEntity decayEntity))
						{
							if (item is TrainCar { isServer: not false } trainCar && trainCar.IsAlive())
							{
								trainCar.Kill(DestroyMode.Gib);
							}
						}
						else if (should_destroy_buildings && decayEntity.parentEntity.Get(serverside: true) != this && decayEntity.isServer && decayEntity.IsAlive())
						{
							decayEntity.Kill(DestroyMode.Gib);
						}
					}
					else if (treeEntity.isServer)
					{
						treeEntity.Kill();
					}
				}
				else if (lootContainer.isServer && lootContainer.IsAlive())
				{
					lootContainer.Kill(DestroyMode.Gib);
				}
			}
			else if (barricade.isServer && barricade.IsAlive())
			{
				barricade.Kill(DestroyMode.Gib);
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	private bool CheckForObstacle()
	{
		if (obstacleHits == null)
		{
			return false;
		}
		if (obstacleHits.Count <= 0)
		{
			return false;
		}
		foreach (RaycastHit obstacleHit in obstacleHits)
		{
			if (obstacleHit.collider == null)
			{
				continue;
			}
			if (obstacleHit.collider.ToBaseEntity() is BradleyAPC)
			{
				obstacleHits.Clear();
				return true;
			}
			if (!(obstacleHit.collider.ToBaseEntity() is BasePlayer basePlayer) || IsPlayerIgnored(basePlayer) || basePlayer.IsFlying || IsInvalidPlayer(basePlayer))
			{
				continue;
			}
			if ((float)timeSincePlayerDetected > 10f)
			{
				Invoke(StartHorn, UnityEngine.Random.Range(1, 4));
				timeSincePlayerDetected = 0f;
			}
			if (playerRecords.ContainsKey(basePlayer))
			{
				VendorTargetInfo value = playerRecords[basePlayer];
				if (value.lastBlockingTime > UnityEngine.Time.time + 60f)
				{
					value.blockingAccumulator = 0f;
					value.lastBlockingTime = UnityEngine.Time.time;
					playerRecords[basePlayer] = value;
					continue;
				}
				value.lastBlockingTime = UnityEngine.Time.time;
				value.blockingAccumulator += UnityEngine.Time.deltaTime;
				if (value.blockingAccumulator > allowedVendorBlockTime)
				{
					IgnorePlayer(basePlayer);
				}
				else
				{
					playerRecords[basePlayer] = value;
				}
			}
			else
			{
				playerRecords.Add(basePlayer, new VendorTargetInfo
				{
					blockingAccumulator = UnityEngine.Time.deltaTime,
					ignoredUntil = 0f,
					lastBlockingTime = UnityEngine.Time.time,
					lastSeenTime = UnityEngine.Time.time
				});
			}
			obstacleHits.Clear();
			return true;
		}
		return false;
	}

	private void UpdateObstacles()
	{
		UpdateObstacleList(obstacleHits, base.transform.forward);
	}

	private void UpdateObstacleList(List<RaycastHit> hits, Vector3 forward, float checkRadius = 2.5f)
	{
		hits.Clear();
		GamePhysics.TraceAll(new Ray(base.transform.position + base.transform.forward * (bounds.extents.z / 0.6f - 1f), forward), checkRadius, hits, 15f, obstacleMask | 1 | 0x8000, QueryTriggerInteraction.Ignore, this);
	}

	private void DoSteering()
	{
		float num = Mathf.InverseLerp(5f, 1.5f, myRigidbody.velocity.magnitude * Mathf.Abs(Vector3.Dot(myRigidbody.velocity.normalized, base.transform.forward)));
		ScaleSidewaysFriction(1f - num);
		if (!overrideSteering)
		{
			Vector3 vector = base.transform.InverseTransformPoint(destination);
			steeringAngle = Mathf.Atan2(vector.x, vector.z);
			steeringAngle *= 57.29578f;
			float t = steeringSmoothing * UnityEngine.Time.deltaTime;
			smoothedSteering = Mathf.Lerp(smoothedSteering, steeringAngle, t);
			wheelFL.wheelCollider.steerAngle = smoothedSteering;
			wheelFR.wheelCollider.steerAngle = smoothedSteering;
		}
	}

	private void ApplyForceAtWheels()
	{
		if (handbrake)
		{
			wheelFL.wheelCollider.brakeTorque = 1000f;
			wheelFR.wheelCollider.brakeTorque = 1000f;
			wheelRL.wheelCollider.brakeTorque = 1000f;
			wheelRR.wheelCollider.brakeTorque = 1000f;
			return;
		}
		throttle = Mathf.MoveTowards(throttle, targetThrottle, acceleration * UnityEngine.Time.deltaTime);
		float num = throttle * motorForceConstant * 5f;
		bool flag = myRigidbody.velocity.magnitude >= max_speed;
		wheelFL.wheelCollider.brakeTorque = (flag ? brakeForceConstant : 0f);
		wheelFR.wheelCollider.brakeTorque = (flag ? brakeForceConstant : 0f);
		wheelRL.wheelCollider.brakeTorque = (flag ? brakeForceConstant : 0f);
		wheelRR.wheelCollider.brakeTorque = (flag ? brakeForceConstant : 0f);
		if (wheelFL.wheelCollider.isGrounded)
		{
			wheelFL.wheelCollider.motorTorque = num / 4f;
		}
		if (wheelFR.wheelCollider.isGrounded)
		{
			wheelFR.wheelCollider.motorTorque = num / 4f;
		}
		if (wheelRL.wheelCollider.isGrounded)
		{
			wheelRL.wheelCollider.motorTorque = num / 4f;
		}
		if (wheelRR.wheelCollider.isGrounded)
		{
			wheelRR.wheelCollider.motorTorque = num / 4f;
		}
	}

	private void ApplyBrakesAtWheels()
	{
		brakes = Mathf.Clamp(brakes, 0f, 1f);
		wheelFL.wheelCollider.brakeTorque = brakes * brakeForceConstant;
		wheelFR.wheelCollider.brakeTorque = brakes * brakeForceConstant;
		wheelRL.wheelCollider.brakeTorque = brakes * brakeForceConstant;
		wheelRR.wheelCollider.brakeTorque = brakes * brakeForceConstant;
		wheelFR.wheelCollider.motorTorque = 0f;
		wheelFL.wheelCollider.motorTorque = 0f;
		wheelRL.wheelCollider.motorTorque = 0f;
		wheelRR.wheelCollider.motorTorque = 0f;
	}

	private float CalculateSteeringAngle(float radius)
	{
		return Mathf.Atan(wheelbase / radius);
	}

	private void HandleSplineMovement()
	{
		splineTranslator.SetOffset(splineOffset);
		splineTranslator.Update(UnityEngine.Time.deltaTime);
		splineTranslator.GetCurrentPositionAndTangent(out var position, out var _);
		base.transform.position = Vector3.Lerp(base.transform.position, position, UnityEngine.Time.deltaTime * splineMovementSpeed * 10f);
		Vector3 vector = splineTranslator.PeekNextPositionFollowingDirection();
		Vector3 normalized = (vector - position).normalized;
		base.transform.forward = normalized;
		Vector3 vector2 = base.transform.InverseTransformPoint(vector);
		steeringAngle = Mathf.Atan2(vector2.x, vector2.z);
		steeringAngle *= 57.29578f;
		wheelFL.wheelCollider.steerAngle = steeringAngle;
		wheelFR.wheelCollider.steerAngle = steeringAngle;
	}

	private void TravelOnSpline()
	{
		splineTranslator.SetSpeed(splineMovementSpeed);
		slowdownStartSpeed = splineMovementSpeed;
		HandleSplineMovement();
	}

	private void SlowOnSpline()
	{
		splineTranslator.SetSpeed(slowdownStartSpeed);
		HandleSplineMovement();
		slowdownStartSpeed = Mathf.MoveTowards(slowdownStartSpeed, 0f, UnityEngine.Time.deltaTime * 2f);
	}

	private void StopSplineMovement()
	{
		overrideSteering = false;
		myRigidbody.isKinematic = false;
		int num = FindClosestNode() + 2 % currentPath.Count;
		currentPathIndex = num;
		SetDestination(currentPath[currentPathIndex]);
	}

	public void OnSplinePathTrigger(int pathId, WorldSpline spline, int direction)
	{
		if (splineId == -1 && this.spline != spline)
		{
			if (splineTranslator == null)
			{
				splineTranslator = new SimpleSplineTranslator();
			}
			myRigidbody.isKinematic = true;
			splineTranslator.SetSpline(spline).SetSpeed(splineMovementSpeed).SetDirection(direction)
				.CalculateStartingDistance();
			splineId = pathId;
			this.spline = spline;
			if (!IsInvoking(CheckForSplineStart))
			{
				InvokeRepeating(CheckForSplineStart, 0f, 1f);
			}
		}
		else if (splineId != pathId)
		{
			StopSplineMovement();
			splineId = -1;
			followingSpine = false;
		}
	}

	public void OnBasePathTrigger(int pathId, BasePath path)
	{
	}

	private void CheckForSplineStart()
	{
		float start = splineTranslator.GetStart();
		Vector3 tangent;
		Vector3 positionAtDistance = splineTranslator.GetPositionAtDistance(start, out tangent);
		positionAtDistance += splineOffset;
		Vector3 b = spline.transform.TransformPoint(positionAtDistance);
		if (Vector3Ex.Distance2D(base.transform.position, b) < 1.5f)
		{
			overrideSteering = true;
			followingSpine = true;
			CancelInvoke(CheckForSplineStart);
		}
	}

	public void InstallPath(RuntimePath path, int initialDestination)
	{
		if (currentPath == null)
		{
			currentPath = Facepunch.Pool.Get<List<Vector3>>();
		}
		currentPath.Clear();
		IAIPathNode[] nodes = path.Nodes;
		foreach (IAIPathNode iAIPathNode in nodes)
		{
			currentPath.Add(iAIPathNode.Position);
		}
		currentPathIndex = initialDestination;
		SetDestination(currentPath[currentPathIndex]);
	}

	private bool HasPath()
	{
		if (currentPath != null)
		{
			return currentPath.Count > 0;
		}
		return false;
	}

	private bool IsFollowingSpline()
	{
		return followingSpine;
	}

	private void ClearPath()
	{
		currentPath.Clear();
		currentPathIndex = -1;
	}

	private bool IndexValid(int index)
	{
		if (!HasPath())
		{
			return false;
		}
		if (index >= 0)
		{
			return index < currentPath.Count;
		}
		return false;
	}

	private Vector3 GetCurrentPathDestination()
	{
		if (!HasPath())
		{
			return base.transform.position;
		}
		return currentPath[currentPathIndex];
	}

	private bool PathComplete()
	{
		if (HasPath())
		{
			if (currentPathIndex == currentPath.Count - 1)
			{
				return AtDestination();
			}
			return false;
		}
		return true;
	}

	public void SetDestination(Vector3 dest, float destinationDistance = 8f)
	{
		atDestinationDistance = destinationDistance;
		destination = dest;
	}

	public bool AtDestination()
	{
		return Vector3Ex.Distance2D(base.transform.position, destination) <= atDestinationDistance;
	}

	private int FindClosestNode()
	{
		float num = float.MaxValue;
		int result = 0;
		for (int i = 0; i < currentPath.Count; i++)
		{
			Vector3 b = currentPath[i];
			float num2 = Vector3Ex.Distance2D(base.transform.position, b);
			if (num2 < num)
			{
				num = num2;
				result = i;
			}
		}
		return result;
	}

	private void FetchTargets()
	{
		if (playerRecords == null)
		{
			playerRecords = new ListDictionary<BasePlayer, VendorTargetInfo>();
		}
		if (localPlayers == null)
		{
			localPlayers = Facepunch.Pool.Get<List<BasePlayer>>();
		}
		List<BaseEntity> obj = Facepunch.Pool.Get<List<BaseEntity>>();
		FetchCycle(obj);
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	private void FetchCycle(List<BaseEntity> foundEntities)
	{
		Vis.Entities(base.transform.position, searchRange, foundEntities, 133120);
		localPlayers.Clear();
		foreach (BaseEntity foundEntity in foundEntities)
		{
			if (!(foundEntity is BasePlayer basePlayer) || basePlayer is HumanNPC || basePlayer is NPCPlayer || IsInvalidPlayer(basePlayer))
			{
				continue;
			}
			if (playerRecords.ContainsKey(basePlayer))
			{
				VendorTargetInfo value = playerRecords[basePlayer];
				value.lastSeenTime = UnityEngine.Time.time;
				playerRecords[basePlayer] = value;
				if (!IsPlayerIgnored(basePlayer))
				{
					localPlayers.Add(basePlayer);
				}
			}
			else
			{
				playerRecords.Add(basePlayer, new VendorTargetInfo
				{
					blockingAccumulator = 0f,
					ignoredUntil = 0f,
					lastBlockingTime = 0f,
					lastSeenTime = UnityEngine.Time.time
				});
				localPlayers.Add(basePlayer);
			}
		}
	}

	private bool IsInvalidPlayer(BasePlayer player)
	{
		int result = 0 | (player.IsDead() ? 1 : 0) | (player.IsSleeping() ? 1 : 0) | (player.IsHostile() ? 1 : 0) | (player.isClient ? 1 : 0);
		if (player.IsHostile())
		{
			IgnorePlayer(player);
		}
		return (byte)result != 0;
	}

	private void IgnorePlayer(BasePlayer player)
	{
		if (localPlayers.Contains(player))
		{
			localPlayers.Remove(player);
		}
		float num = 90f;
		if (playerRecords.ContainsKey(player))
		{
			VendorTargetInfo value = playerRecords[player];
			value.ignoredUntil = UnityEngine.Time.time + num;
			playerRecords[player] = value;
		}
		else
		{
			playerRecords.Add(player, new VendorTargetInfo
			{
				blockingAccumulator = 0f,
				ignoredUntil = num,
				lastBlockingTime = 0f,
				lastSeenTime = UnityEngine.Time.time
			});
		}
	}

	private bool IsValidPatrons()
	{
		List<BasePlayer> list = localPlayers;
		if ((list != null && list.Count == 0) || localPlayers == null)
		{
			return false;
		}
		return localPlayers.Count > 0;
	}

	private bool IsPlayerIgnored(BasePlayer player)
	{
		if (playerRecords.ContainsKey(player))
		{
			return playerRecords[player].IsIgnored;
		}
		return false;
	}
}
