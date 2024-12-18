using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using ConVar;
using Network;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace Facepunch.Rust.Profiling;

[ConsoleSystem.Factory("profile")]
public static class RuntimeProfiler
{
	private static class ProfilerCategories
	{
		public static readonly ProfilerCategory VSync = new ProfilerCategory("VSync");

		public static readonly ProfilerCategory PlayerLoop = new ProfilerCategory("PlayerLoop");
	}

	private static int profilingPreset = 0;

	private static int _profilingInterval = 60;

	private static bool _init = false;

	private static Stopwatch serializationTimer = new Stopwatch();

	private static AzureAnalyticsUploader frameProfilingUploader;

	private static AzureAnalyticsUploader entityProfilingUploader;

	private static AzureAnalyticsUploader entityAggregateUploader;

	private static AzureAnalyticsUploader invokeDetailsUploader;

	private static AzureAnalyticsUploader methodUploader;

	private static AzureAnalyticsUploader objectWorkQueueUploader;

	private static AzureAnalyticsUploader packetUploader;

	private static AzureAnalyticsUploader lagSpikeUploader;

	private static AzureAnalyticsUploader rconUploader;

	private static AzureAnalyticsUploader raknetUploader;

	private static AzureAnalyticsUploader poolUploader;

	public static TimeSpan ServerMgr_Update;

	public static TimeSpan Net_Cycle;

	public static TimeSpan Physics_SyncTransforms;

	public static TimeSpan Companion_Tick;

	public static TimeSpan BasePlayer_ServerCycle;

	private static DateTime nextPoolFlush;

	private static readonly ProfilerRecorderOptions PhysicsRecorderOptions = ProfilerRecorderOptions.WrapAroundWhenCapacityReached;

