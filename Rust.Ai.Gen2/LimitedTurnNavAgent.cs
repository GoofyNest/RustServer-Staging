using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Rust.Ai.Gen2;

[SoftRequireComponent(typeof(NavMeshAgent))]
public class LimitedTurnNavAgent : EntityComponent<BaseEntity>, IServerComponent
{
	public enum Speeds
	{
		Sneak,
		Walk,
		Jog,
		Run,
		Sprint,
		FullSprint
	}

	[SerializeField]
	private NavMeshAgent agent;

	[Header("Speed")]
	[SerializeField]
	private float sneakSpeed = 0.6f;

	[SerializeField]
	private float walkSpeed = 0.89f;

	[SerializeField]
	private float jogSpeed = 2.45f;

	[SerializeField]
	private float runSpeed = 4.4f;

	[SerializeField]
	private float sprintSpeed = 6f;

	[SerializeField]
	private float fullSprintSpeed = 9f;

	[SerializeField]
	public ResettableFloat acceleration = new ResettableFloat(10f);

	[SerializeField]
	public ResettableFloat deceleration = new ResettableFloat(2f);

	[SerializeField]
	private float maxTurnRadius = 2f;

	[SerializeField]
	private TerrainTopology.Enum preferedTopology = TerrainTopology.Enum.Field | TerrainTopology.Enum.Forest | TerrainTopology.Enum.Forestside | TerrainTopology.Enum.Lakeside | TerrainTopology.Enum.Mainland;

	[SerializeField]
	private TerrainBiome.Enum preferedBiome = TerrainBiome.Enum.Arid | TerrainBiome.Enum.Temperate | TerrainBiome.Enum.Tundra | TerrainBiome.Enum.Arctic;

	private static NavMeshPath path;

	[NonSerialized]
	public UnityEvent onPathFailed = new UnityEvent();

	private LockState movementLock = new LockState();

	private bool isNavMeshReady;

	private static ListHashSet<LimitedTurnNavAgent> steeringComponents = new ListHashSet<LimitedTurnNavAgent>();

	[NonSerialized]
	public float currentDeviation;

	[NonSerialized]
	public bool shouldStopAtDestination = true;

	private float cachedPathLength;

	private Vector3? previousLocalPosition;

	private float curSpeed;

	private float desiredSpeed;

	public bool IsNavmeshReady => isNavMeshReady;

	public Vector3? lastValidDestination { get; private set; }

	public bool IsFollowingPath
	{
		get
		{
			if (agent.hasPath)
			{
				return agent.remainingDistance > (shouldStopAtDestination ? base.baseEntity.bounds.extents.z : maxTurnRadius);
			}
			return false;
		}
	}

	public LockState.LockHandle Pause()
	{
		if (!movementLock.IsLocked)
		{
			OnPaused();
		}
		return movementLock.AddLock();
	}

	public bool Unpause(ref LockState.LockHandle handle)
	{
		bool result = movementLock.RemoveLock(ref handle);
		if (!movementLock.IsLocked)
		{
			OnUnpaused();
		}
		return result;
	}

	public void Move(Vector3 offset)
	{
		using (TimeWarning.New("LimitedTurnNavAgent:Move"))
		{
			agent.Move(offset);
		}
	}

	public void ResetPath()
	{
		using (TimeWarning.New("LimitedTurnNavAgent:ResetPath"))
		{
			shouldStopAtDestination = true;
			acceleration.Reset();
			deceleration.Reset();
			currentDeviation = 0f;
			if (agent.hasPath)
			{
				agent.ResetPath();
			}
		}
	}

	public bool CanReach(Vector3 location, bool triggerPathFailed = false)
	{
		using (TimeWarning.New("LimitedTurnNavAgent:CanReach"))
		{
			if (!IsPositionOnNavmesh(location, out var sample))
			{
				if (triggerPathFailed)
				{
					FailPath(location);
				}
				return false;
			}
			if (!CalculatePathCustom(sample, path))
			{
				if (triggerPathFailed)
				{
					FailPath(sample, path);
				}
				return false;
			}
			bool flag = path.status == NavMeshPathStatus.PathComplete;
			if (!flag && triggerPathFailed)
			{
				FailPath(sample, path);
			}
			else if (flag && triggerPathFailed)
			{
				lastValidDestination = path.GetDestination();
			}
			return flag;
		}
	}

