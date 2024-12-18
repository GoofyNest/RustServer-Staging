using Facepunch;
using ProtoBuf;
using Rust;
using UnityEngine;

public class Elevator : IOEntity, IFlagNotify
{
	public enum Direction
	{
		Up,
		Down
	}

	public Transform LiftRoot;

	public GameObjectRef LiftEntityPrefab;

	public GameObjectRef IoEntityPrefab;

	public Transform IoEntitySpawnPoint;

	public GameObject FloorBlockerVolume;

	public float LiftSpeedPerMetre = 1f;

	public GameObject[] PoweredObjects;

	public MeshRenderer PoweredMesh;

	[ColorUsage(true, true)]
	public Color PoweredLightColour;

	[ColorUsage(true, true)]
	public Color UnpoweredLightColour;

	public float LiftMoveDelay;

	protected const Flags TopFloorFlag = Flags.Reserved1;

	public const Flags ElevatorPowered = Flags.Reserved2;

	private EntityRef<ElevatorLift> liftEntity;

	private IOEntity ioEntity;

	private int[] previousPowerAmount = new int[2];

	protected virtual bool IsStatic => false;

	public int Floor { get; set; }

	protected bool IsTop => HasFlag(Flags.Reserved1);

	protected virtual float FloorHeight => 3f;

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.elevator != null)
		{
			Floor = info.msg.elevator.floor;
			liftEntity.uid = info.msg.elevator.spawnedLift;
		}
		if (FloorBlockerVolume != null)
		{
			FloorBlockerVolume.SetActive(Floor > 0);
		}
	}

	public override void OnDeployed(BaseEntity parent, BasePlayer deployedBy, Item fromItem)
	{
		base.OnDeployed(parent, deployedBy, fromItem);
		Elevator elevatorInDirection = GetElevatorInDirection(Direction.Down);
		if (elevatorInDirection != null)
		{
			elevatorInDirection.SetFlag(Flags.Reserved1, b: false);
			Floor = elevatorInDirection.Floor + 1;
		}
		SetFlag(Flags.Reserved1, b: true);
		UpdateChildEntities(isTop: true);
		SendNetworkUpdate();
	}

	protected virtual void CallElevator()
	{
		EntityLinkBroadcast(delegate(Elevator elevatorEnt)
		{
			if (elevatorEnt.IsTop)
			{
				elevatorEnt.RequestMoveLiftTo(Floor, out var _, this);
			}
		}, (ConstructionSocket socket) => socket.socketType == ConstructionSocket.Type.Elevator);
	}

	public void Server_RaiseLowerElevator(Direction dir, bool goTopBottom)
	{
		if (IsBusy())
		{
			return;
		}
		int num = LiftPositionToFloor();
		switch (dir)
		{
		case Direction.Up:
			num++;
			if (goTopBottom)
			{
				num = Floor;
			}
			break;
		case Direction.Down:
			num--;
			if (goTopBottom)
			{
				num = 0;
			}
			break;
		}
		RequestMoveLiftTo(num, out var _, this);
	}

	protected bool RequestMoveLiftTo(int targetFloor, out float timeToTravel, Elevator fromElevator)
	{
		timeToTravel = 0f;
		if (IsBusy())
		{
			return false;
		}
		if (!IsStatic && ioEntity != null && !ioEntity.IsPowered())
		{
			return false;
		}
		if (!IsValidFloor(targetFloor))
		{
			return false;
		}
		int num = LiftPositionToFloor();
		if (num == targetFloor)
		{
			OpenDoorsAtFloor(num);
			return false;
		}
		if (!liftEntity.IsValid(base.isServer))
		{
			return false;
		}
		ElevatorLift elevatorLift = liftEntity.Get(base.isServer);
		if (!elevatorLift.CanMove())
		{
			return false;
		}
		Vector3 worldSpaceFloorPosition = GetWorldSpaceFloorPosition(targetFloor);
		if (!GamePhysics.LineOfSight(elevatorLift.transform.position, worldSpaceFloorPosition, 2097152))
		{
			return false;
		}
		OnMoveBegin();
		timeToTravel = TimeToTravelDistance(Mathf.Abs(elevatorLift.transform.position.y - worldSpaceFloorPosition.y));
		LeanTween.moveY(elevatorLift.gameObject, worldSpaceFloorPosition.y, timeToTravel).delay = LiftMoveDelay;
		timeToTravel += LiftMoveDelay;
		SetFlag(Flags.Busy, b: true);
		if (targetFloor < Floor)
		{
			elevatorLift.ToggleHurtTrigger(state: true);
		}
		elevatorLift.SetFlag(Flags.Busy, b: true);
		Invoke(ClearBusy, timeToTravel + 1f);
		elevatorLift.NotifyNewFloor(targetFloor, Floor);
		EntityLinkBroadcast(delegate(Elevator elevatorEnt)
		{
			elevatorEnt.SetFlag(Flags.Busy, b: true);
		}, (ConstructionSocket socket) => socket.socketType == ConstructionSocket.Type.Elevator);
		if (ioEntity != null)
		{
			ioEntity.SetFlag(Flags.Busy, b: true);
			ioEntity.SendChangedToRoot(forceUpdate: true);
		}
		return true;
	}

	protected virtual void OpenLiftDoors()
	{
		NotifyLiftEntityDoorsOpen(state: true);
	}

	protected virtual void OnMoveBegin()
	{
	}

	private float TimeToTravelDistance(float distance)
	{
		return distance / LiftSpeedPerMetre;
	}

	protected virtual Vector3 GetWorldSpaceFloorPosition(int targetFloor)
	{
		int num = Floor - targetFloor;
		Vector3 vector = Vector3.up * ((float)num * FloorHeight);
		vector.y -= 1f;
		return base.transform.position - vector;
	}

	protected virtual void ClearBusy()
	{
		SetFlag(Flags.Busy, b: false);
		if (liftEntity.IsValid(base.isServer))
		{
			liftEntity.Get(base.isServer).ToggleHurtTrigger(state: false);
			liftEntity.Get(base.isServer).SetFlag(Flags.Busy, b: false);
		}
		if (ioEntity != null)
		{
			ioEntity.SetFlag(Flags.Busy, b: false);
			ioEntity.SendChangedToRoot(forceUpdate: true);
		}
		EntityLinkBroadcast(delegate(Elevator elevatorEnt)
		{
			elevatorEnt.SetFlag(Flags.Busy, b: false);
		}, (ConstructionSocket socket) => socket.socketType == ConstructionSocket.Type.Elevator);
	}

	protected virtual bool IsValidFloor(int targetFloor)
	{
		if (targetFloor <= Floor)
		{
			return targetFloor >= 0;
		}
		return false;
	}

	private Elevator GetElevatorInDirection(Direction dir)
	{
		EntityLink entityLink = FindLink((dir == Direction.Down) ? "elevator/sockets/elevator-male" : "elevator/sockets/elevator-female");
		if (entityLink != null && !entityLink.IsEmpty())
		{
			BaseEntity owner = entityLink.connections[0].owner;
			if (owner != null && owner.isServer && owner is Elevator elevator && elevator != this)
			{
				return elevator;
			}
		}
		return null;
	}

	public void UpdateChildEntities(bool isTop)
	{
		if (isTop)
		{
			if (!liftEntity.IsValid(base.isServer))
			{
				FindExistingLiftChild();
			}
			if (!liftEntity.IsValid(base.isServer))
			{
				ElevatorLift elevatorLift = GameManager.server.CreateEntity(LiftEntityPrefab.resourcePath, GetWorldSpaceFloorPosition(Floor), LiftRoot.rotation) as ElevatorLift;
				elevatorLift.SetOwnerElevator(this);
				elevatorLift.Spawn();
				liftEntity.Set(elevatorLift);
			}
			if (liftEntity.IsValid(base.isServer))
			{
				if (liftEntity.Get(base.isServer).GetParentEntity() == this)
				{
					liftEntity.Get(base.isServer).SetParent(null, worldPositionStays: true);
				}
				liftEntity.Get(base.isServer).SetOwnerElevator(this);
				liftEntity.Get(base.isServer).SetFlag(Flags.Reserved5, HasFlag(Flags.Reserved2) || IsStatic);
			}
			if (ioEntity == null)
			{
				FindExistingIOChild();
			}
			if (ioEntity == null && IoEntityPrefab.isValid)
			{
				ioEntity = GameManager.server.CreateEntity(IoEntityPrefab.resourcePath, IoEntitySpawnPoint.position, IoEntitySpawnPoint.rotation) as IOEntity;
				ioEntity.SetParent(this, worldPositionStays: true);
				ioEntity.Spawn();
			}
		}
		else
		{
			if (liftEntity.IsValid(base.isServer))
			{
				liftEntity.Get(base.isServer).Kill();
				liftEntity.Set(null);
			}
			if (ioEntity != null)
			{
				ioEntity.Kill();
			}
		}
	}

	private void FindExistingIOChild()
	{
		foreach (BaseEntity child in children)
		{
			if (child is IOEntity iOEntity)
			{
				ioEntity = iOEntity;
				break;
			}
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		if (info.msg.elevator == null)
		{
			info.msg.elevator = Pool.Get<ProtoBuf.Elevator>();
		}
		info.msg.elevator.floor = Floor;
		info.msg.elevator.spawnedLift = liftEntity.uid;
	}

	protected int LiftPositionToFloor()
	{
		if (!liftEntity.IsValid(base.isServer))
		{
			return 0;
		}
		Vector3 position = liftEntity.Get(base.isServer).transform.position;
		int result = -1;
		float num = float.MaxValue;
		for (int i = 0; i <= Floor; i++)
		{
			float num2 = Vector3.Distance(GetWorldSpaceFloorPosition(i), position);
			if (num2 < num)
			{
				num = num2;
				result = i;
			}
		}
		return result;
	}

	public override void DestroyShared()
	{
		Cleanup();
		base.DestroyShared();
	}

	private void Cleanup()
	{
		Elevator elevatorInDirection = GetElevatorInDirection(Direction.Down);
		if (elevatorInDirection != null)
		{
			elevatorInDirection.SetFlag(Flags.Reserved1, b: true);
		}
		Elevator elevatorInDirection2 = GetElevatorInDirection(Direction.Up);
		if (elevatorInDirection2 != null)
		{
			elevatorInDirection2.Kill(DestroyMode.Gib);
		}
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		SetFlag(Flags.Busy, b: false);
		UpdateChildEntities(IsTop);
		if (ioEntity != null)
		{
			ioEntity.SetFlag(Flags.Busy, b: false);
		}
	}

	public override void UpdateHasPower(int inputAmount, int inputSlot)
	{
		base.UpdateHasPower(inputAmount, inputSlot);
		if (inputAmount > 0 && previousPowerAmount[inputSlot] == 0)
		{
			CallElevator();
		}
		previousPowerAmount[inputSlot] = inputAmount;
	}

	private void OnPhysicsNeighbourChanged()
	{
		if (!IsStatic && GetElevatorInDirection(Direction.Down) == null && !HasFloorSocketConnection())
		{
			Kill(DestroyMode.Gib);
		}
	}

	private bool HasFloorSocketConnection()
	{
		EntityLink entityLink = FindLink("elevator/sockets/block-male");
		if (entityLink != null && !entityLink.IsEmpty())
		{
			return true;
		}
		return false;
	}

	public void NotifyLiftEntityDoorsOpen(bool state)
	{
		if (!liftEntity.IsValid(base.isServer))
		{
			return;
		}
		foreach (BaseEntity child in liftEntity.Get(base.isServer).children)
		{
			if (child is Door door)
			{
				door.SetOpen(state);
			}
		}
	}

	protected virtual void OpenDoorsAtFloor(int floor)
	{
	}

	internal override void DoServerDestroy()
	{
		base.DoServerDestroy();
		if (liftEntity.Get(base.isServer) != null)
		{
			liftEntity.Get(base.isServer).Kill();
		}
	}

	public override void OnKilled(HitInfo info)
	{
		base.OnKilled(info);
		if (liftEntity.Get(base.isServer) != null)
		{
			liftEntity.Get(base.isServer).Kill(DestroyMode.Gib);
		}
	}

	public override void OnFlagsChanged(Flags old, Flags next)
	{
		base.OnFlagsChanged(old, next);
		if (!Rust.Application.isLoading && base.isServer && old.HasFlag(Flags.Reserved1) != next.HasFlag(Flags.Reserved1))
		{
			UpdateChildEntities(next.HasFlag(Flags.Reserved1));
			SendNetworkUpdate();
		}
		if (base.isServer)
		{
			ElevatorLift elevatorLift = liftEntity.Get(base.isServer);
			if (elevatorLift != null)
			{
				elevatorLift.SetFlag(Flags.Reserved5, HasFlag(Flags.Reserved2) || IsStatic);
			}
		}
		if (old.HasFlag(Flags.Reserved1) != next.HasFlag(Flags.Reserved1) && FloorBlockerVolume != null)
		{
			FloorBlockerVolume.SetActive(next.HasFlag(Flags.Reserved1));
		}
	}

	private void FindExistingLiftChild()
	{
		foreach (BaseEntity child in children)
		{
			if (child is ElevatorLift entity)
			{
				liftEntity.Set(entity);
				break;
			}
		}
	}

	public void OnFlagToggled(bool state)
	{
		if (base.isServer)
		{
			SetFlag(Flags.Reserved2, state);
			ElevatorLift elevatorLift = liftEntity.Get(base.isServer);
			if (elevatorLift != null)
			{
				elevatorLift.SetFlag(Flags.Reserved5, HasFlag(Flags.Reserved2) || IsStatic);
			}
		}
	}
}
