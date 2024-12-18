using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using UnityEngine;
using UnityEngine.Events;

namespace Rust.Ai.Gen2;

public class SenseComponent : EntityComponent<BaseEntity>, IServerComponent
{
	[Serializable]
	public struct Cone
	{
		public float halfAngle;

		public float range;

		public Cone(float halfAngle = 80f, float range = 10f)
		{
			this.halfAngle = halfAngle;
			this.range = range;
		}
	}

	public class VisibilityStatus : Facepunch.Pool.IPooled
	{
		public Vector3 position;

		public bool isVisible;

		public double lastTimeVisibleChanged;

		public void UpdateVisibility(bool isNowVisible, Vector3? position = null)
		{
			if (isVisible != isNowVisible)
			{
				lastTimeVisibleChanged = UnityEngine.Time.timeAsDouble;
				isVisible = isNowVisible;
			}
			if (position.HasValue)
			{
				this.position = position.Value;
			}
		}

		public float GetTimeSeen()
		{
			if (!isVisible)
			{
				return 0f;
			}
			return (float)(UnityEngine.Time.timeAsDouble - lastTimeVisibleChanged);
		}

		public float GetTimeNotSeen()
		{
			if (isVisible)
			{
				return 0f;
			}
			return (float)(UnityEngine.Time.timeAsDouble - lastTimeVisibleChanged);
		}

		public void EnterPool()
		{
		}

		public void LeavePool()
		{
			isVisible = true;
			lastTimeVisibleChanged = UnityEngine.Time.timeAsDouble;
		}
	}

	private readonly struct DistanceCache
	{
		public readonly float distanceToTargetSq;

		public readonly int lastFrameDistanceUpdated;

		public readonly BaseEntity target;

		public DistanceCache(BaseEntity self, BaseEntity target)
		{
			this.target = target;
			distanceToTargetSq = Vector3.SqrMagnitude(target.transform.position - self.transform.position);
			lastFrameDistanceUpdated = UnityEngine.Time.frameCount;
		}

		public bool IsCacheStale(BaseEntity currentTarget)
		{
			if (lastFrameDistanceUpdated == UnityEngine.Time.frameCount)
			{
				return target != currentTarget;
			}
			return true;
		}
	}

	[SerializeField]
	private Vector3 LongRangeVisionRectangle = new Vector3(6f, 30f, 60f);

	[SerializeField]
	private Cone ShortRangeVisionCone = new Cone(100f, 30f);

	[SerializeField]
	private float touchDistance = 6f;

	[SerializeField]
	private float hearingMultiplier = 1f;

	[NonSerialized]
	public ResettableFloat timeToForgetSightings = new ResettableFloat(30f);

	private const float timeToForgetNoises = 5f;

	private static HashSet<BaseEntity> entitiesUpdatedThisFrame = new HashSet<BaseEntity>();

	[ServerVar]
	public static float minRefreshIntervalSeconds = 0.2f;

	[ServerVar]
	public static float maxRefreshIntervalSeconds = 1f;

	private double? _lastTickTime;

	private double nextRefreshTime;

	private double spawnTime;

	private Dictionary<BaseEntity, double> _alliesWeAreAwareOf = new Dictionary<BaseEntity, double>(3);

	private Dictionary<BaseEntity, VisibilityStatus> entitiesWeAreAwareOf = new Dictionary<BaseEntity, VisibilityStatus>(8);

	private static readonly Dictionary<NpcNoiseIntensity, float> noiseRadii = new Dictionary<NpcNoiseIntensity, float>
	{
		{
			NpcNoiseIntensity.None,
			0f
		},
		{
			NpcNoiseIntensity.Low,
			10f
		},
		{
			NpcNoiseIntensity.Medium,
			50f
		},
		{
			NpcNoiseIntensity.High,
			100f
		}
	};

	private NpcNoiseEvent _currentNoise;

	[SerializeField]
	private float foodDetectionRange = 30f;

	private BaseEntity _nearestFood;

	[SerializeField]
	private float fireDetectionRange = 20f;

