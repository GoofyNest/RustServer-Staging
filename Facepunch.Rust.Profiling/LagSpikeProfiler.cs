using System;
using System.Collections.Generic;
using ConVar;
using Facepunch.Extend;
using Network;
using UnityEngine;

namespace Facepunch.Rust.Profiling;

public static class LagSpikeProfiler
{
	public static bool enabled = false;

	private static List<EventRecord> pendingEvents = new List<EventRecord>();

	public static void Serialize(AzureAnalyticsUploader uploader, int frameIndex, DateTime timestamp)
	{
		try
		{
			if (!enabled)
			{
				return;
			}
			foreach (EventRecord pendingEvent in pendingEvents)
			{
				pendingEvent.Timestamp = timestamp;
				pendingEvent.AddField("frame_index", frameIndex);
				uploader.Append(pendingEvent);
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("Failed to serialize lag spikes: " + ex.Message);
		}
	}

	public static void Reset()
	{
		pendingEvents.Clear();
	}

	private static void AddPendingRecord(EventRecord record)
	{
		pendingEvents.Add(record);
		if (pendingEvents.Count > 5000)
		{
			Pool.Free(ref record);
		}
	}

	public static void RPC(TimeSpan time, Message packet, BaseEntity entity, uint rpcId)
	{
		if (enabled)
		{
			string value = StringPool.Get(rpcId);
			AddPendingRecord(CreateRecord(time, "rpc").AddField("entity", entity).AddField("rpc", value).AddField("connection_user", (packet?.connection?.userid).GetValueOrDefault()));
		}
	}

	public static void ConsoleCommand(TimeSpan time, Message packet, string command)
	{
		if (enabled)
		{
			string value = command.Truncate(4096);
			AddPendingRecord(CreateRecord(time, "console_command").AddField("command", value).AddField("command_length", command.Length).AddField("connection_user", (packet?.connection?.userid).GetValueOrDefault()));
		}
	}

	public static void RconCommand(TimeSpan time, string command)
	{
		if (enabled)
		{
			string value = command.Truncate(4096);
			AddPendingRecord(CreateRecord(time, "console_command").AddField("command", value).AddField("command_length", command.Length));
		}
	}

	private static EventRecord CreateRecord(TimeSpan duration, string reason)
	{
		return EventRecord.New("lag_spike").AddField("duration", duration).AddField("reason", reason)
			.AddField("server_id", ConVar.Server.server_id);
	}
}
