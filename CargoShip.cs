using System.Collections.Generic;
using System.Text;
using Facepunch;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;

public class CargoShip : BaseEntity
{
	private struct HarborInfo
	{
		public BasePath harborPath;

		public Transform harborTransform;

		public int approachNode;
	}

	public int targetNodeIndex = -1;

	public GameObject wakeParent;

	public GameObjectRef scientistTurretPrefab;

	public Transform[] scientistSpawnPoints;

	public List<Transform> crateSpawns;

	public GameObjectRef lockedCratePrefab;

	public GameObjectRef militaryCratePrefab;

	public GameObjectRef eliteCratePrefab;

	public GameObjectRef junkCratePrefab;

	public Transform waterLine;

	public Transform rudder;

	public Transform propeller;

	public GameObjectRef escapeBoatPrefab;

	public Transform escapeBoatPoint;

	public GameObjectRef microphonePrefab;

	public Transform microphonePoint;

	public GameObjectRef speakerPrefab;

	public Transform[] speakerPoints;

	public GameObject radiation;

	public GameObjectRef mapMarkerEntityPrefab;

	public GameObject hornOrigin;

	public SoundDefinition hornDef;

	public CargoShipSounds cargoShipSounds;

	public GameObject[] layouts;

	public GameObjectRef playerTest;

	public Transform bowPoint;

	private uint layoutChoice;

	public const Flags IsDocked = Flags.Reserved1;

	public const Flags HasDocked = Flags.Reserved2;

	public const Flags DockedHarborIndex0 = Flags.Reserved3;

	public const Flags DockedHarborIndex1 = Flags.Reserved4;

	public const Flags Egressing = Flags.Reserved8;

	[ServerVar]
	public static bool docking_debug = false;

	[ServerVar]
	public static bool should_dock = true;

	[ServerVar]
	public static float dock_time = 480f;

	[ServerVar]
	public static bool event_enabled = true;

	[ServerVar]
	public static float event_duration_minutes = 50f;

	[ServerVar]
	public static float egress_duration_minutes = 10f;

	[ServerVar]
	public static int loot_rounds = 3;

	[ServerVar]
	public static float loot_round_spacing_minutes = 10f;

	[ServerVar]
	public static bool refresh_loot_on_dock = true;

	private static List<HarborInfo> harbors = new List<HarborInfo>();

	private int currentHarborApproachNode;

	private int harborIndex;

	private bool isDoingHarborApproach;

	private int dockCount;

	private bool shouldLookAhead;

	private float lifetime;

	private CargoShipContainerDestination[] containerDestinations;

	private HashSet<ulong> boardedPlayerIds = new HashSet<ulong>();

	private static bool hasCalculatedApproaches = false;

	private BaseEntity mapMarkerInstance;

	private Vector3 currentVelocity = Vector3.zero;

	private float currentThrottle;

	private float currentTurnSpeed;

	private float turnScale;

	private int lootRoundsPassed;

	private int hornCount;

	private float currentRadiation;

	private bool egressing;

	private BasePath harborApproachPath;

	private HarborProximityManager proxManager;

	private float lastSpeed = 0.3f;

	public bool IsShipDocked => HasFlag(Flags.Reserved1);

	public static int TotalAvailableHarborDockingPaths => harbors.Count;

	private bool HasFinishedDocking
	{
		get
		{
			if (!should_dock)
			{
				return true;
			}
			return dockCount == harbors.Count;
		}
	}

	private float EventTimeRemaining => event_duration_minutes * 60f - lifetime;

	public static List<Vector3> GetCargoApproachPath(int index)
	{
		if (index >= TotalAvailableHarborDockingPaths)
		{
			return null;
		}
		CalculateHarborApproachNodes();
		List<Vector3> list = new List<Vector3>();
		list.Add(TerrainMeta.Path.OceanPatrolFar[harbors[index].approachNode]);
		foreach (BasePathNode node in harbors[index].harborPath.nodes)
		{
			list.Add(node.Position);
		}
		return list;
	}

