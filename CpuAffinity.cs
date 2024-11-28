using System;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Facepunch.Models;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class CpuAffinity
{
	private static bool _appliedAutoCpuAffinity;

	public static void Apply()
	{
	}

	private static void ApplyImpl(Facepunch.Models.Manifest manifest)
	{
		try
		{
			string cpuModel = SystemInfo.processorType;
			(string, int, int) tuple = ReadManifestCoreRanges(manifest).FirstOrDefault(((string Cpu, int Min, int Max) r) => cpuModel.Contains(r.Cpu, StringComparison.OrdinalIgnoreCase));
			if (tuple.Item2 >= 0 && tuple.Item3 > tuple.Item2)
			{
				ulong num = 0uL;
				for (int i = tuple.Item2; i <= tuple.Item3; i++)
				{
					num |= (ulong)(1L << i);
				}
				if (SystemCommands.SetCpuAffinity(num))
				{
					Debug.Log($"Automatically set CPU affinity to cores {tuple.Item2}-{tuple.Item3} ({tuple.Item1})");
				}
			}
		}
		catch (Exception arg)
		{
			Debug.LogWarning($"Failed to auto set CPU affinity: {arg}");
		}
	}

	private static List<(string Cpu, int Min, int Max)> ReadManifestCoreRanges(Facepunch.Models.Manifest manifest)
	{
		List<(string, int, int)> list = new List<(string, int, int)>();
		if (!((manifest?.Metadata)?["PreferredCoreRanges"] is JArray { Count: >0 } jArray))
		{
			return list;
		}
		foreach (JToken item in jArray)
		{
			if (item is JObject jObject)
			{
				string text = jObject["Cpu"]?.Value<string>();
				int? num = jObject["Min"]?.Value<int>();
				int? num2 = jObject["Max"]?.Value<int>();
				if (text != null && num.HasValue && num2.HasValue)
				{
					list.Add((text, num.Value, num2.Value));
				}
			}
		}
		return list;
	}
}
