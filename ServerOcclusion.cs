using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;

public static class ServerOcclusion
{
	public readonly struct Grid : IEquatable<Grid>
	{
		public readonly int x;

		public readonly int y;

		public readonly int z;

		public const float Resolution = 16f;

		public const float HalfResolution = 8f;

		public Grid(int x, int y, int z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public int GetOffset(float axis)
		{
			return Mathf.RoundToInt(axis / 2f / 16f);
		}

		public Vector3 GetCenterPoint()
		{
			return new Vector3((float)(x - GetOffset(TerrainMeta.Size.x)) * 16f, (float)(y - GetOffset(MaxY)) * 16f, (float)(z - GetOffset(TerrainMeta.Size.z)) * 16f);
		}

		public override string ToString()
		{
			return $"(x: {x}, y: {y}, z: {z})";
		}

		public bool Equals(Grid other)
		{
			if (x == other.x && y == other.y)
			{
				return z == other.z;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(x, y, z);
		}

		public bool IsBlocked()
		{
			return GamePhysics.CheckBounds(new Bounds(GetCenterPoint(), new Vector3(16f, 16f, 16f)), 8388608);
		}

		public bool IsUnderTerrain()
		{
			if (AntiHack.TestInsideTerrain(GetCenterPoint()))
			{
				return true;
			}
			return false;
		}

		public int GetIndex()
		{
			return GetGridIndex(x, y, z);
		}
	}

	public readonly struct SubGrid : IEquatable<SubGrid>
	{
		public readonly int x;

		public readonly int y;

		public readonly int z;

		public const float Resolution = 2f;

		public const float HalfResolution = 1f;

		public SubGrid(int x, int y, int z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public int GetOffset(float axis)
		{
			return Mathf.RoundToInt(axis / 2f / 2f);
		}

		public Vector3 GetCenterPoint()
		{
			return new Vector3((float)(x - GetOffset(TerrainMeta.Size.x)) * 2f, (float)(y - GetOffset(MaxY)) * 2f, (float)(z - GetOffset(TerrainMeta.Size.z)) * 2f);
		}

		public override string ToString()
		{
			return $"(x: {x}, y: {y}, z: {z})";
		}

		public bool Equals(SubGrid other)
		{
			if (x == other.x && y == other.y)
			{
				return z == other.z;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(x, y, z);
		}

		public bool IsBlocked()
		{
			bool flag = false;
			for (int i = 0; i < GridOffsets.Length; i++)
			{
				Vector3 pos = GetCenterPoint() + GridOffsets[i];
				flag = false;
				if (OcclusionIncludeRocks)
				{
					flag = AntiHack.IsInsideMesh(pos);
				}
				if (!AntiHack.TestInsideTerrain(pos) && !flag)
				{
					return false;
				}
			}
			if (flag)
			{
				return AntiHack.isInsideRayHit.collider.gameObject.HasCustomTag(GameObjectTag.AllowBarricadePlacement);
			}
			return true;
		}

		public int GetIndex()
		{
			return GetSubGridIndex(x, y, z);
		}

		public int GetDistance(SubGrid other)
		{
			return Mathf.Abs(x - other.x) + Mathf.Abs(y - other.y) + Mathf.Abs(z - other.z);
		}
	}

	public static int MaxY = 200;

	public static int ChunkCountX;

	public static int ChunkCountY;

	public static int ChunkCountZ;

	public static int SubChunkCountX;

	public static int SubChunkCountY;

	public static int SubChunkCountZ;

	public static LimitDictionary<(SubGrid, SubGrid), bool> OcclusionCache = new LimitDictionary<(SubGrid, SubGrid), bool>(32768);

	public static BitArray[] OcclusionSubGridBlocked;

	public const int OcclusionChunkSize = 16;

	public const int OcclusionChunkResolution = 8;

	public static readonly Vector3[] GridOffsets = new Vector3[2]
	{
		new Vector3(0f, 0f, 0f),
		new Vector3(0f, 1f, 0f)
	};

	public static (int, int, int)[] neighbours = new(int, int, int)[6]
	{
		(1, 0, 0),
		(-1, 0, 0),
		(0, 1, 0),
		(0, -1, 0),
		(0, 0, 1),
		(0, 0, -1)
	};

	public static bool OcclusionEnabled { get; set; } = false;


	public static bool OcclusionIncludeRocks { get; set; } = false;


	public static float OcclusionPollRate => 2f;

	public static int MinOcclusionDistance => 25;

	public static int OcclusionMaxBFSIterations => 512;

	public static int GetGridIndex(int x, int y, int z)
	{
		return z * ChunkCountX * ChunkCountY + y * ChunkCountZ + x;
	}

	public static int GetSubGridIndex(int x, int y, int z)
	{
		return z * SubChunkCountX * SubChunkCountY + y * SubChunkCountX + x;
	}

	public static int GetGrid(float position, float axis)
	{
		return Mathf.RoundToInt(position / 16f + axis / 16f);
	}

	public static Grid GetGrid(Vector3 position)
	{
		int grid = GetGrid(position.x, TerrainMeta.Size.x / 2f);
		int grid2 = GetGrid(position.y, MaxY / 2);
		int grid3 = GetGrid(position.z, TerrainMeta.Size.z / 2f);
		if (IsValidGrid(grid, grid2, grid3))
		{
			return new Grid(grid, grid2, grid3);
		}
		return default(Grid);
	}

	public static int GetSubGrid(float position, float axis)
	{
		return Mathf.RoundToInt(position / 2f + axis / 2f);
	}

	public static SubGrid GetSubGrid(Vector3 position)
	{
		int subGrid = GetSubGrid(position.x, TerrainMeta.Size.x / 2f);
		int subGrid2 = GetSubGrid(position.y, MaxY / 2);
		int subGrid3 = GetSubGrid(position.z, TerrainMeta.Size.z / 2f);
		if (IsValidSubGrid(subGrid, subGrid2, subGrid3))
		{
			return new SubGrid(subGrid, subGrid2, subGrid3);
		}
		return default(SubGrid);
	}

	public static bool IsBlocked(int x, int y, int z)
	{
		int result;
		int x2 = Math.DivRem(x, 8, out result);
		int result2;
		int y2 = Math.DivRem(y, 8, out result2);
		int result3;
		int z2 = Math.DivRem(z, 8, out result3);
		int gridIndex = GetGridIndex(x2, y2, z2);
		BitArray bitArray = (IsValidGrid(x2, y2, z2) ? OcclusionSubGridBlocked[gridIndex] : null);
		int index = result3 * 8 * 8 + result2 * 8 + result;
		return bitArray?[index] ?? false;
	}

	public static bool IsBlocked(SubGrid sub)
	{
		return IsBlocked(sub.x, sub.y, sub.z);
	}

	public static bool IsValidGrid(int x, int y, int z)
	{
		if (x < 0 || y < 0 || z < 0)
		{
			return false;
		}
		if (x >= ChunkCountX || y >= ChunkCountY || z >= ChunkCountZ)
		{
			return false;
		}
		return true;
	}

	public static bool IsValidSubGrid(int x, int y, int z)
	{
		if (x < 0 || y < 0 || z < 0)
		{
			return false;
		}
		if (x >= SubChunkCountX || y >= SubChunkCountY || z >= SubChunkCountZ)
		{
			return false;
		}
		return true;
	}

	public static void CalculatePathBetweenGrids(SubGrid grid1, SubGrid grid2, out bool directPath, out bool anyPath)
	{
		anyPath = true;
		directPath = true;
		int num = grid1.x;
		int num2 = grid1.y;
		int num3 = grid1.z;
		int x = grid2.x;
		int y = grid2.y;
		int z = grid2.z;
		int num4 = x - grid1.x;
		int num5 = y - grid1.y;
		int num6 = z - grid1.z;
		int num7 = Mathf.Abs(num4);
		int num8 = Mathf.Abs(num5);
		int num9 = Mathf.Abs(num6);
		int num10 = num7 << 1;
		int num11 = num8 << 1;
		int num12 = num9 << 1;
		int num13 = ((num4 >= 0) ? 1 : (-1));
		int num14 = ((num5 >= 0) ? 1 : (-1));
		int num15 = ((num6 >= 0) ? 1 : (-1));
		bool originBlocked2;
		bool neighboursBlocked2;
		if (num7 >= num8 && num7 >= num9)
		{
			int num16 = num11 - num7;
			int num17 = num12 - num7;
			for (int i = 0; i < num7; i++)
			{
				AddToGridArea(new SubGrid(num, num2, num3), out originBlocked2, out neighboursBlocked2);
				if (directPath && originBlocked2)
				{
					directPath = false;
				}
				if (originBlocked2 && neighboursBlocked2)
				{
					anyPath = false;
					return;
				}
				if (num16 > 0)
				{
					num2 += num14;
					num16 -= num10;
				}
				if (num17 > 0)
				{
					num3 += num15;
					num17 -= num10;
				}
				num16 += num11;
				num17 += num12;
				num += num13;
			}
		}
		else if (num8 >= num7 && num8 >= num9)
		{
			int num16 = num10 - num8;
			int num17 = num12 - num8;
			for (int j = 0; j < num8; j++)
			{
				AddToGridArea(new SubGrid(num, num2, num3), out originBlocked2, out neighboursBlocked2);
				if (directPath && originBlocked2)
				{
					directPath = false;
				}
				if (originBlocked2 && neighboursBlocked2)
				{
					anyPath = false;
					return;
				}
				if (num16 > 0)
				{
					num += num13;
					num16 -= num11;
				}
				if (num17 > 0)
				{
					num3 += num15;
					num17 -= num11;
				}
				num16 += num10;
				num17 += num12;
				num2 += num14;
			}
		}
		else
		{
			int num16 = num11 - num9;
			int num17 = num10 - num9;
			for (int k = 0; k < num9; k++)
			{
				AddToGridArea(new SubGrid(num, num2, num3), out originBlocked2, out neighboursBlocked2);
				if (directPath && originBlocked2)
				{
					directPath = false;
				}
				if (originBlocked2 && neighboursBlocked2)
				{
					anyPath = false;
					return;
				}
				if (num16 > 0)
				{
					num2 += num14;
					num16 -= num12;
				}
				if (num17 > 0)
				{
					num += num13;
					num17 -= num12;
				}
				num16 += num11;
				num17 += num10;
				num3 += num15;
			}
		}
		AddToGridArea(grid2, out originBlocked2, out neighboursBlocked2);
		if (directPath && originBlocked2)
		{
			directPath = false;
		}
		if (originBlocked2 && neighboursBlocked2)
		{
			anyPath = false;
		}
		static void AddNeighbours(SubGrid grid, out bool blocked)
		{
			blocked = true;
			for (int l = 0; l < neighbours.Length; l++)
			{
				int x2 = grid.x + neighbours[l].Item1;
				int y2 = grid.y + neighbours[l].Item2;
				int z2 = grid.z + neighbours[l].Item3;
				if (!IsValidSubGrid(x2, y2, z2))
				{
					break;
				}
				if (!IsBlocked(new SubGrid(x2, y2, z2)))
				{
					blocked = false;
				}
			}
		}
		static void AddToGridArea(SubGrid grid, out bool originBlocked, out bool neighboursBlocked)
		{
			originBlocked = true;
			if (!IsBlocked(grid))
			{
				originBlocked = false;
				AddNeighbours(grid, out neighboursBlocked);
			}
			else
			{
				AddNeighbours(grid, out neighboursBlocked);
			}
		}
	}

	public static void SetupGrid()
	{
		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();
		Vector3 size = TerrainMeta.Size;
		ChunkCountX = Mathf.Max(Mathf.CeilToInt(size.x / 16f), 1);
		ChunkCountY = Mathf.Max(Mathf.CeilToInt((float)MaxY / 16f), 1);
		ChunkCountZ = Mathf.Max(Mathf.CeilToInt(size.z / 16f), 1);
		SubChunkCountX = Mathf.Max(Mathf.CeilToInt(size.x / 2f), 1);
		SubChunkCountY = Mathf.Max(Mathf.CeilToInt((float)MaxY / 2f), 1);
		SubChunkCountZ = Mathf.Max(Mathf.CeilToInt(size.z / 2f), 1);
		OcclusionSubGridBlocked = new BitArray[ChunkCountX * ChunkCountY * ChunkCountZ];
		UnityEngine.Debug.Log($"Preparing Occlusion Grid ({SubChunkCountX}, {SubChunkCountY}, {SubChunkCountZ})");
		for (int i = 0; i < ChunkCountX; i++)
		{
			for (int j = 0; j < ChunkCountY; j++)
			{
				for (int k = 0; k < ChunkCountZ; k++)
				{
					Grid cell2 = new Grid(i, j, k);
					bool flag = cell2.IsBlocked();
					bool flag2 = cell2.IsUnderTerrain() && !flag;
					if (flag || flag2)
					{
						PopulateSubGrid(cell2, flag2);
					}
				}
			}
		}
		UnityEngine.Debug.Log($"Initialized {SubChunkCountX * SubChunkCountY * SubChunkCountZ} occlusion sub-chunks - took {stopwatch.Elapsed.TotalMilliseconds / 1000.0} seconds");
		static void PopulateSubGrid(Grid cell, bool underTerrain)
		{
			int num = cell.x * 8;
			int num2 = cell.y * 8;
			int num3 = cell.z * 8;
			int index = cell.GetIndex();
			ref BitArray reference = ref OcclusionSubGridBlocked[index];
			BitArray bitArray = reference ?? (reference = new BitArray(512));
			for (int l = 0; l < 8; l++)
			{
				for (int m = 0; m < 8; m++)
				{
					for (int n = 0; n < 8; n++)
					{
						int index2 = n * 8 * 8 + m * 8 + l;
						SubGrid subGrid = new SubGrid(num + l, num2 + m, num3 + n);
						bitArray[index2] = underTerrain || subGrid.IsBlocked();
					}
				}
			}
		}
	}

	[ServerVar(Help = "Tests occlusion visibility between two positions")]
	public static string serverocclusiondebug(ConsoleSystem.Arg arg)
	{
		Vector3 vector = arg.GetVector3(0);
		Vector3 vector2 = arg.GetVector3(1);
		SubGrid subGrid = GetSubGrid(vector);
		SubGrid subGrid2 = GetSubGrid(vector2);
		if (subGrid.Equals(default(SubGrid)) || subGrid2.Equals(default(SubGrid)))
		{
			return $"Invalid grid(s), positions provided: {vector} - {vector2}";
		}
		CalculatePathBetweenGrids(subGrid, subGrid2, out var directPath, out var anyPath);
		return $"Grid 1: {subGrid}, Grid 2: {subGrid2}\nDirect Path: {directPath}, Any Path: {anyPath}";
	}
}
