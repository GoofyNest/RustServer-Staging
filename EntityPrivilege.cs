#define UNITY_ASSERTIONS
using System;
using System.Linq;
using ConVar;
using Facepunch.Rust;
using Network;
using ProtoBuf;
using UnityEngine;
using UnityEngine.Assertions;

public class EntityPrivilege : SimplePrivilege
{
	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("EntityPrivilege.OnRpcMessage"))
		{
			if (rpc == 1092560690 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - AddSelfAuthorize ");
				}
				using (TimeWarning.New("AddSelfAuthorize"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(1092560690u, "AddSelfAuthorize", this, player, 3f))
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
							AddSelfAuthorize(rpc2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in AddSelfAuthorize");
					}
				}
				return true;
			}
			if (rpc == 253307592 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - ClearList ");
				}
				using (TimeWarning.New("ClearList"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(253307592u, "ClearList", this, player, 3f))
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
							RPCMessage rpc3 = rPCMessage;
							ClearList(rpc3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in ClearList");
					}
				}
				return true;
			}
			if (rpc == 3617985969u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RemoveSelfAuthorize ");
				}
				using (TimeWarning.New("RemoveSelfAuthorize"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(3617985969u, "RemoveSelfAuthorize", this, player, 3f))
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
							RPCMessage rpc4 = rPCMessage;
							RemoveSelfAuthorize(rpc4);
						}
					}
					catch (Exception exception3)
					{
						Debug.LogException(exception3);
						player.Kick("RPC Error in RemoveSelfAuthorize");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	private void AddSelfAuthorize(RPCMessage rpc)
	{
		if (rpc.player.CanInteract())
		{
			AddPlayer(rpc.player);
			SendNetworkUpdate();
		}
	}

	public void AddPlayer(BasePlayer player)
	{
		if (!AtMaxAuthCapacity())
		{
			authorizedPlayers.RemoveAll((PlayerNameID x) => x.userid == (ulong)player.userID);
			PlayerNameID playerNameID = new PlayerNameID();
			playerNameID.userid = player.userID;
			playerNameID.username = player.displayName;
			authorizedPlayers.Add(playerNameID);
			Analytics.Azure.OnEntityAuthChanged(this, player, authorizedPlayers.Select((PlayerNameID x) => x.userid), "added", player.userID);
			UpdateMaxAuthCapacity();
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	private void RemoveSelfAuthorize(RPCMessage rpc)
	{
		if (rpc.player.CanInteract())
		{
			authorizedPlayers.RemoveAll((PlayerNameID x) => x.userid == (ulong)rpc.player.userID);
			Analytics.Azure.OnEntityAuthChanged(this, rpc.player, authorizedPlayers.Select((PlayerNameID x) => x.userid), "removed", rpc.player.userID);
			UpdateMaxAuthCapacity();
			SendNetworkUpdate();
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	private void ClearList(RPCMessage rpc)
	{
		if (rpc.player.CanInteract())
		{
			authorizedPlayers.Clear();
			UpdateMaxAuthCapacity();
			SendNetworkUpdate();
		}
	}
}
