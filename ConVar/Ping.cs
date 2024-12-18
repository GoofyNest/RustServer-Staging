using Facepunch.Ping;

namespace ConVar;

[Factory("ping")]
public class Ping : ConsoleSystem
{
	[ServerVar]
	[ClientVar]
	public static int ping_samples
	{
		get
		{
			return PingEstimater.numSamples;
		}
		set
		{
			PingEstimater.numSamples = value;
		}
	}

	[ServerVar]
	[ClientVar]
	public static bool ping_parallel
	{
		get
		{
			return PingEstimater.parallel;
		}
		set
		{
			PingEstimater.parallel = value;
		}
	}

	[ServerVar]
	[ClientVar]
	public static int ping_refresh_interval
	{
		get
		{
			return PingEstimater.refreshIntervalMinutes;
		}
		set
		{
			PingEstimater.refreshIntervalMinutes = value;
		}
	}

	[ServerVar]
	[ClientVar]
	public static bool auto_refresh_region
	{
		get
		{
			return PingEstimater.AutoRefresh;
		}
		set
		{
			PingEstimater.AutoRefresh = value;
		}
	}

	[ServerVar]
	[ClientVar]
	public static bool ping_estimate_logging
	{
		get
		{
			return PingEstimater.logging;
		}
		set
		{
			PingEstimater.logging = value;
		}
	}

	[ServerVar]
	[ClientVar]
	public static bool ping_estimation
	{
		get
		{
			return PingEstimater.enabled;
		}
		set
		{
			PingEstimater.enabled = value;
		}
	}
}
