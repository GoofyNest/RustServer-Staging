using System.Collections.Generic;
using System.Diagnostics;
using ConVar;
using Facepunch.Rust.Profiling;
using Network;
using UnityEngine;

public static class ConsoleNetwork
{
	private static Stopwatch timer = new Stopwatch();

	internal static void Init()
	{
	}

	internal static void OnClientCommand(Message packet)
	{
		if (packet.read.Unread > ConVar.Server.maxpacketsize_command)
		{
			UnityEngine.Debug.LogWarning("Dropping client command due to size");
			return;
		}
		timer.Restart();
		string text = packet.read.StringRaw();
		if (packet.connection == null || !packet.connection.connected)
		{
			UnityEngine.Debug.LogWarning("Client without connection tried to run command: " + text);
			return;
		}
		string text2 = ConsoleSystem.Run(ConsoleSystem.Option.Server.FromConnection(packet.connection).Quiet(), text);
		if (!string.IsNullOrEmpty(text2))
		{
			SendClientReply(packet.connection, text2);
		}
		if (timer.Elapsed > RuntimeProfiler.ConsoleCommandWarningThreshold)
		{
			LagSpikeProfiler.ConsoleCommand(timer.Elapsed, packet, text);
		}
	}

	internal static void SendClientReply(Connection cn, string strCommand)
	{
		if (Network.Net.sv.IsConnected())
		{
			NetWrite netWrite = Network.Net.sv.StartWrite();
			netWrite.PacketID(Message.Type.ConsoleMessage);
			netWrite.String(strCommand);
			netWrite.Send(new SendInfo(cn));
		}
	}

	public static void SendClientCommand(Connection cn, string strCommand, params object[] args)
	{
		if (Network.Net.sv.IsConnected())
		{
			NetWrite netWrite = Network.Net.sv.StartWrite();
			netWrite.PacketID(Message.Type.ConsoleCommand);
			string val = ConsoleSystem.BuildCommand(strCommand, args);
			netWrite.String(val);
			netWrite.Send(new SendInfo(cn));
		}
	}

	public static void SendClientCommandImmediate(Connection cn, string strCommand, params object[] args)
	{
		if (Network.Net.sv.IsConnected())
		{
			NetWrite netWrite = Network.Net.sv.StartWrite();
			netWrite.PacketID(Message.Type.ConsoleCommand);
			string val = ConsoleSystem.BuildCommand(strCommand, args);
			netWrite.String(val);
			netWrite.SendImmediate(new SendInfo(cn)
			{
				priority = Priority.Immediate
			});
		}
	}

	public static void SendClientCommand(List<Connection> cn, string strCommand, params object[] args)
	{
		if (Network.Net.sv.IsConnected())
		{
			NetWrite netWrite = Network.Net.sv.StartWrite();
			netWrite.PacketID(Message.Type.ConsoleCommand);
			netWrite.String(ConsoleSystem.BuildCommand(strCommand, args));
			netWrite.Send(new SendInfo(cn));
		}
	}

	public static void BroadcastToAllClients(string strCommand, params object[] args)
	{
		if (Network.Net.sv.IsConnected())
		{
			NetWrite netWrite = Network.Net.sv.StartWrite();
			netWrite.PacketID(Message.Type.ConsoleCommand);
			netWrite.String(ConsoleSystem.BuildCommand(strCommand, args));
			netWrite.Send(new SendInfo(Network.Net.sv.connections));
		}
	}
}
