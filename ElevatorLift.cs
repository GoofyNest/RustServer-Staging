#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Network;
using ProtoBuf;
using UnityEngine;
using UnityEngine.Assertions;

public class ElevatorLift : BaseCombatEntity
{
	public GameObject DescendingHurtTrigger;

	public GameObject MovementCollider;

	public ElevatorButton[] Buttons;

	public Transform UpButtonPoint;

	public Transform DownButtonPoint;

	public Transform GoTopButtonPoint;

	public Transform GoBottomButtonPoint;

	public TriggerNotify VehicleTrigger;

	public GameObjectRef LiftArrivalScreenBounce;

	public SoundDefinition liftMovementLoopDef;

	public SoundDefinition liftMovementStartDef;

	public SoundDefinition liftMovementStopDef;

	public SoundDefinition liftMovementAccentSoundDef;

	public GameObjectRef liftButtonPressedEffect;

	public float movementAccentMinInterval = 0.75f;

	public float movementAccentMaxInterval = 3f;

	private Sound liftMovementLoopSound;

	private float nextMovementAccent;

	public Vector3 lastPosition;

	public List<BaseEntity> vehicleWhitelist;

	private EntityRef<Elevator> ownerElevator;

	public const Flags PressedUp = Flags.Reserved1;

	public const Flags PressedDown = Flags.Reserved2;

	public const Flags Express = Flags.Reserved6;

	public const Flags FlagCanMove = Flags.Reserved5;

	private HashSet<uint> vehiclePrefabWhitelist = new HashSet<uint>();

	private Elevator owner => ownerElevator.Get(base.isServer);

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("ElevatorLift.OnRpcMessage"))
		{
			if (rpc == 4061236510u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_RaiseLowerFloor ");
				}
				using (TimeWarning.New("Server_RaiseLowerFloor"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(4061236510u, "Server_RaiseLowerFloor", this, player, 3f))
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
							Server_RaiseLowerFloor(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in Server_RaiseLowerFloor");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.elevatorLift != null)
		{
			ownerElevator.uid = info.msg.elevatorLift.owner;
		}
	}

	public void SetOwnerElevator(Elevator e)
	{
		ownerElevator.Set(e);
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.elevatorLift = Facepunch.Pool.Get<ProtoBuf.ElevatorLift>();
		if (owner != null)
		{
			info.msg.elevatorLift.owner = ownerElevator.uid;
			info.msg.elevatorLift.topElevatorHeight = owner.transform.position.y;
		}
	}

	public override void ServerInit()
	{
		base.ServerInit();
		FillVehicleWhitelist();
		ToggleHurtTrigger(state: false);
	}

	public void ToggleHurtTrigger(bool state)
	{
		if (DescendingHurtTrigger != null)
		{
			DescendingHurtTrigger.SetActive(state);
		}
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void Server_RaiseLowerFloor(RPCMessage msg)
	{
		Elevator.Direction direction = (Elevator.Direction)msg.read.Int32();
		bool flag = msg.read.Bit();
		SetFlag((direction == Elevator.Direction.Up) ? Flags.Reserved1 : Flags.Reserved2, b: true);
		SetFlag(Flags.Reserved6, flag);
		owner.Server_RaiseLowerElevator(direction, flag);
		Invoke(ClearDirection, 0.7f);
		if (liftButtonPressedEffect.isValid)
		{
			Effect.server.Run(liftButtonPressedEffect.resourcePath, base.transform.position, Vector3.up);
		}
	}

	private void FillVehicleWhitelist()
	{
		foreach (BaseEntity item in vehicleWhitelist)
		{
			vehiclePrefabWhitelist.Add(item.prefabID);
		}
	}

	private void ClearDirection()
	{
		SetFlag(Flags.Reserved1, b: false);
		SetFlag(Flags.Reserved2, b: false);
		SetFlag(Flags.Reserved6, b: false);
	}

	public override void Hurt(HitInfo info)
	{
		if (owner != null)
		{
			owner.Hurt(info);
		}
	}

	public override void AdminKill()
	{
		if (owner != null)
		{
			owner.AdminKill();
		}
		else
		{
			base.AdminKill();
		}
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		ClearDirection();
	}

	public bool CanMove()
	{
		if (VehicleTrigger.HasContents && VehicleTrigger.entityContents != null)
		{
			foreach (BaseEntity entityContent in VehicleTrigger.entityContents)
			{
				if (!vehiclePrefabWhitelist.Contains(entityContent.prefabID))
				{
					return false;
				}
			}
		}
		return true;
	}

	public virtual void NotifyNewFloor(int newFloor, int totalFloors)
	{
	}

	private void ToggleMovementCollider(bool state)
	{
		if (MovementCollider != null)
		{
			MovementCollider.SetActive(state);
		}
	}

	public override void OnFlagsChanged(Flags old, Flags next)
	{
		base.OnFlagsChanged(old, next);
		ToggleMovementCollider(!next.HasFlag(Flags.Busy));
	}
}
