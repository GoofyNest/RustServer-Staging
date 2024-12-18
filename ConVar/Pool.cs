using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Facepunch;
using Facepunch.Extend;
using Network;
using UnityEngine;

namespace ConVar;

[Factory("pool")]
public class Pool : ConsoleSystem
{
	[ServerVar]
	[ClientVar]
	public static int mode = 2;

	[ServerVar]
	[ClientVar]
	public static bool prewarm = true;

	[ServerVar]
	[ClientVar]
	public static bool enabled = true;

	[ServerVar]
	[ClientVar]
	public static bool debug = false;

	[ServerVar]
	[ClientVar]
	public static void print_memory(Arg arg)
	{
		if (Facepunch.Pool.Directory.Count == 0)
		{
			arg.ReplyWith("Memory pool is empty.");
			return;
		}
		bool flag = arg.HasArg("--raw", remove: true);
		bool flag2 = arg.HasArg("--json", remove: true);
		string @string = arg.GetString(0, null);
		TextTable textTable = new TextTable();
		textTable.AddColumn("type");
		textTable.AddColumn("capacity");
		textTable.AddColumn("pooled");
		textTable.AddColumn("active");
		textTable.AddColumn("max");
		textTable.AddColumn("hits");
		textTable.AddColumn("misses");
		textTable.AddColumn("spills");
		foreach (KeyValuePair<Type, Facepunch.Pool.IPoolCollection> item in Facepunch.Pool.Directory.OrderByDescending((KeyValuePair<Type, Facepunch.Pool.IPoolCollection> x) => x.Value.ItemsCreated))
		{
			Type key = item.Key;
			Facepunch.Pool.IPoolCollection value = item.Value;
			if (@string == null || key.ToString().Contains(@string))
			{
				textTable.AddRow(key.ToString().Replace("System.Collections.Generic.", ""), flag ? value.ItemsCapacity.ToString() : value.ItemsCapacity.FormatNumberShort(), flag ? value.ItemsInStack.ToString() : value.ItemsInStack.FormatNumberShort(), flag ? value.ItemsInUse.ToString() : value.ItemsInUse.FormatNumberShort(), flag ? value.MaxItemsInUse.ToString() : value.MaxItemsInUse.FormatNumberShort(), flag ? value.ItemsTaken.ToString() : value.ItemsTaken.FormatNumberShort(), flag ? value.ItemsCreated.ToString() : value.ItemsCreated.FormatNumberShort(), flag ? value.ItemsSpilled.ToString() : value.ItemsSpilled.FormatNumberShort());
			}
		}
		arg.ReplyWith(flag2 ? textTable.ToJson() : textTable.ToString());
	}

	[ServerVar]
	[ClientVar]
	public static void reset_max_pool_counter(Arg arg)
	{
		if (Facepunch.Pool.Directory.Count == 0)
		{
			arg.ReplyWith("Memory pool is empty.");
			return;
		}
		foreach (Facepunch.Pool.IPoolCollection value in Facepunch.Pool.Directory.Values)
		{
			value.ResetMaxUsageCounter();
		}
		arg.ReplyWith("Reset max item counter of pool");
	}

	[ServerVar]
	[ClientVar]
	public static void print_arraypool(Arg arg)
	{
		ArrayPool<byte> arrayPool = BaseNetwork.ArrayPool;
		ConcurrentQueue<byte[]>[] buffer = arrayPool.GetBuffer();
		TextTable textTable = new TextTable();
		textTable.AddColumn("index");
		textTable.AddColumn("size");
		textTable.AddColumn("bytes");
		textTable.AddColumn("count");
		textTable.AddColumn("memory");
		for (int i = 0; i < buffer.Length; i++)
		{
			int num = arrayPool.IndexToSize(i);
			int count = buffer[i].Count;
			int input = num * count;
			textTable.AddRow(i.ToString(), num.ToString(), num.FormatBytes(), count.ToString(), input.FormatBytes());
		}
		arg.ReplyWith(arg.HasArg("--json") ? textTable.ToJson() : textTable.ToString());
	}