	[NonSerialized]
	public UnityEvent onFireMelee = new UnityEvent();

	private BaseEntity _nearestFire;

	private double? lastMeleeTime;

	[SerializeField]
	private float TargetingCooldown = 5f;

	private const float npcDistPenaltyToFavorTargetingPlayers = 10f;

	private BaseEntity _target;

	private double? lastTargetTime;

	private LockState lockState = new LockState();

	private DistanceCache? distanceCache;

	public float RefreshInterval
	{
		get
		{
			if (!ShouldRefreshFast)
			{
				return maxRefreshIntervalSeconds;
			}
			return minRefreshIntervalSeconds;
		}
	}

	private double LastTickTime
	{
		get
		{
			double valueOrDefault = _lastTickTime.GetValueOrDefault();
			if (!_lastTickTime.HasValue)
			{
				valueOrDefault = UnityEngine.Time.timeAsDouble;
				_lastTickTime = valueOrDefault;
				return valueOrDefault;
			}
			return valueOrDefault;
		}
		set
		{
			_lastTickTime = value;
		}
	}

	public bool HasPlayerInVicinity { get; private set; }

	public bool ShouldRefreshFast
	{
		get
		{
			if (!HasPlayerInVicinity)
			{
				if (_target != null)
				{
					return _target.IsNonNpcPlayer();
				}
				return false;
			}
			return true;
		}
	}

	public NpcNoiseEvent currentNoise => _currentNoise;

	private bool ChangedTargetRecently
	{
		get
		{
			if (lastTargetTime.HasValue)
			{
				return UnityEngine.Time.timeAsDouble - lastTargetTime.Value < (double)TargetingCooldown;
			}
			return true;
		}
	}

	public void GetInitialAllies(List<BaseEntity> allies)
	{
		using PooledList<BaseEntity> pooledList = Facepunch.Pool.Get<PooledList<BaseEntity>>();
		foreach (var (baseEntity2, num2) in _alliesWeAreAwareOf)
		{
			if (!baseEntity2.IsValid() || (baseEntity2 is BaseCombatEntity baseCombatEntity && baseCombatEntity.IsDead()))
			{
				pooledList.Add(baseEntity2);
			}
			else if (!(num2 - spawnTime > (double)(maxRefreshIntervalSeconds * 2f)))
			{
				allies.Add(baseEntity2);
			}
		}
		foreach (BaseEntity item in pooledList)
		{
			_alliesWeAreAwareOf.Remove(item);
		}
	}

	public Vector3? GetLKP(BaseEntity entity)
	{
		if (GetVisibilityStatus(entity, out var status))
		{
			return status.isVisible ? entity.transform.position : status.position;
		}
		return null;
	}

	public bool GetVisibilityStatus(BaseEntity entity, out VisibilityStatus status)
	{
		status = null;
		if (!CanTarget(entity))
		{
			return false;
		}
		if (!entitiesWeAreAwareOf.TryGetValue(entity, out status))
		{
			return false;
		}
		return true;
	}

	public bool Forget(BaseEntity entity)
	{
		if (!entitiesWeAreAwareOf.TryGetValue(entity, out var value))
		{
			return false;
		}
		entitiesWeAreAwareOf.Remove(entity);
		Facepunch.Pool.Free(ref value);
		return true;
	}

	public bool IsVisible(BaseEntity entity)
	{
		if (!GetVisibilityStatus(entity, out var status))
		{
			return false;
		}
		return status.isVisible;
	}

	public void GetSeenEntities(List<BaseEntity> perceivedEntities)
	{
		using (TimeWarning.New("SenseComponent:GetSeenEntities"))
		{
			foreach (BaseEntity key in entitiesWeAreAwareOf.Keys)
			{
				if (IsVisible(key))
				{
					perceivedEntities.Add(key);
				}
			}
		}
	}

	public void GetOncePerceivedEntities(List<BaseEntity> perceivedEntities)
	{
		foreach (BaseEntity key in entitiesWeAreAwareOf.Keys)
		{
			if (GetVisibilityStatus(key, out var _))
			{
				perceivedEntities.Add(key);
			}
		}
	}

