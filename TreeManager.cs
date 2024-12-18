#define UNITY_ASSERTIONS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ConVar;
using Facepunch;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

public class TreeManager : BaseEntity
{
	private struct ToProcess
	{
		public struct Telemetry
		{
			public TimeSpan InitialTime;

			public TimeSpan IterativeTime;

			public int FramesToComplete;

			public void Report(BasePlayer player)
			{
				TimeSpan timeSpan = InitialTime + IterativeTime;
				TimeSpan timeSpan2 = new TimeSpan((long)(PlayerBudgetMS * 10000f * (float)FramesToComplete));
				TimeSpan timeSpan3 = timeSpan - timeSpan2;
				TimeSpan timeSpan4 = timeSpan / FramesToComplete;
				RustLog.Log(RustLog.EntryType.Network, 1, player.gameObject, "TreeManager: Initial: {0}ms, Iterative: {1}ms, Total: {2}ms({3}ms/frame), Overspent: {4}ms", InitialTime.TotalMilliseconds, IterativeTime.TotalMilliseconds, timeSpan.TotalMilliseconds, timeSpan4.TotalMilliseconds, timeSpan3.TotalMilliseconds);
			}
		}

		public BasePlayer Player;

		public BitArray SentCells;

		public int Left;

		public int Range;

		public int OldCellIndex;

		public int LastProcessedIndex;

		public Telemetry Stats;
	}

	private struct TreeCell
	{
		public TreeList TreeList;

		public MemoryStream SerializedCell;

		public bool IsDirty;
	}

	public static ListHashSet<BaseEntity> entities = new ListHashSet<BaseEntity>();

	public static TreeManager server;

	[ServerVar]
	public static bool EnableTreeStreaming = true;

	[ServerVar]
	public static float PlayerBudgetMS = 0.01f;

	[ServerVar]
	public static float UpdateBudgetMS = 1f;

	private const string CellSizeHelp = "Define cell size(in m) of a grid for trees  - only has effect on world load and must be > 1. This affects how much data we send per tree cell(bigger the cell - more trees we have to send). The smaller the cell, the more cells we have to process and the more memory we need per player to track what's left to send(gridSize ^ 2 / 8 bytes). We readjust CellSize to ensure gridSize never exceeds 512.";

	[ServerVar(Help = "Define cell size(in m) of a grid for trees  - only has effect on world load and must be > 1. This affects how much data we send per tree cell(bigger the cell - more trees we have to send). The smaller the cell, the more cells we have to process and the more memory we need per player to track what's left to send(gridSize ^ 2 / 8 bytes). We readjust CellSize to ensure gridSize never exceeds 512.")]
	public static int CellSize = 100;

	private const string UseLazySerializationHelp = "Instead of reserializing grid cell on every tree add/removal(which can cost 0.25ms on 4.5k world), defer it to the streaming update. This reduces amount of times we need to serialize the tree list, but causes the player queue to take longer to process, as that's where evaluation happens.";

	[ServerVar(Help = "Instead of reserializing grid cell on every tree add/removal(which can cost 0.25ms on 4.5k world), defer it to the streaming update. This reduces amount of times we need to serialize the tree list, but causes the player queue to take longer to process, as that's where evaluation happens.")]
	public static bool UseLazySerialization = true;

	private List<ToProcess> playersToProcess = new List<ToProcess>(100);

	private int gridSize = 64;

