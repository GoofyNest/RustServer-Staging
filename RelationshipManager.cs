#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using CompanionServer;
using ConVar;
using Facepunch;
using Facepunch.Rust;
using Network;
using ProtoBuf;
using UnityEngine;
using UnityEngine.Assertions;

public class RelationshipManager : BaseEntity
{
	public enum RelationshipType
	{
		NONE,
		Acquaintance,
		Friend,
		Enemy
	}

	public class PlayerRelationshipInfo : Facepunch.Pool.IPooled, IServerFileReceiver, IPlayerInfo
	{
		public string displayName;

		public ulong player;

		public RelationshipType type;

		public int weight;

		public uint mugshotCrc;

		public string notes;

		public float lastSeenTime;

		[NonSerialized]
		public float lastMugshotTime;

		public ulong UserId => player;

		public string UserName => displayName;

		public bool IsOnline => false;

		public bool IsMe => false;

		public bool IsFriend => false;

		public bool IsPlayingThisGame => true;

		public string ServerEndpoint => string.Empty;

		public void EnterPool()
		{
			Reset();
		}

		public void LeavePool()
		{
			Reset();
		}

		private void Reset()
		{
			displayName = null;
			player = 0uL;
			type = RelationshipType.NONE;
			weight = 0;
			mugshotCrc = 0u;
			notes = "";
			lastMugshotTime = 0f;
		}

		public ProtoBuf.RelationshipManager.PlayerRelationshipInfo ToProto()
		{
			ProtoBuf.RelationshipManager.PlayerRelationshipInfo playerRelationshipInfo = Facepunch.Pool.Get<ProtoBuf.RelationshipManager.PlayerRelationshipInfo>();
			playerRelationshipInfo.playerID = player;
			playerRelationshipInfo.type = (int)type;
			playerRelationshipInfo.weight = weight;
			playerRelationshipInfo.mugshotCrc = mugshotCrc;
			playerRelationshipInfo.displayName = displayName;
			playerRelationshipInfo.notes = notes;
			playerRelationshipInfo.timeSinceSeen = UnityEngine.Time.realtimeSinceStartup - lastSeenTime;
			return playerRelationshipInfo;
		}

		public static PlayerRelationshipInfo FromProto(ProtoBuf.RelationshipManager.PlayerRelationshipInfo proto)
		{
			return new PlayerRelationshipInfo
			{
				type = (RelationshipType)proto.type,
				weight = proto.weight,
				displayName = proto.displayName,
				mugshotCrc = proto.mugshotCrc,
				notes = proto.notes,
				player = proto.playerID,
				lastSeenTime = UnityEngine.Time.realtimeSinceStartup - proto.timeSinceSeen
			};
		}
	}

	public class PlayerRelationships : Facepunch.Pool.IPooled
	{
		public bool dirty;

		public ulong ownerPlayer;

		public Dictionary<ulong, PlayerRelationshipInfo> relations;

		public bool Forget(ulong player)
		{
			if (relations.TryGetValue(player, out var value))
			{
				relations.Remove(player);
				if (value.mugshotCrc != 0)
				{
					ServerInstance.DeleteMugshot(ownerPlayer, player, value.mugshotCrc);
				}
				return true;
			}
			return false;
		}

		public PlayerRelationshipInfo GetRelations(ulong player)
		{
			BasePlayer basePlayer = FindByID(player);
			if (relations.TryGetValue(player, out var value))
			{
				if (basePlayer != null)
				{
					value.displayName = basePlayer.displayName;
				}
				return value;
			}
			PlayerRelationshipInfo playerRelationshipInfo = Facepunch.Pool.Get<PlayerRelationshipInfo>();
			if (basePlayer != null)
			{
				playerRelationshipInfo.displayName = basePlayer.displayName;
			}
			playerRelationshipInfo.player = player;
			relations.Add(player, playerRelationshipInfo);
			return playerRelationshipInfo;
		}

		public PlayerRelationships()
		{
			LeavePool();
		}

		public void EnterPool()
		{
			ownerPlayer = 0uL;
			if (relations != null)
			{
				relations.Clear();
				Facepunch.Pool.Free(ref relations, freeElements: false);
			}
		}

		public void LeavePool()
		{
			ownerPlayer = 0uL;
			relations = Facepunch.Pool.Get<Dictionary<ulong, PlayerRelationshipInfo>>();
			relations.Clear();
		}
	}

	public class PlayerTeam : Facepunch.Pool.IPooled
	{
		public ulong teamID;

		public string teamName;

		public ulong teamLeader;

		public List<ulong> members = new List<ulong>();

		public List<ulong> invites = new List<ulong>();

		public float teamStartTime;

		private List<Network.Connection> onlineMemberConnections = new List<Network.Connection>();

		public float teamLifetime => UnityEngine.Time.realtimeSinceStartup - teamStartTime;

		public BasePlayer GetLeader()
		{
			return FindByID(teamLeader);
		}

		public void SendInvite(BasePlayer player)
		{
			if (invites.Count > 8)
			{
				invites.RemoveRange(0, 1);
			}
			BasePlayer basePlayer = FindByID(teamLeader);
			if (!(basePlayer == null))
			{
				invites.Add(player.userID);
				player.ClientRPC(RpcTarget.Player("CLIENT_PendingInvite", player), basePlayer.displayName, teamLeader, teamID);
			}
		}