	private Matrix4x4 GetEyeTransform()
	{
		return Matrix4x4.TRS(base.baseEntity.CenterPoint(), base.baseEntity.transform.rotation, Vector3.one);
	}

	public override void InitShared()
	{
		base.InitShared();
		spawnTime = UnityEngine.Time.timeAsDouble;
	}

	public void Tick()
	{
		using (TimeWarning.New("SenseComponent:Tick"))
		{
			double timeAsDouble = UnityEngine.Time.timeAsDouble;
			if (timeAsDouble < nextRefreshTime)
			{
				return;
			}
			float deltaTime = (float)(timeAsDouble - LastTickTime);
			LastTickTime = timeAsDouble;
			HasPlayerInVicinity = false;
			entitiesUpdatedThisFrame.Clear();
			using (TimeWarning.New("SenseComponent:Tick:ProcessEntities"))
			{
				using PooledList<BaseEntity> pooledList = Facepunch.Pool.Get<PooledList<BaseEntity>>();
				BaseEntity.Query.Server.GetPlayersAndBrainsInSphere(base.baseEntity.transform.position, LongRangeVisionRectangle.z, pooledList, BaseEntity.Query.DistanceCheckType.None);
				foreach (BaseEntity item in pooledList)
				{
					if (!(item == base.baseEntity))
					{
						if (item.IsNonNpcPlayer())
						{
							HasPlayerInVicinity = true;
						}
						if (base.baseEntity.InSameNpcTeam(item) && !_alliesWeAreAwareOf.ContainsKey(item))
						{
							_alliesWeAreAwareOf.Add(item, timeAsDouble);
						}
						if (CanTarget(item))
						{
							ProcessEntity(item);
						}
					}
				}
			}
			using (TimeWarning.New("SenseComponent:Tick:RemoveEntities"))
			{
				using PooledList<BaseEntity> pooledList2 = Facepunch.Pool.Get<PooledList<BaseEntity>>();
				foreach (var (baseEntity2, visibilityStatus2) in entitiesWeAreAwareOf)
				{
					if (!CanTarget(baseEntity2))
					{
						pooledList2.Add(baseEntity2);
					}
					else if (!visibilityStatus2.isVisible && visibilityStatus2.GetTimeNotSeen() > timeToForgetSightings.Value)
					{
						pooledList2.Add(baseEntity2);
					}
					else if (!entitiesUpdatedThisFrame.Contains(baseEntity2) && visibilityStatus2.isVisible)
					{
						entitiesWeAreAwareOf[baseEntity2].UpdateVisibility(isNowVisible: false);
					}
				}
				entitiesUpdatedThisFrame.Clear();
				foreach (BaseEntity item2 in pooledList2)
				{
					if (_target.IsValid() && _target == item2)
					{
						ClearTarget();
					}
					Forget(item2);
				}
			}
			TickHearing(deltaTime);
			TickFoodDetection(deltaTime);
			TickFireDetection(deltaTime);
			TickTargeting(deltaTime);
			nextRefreshTime = UnityEngine.Time.timeAsDouble + (double)RefreshInterval;
		}
	}

	private void GetModifiedSenses(BaseEntity entity, out float modTouchDistance, out float modHalfAngle, out float modShortVisionRange, out Vector3 modLongVisionRectangle)
	{
		modTouchDistance = touchDistance;
		modHalfAngle = ShortRangeVisionCone.halfAngle;
		modShortVisionRange = ShortRangeVisionCone.range;
		modLongVisionRectangle = LongRangeVisionRectangle;
		if (entity.ToNonNpcPlayer(out var player))
		{
			if (player.IsDucked())
			{
				modTouchDistance = base.baseEntity.bounds.extents.z * 1.5f;
				modHalfAngle = ShortRangeVisionCone.halfAngle * 0.85f;
				modShortVisionRange = ShortRangeVisionCone.range * 0.5f;
				modLongVisionRectangle = Vector3.Scale(LongRangeVisionRectangle, new Vector3(3f, 0.5f, 0.5f));
			}
			else if (player.IsRunning())
			{
				modTouchDistance = touchDistance * 3f;
				modHalfAngle = ShortRangeVisionCone.halfAngle;
				modShortVisionRange = ShortRangeVisionCone.range * 1.3f;
				modLongVisionRectangle = LongRangeVisionRectangle * 1.15f;
			}
		}
	}