	public bool SetDestination(Vector3 newDestination, bool acceptPartialPaths = false)
	{
		using (TimeWarning.New("LimitedTurnNavAgent:SetDestination"))
		{
			if (shouldStopAtDestination && agent.hasPath && Vector3.Distance(agent.destination, newDestination) < 1f)
			{
				return true;
			}
			if (!CalculatePathCustom(newDestination, path))
			{
				FailPath(newDestination, path);
				return false;
			}
			if (!(acceptPartialPaths ? (path.status != NavMeshPathStatus.PathInvalid) : (path.status == NavMeshPathStatus.PathComplete)))
			{
				FailPath(newDestination, path);
				return false;
			}
			SetPath(path);
			return true;
		}
	}

	public bool SetDestinationFromDirection(Vector3 normalizedDirection, float distance = 10f, bool restrictTerrain = false)
	{
		using (TimeWarning.New("LimitedTurnNavAgent:SetDestinationFromDirection"))
		{
			using PooledList<Vector3> pooledList = Facepunch.Pool.Get<PooledList<Vector3>>();
			SamplePositionsInDonutShape(base.baseEntity.transform.position, pooledList, distance, distance);
			using PooledList<(Vector3, float)> pooledList2 = Facepunch.Pool.Get<PooledList<(Vector3, float)>>();
			foreach (Vector3 item in pooledList)
			{
				float num = 0f;
				if (restrictTerrain && !IsPositionOnValidTerrain(item))
				{
					num -= 100f;
				}
				if (!restrictTerrain)
				{
					num -= WaterLevel.GetOverallWaterDepth(item, waves: true, volumes: false) * 10f;
				}
				num += Vector3.Dot((item - base.baseEntity.transform.position).normalized, normalizedDirection);
				pooledList2.Add((item, num));
			}
			pooledList2.Sort(((Vector3 position, float score) a, (Vector3 position, float score) b) => b.score.CompareTo(a.score));
			for (int i = 0; i < pooledList2.Count; i++)
			{
				pooledList[i] = pooledList2[i].Item1;
			}
			if (!GetFirstReachablePoint(pooledList, ref path))
			{
				FailPath(null);
				return false;
			}
			SetPath(path);
			return true;
		}
	}

	public override void InitShared()
	{
		base.InitShared();
		if (path == null)
		{
			path = new NavMeshPath();
		}
	}

	private void OnPaused()
	{
		if (agent.enabled && agent.isOnNavMesh)
		{
			ResetPath();
		}
	}

	private void OnUnpaused()
	{
	}

	private void SetPath(NavMeshPath newPath)
	{
		using (TimeWarning.New("LimitedTurnNavAgent:SetPath"))
		{
			if (agent.path != newPath)
			{
				agent.SetPath(newPath);
			}
			cachedPathLength = newPath.GetPathLength();
			lastValidDestination = newPath.GetDestination();
		}
	}

	private void ShowFailedPath(Vector3? destination, NavMeshPath failedPath)
	{
	}

	private void FailPath(Vector3? destination, NavMeshPath failedPath = null)
	{
		ShowFailedPath(destination, failedPath);
		onPathFailed.Invoke();
		ResetPath();
	}

	public void SetSpeed(Speeds speed)
	{
		switch (speed)
		{
		case Speeds.Sneak:
			desiredSpeed = sneakSpeed;
			break;
		case Speeds.Walk:
			desiredSpeed = walkSpeed;
			break;
		case Speeds.Jog:
			desiredSpeed = jogSpeed;
			break;
		case Speeds.Run:
			desiredSpeed = runSpeed;
			break;
		case Speeds.Sprint:
			desiredSpeed = sprintSpeed;
			break;
		case Speeds.FullSprint:
			desiredSpeed = fullSprintSpeed;
			break;
		default:
			desiredSpeed = walkSpeed;
			break;
		}
	}

	public void SetSpeed(float ratio, Speeds minSpeed = Speeds.Sneak, Speeds maxSpeed = Speeds.Sprint, int offset = 0)
	{
		int num = Mathf.FloorToInt(Mathf.Lerp((float)minSpeed, (float)maxSpeed, ratio));
		num = Mathf.Clamp(num + offset, (int)minSpeed, (int)maxSpeed);
		SetSpeed((Speeds)num);
	}

