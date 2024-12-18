using System;
using UnityEngine;

namespace ConVar;

[Factory("env")]
public class Env : ConsoleSystem
{
	[ClientVar(Default = "1")]
	public static bool nightlight_enabled = true;

	[ReplicatedVar(Default = "0")]
	public static bool redMoon = false;

	[ClientVar(Default = "0", Help = "Toggles nightlight screen effect when using debug camera")]
	public static bool nightlight_debugcamera_enabled = true;

	private static float nightlight_distance_internal = 7f;

	private static float nightlight_fadefraction_internal = 0.65f;

	private static float nightlight_brightness_internal = 0.0175f;

	[ServerVar]
	public static bool progresstime
	{
		get
		{
			if (TOD_Sky.Instance == null)
			{
				return false;
			}
			return TOD_Sky.Instance.Components.Time.ProgressTime;
		}
		set
		{
			if (!(TOD_Sky.Instance == null))
			{
				TOD_Sky.Instance.Components.Time.ProgressTime = value;
			}
		}
	}

	[ServerVar(ShowInAdminUI = true)]
	public static float time
	{
		get
		{
			if (TOD_Sky.Instance == null)
			{
				return 0f;
			}
			return TOD_Sky.Instance.Cycle.Hour;
		}
		set
		{
			if (!(TOD_Sky.Instance == null))
			{
				TOD_Sky.Instance.Cycle.Hour = value;
			}
		}
	}

	[ServerVar]
	public static int day
	{
		get
		{
			if (TOD_Sky.Instance == null)
			{
				return 0;
			}
			return TOD_Sky.Instance.Cycle.Day;
		}
		set
		{
			if (!(TOD_Sky.Instance == null))
			{
				TOD_Sky.Instance.Cycle.Day = value;
			}
		}
	}

	[ServerVar]
	public static int month
	{
		get
		{
			if (TOD_Sky.Instance == null)
			{
				return 0;
			}
			return TOD_Sky.Instance.Cycle.Month;
		}
		set
		{
			if (!(TOD_Sky.Instance == null))
			{
				TOD_Sky.Instance.Cycle.Month = value;
			}
		}
	}

	[ServerVar]
	public static int year
	{
		get
		{
			if (TOD_Sky.Instance == null)
			{
				return 0;
			}
			return TOD_Sky.Instance.Cycle.Year;
		}
		set
		{
			if (!(TOD_Sky.Instance == null))
			{
				TOD_Sky.Instance.Cycle.Year = value;
			}
		}
	}

	[ReplicatedVar(Default = "0")]
	public static float oceanlevel
	{
		get
		{
			return WaterSystem.OceanLevel;
		}
		set
		{
			WaterSystem.OceanLevel = value;
		}
	}

	[ReplicatedVar(Default = "7")]
	public static float nightlight_distance
	{
		get
		{
			return nightlight_distance_internal;
		}
		set
		{
			value = Mathf.Clamp(value, 0f, 25f);
			nightlight_distance_internal = value;
		}
	}

	[ReplicatedVar(Default = "0.65")]
	public static float nightlight_fadefraction
	{
		get
		{
			return nightlight_fadefraction_internal;
		}
		set
		{
			nightlight_fadefraction_internal = value;
		}
	}

	[ReplicatedVar(Default = "0.0175")]
	public static float nightlight_brightness
	{
		get
		{
			return nightlight_brightness_internal;
		}
		set
		{
			value = Mathf.Clamp(value, 0f, 0.2f);
			nightlight_brightness_internal = value;
		}
	}

	[ServerVar]
	public static void addtime(Arg arg)
	{
		if (!(TOD_Sky.Instance == null))
		{
			DateTime dateTime = TOD_Sky.Instance.Cycle.DateTime.AddTicks(arg.GetTicks(0, 0L));
			TOD_Sky.Instance.Cycle.DateTime = dateTime;
		}
	}
}
