using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Facepunch.Extend;

namespace ConVar;

[Factory("profile")]
public class Profile : ConsoleSystem
{
	private static Action delayedTakeSnapshot;

	private static bool exportDone = true;

	private const string PerfSnapshotHelp = "profile.perfsnapshot [delay=15, int] [name='Profile', str, no extension, max 32chars] [frames=10, int, max 10].\nWill produce a JSON perf snapshot that can be viewed in Perfetto or similar tools";

	private static void NeedProfileFolder()
	{
		if (!Directory.Exists("profile"))
		{
			Directory.CreateDirectory("profile");
		}
	}

	[ClientVar]
	[ServerVar]
	public static void start(Arg arg)
	{
	}

	[ServerVar]
	[ClientVar]
	public static void stop(Arg arg)
	{
	}

	[ServerVar]
	[ClientVar]
	public static void flush_analytics(Arg arg)
	{
	}

	[ServerVar(Help = "profile.perfsnapshot [delay=15, int] [name='Profile', str, no extension, max 32chars] [frames=10, int, max 10].\nWill produce a JSON perf snapshot that can be viewed in Perfetto or similar tools")]
	public static void PerfSnapshot(Arg arg)
	{
		if (!ServerProfiler.IsEnabled())
		{
			arg.ReplyWith("ServerProfiler is disabled");
			return;
		}
		if (!exportDone)
		{
			arg.ReplyWith("Already taking snapshot!");
			return;
		}
		int delay = arg.GetInt(0, 15);
		string name = arg.GetString(1, "Profile").Truncate(32);
		int frames = arg.GetInt(2, 10);
		if (delay == 0)
		{
			Chat.Broadcast("Server taking a perf snapshot", "SERVER", "#eee", 0uL);
			ServerProfiler.RecordNextFrames(frames, delegate(IList<ServerProfiler.Profile> profiles)
			{
				Chat.Broadcast("Snapshot taken", "SERVER", "#eee", 0uL);
				Task.Run(delegate
				{
					ProfileExporter.JSON.Export(name, profiles);
					ServerProfiler.ReleaseResources();
					exportDone = true;
				});
			});
			return;
		}
		Chat.Broadcast($"Server will be taking a perf snapshot, expect stutters in {delay} seconds", "SERVER", "#eee", 0uL);
		delayedTakeSnapshot = delegate
		{
			delay--;
			if (delay > 10 && delay % 5 == 0)
			{
				Chat.Broadcast($"Server will be taking a perf snapshot, expect stutters in {delay} seconds", "SERVER", "#eee", 0uL);
			}
			else if (delay > 0 && delay <= 10)
			{
				Chat.Broadcast($"{delay}...", "SERVER", "#eee", 0uL);
			}
			if (delay == 0)
			{
				ServerProfiler.RecordNextFrames(frames, delegate(IList<ServerProfiler.Profile> profiles)
				{
					Chat.Broadcast("Snapshot taken", "SERVER", "#eee", 0uL);
					Task.Run(delegate
					{
						ProfileExporter.JSON.Export(name, profiles);
						ServerProfiler.ReleaseResources();
						exportDone = true;
					});
				});
				InvokeHandler.CancelInvoke(SingletonComponent<InvokeHandler>.Instance, delayedTakeSnapshot);
				delayedTakeSnapshot = null;
			}
		};
		InvokeHandler.InvokeRepeating(SingletonComponent<InvokeHandler>.Instance, delayedTakeSnapshot, 0f, 1f);
	}
}
