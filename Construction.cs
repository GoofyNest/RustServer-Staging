using System;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Facepunch;
using UnityEngine;
using UnityEngine.Serialization;

public class Construction : PrefabAttribute
{
	public class Grade
	{
		public BuildingGrade grade;

		public float maxHealth;

		public List<ItemAmount> costToBuild;

		public PhysicMaterial physicMaterial => grade.physicMaterial;

		public ProtectionProperties damageProtecton => grade.damageProtecton;
	}

	public struct Target
	{
		public bool valid;

		public Ray ray;

		public BaseEntity entity;

		public Socket_Base socket;

		public bool onTerrain;

		public Vector3 position;

		public Vector3 normal;

		public Vector3 rotation;

		public BasePlayer player;

		public bool inBuildingPrivilege;

		public bool isHoldingShift;

		public Quaternion GetWorldRotation(bool female)
		{
			Quaternion quaternion = socket.rotation;
			if (socket.male && socket.female && female)
			{
				quaternion = socket.rotation * Quaternion.Euler(180f, 0f, 180f);
			}
			return entity.transform.rotation * quaternion;
		}

		public Vector3 GetWorldPosition()
		{
			return entity.transform.localToWorldMatrix.MultiplyPoint3x4(socket.position);
		}
	}

	public struct Placement
	{
		public Vector3 position;

		public Quaternion rotation;

		public bool isPopulated;

		public readonly bool isHoldingShift;

		public Transform transform;

		public Placement(Target target)
		{
			isHoldingShift = target.isHoldingShift;
			position = Vector3.zero;
			rotation = Quaternion.identity;
			isPopulated = true;
			transform = null;
			if (target.entity != null)
			{
				transform = target.entity.transform;
			}
		}
	}

	public BaseEntity.Menu.Option info;

	public bool canBypassBuildingPermission;

	public bool showBuildingBlockedPreview = true;

	[InspectorName("Can bypass road checks")]
	public bool canPlaceOnRoads;

	[FormerlySerializedAs("canRotate")]
	public bool canRotateBeforePlacement;

	[FormerlySerializedAs("canRotate")]
	public bool canRotateAfterPlacement;

	public bool checkVolumeOnRotate;

	public bool checkVolumeOnUpgrade;

	public bool canPlaceAtMaxDistance;

	public bool placeOnWater;

	public bool overridePlacementLayer;

	public LayerMask overridedPlacementLayer;

	public Vector3 rotationAmount = new Vector3(0f, 90f, 0f);

	public Vector3 applyStartingRotation = Vector3.zero;

	public Transform deployOffset;

	public bool enforceLineOfSightCheckAgainstParentEntity;

	public bool canSnap;

	public float holdToPlaceDuration;

	public bool canFloodFillSockets;

	[Range(0f, 10f)]
	public float healthMultiplier = 1f;

	[Range(0f, 10f)]
	public float costMultiplier = 1f;

	[Range(1f, 50f)]
	public float maxplaceDistance = 4f;

	public UnityEngine.Mesh guideMesh;

	[NonSerialized]
	public Socket_Base[] allSockets;

	[NonSerialized]
	public BuildingProximity[] allProximities;

	[NonSerialized]
	public ConstructionGrade defaultGrade;

	[NonSerialized]
	public SocketHandle socketHandle;

	[NonSerialized]
	public Bounds bounds;

	[NonSerialized]
	public bool isBuildingPrivilege;

	[NonSerialized]
	public bool isSleepingBag;

	[NonSerialized]
	public ConstructionGrade[] grades;

	[NonSerialized]
	public Deployable deployable;

	[NonSerialized]
	public ConstructionPlaceholder placeholder;

	public static Translate.Phrase lastPlacementError = string.Empty;

	public static bool lastPlacementErrorIsDetailed;

	public static string lastPlacementErrorDebug;

	public static BuildingBlock lastBuildingBlockError;

