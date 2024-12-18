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

public class TinCanAlarm : DecayEntity, IDetector
{
	[Space]
	public LineRenderer lineRenderer;

	public Transform wireOrigin;

	public Transform wireOriginClient;

	public PlayerDetectionTrigger trigger;

	public Transform wireEndCollider;

	public GroundWatch groundWatch;

	public GroundWatch wireGroundWatch;

	public Animator animator;

	[Space]
	public SoundDefinition alarmSoundDef;

	public SoundDefinition armSoundDef;

	private Vector3 endPoint;

	private const Flags Flag_Used = Flags.Reserved5;

	private BaseEntity lastTriggerEntity;

	private float lastTriggerTime;

	private BasePlayer usingPlayer;

	[Space]
	public float maxWireLength = 10f;

	private const int WIRE_PLACEMENT_LAYER = 1084293377;

	public Transform WireOrigin
	{
		get
		{
			_ = base.isServer;
			return wireOrigin;
		}
	}

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("TinCanAlarm.OnRpcMessage"))
		{
			if (rpc == 3384266798u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_SetEndPoint ");
				}
				using (TimeWarning.New("RPC_SetEndPoint"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg2 = rPCMessage;
							RPC_SetEndPoint(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_SetEndPoint");
					}
				}
				return true;
			}
			if (rpc == 3516830045u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SERVER_StartArming ");
				}
				using (TimeWarning.New("SERVER_StartArming"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(3516830045u, "SERVER_StartArming", this, player, 3f))
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
							RPCMessage msg3 = rPCMessage;
							SERVER_StartArming(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in SERVER_StartArming");
					}
				}
				return true;
			}
			if (rpc == 3508772935u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SERVER_StopArming ");
				}
				using (TimeWarning.New("SERVER_StopArming"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg4 = rPCMessage;
							SERVER_StopArming(msg4);
						}
					}
					catch (Exception exception3)
					{
						Debug.LogException(exception3);
						player.Kick("RPC Error in SERVER_StopArming");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public bool IsUsed()
	{
		return HasFlag(Flags.Reserved5);
	}

	private bool IsArmed()
	{
		return endPoint != Vector3.zero;
	}

	public override bool CanPickup(BasePlayer player)
	{
		if (base.CanPickup(player))
		{
			return player.GetBuildingPrivilege() != null;
		}
		return false;
	}

	public bool ShouldTrigger()
	{
		return IsArmed();
	}

	public void OnObjects()
	{
	}

	public void OnObjectAdded(GameObject obj, Collider col)
	{
		BaseEntity baseEntity = obj.ToBaseEntity();
		if (!(baseEntity != null))
		{
			return;
		}
		if (baseEntity is BuildingBlock && IsGoingThroughWalls(endPoint))
		{
			CutWire();
			return;
		}
		if (baseEntity is BasePlayer { isMounted: not false } basePlayer)
		{
			baseEntity = basePlayer.GetMounted();
		}
		else
		{
			BaseEntity baseEntity2 = baseEntity.GetParentEntity();
			if (baseEntity2 != null)
			{
				baseEntity = baseEntity2;
			}
		}
		if ((!(UnityEngine.Time.realtimeSinceStartup - lastTriggerTime < 1f) || !(baseEntity == lastTriggerEntity)) && (baseEntity is BasePlayer || baseEntity is Door || baseEntity is BaseNpc || baseEntity is BaseVehicle || baseEntity is Elevator))
		{
			lastTriggerTime = UnityEngine.Time.realtimeSinceStartup;
			lastTriggerEntity = baseEntity;
			TriggerAlarm();
		}
	}

	public void OnEmpty()
	{
	}

	private void TriggerAlarm()
	{
		ClientRPC(RpcTarget.NetworkGroup("RPC_TriggerAlarm"));
	}

	public void ServerOnWireDeploying()
	{
		if (!usingPlayer.IsValid() || !usingPlayer.IsConnected)
		{
			PlayerStopsArming(usingPlayer);
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	public void SERVER_StartArming(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (!IsUsed() && player.CanBuild())
		{
			PlayerStartsArming(player);
		}
	}

	[RPC_Server]
	public void SERVER_StopArming(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (player.CanBuild())
		{
			PlayerStopsArming(player);
		}
	}

	public void PlayerStartsArming(BasePlayer player)
	{
		if (!IsUsed() && !(player == null))
		{
			usingPlayer = player;
			SetFlag(Flags.Reserved5, b: true);
			if (IsInvoking(ServerOnWireDeploying))
			{
				CancelInvoke(ServerOnWireDeploying);
			}
			InvokeRepeating(ServerOnWireDeploying, 0f, 0f);
			ClientRPC(RpcTarget.Player("CLIENT_StartArming", player));
		}
	}

	public void PlayerStopsArming(BasePlayer player)
	{
		usingPlayer = null;
		SetFlag(Flags.Reserved5, b: false);
		CancelInvoke(ServerOnWireDeploying);
		ClientRPC(RpcTarget.Player("CLIENT_StopArming", player));
	}

	public void CutWire()
	{
		TriggerAlarm();
		endPoint = Vector3.zero;
		SendNetworkUpdate();
	}

	private void UpdateWireTip()
	{
		if (base.isServer)
		{
			if (!IsArmed())
			{
				wireEndCollider.SetActive(active: false);
				return;
			}
			wireEndCollider.position = endPoint;
			wireEndCollider.SetActive(active: true);
		}
	}

	private void OnGroundMissing()
	{
		if (!base.IsDestroyed && !base.isClient)
		{
			if (!groundWatch.OnGround())
			{
				Kill(DestroyMode.Gib);
			}
			else if (!wireGroundWatch.OnGround())
			{
				CutWire();
			}
		}
	}

	public override void OnDeployed(BaseEntity parent, BasePlayer deployedBy, Item fromItem)
	{
		base.OnDeployed(parent, deployedBy, fromItem);
		if (base.isServer)
		{
			PlayerStartsArming(deployedBy);
		}
	}

	public override void OnAttacked(HitInfo info)
	{
		base.OnAttacked(info);
		if (info.hasDamage && !info.damageTypes.Has(DamageType.Heat))
		{
			TriggerAlarm();
		}
	}

	private void UpdateTrigger()
	{
		if (!IsArmed())
		{
			trigger.SetActive(active: false);
			return;
		}
		trigger.SetActive(active: true);
		Vector3 position = wireOrigin.position;
		Vector3 vector = endPoint;
		Vector3 position2 = (position + vector) / 2f;
		Vector3 forward = vector - position;
		float magnitude = forward.magnitude;
		trigger.transform.position = position2;
		Vector3 localScale = trigger.transform.localScale;
		localScale.z = magnitude;
		trigger.transform.rotation = Quaternion.LookRotation(forward);
		trigger.transform.localScale = new Vector3(0.15f, 0.15f, localScale.z);
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.tinCanAlarm = Facepunch.Pool.Get<ProtoBuf.TinCanAlarm>();
		info.msg.tinCanAlarm.endPoint = endPoint;
		UpdateTrigger();
		UpdateWireTip();
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.tinCanAlarm != null)
		{
			endPoint = info.msg.tinCanAlarm.endPoint;
			UpdateTrigger();
			if (info.fromDisk && !usingPlayer.IsValid())
			{
				PlayerStopsArming(usingPlayer);
			}
		}
	}

	[RPC_Server]
	public void RPC_SetEndPoint(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		Vector3 vector = msg.read.Vector3();
		if (player.CanBuild() && player.IsVisibleAndCanSee(vector) && !IsGoingThroughWalls(vector) && IsInValidVolume(vector) && IsOnValidEntities(vector) && !(Vector3.Distance(wireOrigin.position, vector) > maxWireLength))
		{
			endPoint = vector;
			SendNetworkUpdate();
			PlayerStopsArming(player);
		}
	}

	private bool IsGoingThroughWalls(Vector3 position)
	{
		float maxDistance = Vector3.Distance(wireOrigin.position, position);
		Vector3 vector = position - wireOrigin.position;
		RaycastHit hitInfo;
		bool flag = GamePhysics.Trace(new Ray(wireOrigin.position, vector), 0f, out hitInfo, maxDistance, 1218519297, QueryTriggerInteraction.Ignore, this);
		if (!flag)
		{
			flag = GamePhysics.Trace(new Ray(position, -vector), 0f, out var _, maxDistance, 1218519297, QueryTriggerInteraction.Ignore, this);
		}
		return flag;
	}

	private bool IsInValidVolume(Vector3 position)
	{
		List<Collider> obj = Facepunch.Pool.Get<List<Collider>>();
		GamePhysics.OverlapSphere(position, 0.1f, obj, 536870912, QueryTriggerInteraction.Collide);
		bool result = true;
		foreach (Collider item in obj)
		{
			if (item.gameObject.HasCustomTag(GameObjectTag.BlockPlacement))
			{
				result = false;
				break;
			}
			if (!(item.GetMonument() != null))
			{
				ColliderInfo component = item.GetComponent<ColliderInfo>();
				if (!(component != null) || !component.HasFlag(ColliderInfo.Flags.Tunnels))
				{
					result = false;
				}
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return result;
	}

	private bool IsOnValidEntities(Vector3 position)
	{
		List<BaseEntity> obj = Facepunch.Pool.Get<List<BaseEntity>>();
		Vis.Entities(position, 0.1f, obj, 1084293377);
		bool result = true;
		foreach (BaseEntity item in obj)
		{
			if (item is AnimatedBuildingBlock || item is ElevatorLift || item is Elevator)
			{
				result = false;
				break;
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return result;
	}
}