	private bool IsInAnyRange(BaseEntity entity)
	{
		using (TimeWarning.New("IsInAnyRange"))
		{
			Vector3 position = GetEyeTransform().GetPosition();
			Vector3 vector = GetEyeTransform().rotation * Vector3.forward;
			Vector3 vector2 = entity.transform.position - position;
			float magnitude = vector2.magnitude;
			GetModifiedSenses(entity, out var modTouchDistance, out var modHalfAngle, out var modShortVisionRange, out var modLongVisionRectangle);
			if (magnitude < modTouchDistance)
			{
				return true;
			}
			if (Vector3.Angle(vector, vector2.normalized) < modHalfAngle)
			{
				if (magnitude < modShortVisionRange)
				{
					return true;
				}
				if (TOD_Sky.Instance.IsDay && magnitude < modLongVisionRectangle.z && Mathf.Abs(entity.transform.position.y - position.y) < modLongVisionRectangle.y * 0.5f && Vector3.Cross(vector, entity.transform.position - position).magnitude < modLongVisionRectangle.x * 0.5f)
				{
					return true;
				}
			}
			return false;
		}
	}

	private void ProcessEntity(BaseEntity entity)
	{
		bool flag = IsInAnyRange(entity);
		if (flag && entity.ToNonNpcPlayer(out var player))
		{
			using (TimeWarning.New("SenseComponent:ProcessEntity:CanSee"))
			{
				Vector3 position = GetEyeTransform().GetPosition();
				flag = base.baseEntity.CanSee(position, player.eyes.position);
			}
		}
		if (entitiesWeAreAwareOf.TryGetValue(entity, out var value))
		{
			value.UpdateVisibility(flag, flag ? new Vector3?(entity.transform.position) : null);
			entitiesUpdatedThisFrame.Add(entity);
		}
		else if (flag)
		{
			VisibilityStatus visibilityStatus = Facepunch.Pool.Get<VisibilityStatus>();
			visibilityStatus.position = entity.transform.position;
			entitiesWeAreAwareOf.Add(entity, visibilityStatus);
			entitiesUpdatedThisFrame.Add(entity);
		}
	}

	private void TickHearing(float deltaTime)
	{
		using (TimeWarning.New("SenseComponent:TickHearing"))
		{
			if (_currentNoise != null)
			{
				Facepunch.Pool.Free(ref _currentNoise);
			}
			if (hearingMultiplier <= 0f)
			{
				return;
			}
			using PooledList<NpcNoiseEvent> pooledList = Facepunch.Pool.Get<PooledList<NpcNoiseEvent>>();
			SingletonComponent<NpcNoiseManager>.Instance.GetNoisesAround(base.baseEntity.transform.position, noiseRadii[NpcNoiseIntensity.High] * hearingMultiplier, pooledList);
			NpcNoiseEvent npcNoiseEvent = null;
			foreach (NpcNoiseEvent item in pooledList)
			{
				if (item.Initiator == base.baseEntity || UnityEngine.Time.timeAsDouble - item.EventTime > 5.0 || (npcNoiseEvent != null && item.Intensity < npcNoiseEvent.Intensity))
				{
					continue;
				}
				if (!noiseRadii.TryGetValue(item.Intensity, out var value))
				{
					Debug.LogError($"Unknown noise intensity: {item.Intensity}");
					continue;
				}
				float num = Vector3.Distance(item.Position, base.baseEntity.transform.position);
				if (!(num > value * hearingMultiplier) && (npcNoiseEvent == null || item.Intensity != npcNoiseEvent.Intensity || !(num > Vector3.Distance(npcNoiseEvent.Position, base.baseEntity.transform.position))))
				{
					npcNoiseEvent = item;
				}
			}
			if (npcNoiseEvent != null)
			{
				_currentNoise = Facepunch.Pool.Get<NpcNoiseEvent>();
				_currentNoise.Initiator = npcNoiseEvent.Initiator;
				_currentNoise.Position = npcNoiseEvent.Position;
				_currentNoise.Intensity = npcNoiseEvent.Intensity;
			}
		}
	}

