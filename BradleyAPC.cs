using System;
using System.Collections;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Facepunch.Extend;
using Network;
using ProtoBuf;
using Rust;
using Rust.Ai;
using UnityEngine;
using UnityEngine.AI;

public class BradleyAPC : BaseCombatEntity, TriggerHurtNotChild.IHurtTriggerUser, IPathListener
{
	[Serializable]
	public class ScientistSpawnGroup
	{
		public float BradleyHealth;

		public List<GameObjectRef> SpawnPrefabs;

		public bool Spawned;
	}

	[Serializable]
	public class TargetInfo : Facepunch.Pool.IPooled
	{
		public float damageReceivedFrom;

		public BaseEntity entity;

		public float lastSeenTime;

		public Vector3 lastSeenPosition;

		public void EnterPool()
		{
			entity = null;
			lastSeenPosition = Vector3.zero;
			lastSeenTime = 0f;
		}

		public void Setup(BaseEntity ent, float time)
		{
			entity = ent;
			lastSeenTime = time;
		}

		public void LeavePool()
		{
		}

		public float GetPriorityScore(BradleyAPC apc)
		{
			BasePlayer basePlayer = entity as BasePlayer;
			if ((bool)basePlayer)
			{
				float value = Vector3.Distance(entity.transform.position, apc.transform.position);
				float num = (1f - Mathf.InverseLerp(10f, 80f, value)) * 50f;
				float value2 = ((basePlayer.GetHeldEntity() == null) ? 0f : basePlayer.GetHeldEntity().hostileScore);
				float num2 = Mathf.InverseLerp(4f, 20f, value2) * 100f;
				float num3 = Mathf.InverseLerp(10f, 3f, UnityEngine.Time.time - lastSeenTime) * 100f;
				float num4 = Mathf.InverseLerp(0f, 100f, damageReceivedFrom) * 50f;
				return num + num2 + num4 + num3;
			}
			return 0f;
		}

		public bool IsVisible()
		{
			if (lastSeenTime != -1f)
			{
				return UnityEngine.Time.time - lastSeenTime < sightUpdateRate * 2f;
			}
			return false;
		}

		public bool IsValid()
		{
			return entity != null;
		}
	}

	[Header("Sound")]
	public BlendedLoopEngineSound engineSound;

	public SoundDefinition treadLoopDef;

	public AnimationCurve treadGainCurve;

	public AnimationCurve treadPitchCurve;

	public AnimationCurve treadFreqCurve;

	private Sound treadLoop;

	private SoundModulation.Modulator treadGain;

	private SoundModulation.Modulator treadPitch;

	public SoundDefinition chasisLurchSoundDef;

	public float chasisLurchAngleDelta = 2f;

	public float chasisLurchSpeedDelta = 2f;

	private float lastAngle;

	private float lastSpeed;

	public SoundDefinition turretTurnLoopDef;

	public float turretLoopGainSpeed = 3f;

	public float turretLoopPitchSpeed = 3f;

	public float turretLoopMinAngleDelta;

	public float turretLoopMaxAngleDelta = 10f;

	public float turretLoopPitchMin = 0.5f;

	public float turretLoopPitchMax = 1f;

	public float turretLoopGainThreshold = 0.0001f;

	private Sound turretTurnLoop;

	private SoundModulation.Modulator turretTurnLoopGain;

	private SoundModulation.Modulator turretTurnLoopPitch;

	public float enginePitch = 0.9f;

	public float rpmMultiplier = 0.6f;

	private TreadAnimator treadAnimator;

	[Header("Wheels")]
	public WheelCollider[] leftWheels;

	public WheelCollider[] rightWheels;

	[Header("Movement Config")]
	public float moveForceMax = 2000f;

	public float brakeForce = 100f;

	public float turnForce = 2000f;

	public float sideStiffnessMax = 1f;

	public float sideStiffnessMin = 0.5f;

	public Transform centerOfMass;

	public float stoppingDist = 5f;

	[Header("Control")]
	public float throttle = 1f;

	public float turning;

	public float rightThrottle;

	public float leftThrottle;

	public bool brake;

	[Header("Other")]
	public Rigidbody myRigidBody;

	public Collider myCollider;

	public Vector3 destination;

	private Vector3 finalDestination;

	public Transform followTest;

	public TriggerHurtEx impactDamager;

	[Header("Weapons")]
	public Transform mainTurretEyePos;

	public Transform mainTurret;

	public Transform CannonPitch;

	public Transform CannonMuzzle;

	public Transform coaxPitch;

	public Transform coaxMuzzle;

	public Transform topTurretEyePos;

	public Transform topTurretYaw;

	public Transform topTurretPitch;

	public Transform topTurretMuzzle;

	public GameObjectRef SmokeGrenadePrefab;

	private Vector3 turretAimVector = Vector3.forward;

	private Vector3 desiredAimVector = Vector3.forward;

	private Vector3 topTurretAimVector = Vector3.forward;

	private Vector3 desiredTopTurretAimVector = Vector3.forward;

	[Header("Effects")]
	public GameObjectRef explosionEffect;

	public GameObjectRef servergibs;

	public GameObjectRef fireBall;

	public GameObjectRef crateToDrop;

	public GameObjectRef debrisFieldMarker;

	[Header("Loot")]
	public int maxCratesToSpawn;

	[Header("Spline")]
	public float splineMovementSpeed = 2f;

	public Vector3 splineOffset;

	[Header("Other")]
	public int patrolPathIndex;

	public IAIPath patrolPath;

	public bool DoAI = true;

	public GameObjectRef mainCannonMuzzleFlash;

	public GameObjectRef mainCannonProjectile;

	public float recoilScale = 200f;

	public NavMeshPath navMeshPath;

	public int navMeshPathIndex;

	private SimpleSplineTranslator splineTranslator;

	private LayerMask obstacleHitMask;

	private TimeSince timeSinceSeemingStuck;

	private TimeSince timeSinceStuckReverseStart;

	private const string prefabPath = "assets/prefabs/npc/m2bradley/bradleyapc.prefab";

	private float nextFireTime = 10f;

	private int numBursted;

	private float nextPatrolTime;

	private float nextEngagementPathTime;

	private float currentSpeedZoneLimit;

	[Header("Pathing")]
	public List<Vector3> currentPath;

	public int currentPathIndex;

	public bool pathLooping;

	private bool followingSpine;

	private int splineId = -1;

	private WorldSpline spline;

	private int entryDirection = 1;

	private TimeSince lastJoinedSpline;

	private float lastDist;

	[Header("Scientists")]
	public GameObject AIRoot;

	public GameObjectRef MonumentScientistPrefab;

	public GameObjectRef RoadScientistPrefab;

	public int ScientistSpawnCount = 4;

	public float ScientistSpawnRadius = 3f;

	public List<GameObject> ScientistSpawnPoints = new List<GameObject>();

	public List<ScientistSpawnGroup> ScientistSpawns = new List<ScientistSpawnGroup>();

	public bool SetScientistChaseBasedOnWeapon = true;

	[ServerVar]
	public static float DeployHealthRangeMin = 0.4f;

	[ServerVar]
	public static float DeployHealthRangeMax = 0.5f;

	[ServerVar]
	public static float DeployAttackDistanceMax = 50f;

	[ServerVar]
	public static float DeployInterval = 1f;

	[ServerVar]
	public static float DeployOnDamageCheckInterval = 1f;

	[ServerVar]
	public static float ScientistRedeploymentMinInterval = 60f;

	[ServerVar]
	public static float MountAfterNotAttackedDuration = 180f;

	[ServerVar]
	public static float MountAfterNotTargetsDuration = 60f;

	[ServerVar]
	public static float MountAfterNotFiredDuration = 60f;

	[ServerVar]
	public static bool UseSmokeGrenades = true;

	[ServerVar]
	public static bool KillScientistsOnBradleyDeath = false;

	[HideInInspector]
	public bool RoadSpawned = true;

	private List<ScientistNPC> activeScientists = new List<ScientistNPC>();

	private List<GameObjectRef> mountedScientistPrefabs = new List<GameObjectRef>();

	private List<Vector3> scientistSpawnPositions = new List<Vector3>();

	private int numberOfScientistsToSpawn;

	private TimeSince timeSinceScientistDeploy;

	private TimeSince timeSinceDeployCheck;

	private TimeSince timeSinceValidTarget;

	private TimeSince deployedTimeSinceBradleyAttackedTarget;

	private static int walkableAreaMask;

	private bool mountingScientists;

	private bool inDeployedState;

	private bool deployingScientists;

	private Dictionary<uint, GameObjectRef> scientistPrefabLookUp = new Dictionary<uint, GameObjectRef>();

	[Header("Targeting")]
	public float viewDistance = 100f;

	public float searchRange = 100f;

	public float searchFrequency = 2f;

	public float memoryDuration = 20f;

	public static float sightUpdateRate = 0.5f;

	public List<TargetInfo> targetList = new List<TargetInfo>();

	private BaseCombatEntity mainGunTarget;

	[Header("Coax")]
	public float coaxFireRate = 0.06667f;