	public BaseEntity CreateConstruction(Target target, bool bNeedsValidPlacement = false)
	{
		GameObject gameObject = GameManager.server.CreatePrefab(fullName, Vector3.zero, Quaternion.identity, active: false);
		bool flag = UpdatePlacement(gameObject.transform, this, ref target);
		BaseEntity baseEntity = gameObject.ToBaseEntity();
		if (bNeedsValidPlacement && !flag)
		{
			if (baseEntity.IsValid())
			{
				baseEntity.Kill();
			}
			else
			{
				GameManager.Destroy(gameObject);
			}
			return null;
		}
		DecayEntity decayEntity = baseEntity as DecayEntity;
		if ((bool)decayEntity)
		{
			decayEntity.AttachToBuilding(target.entity as DecayEntity);
		}
		return baseEntity;
	}

	public bool HasMaleSockets(Target target)
	{
		Socket_Base[] array = allSockets;
		foreach (Socket_Base socket_Base in array)
		{
			if (socket_Base.male && !socket_Base.maleDummy && socket_Base.TestTarget(target))
			{
				return true;
			}
		}
		return false;
	}

	public void FindMaleSockets(Target target, List<Socket_Base> sockets)
	{
		Socket_Base[] array = allSockets;
		foreach (Socket_Base socket_Base in array)
		{
			if (socket_Base.male && !socket_Base.maleDummy && socket_Base.TestTarget(target))
			{
				sockets.Add(socket_Base);
			}
		}
	}

	public ConstructionGrade GetGrade(BuildingGrade.Enum iGrade, ulong iSkin)
	{
		ConstructionGrade[] array = grades;
		foreach (ConstructionGrade constructionGrade in array)
		{
			if (constructionGrade.gradeBase.type == iGrade && constructionGrade.gradeBase.skin == iSkin)
			{
				return constructionGrade;
			}
		}
		return defaultGrade;
	}

	protected override void AttributeSetup(GameObject rootObj, string name, bool serverside, bool clientside, bool bundling)
	{
		base.AttributeSetup(rootObj, name, serverside, clientside, bundling);
		isBuildingPrivilege = rootObj.GetComponent<BuildingPrivlidge>();
		isSleepingBag = rootObj.GetComponent<SleepingBag>();
		bounds = rootObj.GetComponent<BaseEntity>().bounds;
		deployable = GetComponent<Deployable>();
		placeholder = GetComponentInChildren<ConstructionPlaceholder>();
		allSockets = GetComponentsInChildren<Socket_Base>(includeInactive: true);
		allProximities = GetComponentsInChildren<BuildingProximity>(includeInactive: true);
		socketHandle = GetComponentsInChildren<SocketHandle>(includeInactive: true).FirstOrDefault();
		grades = rootObj.GetComponents<ConstructionGrade>();
		ConstructionGrade[] array = grades;
		foreach (ConstructionGrade constructionGrade in array)
		{
			if (!(constructionGrade == null))
			{
				constructionGrade.construction = this;
				if (!(defaultGrade != null))
				{
					defaultGrade = constructionGrade;
				}
			}
		}
	}

	protected override Type GetIndexedType()
	{
		return typeof(Construction);
	}