	public bool ConsumeCurrentNoise()
	{
		if (_currentNoise == null)
		{
			return false;
		}
		Facepunch.Pool.Free(ref _currentNoise);
		return true;
	}

	public bool FindFood(out BaseEntity food)
	{
		if (_nearestFood == null || _nearestFood.IsDestroyed)
		{
			food = null;
			return false;
		}
		food = _nearestFood;
		return true;
	}

	private void TickFoodDetection(float deltaTime)
	{
		using (TimeWarning.New("SenseComponent:TickFoodDetection"))
		{
			_nearestFood = null;
			if (foodDetectionRange <= 0f)
			{
				return;
			}
			float num = foodDetectionRange * foodDetectionRange;
			float num2 = float.MaxValue;
			using PooledList<BaseEntity> pooledList = Facepunch.Pool.Get<PooledList<BaseEntity>>();
			SingletonComponent<NpcFoodManager>.Instance.GetFoodAround(base.baseEntity.transform.position, foodDetectionRange, pooledList);
			LimitedTurnNavAgent component = base.baseEntity.GetComponent<LimitedTurnNavAgent>();
			foreach (BaseEntity item in pooledList)
			{
				if (!NpcFoodManager.IsFoodImmobile(item))
				{
					continue;
				}
				if (!component.IsPositionOnNavmesh(item.transform.position, out var sample))
				{
					SingletonComponent<NpcFoodManager>.Instance.Remove(item);
					continue;
				}
				sample = item.transform.position - base.baseEntity.transform.position;
				float sqrMagnitude = sample.sqrMagnitude;
				if (sqrMagnitude < num2 && sqrMagnitude < num)
				{
					_nearestFood = item;
					num2 = sqrMagnitude;
				}
			}
		}
	}

	public bool FindFire(out BaseEntity fire)
	{
		if (!_nearestFire.IsValid() || _nearestFire.IsDestroyed || !NpcFireManager.IsOnFire(_nearestFire))
		{
			_nearestFire = null;
		}
		fire = _nearestFire;
		return fire != null;
	}

	private void TickFireDetection(float deltaTime)
	{
		using (TimeWarning.New("SenseComponent:TickFireDetection"))
		{
			if (fireDetectionRange <= 0f)
			{
				return;
			}
			if (_target != null && SingletonComponent<NpcFireManager>.Instance.DidMeleeWithFireRecently(base.baseEntity, _target, out var meleeTime) && (!lastMeleeTime.HasValue || meleeTime != lastMeleeTime.Value))
			{
				lastMeleeTime = meleeTime;
				onFireMelee.Invoke();
			}
			using PooledList<BaseEntity> pooledList = Facepunch.Pool.Get<PooledList<BaseEntity>>();
			SingletonComponent<NpcFireManager>.Instance.GetFiresAround(base.baseEntity.transform.position, fireDetectionRange, pooledList);
			BaseEntity baseEntity = null;
			float num = fireDetectionRange * fireDetectionRange;
			float num2 = float.MaxValue;
			foreach (BaseEntity item in pooledList)
			{
				float sqrMagnitude = (item.transform.position - base.baseEntity.transform.position).sqrMagnitude;
				if (sqrMagnitude < num2 && sqrMagnitude < num)
				{
					baseEntity = item;
					num2 = sqrMagnitude;
				}
			}
			if (baseEntity != null)
			{
				_nearestFire = baseEntity;
			}
		}
	}

	public LockState.LockHandle LockCurrentTarget()
	{
		return lockState.AddLock();
	}

