using System;
using System.Collections.Generic;
using System.Text;
using ConVar;
using Rust;
using Rust.Ai;
using UnityEngine;

public class PatrolHelicopterAI : BaseMonoBehaviour
{
	public class targetinfo
	{
		public BasePlayer ply;

		public BaseEntity ent;

		public float lastSeenTime = float.PositiveInfinity;

		public float visibleFor;

		public float nextLOSCheck;

		public targetinfo(BaseEntity initEnt, BasePlayer initPly = null)
		{
			ply = initPly;
			ent = initEnt;
			lastSeenTime = float.PositiveInfinity;
			nextLOSCheck = UnityEngine.Time.realtimeSinceStartup + 1.5f;
		}

		public bool IsVisible()
		{
			return TimeSinceSeen() < 1.5f;
		}

		public float TimeSinceSeen()
		{
			return UnityEngine.Time.realtimeSinceStartup - lastSeenTime;
		}
	}

	private class DangerZone
	{
		public float Radius;

		private float score;

		private float lastActiveTime = UnityEngine.Time.realtimeSinceStartup;

		private const float isStaleTime = 5f;

		private Vector3 centre;

		private BaseEntity parent;

		public Vector3 Centre
		{
			get
			{
				if (parent == null)
				{
					return centre;
				}
				return parent.transform.TransformPoint(centre);
			}
		}

		public float Score
		{
			get
			{
				return score;
			}
			set
			{
				score = value;
				lastActiveTime = UnityEngine.Time.realtimeSinceStartup;
			}
		}

		public float LastActiveTime => lastActiveTime;

		public DangerZone(Vector3 centre, float radius = 20f, BaseEntity parent = null)
		{
			if (parent == null)
			{
				this.centre = centre;
			}
			else
			{
				this.centre = parent.transform.InverseTransformPoint(centre);
			}
			this.parent = parent;
			Radius = radius;
		}

		public bool IsPointInside(Vector3 point)
		{
			return Vector3.Distance(point, Centre) <= Radius;
		}

		public bool IsStale()
		{
			return UnityEngine.Time.realtimeSinceStartup - lastActiveTime > 5f;
		}

		public Vector3 GetNearestEdge(Vector3 point)
		{
			Vector3 normalized = (point - Centre).normalized;
			normalized.y = 0f;
			return Centre + normalized * Radius;
		}
	}

	public enum aiState
	{
		IDLE,
		MOVE,
		ORBIT,
		STRAFE,
		PATROL,
		ORBITSTRAFE,
		GUARD,
		FLEE,
		DEATH
	}

	public Vector3 interestZoneOrigin;

	public Vector3 destination;

	public bool hasInterestZone;

	public float moveSpeed;

	public float maxSpeed = 25f;

	public float courseAdjustLerpTime = 2f;

	public Quaternion targetRotation;

	public Vector3 windVec;

	public Vector3 targetWindVec;

	public float windForce = 5f;

	public float windFrequency = 1f;

	public float targetThrottleSpeed;

	public float throttleSpeed;

	public float maxRotationSpeed = 90f;

	public float rotationSpeed;

	public float terrainPushForce = 100f;

	public float obstaclePushForce = 100f;

	public HelicopterTurret leftGun;

	public HelicopterTurret rightGun;

	public static PatrolHelicopterAI heliInstance;

	public PatrolHelicopter helicopterBase;

	public aiState _currentState;

	public float oceanDepthTargetCutoff = 3f;

	public AIHelicopterAnimation anim;

	private Vector3 _aimTarget;

	private bool movementLockingAiming;

	private bool hasAimTarget;

	private bool aimDoorSide;

	private Vector3 pushVec = Vector3.zero;

	private Vector3 _lastPos;

	private Vector3 _lastMoveDir;

	private bool isDead;

	private bool isRetiring;

	private float spawnTime;

	private float lastDamageTime;

	private bool forceTerrainPushback;

	[ServerVar]
	public static float flee_damage_percentage = 0.35f;

	[ServerVar]
	public static bool use_danger_zones = true;

	[ServerVar]
	public static bool monument_crash = true;

	private bool shouldDebug;

	public List<targetinfo> _targetList = new List<targetinfo>();

	private List<DangerZone> dangerZones = new List<DangerZone>();

	private List<DangerZone> noGoZones = new List<DangerZone>();

	private const int max_zones = 20;

	private const float no_go_zone_size = 250f;

	private const float danger_zone_size = 20f;

	private DangerZone leastActiveZone;

	private float deathTimeout;

	private bool didImpact;

	private Collider[] collisions;

	private bool reachedSpinoutLocation;

	private float destination_min_dist = 2f;

	private float currentOrbitDistance;

	private float currentOrbitTime;

	private bool hasEnteredOrbit;

	private float orbitStartTime;

	private float maxOrbitDuration = 30f;

	private bool breakingOrbit;

	private float timeBetweenRocketsOrbit = 0.5f;

	private bool didGetToDesination;

	public List<MonumentInfo> _visitedMonuments;

	public float arrivalTime;

	public GameObjectRef rocketProjectile;

	public GameObjectRef rocketProjectile_Napalm;

	private bool leftTubeFiredLast;

	private float lastRocketTime;

	private float timeBetweenRockets = 0.2f;

	private int numRocketsLeft = 12;

	private const int maxRockets = 12;

	private Vector3 strafe_target_position;

	[NonSerialized]
	public BasePlayer strafe_target;

	private bool puttingDistance;

	private const float strafe_approach_range = 175f;

	private const float strafe_firing_range = 150f;

	private float get_out_of_strafe_distance = 15f;

	private bool passNapalm;

	private Vector3 cached_strafe_pos;

	private TimeSince timeSinceRefreshed;

	private bool useNapalm;

	[NonSerialized]
	private float lastNapalmTime = float.NegativeInfinity;

	[NonSerialized]
	private float lastStrafeTime = float.NegativeInfinity;

	private float _lastThinkTime;

	public bool IsDead => isDead;