	private static readonly List<RustProfilerRecorder> recorders = new List<RustProfilerRecorder>
	{
		new RustProfilerRecorder("cpu_total", ProfilerCategory.Scripts, "CPU Total Frame Time"),
		new RustProfilerRecorder("main_thread", ProfilerCategory.Scripts, "CPU Main Thread Frame Time"),
		new RustProfilerRecorder("gc_collect_time", ProfilerCategory.Memory, "GC.Collect"),
		new RustProfilerRecorder("player_loop", ProfilerCategories.PlayerLoop, "PlayerLoop"),
		new RustProfilerRecorder("wait_for_target_fps", ProfilerCategories.VSync, "WaitForTargetFPS"),
		new RustProfilerRecorder("ram_app_resident", ProfilerCategory.Memory, "App Resident Memory"),
		new RustProfilerRecorder("ram_total_used", ProfilerCategory.Memory, "Total Used Memory"),
		new RustProfilerRecorder("ram_gc_used", ProfilerCategory.Memory, "GC Used Memory"),
		new RustProfilerRecorder("gc_alloc_bytes", ProfilerCategory.Memory, "GC Allocated In Frame"),
		new RustProfilerRecorder("gc_alloc_count", ProfilerCategory.Memory, "GC Allocation In Frame Count"),
		new RustProfilerRecorder("active_dynamic_bodies", ProfilerCategory.Physics, "ctive Dynamic Bodies", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("active_kinematic_bodies", ProfilerCategory.Physics, "Active Kinematic Bodies", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("static_colliders", ProfilerCategory.Physics, "Static Colliders", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("dynamic_bodies", ProfilerCategory.Physics, "Dynamic Bodies", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("articulation_bodies", ProfilerCategory.Physics, "Articulation Bodies", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("active_constraints", ProfilerCategory.Physics, "Active Constraints", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("overlaps", ProfilerCategory.Physics, "Overlaps", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("discreet_overlaps", ProfilerCategory.Physics, "Discreet Overlaps", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("continuous_overlaps", ProfilerCategory.Physics, "Continuous Overlaps", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("modified_overlaps", ProfilerCategory.Physics, "Modified Overlaps", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("trigger_overlaps", ProfilerCategory.Physics, "Trigger Overlaps", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("colliders_synced", ProfilerCategory.Physics, "Colliders Synced", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("rigidbodies_synced", ProfilerCategory.Physics, "Rigidbodies Synced", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("physics_queries", ProfilerCategory.Physics, "Physics Queries", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("broadphase_adds_removes", ProfilerCategory.Physics, "Broadphase Adds/Removes", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("broadphase_adds", ProfilerCategory.Physics, "Broadphase Adds", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("broadphase_removes", ProfilerCategory.Physics, "Broadphase Removes", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("narrowphase_touches", ProfilerCategory.Physics, "Narrowphase Touches", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("narrowphase_new_touches", ProfilerCategory.Physics, "Narrowphase New Touches", 1, PhysicsRecorderOptions),
		new RustProfilerRecorder("narrowphase_lost_touches", ProfilerCategory.Physics, "Narrowphase Lost Touches", 1, PhysicsRecorderOptions)
	};

	private static Stopwatch invokeExecutionResetTimer = new Stopwatch();

	[ServerVar]
	public static int rpc_lagspike_threshold
	{
		get
		{
			return (int)RpcWarningThreshold.TotalMilliseconds;
		}
		set
		{
			RpcWarningThreshold = TimeSpan.FromMilliseconds(value);
		}
	}

	[ServerVar]
	public static int command_lagspike_threshold
	{
		get
		{
			return (int)ConsoleCommandWarningThreshold.TotalMilliseconds;
		}
		set
		{
			ConsoleCommandWarningThreshold = TimeSpan.FromMilliseconds(value);
		}
	}

	[ServerVar]
	public static int rcon_lagspike_threshold
	{
		get
		{
			return (int)RconCommandWarningThreshold.TotalMilliseconds;
		}
		set
		{
			RconCommandWarningThreshold = TimeSpan.FromMilliseconds(value);
		}
	}

	public static TimeSpan RpcWarningThreshold { get; private set; } = TimeSpan.FromMilliseconds(40.0);


	public static TimeSpan ConsoleCommandWarningThreshold { get; private set; } = TimeSpan.FromMilliseconds(40.0);


	public static TimeSpan RconCommandWarningThreshold { get; private set; } = TimeSpan.FromMilliseconds(40.0);


	[ServerVar(Saved = true, Help = "0 = off, 1 = basic, 2 = everything. This will reset all profiling convars, however they can be modified afterwards")]
	public static int runtime_profiling
	{
		get
		{
			return profilingPreset;
		}
		set
		{
			profilingPreset = Mathf.Max(0, value);
			OnProfilingPresetChanged();
		}
	}

	[ServerVar(Saved = true, Help = "Enable to allow runtime profiling to persist across restarts")]
	public static bool runtime_profiling_persist { get; set; } = false;


	[ServerVar(Help = "Record inbound RPC & ConsoleCommands that cause lag spikes")]
	public static bool profiling_lagspikes
	{
		get
		{
			return LagSpikeProfiler.enabled;
		}
		set
		{
			LagSpikeProfiler.enabled = value;
		}
	}

	[ServerVar(Help = "Record type of packets inbound/outbound per frame")]
	public static bool profiling_packets
	{
		get
		{
			return PacketProfiler.enabled;
		}
		set
		{
			PacketProfiler.enabled = value;
		}
	}

	[ServerVar(Help = "0 = off, 1 = stats per frame, 2 = stats per method")]
	public static int profiling_invokes
	{
		get
		{
			return InvokeProfiler.update.mode;
		}
		set
		{
			InvokeProfiler.update.mode = Mathf.Max(0, value);
		}
	}

	[ServerVar(Help = "0 = off, 1 = stats per frame, 2 = stats per method")]
	public static int profiling_fixed_invokes
	{
		get
		{
			return InvokeProfiler.fixedUpdate.mode;
		}
		set
		{
			InvokeProfiler.fixedUpdate.mode = Mathf.Max(0, value);
		}
	}

	[ServerVar(Help = "0 = off, 1 = spawn/kill, 2 = spawn/kill per entity, 3 = count every '5 min'")]
	public static int profiling_entities
	{
		get
		{
			return EntityProfiler.mode;
		}
		set
		{
			EntityProfiler.mode = Mathf.Max(0, value);
		}
	}

	[ServerVar(Help = "How frequently to count all entities across the server")]
	public static int profiling_entity_count_interval
	{
		get
		{
			return (int)EntityProfiler.aggregateEntityCountDelay.TotalSeconds;
		}
		set
		{
			EntityProfiler.aggregateEntityCountDelay = TimeSpan.FromSeconds(Mathf.Max(60, value));
		}
	}

	[ServerVar(Help = "Record execution time of ObjectWorkQueues per frame")]
	public static bool profiling_work_queue
	{
		get
		{
			return WorkQueueProfiler.enabled;
		}
		set
		{
			WorkQueueProfiler.enabled = value;
		}
	}

	[ServerVar(Help = "0 = off, 1 = count per frame, 2 = connection attempts, 3 = messages")]
	public static int profiling_rcon
	{
		get
		{
			return RconProfiler.mode;
		}
		set
		{
			RconProfiler.mode = Mathf.Max(0, value);
		}
	}

	[ServerVar(Help = "Clamp the length of logged RCON messages to prevent the profiler from being flooded with large messages")]
	public static int profiling_rcon_message_length
	{
		get
		{
			return RconProfiler.ClampedMessageLength;
		}
		set
		{
			RconProfiler.ClampedMessageLength = Mathf.Max(64, value);
		}
	}

	[ServerVar]
	public static int runtime_profiling_interval
	{
		get
		{
			return _profilingInterval;
		}
		set
		{
			_profilingInterval = Mathf.Clamp(value, 60, 1800);
		}
	}

	[ServerVar(Help = "Should analytics bulk uploaders use pooling")]
	public static bool runtime_profiling_uploader_pooling
	{
		get
		{
			return AzureAnalyticsUploader.UsePooling;
		}
		set
		{
			AzureAnalyticsUploader.UsePooling = value;
		}
	}

	[ServerVar(Help = "Raknet statistics, 0 = off, 2 = per connection")]
	public static int profiling_ping
	{
		get
		{
			return PlayerNetworkingProfiler.level;
		}
		set
		{
			PlayerNetworkingProfiler.level = Mathf.Max(0, value);
		}
	}

	[ServerVar(Help = "0 = off, 1 = flush every 5 minutes")]
	public static int runtime_profiling_pooling { get; set; }

	[ServerVar(Help = "How often to flush raknet stats per second")]
	public static float profiling_ping_interval
	{
		get
		{
			return (float)PlayerNetworkingProfiler.MinFlushInterval.TotalSeconds;
		}
		set
		{
			PlayerNetworkingProfiler.MinFlushInterval = TimeSpan.FromSeconds(value);
		}
	}

	[ServerVar]
	public static int profiling_ping_per_frame
	{
		get
		{
			return PlayerNetworkingProfiler.ConnectionsPerFrame;
		}
		set
		{
			PlayerNetworkingProfiler.ConnectionsPerFrame = Mathf.Max(1, value);
		}
	}

	[ServerVar(Help = "How often to flush pooling stats in seconds")]
	public static int runtime_profiling_pool_flush_interval { get; set; } = 300;


	[ServerVar]
	[ClientVar(ClientAdmin = true)]
	public static void dump_profile_recorders(ConsoleSystem.Arg arg)
	{
		List<ProfilerRecorderHandle> list = new List<ProfilerRecorderHandle>();
		ProfilerRecorderHandle.GetAvailable(list);
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("Name,Category,UnitType,Flags");
		foreach (ProfilerRecorderHandle item in list)
		{
			ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(item);
			stringBuilder.Append(description.Name).Append(",").Append(description.Category.ToString())
				.Append(",")
				.Append(description.UnitType.ToString())
				.Append(",")
				.Append(description.Flags.ToString())
				.AppendLine();
		}
		string contents = stringBuilder.ToString();
		File.WriteAllText("profiler_recorders.csv", contents);
		arg.ReplyWith($"Successfully dumped '{list.Count}' markers");
	}

	public static void Disable()
	{
		runtime_profiling = 0;
	}

	private static void Start()
	{
		ResetAllMeasurements();
	}

	private static void OnProfilingPresetChanged()
	{
		profiling_lagspikes = false;
		profiling_packets = false;
		profiling_invokes = 0;
		profiling_fixed_invokes = 0;
		profiling_entities = 0;
		profiling_work_queue = false;
		profiling_rcon = 0;
		if (profilingPreset >= 1)
		{
			profiling_entities = 1;
			profiling_lagspikes = true;
			profiling_rcon = 1;
			runtime_profiling_pooling = 1;
		}
		if (profilingPreset >= 2)
		{
			profiling_packets = true;
			profiling_invokes = 2;
			profiling_fixed_invokes = 2;
			profiling_entities = 3;
			profiling_work_queue = true;
			profiling_rcon = 3;
			profiling_ping = 2;
		}
	}

	public static void Update()
	{
		if (!Bootstrap.bootstrapInitRun)
		{
			return;
		}
		if (runtime_profiling == 0)
		{
			_init = false;
		}
		else if (!string.IsNullOrEmpty(Analytics.BulkUploadConnectionString))
		{
			if (!_init)
			{
				Start();
				_init = true;
			}
			EnsureUploadersCreated();
			CollectLastFrameStats();
		}
	}

	private static void EnsureUploadersCreated()
	{
		if (frameProfilingUploader.NeedsCreation())
		{
			frameProfilingUploader = AzureAnalyticsUploader.Create("frame_profiling", TimeSpan.FromSeconds(runtime_profiling_interval));
		}
		if (entityProfilingUploader.NeedsCreation())
		{
			entityProfilingUploader = AzureAnalyticsUploader.Create("entity_profiling", TimeSpan.FromSeconds(runtime_profiling_interval), AnalyticsDocumentMode.CSV);
		}
		if (entityAggregateUploader.NeedsCreation())
		{
			entityAggregateUploader = AzureAnalyticsUploader.Create("entity_aggregates", TimeSpan.FromSeconds(runtime_profiling_interval), AnalyticsDocumentMode.CSV);
		}
		if (invokeDetailsUploader.NeedsCreation())
		{
			invokeDetailsUploader = AzureAnalyticsUploader.Create("invoke_breakdown", TimeSpan.FromSeconds(runtime_profiling_interval), AnalyticsDocumentMode.CSV);
		}
		if (methodUploader.NeedsCreation())
		{
			methodUploader = AzureAnalyticsUploader.Create("unity_methods", TimeSpan.FromSeconds(runtime_profiling_interval), AnalyticsDocumentMode.CSV);
		}
		if (objectWorkQueueUploader.NeedsCreation())
		{
			objectWorkQueueUploader = AzureAnalyticsUploader.Create("object_work_queue", TimeSpan.FromSeconds(runtime_profiling_interval), AnalyticsDocumentMode.CSV);
		}
		if (packetUploader.NeedsCreation())
		{
			packetUploader = AzureAnalyticsUploader.Create("packets_per_type", TimeSpan.FromSeconds(runtime_profiling_interval), AnalyticsDocumentMode.CSV);
		}
		if (lagSpikeUploader.NeedsCreation())
		{
			lagSpikeUploader = AzureAnalyticsUploader.Create("lag_spikes", TimeSpan.FromSeconds(runtime_profiling_interval));
			lagSpikeUploader.UseJsonDataObject = true;
		}
		if (rconUploader.NeedsCreation())
		{
			rconUploader = AzureAnalyticsUploader.Create("rcon_profiling", TimeSpan.FromSeconds(runtime_profiling_interval));
		}
		if (raknetUploader.NeedsCreation())
		{
			raknetUploader = AzureAnalyticsUploader.Create("raknet", TimeSpan.FromSeconds(runtime_profiling_interval), AnalyticsDocumentMode.CSV);
		}
		if (poolUploader.NeedsCreation())
		{
			poolUploader = AzureAnalyticsUploader.Create("pool_profiling", TimeSpan.FromSeconds(runtime_profiling_interval));
			poolUploader.UseJsonDataObject = true;
		}
	}

	private static void CollectLastFrameStats()
	{
		WriteFrameData(UnityEngine.Time.frameCount - 1);
	}

	private static void WriteFrameData(int frameIndex)
	{
		serializationTimer.Restart();
		RconProfilerStats obj = RconProfiler.GetCurrentStats();
		DateTime utcNow = DateTime.UtcNow;
		EventRecord eventRecord = EventRecord.New("frame_profiling").AddField("frame_index", frameIndex);
		eventRecord.Timestamp = utcNow;
		LagSpikeProfiler.Serialize(lagSpikeUploader, frameIndex, utcNow);
		SerializeCommon(eventRecord, obj);
		SerializeNetworking(eventRecord, frameIndex, utcNow);
		SerializeInvokes(eventRecord);
		SerializeInvokeExecutionTime(InvokeProfiler.update, invokeDetailsUploader, frameIndex, utcNow);
		SerializeInvokeExecutionTime(InvokeProfiler.fixedUpdate, invokeDetailsUploader, frameIndex, utcNow);
		SerializeProfilingSamples(eventRecord);
		EntityProfiler.Serialize(eventRecord, frameIndex, utcNow, entityProfilingUploader);
		EntityProfiler.TrySerializeEntityAggregates(frameIndex, utcNow, entityAggregateUploader);
		WorkQueueProfiler.Serialize(objectWorkQueueUploader, frameIndex, utcNow);
		PlayerNetworkingProfiler.Serialize(raknetUploader, frameIndex, utcNow);
		SerializeRconEvents(rconUploader, frameIndex, utcNow, obj);
		SerializeMemoryPool(poolUploader, frameIndex, utcNow);
		ResetAllMeasurements();
		Pool.Free(ref obj);
		eventRecord.AddField("serialization_time", serializationTimer.Elapsed);
		frameProfilingUploader.Append(eventRecord);
	}

	private static void ResetAllMeasurements()
	{
		LagSpikeProfiler.Reset();
		PacketProfiler.Reset();
		InvokeProfiler.update?.Reset();
		InvokeProfiler.fixedUpdate?.Reset();
		EntityProfiler.Reset();
		WorkQueueProfiler.Reset();
		EntityProfiler.Reset();
		RconProfiler.Reset();
	}

	private static void SerializeCommon(EventRecord record, RconProfilerStats rconStats)
	{
		try
		{
			string hostname = ConVar.Server.hostname;
			PerformanceSamplePoint lastFrame = PerformanceMetrics.LastFrame;
			record.AddField("server_id", ConVar.Server.server_id).AddField("hostname", hostname).AddField("unity_time", UnityEngine.Time.time)
				.AddField("unity_realtime", UnityEngine.Time.realtimeSinceStartup)
				.AddField("garbage_collects", System.GC.CollectionCount(0))
				.AddField("ram_get_total_memory", System.GC.GetTotalMemory(forceFullCollection: false))
				.AddField("players_connected", BasePlayer.activePlayerList.Count)
				.AddField("players_sleeping", BasePlayer.sleepingPlayerList.Count)
				.AddField("connection_count", global::Network.Net.sv.connections.Count)
				.AddField("entity_count", BaseNetworkable.serverEntities.Count)
				.AddField("servermgr_update", ServerMgr_Update)
				.AddField("net_cycle", Net_Cycle)
				.AddField("physics_sync_time", Physics_SyncTransforms)
				.AddField("companion_tick", Companion_Tick)
				.AddField("baseplayer_tick", BasePlayer_ServerCycle)
				.AddField("fixed_update_scripts", lastFrame.FixedUpdate)
				.AddField("update_scripts", lastFrame.Update)
				.AddField("late_update_scripts", lastFrame.LateUpdate)
				.AddField("physics_update", lastFrame.PhysicsUpdate)
				.AddField("rcon_execution_time", RconProfiler.ExecutionTime)
				.AddField("rcon_new_connections", rconStats.NewConnectionCount)
				.AddField("rcon_failed_connections", rconStats.FailedConnectionCount)
				.AddField("rcon_connection_count", rconStats.ConnectionCount)
				.AddField("rcon_message_count", rconStats.MessageCount)
				.AddField("rcon_messages_length", rconStats.MessageLengthSum)
				.AddField("rcon_errors", rconStats.ErrorCount);
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogError("Failed to serialize common data: " + ex.Message);
		}
	}

	private static void SerializeNetworking(EventRecord frameRecord, int frameIndex, DateTime timestamp)
	{
		if (!PacketProfiler.enabled)
		{
			return;
		}
		try
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			for (int i = 0; i < 27; i++)
			{
				int num6 = PacketProfiler.inboundCount[i];
				int num7 = PacketProfiler.inboundBytes[i];
				int num8 = PacketProfiler.outboundCount[i];
				int num9 = PacketProfiler.outboundSum[i];
				int num10 = PacketProfiler.outboundBytes[i];
				num += num6;
				num2 += num7;
				num3 += num8;
				num4 += num9;
				num5 += num10;
				if (num6 > 0 || num8 > 0)
				{
					EventRecord eventRecord = EventRecord.CSV();
					eventRecord.AddField("", frameIndex).AddField("", timestamp).AddField("", PacketProfiler.AnalyticsKeys.MessageType[i])
						.AddField("", num6)
						.AddField("", num7)
						.AddField("", num8)
						.AddField("", num9)
						.AddField("", num10)
						.AddField("", ConVar.Server.server_id);
					packetUploader.Append(eventRecord);
				}
			}
			frameRecord.AddField("inbound_count_total", num);
			frameRecord.AddField("inbound_bytes_total", num2);
			frameRecord.AddField("outbound_count_total", num3);
			frameRecord.AddField("outbound_sum_total", num4);
			frameRecord.AddField("outbound_bytes_total", num5);
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogError("Failed to serialize networking data: " + ex.Message);
		}
	}

	private static void SerializeInvokes(EventRecord record)
	{
		try
		{
			if (InvokeProfiler.update.mode != 0)
			{
				record.AddField("invokes_elapsed_time", InvokeProfiler.update.elapsedTime).AddField("invokes_executed", InvokeProfiler.update.executedCount).AddField("invokes_count", InvokeProfiler.update.tickCount)
					.AddField("invokes_added", InvokeProfiler.update.addCount)
					.AddField("invokes_removed", InvokeProfiler.update.deletedCount);
			}
			if (InvokeProfiler.fixedUpdate.mode != 0)
			{
				record.AddField("invokes_fixed_elapsed_time", InvokeProfiler.fixedUpdate.elapsedTime).AddField("invokes_fixed_executed", InvokeProfiler.fixedUpdate.executedCount).AddField("invokes_fixed_count", InvokeProfiler.fixedUpdate.tickCount)
					.AddField("invokes_fixed_added", InvokeProfiler.fixedUpdate.addCount)
					.AddField("invokes_fixed_removed", InvokeProfiler.fixedUpdate.deletedCount);
			}
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogError("Failed to serialize invoke data: " + ex.Message);
		}
	}

	private static void SerializeInvokeExecutionTime(InvokeProfiler profiler, AzureAnalyticsUploader uploader, int frameIndex, DateTime timestamp, bool reset = true)
	{
		if (profiler.mode < 2)
		{
			return;
		}
		try
		{
			invokeExecutionResetTimer.Restart();
			foreach (InvokeTrackingData trackingData in profiler.trackingDataList)
			{
				if (trackingData.Calls != 0)
				{
					EventRecord eventRecord = EventRecord.CSV();
					eventRecord.AddField("", frameIndex).AddField("", timestamp).AddField("", profiler.Name)
						.AddField("", trackingData.TypeName)
						.AddField("", trackingData.Key.MethodName)
						.AddField("", trackingData.ExecutionTime)
						.AddField("", trackingData.Calls)
						.AddField("", ConVar.Server.server_id);
					uploader.Append(eventRecord);
					if (reset)
					{
						trackingData.Reset();
					}
				}
			}
			invokeExecutionResetTimer.Stop();
			EventRecord eventRecord2 = EventRecord.CSV();
			eventRecord2.AddField("", frameIndex).AddField("", timestamp).AddField("", "Update")
				.AddField("", "RuntimeProfiler")
				.AddField("", "Invoke_Execution_Serialization")
				.AddField("", invokeExecutionResetTimer.Elapsed)
				.AddField("", 1)
				.AddField("", ConVar.Server.server_id);
			uploader.Append(eventRecord2);
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogError("Failed to serialize '" + profiler.Name + "' invoke execution time: " + ex.Message);
		}
	}

	private static void SerializeProfilingSamples(EventRecord record)
	{
		try
		{
			foreach (RustProfilerRecorder recorder2 in recorders)
			{
				string columnName = recorder2.ColumnName;
				ProfilerRecorder recorder = recorder2.Recorder;
				record.AddField(columnName, recorder.LastValue);
			}
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogError("Failed to serialize profiling samples: " + ex.Message);
		}
	}

	private static void SerializeRconEvents(AzureAnalyticsUploader uploader, int frameIndex, DateTime timestamp, RconProfilerStats rconStats)
	{
		foreach (RconConnectionAttempt connectionAttempt in rconStats.ConnectionAttempts)
		{
			EventRecord record = CreatePoint("rcon_connection_attempt", frameIndex, timestamp).AddField("ip", connectionAttempt.IP).AddField("port", connectionAttempt.Port).AddField("connection_id", connectionAttempt.ConnectionId)
				.AddField("password", connectionAttempt.PasswordAttempt)
				.AddField("success", connectionAttempt.Success);
			uploader.Append(record);
		}
		foreach (RconMessageStats message in rconStats.Messages)
		{
			EventRecord record2 = CreatePoint("rcon_message", frameIndex, timestamp).AddField("ip", message.IP).AddField("port", message.Port).AddField("connection_id", message.ConnectionId)
				.AddField("message", message.Message)
				.AddField("message_length", message.MessageLength);
			uploader.Append(record2);
		}
		foreach (RconDisconnects disconnect in rconStats.Disconnects)
		{
			EventRecord record3 = CreatePoint("rcon_disconnect", frameIndex, timestamp).AddField("ip", disconnect.IP).AddField("port", disconnect.Port).AddField("connection_id", disconnect.ConnectionId);
			uploader.Append(record3);
		}
	}

	private static void SerializeMemoryPool(AzureAnalyticsUploader uploader, int frameIndex, DateTime timestamp)
	{
		if (runtime_profiling_pooling == 0 || !(timestamp > nextPoolFlush))
		{
			return;
		}
		nextPoolFlush = timestamp.AddSeconds(runtime_profiling_pool_flush_interval);
		foreach (KeyValuePair<Type, Pool.IPoolCollection> item in Pool.Directory)
		{
			Pool.IPoolCollection value = item.Value;
			EventRecord record = CreatePoint("pool_facepunch", frameIndex, timestamp).AddField("type_name", item.Key.Name).AddField("capacity", value.ItemsCapacity).AddField("stack", value.ItemsInStack)
				.AddField("used", value.ItemsInUse)
				.AddField("created", value.ItemsCreated)
				.AddField("taken", value.ItemsTaken)
				.AddField("spilled", value.ItemsSpilled)
				.AddField("max_used", value.MaxItemsInUse);
			uploader.Append(record);
		}
		ArrayPool<byte> arrayPool = BaseNetwork.ArrayPool;
		ConcurrentQueue<byte[]>[] buffer = arrayPool.GetBuffer();
		for (int i = 0; i < buffer.Length; i++)
		{
			ConcurrentQueue<byte[]> concurrentQueue = buffer[i];
			EventRecord record2 = CreatePoint("pool_networking", frameIndex, timestamp).AddField("size", arrayPool.SizeToIndex(i)).AddField("amount", concurrentQueue.Count);
			uploader.Append(record2);
		}
	}

	private static EventRecord CreatePoint(string type, int frameIndex, DateTime timestamp)
	{
		return EventRecord.New(type).AddField("frame_index", frameIndex).SetTimestamp(timestamp)
			.AddField("server_id", ConVar.Server.server_id);
	}
}