	private void OnEnable()
	{
		steeringComponents.TryAdd(this);
	}

	private void OnDisable()
	{
		steeringComponents.Remove(this);
	}

	public static void TickSteering()
	{
		for (int num = steeringComponents.Count - 1; num >= 0; num--)
		{
			LimitedTurnNavAgent limitedTurnNavAgent = steeringComponents[num];
			if (limitedTurnNavAgent.IsUnityNull() || !limitedTurnNavAgent.baseEntity.IsValid())
			{
				steeringComponents.RemoveAt(num);
			}
			else
			{
				limitedTurnNavAgent.Tick();
			}
		}
	}

	private void Tick()
	{
		using (TimeWarning.New("LimitedTurnNavAgent:Tick"))
		{
			try
			{
				if (!AI.move)
				{
					return;
				}
				if (!isNavMeshReady)
				{
					isNavMeshReady = agent != null && agent.enabled && agent.isOnNavMesh;
					if (!isNavMeshReady)
					{
						return;
					}
					agent.updateRotation = false;
					agent.updateUpAxis = false;
					agent.isStopped = true;
				}
				if (movementLock.IsLocked)
				{
					if (previousLocalPosition.HasValue)
					{
						curSpeed = (base.baseEntity.transform.localPosition - previousLocalPosition.Value).magnitude / UnityEngine.Time.deltaTime;
					}
				}
				else if (!shouldStopAtDestination || IsFollowingPath)
				{
					SteerTowardsWaypoint();
				}
				else
				{
					curSpeed = 0f;
					ResetPath();
				}
			}
			finally
			{
				previousLocalPosition = base.baseEntity.transform.localPosition;
			}
		}
	}

	private static float GetBrakingDistance(float speed, float brakingDeceleration)
	{
		float num = speed / Mathf.Max(brakingDeceleration, 0.001f);
		return 0.5f * brakingDeceleration * num * num;
	}

	private void SteerTowardsWaypoint()
	{
		using (TimeWarning.New("SteerTowardsWaypoint"))
		{
			Transform transform = base.baseEntity.transform;
			Vector3 vector = (agent.steeringTarget - transform.position).normalized;
			if (Mathf.Abs(cachedPathLength - Vector3.Distance(transform.position, agent.destination)) < 5f)
			{
				vector = Quaternion.AngleAxis(currentDeviation, Vector3.up) * vector;
			}
			if (shouldStopAtDestination && agent.remainingDistance - maxTurnRadius < GetBrakingDistance(curSpeed, deceleration.Value))
			{
				curSpeed = Mathf.Max(1f, curSpeed - deceleration.Value * UnityEngine.Time.deltaTime);
			}
			else if (curSpeed > desiredSpeed)
			{
				curSpeed = Mathf.Max(desiredSpeed, curSpeed - deceleration.Value * UnityEngine.Time.deltaTime);
			}
			else if (curSpeed < desiredSpeed)
			{
				curSpeed = Mathf.Min(desiredSpeed, curSpeed + acceleration.Value * UnityEngine.Time.deltaTime);
			}
			agent.isStopped = true;
			if (!(vector.magnitude < 0.01f))
			{
				float num = (shouldStopAtDestination ? Mathx.RemapValClamped(agent.remainingDistance, maxTurnRadius * 2f, 0f, maxTurnRadius, 0.001f) : maxTurnRadius);
				float num2 = curSpeed / num;
				Vector3 vector2 = Vector3.RotateTowards(transform.forward, vector, num2 * UnityEngine.Time.deltaTime, 0f);
				Vector3 offset = vector2 * (curSpeed * UnityEngine.Time.deltaTime);
				transform.rotation = Quaternion.LookRotation(vector2.WithY(0f));
				Move(offset);
			}
		}
	}

	public bool IsPositionOnValidTerrain(Vector3 position)
	{
		using (TimeWarning.New("IsPositionOnValidTerrain"))
		{
			return IsPositionAtTopologyRequirement(base.baseEntity, position, preferedTopology) && IsPositionABiomeRequirement(base.baseEntity, position, preferedBiome) && IsAcceptableWaterDepth(base.baseEntity, position);
		}
	}

	public bool IsPositionOnNavmesh(Vector3 position, out Vector3 sample)
	{
		return SamplePosition(position, out sample, 0.5f);
	}