	[ServerVar]
	[ClientVar]
	public static void print_prefabs(Arg arg)
	{
		PrefabPoolCollection pool = GameManager.server.pool;
		if (pool.storage.Count == 0)
		{
			arg.ReplyWith("Prefab pool is empty.");
			return;
		}
		string @string = arg.GetString(0, string.Empty);
		TextTable textTable = new TextTable();
		textTable.AddColumn("id");
		textTable.AddColumn("name");
		textTable.AddColumn("missed");
		textTable.AddColumn("count");
		textTable.AddColumn("target");
		textTable.AddColumn("added");
		textTable.AddColumn("removed");
		foreach (PrefabPool item in pool.storage.Values.OrderByDescending((PrefabPool x) => x.Missed))
		{
			string text = StringPool.Get(item.PrefabName).ToString();
			string prefabName = item.PrefabName;
			string text2 = item.Count.ToString();
			if (string.IsNullOrEmpty(@string) || prefabName.Contains(@string, CompareOptions.IgnoreCase))
			{
				textTable.AddRow(text, Path.GetFileNameWithoutExtension(prefabName), text2, item.TargetCapacity.ToString(), item.Missed.ToString(), item.Pushed.ToString(), item.Popped.ToString());
			}
		}
		arg.ReplyWith(arg.HasArg("--json") ? textTable.ToJson() : textTable.ToString());
	}

	[ServerVar]
	[ClientVar]
	public static void print_assets(Arg arg)
	{
		if (AssetPool.storage.Count == 0)
		{
			arg.ReplyWith("Asset pool is empty.");
			return;
		}
		string @string = arg.GetString(0, string.Empty);
		TextTable textTable = new TextTable();
		textTable.AddColumn("type");
		textTable.AddColumn("allocated");
		textTable.AddColumn("available");
		foreach (KeyValuePair<Type, AssetPool.Pool> item in AssetPool.storage)
		{
			string text = item.Key.ToString();
			string text2 = item.Value.allocated.ToString();
			string text3 = item.Value.available.ToString();
			if (string.IsNullOrEmpty(@string) || text.Contains(@string, CompareOptions.IgnoreCase))
			{
				textTable.AddRow(text, text2, text3);
			}
		}
		arg.ReplyWith(arg.HasArg("--json") ? textTable.ToJson() : textTable.ToString());
	}

	[ServerVar]
	[ClientVar]
	public static void clear_memory(Arg arg)
	{
		Facepunch.Pool.Clear(arg.GetString(0, string.Empty));
	}

	[ServerVar]
	[ClientVar]
	public static void clear_prefabs(Arg arg)
	{
		string @string = arg.GetString(0, string.Empty);
		GameManager.server.pool.Clear(@string);
	}

	[ServerVar]
	[ClientVar]
	public static void clear_assets(Arg arg)
	{
		AssetPool.Clear(arg.GetString(0, string.Empty));
	}

	[ServerVar]
	[ClientVar]
	public static void export_prefabs(Arg arg)
	{
		PrefabPoolCollection pool = GameManager.server.pool;
		if (pool.storage.Count == 0)
		{
			arg.ReplyWith("Prefab pool is empty.");
			return;
		}
		string @string = arg.GetString(0, string.Empty);
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyValuePair<uint, PrefabPool> item in pool.storage)
		{
			string arg2 = item.Key.ToString();
			string text = StringPool.Get(item.Key);
			string arg3 = item.Value.Count.ToString();
			if (string.IsNullOrEmpty(@string) || text.Contains(@string, CompareOptions.IgnoreCase))
			{
				stringBuilder.AppendLine($"{arg2},{Path.GetFileNameWithoutExtension(text)},{arg3}");
			}
		}
		File.WriteAllText("prefabs.csv", stringBuilder.ToString());
	}

	[ServerVar]
	[ClientVar]
	public static void fill_prefabs(Arg arg)
	{
		string @string = arg.GetString(0, string.Empty);
		int @int = arg.GetInt(1);
		PrefabPoolWarmup.Run(@string, @int);
	}
}
