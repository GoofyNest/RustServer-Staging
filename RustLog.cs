using System;
using System.Runtime.CompilerServices;
using UnityEngine;

[Factory("log")]
public class RustLog : ConsoleSystem
{
	public enum EntryType
	{
		General,
		Network,
		Hierarchy,
		Serialization,
		Combat,
		Item,
		Audio
	}

	private static readonly string[] names = Enum.GetNames(typeof(EntryType));

	public static readonly int[] Levels = new int[names.Length];

	[ServerVar]
	[ClientVar]
	public static void Level(Arg args)
	{
		if (args.Args.Length == 0 || args.Args.Length > 2)
		{
			return;
		}
		string @string = args.GetString(0);
		if (string.IsNullOrEmpty(@string))
		{
			return;
		}
		for (int i = 0; i < names.Length; i++)
		{
			string text = names[i];
			if (!(@string != text))
			{
				if (args.Args.Length == 2)
				{
					Levels[i] = args.GetInt(1, Levels[i]);
				}
				else
				{
					Debug.Log(GetLevel((EntryType)i));
				}
				break;
			}
		}
	}

	public static int GetLevel(EntryType type)
	{
		return Levels[(int)type];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Log(EntryType type, int level, GameObject gameObject, string msg)
	{
		if (GetLevel(type) >= level)
		{
			LogImpl(type, msg, gameObject);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Log<T1>(EntryType type, int level, GameObject gameObject, string msgFormat, T1 arg1)
	{
		if (GetLevel(type) >= level)
		{
			string msg = string.Format(msgFormat, arg1);
			LogImpl(type, msg, gameObject);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Log<T1, T2>(EntryType type, int level, GameObject gameObject, string msgFormat, T1 arg1, T2 arg2)
	{
		if (GetLevel(type) >= level)
		{
			string msg = string.Format(msgFormat, arg1, arg2);
			LogImpl(type, msg, gameObject);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Log<T1, T2, T3>(EntryType type, int level, GameObject gameObject, string msgFormat, T1 arg1, T2 arg2, T3 arg3)
	{
		if (GetLevel(type) >= level)
		{
			string msg = string.Format(msgFormat, arg1, arg2, arg3);
			LogImpl(type, msg, gameObject);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Log<T1, T2, T3, T4>(EntryType type, int level, GameObject gameObject, string msgFormat, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
	{
		if (GetLevel(type) >= level)
		{
			string msg = string.Format(msgFormat, arg1, arg2, arg3, arg4);
			LogImpl(type, msg, gameObject);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Log<T1, T2, T3, T4, T5>(EntryType type, int level, GameObject gameObject, string msgFormat, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
	{
		if (GetLevel(type) >= level)
		{
			string msg = string.Format(msgFormat, arg1, arg2, arg3, arg4, arg5);
			LogImpl(type, msg, gameObject);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void LogImpl(EntryType type, string msg, GameObject gameObject)
	{
		Debug.Log($"<color=white>[{type}]</color> {msg}", gameObject);
	}
}
