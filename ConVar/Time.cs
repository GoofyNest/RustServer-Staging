using UnityEngine;

namespace ConVar;

[Factory("time")]
public class Time : ConsoleSystem
{
	public const int SERVER_DEFAULT_TICKS = 16;

	public const int CLIENT_DEFAULT_TICKS = 32;

	public const string CLIENT_DEFAULT_TICKS_STR = "32";

	[ServerVar]
	[Help("Pause time while loading")]
	public static bool pausewhileloading = true;

	private static int _cl_steps = 32;

	private static int _cl_maxstepsperframe = 2;

	[ServerVar(Help = "Desired physics ticks per second on the server")]
	public static int sv_steps
	{
		get
		{
			return Mathf.RoundToInt(1f / UnityEngine.Time.fixedDeltaTime);
		}
		set
		{
			value = Mathf.Clamp(value, 16, 64);
			int num = sv_maxstepsperframe;
			UnityEngine.Time.fixedDeltaTime = 1f / (float)value;
			sv_maxstepsperframe = num;
		}
	}

	[ServerVar(Help = "The maximum amount physics ticks per frame on the server. If things are taking too long, time slows down")]
	public static int sv_maxstepsperframe
	{
		get
		{
			return Mathf.RoundToInt(UnityEngine.Time.maximumDeltaTime / UnityEngine.Time.fixedDeltaTime);
		}
		set
		{
			value = Mathf.Clamp(value, 2, 10);
			UnityEngine.Time.maximumDeltaTime = (float)value * UnityEngine.Time.fixedDeltaTime;
		}
	}

	[ServerVar]
	[Help("The time scale")]
	public static float timescale
	{
		get
		{
			return UnityEngine.Time.timeScale;
		}
		set
		{
			UnityEngine.Time.timeScale = value;
		}
	}

	[ReplicatedVar(Help = "Desired physics ticks per second on clients", Default = "32")]
	public static int cl_steps
	{
		get
		{
			return _cl_steps;
		}
		set
		{
			value = Mathf.Clamp(value, 32, 64);
			_cl_steps = value;
			cl_maxstepsperframe = _cl_maxstepsperframe;
		}
	}

	[ReplicatedVar(Help = "The maximum amount physics ticks per frame on clients. If things are taking too long, time slows down", Default = "2")]
	public static int cl_maxstepsperframe
	{
		get
		{
			return _cl_maxstepsperframe;
		}
		set
		{
			value = Mathf.Clamp(value, 2, 10);
			_cl_maxstepsperframe = value;
		}
	}
}