	public int coaxBurstLength = 10;

	public float coaxAimCone = 3f;

	public float bulletDamage = 15f;

	[Header("TopTurret")]
	public float topTurretFireRate = 0.25f;

	private float nextCoaxTime;

	private int numCoaxBursted;

	private float nextTopTurretTime = 0.3f;

	public GameObjectRef gun_fire_effect;

	public GameObjectRef bulletEffect;

	private float lastLateUpdate;

	protected override float PositionTickRate => 0.1f;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("BradleyAPC.OnRpcMessage"))
		{
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.bradley != null && !info.fromDisk)
		{
			throttle = info.msg.bradley.engineThrottle;
			rightThrottle = info.msg.bradley.throttleRight;
			leftThrottle = info.msg.bradley.throttleLeft;
			desiredAimVector = info.msg.bradley.mainGunVec;
			desiredTopTurretAimVector = info.msg.bradley.topTurretVec;
		}
	}

	public void BuildingCheck()
	{
		List<BaseEntity> obj = Facepunch.Pool.Get<List<BaseEntity>>();
		Vis.Entities(WorldSpaceBounds(), obj, 256);
		foreach (BaseEntity item in obj)
		{
			if (item is Barricade barricade && barricade.IsAlive() && barricade.isServer)
			{
				barricade.Kill(DestroyMode.Gib);
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		if (!info.forDisk)
		{
			info.msg.bradley = Facepunch.Pool.Get<ProtoBuf.BradleyAPC>();
			info.msg.bradley.engineThrottle = throttle;
			info.msg.bradley.throttleLeft = leftThrottle;
			info.msg.bradley.throttleRight = rightThrottle;
			info.msg.bradley.mainGunVec = turretAimVector;
			info.msg.bradley.topTurretVec = topTurretAimVector;
		}
	}

	public static BradleyAPC SpawnRoadDrivingBradley(Vector3 spawnPos, Quaternion spawnRot)
	{
		RuntimePath runtimePath = new RuntimePath();
		PathList pathList = null;
		float num = float.PositiveInfinity;
		foreach (PathList road in TerrainMeta.Path.Roads)
		{
			_ = Vector3.zero;
			float num2 = float.PositiveInfinity;
			Vector3[] points = road.Path.Points;
			foreach (Vector3 a in points)
			{
				float num3 = Vector3.Distance(a, spawnPos);
				if (num3 < num2)
				{
					num2 = num3;
				}
			}
			if (num2 < num)
			{
				pathList = road;
				num = num2;
			}
		}
		if (pathList == null)
		{
			return null;
		}
		Vector3 startPoint = pathList.Path.GetStartPoint();
		Vector3 endPoint = pathList.Path.GetEndPoint();
		bool flag = startPoint == endPoint;
		int num4 = (flag ? (pathList.Path.Points.Length - 1) : pathList.Path.Points.Length);
		IAIPathNode[] nodes = new RuntimePathNode[num4];
		runtimePath.Nodes = nodes;
		IAIPathNode iAIPathNode = null;
		int num5 = 0;
		int num6 = (flag ? (pathList.Path.MaxIndex - 1) : pathList.Path.MaxIndex);
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
		if (flag)
		{
			runtimePath.Nodes[0].AddLink(runtimePath.Nodes[runtimePath.Nodes.Length - 1]);
			runtimePath.Nodes[runtimePath.Nodes.Length - 1].AddLink(runtimePath.Nodes[0]);
		}
		else
		{
			RuntimeInterestNode interestNode = new RuntimeInterestNode(startPoint + Vector3.up * 1f);
			runtimePath.AddInterestNode(interestNode);
			RuntimeInterestNode interestNode2 = new RuntimeInterestNode(endPoint + Vector3.up * 1f);
			runtimePath.AddInterestNode(interestNode2);
		}
		int value = Mathf.CeilToInt(pathList.Path.Length / 500f);
		value = Mathf.Clamp(value, 1, 3);
		if (flag)
		{
			value++;
		}
		for (int k = 0; k < value; k++)
		{
			int num7 = UnityEngine.Random.Range(0, pathList.Path.Points.Length);
			RuntimeInterestNode interestNode3 = new RuntimeInterestNode(pathList.Path.Points[num7] + Vector3.up * 1f);
			runtimePath.AddInterestNode(interestNode3);
		}
		BaseEntity baseEntity = GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/bradleyapc.prefab", spawnPos, spawnRot);
		BradleyAPC bradleyAPC = null;
		if ((bool)baseEntity)
		{
			bradleyAPC = baseEntity.GetComponent<BradleyAPC>();
			if ((bool)bradleyAPC)
			{
				bradleyAPC.RoadSpawned = true;
				bradleyAPC.Spawn();
				bradleyAPC.InstallPatrolPath(runtimePath);
			}
			else
			{
				baseEntity.Kill();
			}
		}
		return bradleyAPC;
	}

	[ServerVar(Name = "spawnroadbradley")]
	public static string svspawnroadbradley(Vector3 pos, Vector3 dir)
	{
		if (!(SpawnRoadDrivingBradley(pos, Quaternion.LookRotation(dir, Vector3.up)) != null))
		{
			return "Failed to spawn road-driving Bradley.";
		}
		return "Spawned road-driving Bradley.";
	}

	public void SetDestination(Vector3 dest)
	{
		destination = dest;
	}

	public override void ServerInit()
	{
		base.ServerInit();
		CacheSpawnPrefabIDS();
		walkableAreaMask = 1 << NavMesh.GetAreaFromName("Walkable");
		deployedTimeSinceBradleyAttackedTarget = 0f;
		timeSinceScientistDeploy = float.PositiveInfinity;
		timeSinceDeployCheck = float.PositiveInfinity;
		numberOfScientistsToSpawn = ScientistSpawnCount;
		Initialize();
		InvokeRepeating(UpdateTargetList, 0f, 2f);
		InvokeRepeating(UpdateTargetVisibilities, 0f, sightUpdateRate);
		InvokeRepeating(BuildingCheck, 1f, 5f);
		AIRoot.SetActive(value: false);
		obstacleHitMask = LayerMask.GetMask("Vehicle World");
		timeSinceSeemingStuck = 0f;
		timeSinceStuckReverseStart = float.MaxValue;
	}

	public override void OnCollision(Collision collision, BaseEntity hitEntity)
	{
	}

	public void Initialize()
	{
		myRigidBody.centerOfMass = centerOfMass.localPosition;
		destination = base.transform.position;
		finalDestination = base.transform.position;
	}

	public BasePlayer FollowPlayer()
	{
		foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
		{
			if (activePlayer.IsAdmin && activePlayer.IsAlive() && !activePlayer.IsSleeping() && activePlayer.GetActiveItem() != null && activePlayer.GetActiveItem().info.shortname == "tool.binoculars")
			{
				return activePlayer;
			}
		}
		return null;
	}

	public static Vector3 Direction2D(Vector3 aimAt, Vector3 aimFrom)
	{
		return (new Vector3(aimAt.x, 0f, aimAt.z) - new Vector3(aimFrom.x, 0f, aimFrom.z)).normalized;
	}

	public bool IsAtDestination()
	{
		return Vector3Ex.Distance2D(base.transform.position, destination) <= stoppingDist;
	}

	public bool IsAtFinalDestination()
	{
		return Vector3Ex.Distance2D(base.transform.position, finalDestination) <= stoppingDist;
	}

	public Vector3 ClosestPointAlongPath(Vector3 start, Vector3 end, Vector3 fromPos)
	{
		Vector3 vector = end - start;
		Vector3 rhs = fromPos - start;
		float num = Vector3.Dot(vector, rhs);
		float num2 = Vector3.SqrMagnitude(end - start);
		float num3 = Mathf.Clamp01(num / num2);
		return start + vector * num3;
	}

	public void FireGunTest()
	{
		if (UnityEngine.Time.time < nextFireTime)
		{
			return;
		}
		deployedTimeSinceBradleyAttackedTarget = 0f;
		nextFireTime = UnityEngine.Time.time + 0.25f;
		numBursted++;
		if (numBursted >= 4)
		{
			nextFireTime = UnityEngine.Time.time + 5f;
			numBursted = 0;
		}
		Vector3 modifiedAimConeDirection = AimConeUtil.GetModifiedAimConeDirection(2f, CannonMuzzle.rotation * Vector3.forward);
		Vector3 normalized = (CannonPitch.transform.rotation * Vector3.back + base.transform.up * -1f).normalized;
		myRigidBody.AddForceAtPosition(normalized * recoilScale, CannonPitch.transform.position, ForceMode.Impulse);
		Effect.server.Run(mainCannonMuzzleFlash.resourcePath, this, StringPool.Get(CannonMuzzle.gameObject.name), Vector3.zero, Vector3.zero);
		BaseEntity baseEntity = GameManager.server.CreateEntity(mainCannonProjectile.resourcePath, CannonMuzzle.transform.position, Quaternion.LookRotation(modifiedAimConeDirection));
		if (!(baseEntity == null))
		{
			ServerProjectile component = baseEntity.GetComponent<ServerProjectile>();
			if ((bool)component)
			{
				component.InitializeVelocity(modifiedAimConeDirection * component.speed);
			}
			if (baseEntity.TryGetComponent<TimedExplosive>(out var component2))
			{
				component2.creatorEntity = this;
			}
			baseEntity.Spawn();
		}
	}

	public void InstallPatrolPath(IAIPath path)
	{
		patrolPath = path;
		currentPath = new List<Vector3>();
		currentPathIndex = -1;
	}

	public void UpdateMovement_Patrol()
	{
		if (patrolPath == null || UnityEngine.Time.time < nextPatrolTime)
		{
			return;
		}
		nextPatrolTime = UnityEngine.Time.time + 20f;
		if (HasPath() && !IsAtFinalDestination())
		{
			return;
		}
		IAIPathInterestNode randomInterestNodeAwayFrom = patrolPath.GetRandomInterestNodeAwayFrom(base.transform.position);
		IAIPathNode closestToPoint = patrolPath.GetClosestToPoint(randomInterestNodeAwayFrom.Position);
		bool flag = false;
		List<IAIPathNode> nodes = Facepunch.Pool.Get<List<IAIPathNode>>();
		IAIPathNode iAIPathNode;
		if (GetEngagementPath(ref nodes))
		{
			flag = true;
			iAIPathNode = nodes[nodes.Count - 1];
		}
		else
		{
			iAIPathNode = patrolPath.GetClosestToPoint(base.transform.position);
		}
		if (!(Vector3.Distance(finalDestination, closestToPoint.Position) > 2f))
		{
			return;
		}
		if (closestToPoint == iAIPathNode)
		{
			currentPath.Clear();
			currentPath.Add(closestToPoint.Position);
			currentPathIndex = -1;
			pathLooping = false;
			finalDestination = closestToPoint.Position;
		}
		else
		{
			if (!AStarPath.FindPath(iAIPathNode, closestToPoint, out var path, out var _))
			{
				return;
			}
			currentPath.Clear();
			if (flag)
			{
				for (int i = 0; i < nodes.Count - 1; i++)
				{
					currentPath.Add(nodes[i].Position);
				}
			}
			foreach (IAIPathNode item in path)
			{
				currentPath.Add(item.Position);
			}
			currentPathIndex = -1;
			pathLooping = false;
			finalDestination = closestToPoint.Position;
		}
	}

	private void EnterSpline()
	{
		myRigidBody.isKinematic = true;
	}

	private void LeaveSpline()
	{
		lastJoinedSpline = 0f;
		myRigidBody.isKinematic = false;
	}

	public void DoSplineMove()
	{
		if (base.isClient)
		{
			return;
		}
		splineTranslator.SetOffset(splineOffset);
		Vector3 tangent;
		if (targetList.Count > 0)
		{
			TargetInfo targetInfo = targetList[0];
			if (targetInfo.IsValid() && targetInfo.IsVisible())
			{
				tangent = targetInfo.lastSeenPosition - base.transform.position;
				Vector3 normalized = tangent.normalized;
				float num = Vector3.Dot(base.transform.forward, normalized);
				if (num > 0f)
				{
					splineTranslator.SetDirection(entryDirection);
				}
				else if (num < 0f)
				{
					splineTranslator.SetDirection(-entryDirection);
				}
			}
		}
		splineTranslator.Update(UnityEngine.Time.deltaTime);
		splineTranslator.GetCurrentPositionAndTangent(out var position, out tangent);
		base.transform.position = Vector3.Lerp(base.transform.position, position, UnityEngine.Time.deltaTime * splineMovementSpeed * 10f);
		Vector3 normalized2 = (splineTranslator.PeekNextPosition(0.1f, entryDirection) - position).normalized;
		base.transform.forward = normalized2;
		if (Math.Abs(splineTranslator.CurrentDistance - splineTranslator.GetEnd()) < 1f)
		{
			followingSpine = false;
			LeaveSpline();
		}
	}

	public void UpdateMovement_Hunt()
	{
		if (patrolPath == null)
		{
			return;
		}
		TargetInfo targetInfo = targetList[0];
		if (!targetInfo.IsValid())
		{
			return;
		}
		if (HasPath() && targetInfo.IsVisible())
		{
			if (currentPath.Count > 1)
			{
				Vector3 item = currentPath[currentPathIndex];
				ClearPath();
				currentPath.Add(item);
				finalDestination = item;
				currentPathIndex = 0;
			}
		}
		else
		{
			if (!(UnityEngine.Time.time > nextEngagementPathTime) || HasPath() || targetInfo.IsVisible())
			{
				return;
			}
			bool flag = false;
			IAIPathNode start = patrolPath.GetClosestToPoint(base.transform.position);
			List<IAIPathNode> nodes = Facepunch.Pool.Get<List<IAIPathNode>>();
			if (GetEngagementPath(ref nodes))
			{
				flag = true;
				start = nodes[nodes.Count - 1];
			}
			IAIPathNode iAIPathNode = null;
			List<IAIPathNode> nearNodes = Facepunch.Pool.Get<List<IAIPathNode>>();
			patrolPath.GetNodesNear(targetInfo.lastSeenPosition, ref nearNodes, 30f);
			Stack<IAIPathNode> stack = null;
			float num = float.PositiveInfinity;
			float y = mainTurretEyePos.localPosition.y;
			foreach (IAIPathNode item2 in nearNodes)
			{
				Stack<IAIPathNode> path = new Stack<IAIPathNode>();
				if (targetInfo.entity.IsVisible(item2.Position + new Vector3(0f, y, 0f)) && AStarPath.FindPath(start, item2, out path, out var pathCost) && pathCost < num)
				{
					stack = path;
					num = pathCost;
					iAIPathNode = item2;
				}
			}
			if (stack == null && nearNodes.Count > 0)
			{
				Stack<IAIPathNode> path2 = new Stack<IAIPathNode>();
				IAIPathNode iAIPathNode2 = nearNodes[UnityEngine.Random.Range(0, nearNodes.Count)];
				if (AStarPath.FindPath(start, iAIPathNode2, out path2, out var pathCost2) && pathCost2 < num)
				{
					stack = path2;
					iAIPathNode = iAIPathNode2;
				}
			}
			if (stack != null)
			{
				currentPath.Clear();
				if (flag)
				{
					for (int i = 0; i < nodes.Count - 1; i++)
					{
						currentPath.Add(nodes[i].Position);
					}
				}
				foreach (IAIPathNode item3 in stack)
				{
					currentPath.Add(item3.Position);
				}
				currentPathIndex = -1;
				pathLooping = false;
				finalDestination = iAIPathNode.Position;
			}
			Facepunch.Pool.FreeUnmanaged(ref nearNodes);
			Facepunch.Pool.FreeUnmanaged(ref nodes);
			nextEngagementPathTime = UnityEngine.Time.time + 5f;
		}
	}

	public void DoSimpleAI()
	{
		if (base.isClient)
		{
			return;
		}
		SetFlag(Flags.Reserved5, TOD_Sky.Instance.IsNight);
		if (!DoAI)
		{
			return;
		}
		SetTarget();
		if (mountingScientists || inDeployedState)
		{
			ClearPath();
		}
		else if (!IsOnSpline())
		{
			if (targetList.Count > 0)
			{
				UpdateMovement_Hunt();
			}
			else
			{
				UpdateMovement_Patrol();
			}
		}
		if (!IsOnSpline())
		{
			AdvancePathMovement(force: false);
			float num = Vector3.Distance(base.transform.position, destination);
			float value = Vector3.Distance(base.transform.position, finalDestination);
			if (num > stoppingDist)
			{
				Vector3 lhs = Direction2D(destination, base.transform.position);
				float num2 = Vector3.Dot(lhs, base.transform.right);
				float num3 = Vector3.Dot(lhs, base.transform.right);
				float num4 = Vector3.Dot(lhs, -base.transform.right);
				if (Vector3.Dot(lhs, -base.transform.forward) > num2)
				{
					if (num3 >= num4)
					{
						turning = 1f;
					}
					else
					{
						turning = -1f;
					}
				}
				else
				{
					turning = Mathf.Clamp(num2 * 3f, -1f, 1f);
				}
				float throttleScaleFromTurn = 1f - Mathf.InverseLerp(0f, 0.3f, Mathf.Abs(turning));
				AvoidObstacles(ref throttleScaleFromTurn);
				float num5 = Vector3.Dot(myRigidBody.velocity, base.transform.forward);
				if (!(throttle > 0f) || !(num5 < 0.5f))
				{
					timeSinceSeemingStuck = 0f;
				}
				else if ((float)timeSinceSeemingStuck > 10f)
				{
					timeSinceStuckReverseStart = 0f;
					timeSinceSeemingStuck = 0f;
				}
				float num6 = Mathf.InverseLerp(0.1f, 0.4f, Vector3.Dot(base.transform.forward, Vector3.up));
				if ((float)timeSinceStuckReverseStart < 3f)
				{
					throttle = -0.75f;
					turning = 1f;
				}
				else
				{
					throttle = (0.1f + Mathf.InverseLerp(0f, 20f, value) * 1f) * throttleScaleFromTurn + num6;
				}
			}
		}
		DoWeaponAiming();
		SendNetworkUpdate();
	}

	private void SetTarget()
	{
		if (targetList.Count == 0)
		{
			mainGunTarget = null;
		}
		else if (targetList[0].IsValid() && targetList[0].IsVisible())
		{
			mainGunTarget = targetList[0].entity as BaseCombatEntity;
		}
		else
		{
			mainGunTarget = null;
		}
	}

	public void FixedUpdate()
	{
		if (mountingScientists)
		{
			UpdateMountScientists();
		}
		else if (inDeployedState)
		{
			UpdateDeployed();
		}
		DoSimpleAI();
		if (IsOnSpline())
		{
			DoSplineMove();
		}
		else
		{
			DoPhysicsMove();
		}
		DoWeapons();
		DoHealing();
	}

	private void AvoidObstacles(ref float throttleScaleFromTurn)
	{
		Ray ray = new Ray(base.transform.position + base.transform.forward * (bounds.extents.z - 1f), base.transform.forward);
		if (!GamePhysics.Trace(ray, 3f, out var hitInfo, 20f, obstacleHitMask, QueryTriggerInteraction.Ignore, this))
		{
			return;
		}
		if (hitInfo.point == Vector3.zero)
		{
			hitInfo.point = hitInfo.collider.ClosestPointOnBounds(ray.origin);
		}
		float num = base.transform.AngleToPos(hitInfo.point);
		float num2 = Mathf.Abs(num);
		if (num2 > 75f || !(hitInfo.collider.ToBaseEntity() is BradleyAPC))
		{
			return;
		}
		bool flag = false;
		if (num2 < 5f)
		{
			float num3 = ((throttle < 0f) ? 150f : 50f);
			if (Vector3.SqrMagnitude(base.transform.position - hitInfo.point) < num3)
			{
				flag = true;
			}
		}
		if (num > 30f)
		{
			turning = -1f;
		}
		else
		{
			turning = 1f;
		}
		throttleScaleFromTurn = (flag ? (-1f) : 1f);
		int num4 = currentPathIndex;
		_ = currentPathIndex;
		float num5 = Vector3.Distance(base.transform.position, destination);
		while (HasPath() && (double)num5 < 26.6 && currentPathIndex >= 0)
		{
			int num6 = currentPathIndex;
			AdvancePathMovement(force: true);
			num5 = Vector3.Distance(base.transform.position, destination);
			if (currentPathIndex == num4 || currentPathIndex == num6)
			{
				break;
			}
		}
	}

	public void DoPhysicsMove()
	{
		if (base.isClient)
		{
			return;
		}
		Vector3 velocity = myRigidBody.velocity;
		throttle = Mathf.Clamp(throttle, -1f, 1f);
		leftThrottle = throttle;
		rightThrottle = throttle;
		if (turning > 0f)
		{
			rightThrottle = 0f - turning;
			leftThrottle = turning;
		}
		else if (turning < 0f)
		{
			leftThrottle = turning;
			rightThrottle = turning * -1f;
		}
		Vector3.Distance(base.transform.position, GetFinalDestination());
		float num = Vector3.Distance(base.transform.position, GetCurrentPathDestination());
		float num2 = 15f;
		if (num < 20f)
		{
			float value = Vector3.Dot(PathDirection(currentPathIndex), PathDirection(currentPathIndex + 1));
			float num3 = Mathf.InverseLerp(2f, 10f, num);
			float num4 = Mathf.InverseLerp(0.5f, 0.8f, value);
			num2 = 15f - 14f * ((1f - num4) * (1f - num3));
		}
		_ = 20f;
		if (patrolPath != null)
		{
			float num5 = num2;
			foreach (IAIPathSpeedZone speedZone in patrolPath.SpeedZones)
			{
				if (speedZone.WorldSpaceBounds().Contains(base.transform.position))
				{
					num5 = Mathf.Min(num5, speedZone.GetMaxSpeed());
				}
			}
			currentSpeedZoneLimit = Mathf.Lerp(currentSpeedZoneLimit, num5, UnityEngine.Time.deltaTime);
			num2 = Mathf.Min(num2, currentSpeedZoneLimit);
		}
		if (PathComplete())
		{
			num2 = 0f;
		}
		brake = velocity.magnitude >= num2;
		ApplyBrakes(brake ? 1f : 0f);
		float num6 = throttle;
		leftThrottle = Mathf.Clamp(leftThrottle + num6, -1f, 1f);
		rightThrottle = Mathf.Clamp(rightThrottle + num6, -1f, 1f);
		float t = Mathf.InverseLerp(2f, 1f, velocity.magnitude * Mathf.Abs(Vector3.Dot(velocity.normalized, base.transform.forward)));
		float torqueAmount = Mathf.Lerp(moveForceMax, turnForce, t);
		float num7 = Mathf.InverseLerp(5f, 1.5f, velocity.magnitude * Mathf.Abs(Vector3.Dot(velocity.normalized, base.transform.forward)));
		ScaleSidewaysFriction(1f - num7);
		SetMotorTorque(leftThrottle, rightSide: false, torqueAmount);
		SetMotorTorque(rightThrottle, rightSide: true, torqueAmount);
		impactDamager.damageEnabled = myRigidBody.velocity.magnitude > 2f;
	}

	public void ApplyBrakes(float amount)
	{
		ApplyBrakeTorque(amount, rightSide: true);
		ApplyBrakeTorque(amount, rightSide: false);
	}

	public float GetMotorTorque(bool rightSide)
	{
		float num = 0f;
		WheelCollider[] array = (rightSide ? rightWheels : leftWheels);
		foreach (WheelCollider wheelCollider in array)
		{
			num += wheelCollider.motorTorque;
		}
		return num / (float)rightWheels.Length;
	}

	public void ScaleSidewaysFriction(float scale)
	{
		float stiffness = 0.75f + 0.75f * scale;
		WheelCollider[] array = rightWheels;
		foreach (WheelCollider obj in array)
		{
			WheelFrictionCurve sidewaysFriction = obj.sidewaysFriction;
			sidewaysFriction.stiffness = stiffness;
			obj.sidewaysFriction = sidewaysFriction;
		}
		array = leftWheels;
		foreach (WheelCollider obj2 in array)
		{
			WheelFrictionCurve sidewaysFriction2 = obj2.sidewaysFriction;
			sidewaysFriction2.stiffness = stiffness;
			obj2.sidewaysFriction = sidewaysFriction2;
		}
	}

	public void SetMotorTorque(float newThrottle, bool rightSide, float torqueAmount)
	{
		newThrottle = Mathf.Clamp(newThrottle, -1f, 1f);
		float num = torqueAmount * newThrottle;
		int num2 = (rightSide ? rightWheels.Length : leftWheels.Length);
		int num3 = 0;
		WheelCollider[] array = (rightSide ? rightWheels : leftWheels);
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].GetGroundHit(out var _))
			{
				num3++;
			}
		}
		float num4 = 1f;
		if (num3 > 0)
		{
			num4 = num2 / num3;
		}
		array = (rightSide ? rightWheels : leftWheels);
		foreach (WheelCollider wheelCollider in array)
		{
			if (wheelCollider.GetGroundHit(out var _))
			{
				wheelCollider.motorTorque = num * num4;
			}
			else
			{
				wheelCollider.motorTorque = num;
			}
		}
	}

	public void ApplyBrakeTorque(float amount, bool rightSide)
	{
		WheelCollider[] array = (rightSide ? rightWheels : leftWheels);
		for (int i = 0; i < array.Length; i++)
		{
			array[i].brakeTorque = brakeForce * amount;
		}
	}

	public void CreateExplosionMarker(float durationMinutes)
	{
		BaseEntity baseEntity = GameManager.server.CreateEntity(debrisFieldMarker.resourcePath, base.transform.position, Quaternion.identity);
		baseEntity.Spawn();
		baseEntity.SendMessage("SetDuration", durationMinutes, SendMessageOptions.DontRequireReceiver);
	}

	public override void OnKilled(HitInfo info)
	{
		if (base.isClient)
		{
			return;
		}
		CreateExplosionMarker(10f);
		Effect.server.Run(explosionEffect.resourcePath, mainTurretEyePos.transform.position, Vector3.up, null, broadcast: true);
		Vector3 zero = Vector3.zero;
		GameObject gibSource = servergibs.Get().GetComponent<ServerGib>()._gibSource;
		List<ServerGib> list = ServerGib.CreateGibs(servergibs.resourcePath, base.gameObject, gibSource, zero, 3f);
		for (int i = 0; i < 12 - maxCratesToSpawn; i++)
		{
			BaseEntity baseEntity = GameManager.server.CreateEntity(this.fireBall.resourcePath, base.transform.position, base.transform.rotation);
			if (!baseEntity)
			{
				continue;
			}
			float minInclusive = 3f;
			float maxInclusive = 10f;
			Vector3 onUnitSphere = UnityEngine.Random.onUnitSphere;
			baseEntity.transform.position = base.transform.position + new Vector3(0f, 1.5f, 0f) + onUnitSphere * UnityEngine.Random.Range(-4f, 4f);
			Collider component = baseEntity.GetComponent<Collider>();
			baseEntity.Spawn();
			baseEntity.SetVelocity(zero + onUnitSphere * UnityEngine.Random.Range(minInclusive, maxInclusive));
			foreach (ServerGib item in list)
			{
				UnityEngine.Physics.IgnoreCollision(component, item.GetCollider(), ignore: true);
			}
		}
		for (int j = 0; j < maxCratesToSpawn; j++)
		{
			Vector3 onUnitSphere2 = UnityEngine.Random.onUnitSphere;
			onUnitSphere2.y = 0f;
			onUnitSphere2.Normalize();
			Vector3 pos = base.transform.position + new Vector3(0f, 1.5f, 0f) + onUnitSphere2 * UnityEngine.Random.Range(2f, 3f);
			BaseEntity baseEntity2 = GameManager.server.CreateEntity(crateToDrop.resourcePath, pos, Quaternion.LookRotation(onUnitSphere2));
			baseEntity2.Spawn();
			LootContainer lootContainer = baseEntity2 as LootContainer;
			if ((bool)lootContainer)
			{
				lootContainer.Invoke(lootContainer.RemoveMe, 1800f);
			}
			Collider component2 = baseEntity2.GetComponent<Collider>();
			Rigidbody rigidbody = baseEntity2.gameObject.AddComponent<Rigidbody>();
			rigidbody.useGravity = true;
			rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
			rigidbody.mass = 2f;
			rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
			rigidbody.velocity = zero + onUnitSphere2 * UnityEngine.Random.Range(1f, 3f);
			rigidbody.angularVelocity = Vector3Ex.Range(-1.75f, 1.75f);
			rigidbody.drag = 0.5f * (rigidbody.mass / 5f);
			rigidbody.angularDrag = 0.2f * (rigidbody.mass / 5f);
			FireBall fireBall = GameManager.server.CreateEntity(this.fireBall.resourcePath) as FireBall;
			if ((bool)fireBall)
			{
				fireBall.SetParent(baseEntity2);
				fireBall.Spawn();
				fireBall.GetComponent<Rigidbody>().isKinematic = true;
				fireBall.GetComponent<Collider>().enabled = false;
			}
			baseEntity2.SendMessage("SetLockingEnt", fireBall.gameObject, SendMessageOptions.DontRequireReceiver);
			foreach (ServerGib item2 in list)
			{
				UnityEngine.Physics.IgnoreCollision(component2, item2.GetCollider(), ignore: true);
			}
		}
		KillSpawnedScientists();
		if (info != null && info.InitiatorPlayer != null && info.InitiatorPlayer.serverClan != null)
		{
			info.InitiatorPlayer.AddClanScore(ClanScoreEventType.DestroyedBradley);
		}
		base.OnKilled(info);
	}

	public override void OnAttacked(HitInfo info)
	{
		base.OnAttacked(info);
		if (!base.isClient)
		{
			BasePlayer basePlayer = info.Initiator as BasePlayer;
			if (!(basePlayer is ScientistNPC) && basePlayer != null)
			{
				TrySpawnScientists(basePlayer);
				AddOrUpdateTarget(basePlayer, info.PointStart, info.damageTypes.Total());
			}
		}
	}

	public override void Hurt(HitInfo info)
	{
		if (!(info.Initiator != null) || !(info.Initiator is ScientistNPC))
		{
			base.Hurt(info);
		}
	}

	public override void OnHealthChanged(float oldvalue, float newvalue)
	{
		base.OnHealthChanged(oldvalue, newvalue);
		if (base.isServer)
		{
			SetFlag(Flags.Reserved2, base.healthFraction <= 0.75f);
			SetFlag(Flags.Reserved3, base.healthFraction < 0.4f);
		}
	}

	public void DoHealing()
	{
		if (!base.isClient && base.SecondsSinceAttacked > 600f)
		{
			if (base.healthFraction < 1f)
			{
				float amount = MaxHealth() / 300f * UnityEngine.Time.fixedDeltaTime;
				Heal(amount);
			}
			if (numberOfScientistsToSpawn < ScientistSpawnCount && base.healthFraction >= 0.95f && (float)timeSinceScientistDeploy > 30f)
			{
				numberOfScientistsToSpawn = ScientistSpawnCount;
			}
		}
	}

	public BasePlayer GetPlayerDamageInitiator()
	{
		return null;
	}

	public float GetDamageMultiplier(BaseEntity ent)
	{
		float num = ((throttle > 0f) ? 10f : 0f);
		float num2 = Vector3.Dot(myRigidBody.velocity, base.transform.forward);
		if (num2 > 0f)
		{
			num += num2 * 0.5f;
		}
		if (ent is BaseVehicle)
		{
			num *= 10f;
		}
		return num;
	}

	public void OnHurtTriggerOccupant(BaseEntity hurtEntity, DamageType damageType, float damageTotal)
	{
	}

	private void CheckForSplineStart()
	{
		float start = splineTranslator.GetStart();
		Vector3 tangent;
		Vector3 positionAtDistance = splineTranslator.GetPositionAtDistance(start, out tangent);
		positionAtDistance += splineOffset;
		Vector3 b = spline.transform.TransformPoint(positionAtDistance);
		float num = Vector3Ex.Distance2D(base.transform.position, b);
		if (num < 1.5f)
		{
			followingSpine = true;
			CancelInvoke(CheckForSplineStart);
			EnterSpline();
		}
		lastDist = num;
	}

	private bool ShouldJoinSpline(WorldSpline spline)
	{
		if (targetList.Count > 0)
		{
			TargetInfo targetInfo = targetList[0];
			if (targetInfo.IsValid() && targetInfo.IsVisible())
			{
				Vector3 normalized = (targetInfo.lastSeenPosition - base.transform.position).normalized;
				float num = Vector3.Dot((spline.transform.position - base.transform.position).normalized, normalized);
				if (num > 0f)
				{
					return true;
				}
				if (num < 0f)
				{
					return false;
				}
			}
		}
		return true;
	}

	public void OnSplinePathTrigger(int pathId, WorldSpline spline, int direction)
	{
		if (followingSpine || (float)lastJoinedSpline <= 5f)
		{
			return;
		}
		lastJoinedSpline = 0f;
		if (ShouldJoinSpline(spline))
		{
			if (splineTranslator == null)
			{
				splineTranslator = new SimpleSplineTranslator();
			}
			splineTranslator.SetSpline(spline).SetSpeed(splineMovementSpeed).SetDirection(direction)
				.CalculateStartingDistance();
			splineId = pathId;
			this.spline = spline;
			entryDirection = direction;
			followingSpine = true;
			if (!IsInvoking(CheckForSplineStart))
			{
				InvokeRepeating(CheckForSplineStart, 0f, 1f);
			}
		}
	}

	public void OnBasePathTrigger(int pathId, BasePath path)
	{
	}

	private bool IsOnSpline()
	{
		return followingSpine;
	}

	public bool HasPath()
	{
		if (currentPath != null)
		{
			return currentPath.Count > 0;
		}
		return false;
	}

	public void ClearPath()
	{
		currentPath.Clear();
		currentPathIndex = -1;
	}

	public bool IndexValid(int index)
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

	public Vector3 GetFinalDestination()
	{
		if (!HasPath())
		{
			return base.transform.position;
		}
		return finalDestination;
	}

	public Vector3 GetCurrentPathDestination()
	{
		if (!HasPath())
		{
			return base.transform.position;
		}
		return currentPath[currentPathIndex];
	}

	public bool PathComplete()
	{
		if (HasPath())
		{
			if (currentPathIndex == currentPath.Count - 1)
			{
				return AtCurrentPathNode();
			}
			return false;
		}
		return true;
	}

	public bool AtCurrentPathNode()
	{
		if (currentPathIndex < 0 || currentPathIndex >= currentPath.Count)
		{
			return false;
		}
		return Vector3.Distance(base.transform.position, currentPath[currentPathIndex]) <= stoppingDist;
	}

	public int GetLoopedIndex(int index)
	{
		if (!HasPath())
		{
			Debug.LogWarning("Warning, GetLoopedIndex called without a path");
			return 0;
		}
		if (!pathLooping)
		{
			return Mathf.Clamp(index, 0, currentPath.Count - 1);
		}
		if (index >= currentPath.Count)
		{
			return index % currentPath.Count;
		}
		if (index < 0)
		{
			return currentPath.Count - Mathf.Abs(index % currentPath.Count);
		}
		return index;
	}

	public Vector3 PathDirection(int index)
	{
		if (!HasPath() || currentPath.Count <= 1)
		{
			return base.transform.forward;
		}
		index = GetLoopedIndex(index);
		Vector3 vector;
		Vector3 vector2;
		if (pathLooping)
		{
			int loopedIndex = GetLoopedIndex(index - 1);
			vector = currentPath[loopedIndex];
			vector2 = currentPath[GetLoopedIndex(index)];
		}
		else
		{
			vector = ((index - 1 >= 0) ? currentPath[index - 1] : base.transform.position);
			vector2 = currentPath[index];
		}
		return (vector2 - vector).normalized;
	}

	public Vector3 IdealPathPosition()
	{
		if (!HasPath())
		{
			return base.transform.position;
		}
		int loopedIndex = GetLoopedIndex(currentPathIndex - 1);
		if (loopedIndex == currentPathIndex)
		{
			return currentPath[currentPathIndex];
		}
		return ClosestPointAlongPath(currentPath[loopedIndex], currentPath[currentPathIndex], base.transform.position);
	}

	public void AdvancePathMovement(bool force)
	{
		if (HasPath())
		{
			if (force || AtCurrentPathNode() || currentPathIndex == -1)
			{
				currentPathIndex = GetLoopedIndex(currentPathIndex + 1);
			}
			if (PathComplete())
			{
				ClearPath();
				return;
			}
			Vector3 vector = IdealPathPosition();
			Vector3 vector2 = currentPath[currentPathIndex];
			float a = Vector3.Distance(vector, vector2);
			float value = Vector3.Distance(base.transform.position, vector);
			float num = Mathf.InverseLerp(8f, 0f, value);
			vector += Direction2D(vector2, vector) * Mathf.Min(a, num * 20f);
			SetDestination(vector);
		}
	}

	public bool GetPathToClosestTurnableNode(IAIPathNode start, Vector3 forward, ref List<IAIPathNode> nodes)
	{
		float num = float.NegativeInfinity;
		IAIPathNode iAIPathNode = null;
		foreach (IAIPathNode item in start.Linked)
		{
			float num2 = Vector3.Dot(forward, (item.Position - start.Position).normalized);
			if (num2 > num)
			{
				num = num2;
				iAIPathNode = item;
			}
		}
		if (iAIPathNode != null)
		{
			nodes.Add(iAIPathNode);
			if (!iAIPathNode.Straightaway)
			{
				return true;
			}
			return GetPathToClosestTurnableNode(iAIPathNode, (iAIPathNode.Position - start.Position).normalized, ref nodes);
		}
		return false;
	}

	public bool GetEngagementPath(ref List<IAIPathNode> nodes)
	{
		IAIPathNode closestToPoint = patrolPath.GetClosestToPoint(base.transform.position);
		Vector3 normalized = (closestToPoint.Position - base.transform.position).normalized;
		if (Vector3.Dot(base.transform.forward, normalized) > 0f)
		{
			nodes.Add(closestToPoint);
			if (!closestToPoint.Straightaway)
			{
				return true;
			}
		}
		return GetPathToClosestTurnableNode(closestToPoint, base.transform.forward, ref nodes);
	}

	private void CacheSpawnPrefabIDS()
	{
		scientistPrefabLookUp.Clear();
		foreach (ScientistSpawnGroup scientistSpawn in ScientistSpawns)
		{
			foreach (GameObjectRef spawnPrefab in scientistSpawn.SpawnPrefabs)
			{
				uint key = spawnPrefab.GetEntity().prefabID;
				if (!scientistPrefabLookUp.ContainsKey(key))
				{
					scientistPrefabLookUp.Add(key, spawnPrefab);
				}
			}
		}
	}

	private void TrySpawnScientists(BasePlayer triggeringPlayer)
	{
		if (!(triggeringPlayer == null) && !deployingScientists && !mountingScientists && !((float)timeSinceDeployCheck <= DeployOnDamageCheckInterval))
		{
			timeSinceDeployCheck = 0f;
			List<ScientistSpawnGroup> obj = GetTriggereringSpawnGroups();
			List<GameObjectRef> obj2 = Facepunch.Pool.Get<List<GameObjectRef>>();
			AddMountedScientistsToSpawn(obj2);
			AddSpawnGroupSpawns(obj, obj2);
			if (obj2.Count == 0)
			{
				Facepunch.Pool.FreeUnmanaged(ref obj2);
				Facepunch.Pool.FreeUnmanaged(ref obj);
			}
			else if (CanDeployScientists(triggeringPlayer, obj2, scientistSpawnPositions))
			{
				SetSpawnGroupsAsSpawned(obj);
				Facepunch.Pool.FreeUnmanaged(ref obj);
				ClearMountedScientists();
				StartCoroutine(DeployScientists(triggeringPlayer, obj2, scientistSpawnPositions));
			}
			else
			{
				Facepunch.Pool.FreeUnmanaged(ref obj2);
				Facepunch.Pool.FreeUnmanaged(ref obj);
			}
		}
	}

	private List<ScientistSpawnGroup> GetTriggereringSpawnGroups()
	{
		List<ScientistSpawnGroup> list = Facepunch.Pool.Get<List<ScientistSpawnGroup>>();
		foreach (ScientistSpawnGroup scientistSpawn in ScientistSpawns)
		{
			if (!scientistSpawn.Spawned && !(base.healthFraction > scientistSpawn.BradleyHealth))
			{
				list.Add(scientistSpawn);
			}
		}
		return list;
	}

	private void AddMountedScientistsToSpawn(List<GameObjectRef> scientists)
	{
		if (mountedScientistPrefabs.Count != 0)
		{
			scientists.AddRange(mountedScientistPrefabs);
		}
	}

	private void ClearMountedScientists()
	{
		mountedScientistPrefabs.Clear();
	}

	private void AddSpawnGroupSpawns(List<ScientistSpawnGroup> spawnGroups, List<GameObjectRef> scientists)
	{
		if (spawnGroups == null)
		{
			return;
		}
		foreach (ScientistSpawnGroup spawnGroup in spawnGroups)
		{
			if (spawnGroup != null)
			{
				scientists.AddRange(spawnGroup.SpawnPrefabs);
			}
		}
	}

	private void SetSpawnGroupsAsSpawned(List<ScientistSpawnGroup> spawnGroups)
	{
		if (spawnGroups == null)
		{
			return;
		}
		foreach (ScientistSpawnGroup spawnGroup in spawnGroups)
		{
			if (spawnGroup != null)
			{
				spawnGroup.Spawned = true;
			}
		}
	}

	private void UpdateDeployed()
	{
		if (!mountingScientists)
		{
			bool flag = false;
			float num = (UseSmokeGrenades ? 8f : 5f);
			if ((float)timeSinceScientistDeploy > num && AliveScientistCount() == 0)
			{
				flag = true;
			}
			else if (targetList.Count == 0 && (float)timeSinceValidTarget > MountAfterNotTargetsDuration)
			{
				flag = true;
			}
			else if (base.SecondsSinceAttacked > MountAfterNotAttackedDuration && (float)timeSinceScientistDeploy > MountAfterNotAttackedDuration)
			{
				flag = true;
			}
			else if (UnableToFireAtPlayers())
			{
				flag = true;
			}
			if (flag)
			{
				StartCoroutine(RecallSpawnedScientists());
			}
		}
	}

	private bool UnableToFireAtPlayers()
	{
		if ((float)deployedTimeSinceBradleyAttackedTarget < MountAfterNotFiredDuration)
		{
			return false;
		}
		foreach (ScientistNPC activeScientist in activeScientists)
		{
			if (!(activeScientist == null) && activeScientist.SecondsSinceDealtDamage < MountAfterNotFiredDuration)
			{
				return false;
			}
		}
		return true;
	}

	private void UpdateMountScientists()
	{
		if (ActiveScientistCount() <= 0)
		{
			AIRoot.SetActive(value: false);
			SetMountingScientists(flag: false);
			inDeployedState = false;
			SetDeployingScientists(flag: false);
			activeScientists.Clear();
			timeSinceScientistDeploy = 0f;
		}
	}

	public int ActiveScientistCount()
	{
		int num = 0;
		foreach (ScientistNPC activeScientist in activeScientists)
		{
			if (!(activeScientist == null))
			{
				num++;
			}
		}
		return num;
	}

	public int AliveScientistCount()
	{
		if (inDeployedState)
		{
			return ActiveScientistCount();
		}
		return numberOfScientistsToSpawn;
	}

	private bool CanDeployScientists(BaseEntity attacker, List<GameObjectRef> scientistPrefabs, List<Vector3> spawnPositions)
	{
		int count = scientistPrefabs.Count;
		if (!inDeployedState && Vector3.Distance(attacker.transform.position, base.transform.position) > DeployAttackDistanceMax)
		{
			return false;
		}
		spawnPositions.Clear();
		bool flag = false;
		int num = 0;
		int num2 = 0;
		int layerMask = 8454144;
		while (!flag)
		{
			if (UnityEngine.Physics.Raycast(ScientistSpawnPoints[num2 % ScientistSpawnPoints.Count].transform.position + Vector3.up * 1f, Vector3.down, out var hitInfo, 2f, layerMask) && NavMesh.SamplePosition(hitInfo.point + Vector3.up * 0.3f, out var _, 6f, walkableAreaMask))
			{
				spawnPositions.Add(hitInfo.point + Vector3.up * 0.1f);
				num2++;
				if (num2 >= count)
				{
					break;
				}
			}
			else
			{
				num++;
				if (num > count * 2)
				{
					flag = true;
				}
			}
		}
		return !flag;
	}

	private IEnumerator DeployScientists(BasePlayer triggerPlayer, List<GameObjectRef> scientistPrefabs, List<Vector3> spawnPositions)
	{
		if (base.isClient || spawnPositions == null || spawnPositions.Count == 0)
		{
			Facepunch.Pool.FreeUnmanaged(ref scientistPrefabs);
			yield break;
		}
		deployedTimeSinceBradleyAttackedTarget = 0f;
		timeSinceScientistDeploy = 0f;
		timeSinceValidTarget = 0f;
		AIRoot.SetActive(value: true);
		SetMountingScientists(flag: false);
		inDeployedState = true;
		SetDeployingScientists(flag: true);
		if (UseSmokeGrenades)
		{
			DropSmokeGrenade(spawnPositions[0], 6f);
			yield return new WaitForSeconds(3f);
		}
		yield return new WaitForEndOfFrame();
		yield return new WaitForEndOfFrame();
		int index = 0;
		foreach (Vector3 spawnPos in spawnPositions)
		{
			ScientistNPC scientist = SpawnScientist(scientistPrefabs[index], spawnPos, RoadSpawned);
			index++;
			yield return new WaitForEndOfFrame();
			InitScientist(scientist, spawnPos, triggerPlayer, RoadSpawned, index % 2 == 0);
			yield return new WaitForSeconds(DeployInterval);
		}
		SetDeployingScientists(flag: false);
		Facepunch.Pool.FreeUnmanaged(ref scientistPrefabs);
	}

	private void SetDeployingScientists(bool flag)
	{
		deployingScientists = flag;
	}

	private void SetMountingScientists(bool flag)
	{
		mountingScientists = flag;
	}

	private ScientistNPC SpawnScientist(GameObjectRef scientistPrefab, Vector3 spawnPos, bool roadSpawned)
	{
		ScientistNPC scientistNPC = GameManager.server.CreateEntity(scientistPrefab.resourcePath, spawnPos, Quaternion.identity) as ScientistNPC;
		scientistNPC.VirtualInfoZone = AIRoot.GetComponent<AIInformationZone>();
		scientistNPC.GetComponent<ScientistBrain>().MovementTickStartDelay = 0f;
		NavMeshAgent component = scientistNPC.GetComponent<NavMeshAgent>();
		if (component != null)
		{
			NPCPlayerNavigator component2 = scientistNPC.GetComponent<NPCPlayerNavigator>();
			component.agentTypeID = (roadSpawned ? BaseNavigator.GetNavMeshAgentID("Animal") : BaseNavigator.GetNavMeshAgentID("Humanoid"));
			component2.DefaultArea = (roadSpawned ? "Walkable" : "HumanNPC");
		}
		scientistNPC.Spawn();
		scientistNPC.EquipTest();
		activeScientists.Add(scientistNPC);
		return scientistNPC;
	}

	private void InitScientist(ScientistNPC scientist, Vector3 spawnPos, BasePlayer triggerPlayer, bool roadSpawned, bool startChasing)
	{
		if (scientist == null)
		{
			return;
		}
		scientist.transform.position = spawnPos;
		if (!scientist.Brain.Navigator.PlaceOnNavMesh(0.2f))
		{
			activeScientists.Remove(scientist);
			scientist.Kill();
		}
		else if (triggerPlayer != null)
		{
			scientist.Brain.Events.Memory.Entity.Set(triggerPlayer, 0);
			scientist.Brain.Senses.Memory.SetKnown(triggerPlayer, scientist, null);
			scientist.Brain.Events.Memory.Position.Set(scientist.Brain.Navigator.transform.position, 7);
			scientist.Brain.Events.Memory.Position.Set(scientist.Brain.Navigator.transform.position, 4);
			scientist.Brain.Events.Memory.Entity.Set(this, 7);
			AttackEntity attackEntity = scientist.GetAttackEntity();
			if (SetScientistChaseBasedOnWeapon && attackEntity != null && !attackEntity.CanUseAtLongRange)
			{
				startChasing = true;
			}
			scientist.Brain.Navigator.CanPathFindToChaseTargetIfNoMovePoint = startChasing;
			scientist.Brain.Navigator.CanUseRandomMovePointIfNonFound = !startChasing;
			if (startChasing)
			{
				scientist.Brain.SwitchToState(AIState.Chase, 6);
			}
			else
			{
				scientist.Brain.SwitchToState(AIState.TakeCover, 4);
			}
			scientist.Brain.Think(0f);
		}
	}

	private void DropSmokeGrenade(Vector3 position, float duration)
	{
		SmokeGrenade component = GameManager.server.CreateEntity(SmokeGrenadePrefab.resourcePath, position, Quaternion.identity).GetComponent<SmokeGrenade>();
		component.smokeDuration = duration;
		component.Spawn();
	}

	private void KillSpawnedScientists()
	{
		foreach (ScientistNPC activeScientist in activeScientists)
		{
			if (!(activeScientist == null))
			{
				if (KillScientistsOnBradleyDeath)
				{
					activeScientist.Kill();
				}
				else
				{
					activeScientist.Brain.LoadAIDesignAtIndex(1);
				}
			}
		}
		activeScientists.Clear();
		numberOfScientistsToSpawn = 0;
	}

	private IEnumerator RecallSpawnedScientists()
	{
		if (!inDeployedState || mountingScientists)
		{
			yield break;
		}
		int num = 0;
		foreach (ScientistNPC activeScientist in activeScientists)
		{
			if (!(activeScientist == null) && activeScientist.IsAlive())
			{
				num++;
			}
		}
		numberOfScientistsToSpawn = 0;
		SetMountingScientists(flag: true);
		if (num > 0 && UseSmokeGrenades)
		{
			DropSmokeGrenade(ScientistSpawnPoints[0].transform.position, 10f);
			yield return new WaitForSeconds(3f);
		}
		foreach (ScientistNPC activeScientist2 in activeScientists)
		{
			if (!(activeScientist2 == null))
			{
				activeScientist2.Brain.SwitchToState(AIState.MoveToVector3, 8);
				activeScientist2.Brain.Think(0f);
			}
		}
	}

	public void OnScientistMounted(ScientistNPC scientist)
	{
		if (!(scientist == null))
		{
			if (scientistPrefabLookUp.TryGetValue(scientist.prefabID, out var value))
			{
				mountedScientistPrefabs.Add(value);
			}
			activeScientists.Remove(scientist);
			numberOfScientistsToSpawn++;
		}
	}

	public void AddOrUpdateTarget(BaseEntity ent, Vector3 pos, float damageFrom = 0f)
	{
		if ((AI.ignoreplayers && !ent.IsNpc) || !(ent is BasePlayer item) || SimpleAIMemory.PlayerIgnoreList.Contains(item) || ent is ScientistNPC)
		{
			return;
		}
		TargetInfo targetInfo = null;
		foreach (TargetInfo target in targetList)
		{
			if (target.entity == ent)
			{
				targetInfo = target;
				break;
			}
		}
		if (targetInfo == null)
		{
			targetInfo = Facepunch.Pool.Get<TargetInfo>();
			targetInfo.Setup(ent, UnityEngine.Time.time - 1f);
			targetList.Add(targetInfo);
		}
		targetInfo.lastSeenPosition = pos;
		targetInfo.damageReceivedFrom += damageFrom;
	}

	public void UpdateTargetList()
	{
		List<BaseEntity> obj = Facepunch.Pool.Get<List<BaseEntity>>();
		Vis.Entities(base.transform.position, searchRange, obj, 133120);
		foreach (BaseEntity item in obj)
		{
			if ((AI.ignoreplayers && !item.IsNpc) || !(item is BasePlayer))
			{
				continue;
			}
			BasePlayer basePlayer = item as BasePlayer;
			if (SimpleAIMemory.PlayerIgnoreList.Contains(basePlayer) || basePlayer.IsDead() || basePlayer is HumanNPC || basePlayer is NPCPlayer || (basePlayer.InSafeZone() && !basePlayer.IsHostile()) || !VisibilityTest(item))
			{
				continue;
			}
			bool flag = false;
			foreach (TargetInfo target in targetList)
			{
				if (target.entity == item)
				{
					target.lastSeenTime = UnityEngine.Time.time;
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				TargetInfo targetInfo = Facepunch.Pool.Get<TargetInfo>();
				targetInfo.Setup(item, UnityEngine.Time.time);
				targetList.Add(targetInfo);
			}
		}
		for (int num = targetList.Count - 1; num >= 0; num--)
		{
			TargetInfo obj2 = targetList[num];
			BasePlayer basePlayer2 = obj2.entity as BasePlayer;
			if (obj2.entity == null || UnityEngine.Time.time - obj2.lastSeenTime > memoryDuration || basePlayer2.IsDead() || (basePlayer2.InSafeZone() && !basePlayer2.IsHostile()) || (AI.ignoreplayers && !basePlayer2.IsNpc) || SimpleAIMemory.PlayerIgnoreList.Contains(basePlayer2))
			{
				targetList.Remove(obj2);
				Facepunch.Pool.Free(ref obj2);
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		targetList.Sort(SortTargets);
		if (targetList.Count > 0)
		{
			timeSinceValidTarget = 0f;
		}
	}

	public int SortTargets(TargetInfo t1, TargetInfo t2)
	{
		return t2.GetPriorityScore(this).CompareTo(t1.GetPriorityScore(this));
	}

	public Vector3 GetAimPoint(BaseEntity ent)
	{
		BasePlayer basePlayer = ent as BasePlayer;
		if (basePlayer != null)
		{
			return basePlayer.eyes.position;
		}
		return ent.CenterPoint();
	}

	public bool VisibilityTest(BaseEntity ent)
	{
		if (ent == null)
		{
			return false;
		}
		if (!(Vector3.Distance(ent.transform.position, base.transform.position) < viewDistance))
		{
			return false;
		}
		bool flag = false;
		if (ent is BasePlayer)
		{
			BasePlayer basePlayer = ent as BasePlayer;
			Vector3 position = mainTurret.transform.position;
			flag = IsVisible(basePlayer.eyes.position, position) || IsVisible(basePlayer.transform.position + Vector3.up * 0.1f, position);
			if (!flag && basePlayer.isMounted && basePlayer.GetMounted().VehicleParent() != null && basePlayer.GetMounted().VehicleParent().AlwaysAllowBradleyTargeting)
			{
				flag = IsVisible(basePlayer.GetMounted().VehicleParent().bounds.center, position);
			}
			if (flag)
			{
				flag = !UnityEngine.Physics.SphereCast(new Ray(position, Vector3Ex.Direction(basePlayer.eyes.position, position)), 0.05f, Vector3.Distance(basePlayer.eyes.position, position), 10551297);
			}
		}
		else
		{
			Debug.LogWarning("Standard vis test!");
			flag = IsVisible(ent.CenterPoint());
		}
		return flag;
	}

	public void UpdateTargetVisibilities()
	{
		foreach (TargetInfo target in targetList)
		{
			if (target.IsValid() && VisibilityTest(target.entity))
			{
				target.lastSeenTime = UnityEngine.Time.time;
				target.lastSeenPosition = target.entity.transform.position;
			}
		}
	}

	public void DoWeaponAiming()
	{
		desiredAimVector = ((mainGunTarget != null) ? (GetAimPoint(mainGunTarget) - mainTurretEyePos.transform.position).normalized : desiredAimVector);
		BaseEntity baseEntity = null;
		if (targetList.Count > 0)
		{
			if (targetList.Count > 1 && targetList[1].IsValid() && targetList[1].IsVisible())
			{
				baseEntity = targetList[1].entity;
			}
			else if (targetList[0].IsValid() && targetList[0].IsVisible())
			{
				baseEntity = targetList[0].entity;
			}
		}
		desiredTopTurretAimVector = ((baseEntity != null) ? (GetAimPoint(baseEntity) - topTurretEyePos.transform.position).normalized : base.transform.forward);
	}

	public void DoWeapons()
	{
		if (mainGunTarget != null && Vector3.Dot(turretAimVector, (GetAimPoint(mainGunTarget) - mainTurretEyePos.transform.position).normalized) >= 0.99f)
		{
			bool flag = VisibilityTest(mainGunTarget);
			float num = Vector3.Distance(mainGunTarget.transform.position, base.transform.position);
			if (UnityEngine.Time.time > nextCoaxTime && flag && num <= 40f)
			{
				numCoaxBursted++;
				FireGun(GetAimPoint(mainGunTarget), 3f, isCoax: true);
				nextCoaxTime = UnityEngine.Time.time + coaxFireRate;
				if (numCoaxBursted >= coaxBurstLength)
				{
					nextCoaxTime = UnityEngine.Time.time + 1f;
					numCoaxBursted = 0;
				}
			}
			if (num >= 10f && flag)
			{
				FireGunTest();
			}
		}
		if (targetList.Count > 1)
		{
			BaseEntity entity = targetList[1].entity;
			if (entity != null && UnityEngine.Time.time > nextTopTurretTime && VisibilityTest(entity))
			{
				FireGun(GetAimPoint(targetList[1].entity), 3f, isCoax: false);
				nextTopTurretTime = UnityEngine.Time.time + topTurretFireRate;
			}
		}
	}

	public void FireGun(Vector3 targetPos, float aimCone, bool isCoax)
	{
		deployedTimeSinceBradleyAttackedTarget = 0f;
		Transform transform = (isCoax ? coaxMuzzle : topTurretMuzzle);
		Vector3 vector = transform.transform.position - transform.forward * 0.25f;
		Vector3 normalized = (targetPos - vector).normalized;
		Vector3 modifiedAimConeDirection = AimConeUtil.GetModifiedAimConeDirection(aimCone, normalized);
		targetPos = vector + modifiedAimConeDirection * 300f;
		List<RaycastHit> obj = Facepunch.Pool.Get<List<RaycastHit>>();
		GamePhysics.TraceAll(new Ray(vector, modifiedAimConeDirection), 0f, obj, 300f, 1220225809);
		for (int i = 0; i < obj.Count; i++)
		{
			RaycastHit hit = obj[i];
			BaseEntity entity = hit.GetEntity();
			if ((!(entity != null) || (!(entity == this) && !entity.EqualNetID(this))) && !(entity is ScientistNPC))
			{
				BaseCombatEntity baseCombatEntity = entity as BaseCombatEntity;
				if (baseCombatEntity != null)
				{
					ApplyDamage(baseCombatEntity, hit.point, modifiedAimConeDirection);
				}
				if (!(entity != null) || entity.ShouldBlockProjectiles())
				{
					targetPos = hit.point;
					break;
				}
			}
		}
		ClientRPC(RpcTarget.NetworkGroup("CLIENT_FireGun"), isCoax, targetPos);
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	private void ApplyDamage(BaseCombatEntity entity, Vector3 point, Vector3 normal)
	{
		float damageAmount = bulletDamage * UnityEngine.Random.Range(0.9f, 1.1f);
		HitInfo info = new HitInfo(this, entity, DamageType.Bullet, damageAmount, point);
		entity.OnAttacked(info);
		if (entity is BasePlayer || entity is BaseNpc)
		{
			Effect.server.ImpactEffect(new HitInfo
			{
				HitPositionWorld = point,
				HitNormalWorld = -normal,
				HitMaterial = StringPool.Get("Flesh")
			});
		}
	}

	public void AimWeaponAt(Transform weaponYaw, Transform weaponPitch, Vector3 direction, float minPitch = -360f, float maxPitch = 360f, float maxYaw = 360f, Transform parentOverride = null)
	{
		Vector3 direction2 = direction;
		direction2 = weaponYaw.parent.InverseTransformDirection(direction2);
		Quaternion localRotation = Quaternion.LookRotation(direction2);
		Vector3 eulerAngles = localRotation.eulerAngles;
		for (int i = 0; i < 3; i++)
		{
			eulerAngles[i] -= ((eulerAngles[i] > 180f) ? 360f : 0f);
		}
		Quaternion localRotation2 = Quaternion.Euler(0f, Mathf.Clamp(eulerAngles.y, 0f - maxYaw, maxYaw), 0f);
		Quaternion localRotation3 = Quaternion.Euler(Mathf.Clamp(eulerAngles.x, minPitch, maxPitch), 0f, 0f);
		if (weaponYaw == null && weaponPitch != null)
		{
			weaponPitch.transform.localRotation = localRotation3;
			return;
		}
		if (weaponPitch == null && weaponYaw != null)
		{
			weaponYaw.transform.localRotation = localRotation;
			return;
		}
		weaponYaw.transform.localRotation = localRotation2;
		weaponPitch.transform.localRotation = localRotation3;
	}

	public void LateUpdate()
	{
		float num = UnityEngine.Time.time - lastLateUpdate;
		lastLateUpdate = UnityEngine.Time.time;
		if (base.isServer)
		{
			float num2 = MathF.PI * 2f / 3f;
			turretAimVector = Vector3.RotateTowards(turretAimVector, desiredAimVector, num2 * num, 0f);
		}
		else
		{
			turretAimVector = Vector3.Lerp(turretAimVector, desiredAimVector, UnityEngine.Time.deltaTime * 10f);
		}
		AimWeaponAt(mainTurret, coaxPitch, turretAimVector, -90f, 90f);
		AimWeaponAt(mainTurret, CannonPitch, turretAimVector, -90f, 7f);
		topTurretAimVector = Vector3.Lerp(topTurretAimVector, desiredTopTurretAimVector, UnityEngine.Time.deltaTime * 5f);
		AimWeaponAt(topTurretYaw, topTurretPitch, topTurretAimVector, -360f, 360f, 360f, mainTurret);
	}
}