		public void AcceptInvite(BasePlayer player)
		{
			if (invites.Contains(player.userID))
			{
				invites.Remove(player.userID);
				AddPlayer(player);
				player.ClearPendingInvite();
			}
		}

		public void RejectInvite(BasePlayer player)
		{
			player.ClearPendingInvite();
			invites.Remove(player.userID);
		}

		public bool AddPlayer(BasePlayer player, bool skipDirtyUpdate = false)
		{
			ulong num = player.userID.Get();
			if (members.Contains(num))
			{
				return false;
			}
			if (player.currentTeam != 0L)
			{
				return false;
			}
			if (members.Count >= maxTeamSize)
			{
				return false;
			}
			player.currentTeam = teamID;
			bool num2 = members.Count == 0;
			members.Add(num);
			ServerInstance.playerToTeam.Add(num, this);
			if (!skipDirtyUpdate)
			{
				MarkDirty();
			}
			player.SendNetworkUpdate();
			if (!num2)
			{
				Analytics.Azure.OnTeamChanged("added", teamID, teamLeader, num, members);
			}
			return true;
		}

		public bool RemovePlayer(ulong playerID)
		{
			if (members.Contains(playerID))
			{
				members.Remove(playerID);
				ServerInstance.playerToTeam.Remove(playerID);
				BasePlayer basePlayer = FindByID(playerID);
				if (basePlayer != null)
				{
					basePlayer.ClearTeam();
					basePlayer.BroadcastAppTeamRemoval();
					basePlayer.SendNetworkUpdate();
				}
				if (teamLeader == playerID)
				{
					if (members.Count > 0)
					{
						SetTeamLeader(members[0]);
						Analytics.Azure.OnTeamChanged("removed", teamID, teamLeader, playerID, members);
					}
					else
					{
						Analytics.Azure.OnTeamChanged("disband", teamID, teamLeader, playerID, members);
						Disband();
					}
				}
				MarkDirty();
				return true;
			}
			return false;
		}

		public void SetTeamLeader(ulong newTeamLeader)
		{
			Analytics.Azure.OnTeamChanged("promoted", teamID, teamLeader, newTeamLeader, members);
			teamLeader = newTeamLeader;
			MarkDirty();
		}

		public void Disband()
		{
			ServerInstance.DisbandTeam(this);
			CompanionServer.Server.TeamChat.Remove(teamID);
		}

		public void MarkDirty()
		{
			foreach (ulong member in members)
			{
				BasePlayer basePlayer = FindByID(member);
				if (basePlayer != null)
				{
					basePlayer.UpdateTeam(teamID);
				}
			}
			this.BroadcastAppTeamUpdate();
		}

		public List<Network.Connection> GetOnlineMemberConnections()
		{
			if (members.Count == 0)
			{
				return null;
			}
			onlineMemberConnections.Clear();
			foreach (ulong member in members)
			{
				BasePlayer basePlayer = FindByID(member);
				if (!(basePlayer == null) && basePlayer.Connection != null)
				{
					onlineMemberConnections.Add(basePlayer.Connection);
				}
			}
			return onlineMemberConnections;
		}

		void Facepunch.Pool.IPooled.EnterPool()
		{
			teamID = 0uL;
			teamName = string.Empty;
			teamLeader = 0uL;
			teamStartTime = 0f;
			members.Clear();
			invites.Clear();
			onlineMemberConnections.Clear();
		}

		void Facepunch.Pool.IPooled.LeavePool()
		{
		}
	}

	[ReplicatedVar(Default = "true")]
	public static bool contacts = true;

	public const FileStorage.Type MugshotFileFormat = FileStorage.Type.jpg;

	private const int MugshotResolution = 256;

	private const int MugshotMaxFileSize = 65536;

	private const float MugshotMaxDistance = 50f;

	public Dictionary<ulong, PlayerRelationships> relationships = new Dictionary<ulong, PlayerRelationships>();

	private int lastReputationUpdateIndex;

	private const int seenReputationSeconds = 60;

	private int startingReputation;

	[ServerVar]
	public static int forgetafterminutes = 960;

	[ServerVar]
	public static int maxplayerrelationships = 128;

	[ServerVar]
	public static float seendistance = 10f;

	[ServerVar]
	public static float mugshotUpdateInterval = 300f;

	private static List<BasePlayer> _dirtyRelationshipPlayers = new List<BasePlayer>();

	public static int maxTeamSize_Internal = 8;

	public Dictionary<ulong, BasePlayer> cachedPlayers = new Dictionary<ulong, BasePlayer>();

	public Dictionary<ulong, PlayerTeam> playerToTeam = new Dictionary<ulong, PlayerTeam>();

	public Dictionary<ulong, PlayerTeam> teams = new Dictionary<ulong, PlayerTeam>();

	private ulong lastTeamIndex = 1uL;

	[ServerVar]
	public static int maxTeamSize
	{
		get
		{
			return maxTeamSize_Internal;
		}
		set
		{
			maxTeamSize_Internal = value;
			if ((bool)ServerInstance)
			{
				ServerInstance.SendNetworkUpdate();
			}
		}
	}

