using System;
using ConVar;
using UnityEngine;

namespace Facepunch.Rust.Profiling;

public static class WorkQueueProfiler
{
	public static bool enabled;

	public static void Serialize(AzureAnalyticsUploader uploader, int frameIndex, DateTime timestamp)
	{
		if (!enabled)
		{
			return;
		}
		try
		{
			foreach (ObjectWorkQueue item in ObjectWorkQueue.All)
			{
				if (item.LastProcessedCount != 0)
				{
					EventRecord eventRecord = EventRecord.CSV();
					eventRecord.AddField("", frameIndex).AddField("", timestamp).AddField("", item.Name)
						.AddField("", item.LastQueueLength)
						.AddField("", item.LastExecutionTime)
						.AddField("", item.LastProcessedCount)
						.AddField("", Server.server_id);
					uploader.Append(eventRecord);
				}
			}
			foreach (PersistentObjectWorkQueue item2 in PersistentObjectWorkQueue.All)
			{
				if (item2.ListLength != 0)
				{
					EventRecord eventRecord2 = EventRecord.CSV();
					eventRecord2.AddField("", frameIndex).AddField("", timestamp).AddField("", item2.Name)
						.AddField("", item2.ListLength)
						.AddField("", item2.LastExecutionTime)
						.AddField("", item2.LastProcessedCount)
						.AddField("", Server.server_id);
					uploader.Append(eventRecord2);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("Failed to serialize work queues: " + ex.Message);
		}
	}

	public static void Reset()
	{
	}
}