	[ServerVar]
	private void dumpstate()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("[State] " + _currentState);
		stringBuilder.AppendLine($"[Has Interest Zone] {hasInterestZone}");
		stringBuilder.AppendLine($"[Interest Zone] {interestZoneOrigin}");
		stringBuilder.AppendLine($"[Target Count] {_targetList.Count}");
		stringBuilder.AppendLine($"[Retiring] {isRetiring}");
		stringBuilder.AppendLine($"[Has Entered Orbit] {hasEnteredOrbit}");
		stringBuilder.AppendLine($"[Breaking Orbit] {breakingOrbit}");
		stringBuilder.AppendLine($"[Orbit Distance] {currentOrbitDistance}");
		Debug.Log(stringBuilder.ToString());
	}

	public void Awake()
	{
		if (ConVar.PatrolHelicopter.lifetimeMinutes == 0f)
		{
			Invoke(DestroyMe, 1f);
			return;
		}
		InvokeRepeating(UpdateWind, 0f, 1f / windFrequency);
		_lastPos = base.transform.position;
		spawnTime = UnityEngine.Time.realtimeSinceStartup;
		InitializeAI();
	}

	public void SetInitialDestination(Vector3 dest, float mapScaleDistance = 0.25f)
	{
		hasInterestZone = true;
		interestZoneOrigin = dest;
		float x = TerrainMeta.Size.x;
		float y = dest.y + 25f;
		Vector3 position = Vector3Ex.Range(-1f, 1f);
		position.y = 0f;
		position.Normalize();
		position *= x * mapScaleDistance;
		position.y = y;
		if (mapScaleDistance == 0f)
		{
			position = interestZoneOrigin + new Vector3(0f, 10f, 0f);
		}
		base.transform.position = position;
		ExitCurrentState();
		State_Move_Enter(dest);
	}

	public void Retire()
	{
		if (!isRetiring)
		{
			Invoke(DestroyMe, 240f);
			float x = TerrainMeta.Size.x;
			float y = 200f;
			Vector3 newPos = Vector3Ex.Range(-1f, 1f);
			newPos.y = 0f;
			newPos.Normalize();
			newPos *= x * 20f;
			newPos.y = y;
			ExitCurrentState();
			isRetiring = true;
			State_Move_Enter(newPos);
		}
	}

	public void SetIdealRotation(Quaternion newTargetRot, float rotationSpeedOverride = -1f)
	{
		float num = ((rotationSpeedOverride == -1f) ? Mathf.Clamp01(moveSpeed / (maxSpeed * 0.5f)) : rotationSpeedOverride);
		rotationSpeed = num * maxRotationSpeed;
		targetRotation = newTargetRot;
	}

	public Quaternion GetYawRotationTo(Vector3 targetDest)
	{
		Vector3 vector = targetDest;
		vector.y = 0f;
		Vector3 position = base.transform.position;
		position.y = 0f;
		Vector3 normalized = (vector - position).normalized;
		if (!(normalized != Vector3.zero))
		{
			return Quaternion.identity;
		}
		return Quaternion.LookRotation(normalized);
	}

	public void SetTargetDestination(Vector3 targetDest, float minDist = 5f, float minDistForFacingRotation = 30f)
	{
		destination = targetDest;
		destination_min_dist = minDist;
		float num = Vector3.Distance(targetDest, base.transform.position);
		if (num > minDistForFacingRotation && !IsTargeting())
		{
			SetIdealRotation(GetYawRotationTo(destination));
		}
		targetThrottleSpeed = GetThrottleForDistance(num);
	}

	public bool AtDestination()
	{
		return Vector3Ex.Distance2D(base.transform.position, destination) < destination_min_dist;
	}

	public bool AtRotation()
	{
		return Quaternion.Angle(base.transform.rotation, targetRotation) <= 8f;
	}

	private void NoGoZoneAdded(DangerZone zone)
	{
		if (use_danger_zones && zone.IsPointInside(base.transform.position))
		{
			_targetList.Clear();
			ExitCurrentState();
			Vector3 nearestEdge = zone.GetNearestEdge(base.transform.position);
			nearestEdge.y = UnityEngine.Random.Range(35f, 45f);
			State_Flee_Enter(nearestEdge);
		}
	}

	public void MoveToDestination()
	{
		Vector3 vector = (_lastMoveDir = Vector3.Lerp(_lastMoveDir, (destination - base.transform.position).normalized, UnityEngine.Time.deltaTime / courseAdjustLerpTime));
		throttleSpeed = Mathf.Lerp(throttleSpeed, targetThrottleSpeed, UnityEngine.Time.deltaTime / 3f);
		float num = throttleSpeed * maxSpeed;
		TerrainPushback();
		Vector3 vector2 = windVec * windForce * UnityEngine.Time.deltaTime;
		Vector3 vector3 = vector * num * UnityEngine.Time.deltaTime;
		base.transform.position += vector3 + vector2;
		moveSpeed = Mathf.Lerp(moveSpeed, Vector3.Distance(_lastPos, base.transform.position) / UnityEngine.Time.deltaTime, UnityEngine.Time.deltaTime * 2f);
		_lastPos = base.transform.position;
	}

	public void TerrainPushback()
	{
		if (_currentState != aiState.DEATH || forceTerrainPushback)
		{
			Vector3 vector = base.transform.position + new Vector3(0f, 2f, 0f);
			Vector3 normalized = (destination - vector).normalized;
			float b = Vector3.Distance(destination, base.transform.position);
			Ray ray = new Ray(vector, normalized);
			float num = 5f;
			float num2 = Mathf.Min(100f, b);
			int mask = LayerMask.GetMask("Terrain", "World", "Construction");
			Vector3 b2 = Vector3.zero;
			if (UnityEngine.Physics.SphereCast(ray, num, out var hitInfo, num2 - num * 0.5f, mask))
			{
				float num3 = 1f - hitInfo.distance / num2;
				float num4 = terrainPushForce * num3;
				b2 = Vector3.up * num4;
			}
			Ray ray2 = new Ray(vector, _lastMoveDir);
			float num5 = Mathf.Min(10f, b);
			if (UnityEngine.Physics.SphereCast(ray2, num, out var hitInfo2, num5 - num * 0.5f, mask))
			{
				float num6 = 1f - hitInfo2.distance / num5;
				float num7 = obstaclePushForce * num6;
				b2 += _lastMoveDir * num7 * -1f;
				b2 += Vector3.up * num7;
			}
			float num8 = base.transform.position.y - WaterSystem.OceanLevel;
			if (num8 < num5)
			{
				float num9 = 1f - num8 / num5;
				float num10 = terrainPushForce * num8 * num9;
				b2 += Vector3.up * num10;
			}
			pushVec = Vector3.Lerp(pushVec, b2, UnityEngine.Time.deltaTime);
			base.transform.position += pushVec * UnityEngine.Time.deltaTime;
		}
	}

	public void UpdateRotation()
	{
		if (hasAimTarget)
		{
			Vector3 position = base.transform.position;
			position.y = 0f;
			Vector3 aimTarget = _aimTarget;
			aimTarget.y = 0f;
			Vector3 normalized = (aimTarget - position).normalized;
			Vector3 vector = Vector3.Cross(normalized, Vector3.up);
			float num = Vector3.Angle(normalized, base.transform.right);
			float num2 = Vector3.Angle(normalized, -base.transform.right);
			if (aimDoorSide)
			{
				if (num < num2)
				{
					targetRotation = Quaternion.LookRotation(vector);
				}
				else
				{
					targetRotation = Quaternion.LookRotation(-vector);
				}
			}
			else
			{
				targetRotation = Quaternion.LookRotation(normalized);
			}
		}
		rotationSpeed = Mathf.Lerp(rotationSpeed, maxRotationSpeed, UnityEngine.Time.deltaTime / 2f);
		base.transform.rotation = Quaternion.Lerp(base.transform.rotation, targetRotation, rotationSpeed * UnityEngine.Time.deltaTime);
	}

	public void UpdateSpotlight()
	{
		if (hasInterestZone)
		{
			helicopterBase.spotlightTarget = new Vector3(interestZoneOrigin.x, TerrainMeta.HeightMap.GetHeight(interestZoneOrigin), interestZoneOrigin.z);
		}
		else
		{
			helicopterBase.spotlightTarget = Vector3.zero;
		}
	}

	public void Update()
	{
		if (helicopterBase.isClient)
		{
			return;
		}
		heliInstance = this;
		UpdateTargetList();
		MoveToDestination();
		UpdateRotation();
		UpdateSpotlight();
		anim.UpdateAnimation();
		anim.UpdateLastPosition();
		AIThink();
		DoMachineGuns();
		if (!isRetiring && !isDead)
		{
			float num = Mathf.Max(spawnTime + ConVar.PatrolHelicopter.lifetimeMinutes * 60f, lastDamageTime + 180f);
			if (UnityEngine.Time.realtimeSinceStartup > num)
			{
				Retire();
			}
		}
	}

	public void FixedUpdate()
	{
		if (_currentState == aiState.DEATH)
		{
			PhysicsDeathCheck();
		}
	}

	public void OtherDamaged(HitInfo info)
	{
		BasePlayer basePlayer = info.Initiator as BasePlayer;
		if (!(basePlayer == null) && use_danger_zones)
		{
			UpdateDangerZones(basePlayer.transform.position, info.damageTypes.Total(), basePlayer);
		}
	}

	public void WeakspotDamaged(PatrolHelicopter.weakspot weak, HitInfo info)
	{
		BasePlayer basePlayer = info.Initiator as BasePlayer;
		if (!(basePlayer == null))
		{
			if (use_danger_zones)
			{
				UpdateDangerZones(basePlayer.transform.position, info.damageTypes.Total(), basePlayer, weak);
			}
			else
			{
				TryStrafePlayer(info, 5f);
			}
		}
	}

	public void TryStrafePlayer(HitInfo info, float timeSinceDamagedThreshold)
	{
		if (!isRetiring && IsAlive() && _currentState != aiState.FLEE)
		{
			BasePlayer basePlayer = info.Initiator as BasePlayer;
			bool num = ValidRocketTarget(basePlayer);
			bool flag = num && CanStrafe();
			bool flag2 = !num && CanUseNapalm();
			float num2 = UnityEngine.Time.realtimeSinceStartup - lastDamageTime;
			lastDamageTime = UnityEngine.Time.realtimeSinceStartup;
			if (num2 < timeSinceDamagedThreshold && basePlayer != null && (flag || flag2))
			{
				ExitCurrentState();
				State_Strafe_Enter(basePlayer, flag2);
			}
		}
	}

	public void CriticalDamage()
	{
		isDead = true;
		ExitCurrentState();
		State_Death_Enter();
	}

	public void DoMachineGuns()
	{
		if (_targetList.Count > 0)
		{
			if (leftGun.NeedsNewTarget())
			{
				leftGun.UpdateTargetFromList(_targetList);
			}
			if (rightGun.NeedsNewTarget())
			{
				rightGun.UpdateTargetFromList(_targetList);
			}
		}
		leftGun.TurretThink();
		rightGun.TurretThink();
	}

	public void FireGun(Vector3 targetPos, float aimCone, bool left)
	{
		if (ConVar.PatrolHelicopter.guns == 0)
		{
			return;
		}
		Vector3 position = (left ? helicopterBase.left_gun_muzzle.transform : helicopterBase.right_gun_muzzle.transform).position;
		Vector3 normalized = (targetPos - position).normalized;
		position += normalized * 2f;
		Vector3 modifiedAimConeDirection = AimConeUtil.GetModifiedAimConeDirection(aimCone, normalized);
		if (GamePhysics.Trace(new Ray(position, modifiedAimConeDirection), 0f, out var hitInfo, 300f, 1220225809))
		{
			targetPos = hitInfo.point;
			if ((bool)hitInfo.collider)
			{
				BaseEntity entity = hitInfo.GetEntity();
				if ((bool)entity && entity != helicopterBase)
				{
					BaseCombatEntity baseCombatEntity = entity as BaseCombatEntity;
					HitInfo info = new HitInfo(helicopterBase, entity, DamageType.Bullet, helicopterBase.bulletDamage * ConVar.PatrolHelicopter.bulletDamageScale, hitInfo.point);
					if ((bool)baseCombatEntity)
					{
						baseCombatEntity.OnAttacked(info);
						if (baseCombatEntity is BasePlayer)
						{
							Effect.server.ImpactEffect(new HitInfo
							{
								HitPositionWorld = hitInfo.point - modifiedAimConeDirection * 0.25f,
								HitNormalWorld = -modifiedAimConeDirection,
								HitMaterial = StringPool.Get("Flesh")
							});
						}
					}
					else
					{
						entity.OnAttacked(info);
					}
				}
			}
		}
		else
		{
			targetPos = position + modifiedAimConeDirection * 300f;
		}
		helicopterBase.ClientRPC(RpcTarget.NetworkGroup("FireGun"), left, targetPos);
	}

	public bool CanInterruptState()
	{
		aiState currentState = _currentState;
		return currentState == aiState.IDLE || currentState == aiState.MOVE || currentState == aiState.PATROL;
	}

	public bool IsAlive()
	{
		if (!isDead)
		{
			return _currentState != aiState.DEATH;
		}
		return false;
	}

	public void DestroyMe()
	{
		if (dangerZones != null)
		{
			helicopterBase.Kill();
		}
	}

	public Vector3 GetLastMoveDir()
	{
		return _lastMoveDir;
	}

	public Vector3 GetMoveDirection()
	{
		return (destination - base.transform.position).normalized;
	}

	public float GetMoveSpeed()
	{
		return moveSpeed;
	}

	public float GetMaxRotationSpeed()
	{
		return maxRotationSpeed;
	}

	public bool IsTargeting()
	{
		return hasAimTarget;
	}

	public void UpdateWind()
	{
		targetWindVec = UnityEngine.Random.onUnitSphere;
	}

	public void SetAimTarget(Vector3 aimTarg, bool isDoorSide)
	{
		if (!movementLockingAiming)
		{
			hasAimTarget = true;
			_aimTarget = aimTarg;
			aimDoorSide = isDoorSide;
		}
	}

	public void ClearAimTarget()
	{
		hasAimTarget = false;
		_aimTarget = Vector3.zero;
	}

	public void UpdateTargetList()
	{
		BasePlayer strafeTarget = null;
		bool flag = false;
		bool shouldUseNapalm = false;
		float num = 0f;
		targetinfo targetinfo = null;
		for (int num2 = _targetList.Count - 1; num2 >= 0; num2--)
		{
			targetinfo targetinfo2 = _targetList[num2];
			if (targetinfo2 == null || targetinfo2.ent == null)
			{
				_targetList.Remove(targetinfo2);
			}
			else if (use_danger_zones && IsInNoGoZone(targetinfo2.ply.transform.position))
			{
				_targetList.Remove(targetinfo2);
			}
			else if (AI.ignoreplayers || SimpleAIMemory.PlayerIgnoreList.Contains(targetinfo2.ply))
			{
				_targetList.Remove(targetinfo2);
			}
			else
			{
				UpdateTargetLineOfSightTime(targetinfo2);
				bool flag2 = (targetinfo2.ply ? targetinfo2.ply.IsDead() : (targetinfo2.ent.Health() <= 0f));
				if (targetinfo2.TimeSinceSeen() >= 6f || flag2)
				{
					bool flag3 = UnityEngine.Random.Range(0f, 1f) >= 0f;
					if ((CanStrafe() || CanUseNapalm()) && IsAlive() && !flag && !flag2 && (targetinfo2.ply == leftGun._target || targetinfo2.ply == rightGun._target) && flag3)
					{
						shouldUseNapalm = !ValidRocketTarget(targetinfo2.ply) || UnityEngine.Random.Range(0f, 1f) > 0.75f;
						flag = true;
						strafeTarget = targetinfo2.ply;
					}
					_targetList.Remove(targetinfo2);
					if (leftGun._target == targetinfo2.ply)
					{
						leftGun._target = null;
					}
					if (rightGun._target == targetinfo2.ply)
					{
						rightGun._target = null;
					}
				}
				if (use_danger_zones && !flag && (CanStrafe() || CanUseNapalm()) && IsAlive() && (UnityEngine.Time.realtimeSinceStartup - lastNapalmTime > 20f || UnityEngine.Time.realtimeSinceStartup - lastStrafeTime > 15f) && IsInDangerZone(targetinfo2.ply.transform.position, out var dangerZone) && dangerZone != null && dangerZone.Score > num)
				{
					num = dangerZone.Score;
					targetinfo = targetinfo2;
				}
			}
		}
		if (use_danger_zones && !flag && targetinfo != null)
		{
			shouldUseNapalm = !ValidRocketTarget(targetinfo.ply) || UnityEngine.Random.Range(0f, 1f) > 0.75f;
			flag = true;
			strafeTarget = targetinfo.ply;
			targetinfo = null;
		}
		AddNewTargetsToList();
		if (flag && !isRetiring && !isDead)
		{
			ExitCurrentState();
			State_Strafe_Enter(strafeTarget, shouldUseNapalm);
		}
	}

	private void UpdateTargetLineOfSightTime(targetinfo targ)
	{
		if (UnityEngine.Time.realtimeSinceStartup > targ.nextLOSCheck)
		{
			targ.nextLOSCheck = UnityEngine.Time.realtimeSinceStartup + 1f;
			if (PlayerVisible(targ.ply))
			{
				targ.lastSeenTime = UnityEngine.Time.realtimeSinceStartup;
				targ.visibleFor += 1f;
			}
			else
			{
				targ.visibleFor = 0f;
			}
		}
	}

	private void AddNewTargetsToList()
	{
		foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
		{
			if (AI.ignoreplayers || SimpleAIMemory.PlayerIgnoreList.Contains(activePlayer) || activePlayer.InSafeZone() || activePlayer.IsInTutorial || Vector3Ex.Distance2D(base.transform.position, activePlayer.transform.position) > 150f || (use_danger_zones && IsInNoGoZone(activePlayer.transform.position)))
			{
				continue;
			}
			bool flag = false;
			foreach (targetinfo target in _targetList)
			{
				if (target.ply == activePlayer)
				{
					flag = true;
					break;
				}
			}
			if (!flag && activePlayer.GetThreatLevel() > 0.5f && PlayerVisible(activePlayer))
			{
				_targetList.Add(new targetinfo(activePlayer, activePlayer));
			}
		}
	}

	private Vector3? FindTargetWithZones(bool withOffset = true)
	{
		int num = -1;
		float num2 = 0f;
		for (int i = 0; i < _targetList.Count; i++)
		{
			if (use_danger_zones)
			{
				Vector3 position = _targetList[i].ply.transform.position;
				if (!IsInNoGoZone(position) && IsInDangerZone(position, out var dangerZone) && dangerZone != null && dangerZone.Score > num2)
				{
					num2 = dangerZone.Score;
					num = i;
				}
			}
		}
		if (num == -1)
		{
			return null;
		}
		Vector3 vector = Vector3.zero;
		if (withOffset)
		{
			vector = GetTargetOffset();
		}
		return _targetList[num].ply.transform.position + vector;
	}

	private Vector3 FindDefaultTarget(bool withOffset = true)
	{
		Vector3 vector = Vector3.zero;
		if (withOffset)
		{
			vector = GetTargetOffset();
		}
		return _targetList[0].ply.transform.position + vector;
	}

	private Vector3 GetTargetOffset()
	{
		return new Vector3(0f, 20f, 0f);
	}

	public bool PlayerVisible(BasePlayer ply)
	{
		Vector3 position = ply.eyes.position;
		if (ply.eyes.position.y < WaterSystem.OceanLevel && Mathf.Abs(WaterSystem.OceanLevel - ply.eyes.position.y) > oceanDepthTargetCutoff)
		{
			return false;
		}
		if (TOD_Sky.Instance.IsNight && Vector3.Distance(position, interestZoneOrigin) > 40f)
		{
			return false;
		}
		Vector3 vector = base.transform.position - Vector3.up * 6f;
		float num = Vector3.Distance(position, vector);
		Vector3 normalized = (position - vector).normalized;
		if (GamePhysics.Trace(new Ray(vector + normalized * 5f, normalized), 0f, out var hitInfo, num * 1.1f, 1218652417) && hitInfo.collider.gameObject.ToBaseEntity() == ply)
		{
			return true;
		}
		return false;
	}

	public void WasAttacked(HitInfo info)
	{
		BasePlayer basePlayer = info.Initiator as BasePlayer;
		if (!(basePlayer is ScientistNPC) && basePlayer != null)
		{
			_targetList.Add(new targetinfo(basePlayer, basePlayer));
		}
	}

	public void UpdateDangerZones(Vector3 position, float damage, BasePlayer ply, PatrolHelicopter.weakspot weak = null)
	{
		if (!use_danger_zones)
		{
			return;
		}
		if (IsInNoGoZone(position))
		{
			if (shouldDebug)
			{
				Debug.Log("Inside no go zone - ignoring damage");
			}
			return;
		}
		float num = damage;
		if (weak != null)
		{
			if (shouldDebug)
			{
				Debug.Log("Hit weakspot: " + num);
			}
			num = weak.body.MaxHealth() * weak.healthFractionOnDestroyed * (damage / weak.maxHealth);
			if (shouldDebug)
			{
				Debug.Log("Potential Damage: " + num);
			}
		}
		if (dangerZones.Count == 0)
		{
			MakeZone(position, num, ply.GetParentEntity());
			return;
		}
		DangerZone dangerZone = null;
		bool flag = false;
		for (int num2 = dangerZones.Count - 1; num2 >= 0; num2--)
		{
			dangerZone = dangerZones[num2];
			if (dangerZone.IsStale())
			{
				if (shouldDebug)
				{
					Debug.Log("zone is stale");
				}
				dangerZones.RemoveAt(num2);
			}
			else if (dangerZone.IsPointInside(position))
			{
				if (shouldDebug)
				{
					Debug.Log("zone has " + dangerZone.Score + " score");
				}
				if (leastActiveZone == null || dangerZone.LastActiveTime < leastActiveZone.LastActiveTime)
				{
					leastActiveZone = dangerZone;
				}
				dangerZone.Score += num;
				flag = true;
				UpdateNoGoZones(dangerZone);
				break;
			}
		}
		if (flag && shouldDebug)
		{
			Debug.Log("We found a zone");
		}
		if (flag)
		{
			return;
		}
		if (shouldDebug)
		{
			Debug.Log("making a new zone ");
		}
		if (dangerZones.Count + 1 > 20)
		{
			if (leastActiveZone != null && dangerZones.Contains(leastActiveZone))
			{
				dangerZones.Remove(leastActiveZone);
			}
			else
			{
				dangerZones.RemoveAt(0);
			}
		}
		MakeZone(position, num, ply.GetParentEntity());
	}

	private void MakeZone(Vector3 position, float damage, BaseEntity parent = null)
	{
		DangerZone dangerZone = new DangerZone(position, 20f, parent);
		dangerZone.Score += damage;
		dangerZones.Add(dangerZone);
	}

	private void UpdateNoGoZones(DangerZone zone)
	{
		if (zone.Score >= helicopterBase.startHealth * flee_damage_percentage)
		{
			dangerZones.Remove(zone);
			zone.Radius = 250f;
			noGoZones.Add(zone);
			NoGoZoneAdded(zone);
		}
	}

	public void ClearStaleZones()
	{
		for (int num = dangerZones.Count - 1; num >= 0; num--)
		{
			if (dangerZones[num].IsStale())
			{
				dangerZones.RemoveAt(num);
			}
		}
	}

	private void RemoveLeastSignificantZone()
	{
		dangerZones.Sort((DangerZone a, DangerZone b) => a.Score.CompareTo(b.Score));
		dangerZones.RemoveAt(0);
	}

	private bool IsInNoGoZone(Vector3 position)
	{
		bool result = false;
		foreach (DangerZone noGoZone in noGoZones)
		{
			if (noGoZone.IsPointInside(position))
			{
				result = true;
			}
		}
		return result;
	}

	private bool IsInDangerZone(Vector3 position, out DangerZone dangerZone)
	{
		bool result = false;
		dangerZone = null;
		foreach (DangerZone dangerZone2 in dangerZones)
		{
			if (dangerZone2.IsPointInside(position))
			{
				dangerZone = dangerZone2;
				result = true;
			}
		}
		return result;
	}

	public void State_Death_Think(float timePassed)
	{
		if (!reachedSpinoutLocation)
		{
			if (AtDestination())
			{
				forceTerrainPushback = false;
				reachedSpinoutLocation = true;
				StartSpinout();
			}
			return;
		}
		float num = UnityEngine.Time.realtimeSinceStartup * 0.25f;
		float x = Mathf.Sin(MathF.PI * 2f * num) * 10f;
		float z = Mathf.Cos(MathF.PI * 2f * num) * 10f;
		Vector3 vector = new Vector3(x, 0f, z);
		SetAimTarget(base.transform.position + vector, isDoorSide: true);
		if (base.transform.position.y - WaterSystem.OceanLevel <= 0f)
		{
			didImpact = true;
		}
		if (reachedSpinoutLocation && (didImpact || UnityEngine.Time.realtimeSinceStartup > deathTimeout))
		{
			KillOfNaturalCauses();
		}
	}

	public void State_Death_Enter()
	{
		_currentState = aiState.DEATH;
		if (collisions == null)
		{
			collisions = new Collider[10];
		}
		MonumentInfo monumentInfo = null;
		if (monument_crash)
		{
			monumentInfo = GetCloseMonument(800f);
		}
		if (monumentInfo == null)
		{
			reachedSpinoutLocation = true;
			StartSpinout();
			return;
		}
		forceTerrainPushback = true;
		Vector3 position = monumentInfo.transform.position;
		position.y = TerrainMeta.HeightMap.GetHeight(position) + 200f;
		if (TransformUtil.GetGroundInfo(position, out var hitOut, 300f, 1235288065))
		{
			position.y = hitOut.point.y;
		}
		position.y += 30f;
		float distToTarget = Vector3.Distance(base.transform.position, destination);
		targetThrottleSpeed = GetThrottleForDistance(distToTarget);
		SetTargetDestination(position, 15f);
	}

	public void State_Death_Leave()
	{
	}

	private MonumentInfo GetCloseMonument(float maxDistance)
	{
		MonumentInfo result = null;
		if (TerrainMeta.Path != null && TerrainMeta.Path.Monuments != null && TerrainMeta.Path.Monuments.Count > 0)
		{
			float num = float.MaxValue;
			foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
			{
				if (monument.IsSafeZone)
				{
					continue;
				}
				MonumentType type = monument.Type;
				if (type == MonumentType.Mountain || type == MonumentType.Lighthouse || type == MonumentType.Lake || type == MonumentType.WaterWell || type == MonumentType.Cave || type == MonumentType.Building || monument.Tier == (MonumentTier)0 || monument.transform.position.y < WaterSystem.OceanLevel || !monument.AllowPatrolHeliCrash)
				{
					continue;
				}
				float num2 = Vector3Ex.Distance2D(base.transform.position, monument.transform.position);
				if (num2 < num)
				{
					num = num2;
					if (num <= maxDistance)
					{
						result = monument;
					}
				}
			}
		}
		return result;
	}

	private void PhysicsDeathCheck()
	{
		if (!reachedSpinoutLocation)
		{
			return;
		}
		int mask = LayerMask.GetMask("Terrain", "World", "Construction", "Water");
		didImpact = false;
		UnityEngine.Physics.OverlapSphereNonAlloc(base.transform.position, 5f, collisions, mask);
		Collider[] array = collisions;
		foreach (Collider collider in array)
		{
			if (!(collider == null) && !(collider.gameObject == base.gameObject))
			{
				didImpact = true;
				break;
			}
		}
	}

	private void KillOfNaturalCauses()
	{
		helicopterBase.Hurt(helicopterBase.health * 2f, DamageType.Generic, null, useProtection: false);
	}

	private void StartSpinout()
	{
		maxRotationSpeed *= 8f;
		Vector3 randomOffset = GetRandomOffset(base.transform.position, 20f, 60f, 0f, 0f);
		int num = 1237003025;
		TransformUtil.GetGroundInfo(randomOffset - Vector3.up * 2f, out var pos, out var _, 500f, num);
		SetTargetDestination(pos);
		targetThrottleSpeed = 0.5f;
		deathTimeout = UnityEngine.Time.realtimeSinceStartup + 10f;
	}

	public void State_Flee_Think(float timePassed)
	{
		UpdateMove(timePassed);
	}

	public void State_Flee_Enter(Vector3 newPos)
	{
		_currentState = aiState.FLEE;
		helicopterBase.DoFlare();
		TryMove(newPos);
	}

	public void State_Flee_Leave()
	{
	}

	public void State_Idle_Think(float timePassed)
	{
		ExitCurrentState();
		State_Patrol_Enter();
	}

	public void State_Idle_Enter()
	{
		_currentState = aiState.IDLE;
	}

	public void State_Idle_Leave()
	{
	}

	public void State_Move_Think(float timePassed)
	{
		UpdateMove(timePassed);
	}

	public void State_Move_Enter(Vector3 newPos)
	{
		_currentState = aiState.MOVE;
		TryMove(newPos);
	}

	public void State_Move_Leave()
	{
	}

	private void TryMove(Vector3 newPos)
	{
		destination_min_dist = 10f;
		SetTargetDestination(newPos);
		float distToTarget = Vector3.Distance(base.transform.position, destination);
		targetThrottleSpeed = GetThrottleForDistance(distToTarget);
	}

	private void UpdateMove(float timePassed)
	{
		float distToTarget = Vector3.Distance(base.transform.position, destination);
		targetThrottleSpeed = GetThrottleForDistance(distToTarget);
		if (AtDestination())
		{
			ExitCurrentState();
			State_Idle_Enter();
		}
	}

	public void State_Orbit_Think(float timePassed)
	{
		OrbitUpdate(timePassed);
	}

	public Vector3 GetOrbitPosition(float rate)
	{
		float x = Mathf.Sin(rate) * currentOrbitDistance;
		float z = Mathf.Cos(rate) * currentOrbitDistance;
		Vector3 vector = new Vector3(x, 20f, z);
		return interestZoneOrigin + vector;
	}

	public void State_Orbit_Enter(float orbitDistance)
	{
		_currentState = aiState.ORBIT;
		OrbitInit(orbitDistance);
	}

	public void State_Orbit_Leave()
	{
		breakingOrbit = false;
		hasEnteredOrbit = false;
		currentOrbitTime = 0f;
		ClearAimTarget();
	}

	private void OrbitInit(float orbitDistance, float minDistForFacingRotation = 0f)
	{
		breakingOrbit = false;
		hasEnteredOrbit = false;
		orbitStartTime = UnityEngine.Time.realtimeSinceStartup;
		Vector3 vector = base.transform.position - interestZoneOrigin;
		currentOrbitTime = Mathf.Atan2(vector.x, vector.z);
		currentOrbitDistance = orbitDistance;
		ClearAimTarget();
		float num = Vector3Ex.Distance2D(base.transform.position, interestZoneOrigin);
		if (num > orbitDistance && num < 120f)
		{
			currentOrbitDistance = num;
		}
		SetTargetDestination(GetOrbitPosition(currentOrbitTime), 20f, minDistForFacingRotation);
		if (shouldDebug)
		{
			DebugOrbit();
		}
	}

	private void OrbitUpdate(float timePassed, float minDistForFacingRotation = 1f, bool canBreak = true)
	{
		if (breakingOrbit)
		{
			if (AtDestination())
			{
				ExitCurrentState();
				State_Idle_Enter();
			}
		}
		else
		{
			if (Vector3Ex.Distance2D(base.transform.position, destination) > 15f)
			{
				return;
			}
			if (!hasEnteredOrbit)
			{
				hasEnteredOrbit = true;
				orbitStartTime = UnityEngine.Time.realtimeSinceStartup;
			}
			if (_targetList.Count == 0 && !isRetiring && canBreak)
			{
				StartBreakOrbit();
				return;
			}
			float num = MathF.PI * 2f * currentOrbitDistance;
			float num2 = 0.5f * maxSpeed;
			float num3 = num / num2;
			currentOrbitTime += timePassed / num3;
			float rate = currentOrbitTime * 30f;
			Vector3 orbitPosition = GetOrbitPosition(rate);
			ClearAimTarget();
			SetTargetDestination(orbitPosition, 2f, minDistForFacingRotation);
			targetThrottleSpeed = 0.5f;
		}
		if (UnityEngine.Time.realtimeSinceStartup - orbitStartTime > maxOrbitDuration && !breakingOrbit && canBreak)
		{
			StartBreakOrbit();
		}
	}

	private void StartBreakOrbit()
	{
		breakingOrbit = true;
		Vector3 appropriatePosition = GetAppropriatePosition(base.transform.position + base.transform.forward * 75f, 40f, 50f);
		SetTargetDestination(appropriatePosition, 15f, 0f);
	}

	private void DebugOrbit()
	{
	}

	public void State_OrbitStrafe_Enter()
	{
		_currentState = aiState.ORBITSTRAFE;
		if (strafe_target == null)
		{
			ExitCurrentState();
			State_Patrol_Enter();
		}
		if (strafe_target.GetParentEntity() != null)
		{
			ExitCurrentState();
			State_Patrol_Enter();
		}
		interestZoneOrigin = strafe_target_position;
		puttingDistance = true;
		didGetToDesination = false;
		Vector3 targetDest = interestZoneOrigin + base.transform.forward * 95f;
		targetDest.y = base.transform.position.y;
		SetTargetDestination(targetDest);
		if (strafe_target.IsNearEnemyBase() || UnityEngine.Random.Range(0f, 1f) > 0.75f)
		{
			useNapalm = true;
			lastNapalmTime = UnityEngine.Time.realtimeSinceStartup;
		}
		numRocketsLeft = 12 + UnityEngine.Random.Range(-3, 16);
		lastRocketTime = 0f;
	}

	public void State_OrbitStrafe_Think(float timePassed)
	{
		if (puttingDistance)
		{
			if (AtDestination())
			{
				didGetToDesination = true;
			}
			if (didGetToDesination)
			{
				SetIdealRotation(Quaternion.LookRotation(interestZoneOrigin - base.transform.position), 0.8f);
				if (AtRotation())
				{
					puttingDistance = false;
					float b = Vector3Ex.Distance2D(base.transform.position, interestZoneOrigin);
					b = Mathf.Max(70f, b);
					OrbitInit(b, 1000f);
				}
			}
			return;
		}
		OrbitUpdate(timePassed, 1000f, canBreak: false);
		if (hasEnteredOrbit && !breakingOrbit)
		{
			SetIdealRotation(Quaternion.LookRotation(interestZoneOrigin - base.transform.position), 3.5f);
			if (ClipRocketsLeft() > 0 && UnityEngine.Time.realtimeSinceStartup - lastRocketTime > timeBetweenRocketsOrbit && CanSeeForStrafe(interestZoneOrigin))
			{
				FireRocket(interestZoneOrigin);
			}
		}
		if (ClipRocketsLeft() <= 0)
		{
			ExitCurrentState();
			State_Move_Enter(GetAppropriatePosition(strafe_target_position + base.transform.forward * 120f));
		}
	}

	public void State_OrbitStrafe_Leave()
	{
		breakingOrbit = false;
		hasEnteredOrbit = false;
		currentOrbitTime = 0f;
		ClearAimTarget();
		lastStrafeTime = UnityEngine.Time.realtimeSinceStartup;
		strafe_target = null;
	}

	private Vector3 GetRandomPatrolDestination()
	{
		return FindValidDestination();
	}

	private Vector3 FindValidDestination(int maxAttempts = 5)
	{
		if (use_danger_zones)
		{
			for (int i = 0; i < maxAttempts; i++)
			{
				Vector3 vector = GenerateRandomDestination();
				if (!IsInNoGoZone(vector))
				{
					return vector;
				}
			}
			Vector3 vector2 = GenerateRandomDestination(forceMonument: true);
			if (IsInNoGoZone(vector2))
			{
				noGoZones?.Clear();
			}
			return vector2;
		}
		return GenerateRandomDestination();
	}

	public Vector3 GenerateRandomDestination(bool forceMonument = false)
	{
		Vector3 vector = Vector3.zero;
		bool flag = UnityEngine.Random.Range(0f, 1f) >= 0.6f;
		if (forceMonument)
		{
			flag = true;
		}
		if (flag)
		{
			if (TerrainMeta.Path != null && TerrainMeta.Path.Monuments != null && TerrainMeta.Path.Monuments.Count > 0)
			{
				MonumentInfo monumentInfo = null;
				if (_visitedMonuments.Count > 0)
				{
					foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
					{
						if (monument.IsSafeZone)
						{
							continue;
						}
						bool flag2 = false;
						foreach (MonumentInfo visitedMonument in _visitedMonuments)
						{
							if (monument == visitedMonument)
							{
								flag2 = true;
							}
						}
						if (!flag2)
						{
							monumentInfo = monument;
							break;
						}
					}
				}
				if (monumentInfo == null)
				{
					_visitedMonuments.Clear();
					for (int i = 0; i < 5; i++)
					{
						monumentInfo = TerrainMeta.Path.Monuments[UnityEngine.Random.Range(0, TerrainMeta.Path.Monuments.Count)];
						if (!monumentInfo.IsSafeZone)
						{
							break;
						}
					}
				}
				if ((bool)monumentInfo)
				{
					vector = monumentInfo.transform.position;
					_visitedMonuments.Add(monumentInfo);
					vector.y = TerrainMeta.HeightMap.GetHeight(vector) + 200f;
					if (TransformUtil.GetGroundInfo(vector, out var hitOut, 300f, 1235288065))
					{
						vector.y = hitOut.point.y;
					}
					vector.y += 30f;
				}
			}
			else
			{
				vector = GetRandomMapPosition();
			}
		}
		else
		{
			vector = GetRandomMapPosition();
		}
		return vector;
	}

	public void State_Patrol_Think(float timePassed)
	{
		float num = Vector3Ex.Distance2D(base.transform.position, destination);
		if (num <= 25f)
		{
			targetThrottleSpeed = GetThrottleForDistance(num);
		}
		else
		{
			targetThrottleSpeed = 0.5f;
		}
		if (AtDestination() && arrivalTime == 0f)
		{
			arrivalTime = UnityEngine.Time.realtimeSinceStartup;
			ExitCurrentState();
			maxOrbitDuration = 20f;
			State_Orbit_Enter(75f);
		}
		if (_targetList.Count <= 0)
		{
			return;
		}
		if (use_danger_zones)
		{
			Vector3? vector = FindTargetWithZones();
			if (vector.HasValue)
			{
				interestZoneOrigin = vector.Value;
				OrbitInterestZone();
			}
		}
		else
		{
			interestZoneOrigin = FindDefaultTarget();
			OrbitInterestZone();
		}
	}

	private void OrbitInterestZone()
	{
		ExitCurrentState();
		maxOrbitDuration = 10f;
		State_Orbit_Enter(80f);
	}

	public void State_Patrol_Enter()
	{
		_currentState = aiState.PATROL;
		Vector3 randomPatrolDestination = GetRandomPatrolDestination();
		SetTargetDestination(randomPatrolDestination, 10f);
		interestZoneOrigin = randomPatrolDestination;
		arrivalTime = 0f;
	}

	public void State_Patrol_Leave()
	{
	}

	private Vector3 GetRandomMapPosition()
	{
		float x = TerrainMeta.Size.x;
		float y = 30f;
		Vector3 result = Vector3Ex.Range(-0.7f, 0.7f);
		result.y = 0f;
		result.Normalize();
		result *= x * UnityEngine.Random.Range(0f, 0.75f);
		result.y = y;
		return result;
	}

	public int ClipRocketsLeft()
	{
		return numRocketsLeft;
	}

	public bool CanStrafe()
	{
		if (UnityEngine.Time.realtimeSinceStartup - lastStrafeTime >= UnityEngine.Random.Range(15f, 25f))
		{
			return CanInterruptState();
		}
		return false;
	}

	public bool CanUseNapalm()
	{
		return UnityEngine.Time.realtimeSinceStartup - lastNapalmTime >= UnityEngine.Random.Range(25f, 35f);
	}

	public void State_Strafe_Enter(BasePlayer strafeTarget, bool shouldUseNapalm = false)
	{
		StartStrafe(strafeTarget, shouldUseNapalm);
	}

	public void State_Strafe_Think(float timePassed)
	{
		if (puttingDistance)
		{
			if (AtDestination())
			{
				RefreshTargetPosition();
				SetIdealRotation(GetYawRotationTo(strafe_target_position), 1.2f);
				if (AtRotation())
				{
					puttingDistance = false;
					cached_strafe_pos = strafe_target_position;
					SetTargetDestination(strafe_target_position + new Vector3(0f, 40f, 0f), 10f);
				}
			}
			return;
		}
		RefreshTargetPosition();
		SetIdealRotation(GetYawRotationTo(strafe_target_position));
		float num = Vector3Ex.Distance2D(cached_strafe_pos, base.transform.position);
		if (num <= 150f && ClipRocketsLeft() > 0 && UnityEngine.Time.realtimeSinceStartup - lastRocketTime > timeBetweenRockets && CanSeeForStrafe(strafe_target_position))
		{
			FireRocket(strafe_target_position);
		}
		if (num <= get_out_of_strafe_distance || ClipRocketsLeft() <= 0)
		{
			if (UnityEngine.Random.value > 0.6f && strafe_target != null)
			{
				ExitCurrentState();
				State_OrbitStrafe_Enter();
			}
			else
			{
				ExitCurrentState();
				State_Move_Enter(GetAppropriatePosition(strafe_target_position + base.transform.forward * 120f));
			}
		}
	}

	private Vector3 GetPredictedPosition()
	{
		Vector3 vector = strafe_target_position;
		float num = timeSinceRefreshed;
		RefreshTargetPosition();
		Vector3 vector2 = strafe_target_position;
		return vector2 + (vector2 - vector) * (num / UnityEngine.Time.deltaTime);
	}

	private bool CanSeeForStrafe(Vector3 targetPos)
	{
		float num = Vector3.Distance(targetPos, base.transform.position) - 10f;
		if (num < 0f)
		{
			num = 0f;
		}
		return !UnityEngine.Physics.Raycast(base.transform.position, (targetPos - base.transform.position).normalized, num, LayerMask.GetMask("Terrain", "World"));
	}

	public bool ValidRocketTarget(BasePlayer ply)
	{
		if (ply == null)
		{
			return false;
		}
		return !ply.IsNearEnemyBase();
	}

	public void State_Strafe_Leave()
	{
		lastStrafeTime = UnityEngine.Time.realtimeSinceStartup;
		if (useNapalm)
		{
			lastNapalmTime = UnityEngine.Time.realtimeSinceStartup;
		}
		useNapalm = false;
		movementLockingAiming = false;
	}

	private void StartStrafe(BasePlayer strafeTarget, bool shouldUseNapalm = false)
	{
		strafe_target = strafeTarget;
		get_out_of_strafe_distance = UnityEngine.Random.Range(13f, 17f);
		if (CanUseNapalm() && shouldUseNapalm)
		{
			passNapalm = shouldUseNapalm;
			useNapalm = true;
			lastNapalmTime = UnityEngine.Time.realtimeSinceStartup;
		}
		lastStrafeTime = UnityEngine.Time.realtimeSinceStartup;
		_currentState = aiState.STRAFE;
		RefreshTargetPosition();
		numRocketsLeft = 12 + UnityEngine.Random.Range(-1, 1);
		lastRocketTime = 0f;
		movementLockingAiming = true;
		Vector3 randomOffset = GetRandomOffset(strafe_target_position, 175f, 192.5f);
		SetTargetDestination(randomOffset, 10f);
		SetIdealRotation(GetYawRotationTo(randomOffset));
		puttingDistance = true;
	}

	public void FireRocket(Vector3 targetPos)
	{
		numRocketsLeft--;
		lastRocketTime = UnityEngine.Time.realtimeSinceStartup;
		float num = UnityEngine.Random.Range(3.9f, 4.1f);
		bool flag = leftTubeFiredLast;
		leftTubeFiredLast = !leftTubeFiredLast;
		Transform transform = (flag ? helicopterBase.rocket_tube_left.transform : helicopterBase.rocket_tube_right.transform);
		Vector3 vector = transform.position + transform.forward * 1f;
		Vector3 vector2 = (targetPos - vector).normalized;
		if (num > 0f)
		{
			vector2 = AimConeUtil.GetModifiedAimConeDirection(num, vector2);
		}
		Effect.server.Run(helicopterBase.rocket_fire_effect.resourcePath, helicopterBase, StringPool.Get(flag ? "rocket_tube_left" : "rocket_tube_right"), Vector3.zero, Vector3.forward, null, broadcast: true);
		BaseEntity baseEntity = GameManager.server.CreateEntity(useNapalm ? rocketProjectile_Napalm.resourcePath : rocketProjectile.resourcePath, vector);
		if (!(baseEntity == null))
		{
			ServerProjectile component = baseEntity.GetComponent<ServerProjectile>();
			if ((bool)component)
			{
				component.InitializeVelocity(vector2 * component.speed);
			}
			baseEntity.Spawn();
		}
	}

	private void RefreshTargetPosition()
	{
		if (!(strafe_target == null))
		{
			timeSinceRefreshed = 0f;
			int mask = LayerMask.GetMask("Terrain", "World", "Construction", "Water", "Vehicle Large");
			if (TransformUtil.GetGroundInfo(strafe_target.transform.position, out var pos, out var _, 100f, mask, base.transform))
			{
				strafe_target_position = pos;
			}
			else
			{
				strafe_target_position = strafe_target.transform.position;
			}
		}
	}

	public void InitializeAI()
	{
		_lastThinkTime = UnityEngine.Time.realtimeSinceStartup;
	}

	public void OnCurrentStateExit()
	{
		switch (_currentState)
		{
		default:
			State_Idle_Leave();
			break;
		case aiState.MOVE:
			State_Move_Leave();
			break;
		case aiState.STRAFE:
			State_Strafe_Leave();
			break;
		case aiState.ORBIT:
			State_Orbit_Leave();
			break;
		case aiState.ORBITSTRAFE:
			State_OrbitStrafe_Leave();
			break;
		case aiState.FLEE:
			State_Flee_Leave();
			break;
		case aiState.PATROL:
			State_Patrol_Leave();
			break;
		}
	}

	public void ExitCurrentState()
	{
		if (isRetiring || isDead)
		{
			if (shouldDebug)
			{
				Debug.Log("Patrol Helicopter attempting to exit state whilst retiring/dying.");
			}
		}
		else
		{
			OnCurrentStateExit();
			_currentState = aiState.IDLE;
		}
	}

	public float GetTime()
	{
		return UnityEngine.Time.realtimeSinceStartup;
	}

	public void AIThink()
	{
		float time = GetTime();
		float timePassed = time - _lastThinkTime;
		_lastThinkTime = time;
		switch (_currentState)
		{
		default:
			State_Idle_Think(timePassed);
			break;
		case aiState.MOVE:
			State_Move_Think(timePassed);
			break;
		case aiState.STRAFE:
			State_Strafe_Think(timePassed);
			break;
		case aiState.ORBIT:
			State_Orbit_Think(timePassed);
			break;
		case aiState.PATROL:
			State_Patrol_Think(timePassed);
			break;
		case aiState.ORBITSTRAFE:
			State_OrbitStrafe_Think(timePassed);
			break;
		case aiState.FLEE:
			State_Flee_Think(timePassed);
			break;
		case aiState.DEATH:
			State_Death_Think(timePassed);
			break;
		}
	}

	public Vector3 GetRandomOffset(Vector3 origin, float minRange, float maxRange = 0f, float minHeight = 20f, float maxHeight = 30f)
	{
		Vector3 onUnitSphere = UnityEngine.Random.onUnitSphere;
		onUnitSphere.y = 0f;
		onUnitSphere.Normalize();
		maxRange = Mathf.Max(minRange, maxRange);
		Vector3 origin2 = origin + onUnitSphere * UnityEngine.Random.Range(minRange, maxRange);
		return GetAppropriatePosition(origin2, minHeight, maxHeight);
	}

	public Vector3 GetAppropriatePosition(Vector3 origin, float minHeight = 20f, float maxHeight = 30f)
	{
		float num = 100f;
		Ray ray = new Ray(origin + new Vector3(0f, num, 0f), Vector3.down);
		float num2 = 5f;
		int mask = LayerMask.GetMask("Terrain", "World", "Construction", "Water");
		if (UnityEngine.Physics.SphereCast(ray, num2, out var hitInfo, num * 2f - num2, mask))
		{
			origin = hitInfo.point;
		}
		origin.y += UnityEngine.Random.Range(minHeight, maxHeight);
		return origin;
	}

	public float GetThrottleForDistance(float distToTarget)
	{
		float num = 0f;
		if (distToTarget >= 75f)
		{
			return 1f;
		}
		if (distToTarget >= 50f)
		{
			return 0.75f;
		}
		if (distToTarget >= 25f)
		{
			return 0.33f;
		}
		if (distToTarget >= 5f)
		{
			return 0.05f;
		}
		return 0.05f * (1f - distToTarget / 5f);
	}
}
