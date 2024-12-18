using System.IO;
using System.Threading.Tasks;
using Network;
using ProtoBuf;
using UnityEngine;

namespace ConVar;

[Factory("demo")]
public class Demo : ConsoleSystem
{
	public class Header : DemoHeader, IDemoHeader
	{
		long IDemoHeader.Length
		{
			get
			{
				return length;
			}
			set
			{
				length = value;
			}
		}

		public void Write(BinaryWriter writer)
		{
			byte[] array = ToProtoBytes();
			writer.Write("RUST DEMO FORMAT");
			writer.Write(array.Length);
			writer.Write(array);
			writer.Write('\0');
		}
	}

	public static uint Version = 3u;

	[ServerVar]
	public static float splitseconds = 3600f;

	[ServerVar]
	public static float splitmegabytes = 200f;

	[ServerVar(Saved = true)]
	public static string recordlist = "";

	private static int _recordListModeValue = 0;

	[ServerVar(Name = "upload_demos", Saved = true)]
	public static bool UploadDemos
	{
		get
		{
			return DemoConVars.UploadDemos;
		}
		set
		{
			DemoConVars.UploadDemos = value;
		}
	}

	[ServerVar(Name = "upload_url", Saved = true)]
	public static string UploadUrl
	{
		get
		{
			return DemoConVars.UploadEndpoint;
		}
		set
		{
			DemoConVars.UploadEndpoint = value;
		}
	}

	[ServerVar(Name = "full_server_demo", Saved = true)]
	public static bool ServerDemosEnabled
	{
		get
		{
			return DemoConVars.ServerDemosEnabled;
		}
		set
		{
			DemoConVars.EnableServerDemos(value);
		}
	}

	[ServerVar(Name = "server_flush_seconds", Saved = true)]
	public static int ServerDemoFlushInterval
	{
		get
		{
			return DemoConVars.ServerDemoFlushIntervalSeconds;
		}
		set
		{
			DemoConVars.ServerDemoFlushIntervalSeconds = Mathf.Clamp(value, 60, 1800);
		}
	}

	[ServerVar(Name = "upload_bandwidth_limit_ratio")]
	public static float UploadBandwidthLimitRatio
	{
		get
		{
			return DemoConVars.BandwidthLimitRatio;
		}
		set
		{
			DemoConVars.BandwidthLimitRatio = value;
		}
	}

	[ServerVar(Name = "server_demo_directory", Help = "Directory to save full server demos")]
	public static string ServerDemoDirectory
	{
		get
		{
			return DemoConVars.ServerDemoDirectory;
		}
		set
		{
			DemoConVars.ServerDemoDirectory = value;
		}
	}

	[ServerVar(Name = "delete_after_upload", Saved = true, Help = "Should the full server demos be deleted after they are uploaded")]
	public static bool DeleteDemoAfterUpload
	{
		get
		{
			return DemoConVars.DeleteDemoAfterUpload;
		}
		set
		{
			DemoConVars.DeleteDemoAfterUpload = value;
		}
	}

	[ServerVar(Name = "zip_demos", Saved = true, Help = "Should we be zipping the demos before we upload them")]
	public static bool ZipServerDemos
	{
		get
		{
			return DemoConVars.ZipServerDemos;
		}
		set
		{
			DemoConVars.ZipServerDemos = value;
		}
	}

	[ServerVar(Name = "server_demo_disk_space_gb", Saved = true, Help = "How much disk space full server demos can take before we start to delete them")]
	public static int MaxDemoDiskSpaceGB
	{
		get
		{
			return DemoConVars.MaxDemoDiskSpaceGB;
		}
		set
		{
			DemoConVars.MaxDemoDiskSpaceGB = Mathf.Max(value, 0);
		}
	}

	[ServerVar(Name = "server_demo_cleanup_interval", Saved = true, Help = "How many minutes between cleaning up demos from the disk")]
	public static int DemoDiskCleanupIntervalMinutes
	{
		get
		{
			return DemoConVars.DiskCleanupIntervalMinutes;
		}
		set
		{
			DemoConVars.DiskCleanupIntervalMinutes = Mathf.Max(value, 1);
		}
	}

	[ServerVar(Name = "max_upload_concurrency", Help = "Max parallel requests when uploading demos")]
	public static int DemoUploadConcurrency
	{
		get
		{
			return DemoConVars.MaxUploadConcurrency;
		}
		set
		{
			DemoConVars.MaxUploadConcurrency = Mathf.Max(value, 1);
		}
	}

	[ServerVar(Saved = true, Help = "Controls the behavior of recordlist, 0=whitelist, 1=blacklist")]
	public static int recordlistmode
	{
		get
		{
			return _recordListModeValue;
		}
		set
		{
			_recordListModeValue = Mathf.Clamp(value, 0, 1);
		}
	}

	[ServerVar(Name = "benchmark_demo_upload")]
	public static void BenchmarkDemoUpload(Arg arg)
	{
		Task.Run(delegate
		{
			Network.Net.sv.serverDemos.BenchmarkDemoUpload(arg.GetInt(0, 4), arg.GetString(2), arg.GetInt(1));
		});
	}

	[ServerVar]
	public static string record(Arg arg)
	{
		BasePlayer playerOrSleeper = arg.GetPlayerOrSleeper(0);
		if (!playerOrSleeper || playerOrSleeper.net == null || playerOrSleeper.net.connection == null)
		{
			return "Player not found";
		}
		if (playerOrSleeper.net.connection.IsRecording)
		{
			return "Player already recording a demo";
		}
		playerOrSleeper.StartDemoRecording();
		return null;
	}

	[ServerVar]
	public static string stop(Arg arg)
	{
		BasePlayer playerOrSleeper = arg.GetPlayerOrSleeper(0);
		if (!playerOrSleeper || playerOrSleeper.net == null || playerOrSleeper.net.connection == null)
		{
			return "Player not found";
		}
		if (!playerOrSleeper.net.connection.IsRecording)
		{
			return "Player not recording a demo";
		}
		playerOrSleeper.StopDemoRecording();
		return null;
	}
}
