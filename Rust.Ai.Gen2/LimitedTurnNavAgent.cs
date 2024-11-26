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

	private static NavMeshPath path;

	[NonSerialized]
	public UnityEvent onPathFailed = new UnityEvent();

	private LockState movementLock = new LockState();

	private bool isNavMeshReady;

	private Queue<Vector3> pendingDestinationCandidates = new Queue<Vector3>();

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
	private float sprintSpeed = 7.5f;

	[SerializeField]
	private float fullSprintSpeed = 9f;

	private float desiredSpeed;

	[SerializeField]
	public ResettableFloat acceleration = new ResettableFloat(10f);

	[SerializeField]
	public ResettableFloat deceleration = new ResettableFloat(2f);

	[SerializeField]
	private float maxTurnRadius = 2f;

	[NonSerialized]
	public float currentDeviation;

	[NonSerialized]
	public bool shouldStopAtDestination = true;

	private float cachedPathLength;

	private Vector3? previousLocalPosition;

	private float curSpeed;

	private static ListHashSet<LimitedTurnNavAgent> steeringComponents = new ListHashSet<LimitedTurnNavAgent>();

	[SerializeField]
	private TerrainTopology.Enum preferedTopology = TerrainTopology.Enum.Field | TerrainTopology.Enum.Forest | TerrainTopology.Enum.Forestside | TerrainTopology.Enum.Lakeside | TerrainTopology.Enum.Mainland;

	[SerializeField]
	private TerrainBiome.Enum preferedBiome = TerrainBiome.Enum.Arid | TerrainBiome.Enum.Temperate | TerrainBiome.Enum.Tundra | TerrainBiome.Enum.Arctic;

	public bool IsNavmeshReady => isNavMeshReady;

	public Vector3? lastValidDestination { get; private set; }

	public bool IsFollowingPath
	{
		get
		{
			if (pendingDestinationCandidates.Count == 0)
			{
				if (agent.hasPath)
				{
					return agent.remainingDistance > (shouldStopAtDestination ? base.baseEntity.bounds.extents.z : maxTurnRadius);
				}
				return false;
			}
			return true;
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
			pendingDestinationCandidates.Clear();
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
					FailPathAndClearPendingRequests(location);
				}
				return false;
			}
			if (!CalculatePathCustom(sample, path))
			{
				if (triggerPathFailed)
				{
					FailPathAndClearPendingRequests(sample, path);
				}
				return false;
			}
			bool flag = path.status == NavMeshPathStatus.PathComplete;
			if (!flag && triggerPathFailed)
			{
				FailPathAndClearPendingRequests(sample, path);
			}
			else if (flag && triggerPathFailed)
			{
				lastValidDestination = path.GetDestination();
			}
			return flag;
		}
	}

	public void SetDestinationAsync(Vector3 newDestination)
	{
		using (TimeWarning.New("LimitedTurnNavAgent:SetDestinationAsync"))
		{
			if (agent.pathPending)
			{
				if (!agent.SetDestination(newDestination))
				{
					FailPathAndClearPendingRequests(newDestination);
				}
			}
			else if (!shouldStopAtDestination || !agent.hasPath || !(Vector3.Distance(agent.destination, newDestination) < 1f))
			{
				if (!agent.SetDestination(newDestination))
				{
					FailPathAndClearPendingRequests(newDestination);
					return;
				}
				pendingDestinationCandidates.Clear();
				pendingDestinationCandidates.Enqueue(newDestination);
			}
		}
	}

	private void TickPathfinding()
	{
		if (pendingDestinationCandidates.Count == 0 || agent.pathPending)
		{
			return;
		}
		Vector3 result = pendingDestinationCandidates.Peek();
		if (agent.pathStatus == NavMeshPathStatus.PathComplete && Vector3.Distance(result, agent.pathEndPosition) <= 0.5f)
		{
			SetPathAndClearPendingRequests(agent.path);
			return;
		}
		if (pendingDestinationCandidates.Count == 1)
		{
			FailPathAndClearPendingRequests(result, agent.path);
			return;
		}
		pendingDestinationCandidates.Dequeue();
		while (pendingDestinationCandidates.TryPeek(out result) && !agent.SetDestination(result))
		{
			pendingDestinationCandidates.Dequeue();
		}
		if (pendingDestinationCandidates.Count == 0)
		{
			FailPathAndClearPendingRequests(result, agent.path);
		}
	}

	public void SetDestinationFromDirectionAsync(Vector3 normalizedDirection, float distance = 10f, float randomPct = 0f, bool restrictTerrain = false)
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
		foreach (Vector3 item2 in pooledList)
		{
			if (SamplePosition(item2, out var sample, 10f))
			{
				pendingDestinationCandidates.Enqueue(sample);
			}
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

	private void SetPathAndClearPendingRequests(NavMeshPath newPath)
	{
		using (TimeWarning.New("LimitedTurnNavAgent:SetPathAndClearPendingRequests"))
		{
			pendingDestinationCandidates.Clear();
			if (agent.path != newPath)
			{
				agent.SetPath(newPath);
			}
			cachedPathLength = newPath.GetPathLength();
			lastValidDestination = newPath.GetDestination();
		}
	}

	private void FailPathAndClearPendingRequests(Vector3? destination, NavMeshPath failedPath = null)
	{
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
					return;
				}
				TickPathfinding();
				if (!shouldStopAtDestination || IsFollowingPath)
				{
					SteerTowardsWaypoint();
					return;
				}
				curSpeed = 0f;
				ResetPath();
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
			return NavHelpers.IsPositionAtTopologyRequirement(base.baseEntity, position, preferedTopology) && NavHelpers.IsPositionABiomeRequirement(base.baseEntity, position, preferedBiome) && NavHelpers.IsAcceptableWaterDepth(base.baseEntity, position);
		}
	}

	public bool IsPositionOnNavmesh(Vector3 position, out Vector3 sample)
	{
		return SamplePosition(position, out sample, 0.5f);
	}

	public bool SampleGroundPositionWithPhysics(Vector3 position, out Vector3 sample, float maxDistance)
	{
		using (TimeWarning.New("SampleGroundPositionWithPhysics"))
		{
			sample = position;
			if (GamePhysics.Trace(new Ray(position, Vector3.down), 0f, out var hitInfo, maxDistance, 1503731969, QueryTriggerInteraction.Ignore))
			{
				sample = hitInfo.point;
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
				if (SamplePosition(point, out var sample, 10f) && CalculatePathCustom(sample, navPath) && navPath.status == NavMeshPathStatus.PathComplete)
				{
					return true;
				}
			}
			return false;
		}
	}

	public bool GetFirstReachablePointInShuffledTopPct(List<Vector3> points, ref NavMeshPath navPath, float topPct = 0.05f)
	{
		using (TimeWarning.New("GetFirstReachablePointInShuffledTopPct"))
		{
			using PooledList<Vector3> pooledList = Facepunch.Pool.Get<PooledList<Vector3>>();
			int a = Mathf.Max(2, Mathf.FloorToInt((float)points.Count * topPct));
			a = Mathf.Min(a, points.Count);
			for (int i = 0; i < a; i++)
			{
				pooledList.Add(points[i]);
			}
			pooledList.Shuffle((uint)UnityEngine.Random.Range(0, int.MaxValue));
			if (a < points.Count)
			{
				pooledList.AddRange(points.GetRange(a, points.Count - a));
			}
			return GetFirstReachablePoint(pooledList, ref navPath);
		}
	}
}