	public bool UnlockTarget(ref LockState.LockHandle handle)
	{
		return lockState.RemoveLock(ref handle);
	}

	public bool CanTarget(BaseEntity entity)
	{
		if (!entity.IsValid())
		{
			return false;
		}
		if (entity.IsTransferProtected())
		{
			return false;
		}
		if (entity.IsDestroyed)
		{
			return false;
		}
		if (!entity.IsNonNpcPlayer() && !entity.IsNpc)
		{
			return false;
		}
		if (entity.IsNpcPlayer())
		{
			return false;
		}
		if (entity is BaseCombatEntity baseCombatEntity && baseCombatEntity.IsDead())
		{
			return false;
		}
		if (base.baseEntity.InSameNpcTeam(entity))
		{
			return false;
		}
		if (entity is BasePlayer item)
		{
			if (AI.ignoreplayers)
			{
				return false;
			}
			if (SimpleAIMemory.PlayerIgnoreList.Contains(item))
			{
				return false;
			}
		}
		return true;
	}

	public bool FindTarget(out BaseEntity target)
	{
		if (!CanTarget(_target))
		{
			ClearTarget();
			target = null;
			return false;
		}
		target = _target;
		return target != null;
	}

	public bool FindTargetPosition(out Vector3 targetPosition)
	{
		if (!FindTarget(out var target))
		{
			targetPosition = Vector3.zero;
			return false;
		}
		targetPosition = target.transform.position;
		return true;
	}

	public bool TrySetTarget(BaseEntity newTarget, bool bypassCooldown = true)
	{
		if (lockState.IsLocked)
		{
			return false;
		}
		if (newTarget == null)
		{
			ClearTarget();
			return true;
		}
		if (newTarget == _target)
		{
			return true;
		}
		if (!CanTarget(newTarget))
		{
			return false;
		}
		if (_target != null && !bypassCooldown && ChangedTargetRecently)
		{
			return false;
		}
		lastTargetTime = UnityEngine.Time.timeAsDouble;
		_target = newTarget;
		base.baseEntity.ClientRPC(RpcTarget.NetworkGroup("CL_SetLookAtTarget"), _target.net.ID);
		return true;
	}

	public bool IsTargetInRange(float range)
	{
		return IsTargetInRangeSq(range * range);
	}

	public bool IsTargetInRangeSq(float rangeSq)
	{
		if (_target == null)
		{
			return false;
		}
		if (!distanceCache.HasValue || distanceCache.Value.IsCacheStale(_target))
		{
			distanceCache = new DistanceCache(base.baseEntity, _target);
		}
		return distanceCache.Value.distanceToTargetSq < rangeSq;
	}

	public void ClearTarget(bool forget = true)
	{
		if (_target.IsValid())
		{
			if (forget)
			{
				Forget(_target);
			}
			lastTargetTime = null;
			_target = null;
			base.baseEntity.ClientRPC(RpcTarget.NetworkGroup("CL_ClearTarget"));
		}
	}

	private void TickTargeting(float deltaTime)
	{
		using (TimeWarning.New("SenseComponent:TickTargeting"))
		{
			if (_target != null && !CanTarget(_target))
			{
				ClearTarget();
			}
			if (_target != null && ChangedTargetRecently)
			{
				return;
			}
			using PooledList<BaseEntity> pooledList = Facepunch.Pool.Get<PooledList<BaseEntity>>();
			GetOncePerceivedEntities(pooledList);
			if (pooledList.Count == 0)
			{
				return;
			}
			BaseEntity baseEntity = null;
			float num = float.MaxValue;
			foreach (BaseEntity item in pooledList)
			{
				if (CanTarget(item))
				{
					float num2 = base.baseEntity.SqrDistance(item);
					if (item.IsNpc)
					{
						num2 += 100f;
					}
					if (num2 < num)
					{
						num = num2;
						baseEntity = item;
					}
				}
			}
			if (baseEntity != null)
			{
				TrySetTarget(baseEntity, bypassCooldown: false);
			}
		}
	}
}
