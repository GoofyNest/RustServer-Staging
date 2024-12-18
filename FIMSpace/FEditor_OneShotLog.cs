using System;
using System.Diagnostics;
using UnityEngine;

namespace FIMSpace;

public static class FEditor_OneShotLog
{
	public static bool CanDrawLog(string id, int delayToNextCallInSeconds = int.MaxValue, int callLimitBeforeTimeMove = 1, int logSeparation = 0)
	{
		int id2 = Process.GetCurrentProcess().Id;
		if (PlayerPrefs.GetInt(id + "s", 0) != id2)
		{
			PlayerPrefs.SetInt(id + "s", id2);
			PlayerPrefs.SetString(id + "acc", DateTime.Now.ToBinary().ToString());
			PlayerPrefs.SetInt(id + "counter", 0);
			PlayerPrefs.SetInt(id + "sep", logSeparation);
			if (delayToNextCallInSeconds == int.MaxValue)
			{
				return true;
			}
		}
		else if (delayToNextCallInSeconds == int.MaxValue)
		{
			return false;
		}
		string @string = PlayerPrefs.GetString(id + "acc");
		int num = PlayerPrefs.GetInt(id + "counter");
		int @int = PlayerPrefs.GetInt(id + "sep");
		if (long.TryParse(@string, out var result))
		{
			DateTime value = DateTime.FromBinary(result);
			if (DateTime.Now.Subtract(value).TotalSeconds > (double)delayToNextCallInSeconds)
			{
				PlayerPrefs.SetInt(id + "counter", 0);
				num = 0;
				PlayerPrefs.SetString(id + "acc", DateTime.Now.ToBinary().ToString());
			}
			@int++;
			PlayerPrefs.SetInt(id + "sep", @int);
			if (@int >= logSeparation)
			{
				@int = 0;
				PlayerPrefs.SetInt(id + "sep", @int);
				num++;
				PlayerPrefs.SetInt(id + "counter", num);
				if (num - 1 < callLimitBeforeTimeMove)
				{
					return true;
				}
			}
			return false;
		}
		return false;
	}

	public static bool EditorCanDrawLog(string id, int delayToNextCallInSeconds = int.MaxValue, int callLimitBeforeTimeMove = 1, int logSeparation = 0)
	{
		return false;
	}
}
