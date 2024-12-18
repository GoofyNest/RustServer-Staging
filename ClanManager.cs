#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConVar;
using Facepunch;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

public class ClanManager : BaseEntity
{
	private RealTimeSince _sinceLastLeaderboardUpdate;

	private List<ClanLeaderboardEntry> _leaderboardCache;

	public static readonly TokenisedPhrase InvitationToast = new TokenisedPhrase("clan.invitation.toast", "You were invited to {clanName}! Press [clan.toggleclan] to manage your clan invitations.");

	public const int LogoSize = 512;

	private string _backendType;

	private ClanChangeTracker _changeTracker;

	private const int MaxMetadataRequestsPerSecond = 3;

	private const float MaxMetadataRequestInterval = 0.5f;

	private const float MetadataExpiry = 300f;

	private readonly Dictionary<long, List<Connection>> _clanMemberConnections = new Dictionary<long, List<Connection>>();

	public static ClanManager ServerInstance { get; private set; }

	public IClanBackend Backend { get; private set; }

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("ClanManager.OnRpcMessage"))
		{
			if (rpc == 3593616087u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_AcceptInvitation ");
				}
				using (TimeWarning.New("Server_AcceptInvitation"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3593616087u, "Server_AcceptInvitation", this, player, 3uL))
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
							Server_AcceptInvitation(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in Server_AcceptInvitation");
					}
				}
				return true;
			}
			if (rpc == 73135447 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_CancelInvitation ");
				}
				using (TimeWarning.New("Server_CancelInvitation"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(73135447u, "Server_CancelInvitation", this, player, 3uL))
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
							Server_CancelInvitation(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in Server_CancelInvitation");
					}
				}
				return true;
			}
			if (rpc == 785874715 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_CancelInvite ");
				}
				using (TimeWarning.New("Server_CancelInvite"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(785874715u, "Server_CancelInvite", this, player, 3uL))
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
							RPCMessage msg4 = rPCMessage;
							Server_CancelInvite(msg4);
						}
					}
					catch (Exception exception3)
					{
						Debug.LogException(exception3);
						player.Kick("RPC Error in Server_CancelInvite");
					}
				}
				return true;
			}
			if (rpc == 4017901233u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_CreateClan ");
				}
				using (TimeWarning.New("Server_CreateClan"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(4017901233u, "Server_CreateClan", this, player, 3uL))
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
							RPCMessage msg5 = rPCMessage;
							Server_CreateClan(msg5);
						}
					}
					catch (Exception exception4)
					{
						Debug.LogException(exception4);
						player.Kick("RPC Error in Server_CreateClan");
					}
				}
				return true;
			}
			if (rpc == 835697933 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_CreateRole ");
				}
				using (TimeWarning.New("Server_CreateRole"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(835697933u, "Server_CreateRole", this, player, 3uL))
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
							RPCMessage msg6 = rPCMessage;
							Server_CreateRole(msg6);
						}
					}
					catch (Exception exception5)
					{
						Debug.LogException(exception5);
						player.Kick("RPC Error in Server_CreateRole");
					}
				}
				return true;
			}
			if (rpc == 3966624879u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_DeleteRole ");
				}
				using (TimeWarning.New("Server_DeleteRole"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3966624879u, "Server_DeleteRole", this, player, 3uL))
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
							RPCMessage msg7 = rPCMessage;
							Server_DeleteRole(msg7);
						}
					}
					catch (Exception exception6)
					{
						Debug.LogException(exception6);
						player.Kick("RPC Error in Server_DeleteRole");
					}
				}
				return true;
			}
			if (rpc == 4071826018u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_GetClan ");
				}
				using (TimeWarning.New("Server_GetClan"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(4071826018u, "Server_GetClan", this, player, 3uL))
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
							RPCMessage msg8 = rPCMessage;
							Server_GetClan(msg8);
						}
					}
					catch (Exception exception7)
					{
						Debug.LogException(exception7);
						player.Kick("RPC Error in Server_GetClan");
					}
				}
				return true;
			}
			if (rpc == 2338234158u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_GetClanMetadata ");
				}
				using (TimeWarning.New("Server_GetClanMetadata"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(2338234158u, "Server_GetClanMetadata", this, player, 3uL))
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
							RPCMessage msg9 = rPCMessage;
							Server_GetClanMetadata(msg9);
						}
					}
					catch (Exception exception8)
					{
						Debug.LogException(exception8);
						player.Kick("RPC Error in Server_GetClanMetadata");
					}
				}
				return true;
			}
			if (rpc == 507204008 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_GetInvitations ");
				}
				using (TimeWarning.New("Server_GetInvitations"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(507204008u, "Server_GetInvitations", this, player, 3uL))
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
							RPCMessage msg10 = rPCMessage;
							Server_GetInvitations(msg10);
						}
					}
					catch (Exception exception9)
					{
						Debug.LogException(exception9);
						player.Kick("RPC Error in Server_GetInvitations");
					}
				}
				return true;
			}
			if (rpc == 1953068009 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_GetLeaderboard ");
				}
				using (TimeWarning.New("Server_GetLeaderboard"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(1953068009u, "Server_GetLeaderboard", this, player, 3uL))
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
							RPCMessage msg11 = rPCMessage;
							Server_GetLeaderboard(msg11);
						}
					}
					catch (Exception exception10)
					{
						Debug.LogException(exception10);
						player.Kick("RPC Error in Server_GetLeaderboard");
					}
				}
				return true;
			}
			if (rpc == 3858074978u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_GetLogs ");
				}
				using (TimeWarning.New("Server_GetLogs"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3858074978u, "Server_GetLogs", this, player, 3uL))
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
							RPCMessage msg12 = rPCMessage;
							Server_GetLogs(msg12);
						}
					}
					catch (Exception exception11)
					{
						Debug.LogException(exception11);
						player.Kick("RPC Error in Server_GetLogs");
					}
				}
				return true;
			}
			if (rpc == 558876504 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_GetScoreEvents ");
				}
				using (TimeWarning.New("Server_GetScoreEvents"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(558876504u, "Server_GetScoreEvents", this, player, 3uL))
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
							RPCMessage msg13 = rPCMessage;
							Server_GetScoreEvents(msg13);
						}
					}
					catch (Exception exception12)
					{
						Debug.LogException(exception12);
						player.Kick("RPC Error in Server_GetScoreEvents");
					}
				}
				return true;
			}
			if (rpc == 1782867876 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_Invite ");
				}
				using (TimeWarning.New("Server_Invite"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(1782867876u, "Server_Invite", this, player, 3uL))
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
							RPCMessage msg14 = rPCMessage;
							Server_Invite(msg14);
						}
					}
					catch (Exception exception13)
					{
						Debug.LogException(exception13);
						player.Kick("RPC Error in Server_Invite");
					}
				}
				return true;
			}
			if (rpc == 3093528332u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_Kick ");
				}
				using (TimeWarning.New("Server_Kick"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3093528332u, "Server_Kick", this, player, 3uL))
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
							RPCMessage msg15 = rPCMessage;
							Server_Kick(msg15);
						}
					}
					catch (Exception exception14)
					{
						Debug.LogException(exception14);
						player.Kick("RPC Error in Server_Kick");
					}
				}
				return true;
			}
			if (rpc == 2235419116u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_SetColor ");
				}
				using (TimeWarning.New("Server_SetColor"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(2235419116u, "Server_SetColor", this, player, 3uL))
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
							RPCMessage msg16 = rPCMessage;
							Server_SetColor(msg16);
						}
					}
					catch (Exception exception15)
					{
						Debug.LogException(exception15);
						player.Kick("RPC Error in Server_SetColor");
					}
				}
				return true;
			}
			if (rpc == 1189444132 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_SetLogo ");
				}
				using (TimeWarning.New("Server_SetLogo"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(1189444132u, "Server_SetLogo", this, player, 3uL))
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
							RPCMessage msg17 = rPCMessage;
							Server_SetLogo(msg17);
						}
					}
					catch (Exception exception16)
					{
						Debug.LogException(exception16);
						player.Kick("RPC Error in Server_SetLogo");
					}
				}
				return true;
			}
			if (rpc == 4088477037u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_SetMotd ");
				}
				using (TimeWarning.New("Server_SetMotd"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(4088477037u, "Server_SetMotd", this, player, 3uL))
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
							RPCMessage msg18 = rPCMessage;
							Server_SetMotd(msg18);
						}
					}
					catch (Exception exception17)
					{
						Debug.LogException(exception17);
						player.Kick("RPC Error in Server_SetMotd");
					}
				}
				return true;
			}
			if (rpc == 285489852 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_SetPlayerNotes ");
				}
				using (TimeWarning.New("Server_SetPlayerNotes"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(285489852u, "Server_SetPlayerNotes", this, player, 3uL))
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
							RPCMessage msg19 = rPCMessage;
							Server_SetPlayerNotes(msg19);
						}
					}
					catch (Exception exception18)
					{
						Debug.LogException(exception18);
						player.Kick("RPC Error in Server_SetPlayerNotes");
					}
				}
				return true;
			}
			if (rpc == 3232449870u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_SetPlayerRole ");
				}
				using (TimeWarning.New("Server_SetPlayerRole"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3232449870u, "Server_SetPlayerRole", this, player, 3uL))
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
							RPCMessage msg20 = rPCMessage;
							Server_SetPlayerRole(msg20);
						}
					}
					catch (Exception exception19)
					{
						Debug.LogException(exception19);
						player.Kick("RPC Error in Server_SetPlayerRole");
					}
				}
				return true;
			}
			if (rpc == 738181899 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_SwapRoles ");
				}
				using (TimeWarning.New("Server_SwapRoles"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(738181899u, "Server_SwapRoles", this, player, 3uL))
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
							RPCMessage msg21 = rPCMessage;
							Server_SwapRoles(msg21);
						}
					}
					catch (Exception exception20)
					{
						Debug.LogException(exception20);
						player.Kick("RPC Error in Server_SwapRoles");
					}
				}
				return true;
			}
			if (rpc == 1548667516 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_UpdateRole ");
				}
				using (TimeWarning.New("Server_UpdateRole"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(1548667516u, "Server_UpdateRole", this, player, 3uL))
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
							RPCMessage msg22 = rPCMessage;
							Server_UpdateRole(msg22);
						}
					}
					catch (Exception exception21)
					{
						Debug.LogException(exception21);
						player.Kick("RPC Error in Server_UpdateRole");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_CreateClan(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		if (!ClanValidator.ValidateClanName(msg.read.String(), out var validated))
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.InvalidText, null, hasClanInfo: false));
			return;
		}
		ClanValueResult<IClan> result = await Backend.Create(msg.player.userID, validated);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.Success, clan));
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_GetClan(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		ClanValueResult<IClan> result = await Backend.Get(msg.player.clanId);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			await clan.RefreshIfStale();
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.Success, clan));
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_GetLogs(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		ClanValueResult<IClan> result = await Backend.Get(msg.player.clanId);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			ClanValueResult<ClanLogs> clanValueResult = await clan.GetLogs(100, msg.player.userID);
			if (clanValueResult.IsSuccess)
			{
				ClientRPC(RpcTarget.Player("Client_ReceiveClanLogs", msg.player), clanValueResult.Value.ToProto());
			}
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, clanValueResult.Result, clan));
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_GetScoreEvents(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		ClanValueResult<IClan> result = await Backend.Get(msg.player.clanId);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			ClanValueResult<ClanScoreEvents> clanValueResult = await clan.GetScoreEvents(100, msg.player.userID);
			if (clanValueResult.IsSuccess)
			{
				ClientRPCPlayer(null, msg.player, "Client_ReceiveClanScoreEvents", clanValueResult.Value.ToProto());
			}
			ClientRPCPlayer(null, msg.player, "Client_ReceiveActionResult", BuildActionResult(requestId, clanValueResult.Result, clan));
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_GetInvitations(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		ClanValueResult<List<ClanInvitation>> clanValueResult = await Backend.ListInvitations(msg.player.userID);
		if (clanValueResult.IsSuccess)
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveClanInvitations", msg.player), clanValueResult.Value.ToProto());
		}
		ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, clanValueResult.Result, null, hasClanInfo: false));
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_GetLeaderboard(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		if (_leaderboardCache == null || (float)_sinceLastLeaderboardUpdate > 30f)
		{
			ClanValueResult<List<ClanLeaderboardEntry>> clanValueResult = await Backend.GetLeaderboard();
			if (clanValueResult.IsSuccess)
			{
				_leaderboardCache = clanValueResult.Value;
				_sinceLastLeaderboardUpdate = 0f;
			}
			else
			{
				_leaderboardCache = null;
			}
		}
		if (_leaderboardCache != null)
		{
			ClientRPCPlayer(null, msg.player, "Client_ReceiveClanLeaderboard", _leaderboardCache.ToProto());
		}
		ClanResult result = ((_leaderboardCache != null) ? ClanResult.Success : ClanResult.Fail);
		ClientRPCPlayer(null, msg.player, "Client_ReceiveActionResult", BuildActionResult(requestId, result, null, hasClanInfo: false));
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_SetLogo(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		byte[] newLogo = msg.read.BytesWithSize();
		if (!msg.player.CanModifyClan())
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.CantModifyClanHere, null, hasClanInfo: false));
			return;
		}
		if (!ImageProcessing.IsValidPNG(newLogo, 512, 512))
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.InvalidLogo, null, hasClanInfo: false));
			return;
		}
		ClanValueResult<IClan> result = await Backend.Get(msg.player.clanId);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			ClanResult result2 = await clan.SetLogo(newLogo, msg.player.userID);
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, result2, clan));
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_SetColor(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		Color32 newColor = msg.read.Color32();
		if (!msg.player.CanModifyClan())
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.CantModifyClanHere, null, hasClanInfo: false));
			return;
		}
		if (newColor.a != byte.MaxValue)
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.InvalidColor, null, hasClanInfo: false));
			return;
		}
		ClanValueResult<IClan> result = await Backend.Get(msg.player.clanId);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			ClanResult result2 = await clan.SetColor(newColor, msg.player.userID);
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, result2, clan));
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_SetMotd(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		string motd = msg.read.StringMultiLine();
		if (!msg.player.CanModifyClan())
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.CantModifyClanHere, null, hasClanInfo: false));
			return;
		}
		if (!ClanValidator.ValidateMotd(motd, out var validatedMotd))
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.InvalidText, null, hasClanInfo: false));
			return;
		}
		ClanValueResult<IClan> result = await Backend.Get(msg.player.clanId);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			long previousTimestamp = clan.MotdTimestamp;
			ClanResult clanResult = await clan.SetMotd(validatedMotd, msg.player.userID);
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, clanResult, clan));
			if (clanResult == ClanResult.Success)
			{
				ClanPushNotifications.SendClanAnnouncement(clan, previousTimestamp, msg.player.userID);
			}
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_Invite(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		ulong steamId = msg.read.UInt64();
		if (!msg.player.CanModifyClan())
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.CantModifyClanHere, null, hasClanInfo: false));
			return;
		}
		ClanValueResult<IClan> result = await Backend.Get(msg.player.clanId);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			ClanResult result2 = await clan.Invite(steamId, msg.player.userID);
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, result2, clan));
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_CancelInvite(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		ulong steamId = msg.read.UInt64();
		if (!msg.player.CanModifyClan())
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.CantModifyClanHere, null, hasClanInfo: false));
			return;
		}
		ClanValueResult<IClan> result = await Backend.Get(msg.player.clanId);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			ClanResult result2 = await clan.CancelInvite(steamId, msg.player.userID);
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, result2, clan));
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_AcceptInvitation(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		long clanId = msg.read.Int64();
		if (!msg.player.CanModifyClan())
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.CantModifyClanHere, null, hasClanInfo: false));
			return;
		}
		ClanValueResult<IClan> result = await Backend.Get(clanId);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			ClanResult result2 = await clan.AcceptInvite(msg.player.userID);
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, result2, null));
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_CancelInvitation(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		long clanId = msg.read.Int64();
		if (!msg.player.CanModifyClan())
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.CantModifyClanHere, null, hasClanInfo: false));
			return;
		}
		ClanValueResult<IClan> result = await Backend.Get(clanId);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			ClanResult result2 = await clan.CancelInvite(msg.player.userID, msg.player.userID);
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, result2, null));
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_Kick(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		ulong steamId = msg.read.UInt64();
		if ((ulong)msg.player.userID != steamId && !msg.player.CanModifyClan())
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.CantModifyClanHere, null, hasClanInfo: false));
			return;
		}
		ClanValueResult<IClan> result = await Backend.Get(msg.player.clanId);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			ClanResult result2 = await clan.Kick(steamId, msg.player.userID);
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, result2, clan));
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_SetPlayerRole(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		ulong steamId = msg.read.UInt64();
		int newRoleId = msg.read.Int32();
		if (!msg.player.CanModifyClan())
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.CantModifyClanHere, null, hasClanInfo: false));
			return;
		}
		ClanValueResult<IClan> result = await Backend.Get(msg.player.clanId);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			ClanResult result2 = await clan.SetPlayerRole(steamId, newRoleId, msg.player.userID);
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, result2, clan));
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_SetPlayerNotes(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		ulong steamId = msg.read.UInt64();
		string text = msg.read.StringMultiLine(1024);
		if (!msg.player.CanModifyClan())
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.CantModifyClanHere, null, hasClanInfo: false));
			return;
		}
		if (!ClanValidator.ValidatePlayerNote(text, out var validatedNotes))
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.InvalidText, null, hasClanInfo: false));
			return;
		}
		ClanValueResult<IClan> result = await Backend.Get(msg.player.clanId);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			ClanResult result2 = await clan.SetPlayerNotes(steamId, validatedNotes, msg.player.userID);
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, result2, clan));
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_CreateRole(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		string text = msg.read.String(128);
		if (!msg.player.CanModifyClan())
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.CantModifyClanHere, null, hasClanInfo: false));
			return;
		}
		if (!ClanValidator.ValidateRoleName(text, out var validated))
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.InvalidText, null, hasClanInfo: false));
			return;
		}
		ClanRole role = new ClanRole
		{
			Name = validated
		};
		ClanValueResult<IClan> result = await Backend.Get(msg.player.clanId);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			ClanResult result2 = await clan.CreateRole(role, msg.player.userID);
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, result2, clan));
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_UpdateRole(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		if (!msg.player.CanModifyClan())
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.CantModifyClanHere, null, hasClanInfo: false));
			return;
		}
		using ClanInfo.Role role = ClanInfo.Role.Deserialize(msg.read);
		if (!ClanValidator.ValidateRoleName(role.name, out var validated))
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.InvalidText, null, hasClanInfo: false));
			return;
		}
		role.name = validated;
		ClanValueResult<IClan> result = await Backend.Get(msg.player.clanId);
		if (!CheckClanResult(requestId, msg.player, result, out var clan))
		{
			return;
		}
		ClanResult result2 = await clan.UpdateRole(role.FromProto(), msg.player.userID);
		ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, result2, clan));
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_DeleteRole(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		int roleId = msg.read.Int32();
		if (!msg.player.CanModifyClan())
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.CantModifyClanHere, null, hasClanInfo: false));
			return;
		}
		ClanValueResult<IClan> result = await Backend.Get(msg.player.clanId);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			ClanResult result2 = await clan.DeleteRole(roleId, msg.player.userID);
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, result2, clan));
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_SwapRoles(RPCMessage msg)
	{
		int requestId = msg.read.Int32();
		int roleIdA = msg.read.Int32();
		int roleIdB = msg.read.Int32();
		if (!msg.player.CanModifyClan())
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, ClanResult.CantModifyClanHere, null, hasClanInfo: false));
			return;
		}
		ClanValueResult<IClan> result = await Backend.Get(msg.player.clanId);
		if (CheckClanResult(requestId, msg.player, result, out var clan))
		{
			ClanResult result2 = await clan.SwapRoleRanks(roleIdA, roleIdB, msg.player.userID);
			ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", msg.player), BuildActionResult(requestId, result2, clan));
		}
	}

	private bool CheckClanResult(int requestId, BasePlayer player, ClanValueResult<IClan> result, out IClan clan)
	{
		if (result.IsSuccess)
		{
			clan = result.Value;
			return true;
		}
		ClientRPC(RpcTarget.Player("Client_ReceiveActionResult", player), BuildActionResult(requestId, result.Result, null));
		clan = null;
		return false;
	}

	private static ClanActionResult BuildActionResult(int requestId, ClanResult result, IClan clan, bool hasClanInfo = true)
	{
		ClanActionResult clanActionResult = Facepunch.Pool.Get<ClanActionResult>();
		clanActionResult.requestId = requestId;
		clanActionResult.result = (int)result;
		clanActionResult.hasClanInfo = hasClanInfo;
		clanActionResult.clanInfo = clan.ToProto();
		return clanActionResult;
	}

	public async Task Initialize()
	{
		if (string.IsNullOrWhiteSpace(_backendType))
		{
			throw new InvalidOperationException("Clan backend type has not been assigned!");
		}
		IClanBackend backend = CreateBackendInstance(_backendType);
		if (backend == null)
		{
			throw new InvalidOperationException("Clan backend failed to create (returned null)");
		}
		try
		{
			_changeTracker = new ClanChangeTracker(this);
			await backend.Initialize(_changeTracker);
			Backend = backend;
			InvokeRandomized(delegate
			{
				_changeTracker.HandleEvents();
			}, 1f, 0.25f, 0.1f);
		}
		catch (Exception innerException)
		{
			throw new InvalidOperationException("Clan backend failed to initialize (threw exception)", innerException);
		}
	}

	public void Shutdown()
	{
		if (Backend == null)
		{
			return;
		}
		try
		{
			Backend.Dispose();
			Backend = null;
		}
		catch (Exception innerException)
		{
			throw new InvalidOperationException("Clan backend failed to shutdown (threw exception)", innerException);
		}
	}

	public override void Spawn()
	{
		base.Spawn();
		if (!base.isServer)
		{
			return;
		}
		if (Rust.Application.isLoadingSave)
		{
			if (!Clan.enabled)
			{
				Debug.LogWarning("Clan manager was loaded from a save, but the server has the clan system disabled - destroying clan manager!");
				Invoke(delegate
				{
					Kill();
				}, 0.1f);
			}
		}
		else if (!Rust.Application.isLoadingSave)
		{
			_backendType = ChooseBackendType();
			if (string.IsNullOrWhiteSpace(_backendType))
			{
				Debug.LogError("Clan manager did not choose a backend type!");
			}
			else
			{
				Debug.Log("Clan manager will use backend type: " + _backendType);
			}
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		if (info.forDisk)
		{
			info.msg.clanManager = Facepunch.Pool.Get<ProtoBuf.ClanManager>();
			info.msg.clanManager.backendType = _backendType;
		}
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.clanManager != null)
		{
			_backendType = info.msg.clanManager.backendType;
		}
	}

	private static string ChooseBackendType()
	{
		if (NexusServer.Started)
		{
			return "nexus";
		}
		return "local";
	}

	private static IClanBackend CreateBackendInstance(string type)
	{
		if (!(type == "local"))
		{
			if (type == "nexus")
			{
				return new NexusClanBackend();
			}
			throw new NotSupportedException("Clan backend '" + type + "' is not supported");
		}
		return new LocalClanBackend(ConVar.Server.rootFolder, Clan.maxMemberCount);
	}

	public override void InitShared()
	{
		base.InitShared();
		if (base.isServer)
		{
			if (ServerInstance != null)
			{
				Debug.LogError("Major fuckup! Server ClanManager spawned twice, contact Developers!");
				UnityEngine.Object.Destroy(base.gameObject);
			}
			else
			{
				ServerInstance = this;
			}
		}
	}

	public void OnDestroy()
	{
		if (base.isServer)
		{
			if (ServerInstance == this)
			{
				ServerInstance = null;
			}
			Shutdown();
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	public async void Server_GetClanMetadata(RPCMessage msg)
	{
		long clanId = msg.read.Int64();
		ClanValueResult<IClan> clanValueResult = await Backend.Get(clanId);
		if (clanValueResult.IsSuccess)
		{
			IClan value = clanValueResult.Value;
			ClientRPC(RpcTarget.Player("Client_GetClanMetadataResponse", msg.player), clanId, value.Name ?? "", value.Members?.Count ?? 0, value.Color);
		}
		else
		{
			ClientRPC(RpcTarget.Player("Client_GetClanMetadataResponse", msg.player), clanId, "[unknown]", 0, (Color32)Color.white);
		}
	}

	public void AddScore(IClan clan, ClanScoreEvent entry)
	{
		Assert.IsNotNull(clan, "clan != null");
		ValueTask<ClanResult> task2 = clan.AddScoreEvent(entry);
		if (task2.IsCompletedSuccessfully)
		{
			CheckResult(task2.Result);
		}
		else
		{
			AwaitResult(task2);
		}
		async void AwaitResult(ValueTask<ClanResult> task)
		{
			try
			{
				CheckResult(await task);
			}
			catch (Exception exception)
			{
				Debug.LogError($"Exception while adding score event to clan {clan.ClanId}:");
				Debug.LogException(exception);
			}
		}
		void CheckResult(ClanResult result)
		{
			if (result != ClanResult.Success)
			{
				Debug.LogWarning($"Failed to add score event to clan {clan.ClanId}: {result}");
			}
		}
	}

	public void SendClanChanged(IClan clan)
	{
		List<Connection> obj = Facepunch.Pool.Get<List<Connection>>();
		foreach (ClanMember member in clan.Members)
		{
			BasePlayer basePlayer = BasePlayer.FindByID(member.SteamId);
			if (basePlayer != null && basePlayer.IsConnected)
			{
				obj.Add(basePlayer.net.connection);
			}
		}
		ClientRPC(RpcTarget.Players("Client_CurrentClanChanged", obj));
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	public void SendClanInvitation(ulong steamId, long clanId)
	{
		BasePlayer basePlayer = BasePlayer.FindByID(steamId);
		if (!(basePlayer == null) && basePlayer.IsConnected)
		{
			ClientRPC(RpcTarget.Player("Client_ReceiveClanInvitation", basePlayer), clanId);
		}
	}

	public bool TryGetClanMemberConnections(long clanId, out List<Connection> connections)
	{
		if (_clanMemberConnections.TryGetValue(clanId, out connections))
		{
			return true;
		}
		if (!Backend.TryGet(clanId, out var clan))
		{
			return false;
		}
		connections = Facepunch.Pool.Get<List<Connection>>();
		foreach (ClanMember member in clan.Members)
		{
			BasePlayer basePlayer = BasePlayer.FindByID(member.SteamId);
			if (basePlayer == null)
			{
				basePlayer = BasePlayer.FindSleeping(member.SteamId);
			}
			if (!(basePlayer == null) && basePlayer.IsConnected)
			{
				connections.Add(basePlayer.Connection);
			}
		}
		_clanMemberConnections.Add(clanId, connections);
		return true;
	}

	public void ClanMemberConnectionsChanged(long clanId)
	{
		if (_clanMemberConnections.TryGetValue(clanId, out var value))
		{
			_clanMemberConnections.Remove(clanId);
			Facepunch.Pool.FreeUnmanaged(ref value);
		}
	}

	public async void LoadClanInfoForSleepers()
	{
		Dictionary<ulong, BasePlayer> sleepers = Facepunch.Pool.Get<Dictionary<ulong, BasePlayer>>();
		sleepers.Clear();
		foreach (BasePlayer sleepingPlayer in BasePlayer.sleepingPlayerList)
		{
			if (sleepingPlayer.IsValid() && !sleepingPlayer.IsNpc && !sleepingPlayer.IsBot)
			{
				sleepers.Add(sleepingPlayer.userID, sleepingPlayer);
			}
		}
		HashSet<ulong> found = Facepunch.Pool.Get<HashSet<ulong>>();
		found.Clear();
		foreach (BasePlayer player in sleepers.Values)
		{
			if (!player.IsValid() || player.IsConnected || found.Contains(player.userID))
			{
				continue;
			}
			try
			{
				ClanValueResult<IClan> clanValueResult = await Backend.GetByMember(player.userID);
				if (clanValueResult.IsSuccess)
				{
					IClan value = clanValueResult.Value;
					player.serverClan = value;
					player.clanId = value.ClanId;
					SendNetworkUpdate();
					found.Add(player.userID);
					foreach (ClanMember member in value.Members)
					{
						if (sleepers.TryGetValue(member.SteamId, out var value2) && found.Add(member.SteamId))
						{
							value2.serverClan = value;
							value2.clanId = value.ClanId;
							value2.SendNetworkUpdate();
						}
					}
				}
				else if (clanValueResult.Result == ClanResult.NoClan)
				{
					player.serverClan = null;
					player.clanId = 0L;
					SendNetworkUpdate();
					found.Add(player.userID);
				}
				else
				{
					Debug.LogError($"Failed to find clan for {player.userID.Get()}: {clanValueResult.Result}");
					Invoke(delegate
					{
						player.LoadClanInfo();
					}, 45 + UnityEngine.Random.Range(0, 30));
				}
			}
			catch (Exception exception)
			{
				DebugEx.Log($"Exception was thrown while loading clan info for {player.userID.Get()}:");
				Debug.LogException(exception);
			}
		}
		found.Clear();
		Facepunch.Pool.FreeUnmanaged(ref found);
		sleepers.Clear();
		Facepunch.Pool.FreeUnmanaged(ref sleepers);
	}
}
