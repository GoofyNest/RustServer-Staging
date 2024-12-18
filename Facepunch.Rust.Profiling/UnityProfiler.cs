using System;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace Facepunch.Rust.Profiling;

public static class UnityProfiler
{
	private struct RecorderInfo
	{
		public string MethodName;

		public ProfilerRecorder Recorder;
	}

	private static List<RecorderInfo> ActiveRecorders = new List<RecorderInfo>();

	private static bool _enabled;

	public static bool enabled
	{
		get
		{
			return _enabled;
		}
		set
		{
			SetEnabled(value);
		}
	}

	private static void SetEnabled(bool state)
	{
		_enabled = state;
		Unload();
		if (!state)
		{
			return;
		}
		List<ProfilerRecorderHandle> list = new List<ProfilerRecorderHandle>();
		ProfilerRecorderHandle.GetAvailable(list);
		foreach (ProfilerRecorderDescription item2 in list.Select((ProfilerRecorderHandle x) => ProfilerRecorderHandle.GetDescription(x)).ToList())
		{
			if (item2.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds && (ushort)item2.Category == (ushort)ProfilerCategory.Scripts)
			{
				ProfilerRecorder recorder = ProfilerRecorder.StartNew(item2.Category, item2.Name, 2);
				RecorderInfo recorderInfo = default(RecorderInfo);
				recorderInfo.MethodName = item2.Name;
				recorderInfo.Recorder = recorder;
				RecorderInfo item = recorderInfo;
				ActiveRecorders.Add(item);
			}
		}
	}

	public static void Unload()
	{
		foreach (RecorderInfo activeRecorder in ActiveRecorders)
		{
			activeRecorder.Recorder.Dispose();
		}
		ActiveRecorders.Clear();
	}

	public static void Serialize(AzureAnalyticsUploader uploader, DateTime timestamp, int frameIndex)
	{
		if (!enabled)
		{
			return;
		}
		try
		{
			foreach (RecorderInfo activeRecorder in ActiveRecorders)
			{
				ProfilerRecorder recorder = activeRecorder.Recorder;
				if (recorder.LastValue != 0L)
				{
					EventRecord eventRecord = EventRecord.CSV();
					EventRecord eventRecord2 = eventRecord.AddField("", frameIndex).AddField("", timestamp).AddField("", activeRecorder.MethodName);
					recorder = activeRecorder.Recorder;
					eventRecord2.AddField("", recorder.LastValue).AddField("", Server.server_id);
					uploader.Append(eventRecord);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("Failed to serialize profiler data: " + ex.Message);
		}
	}
}
