using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ConVar;
using Rust;
using UnityEngine;

namespace Facepunch.Rust.Profiling;

public static class EntityProfiler
{
	public class EntityCounter
	{
		public string Name;

		public int count;

		public int spawned;

		public int killed;

		public bool queued;

		public EntityCounter(string name)
		{
			Name = name;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Reset()
		{
			spawned = 0;
			killed = 0;
			queued = false;
		}
	}

	public static int mode = 0;

	public static TimeSpan aggregateEntityCountDelay = TimeSpan.FromSeconds(300.0);

	public static int spawned;

	public static int killed;

	public static Dictionary<uint, EntityCounter> counts = new Dictionary<uint, EntityCounter>();

	public static List<EntityCounter> list = new List<EntityCounter>();

	private static DateTime aggregateEntityCountCooldown;

	public static void OnSpawned(BaseNetworkable entity)
	{
		if (!counts.TryGetValue(entity.prefabID, out var value))
		{
			value = new EntityCounter(entity.ShortPrefabName);
			counts[entity.prefabID] = value;
		}
		if (!value.queued)
		{
			list.Add(value);
			value.queued = true;
		}
		value.spawned++;
		value.count++;
	}

	public static void OnKilled(BaseNetworkable entity)
	{
		if (!counts.TryGetValue(entity.prefabID, out var value))
		{
			value = new EntityCounter(entity.ShortPrefabName);
			counts[entity.prefabID] = value;
		}
		if (!value.queued)
		{
			list.Add(value);
			value.queued = true;
		}
		value.killed++;
		value.count--;
	}

	public static void Reset()
	{
		killed = 0;
		spawned = 0;
		foreach (EntityCounter item in list)
		{
			item.Reset();
		}
		list.Clear();
	}

	public static void TrySerializeEntityAggregates(int frameIndex, DateTime timestamp, AzureAnalyticsUploader uploader)
	{
		if (mode < 3 || global::Rust.Application.isLoadingSave || aggregateEntityCountCooldown > DateTime.UtcNow)
		{
			return;
		}
		aggregateEntityCountCooldown = DateTime.UtcNow + aggregateEntityCountDelay;
		foreach (EntityCounter value in counts.Values)
		{
			EventRecord eventRecord = EventRecord.CSV();
			eventRecord.AddField("", timestamp).AddField("", ConVar.Server.server_id).AddField("", frameIndex)
				.AddField("", value.Name)
				.AddField("", value.count);
			uploader.Append(eventRecord);
		}
	}

	public static void Serialize(EventRecord record, int frameIndex, DateTime timestamp, AzureAnalyticsUploader uploader)
	{
		if (mode == 0)
		{
			return;
		}
		try
		{
			record.AddField("entities_spawned", spawned);
			record.AddField("entities_killed", killed);
			if (mode < 3)
			{
				return;
			}
			foreach (EntityCounter item in list)
			{
				EventRecord eventRecord = EventRecord.CSV();
				eventRecord.AddField("", timestamp).AddField("", ConVar.Server.server_id).AddField("", frameIndex)
					.AddField("", item.Name)
					.AddField("", item.spawned)
					.AddField("", item.killed);
				uploader.Append(eventRecord);
			}
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}
}