	public bool SampleGroundPositionWithPhysics(Vector3 position, out Vector3 sample, float maxDistance, float radius = 0f)
	{
		using (TimeWarning.New("SampleGroundPositionWithPhysics"))
		{
			sample = position;
			Vector3 origin = position + Vector3.up * radius * 1.5f;
			float maxDistance2 = maxDistance + radius * 1.5f;
			if (GamePhysics.Trace(new Ray(origin, Vector3.down), radius, out var hitInfo, maxDistance2, 1503731969, QueryTriggerInteraction.Ignore))
			{
				if (radius == 0f || hitInfo.distance > 0f)
				{
					sample = hitInfo.point;
				}
				return true;
			}
			return false;
		}
	}

	public bool SamplePosition(Vector3 position, out Vector3 sample, float maxDistance)
	{
		using (TimeWarning.New("SamplePosition"))
		{
			sample = position;
			if (!NavMesh.SamplePosition(position, out var hit, maxDistance, agent.areaMask))
			{
				return false;
			}
			sample = hit.position;
			return hit.hit;
		}
	}

	public void SamplePositionsInDonutShape(Vector3 center, List<Vector3> sampledPositions, float outerRadius = 10f, float innerRadius = 10f, int numRings = 1, int itemsPerRing = 8)
	{
		using (TimeWarning.New("SamplePositionsInDonutShape"))
		{
			for (int i = 0; i < numRings; i++)
			{
				float num = ((numRings != 1) ? Mathf.Lerp(innerRadius, outerRadius, (float)i / (float)(numRings - 1)) : outerRadius);
				for (int j = 0; j < itemsPerRing; j++)
				{
					float num2 = (float)i * MathF.PI / (float)numRings;
					float f = MathF.PI * 2f * (float)j / (float)itemsPerRing + num2;
					Vector3 item = center + new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)) * num;
					sampledPositions.Add(item);
				}
			}
		}
	}

	public bool CalculatePathCustom(Vector3 destination, NavMeshPath path)
	{
		using (TimeWarning.New("CalculatePathCustom"))
		{
			return agent.CalculatePath(destination, path);
		}
	}

	public bool GetFirstReachablePoint(List<Vector3> points, ref NavMeshPath navPath)
	{
		using (TimeWarning.New("GetFirstReachablePoint"))
		{
			foreach (Vector3 point in points)
			{
				if (!SamplePosition(point, out var sample, 10f))
				{
					continue;
				}
				if (CalculatePathCustom(sample, navPath))
				{
					if (navPath.status == NavMeshPathStatus.PathComplete)
					{
						return true;
					}
				}
				else
				{
					ShowFailedPath(sample, navPath);
				}
			}
			return false;
		}
	}

	public static bool IsPositionAtTopologyRequirement(BaseEntity baseEntity, Vector3 position, TerrainTopology.Enum topologyRequirement)
	{
		using (TimeWarning.New("IsPositionAtTopologyRequirement"))
		{
			if (TerrainMeta.TopologyMap == null)
			{
				return false;
			}
			TerrainTopology.Enum topology = (TerrainTopology.Enum)TerrainMeta.TopologyMap.GetTopology(position);
			if ((topologyRequirement & topology) == 0)
			{
				return false;
			}
			return true;
		}
	}

	public static bool IsPositionABiomeRequirement(BaseEntity baseEntity, Vector3 position, TerrainBiome.Enum biomeRequirement)
	{
		using (TimeWarning.New("IsPositionABiomeRequirement"))
		{
			if (biomeRequirement == (TerrainBiome.Enum)0)
			{
				return true;
			}
			if (TerrainMeta.BiomeMap == null)
			{
				return false;
			}
			TerrainBiome.Enum biomeMaxType = (TerrainBiome.Enum)TerrainMeta.BiomeMap.GetBiomeMaxType(position);
			if ((biomeRequirement & biomeMaxType) == 0)
			{
				return false;
			}
			return true;
		}
	}

	public static bool IsAcceptableWaterDepth(BaseEntity baseEntity, Vector3 position, float maxDepth = 0.1f)
	{
		using (TimeWarning.New("IsAcceptableWaterDepth"))
		{
			if (WaterLevel.GetOverallWaterDepth(position, waves: false, volumes: false) > maxDepth)
			{
				return false;
			}
			return true;
		}
	}
}