	private List<TreeCell> treesGrid;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("TreeManager.OnRpcMessage"))
		{
			if (rpc == 1907121457 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					UnityEngine.Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SERVER_RequestTrees ");
				}
				using (TimeWarning.New("SERVER_RequestTrees"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(1907121457u, "SERVER_RequestTrees", this, player, 0uL))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg2 = rPCMessage;
							SERVER_RequestTrees(msg2);
						}
					}
					catch (Exception exception)
					{
						UnityEngine.Debug.LogException(exception);
						player.Kick("RPC Error in SERVER_RequestTrees");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public static Vector3 ProtoHalf3ToVec3(ProtoBuf.Half3 half3)
	{
		Vector3 result = default(Vector3);
		result.x = Mathf.HalfToFloat((ushort)half3.x);
		result.y = Mathf.HalfToFloat((ushort)half3.y);
		result.z = Mathf.HalfToFloat((ushort)half3.z);
		return result;
	}

	public static ProtoBuf.Half3 Vec3ToProtoHalf3(Vector3 vec3)
	{
		ProtoBuf.Half3 result = default(ProtoBuf.Half3);
		result.x = Mathf.FloatToHalf(vec3.x);
		result.y = Mathf.FloatToHalf(vec3.y);
		result.z = Mathf.FloatToHalf(vec3.z);
		return result;
	}

	public int GetTreeCount()
	{
		if (server == this)
		{
			return entities.Count;
		}
		return -1;
	}

	public override void ServerInit()
	{
		base.ServerInit();
		server = this;
		InitTreeGrid();
	}

	private void InitTreeGrid()
	{
		if (CellSize <= 0)
		{
			CellSize = 100;
		}
		gridSize = (int)Mathf.Ceil(World.Size / CellSize);
		gridSize = Math.Clamp(gridSize, 1, 512);
		if (gridSize == 512)
		{
			CellSize = (int)Mathf.Ceil((float)World.Size / (float)gridSize);
		}
		RustLog.Log(RustLog.EntryType.Network, 1, null, "TreeManager: using {0}x{0} grid with cell size {1}", gridSize, CellSize);
		treesGrid = new List<TreeCell>(gridSize * gridSize);
		for (int i = 0; i < gridSize * gridSize; i++)
		{
			TreeCell item = default(TreeCell);
			item.TreeList = new TreeList();
			item.TreeList.trees = new List<ProtoBuf.Tree>();
			item.SerializedCell = new MemoryStream();
			treesGrid.Add(item);
		}
		foreach (BaseEntity entity in entities)
		{
			Vector2i vector2i = ToCellIndices(entity.ServerPosition);
			ProtoBuf.Tree tree = Facepunch.Pool.Get<ProtoBuf.Tree>();
			ExtractTreeNetworkData(entity, tree);
			treesGrid[vector2i.y * gridSize + vector2i.x].TreeList.trees.Add(tree);
		}
		foreach (TreeCell item2 in treesGrid)
		{
			item2.TreeList.WriteToStream(item2.SerializedCell);
		}
	}

	public void SendPendingTrees()
	{
		playersToProcess.RemoveAll((ToProcess record) => record.Player == null);
		if (playersToProcess.IsEmpty())
		{
			return;
		}
		playersToProcess.Sort((ToProcess left, ToProcess right) => right.Left - left.Left);
		Stopwatch obj = Facepunch.Pool.Get<Stopwatch>();
		Stopwatch obj2 = Facepunch.Pool.Get<Stopwatch>();
		obj.Start();
		for (int i = 0; i < playersToProcess.Count; i++)
		{
			if (obj.Elapsed.TotalMilliseconds > (double)UpdateBudgetMS)
			{
				break;
			}
			using (TimeWarning.New("Player"))
			{
				obj2.Restart();
				ToProcess record2 = playersToProcess[i];
				Vector2i vector2i = ToCellIndices(record2.Player.ServerPosition);
				if (record2.OldCellIndex != vector2i.y * gridSize + vector2i.x)
				{
					record2.LastProcessedIndex = -1;
					record2.Range = 1;
					record2.OldCellIndex = vector2i.y * gridSize + vector2i.x;
				}
				int num = record2.Range;
				while (obj2.Elapsed.TotalMilliseconds < (double)PlayerBudgetMS && record2.Left > 0)
				{
					int num2 = Math.Max(vector2i.x - num / 2, 0);
					int num3 = Math.Max(vector2i.y - num / 2, 0);
					int num4 = Math.Min(num2 + num, gridSize - 1);
					int num5 = Math.Min(num3 + num, gridSize - 1);
					for (int j = num2; j <= num4; j++)
					{
						if (SendToPlayer(num3 * gridSize + j, ref record2) && obj2.Elapsed.TotalMilliseconds >= (double)PlayerBudgetMS)
						{
							obj2.Stop();
							break;
						}
					}
					if (obj2.Elapsed.TotalMilliseconds >= (double)PlayerBudgetMS)
					{
						obj2.Stop();
						break;
					}
					if (num5 - num3 > 1)
					{
						for (int k = num3 + 1; k <= num5 - 1; k++)
						{
							if (SendToPlayer(k * gridSize + num2, ref record2) && obj2.Elapsed.TotalMilliseconds >= (double)PlayerBudgetMS)
							{
								obj2.Stop();
								break;
							}
							if (num4 != num2 && SendToPlayer(k * gridSize + num4, ref record2) && obj2.Elapsed.TotalMilliseconds >= (double)PlayerBudgetMS)
							{
								obj2.Stop();
								break;
							}
						}
					}
					if (obj2.Elapsed.TotalMilliseconds >= (double)PlayerBudgetMS)
					{
						obj2.Stop();
						break;
					}
					if (num5 != num3)
					{
						for (int l = num2; l <= num4; l++)
						{
							if (SendToPlayer(num5 * gridSize + l, ref record2) && obj2.Elapsed.TotalMilliseconds >= (double)PlayerBudgetMS)
							{
								obj2.Stop();
								break;
							}
						}
					}
					if (obj2.IsRunning)
					{
						num++;
						record2.LastProcessedIndex = -1;
					}
				}
				record2.Range = num;
				obj2.Stop();
				ToProcess.Telemetry stats = record2.Stats;
				stats.IterativeTime += obj2.Elapsed;
				stats.FramesToComplete++;
				record2.Stats = stats;
				playersToProcess[i] = record2;
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj2);
		Facepunch.Pool.FreeUnmanaged(ref obj);
		playersToProcess.RemoveAll(delegate(ToProcess record)
		{
			if (record.Left == 0)
			{
				record.Stats.Report(record.Player);
				return true;
			}
			return false;
		});
		static bool SendToPlayer(int index, ref ToProcess record)
		{
			if (record.LastProcessedIndex >= index || record.SentCells[index])
			{
				return false;
			}
			record.LastProcessedIndex = index;
			record.SentCells[index] = true;
			record.Left--;
			UnityEngine.Debug.Assert(record.Left >= 0);
			TreeCell value = server.treesGrid[index];
			if (value.TreeList.trees.IsEmpty())
			{
				return false;
			}
			if (UseLazySerialization && value.IsDirty)
			{
				using (TimeWarning.New("LazySerialize"))
				{
					value.SerializedCell.SetLength(0L);
					value.TreeList.WriteToStream(value.SerializedCell);
					value.IsDirty = false;
					server.treesGrid[index] = value;
				}
			}
			using (TimeWarning.New("RPC"))
			{
				server.ClientRPC(RpcTarget.Player("CLIENT_ReceiveTrees", record.Player), value.SerializedCell);
				return true;
			}
		}
	}

	public static void StartTreesBatch(BasePlayer player)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		int num = server.gridSize * server.gridSize;
		BitArray bitArray = new BitArray(num);
		Vector2i vector2i = ToCellIndices(player.ServerPosition);
		int num2 = Math.Max(vector2i.x - 1, 0);
		int num3 = Math.Max(vector2i.y - 1, 0);
		int num4 = Math.Min(num2 + 3, server.gridSize - 1);
		int num5 = Math.Min(num3 + 3, server.gridSize - 1);
		for (int i = num3; i <= num5; i++)
		{
			for (int j = num2; j <= num4; j++)
			{
				int index = i * server.gridSize + j;
				TreeCell value = server.treesGrid[index];
				if (!value.TreeList.trees.IsEmpty())
				{
					if (UseLazySerialization && value.IsDirty)
					{
						using (TimeWarning.New("LazySerialize"))
						{
							value.SerializedCell.SetLength(0L);
							value.TreeList.WriteToStream(value.SerializedCell);
							value.IsDirty = false;
							server.treesGrid[index] = value;
						}
					}
					server.ClientRPC(RpcTarget.Player("CLIENT_ReceiveTrees", player), value.SerializedCell);
				}
				bitArray[index] = true;
				num--;
			}
		}
		stopwatch.Stop();
		ToProcess toProcess = default(ToProcess);
		toProcess.Player = player;
		toProcess.SentCells = bitArray;
		toProcess.Left = num;
		toProcess.Range = 4;
		toProcess.OldCellIndex = vector2i.y * server.gridSize + vector2i.x;
		toProcess.LastProcessedIndex = -1;
		toProcess.Stats = new ToProcess.Telemetry
		{
			InitialTime = stopwatch.Elapsed,
			FramesToComplete = 1
		};
		ToProcess item = toProcess;
		server.playersToProcess.Add(item);
	}

	private static Vector2i ToCellIndices(Vector3 worldPos)
	{
		float num = (float)World.Size / 2f;
		Vector2 vector = worldPos.XZ2D() + new Vector2(num, num);
		vector.x = Mathf.Clamp(vector.x, 0f, World.Size - 1);
		vector.y = Mathf.Clamp(vector.y, 0f, World.Size - 1);
		return new Vector2i((int)(vector.x / (float)CellSize), (int)(vector.y / (float)CellSize));
	}

	public static void OnTreeDestroyed(BaseEntity billboardEntity)
	{
		entities.Remove(billboardEntity);
		if (Rust.Application.isLoading || Rust.Application.isQuitting)
		{
			return;
		}
		using (TimeWarning.New("TreeManager.OnTreeDestroyed"))
		{
			Vector2i vector2i = ToCellIndices(billboardEntity.ServerPosition);
			int index = vector2i.y * server.gridSize + vector2i.x;
			TreeCell value = server.treesGrid[index];
			List<ProtoBuf.Tree> trees = value.TreeList.trees;
			for (int i = 0; i < trees.Count; i++)
			{
				if (trees[i].netId == billboardEntity.net.ID)
				{
					ProtoBuf.Tree obj = trees[i];
					Facepunch.Pool.Free(ref obj);
					trees.RemoveAt(i);
					if (UseLazySerialization)
					{
						value.IsDirty = true;
						server.treesGrid[index] = value;
					}
					else
					{
						value.SerializedCell.SetLength(0L);
						value.TreeList.WriteToStream(value.SerializedCell);
					}
					break;
				}
			}
			server.ClientRPC(RpcTarget.NetworkGroup("CLIENT_TreeDestroyed"), billboardEntity.net.ID);
		}
	}

	public static void OnTreeSpawned(BaseEntity billboardEntity)
	{
		if (billboardEntity.net.group != null && billboardEntity.net.group.restricted)
		{
			return;
		}
		entities.Add(billboardEntity);
		if (Rust.Application.isLoading || Rust.Application.isQuitting)
		{
			return;
		}
		using (TimeWarning.New("TreeManager.OnTreeSpawned"))
		{
			Vector2i vector2i = ToCellIndices(billboardEntity.ServerPosition);
			int index = vector2i.y * server.gridSize + vector2i.x;
			ProtoBuf.Tree tree = Facepunch.Pool.Get<ProtoBuf.Tree>();
			ExtractTreeNetworkData(billboardEntity, tree);
			TreeCell value = server.treesGrid[index];
			value.TreeList.trees.Add(tree);
			if (UseLazySerialization)
			{
				value.IsDirty = true;
				server.treesGrid[index] = value;
			}
			else
			{
				value.SerializedCell.SetLength(0L);
				value.TreeList.WriteToStream(value.SerializedCell);
			}
			List<Connection> obj = Facepunch.Pool.Get<List<Connection>>();
			foreach (Connection subscriber in server.net.group.subscribers)
			{
				bool flag = true;
				for (int i = 0; i < server.playersToProcess.Count; i++)
				{
					ToProcess toProcess = server.playersToProcess[i];
					if (toProcess.Player.Connection == subscriber && !toProcess.SentCells[index])
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					obj.Add(subscriber);
				}
			}
			if (!obj.IsEmpty())
			{
				using ProtoBuf.Tree tree2 = Facepunch.Pool.Get<ProtoBuf.Tree>();
				ExtractTreeNetworkData(billboardEntity, tree2);
				server.ClientRPC(RpcTarget.Players("CLIENT_TreeSpawned", obj), tree2);
			}
			Facepunch.Pool.FreeUnmanaged(ref obj);
		}
	}

	private static void ExtractTreeNetworkData(BaseEntity billboardEntity, ProtoBuf.Tree tree)
	{
		tree.netId = billboardEntity.net.ID;
		tree.prefabId = billboardEntity.prefabID;
		tree.position = Vec3ToProtoHalf3(billboardEntity.transform.position);
		tree.scale = billboardEntity.transform.lossyScale.y;
	}

	public static void SendSnapshot(BasePlayer player)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		BufferList<BaseEntity> values = entities.Values;
		TreeList treeList = null;
		for (int i = 0; i < values.Count; i++)
		{
			BaseEntity billboardEntity = values[i];
			ProtoBuf.Tree tree = Facepunch.Pool.Get<ProtoBuf.Tree>();
			ExtractTreeNetworkData(billboardEntity, tree);
			if (treeList == null)
			{
				treeList = Facepunch.Pool.Get<TreeList>();
				treeList.trees = Facepunch.Pool.Get<List<ProtoBuf.Tree>>();
			}
			treeList.trees.Add(tree);
			if (treeList.trees.Count >= ConVar.Server.maxpacketsize_globaltrees)
			{
				server.ClientRPC(RpcTarget.Player("CLIENT_ReceiveTrees", player), treeList);
				treeList.Dispose();
				treeList = null;
			}
		}
		if (treeList != null)
		{
			server.ClientRPC(RpcTarget.Player("CLIENT_ReceiveTrees", player), treeList);
			treeList.Dispose();
			treeList = null;
		}
		stopwatch.Stop();
		RustLog.Log(RustLog.EntryType.Network, 1, player.gameObject, "Took {0}ms to send {1} global trees to {2}", stopwatch.Elapsed.TotalMilliseconds, values.Count, player);
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(0uL)]
	private void SERVER_RequestTrees(RPCMessage msg)
	{
		if (EnableTreeStreaming)
		{
			StartTreesBatch(msg.player);
		}
		else
		{
			SendSnapshot(msg.player);
		}
	}
}
