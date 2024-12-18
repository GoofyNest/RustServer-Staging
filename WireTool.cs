#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Facepunch;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

public class WireTool : HeldEntity
{
	public enum WireColour
	{
		Gray,
		Red,
		Green,
		Blue,
		Yellow,
		Pink,
		Purple,
		Orange,
		White,
		LightBlue,
		Invisible,
		Count
	}

	public struct PendingPlug
	{
		public IOEntity ent;

		public bool isInput;

		public int index;
	}

	private const int maxLineNodes = 16;

	private const float industrialWallOffset = 0.04f;

	public IOEntity.IOType wireType;

	public WireColour DefaultColor;

	public float radialMenuHoldTime = 0.25f;

	public float disconnectDelay = 0.15f;

	public float clearDelay = 0.65f;

	private bool justCleared;

	public GameObjectRef plugEffect;

	public SoundDefinition clearStartSoundDef;

	public SoundDefinition clearSoundDef;

	public PendingPlug pendingPlug;

	private const float IndustrialThickness = 0.01f;

	private bool CanChangeColours
	{
		get
		{
			IOEntity.IOType iOType = wireType;
			return iOType == IOEntity.IOType.Electric || iOType == IOEntity.IOType.Fluidic || iOType == IOEntity.IOType.Industrial;
		}
	}

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("WireTool.OnRpcMessage"))
		{
			if (rpc == 2571821359u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_MakeConnection ");
				}
				using (TimeWarning.New("RPC_MakeConnection"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(2571821359u, "RPC_MakeConnection", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(2571821359u, "RPC_MakeConnection", this, player))
						{
							return true;
						}
						if (!RPC_Server.IsActiveItem.Test(2571821359u, "RPC_MakeConnection", this, player))
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
							RPCMessage rpc2 = rPCMessage;
							RPC_MakeConnection(rpc2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_MakeConnection");
					}
				}
				return true;
			}
			if (rpc == 986119119 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_RequestChangeColor ");
				}
				using (TimeWarning.New("RPC_RequestChangeColor"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(986119119u, "RPC_RequestChangeColor", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(986119119u, "RPC_RequestChangeColor", this, player))
						{
							return true;
						}
						if (!RPC_Server.IsActiveItem.Test(986119119u, "RPC_RequestChangeColor", this, player))
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
							RPC_RequestChangeColor(msg2);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in RPC_RequestChangeColor");
					}
				}
				return true;
			}
			if (rpc == 1514179840 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_RequestClear ");
				}
				using (TimeWarning.New("RPC_RequestClear"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(1514179840u, "RPC_RequestClear", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(1514179840u, "RPC_RequestClear", this, player))
						{
							return true;
						}
						if (!RPC_Server.IsActiveItem.Test(1514179840u, "RPC_RequestClear", this, player))
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
							RPC_RequestClear(msg3);
						}
					}
					catch (Exception exception3)
					{
						Debug.LogException(exception3);
						player.Kick("RPC Error in RPC_RequestClear");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	private float GetMaxWireLength(BasePlayer forPlayer)
	{
		if (forPlayer == null || !forPlayer.IsInCreativeMode || !Creative.unlimitedIo)
		{
			return 30f;
		}
		return 200f;
	}

	[RPC_Server]
	[RPC_Server.IsActiveItem]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(5uL)]
	public void RPC_MakeConnection(RPCMessage rpc)
	{
		BasePlayer player = rpc.player;
		if (!CanPlayerUseWires(player))
		{
			return;
		}
		WireConnectionMessage wireConnectionMessage = WireConnectionMessage.Deserialize(rpc.read);
		List<Vector3> linePoints = wireConnectionMessage.linePoints;
		int inputIndex = wireConnectionMessage.inputIndex;
		int outputIndex = wireConnectionMessage.outputIndex;
		IOEntity iOEntity = new EntityRef<IOEntity>(wireConnectionMessage.inputID).Get(serverside: true);
		IOEntity iOEntity2 = new EntityRef<IOEntity>(wireConnectionMessage.outputID).Get(serverside: true);
		if (!(iOEntity == null) && !(iOEntity2 == null) && ValidateLine(linePoints, iOEntity, iOEntity2, player, outputIndex) && inputIndex < iOEntity.inputs.Length && outputIndex < iOEntity2.outputs.Length && !(iOEntity.inputs[inputIndex].connectedTo.Get() != null) && !(iOEntity2.outputs[outputIndex].connectedTo.Get() != null) && (!iOEntity.inputs[inputIndex].rootConnectionsOnly || iOEntity2.IsRootEntity()) && CanModifyEntity(player, iOEntity) && CanModifyEntity(player, iOEntity2))
		{
			List<float> slackLevels = wireConnectionMessage.slackLevels;
			IOEntity.LineAnchor[] array = new IOEntity.LineAnchor[wireConnectionMessage.lineAnchors.Count];
			for (int i = 0; i < wireConnectionMessage.lineAnchors.Count; i++)
			{
				WireLineAnchorInfo wireLineAnchorInfo = wireConnectionMessage.lineAnchors[i];
				array[i].entityRef = new EntityRef<Door>(wireLineAnchorInfo.parentID);
				array[i].boneName = wireLineAnchorInfo.boneName;
				array[i].index = (int)wireLineAnchorInfo.index;
				array[i].position = wireLineAnchorInfo.position;
			}
			WireColour wireColour = IntToColour(wireConnectionMessage.wireColor);
			if (wireColour == WireColour.Invisible && !player.IsInCreativeMode)
			{
				wireColour = DefaultColor;
			}
			iOEntity2.ConnectTo(iOEntity, outputIndex, inputIndex, linePoints, slackLevels, array, wireColour);
			if (wireType == IOEntity.IOType.Industrial)
			{
				iOEntity.NotifyIndustrialNetworkChanged();
				iOEntity2.NotifyIndustrialNetworkChanged();
			}
		}
	}

	[RPC_Server]
	[RPC_Server.IsActiveItem]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(5uL)]
	public void RPC_RequestClear(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (!CanPlayerUseWires(player))
		{
			return;
		}
		NetworkableId uid = msg.read.EntityID();
		int num = msg.read.Int32();
		bool flag = msg.read.Bit();
		bool flag2 = msg.read.Bit();
		IOEntity iOEntity = BaseNetworkable.serverEntities.Find(uid) as IOEntity;
		if (iOEntity == null)
		{
			return;
		}
		WireReconnectMessage wireReconnectMessage = Facepunch.Pool.Get<WireReconnectMessage>();
		if (flag2)
		{
			IOEntity.IOSlot iOSlot = (flag ? iOEntity.inputs : iOEntity.outputs)[num];
			IOEntity iOEntity2 = iOSlot.connectedTo.Get();
			if (iOEntity2 == null)
			{
				return;
			}
			IOEntity.IOSlot iOSlot2 = (flag ? iOEntity2.outputs : iOEntity2.inputs)[iOSlot.connectedToSlot];
			wireReconnectMessage.isInput = !flag;
			wireReconnectMessage.slotIndex = iOSlot.connectedToSlot;
			wireReconnectMessage.entityId = iOSlot.connectedTo.Get().net.ID;
			wireReconnectMessage.wireColor = (int)iOSlot.wireColour;
			wireReconnectMessage.linePoints = Facepunch.Pool.Get<List<Vector3>>();
			wireReconnectMessage.slackLevels = Facepunch.Pool.Get<List<float>>();
			wireReconnectMessage.lineAnchors = Facepunch.Pool.Get<List<WireLineAnchorInfo>>();
			IOEntity iOEntity3 = iOEntity;
			Vector3[] array = iOSlot.linePoints;
			IOEntity.IOSlot iOSlot3 = iOSlot;
			if (array == null || array.Length == 0)
			{
				iOEntity3 = iOEntity2;
				array = iOSlot2.linePoints;
				iOSlot3 = iOSlot2;
			}
			if (array == null)
			{
				array = Array.Empty<Vector3>();
			}
			bool flag3 = iOEntity3 != iOEntity;
			if (iOEntity == iOEntity3 && flag)
			{
				flag3 = true;
			}
			wireReconnectMessage.linePoints.AddRange(array);
			float[] slackLevels = iOSlot.slackLevels;
			if (slackLevels == null || slackLevels.Length == 0)
			{
				slackLevels = iOSlot2.slackLevels;
			}
			float[] array2 = slackLevels;
			foreach (float item in array2)
			{
				wireReconnectMessage.slackLevels.Add(item);
			}
			IOEntity.LineAnchor[] lineAnchors = iOSlot.lineAnchors;
			if (lineAnchors == null || lineAnchors.Length == 0)
			{
				lineAnchors = iOSlot2.lineAnchors;
			}
			if (lineAnchors != null)
			{
				IOEntity.LineAnchor[] array3 = lineAnchors;
				for (int i = 0; i < array3.Length; i++)
				{
					IOEntity.LineAnchor lineAnchor = array3[i];
					EntityRef<Door> entityRef = lineAnchor.entityRef;
					if (entityRef.Get(serverside: true).IsValid())
					{
						wireReconnectMessage.lineAnchors.Add(lineAnchor.ToInfo());
					}
				}
			}
			wireReconnectMessage.slackLevels.RemoveAt(wireReconnectMessage.slackLevels.Count - 1);
			if (flag3)
			{
				wireReconnectMessage.linePoints.Reverse();
				wireReconnectMessage.slackLevels.Reverse();
				int num2 = wireReconnectMessage.linePoints.Count - 1;
				foreach (WireLineAnchorInfo lineAnchor2 in wireReconnectMessage.lineAnchors)
				{
					lineAnchor2.index = num2 - lineAnchor2.index;
				}
			}
			if (wireReconnectMessage.lineAnchors.Count >= 0)
			{
				List<WireLineAnchorInfo> obj = Facepunch.Pool.Get<List<WireLineAnchorInfo>>();
				foreach (WireLineAnchorInfo lineAnchor3 in wireReconnectMessage.lineAnchors)
				{
					if (lineAnchor3.index == 0L || lineAnchor3.index == wireReconnectMessage.linePoints.Count - 1)
					{
						obj.Add(lineAnchor3);
					}
				}
				foreach (WireLineAnchorInfo item2 in obj)
				{
					wireReconnectMessage.lineAnchors.Remove(item2);
				}
				Facepunch.Pool.Free(ref obj, freeElements: false);
			}
			if (wireReconnectMessage.linePoints.Count >= 0)
			{
				wireReconnectMessage.linePoints.RemoveAt(0);
				wireReconnectMessage.linePoints.RemoveAt(wireReconnectMessage.linePoints.Count - 1);
			}
			if (wireReconnectMessage.slackLevels.Count >= 0)
			{
				wireReconnectMessage.slackLevels.RemoveAt(wireReconnectMessage.slackLevels.Count - 1);
			}
			for (int j = 0; j < wireReconnectMessage.linePoints.Count; j++)
			{
				Vector3 vector = Quaternion.Euler(iOSlot3.originRotation) * wireReconnectMessage.linePoints[j];
				Vector3 value = iOSlot3.originPosition + vector;
				wireReconnectMessage.linePoints[j] = value;
			}
		}
		if (AttemptClearSlot(iOEntity, player, num, flag) && flag2)
		{
			ClientRPC(RpcTarget.Player("RPC_OnWireDisconnected", player), wireReconnectMessage);
		}
	}

	[RPC_Server]
	[RPC_Server.IsActiveItem]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(5uL)]
	public void RPC_RequestChangeColor(RPCMessage msg)
	{
		if (!CanPlayerUseWires(msg.player))
		{
			return;
		}
		NetworkableId uid = msg.read.EntityID();
		IOEntity iOEntity = BaseNetworkable.serverEntities.Find(uid) as IOEntity;
		if (iOEntity == null)
		{
			return;
		}
		int index = msg.read.Int32();
		bool flag = msg.read.Bit();
		WireColour wireColour = IntToColour(msg.read.Int32());
		IOEntity.IOSlot iOSlot = (flag ? iOEntity.inputs.ElementAtOrDefault(index) : iOEntity.outputs.ElementAtOrDefault(index));
		if (iOSlot != null)
		{
			IOEntity iOEntity2 = iOSlot.connectedTo.Get();
			if (!(iOEntity2 == null))
			{
				IOEntity.IOSlot obj = (flag ? iOEntity2.outputs : iOEntity2.inputs)[iOSlot.connectedToSlot];
				iOSlot.wireColour = wireColour;
				iOEntity.SendNetworkUpdate();
				obj.wireColour = wireColour;
				iOEntity2.SendNetworkUpdate();
			}
		}
	}

	public static bool AttemptClearSlot(BaseNetworkable clearEnt, BasePlayer ply, int clearIndex, bool isInput)
	{
		IOEntity iOEntity = ((clearEnt != null) ? clearEnt.GetComponent<IOEntity>() : null);
		if (iOEntity == null)
		{
			return false;
		}
		if (ply != null && !CanModifyEntity(ply, iOEntity))
		{
			return false;
		}
		return iOEntity.Disconnect(clearIndex, isInput);
	}

	private WireColour IntToColour(int i)
	{
		i %= 11;
		return (WireColour)i;
	}

	private bool ValidateLine(List<Vector3> lineList, IOEntity inputEntity, IOEntity outputEntity, BasePlayer byPlayer, int outputIndex)
	{
		if (byPlayer != null && byPlayer.IsInCreativeMode && Creative.unlimitedIo)
		{
			return true;
		}
		if (lineList.Count < 2 || lineList.Count > 18)
		{
			return false;
		}
		if (inputEntity == null || outputEntity == null)
		{
			return false;
		}
		Vector3 a = lineList[0];
		float num = 0f;
		int count = lineList.Count;
		float maxWireLength = GetMaxWireLength(byPlayer);
		for (int i = 1; i < count; i++)
		{
			Vector3 vector = lineList[i];
			num += Vector3.Distance(a, vector);
			if (num > maxWireLength)
			{
				return false;
			}
			a = vector;
		}
		Vector3 point = lineList[count - 1];
		Bounds bounds = outputEntity.bounds;
		bounds.Expand(0.5f);
		if (!bounds.Contains(point))
		{
			return false;
		}
		Vector3 position = outputEntity.transform.TransformPoint(lineList[0]);
		point = inputEntity.transform.InverseTransformPoint(position);
		Bounds bounds2 = inputEntity.bounds;
		bounds2.Expand(0.5f);
		if (!bounds2.Contains(point))
		{
			return false;
		}
		if (byPlayer == null)
		{
			return false;
		}
		Vector3 position2 = outputEntity.transform.TransformPoint(lineList[lineList.Count - 1]);
		if (byPlayer.Distance(position2) > 5f && byPlayer.Distance(position) > 5f)
		{
			return false;
		}
		if (outputIndex >= 0 && outputIndex < outputEntity.outputs.Length && outputEntity.outputs[outputIndex].type == IOEntity.IOType.Industrial && !VerifyLineOfSight(lineList, outputEntity.transform.localToWorldMatrix))
		{
			return false;
		}
		return true;
	}

	private bool VerifyLineOfSight(List<Vector3> positions, Matrix4x4 localToWorldSpace)
	{
		Vector3 worldSpaceA = localToWorldSpace.MultiplyPoint3x4(positions[0]);
		for (int i = 1; i < positions.Count; i++)
		{
			Vector3 vector = localToWorldSpace.MultiplyPoint3x4(positions[i]);
			if (!VerifyLineOfSight(worldSpaceA, vector))
			{
				return false;
			}
			worldSpaceA = vector;
		}
		return true;
	}

	private bool VerifyLineOfSight(Vector3 worldSpaceA, Vector3 worldSpaceB)
	{
		float maxDistance = Vector3.Distance(worldSpaceA, worldSpaceB);
		Vector3 normalized = (worldSpaceA - worldSpaceB).normalized;
		List<RaycastHit> obj = Facepunch.Pool.Get<List<RaycastHit>>();
		GamePhysics.TraceAll(new Ray(worldSpaceB, normalized), 0.01f, obj, maxDistance, 2162944);
		bool result = true;
		foreach (RaycastHit item in obj)
		{
			BaseEntity entity = item.GetEntity();
			if (entity != null && item.IsOnLayer(Rust.Layer.Deployed))
			{
				if (entity is VendingMachine)
				{
					result = false;
					break;
				}
			}
			else if (!(entity != null) || !(entity is Door))
			{
				result = false;
				break;
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return result;
	}

	public bool HasPendingPlug()
	{
		if (pendingPlug.ent != null)
		{
			return pendingPlug.index != -1;
		}
		return false;
	}

	public bool PendingPlugIsInput()
	{
		if (pendingPlug.ent != null && pendingPlug.index != -1)
		{
			return pendingPlug.isInput;
		}
		return false;
	}

	public bool PendingPlugIsType(IOEntity.IOType type)
	{
		if (pendingPlug.ent == null || pendingPlug.index == -1)
		{
			return false;
		}
		IOEntity.IOSlot[] array = (pendingPlug.isInput ? pendingPlug.ent.inputs : pendingPlug.ent.outputs);
		if (pendingPlug.index < 0 || pendingPlug.index >= array.Length)
		{
			return false;
		}
		return array[pendingPlug.index].type == type;
	}

	public bool PendingPlugIsOutput()
	{
		if (pendingPlug.ent != null && pendingPlug.index != -1)
		{
			return !pendingPlug.isInput;
		}
		return false;
	}

	public bool PendingPlugIsRoot()
	{
		if (pendingPlug.ent != null)
		{
			return pendingPlug.ent.IsRootEntity();
		}
		return false;
	}

	private void ResetPendingPlug()
	{
		pendingPlug.ent = null;
		pendingPlug.index = -1;
	}

	public static bool CanPlayerUseWires(BasePlayer player)
	{
		if (player != null && player.IsInCreativeMode && Creative.unlimitedIo)
		{
			return true;
		}
		if (!player.CanBuild())
		{
			return false;
		}
		List<Collider> obj = Facepunch.Pool.Get<List<Collider>>();
		GamePhysics.OverlapSphere(player.eyes.position, 0.1f, obj, 536870912, QueryTriggerInteraction.Collide);
		bool result = true;
		foreach (Collider item in obj)
		{
			if (!item.gameObject.CompareTag("IgnoreWireCheck"))
			{
				result = false;
				break;
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return result;
	}

	private static bool CanModifyEntity(BasePlayer player, IOEntity ent)
	{
		if (ent.AllowWireConnections())
		{
			if (!player.CanBuild(ent.transform.position, ent.transform.rotation, ent.bounds))
			{
				if (player.IsInCreativeMode)
				{
					return Creative.unlimitedIo;
				}
				return false;
			}
			return true;
		}
		return false;
	}
}
