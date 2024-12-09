using System;
using System.Collections;
using System.Collections.Generic;
using Facepunch;
using Facepunch.Extend;
using Facepunch.Math;
using Network;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class ConnectionAuth : MonoBehaviour
{
	[NonSerialized]
	public static List<Connection> m_AuthConnection = new List<Connection>();

	private static RealTimeSince _sinceClientRequirementRefresh;

	private static (int, int)? _clientRequirementCache;

	private static string _clientVersionMismatchMessage;

	public bool IsAuthed(ulong iSteamID)
	{
		if ((bool)BasePlayer.FindByID(iSteamID))
		{
			return true;
		}
		if (SingletonComponent<ServerMgr>.Instance.connectionQueue.IsJoining(iSteamID))
		{
			return true;
		}
		if (SingletonComponent<ServerMgr>.Instance.connectionQueue.IsQueued(iSteamID))
		{
			return true;
		}
		return false;
	}

	public static void Reject(Connection connection, string strReason, string strReasonPrivate = null)
	{
		DebugEx.Log(connection.ToString() + " Rejecting connection - " + (string.IsNullOrEmpty(strReasonPrivate) ? strReason : strReasonPrivate));
		Net.sv.Kick(connection, strReason);
		m_AuthConnection.Remove(connection);
	}

	public static void OnDisconnect(Connection connection)
	{
		m_AuthConnection.Remove(connection);
	}

	public void Approve(Connection connection)
	{
		m_AuthConnection.Remove(connection);
		SingletonComponent<ServerMgr>.Instance.connectionQueue.Join(connection);
	}

	public void OnNewConnection(Connection connection)
	{
		connection.connected = false;
		if (connection.token == null || connection.token.Length < 32)
		{
			Reject(connection, "Invalid Token");
			return;
		}
		if (connection.userid == 0L)
		{
			Reject(connection, "Invalid SteamID");
			return;
		}
		if (connection.protocol != 2571)
		{
			if (!DeveloperList.Contains(connection.userid))
			{
				Reject(connection, "Incompatible Version");
				return;
			}
			DebugEx.Log("Not kicking " + connection.userid + " for incompatible protocol (is a developer)");
		}
		if (ServerUsers.Is(connection.userid, ServerUsers.UserGroup.Banned))
		{
			ServerUsers.User user = ServerUsers.Get(connection.userid);
			string text = user?.notes ?? "no reason given";
			string text2 = ((user != null && user.expiry > 0) ? (" for " + (user.expiry - Epoch.Current).FormatSecondsLong()) : "");
			Reject(connection, "You are banned from this server" + text2 + " (" + text + ")");
			return;
		}
		if (ServerUsers.Is(connection.userid, ServerUsers.UserGroup.Moderator))
		{
			DebugEx.Log(connection.ToString() + " has auth level 1");
			connection.authLevel = 1u;
		}
		if (ServerUsers.Is(connection.userid, ServerUsers.UserGroup.Owner))
		{
			DebugEx.Log(connection.ToString() + " has auth level 2");
			connection.authLevel = 2u;
		}
		if (DeveloperList.Contains(connection.userid))
		{
			DebugEx.Log(connection.ToString() + " is a developer");
			connection.authLevel = 3u;
		}
		m_AuthConnection.Add(connection);
		StartCoroutine(AuthorisationRoutine(connection));
	}

	public IEnumerator AuthorisationRoutine(Connection connection)
	{
		yield return StartCoroutine(Auth_Steam.Run(connection));
		yield return StartCoroutine(Auth_EAC.Run(connection));
		yield return StartCoroutine(Auth_CentralizedBans.Run(connection));
		yield return StartCoroutine(Auth_Nexus.Run(connection));
		if (connection.rejected || !connection.active)
		{
			yield break;
		}
		if (IsAuthed(connection.userid))
		{
			Reject(connection, "You are already connected as a player!");
			yield break;
		}
		if (connection.authLevel == 0)
		{
			(int, int)? minClientRequirement = GetMinClientRequirement();
			if (minClientRequirement.HasValue && (connection.clientChangeset < minClientRequirement.Value.Item1 || connection.clientBuildTime < minClientRequirement.Value.Item2))
			{
				Reject(connection, GetClientVersionMismatchMessage());
				yield break;
			}
		}
		Approve(connection);
	}

	private static (int Changeset, int BuildTime)? GetMinClientRequirement()
	{
		if ((float)_sinceClientRequirementRefresh < 10f)
		{
			return _clientRequirementCache;
		}
		_sinceClientRequirementRefresh = 0f;
		_clientRequirementCache = null;
		try
		{
			string text = BuildInfo.Current?.Scm?.Branch ?? "";
			if (!string.IsNullOrWhiteSpace(text))
			{
				JToken jToken = (Facepunch.Application.Manifest?.Metadata)?["ClientVersions"]?[text];
				if (jToken != null && jToken.Type == JTokenType.Object)
				{
					int item = jToken.Value<int>("Changeset");
					int item2 = jToken.Value<int>("Timestamp");
					_clientRequirementCache = (item, item2);
				}
			}
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
			_clientRequirementCache = null;
		}
		return _clientRequirementCache;
	}

	private static string GetClientVersionMismatchMessage()
	{
		if (!string.IsNullOrEmpty(_clientVersionMismatchMessage))
		{
			return _clientVersionMismatchMessage;
		}
		string text = BuildInfo.Current?.Scm?.Branch ?? "unknown";
		string text2 = text.ToLowerInvariant();
		string clientVersionMismatchMessage = ((text2 == "release") ? "Client update required. Close Rust and apply update from Steam." : ((!(text2 == "main")) ? ("Client update required. Apply \"Rust - Staging Branch\" (" + text + " beta) update from Steam.") : "Client update required. Apply \"Rust - Staging Branch\" update from Steam."));
		_clientVersionMismatchMessage = clientVersionMismatchMessage;
		return _clientVersionMismatchMessage;
	}
}