	public bool UpdatePlacement(Transform transform, Construction common, ref Target target)
	{
		if (!target.valid)
		{
			if (common.placeOnWater)
			{
				lastPlacementError = ConstructionErrors.WantsWater;
			}
			return false;
		}
		if (!common.canBypassBuildingPermission && !target.player.CanBuild())
		{
			lastPlacementError = ConstructionErrors.NoPermission;
			return false;
		}
		List<Socket_Base> obj = Facepunch.Pool.Get<List<Socket_Base>>();
		common.FindMaleSockets(target, obj);
		foreach (Socket_Base item in obj)
		{
			Placement placement = default(Placement);
			if (target.entity != null && target.socket != null && target.entity.IsOccupied(target.socket))
			{
				continue;
			}
			if (!placement.isPopulated)
			{
				placement = item.DoPlacement(target);
			}
			if (target.player != null && target.player.IsInTutorial)
			{
				TutorialIsland currentTutorialIsland = target.player.GetCurrentTutorialIsland();
				if (currentTutorialIsland != null && !currentTutorialIsland.CheckPlacement(common, target, ref placement))
				{
					placement = default(Placement);
				}
			}
			if (!placement.isPopulated)
			{
				continue;
			}
			if (target.player.IsInCreativeMode && Creative.freePlacement)
			{
				transform.SetPositionAndRotation(placement.position, placement.rotation);
				return true;
			}
			if (!item.CheckSocketMods(ref placement))
			{
				transform.position = placement.position;
				transform.rotation = placement.rotation;
				continue;
			}
			if (!TestPlacingThroughRock(ref placement, target))
			{
				transform.position = placement.position;
				transform.rotation = placement.rotation;
				lastPlacementError = ConstructionErrors.ThroughRock;
				continue;
			}
			if (!TestPlacingThroughWall(ref placement, transform, common, target))
			{
				transform.position = placement.position;
				transform.rotation = placement.rotation;
				lastPlacementError = ConstructionErrors.LineOfSightBlocked;
				lastPlacementErrorDebug = "Placing through walls";
				continue;
			}
			if (!TestPlacingCloseToRoad(ref placement, target, common))
			{
				transform.position = placement.position;
				transform.rotation = placement.rotation;
				lastPlacementError = ConstructionErrors.TooCloseToRoad;
				continue;
			}
			if (target.entity is Door && target.socket == null)
			{
				transform.position = placement.position;
				transform.rotation = placement.rotation;
				lastPlacementError = ConstructionErrors.CantDeployOnDoor;
				continue;
			}
			if (Vector3.Distance(placement.position, target.player.eyes.position) > common.maxplaceDistance + 1f)
			{
				transform.position = placement.position;
				transform.rotation = placement.rotation;
				lastPlacementError = ConstructionErrors.TooFarAway;
				continue;
			}
			DeployVolume[] volumes = PrefabAttribute.server.FindAll<DeployVolume>(prefabID);
			if (DeployVolume.Check(placement.position, placement.rotation, volumes))
			{
				transform.position = placement.position;
				transform.rotation = placement.rotation;
				if (DeployVolume.LastDeployHit != null)
				{
					lastPlacementErrorDebug = DeployVolume.LastDeployHit.name;
					string blockedByErrorFromCollider = ConstructionErrors.GetBlockedByErrorFromCollider(DeployVolume.LastDeployHit, target.player);
					if (!string.IsNullOrEmpty(blockedByErrorFromCollider))
					{
						lastPlacementError = blockedByErrorFromCollider;
						lastPlacementErrorIsDetailed = true;
						continue;
					}
				}
				lastPlacementError = ConstructionErrors.NotEnoughSpace;
				continue;
			}
			if (BuildingProximity.Check(target.player, this, placement.position, placement.rotation))
			{
				transform.position = placement.position;
				transform.rotation = placement.rotation;
				continue;
			}
			if (common.isBuildingPrivilege && !target.player.CanPlaceBuildingPrivilege(placement.position, placement.rotation, common.bounds))
			{
				transform.position = placement.position;
				transform.rotation = placement.rotation;
				lastPlacementError = ConstructionErrors.StackPrivilege;
				continue;
			}
			bool flag = target.player.IsBuildingBlocked(placement.position, placement.rotation, common.bounds);
			if (!common.canBypassBuildingPermission && flag)
			{
				transform.position = placement.position;
				transform.rotation = placement.rotation;
				lastPlacementError = ConstructionErrors.NoPermission;
				continue;
			}
			target.inBuildingPrivilege = flag;
			transform.SetPositionAndRotation(placement.position, placement.rotation);
			if (common.holdToPlaceDuration > 0f && target.player != null && isServer && target.player.GetHeldEntity() is Planner planner && (Vector3.Distance(target.player.transform.position, planner.serverStartDurationPlacementPosition) > 1f || Mathf.Abs((float)planner.serverStartDurationPlacementTime - common.holdToPlaceDuration) > 0.5f))
			{
				return false;
			}
			Facepunch.Pool.FreeUnmanaged(ref obj);
			return true;
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return false;
	}

	private bool TestPlacingThroughRock(ref Placement placement, Target target)
	{
		OBB oBB = new OBB(placement.position, Vector3.one, placement.rotation, bounds);
		Vector3 center = target.player.GetCenter(ducked: true);
		Vector3 origin = target.ray.origin;
		if (UnityEngine.Physics.Linecast(center, origin, 65536, QueryTriggerInteraction.Ignore))
		{
			return false;
		}
		RaycastHit hit;
		Vector3 end = (oBB.Trace(target.ray, out hit) ? hit.point : oBB.ClosestPoint(origin));
		if (UnityEngine.Physics.Linecast(origin, end, 65536, QueryTriggerInteraction.Ignore))
		{
			return false;
		}
		return true;
	}

	private static bool TestPlacingThroughWall(ref Placement placement, Transform transform, Construction common, Target target)
	{
		Vector3 position = placement.position;
		if (common.deployOffset != null)
		{
			position += placement.rotation * common.deployOffset.localPosition;
		}
		Vector3 vector = position - target.ray.origin;
		if (!UnityEngine.Physics.Raycast(target.ray.origin, vector.normalized, out var hitInfo, vector.magnitude, 2097152))
		{
			return true;
		}
		StabilityEntity stabilityEntity = hitInfo.GetEntity() as StabilityEntity;
		if (!common.enforceLineOfSightCheckAgainstParentEntity && stabilityEntity != null && target.entity == stabilityEntity)
		{
			return true;
		}
		if (vector.magnitude - hitInfo.distance < 0.2f)
		{
			return true;
		}
		transform.SetPositionAndRotation(hitInfo.point, placement.rotation);
		return false;
	}

	private bool TestPlacingCloseToRoad(ref Placement placement, Target target, Construction construction)
	{
		if (construction.canPlaceOnRoads)
		{
			return true;
		}
		TerrainHeightMap heightMap = TerrainMeta.HeightMap;
		TerrainTopologyMap topologyMap = TerrainMeta.TopologyMap;
		if (heightMap == null)
		{
			return true;
		}
		if (topologyMap == null)
		{
			return true;
		}
		OBB oBB = new OBB(placement.position, Vector3.one, placement.rotation, bounds);
		float num = Mathf.Abs(heightMap.GetHeight(oBB.position) - oBB.position.y);
		if (num > 9f)
		{
			return true;
		}
		float radius = Mathf.Lerp(3f, 0f, num / 9f);
		Vector3 position = oBB.position;
		Vector3 point = oBB.GetPoint(-1f, 0f, -1f);
		Vector3 point2 = oBB.GetPoint(-1f, 0f, 1f);
		Vector3 point3 = oBB.GetPoint(1f, 0f, -1f);
		Vector3 point4 = oBB.GetPoint(1f, 0f, 1f);
		int topology = topologyMap.GetTopology(position, radius);
		int topology2 = topologyMap.GetTopology(point, radius);
		int topology3 = topologyMap.GetTopology(point2, radius);
		int topology4 = topologyMap.GetTopology(point3, radius);
		int topology5 = topologyMap.GetTopology(point4, radius);
		if (((topology | topology2 | topology3 | topology4 | topology5) & 0x80800) == 0)
		{
			return true;
		}
		return false;
	}

	public virtual bool ShowAsNeutral(Target target)
	{
		return target.inBuildingPrivilege;
	}
}
