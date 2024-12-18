#define UNITY_ASSERTIONS
using System;
using ConVar;
using Network;
using UnityEngine;
using UnityEngine.Assertions;

public class HBHFSensor : BaseDetector
{
	public GameObjectRef detectUp;

	public GameObjectRef detectDown;

	public GameObjectRef panelPrefab;

	public const Flags Flag_IncludeOthers = Flags.Reserved2;

	public const Flags Flag_IncludeAuthed = Flags.Reserved3;

	private int detectedPlayers;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("HBHFSensor.OnRpcMessage"))
		{
			if (rpc == 4073303808u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SetConfig ");
				}
				using (TimeWarning.New("SetConfig"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(4073303808u, "SetConfig", this, player, 3f))
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
							RPCMessage config = rPCMessage;
							SetConfig(config);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in SetConfig");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override int GetPassthroughAmount(int outputSlot = 0)
	{
		return Mathf.Min(detectedPlayers, GetCurrentEnergy());
	}

	public override void OnObjects()
	{
		base.OnObjects();
		UpdatePassthroughAmount();
		InvokeRandomized(UpdatePassthroughAmount, 0f, 1f, 0.1f);
	}

	public override void OnEmpty()
	{
		base.OnEmpty();
		UpdatePassthroughAmount();
		CancelInvoke(UpdatePassthroughAmount);
	}

	public void UpdatePassthroughAmount()
	{
		if (base.isClient)
		{
			return;
		}
		int num = detectedPlayers;
		detectedPlayers = 0;
		if (myTrigger.entityContents != null && myTrigger.entityContents.Count > 0)
		{
			BuildingPrivlidge buildingPrivilege = GetBuildingPrivilege();
			foreach (BaseEntity entityContent in myTrigger.entityContents)
			{
				if (entityContent is BasePlayer basePlayer)
				{
					bool flag = buildingPrivilege != null && buildingPrivilege.IsAuthed(basePlayer);
					if ((!flag || ShouldIncludeAuthorized()) && (flag || ShouldIncludeOthers()) && entityContent.IsVisible(base.transform.position + base.transform.forward * 0.1f, 10f) && basePlayer != null && basePlayer.IsAlive() && !basePlayer.IsSleeping() && basePlayer.isServer)
					{
						detectedPlayers++;
					}
				}
			}
		}
		if (num != detectedPlayers && IsPowered())
		{
			MarkDirty();
			if (detectedPlayers > num)
			{
				Effect.server.Run(detectUp.resourcePath, base.transform.position, Vector3.up);
			}
			else if (detectedPlayers < num)
			{
				Effect.server.Run(detectDown.resourcePath, base.transform.position, Vector3.up);
			}
		}
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void SetConfig(RPCMessage msg)
	{
		if (CanUse(msg.player))
		{
			bool b = msg.read.Bit();
			bool b2 = msg.read.Bit();
			SetFlag(Flags.Reserved3, b);
			SetFlag(Flags.Reserved2, b2);
		}
	}

	public bool CanUse(BasePlayer player)
	{
		return player.CanBuild();
	}

	public bool ShouldIncludeAuthorized()
	{
		return HasFlag(Flags.Reserved3);
	}

	public bool ShouldIncludeOthers()
	{
		return HasFlag(Flags.Reserved2);
	}
}