	public static RelationshipManager ServerInstance { get; private set; }

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("RelationshipManager.OnRpcMessage"))
		{
			if (rpc == 532372582 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - BagQuotaRequest_SERVER ");
				}
				using (TimeWarning.New("BagQuotaRequest_SERVER"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(532372582u, "BagQuotaRequest_SERVER", this, player, 2uL))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							BagQuotaRequest_SERVER();
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in BagQuotaRequest_SERVER");
					}
				}
				return true;
			}
			if (rpc == 1684577101 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SERVER_ChangeRelationship ");
				}
				using (TimeWarning.New("SERVER_ChangeRelationship"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(1684577101u, "SERVER_ChangeRelationship", this, player, 2uL))
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
							SERVER_ChangeRelationship(msg2);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in SERVER_ChangeRelationship");
					}
				}
				return true;
			}
			if (rpc == 1239936737 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SERVER_ReceiveMugshot ");
				}
				using (TimeWarning.New("SERVER_ReceiveMugshot"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(1239936737u, "SERVER_ReceiveMugshot", this, player, 10uL))
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
							SERVER_ReceiveMugshot(msg3);
						}
					}
					catch (Exception exception3)
					{
						Debug.LogException(exception3);
						player.Kick("RPC Error in SERVER_ReceiveMugshot");
					}
				}
				return true;
			}
			if (rpc == 2178173141u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SERVER_SendFreshContacts ");
				}
				using (TimeWarning.New("SERVER_SendFreshContacts"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(2178173141u, "SERVER_SendFreshContacts", this, player, 1uL))
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
							SERVER_SendFreshContacts(msg4);
						}
					}
					catch (Exception exception4)
					{
						Debug.LogException(exception4);
						player.Kick("RPC Error in SERVER_SendFreshContacts");
					}
				}
				return true;
			}
			if (rpc == 290196604 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SERVER_UpdatePlayerNote ");
				}
				using (TimeWarning.New("SERVER_UpdatePlayerNote"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(290196604u, "SERVER_UpdatePlayerNote", this, player, 10uL))
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
							SERVER_UpdatePlayerNote(msg5);
						}
					}
					catch (Exception exception5)
					{
						Debug.LogException(exception5);
						player.Kick("RPC Error in SERVER_UpdatePlayerNote");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(2uL)]
	public void BagQuotaRequest_SERVER()
	{
	}

	public override void ServerInit()
	{
		base.ServerInit();
		if (contacts)
		{
			InvokeRepeating(UpdateContactsTick, 0f, 1f);
			InvokeRepeating(UpdateReputations, 0f, 0.05f);
			InvokeRepeating(SendRelationships, 0f, 5f);
		}
	}

	public void UpdateReputations()
	{
		if (contacts && BasePlayer.activePlayerList.Count != 0)
		{
			if (lastReputationUpdateIndex >= BasePlayer.activePlayerList.Count)
			{
				lastReputationUpdateIndex = 0;
			}
			BasePlayer basePlayer = BasePlayer.activePlayerList[lastReputationUpdateIndex];
			if (basePlayer.reputation != (basePlayer.reputation = GetReputationFor(basePlayer.userID)))
			{
				basePlayer.SendNetworkUpdate();
			}
			lastReputationUpdateIndex++;
		}
	}

	public void UpdateContactsTick()
	{
		if (!contacts)
		{
			return;
		}
		foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
		{
			UpdateAcquaintancesFor(activePlayer, 1f);
		}
	}

	public int GetReputationFor(ulong playerID)
	{
		int num = startingReputation;
		foreach (PlayerRelationships value2 in relationships.Values)
		{
			if (!value2.relations.TryGetValue(playerID, out var value))
			{
				continue;
			}
			if (value.type == RelationshipType.Friend)
			{
				num++;
			}
			else if (value.type == RelationshipType.Acquaintance)
			{
				if (value.weight > 60)
				{
					num++;
				}
			}
			else if (value.type == RelationshipType.Enemy)
			{
				num--;
			}
		}
		return num;
	}

	[ServerVar]
	public static void wipecontacts(ConsoleSystem.Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (!(basePlayer == null) && !(ServerInstance == null))
		{
			ulong num = basePlayer.userID.Get();
			if (ServerInstance.relationships.ContainsKey(num))
			{
				Debug.Log("Wiped contacts for :" + num);
				ServerInstance.relationships.Remove(num);
				ServerInstance.MarkRelationshipsDirtyFor(num);
			}
			else
			{
				Debug.Log("No contacts for :" + num);
			}
		}
	}

	[ServerVar]
	public static void wipe_all_contacts(ConsoleSystem.Arg arg)
	{
		if (arg.Player() == null || ServerInstance == null)
		{
			return;
		}
		if (!arg.HasArgs() || arg.Args[0] != "confirm")
		{
			Debug.Log("Please append the word 'confirm' at the end of the console command to execute");
			return;
		}
		ServerInstance.relationships.Clear();
		foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
		{
			ServerInstance.MarkRelationshipsDirtyFor(activePlayer);
		}
		Debug.Log("Wiped all contacts.");
	}

	public float GetAcquaintanceMaxDist()
	{
		return seendistance;
	}

	public void UpdateAcquaintancesFor(BasePlayer player, float deltaSeconds)
	{
		PlayerRelationships playerRelationships = GetRelationships(player.userID);
		List<BasePlayer> obj = Facepunch.Pool.Get<List<BasePlayer>>();
		BaseNetworkable.GetCloseConnections(player.transform.position, GetAcquaintanceMaxDist(), obj);
		foreach (BasePlayer item in obj)
		{
			if (item == player || item.isClient || !item.IsAlive() || item.IsSleeping())
			{
				continue;
			}
			PlayerRelationshipInfo relations = playerRelationships.GetRelations(item.userID);
			if (!(Vector3.Distance(player.transform.position, item.transform.position) <= GetAcquaintanceMaxDist()))
			{
				continue;
			}
			relations.lastSeenTime = UnityEngine.Time.realtimeSinceStartup;
			if ((relations.type == RelationshipType.NONE || relations.type == RelationshipType.Acquaintance) && player.IsPlayerVisibleToUs(item, Vector3.zero, 1218519041))
			{
				int num = Mathf.CeilToInt(deltaSeconds);
				if (player.InSafeZone() || item.InSafeZone())
				{
					num = 0;
				}
				if (relations.type != RelationshipType.Acquaintance || (relations.weight < 60 && num > 0))
				{
					SetRelationship(player, item, RelationshipType.Acquaintance, num);
				}
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	public void SetSeen(BasePlayer player, BasePlayer otherPlayer)
	{
		ulong player2 = player.userID.Get();
		ulong player3 = otherPlayer.userID.Get();
		PlayerRelationshipInfo relations = GetRelationships(player2).GetRelations(player3);
		if (relations.type != 0)
		{
			relations.lastSeenTime = UnityEngine.Time.realtimeSinceStartup;
		}
	}

	public bool CleanupOldContacts(PlayerRelationships ownerRelationships, ulong playerID, RelationshipType relationshipType = RelationshipType.Acquaintance)
	{
		int numberRelationships = GetNumberRelationships(playerID);
		if (numberRelationships < maxplayerrelationships)
		{
			return true;
		}
		List<ulong> obj = Facepunch.Pool.Get<List<ulong>>();
		foreach (KeyValuePair<ulong, PlayerRelationshipInfo> relation in ownerRelationships.relations)
		{
			if (relation.Value.type == relationshipType && UnityEngine.Time.realtimeSinceStartup - relation.Value.lastSeenTime > (float)forgetafterminutes * 60f)
			{
				obj.Add(relation.Key);
			}
		}
		int count = obj.Count;
		foreach (ulong item in obj)
		{
			ownerRelationships.Forget(item);
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return numberRelationships - count < maxplayerrelationships;
	}

	public void ForceRelationshipByID(BasePlayer player, ulong otherPlayerID, RelationshipType newType, int weight, bool sendImmediate = false)
	{
		if (!contacts || player == null || (ulong)player.userID == otherPlayerID || player.IsNpc)
		{
			return;
		}
		ulong player2 = player.userID.Get();
		if (HasRelations(player2, otherPlayerID))
		{
			PlayerRelationshipInfo relations = GetRelationships(player2).GetRelations(otherPlayerID);
			if (relations.type != newType)
			{
				relations.weight = 0;
			}
			relations.type = newType;
			relations.weight += weight;
			if (sendImmediate)
			{
				SendRelationshipsFor(player);
			}
			else
			{
				MarkRelationshipsDirtyFor(player);
			}
		}
	}

	public void SetRelationship(BasePlayer player, BasePlayer otherPlayer, RelationshipType type, int weight = 1, bool sendImmediate = false)
	{
		if (!contacts)
		{
			return;
		}
		ulong num = player.userID.Get();
		ulong num2 = otherPlayer.userID.Get();
		if (player == null || player == otherPlayer || player.IsNpc || (otherPlayer != null && otherPlayer.IsNpc))
		{
			return;
		}
		PlayerRelationships playerRelationships = GetRelationships(num);
		if (!CleanupOldContacts(playerRelationships, num))
		{
			CleanupOldContacts(playerRelationships, num, RelationshipType.Enemy);
		}
		PlayerRelationshipInfo relations = playerRelationships.GetRelations(num2);
		bool flag = false;
		if (relations.type != type)
		{
			flag = true;
			relations.weight = 0;
		}
		relations.type = type;
		relations.weight += weight;
		float num3 = UnityEngine.Time.realtimeSinceStartup - relations.lastMugshotTime;
		if (flag || relations.mugshotCrc == 0 || num3 >= mugshotUpdateInterval)
		{
			bool flag2 = otherPlayer.IsAlive();
			bool num4 = player.SecondsSinceAttacked > 10f && !player.IsAiming;
			float num5 = 100f;
			if (num4)
			{
				Vector3 normalized = (otherPlayer.eyes.position - player.eyes.position).normalized;
				bool flag3 = Vector3.Dot(player.eyes.HeadForward(), normalized) >= 0.6f;
				float num6 = Vector3Ex.Distance2D(player.transform.position, otherPlayer.transform.position);
				if (flag2 && num6 < num5 && flag3)
				{
					ClientRPC(RpcTarget.Player("CLIENT_DoMugshot", player), num2);
					relations.lastMugshotTime = UnityEngine.Time.realtimeSinceStartup;
				}
			}
		}
		if (sendImmediate)
		{
			SendRelationshipsFor(player);
		}
		else
		{
			MarkRelationshipsDirtyFor(player);
		}
	}

	public ProtoBuf.RelationshipManager.PlayerRelationships GetRelationshipSaveByID(ulong playerID)
	{
		ProtoBuf.RelationshipManager.PlayerRelationships playerRelationships = Facepunch.Pool.Get<ProtoBuf.RelationshipManager.PlayerRelationships>();
		PlayerRelationships playerRelationships2 = GetRelationships(playerID);
		if (playerRelationships2 != null)
		{
			playerRelationships.playerID = playerID;
			playerRelationships.relations = Facepunch.Pool.Get<List<ProtoBuf.RelationshipManager.PlayerRelationshipInfo>>();
			{
				foreach (KeyValuePair<ulong, PlayerRelationshipInfo> relation in playerRelationships2.relations)
				{
					playerRelationships.relations.Add(relation.Value.ToProto());
				}
				return playerRelationships;
			}
		}
		return null;
	}

	public void MarkRelationshipsDirtyFor(ulong playerID)
	{
		BasePlayer basePlayer = FindByID(playerID);
		if ((bool)basePlayer)
		{
			MarkRelationshipsDirtyFor(basePlayer);
		}
	}

	public static void ForceSendRelationships(BasePlayer player)
	{
		if ((bool)ServerInstance)
		{
			ServerInstance.MarkRelationshipsDirtyFor(player);
		}
	}

	public void MarkRelationshipsDirtyFor(BasePlayer player)
	{
		if (!(player == null) && !_dirtyRelationshipPlayers.Contains(player))
		{
			_dirtyRelationshipPlayers.Add(player);
		}
	}

	public void SendRelationshipsFor(BasePlayer player)
	{
		if (contacts)
		{
			ulong playerID = player.userID.Get();
			ProtoBuf.RelationshipManager.PlayerRelationships relationshipSaveByID = GetRelationshipSaveByID(playerID);
			ClientRPC(RpcTarget.Player("CLIENT_RecieveLocalRelationships", player), relationshipSaveByID);
		}
	}

	public void SendRelationships()
	{
		if (!contacts)
		{
			return;
		}
		foreach (BasePlayer dirtyRelationshipPlayer in _dirtyRelationshipPlayers)
		{
			if (!(dirtyRelationshipPlayer == null) && dirtyRelationshipPlayer.IsConnected && !dirtyRelationshipPlayer.IsSleeping())
			{
				SendRelationshipsFor(dirtyRelationshipPlayer);
			}
		}
		_dirtyRelationshipPlayers.Clear();
	}

	public int GetNumberRelationships(ulong player)
	{
		if (relationships.TryGetValue(player, out var value))
		{
			return value.relations.Count;
		}
		return 0;
	}

	public bool HasRelations(ulong player, ulong otherPlayer)
	{
		if (relationships.TryGetValue(player, out var value) && value.relations.ContainsKey(otherPlayer))
		{
			return true;
		}
		return false;
	}

	public PlayerRelationships GetRelationships(ulong player)
	{
		if (relationships.TryGetValue(player, out var value))
		{
			return value;
		}
		PlayerRelationships playerRelationships = Facepunch.Pool.Get<PlayerRelationships>();
		playerRelationships.ownerPlayer = player;
		relationships.Add(player, playerRelationships);
		return playerRelationships;
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(1uL)]
	public void SERVER_SendFreshContacts(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if ((bool)player)
		{
			SendRelationshipsFor(player);
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(2uL)]
	public void SERVER_ChangeRelationship(RPCMessage msg)
	{
		BasePlayer.EncryptedValue<ulong> userID = msg.player.userID;
		ulong num = msg.read.UInt64();
		int num2 = Mathf.Clamp(msg.read.Int32(), 0, 3);
		PlayerRelationships playerRelationships = GetRelationships(userID);
		playerRelationships.GetRelations(num);
		BasePlayer player = msg.player;
		RelationshipType relationshipType = (RelationshipType)num2;
		if (num2 == 0)
		{
			if (playerRelationships.Forget(num))
			{
				SendRelationshipsFor(player);
			}
			return;
		}
		BasePlayer basePlayer = FindByID(num);
		if (basePlayer == null)
		{
			ForceRelationshipByID(player, num, relationshipType, 0, sendImmediate: true);
		}
		else
		{
			SetRelationship(player, basePlayer, relationshipType, 1, sendImmediate: true);
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(10uL)]
	public void SERVER_UpdatePlayerNote(RPCMessage msg)
	{
		BasePlayer.EncryptedValue<ulong> userID = msg.player.userID;
		ulong player = msg.read.UInt64();
		string notes = msg.read.String();
		GetRelationships(userID).GetRelations(player).notes = notes;
		MarkRelationshipsDirtyFor(userID);
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(10uL)]
	public void SERVER_ReceiveMugshot(RPCMessage msg)
	{
		BasePlayer.EncryptedValue<ulong> userID = msg.player.userID;
		ulong num = msg.read.UInt64();
		uint num2 = msg.read.UInt32();
		byte[] array = msg.read.BytesWithSize(65536u);
		if (array != null && ImageProcessing.IsValidJPG(array, 256, 512) && relationships.TryGetValue(userID, out var value) && value.relations.TryGetValue(num, out var value2))
		{
			uint steamIdHash = GetSteamIdHash(userID, num);
			uint num3 = FileStorage.server.Store(array, FileStorage.Type.jpg, net.ID, steamIdHash);
			if (num3 != num2)
			{
				Debug.LogWarning("Client/Server FileStorage CRC differs");
			}
			if (num3 != value2.mugshotCrc)
			{
				FileStorage.server.RemoveExact(value2.mugshotCrc, FileStorage.Type.jpg, net.ID, steamIdHash);
			}
			value2.mugshotCrc = num3;
			MarkRelationshipsDirtyFor(userID);
		}
	}

	private void DeleteMugshot(ulong steamId, ulong targetSteamId, uint crc)
	{
		if (crc != 0)
		{
			uint steamIdHash = GetSteamIdHash(steamId, targetSteamId);
			FileStorage.server.RemoveExact(crc, FileStorage.Type.jpg, net.ID, steamIdHash);
		}
	}

	public static uint GetSteamIdHash(ulong requesterSteamId, ulong targetSteamId)
	{
		return (uint)(((requesterSteamId & 0xFFFF) << 16) | (targetSteamId & 0xFFFF));
	}

	public int GetMaxTeamSize()
	{
		BaseGameMode activeGameMode = BaseGameMode.GetActiveGameMode(serverside: true);
		if ((bool)activeGameMode)
		{
			return activeGameMode.GetMaxRelationshipTeamSize();
		}
		return maxTeamSize;
	}

	public void OnEnable()
	{
		if (base.isServer)
		{
			if (ServerInstance != null)
			{
				Debug.LogError("Major fuckup! RelationshipManager spawned twice, Contact Developers!");
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
			ServerInstance = null;
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.relationshipManager = Facepunch.Pool.Get<ProtoBuf.RelationshipManager>();
		info.msg.relationshipManager.maxTeamSize = maxTeamSize;
		if (!info.forDisk)
		{
			return;
		}
		info.msg.relationshipManager.lastTeamIndex = lastTeamIndex;
		info.msg.relationshipManager.teamList = Facepunch.Pool.Get<List<ProtoBuf.PlayerTeam>>();
		foreach (KeyValuePair<ulong, PlayerTeam> team in teams)
		{
			PlayerTeam value = team.Value;
			if (value == null)
			{
				continue;
			}
			ProtoBuf.PlayerTeam playerTeam = Facepunch.Pool.Get<ProtoBuf.PlayerTeam>();
			playerTeam.teamLeader = value.teamLeader;
			playerTeam.teamID = value.teamID;
			playerTeam.teamName = value.teamName;
			playerTeam.members = Facepunch.Pool.Get<List<ProtoBuf.PlayerTeam.TeamMember>>();
			foreach (ulong member in value.members)
			{
				ProtoBuf.PlayerTeam.TeamMember teamMember = Facepunch.Pool.Get<ProtoBuf.PlayerTeam.TeamMember>();
				BasePlayer basePlayer = FindByID(member);
				teamMember.displayName = ((basePlayer != null) ? basePlayer.displayName : (SingletonComponent<ServerMgr>.Instance.persistance.GetPlayerName(member) ?? "DEAD"));
				teamMember.userID = member;
				playerTeam.members.Add(teamMember);
			}
			info.msg.relationshipManager.teamList.Add(playerTeam);
		}
		info.msg.relationshipManager.relationships = Facepunch.Pool.Get<List<ProtoBuf.RelationshipManager.PlayerRelationships>>();
		foreach (ulong key in relationships.Keys)
		{
			_ = relationships[key];
			ProtoBuf.RelationshipManager.PlayerRelationships relationshipSaveByID = GetRelationshipSaveByID(key);
			info.msg.relationshipManager.relationships.Add(relationshipSaveByID);
		}
	}

	public void DisbandTeam(PlayerTeam teamToDisband)
	{
		teams.Remove(teamToDisband.teamID);
		Facepunch.Pool.Free(ref teamToDisband);
	}

	public static BasePlayer FindByID(ulong userID)
	{
		BasePlayer value = null;
		if (ServerInstance.cachedPlayers.TryGetValue(userID, out value))
		{
			if (value != null)
			{
				return value;
			}
			ServerInstance.cachedPlayers.Remove(userID);
		}
		BasePlayer basePlayer = BasePlayer.FindByID(userID);
		if (!basePlayer)
		{
			basePlayer = BasePlayer.FindSleeping(userID);
		}
		if (basePlayer != null)
		{
			ServerInstance.cachedPlayers.Add(userID, basePlayer);
		}
		return basePlayer;
	}

	public PlayerTeam FindTeam(ulong TeamID)
	{
		if (teams.ContainsKey(TeamID))
		{
			return teams[TeamID];
		}
		return null;
	}

	public PlayerTeam FindPlayersTeam(ulong userID)
	{
		if (playerToTeam.TryGetValue(userID, out var value))
		{
			return value;
		}
		return null;
	}

	public PlayerTeam CreateTeam()
	{
		PlayerTeam playerTeam = Facepunch.Pool.Get<PlayerTeam>();
		playerTeam.teamID = lastTeamIndex++;
		playerTeam.teamStartTime = UnityEngine.Time.realtimeSinceStartup;
		teams.Add(playerTeam.teamID, playerTeam);
		return playerTeam;
	}

	private PlayerTeam CreateTeam(ulong customId)
	{
		PlayerTeam playerTeam = Facepunch.Pool.Get<PlayerTeam>();
		playerTeam.teamID = customId;
		playerTeam.teamStartTime = UnityEngine.Time.realtimeSinceStartup;
		teams.Add(playerTeam.teamID, playerTeam);
		return playerTeam;
	}

	[ServerUserVar]
	public static void trycreateteam(ConsoleSystem.Arg arg)
	{
		if (maxTeamSize == 0)
		{
			arg.ReplyWith("Teams are disabled on this server");
			return;
		}
		BasePlayer basePlayer = arg.Player();
		if (basePlayer.currentTeam == 0L)
		{
			PlayerTeam playerTeam = ServerInstance.CreateTeam();
			playerTeam.teamLeader = basePlayer.userID;
			playerTeam.AddPlayer(basePlayer);
			Analytics.Azure.OnTeamChanged("created", playerTeam.teamID, basePlayer.userID, basePlayer.userID, playerTeam.members);
		}
	}

	[ServerUserVar]
	public static void promote(ConsoleSystem.Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (basePlayer.currentTeam == 0L)
		{
			return;
		}
		BasePlayer lookingAtPlayer = GetLookingAtPlayer(basePlayer);
		if (!(lookingAtPlayer == null) && !lookingAtPlayer.IsDead() && !(lookingAtPlayer == basePlayer) && lookingAtPlayer.currentTeam == basePlayer.currentTeam)
		{
			PlayerTeam playerTeam = ServerInstance.teams[basePlayer.currentTeam];
			if (playerTeam != null && playerTeam.teamLeader == (ulong)basePlayer.userID)
			{
				playerTeam.SetTeamLeader(lookingAtPlayer.userID);
			}
		}
	}

	[ServerUserVar]
	public static void promote_id(ConsoleSystem.Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (basePlayer.currentTeam == 0L)
		{
			return;
		}
		BasePlayer playerOrSleeperOrBot = arg.GetPlayerOrSleeperOrBot(0);
		if (!(playerOrSleeperOrBot == null) && !playerOrSleeperOrBot.IsDead() && !(playerOrSleeperOrBot == basePlayer) && playerOrSleeperOrBot.currentTeam == basePlayer.currentTeam)
		{
			PlayerTeam playerTeam = ServerInstance.teams[basePlayer.currentTeam];
			if (playerTeam != null && playerTeam.teamLeader == (ulong)basePlayer.userID)
			{
				playerTeam.SetTeamLeader(playerOrSleeperOrBot.userID);
			}
		}
	}

	[ServerUserVar]
	public static void leaveteam(ConsoleSystem.Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (!(basePlayer == null) && basePlayer.currentTeam != 0L)
		{
			PlayerTeam playerTeam = ServerInstance.FindTeam(basePlayer.currentTeam);
			if (playerTeam != null)
			{
				playerTeam.RemovePlayer(basePlayer.userID);
				basePlayer.ClearTeam();
			}
		}
	}

	[ServerUserVar]
	public static void acceptinvite(ConsoleSystem.Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (!(basePlayer == null) && basePlayer.currentTeam == 0L)
		{
			ulong uLong = arg.GetULong(0, 0uL);
			PlayerTeam playerTeam = ServerInstance.FindTeam(uLong);
			if (playerTeam == null)
			{
				basePlayer.ClearPendingInvite();
			}
			else
			{
				playerTeam.AcceptInvite(basePlayer);
			}
		}
	}

	[ServerUserVar]
	public static void rejectinvite(ConsoleSystem.Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (!(basePlayer == null) && basePlayer.currentTeam == 0L)
		{
			ulong uLong = arg.GetULong(0, 0uL);
			PlayerTeam playerTeam = ServerInstance.FindTeam(uLong);
			if (playerTeam == null)
			{
				basePlayer.ClearPendingInvite();
			}
			else
			{
				playerTeam.RejectInvite(basePlayer);
			}
		}
	}

	public static BasePlayer GetLookingAtPlayer(BasePlayer source)
	{
		if (UnityEngine.Physics.Raycast(source.eyes.position, source.eyes.HeadForward(), out var hitInfo, 5f, 1218652417, QueryTriggerInteraction.Ignore))
		{
			BaseEntity entity = hitInfo.GetEntity();
			if ((bool)entity)
			{
				return entity.GetComponent<BasePlayer>();
			}
		}
		return null;
	}

	[ServerVar]
	public static void sleeptoggle(ConsoleSystem.Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (basePlayer == null || !UnityEngine.Physics.Raycast(basePlayer.eyes.position, basePlayer.eyes.HeadForward(), out var hitInfo, 5f, 1218652417, QueryTriggerInteraction.Ignore))
		{
			return;
		}
		BaseEntity entity = hitInfo.GetEntity();
		if (!entity)
		{
			return;
		}
		BasePlayer component = entity.GetComponent<BasePlayer>();
		if ((bool)component && component != basePlayer && !component.IsNpc)
		{
			if (component.IsSleeping())
			{
				component.EndSleeping();
			}
			else
			{
				component.StartSleeping();
			}
		}
	}

	[ServerUserVar]
	public static void kickmember(ConsoleSystem.Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (basePlayer == null)
		{
			return;
		}
		PlayerTeam playerTeam = ServerInstance.FindTeam(basePlayer.currentTeam);
		if (playerTeam != null && !(playerTeam.GetLeader() != basePlayer))
		{
			ulong uLong = arg.GetULong(0, 0uL);
			if ((ulong)basePlayer.userID != uLong)
			{
				playerTeam.RemovePlayer(uLong);
			}
		}
	}

	[ServerUserVar]
	public static void sendinvite(ConsoleSystem.Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		PlayerTeam playerTeam = ServerInstance.FindTeam(basePlayer.currentTeam);
		if (playerTeam == null || playerTeam.GetLeader() == null || playerTeam.GetLeader() != basePlayer)
		{
			return;
		}
		ulong uLong = arg.GetULong(0, 0uL);
		if (uLong == 0L)
		{
			return;
		}
		BasePlayer basePlayer2 = BaseNetworkable.serverEntities.Find(new NetworkableId(uLong)) as BasePlayer;
		if ((bool)basePlayer2 && basePlayer2 != basePlayer && !basePlayer2.IsNpc && basePlayer2.currentTeam == 0L)
		{
			float num = 7f;
			if (!(Vector3.Distance(basePlayer2.transform.position, basePlayer.transform.position) > num))
			{
				playerTeam.SendInvite(basePlayer2);
			}
		}
	}

	[ServerVar]
	public static void fakeinvite(ConsoleSystem.Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		ulong uLong = arg.GetULong(0, 0uL);
		PlayerTeam playerTeam = ServerInstance.FindTeam(uLong);
		if (playerTeam != null)
		{
			if (basePlayer.currentTeam != 0L)
			{
				Debug.Log("already in team");
			}
			playerTeam.SendInvite(basePlayer);
			Debug.Log("sent bot invite");
		}
	}

	[ServerVar]
	public static void addtoteam(ConsoleSystem.Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		PlayerTeam playerTeam = ServerInstance.FindTeam(basePlayer.currentTeam);
		if (playerTeam == null || playerTeam.GetLeader() == null || playerTeam.GetLeader() != basePlayer || !UnityEngine.Physics.Raycast(basePlayer.eyes.position, basePlayer.eyes.HeadForward(), out var hitInfo, 5f, 1218652417, QueryTriggerInteraction.Ignore))
		{
			return;
		}
		BaseEntity entity = hitInfo.GetEntity();
		if ((bool)entity)
		{
			BasePlayer component = entity.GetComponent<BasePlayer>();
			if ((bool)component && component != basePlayer && !component.IsNpc)
			{
				playerTeam.AddPlayer(component);
			}
		}
	}

	[ServerVar]
	public static string createAndAddToTeam(ConsoleSystem.Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		uint uInt = arg.GetUInt(0);
		if (UnityEngine.Physics.Raycast(basePlayer.eyes.position, basePlayer.eyes.HeadForward(), out var hitInfo, 5f, 1218652417, QueryTriggerInteraction.Ignore))
		{
			BaseEntity entity = hitInfo.GetEntity();
			if ((bool)entity)
			{
				BasePlayer component = entity.GetComponent<BasePlayer>();
				if ((bool)component && component != basePlayer && !component.IsNpc)
				{
					if (component.currentTeam != 0L)
					{
						return component.displayName + " is already in a team";
					}
					if (ServerInstance.FindTeam(uInt) != null)
					{
						ServerInstance.FindTeam(uInt).AddPlayer(component);
						return $"Added {component.displayName} to existing team {uInt}";
					}
					PlayerTeam playerTeam = ServerInstance.CreateTeam(uInt);
					playerTeam.teamLeader = component.userID;
					playerTeam.AddPlayer(component);
					return $"Added {component.displayName} to team {uInt}";
				}
			}
		}
		return "Unable to find valid player in front";
	}

	public static bool TeamsEnabled()
	{
		return maxTeamSize > 0;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (!info.fromDisk || info.msg.relationshipManager == null)
		{
			return;
		}
		lastTeamIndex = info.msg.relationshipManager.lastTeamIndex;
		foreach (ProtoBuf.PlayerTeam team in info.msg.relationshipManager.teamList)
		{
			PlayerTeam playerTeam = Facepunch.Pool.Get<PlayerTeam>();
			playerTeam.teamLeader = team.teamLeader;
			playerTeam.teamID = team.teamID;
			playerTeam.teamName = team.teamName;
			playerTeam.members = new List<ulong>();
			foreach (ProtoBuf.PlayerTeam.TeamMember member in team.members)
			{
				playerTeam.members.Add(member.userID);
			}
			teams[playerTeam.teamID] = playerTeam;
		}
		foreach (PlayerTeam value2 in teams.Values)
		{
			foreach (ulong member2 in value2.members)
			{
				playerToTeam[member2] = value2;
				BasePlayer basePlayer = FindByID(member2);
				if (basePlayer != null && basePlayer.currentTeam != value2.teamID)
				{
					Debug.LogWarning($"Player {member2} has the wrong teamID: got {basePlayer.currentTeam}, expected {value2.teamID}. Fixing automatically.");
					basePlayer.currentTeam = value2.teamID;
				}
			}
		}
		foreach (ProtoBuf.RelationshipManager.PlayerRelationships relationship in info.msg.relationshipManager.relationships)
		{
			ulong playerID = relationship.playerID;
			PlayerRelationships playerRelationships = GetRelationships(playerID);
			playerRelationships.relations.Clear();
			foreach (ProtoBuf.RelationshipManager.PlayerRelationshipInfo relation in relationship.relations)
			{
				PlayerRelationshipInfo value = PlayerRelationshipInfo.FromProto(relation);
				playerRelationships.relations.Add(relation.playerID, value);
			}
		}
	}
}