	public override float GetNetworkTime()
	{
		return Time.fixedTime;
	}

	[ServerVar]
	public static void debug_info(ConsoleSystem.Arg arg)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("Harbor Positions:");
		for (int i = 0; i < harbors.Count; i++)
		{
			stringBuilder.AppendLine($"harbor {i} is at {harbors[i].harborTransform.position.x}, {harbors[i].harborTransform.position.y}, {harbors[i].harborTransform.position.z}, approach index: {harbors[i].approachNode}");
		}
		arg.ReplyWith(stringBuilder.ToString());
	}

	[ServerVar]
	public static void debug_cargo_status(ConsoleSystem.Arg arg)
	{
		StringBuilder stringBuilder = new StringBuilder();
		int num = 0;
		foreach (BaseNetworkable serverEntity in BaseNetworkable.serverEntities)
		{
			if (serverEntity is CargoShip cargoShip)
			{
				stringBuilder.AppendLine("Cargoship States:");
				stringBuilder.AppendLine("");
				stringBuilder.AppendLine($"Cargoship [{num}] dump");
				stringBuilder.AppendLine($"is at [{cargoShip.transform.position}]");
				stringBuilder.AppendLine($"dock count [{cargoShip.dockCount}]");
				stringBuilder.AppendLine($"is docked [{cargoShip.IsShipDocked}]");
				stringBuilder.AppendLine($"current approach node [{cargoShip.currentHarborApproachNode}]");
				stringBuilder.AppendLine($"is doing approach [{cargoShip.isDoingHarborApproach}]");
				stringBuilder.AppendLine($"chosen harbor is [{cargoShip.harborIndex}]");
				stringBuilder.AppendLine($"is egressing: {cargoShip.egressing}");
				arg.ReplyWith(stringBuilder.ToString());
				stringBuilder.Clear();
				num++;
			}
		}
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.cargoShip == null)
		{
			return;
		}
		layoutChoice = info.msg.cargoShip.layout;
		if (!base.isServer)
		{
			return;
		}
		isDoingHarborApproach = info.msg.cargoShip.isDoingHarborApproach;
		harborIndex = info.msg.cargoShip.harborIndex;
		CalculateHarborApproachNodes();
		if (isDoingHarborApproach && HasFlag(Flags.Reserved1))
		{
			Invoke(LeaveHarbor, dock_time);
			Invoke(PreHarborLeaveHorn, dock_time - 60f);
		}
		currentHarborApproachNode = info.msg.cargoShip.currentHarborApproachNode;
		dockCount = info.msg.cargoShip.dockCount;
		shouldLookAhead = info.msg.cargoShip.shouldLookAhead;
		lifetime = info.msg.cargoShip.lifetime;
		if (info.msg.cargoShip.isEgressing)
		{
			StartEgress();
		}
		if (HasFinishedDocking)
		{
			Invoke(StartEgress, Mathf.Max(EventTimeRemaining, GetTimeRemainingFromCrates()));
		}
		if (HasFlag(Flags.Reserved1) && !IsInvoking(LeaveHarbor))
		{
			Invoke(LeaveHarbor, dock_time);
		}
		boardedPlayerIds.Clear();
		foreach (ulong playerId in info.msg.cargoShip.playerIds)
		{
			boardedPlayerIds.Add(playerId);
		}
	}

	public void RefreshActiveLayout()
	{
		for (int i = 0; i < layouts.Length; i++)
		{
			layouts[i].SetActive(layoutChoice == i);
		}
		if (base.isServer)
		{
			containerDestinations = GetComponentsInChildren<CargoShipContainerDestination>();
		}
	}

	public static void RegisterHarbor(BasePath path, Transform tf)
	{
		harbors.Add(new HarborInfo
		{
			harborPath = path,
			harborTransform = tf
		});
		if (docking_debug)
		{
			Debug.Log("Added " + tf.name + " to harbor list");
		}
	}

	public void TriggeredEventSpawn()
	{
		Vector3 vector = TerrainMeta.RandomPointOffshore();
		vector.y = WaterLevel.GetWaterSurface(vector, waves: false, volumes: false);
		base.transform.position = vector;
		if (should_dock)
		{
			CalculateHarborApproachNodes();
		}
		if (!event_enabled || event_duration_minutes == 0f)
		{
			Invoke(DelayedDestroy, 1f);
		}
	}

	public void TriggeredEventSpawnDockingTest(int index)
	{
		if (harbors.Count <= 0 || !should_dock)
		{
			TriggeredEventSpawn();
			Debug.Log("No harbors registered.");
			return;
		}
		if (harbors.Count <= 0 || index > harbors.Count - 1)
		{
			Debug.Log("Wrong harbor index or no harbors on map.");
			return;
		}
		CalculateHarborApproachNodes();
		if (harbors.Count > 0)
		{
			if (harbors != null)
			{
				int approachNode = harbors[index].approachNode;
				Vector3 vector = TerrainMeta.Path.OceanPatrolFar[approachNode + 5];
				vector.y = WaterLevel.GetWaterSurface(vector, waves: false, volumes: false);
				base.transform.position = vector;
				base.transform.LookAt(harbors[index].harborPath.nodes[0].Position);
			}
			if (!event_enabled || event_duration_minutes == 0f)
			{
				Invoke(DelayedDestroy, 1f);
			}
		}
	}

	private static void CalculateHarborApproachNodes()
	{
		if (hasCalculatedApproaches)
		{
			return;
		}
		hasCalculatedApproaches = true;
		for (int i = 0; i < harbors.Count; i++)
		{
			HarborInfo value = harbors[i];
			float num = float.MaxValue;
			int num2 = -1;
			for (int j = 0; j < TerrainMeta.Path.OceanPatrolFar.Count; j++)
			{
				Vector3 vector = TerrainMeta.Path.OceanPatrolFar[j];
				Vector3 position = value.harborPath.nodes[0].Position;
				float num3 = Vector3.Distance(vector, position);
				_ = docking_debug;
				float num4 = num3;
				Vector3 vector2 = Vector3.up * 3f;
				if (!GamePhysics.LineOfSightRadius(vector + vector2, position + vector2, 1084293377, 3f))
				{
					num4 *= 20f;
				}
				if (num4 < num)
				{
					num = num4;
					num2 = j;
				}
			}
			if (num2 == -1)
			{
				Debug.LogWarning("Cargo couldn't find harbor approach node. Are you sure ocean paths have been generated?");
				break;
			}
			value.approachNode = num2;
			harbors[i] = value;
		}
	}

	public void OnArrivedAtHarbor()
	{
		SetFlag(Flags.Reserved1, b: true);
		List<Transform> obj = Pool.Get<List<Transform>>();
		float num = Random.Range(dock_time * 0.05f, dock_time * 0.1f);
		foreach (HarborCraneContainerPickup allCrane in HarborCraneContainerPickup.AllCranes)
		{
			if (allCrane == null || allCrane.isClient || allCrane.Distance2D(this) > 150f)
			{
				continue;
			}
			obj.Clear();
			CargoShipContainerDestination[] array = containerDestinations;
			foreach (CargoShipContainerDestination cargoShipContainerDestination in array)
			{
				if (allCrane.IsDestinationValidForCrane(cargoShipContainerDestination))
				{
					obj.Add(cargoShipContainerDestination.transform);
				}
			}
			if (obj.Count > 0)
			{
				allCrane.AssignDestination(obj, this, num);
				num += dock_time * Random.Range(0.1f, 0.15f);
			}
		}
		Pool.FreeUnmanaged(ref obj);
		Invoke(PreHarborLeaveHorn, dock_time - 60f);
		if (refresh_loot_on_dock)
		{
			RespawnLoot();
		}
		if (harborIndex == 0)
		{
			SetFlag(Flags.Reserved3, b: true);
		}
		else if (harborIndex == 1)
		{
			SetFlag(Flags.Reserved4, b: true);
		}
		Invoke(LeaveHarbor, dock_time);
	}

	private void ClearAllHarborEntitiesOnShip()
	{
		List<BaseEntity> obj = Pool.Get<List<BaseEntity>>();
		foreach (BaseEntity child in children)
		{
			if (child is CargoShipContainer)
			{
				obj.Add(child);
			}
		}
		foreach (BaseEntity item in obj)
		{
			item.Kill();
		}
		Pool.FreeUnmanaged(ref obj);
	}

	public void CreateMapMarker()
	{
		if ((bool)mapMarkerInstance)
		{
			mapMarkerInstance.Kill();
		}
		BaseEntity baseEntity = GameManager.server.CreateEntity(mapMarkerEntityPrefab.resourcePath, Vector3.zero, Quaternion.identity);
		baseEntity.Spawn();
		baseEntity.SetParent(this);
		mapMarkerInstance = baseEntity;
	}

	public void DisableCollisionTest()
	{
	}

	public void SpawnCrate(string resourcePath)
	{
		if (crateSpawns.Count == 0)
		{
			return;
		}
		int index = Random.Range(0, crateSpawns.Count);
		Vector3 position = crateSpawns[index].position;
		Quaternion rotation = crateSpawns[index].rotation;
		crateSpawns.Remove(crateSpawns[index]);
		BaseEntity baseEntity = GameManager.server.CreateEntity(resourcePath, position, rotation);
		if ((bool)baseEntity)
		{
			baseEntity.enableSaving = false;
			baseEntity.SendMessage("SetWasDropped", SendMessageOptions.DontRequireReceiver);
			baseEntity.Spawn();
			baseEntity.SetParent(this, worldPositionStays: true);
			Rigidbody component = baseEntity.GetComponent<Rigidbody>();
			if (component != null)
			{
				component.isKinematic = true;
			}
		}
	}

	public void RespawnLoot()
	{
		InvokeRepeating(PlayHorn, 0f, 8f);
		SpawnCrate(lockedCratePrefab.resourcePath);
		SpawnCrate(eliteCratePrefab.resourcePath);
		for (int i = 0; i < 4; i++)
		{
			SpawnCrate(militaryCratePrefab.resourcePath);
		}
		for (int j = 0; j < 4; j++)
		{
			SpawnCrate(junkCratePrefab.resourcePath);
		}
		lootRoundsPassed++;
		if (lootRoundsPassed >= loot_rounds)
		{
			CancelInvoke(RespawnLoot);
		}
	}

	public void SpawnSubEntities()
	{
		if (!Rust.Application.isLoadingSave)
		{
			BaseEntity baseEntity = GameManager.server.CreateEntity(escapeBoatPrefab.resourcePath, escapeBoatPoint.position, escapeBoatPoint.rotation);
			if ((bool)baseEntity)
			{
				baseEntity.SetParent(this, worldPositionStays: true);
				baseEntity.Spawn();
				RHIB component = baseEntity.GetComponent<RHIB>();
				component.SetToKinematic();
				if ((bool)component)
				{
					component.AddFuel(50);
				}
			}
		}
		MicrophoneStand microphoneStand = GameManager.server.CreateEntity(microphonePrefab.resourcePath, microphonePoint.position, microphonePoint.rotation) as MicrophoneStand;
		if ((bool)microphoneStand)
		{
			microphoneStand.enableSaving = false;
			microphoneStand.SetParent(this, worldPositionStays: true);
			microphoneStand.Spawn();
			microphoneStand.SpawnChildEntity();
			IOEntity iOEntity = microphoneStand.ioEntity.Get(serverside: true);
			Transform[] array = speakerPoints;
			foreach (Transform transform in array)
			{
				IOEntity iOEntity2 = GameManager.server.CreateEntity(speakerPrefab.resourcePath, transform.position, transform.rotation) as IOEntity;
				iOEntity2.enableSaving = false;
				iOEntity2.SetParent(this, worldPositionStays: true);
				iOEntity2.Spawn();
				iOEntity.outputs[0].connectedTo.Set(iOEntity2);
				iOEntity2.inputs[0].connectedTo.Set(iOEntity);
				iOEntity = iOEntity2;
			}
			microphoneStand.ioEntity.Get(serverside: true).MarkDirtyForceUpdateOutputs();
		}
	}

	protected override void OnChildAdded(BaseEntity child)
	{
		base.OnChildAdded(child);
		if (!base.isServer)
		{
			return;
		}
		if (Rust.Application.isLoadingSave && child is RHIB rHIB)
		{
			Vector3 localPosition = rHIB.transform.localPosition;
			Vector3 b = base.transform.InverseTransformPoint(escapeBoatPoint.transform.position);
			if (Vector3.Distance(localPosition, b) < 1f)
			{
				rHIB.SetToKinematic();
			}
		}
		if (Rust.Application.isLoadingSave)
		{
			return;
		}
		List<BasePlayer> obj = Pool.Get<List<BasePlayer>>();
		child.GetComponentsInChildren(obj);
		foreach (BasePlayer item in obj)
		{
			if (!item.IsBot && !item.IsNpc && item.IsConnected && boardedPlayerIds.Add(item.userID) && item.serverClan != null)
			{
				item.AddClanScore(ClanScoreEventType.ReachedCargoShip);
			}
		}
		Pool.FreeUnmanaged(ref obj);
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.cargoShip = Pool.Get<ProtoBuf.CargoShip>();
		info.msg.cargoShip.layout = layoutChoice;
		info.msg.cargoShip.currentHarborApproachNode = currentHarborApproachNode;
		info.msg.cargoShip.isDoingHarborApproach = isDoingHarborApproach;
		info.msg.cargoShip.dockCount = dockCount;
		info.msg.cargoShip.shouldLookAhead = shouldLookAhead;
		info.msg.cargoShip.isEgressing = egressing;
		info.msg.cargoShip.harborIndex = harborIndex;
		if (!info.forDisk)
		{
			return;
		}
		info.msg.cargoShip.playerIds = Pool.Get<List<ulong>>();
		foreach (ulong boardedPlayerId in boardedPlayerIds)
		{
			info.msg.cargoShip.playerIds.Add(boardedPlayerId);
		}
		info.msg.cargoShip.lifetime = lifetime;
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		RefreshActiveLayout();
	}

	public void PlayHorn()
	{
		ClientRPC(RpcTarget.NetworkGroup("DoHornSound"));
		hornCount++;
		if (hornCount >= 3)
		{
			hornCount = 0;
			CancelInvoke(PlayHorn);
		}
	}

	public override void Spawn()
	{
		if (!Rust.Application.isLoadingSave)
		{
			layoutChoice = (uint)Random.Range(0, layouts.Length);
			SendNetworkUpdate();
			RefreshActiveLayout();
		}
		base.Spawn();
	}

	public override void ServerInit()
	{
		base.ServerInit();
		CalculateHarborApproachNodes();
		Invoke(FindInitialNode, 2f);
		InvokeRepeating(BuildingCheck, 1f, 5f);
		InvokeRepeating(RespawnLoot, 10f, 60f * loot_round_spacing_minutes);
		Invoke(DisableCollisionTest, 10f);
		float waterSurface = WaterLevel.GetWaterSurface(base.transform.position, waves: false, volumes: false);
		Vector3 vector = base.transform.InverseTransformPoint(waterLine.transform.position);
		base.transform.position = new Vector3(base.transform.position.x, waterSurface - vector.y, base.transform.position.z);
		SpawnSubEntities();
		if (HasFinishedDocking)
		{
			Invoke(StartEgress, Mathf.Max(EventTimeRemaining, 120f));
		}
		CreateMapMarker();
	}

	public void UpdateRadiation()
	{
		currentRadiation += 1f;
		TriggerRadiation[] componentsInChildren = radiation.GetComponentsInChildren<TriggerRadiation>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].RadiationAmountOverride = currentRadiation;
		}
	}

	public void StartEgress()
	{
		if (!isDoingHarborApproach && !egressing)
		{
			egressing = true;
			CancelInvoke(PlayHorn);
			radiation.SetActive(value: true);
			SetFlag(Flags.Reserved8, b: true);
			InvokeRepeating(UpdateRadiation, 10f, 1f);
			Invoke(DelayedDestroy, 60f * egress_duration_minutes);
		}
	}

	public void DelayedDestroy()
	{
		Kill();
	}

	public void FindInitialNode()
	{
		targetNodeIndex = GetClosestNodeToUs();
	}

	private int GetHackableCrateCount()
	{
		int num = 0;
		foreach (BaseEntity child in children)
		{
			if (child is HackableLockedCrate)
			{
				num++;
			}
		}
		return num;
	}

	public void BuildingCheck()
	{
		List<BaseEntity> obj = Pool.Get<List<BaseEntity>>();
		Vis.Entities(WorldSpaceBounds(), obj, 2162689);
		foreach (BaseEntity item in obj)
		{
			if (!(item is JunkPileWater junkPileWater))
			{
				if (item is DecayEntity decayEntity && decayEntity.parentEntity.Get(serverside: true) != this && decayEntity.isServer && decayEntity.IsAlive() && !decayEntity.AllowOnCargoShip)
				{
					decayEntity.Kill(DestroyMode.Gib);
				}
			}
			else
			{
				junkPileWater.SinkAndDestroy();
			}
		}
		Pool.FreeUnmanaged(ref obj);
	}

	public void FixedUpdate()
	{
		if (!base.isClient)
		{
			UpdateMovement();
			lifetime += Time.fixedDeltaTime;
		}
	}

	public void UpdateMovement()
	{
		if (IsOceanPatrolPathAvailable() && IsValidTargetNode())
		{
			InitializeHarborApproach();
			Vector3 approachRotationNode = Vector3.zero;
			CalculateDesiredNodes(out var desiredMoveNode, out approachRotationNode);
			float num = 0f;
			num = CalculateDesiredThrottle(desiredMoveNode);
			UpdateShip(num, desiredMoveNode, approachRotationNode);
			float num2 = (isDoingHarborApproach ? 8f : 80f);
			if (Vector3.Distance(base.transform.position, desiredMoveNode) < num2)
			{
				HandleNodeArrival(desiredMoveNode);
			}
			UpdateHarborApproachProgress();
		}
	}

	[ContextMenu("Break")]
	public void Break()
	{
		CalculateDesiredNodes(out var desiredMoveNode, out var _);
		Vector3 normalized = (base.transform.position - desiredMoveNode).normalized;
		base.transform.forward = normalized;
		currentTurnSpeed = 0f;
	}

	private void UpdateShip(float desiredThrottle, Vector3 desiredWaypoint, Vector3 approachRotationNode)
	{
		Vector3 normalized = (desiredWaypoint - base.transform.position).normalized;
		normalized.y = 0f;
		float num = Vector3.Dot(base.transform.right, normalized);
		float num2 = (isDoingHarborApproach ? 6.5f : 2.5f);
		float num3 = Mathf.InverseLerp(0.05f, 0.5f, Mathf.Abs(num));
		if (num3 == 0f && Vector3.Dot(normalized, -base.transform.forward) >= 0.95f)
		{
			num3 = 1f;
		}
		turnScale = Mathf.Lerp(turnScale, num3, Time.deltaTime * 0.2f);
		float num4 = ((!(num < 0f)) ? 1 : (-1));
		currentTurnSpeed = num2 * turnScale * num4;
		if (!isDoingHarborApproach)
		{
			base.transform.Rotate(Vector3.up, Time.deltaTime * currentTurnSpeed, Space.World);
		}
		currentThrottle = Mathf.Lerp(currentThrottle, desiredThrottle, Time.deltaTime * 0.2f);
		currentVelocity = base.transform.forward * (8f * currentThrottle);
		if (isDoingHarborApproach)
		{
			currentVelocity = normalized * currentThrottle * 5f;
			Vector3 normalized2 = (approachRotationNode - base.transform.position).normalized;
			normalized2.y = 0f;
			if (normalized2 != Vector3.zero)
			{
				base.transform.rotation = Quaternion.Slerp(base.transform.rotation, Quaternion.LookRotation(normalized2), Time.deltaTime * 0.1f);
			}
		}
		if (HasFlag(Flags.Reserved1))
		{
			currentVelocity = Vector3.zero;
		}
		base.transform.position += currentVelocity * Time.deltaTime;
	}

	private void UpdateHarborApproachProgress()
	{
		if (isDoingHarborApproach && harborApproachPath != null && harborApproachPath.TryGetComponent<HarborProximityManager>(out var component))
		{
			float pathLength = harborApproachPath.GetPathLength();
			float pathProgress = harborApproachPath.GetPathProgress(base.transform.position);
			component.UpdateNormalisedState(Mathf.Clamp01(pathProgress / pathLength));
		}
	}

	private void InitializeHarborApproach(bool forceInit = false)
	{
		if (forceInit || harbors.Count > 0)
		{
			harborApproachPath = harbors[harborIndex].harborPath;
			proxManager = harborApproachPath.GetComponent<HarborProximityManager>();
		}
	}

	private float CalculateDesiredThrottle(Vector3 desiredMoveNode)
	{
		Vector3 normalized = (desiredMoveNode - base.transform.position).normalized;
		float value = Vector3.Dot(base.transform.forward, normalized);
		float num = Mathf.InverseLerp(0f, 1f, value);
		if (isDoingHarborApproach)
		{
			if (harborApproachPath.nodes[currentHarborApproachNode].maxVelocityOnApproach > 0f)
			{
				lastSpeed = harborApproachPath.nodes[currentHarborApproachNode].maxVelocityOnApproach;
			}
			num = Mathf.Clamp(num, 0.1f, lastSpeed);
		}
		return num;
	}

	private void CalculateDesiredNodes(out Vector3 desiredMoveNode, out Vector3 approachRotationNode)
	{
		if (isDoingHarborApproach)
		{
			int index = (shouldLookAhead ? Mathf.Min(currentHarborApproachNode + 1, harborApproachPath.nodes.Count - 1) : currentHarborApproachNode);
			approachRotationNode = harborApproachPath.nodes[index].Position;
			desiredMoveNode = harborApproachPath.nodes[currentHarborApproachNode].Position;
			return;
		}
		desiredMoveNode = TerrainMeta.Path.OceanPatrolFar[targetNodeIndex];
		if (egressing)
		{
			desiredMoveNode = base.transform.position + (base.transform.position - Vector3.zero).normalized * 10000f;
			if (base.transform.position.sqrMagnitude > 100000000f)
			{
				Debug.LogWarning("Immediately deleting cargo as it is a long way out of bounds");
				Kill();
			}
		}
		approachRotationNode = Vector3.zero;
	}

	private void HandleNodeArrival(Vector3 waypointPosition)
	{
		if (isDoingHarborApproach)
		{
			if (currentHarborApproachNode == harborApproachPath.nodes.Count - 1)
			{
				EndHarborApproach();
			}
			else
			{
				AdvanceHarborApproach();
			}
			return;
		}
		targetNodeIndex = (targetNodeIndex - 1 + TerrainMeta.Path.OceanPatrolFar.Count) % TerrainMeta.Path.OceanPatrolFar.Count;
		if (HasFinishedDocking)
		{
			return;
		}
		for (int i = 0; i < harbors.Count; i++)
		{
			HarborInfo harborInfo = harbors[i];
			if (harborInfo.harborPath != null && harborInfo.approachNode == targetNodeIndex)
			{
				CargoNotifier component = harborInfo.harborPath.GetComponent<CargoNotifier>();
				harborApproachPath = harborInfo.harborPath;
				harborIndex = i;
				if (component != null)
				{
					StartHarborApproach(component);
					break;
				}
			}
		}
	}

	private void StartHarborApproach(CargoNotifier cn)
	{
		PlayHorn();
		isDoingHarborApproach = true;
		dockCount++;
		shouldLookAhead = false;
		if (proxManager != null)
		{
			proxManager.StartMovement();
		}
		ClearAllHarborEntitiesOnShip();
		foreach (HarborCraneContainerPickup allCrane in HarborCraneContainerPickup.AllCranes)
		{
			if (!(allCrane == null) && !allCrane.isClient && !(allCrane.Distance2D(harborApproachPath.nodes[harborApproachPath.nodes.Count / 2].Position) > 150f))
			{
				allCrane.ReplenishContainers();
			}
		}
	}

	private float GetTimeRemainingFromCrates()
	{
		float requiredHackSeconds = HackableLockedCrate.requiredHackSeconds;
		if (GetHackableCrateCount() != 0)
		{
			return requiredHackSeconds + requiredHackSeconds * 0.3f;
		}
		return 120f;
	}

	private void EndHarborApproach()
	{
		PlayHorn();
		isDoingHarborApproach = false;
		currentHarborApproachNode = 0;
		FindInitialNode();
		if (proxManager != null)
		{
			proxManager.EndMovement();
		}
		if (HasFinishedDocking)
		{
			if (docking_debug)
			{
				Debug.Log($"Finished all docking: {EventTimeRemaining}s left in event");
			}
			Invoke(StartEgress, Mathf.Max(EventTimeRemaining, GetTimeRemainingFromCrates()));
		}
	}

	private void AdvanceHarborApproach()
	{
		if (currentHarborApproachNode + 1 < harborApproachPath.nodes.Count)
		{
			currentHarborApproachNode++;
		}
		if (!shouldLookAhead)
		{
			shouldLookAhead = true;
		}
		if (harborApproachPath.nodes[currentHarborApproachNode].maxVelocityOnApproach == 0f)
		{
			OnArrivedAtHarbor();
		}
	}

	private bool IsOceanPatrolPathAvailable()
	{
		if (TerrainMeta.Path.OceanPatrolFar != null)
		{
			return TerrainMeta.Path.OceanPatrolFar.Count > 0;
		}
		return false;
	}

	private bool IsValidTargetNode()
	{
		return targetNodeIndex != -1;
	}

	private void PreHarborLeaveHorn()
	{
		PlayHorn();
	}

	private void LeaveHarbor()
	{
		if (docking_debug)
		{
			Debug.Log("Cargo is leaving harbor.");
		}
		PlayHorn();
		SetFlag(Flags.Reserved1, b: false);
		SetFlag(Flags.Reserved2, b: true);
		currentHarborApproachNode++;
	}

	public int GetClosestNodeToUs()
	{
		int result = 0;
		float num = float.PositiveInfinity;
		for (int i = 0; i < TerrainMeta.Path.OceanPatrolFar.Count; i++)
		{
			Vector3 b = TerrainMeta.Path.OceanPatrolFar[i];
			float num2 = Vector3.Distance(base.transform.position, b);
			if (num2 < num)
			{
				result = i;
				num = num2;
			}
		}
		return result;
	}

	public override Vector3 GetLocalVelocityServer()
	{
		return currentVelocity;
	}

	public override Quaternion GetAngularVelocityServer()
	{
		return Quaternion.Euler(0f, currentTurnSpeed, 0f);
	}

	public override float InheritedVelocityScale()
	{
		return 1f;
	}

	public override bool BlocksWaterFor(BasePlayer player)
	{
		return true;
	}

	public override float MaxVelocity()
	{
		return 8f;
	}

	public override bool SupportsChildDeployables()
	{
		return true;
	}

	public override bool ForceDeployableSetParent()
	{
		return true;
	}

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("CargoShip.OnRpcMessage"))
		{
		}
		return base.OnRpcMessage(player, rpc, msg);
	}
}
