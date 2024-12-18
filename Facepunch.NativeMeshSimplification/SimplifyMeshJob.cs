using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Facepunch.NativeMeshSimplification;

[BurstCompile]
internal struct SimplifyMeshJob : IJob
{
	[StructLayout(LayoutKind.Sequential, Size = 1)]
	private struct ProfilerMarkerStub
	{
		[BurstDiscard]
		public void Begin()
		{
		}

		[BurstDiscard]
		public void End()
		{
		}
	}

	public int MaxIterations;

	public float ReductionTarget;

	public int Aggressiveness;

	public NativeList<NativeMeshSimplifier.Triangle> Triangles;

	public NativeList<NativeMeshSimplifier.Vertex> Vertices;

	public NativeList<NativeMeshSimplifier.Ref> Refs;

	private static readonly ProfilerMarkerStub k_Iteration;

	private static readonly ProfilerMarkerStub k_UpdateMesh;

	private static readonly ProfilerMarkerStub k_FirstUpdate;

	private static readonly ProfilerMarkerStub k_CompactMesh;

	private static readonly ProfilerMarkerStub k_Flipped;

	private static readonly ProfilerMarkerStub k_UpdateTriangles;

	private static readonly ProfilerMarkerStub k_MemCpy;

	public void Execute()
	{
		Refs.Clear();
		NativeList<int> deleted = new NativeList<int>(Allocator.Temp);
		NativeList<int> deleted2 = new NativeList<int>(Allocator.Temp);
		int deletedTriangles = 0;
		int length = Triangles.Length;
		int num = (int)((float)length * ReductionTarget);
		for (int i = 0; i < MaxIterations; i++)
		{
			k_Iteration.Begin();
			if (length - deletedTriangles <= num)
			{
				k_Iteration.End();
				break;
			}
			if (i % 5 == 0)
			{
				k_UpdateMesh.Begin();
				UpdateMesh(i);
				k_UpdateMesh.End();
			}
			for (int j = 0; j < Triangles.Length; j++)
			{
				NativeListAccessExtensions.Get(in Triangles, j).dirty = false;
			}
			float num2 = 1E-09f * math.pow((float)i + 3f, Aggressiveness);
			for (int k = 0; k < Triangles.Length; k++)
			{
				ref readonly NativeMeshSimplifier.Triangle @readonly = ref NativeListAccessExtensions.GetReadonly(in Triangles, k);
				if (math.any(new bool3(@readonly.err[3] > num2, @readonly.deleted, @readonly.dirty)))
				{
					continue;
				}
				for (int l = 0; l < 3; l++)
				{
					if (@readonly.err[l] > num2)
					{
						continue;
					}
					int num3 = @readonly.vIndex[l];
					int num4 = @readonly.vIndex[(l + 1) % 3];
					ref NativeMeshSimplifier.Vertex reference = ref NativeListAccessExtensions.Get(in Vertices, num3);
					ref readonly NativeMeshSimplifier.Vertex readonly2 = ref NativeListAccessExtensions.GetReadonly(in Vertices, num4);
					if (reference.border != readonly2.border)
					{
						continue;
					}
					CalculateError(in reference, in readonly2, out var pResult);
					deleted.Length = reference.tCount;
					deleted2.Length = readonly2.tCount;
					if (Flipped(pResult, num4, in reference, ref deleted) || Flipped(pResult, num3, in readonly2, ref deleted2))
					{
						continue;
					}
					reference.p = pResult;
					reference.q = readonly2.q + reference.q;
					int length2 = Refs.Length;
					k_UpdateTriangles.Begin();
					UpdateTriangles(num3, in reference, in deleted, ref deletedTriangles);
					UpdateTriangles(num3, in readonly2, in deleted2, ref deletedTriangles);
					k_UpdateTriangles.End();
					int num5 = Refs.Length - length2;
					if (num5 <= reference.tCount)
					{
						if (num5 > 0)
						{
							k_MemCpy.Begin();
							NativeArray<NativeMeshSimplifier.Ref> array = Refs.AsArray();
							NativeSlice<NativeMeshSimplifier.Ref> nativeSlice = new NativeSlice<NativeMeshSimplifier.Ref>(array, reference.tStart, num5);
							NativeSlice<NativeMeshSimplifier.Ref> slice = new NativeSlice<NativeMeshSimplifier.Ref>(array, length2, num5);
							nativeSlice.CopyFrom(slice);
							k_MemCpy.End();
						}
					}
					else
					{
						reference.tStart = length2;
					}
					reference.tCount = num5;
					break;
				}
				if (length - deletedTriangles <= num)
				{
					break;
				}
			}
			k_Iteration.End();
		}
		k_CompactMesh.Begin();
		CompactMesh();
		k_CompactMesh.End();
		deleted.Dispose();
		deleted2.Dispose();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float VertexError(in NativeMeshSimplifier.SymmetricMatrix q, in float3 v)
	{
		float x = v.x;
		float y = v.y;
		float z = v.z;
		return q[0] * x * x + 2f * q[1] * x * y + 2f * q[2] * x * z + 2f * q[3] * x + q[4] * y * y + 2f * q[5] * y * z + 2f * q[6] * y + q[7] * z * z + 2f * q[8] * z + q[9];
	}

	private float CalculateError(int idV1, int idV2)
	{
		float3 pResult;
		return CalculateError(in NativeListAccessExtensions.Get(in Vertices, idV1), in NativeListAccessExtensions.Get(in Vertices, idV2), out pResult);
	}

	private float CalculateError(in NativeMeshSimplifier.Vertex v1, in NativeMeshSimplifier.Vertex v2, out float3 pResult)
	{
		pResult = default(float3);
		NativeMeshSimplifier.SymmetricMatrix q = v1.q + v2.q;
		bool flag = v1.border & v2.border;
		float num = 0f;
		float num2 = q.Det(0, 1, 2, 1, 4, 5, 2, 5, 7);
		if (num2 != 0f && !flag)
		{
			float num3 = math.rcp(num2);
			pResult.x = (0f - num3) * q.Det(1, 2, 3, 4, 5, 6, 5, 7, 8);
			pResult.y = num3 * q.Det(0, 2, 3, 1, 5, 6, 2, 7, 8);
			pResult.z = (0f - num3) * q.Det(0, 1, 3, 1, 4, 6, 2, 5, 8);
			num = VertexError(in q, in pResult);
		}
		else
		{
			float3 v3 = v1.p;
			float3 v4 = v2.p;
			float3 v5 = (v3 + v4) * 0.5f;
			float num4 = VertexError(in q, in v3);
			float num5 = VertexError(in q, in v4);
			float num6 = VertexError(in q, in v5);
			num = math.min(num4, math.min(num5, num6));
			if (num4 == num)
			{
				pResult = v3;
			}
			if (num5 == num)
			{
				pResult = v4;
			}
			if (num6 == num)
			{
				pResult = v5;
			}
		}
		return num;
	}

	private bool Flipped(float3 p, int i1, in NativeMeshSimplifier.Vertex v0, ref NativeList<int> deleted)
	{
		k_Flipped.Begin();
		for (int j = 0; j < v0.tCount; j++)
		{
			ref readonly NativeMeshSimplifier.Ref @readonly = ref NativeListAccessExtensions.GetReadonly(in Refs, v0.tStart + j);
			ref readonly NativeMeshSimplifier.Triangle readonly2 = ref NativeListAccessExtensions.GetReadonly(in Triangles, @readonly.tId);
			if (readonly2.deleted)
			{
				continue;
			}
			int tVertex = @readonly.tVertex;
			int num = readonly2.vIndex[(tVertex + 1) % 3];
			int num2 = readonly2.vIndex[(tVertex + 2) % 3];
			if (num == i1 || num2 == i1)
			{
				deleted[j] = 1;
				continue;
			}
			float3 x = Vertices[num].p - p;
			float3 y = Vertices[num2].p - p;
			float3 x2 = math.cross(x, y);
			if (math.lengthsq(x2) == 0f)
			{
				k_Flipped.End();
				return true;
			}
			float3 x3 = math.normalize(x2);
			deleted[j] = 0;
			if (math.dot(x3, readonly2.n) < 0.2f)
			{
				k_Flipped.End();
				return true;
			}
		}
		k_Flipped.End();
		return false;
	}

	private void UpdateTriangles(int i0, in NativeMeshSimplifier.Vertex v, in NativeList<int> deleted, ref int deletedTriangles)
	{
		for (int j = 0; j < v.tCount; j++)
		{
			ref readonly NativeMeshSimplifier.Ref @readonly = ref NativeListAccessExtensions.GetReadonly(in Refs, v.tStart + j);
			ref NativeMeshSimplifier.Triangle reference = ref NativeListAccessExtensions.Get(in Triangles, @readonly.tId);
			if (!reference.deleted)
			{
				if (deleted[j] > 0)
				{
					reference.deleted = true;
					deletedTriangles++;
					continue;
				}
				ref readonly NativeMeshSimplifier.Vertex readonly2 = ref NativeListAccessExtensions.GetReadonly(in Vertices, reference.vIndex[0]);
				ref readonly NativeMeshSimplifier.Vertex readonly3 = ref NativeListAccessExtensions.GetReadonly(in Vertices, reference.vIndex[1]);
				ref readonly NativeMeshSimplifier.Vertex readonly4 = ref NativeListAccessExtensions.GetReadonly(in Vertices, reference.vIndex[2]);
				reference.vIndex[@readonly.tVertex] = i0;
				reference.dirty = true;
				reference.err[0] = CalculateError(in readonly2, in readonly3, out var pResult);
				reference.err[1] = CalculateError(in readonly3, in readonly4, out pResult);
				reference.err[2] = CalculateError(in readonly4, in readonly2, out pResult);
				reference.err[3] = math.cmin(reference.err.xyz);
				Refs.Add(in @readonly);
			}
		}
	}

	private void UpdateMesh(int iteration)
	{
		bool flag = iteration == 0;
		if (!flag)
		{
			int length = 0;
			for (int i = 0; i < Triangles.Length; i++)
			{
				ref readonly NativeMeshSimplifier.Triangle @readonly = ref NativeListAccessExtensions.GetReadonly(in Triangles, i);
				if (!@readonly.deleted)
				{
					Triangles[length++] = @readonly;
				}
			}
			Triangles.Length = length;
		}
		for (int j = 0; j < Vertices.Length; j++)
		{
			ref NativeMeshSimplifier.Vertex reference = ref NativeListAccessExtensions.Get(in Vertices, j);
			reference.tStart = 0;
			reference.tCount = 0;
		}
		for (int k = 0; k < Triangles.Length; k++)
		{
			ref readonly NativeMeshSimplifier.Triangle readonly2 = ref NativeListAccessExtensions.GetReadonly(in Triangles, k);
			for (int l = 0; l < 3; l++)
			{
				NativeListAccessExtensions.Get(in Vertices, readonly2.vIndex[l]).tCount++;
			}
		}
		int num = 0;
		for (int m = 0; m < Vertices.Length; m++)
		{
			ref NativeMeshSimplifier.Vertex reference2 = ref NativeListAccessExtensions.Get(in Vertices, m);
			reference2.tStart = num;
			num += reference2.tCount;
			reference2.tCount = 0;
		}
		Refs.Length = Triangles.Length * 3;
		for (int n = 0; n < Triangles.Length; n++)
		{
			ref readonly NativeMeshSimplifier.Triangle readonly3 = ref NativeListAccessExtensions.GetReadonly(in Triangles, n);
			for (int num2 = 0; num2 < 3; num2++)
			{
				ref NativeMeshSimplifier.Vertex reference3 = ref NativeListAccessExtensions.Get(in Vertices, readonly3.vIndex[num2]);
				ref NativeMeshSimplifier.Ref reference4 = ref NativeListAccessExtensions.Get(in Refs, reference3.tStart + reference3.tCount);
				reference4.tId = n;
				reference4.tVertex = num2;
				reference3.tCount++;
			}
		}
		if (!flag)
		{
			return;
		}
		k_FirstUpdate.Begin();
		NativeList<int> list = new NativeList<int>(Allocator.Temp);
		NativeList<int> nativeList = new NativeList<int>(Allocator.Temp);
		for (int num3 = 0; num3 < Vertices.Length; num3++)
		{
			ref readonly NativeMeshSimplifier.Vertex readonly4 = ref NativeListAccessExtensions.GetReadonly(in Vertices, num3);
			list.Clear();
			nativeList.Clear();
			for (int num4 = 0; num4 < readonly4.tCount; num4++)
			{
				ref readonly NativeMeshSimplifier.Triangle readonly5 = ref NativeListAccessExtensions.GetReadonly(in Triangles, Refs[readonly4.tStart + num4].tId);
				for (int num5 = 0; num5 < 3; num5++)
				{
					int num6 = 0;
					int value;
					for (value = readonly5.vIndex[num5]; num6 < list.Length && nativeList[num6] != value; num6++)
					{
					}
					if (num6 == list.Length)
					{
						int value2 = 1;
						list.Add(in value2);
						nativeList.Add(in value);
					}
					else
					{
						NativeListAccessExtensions.Get(in list, num6)++;
					}
				}
			}
			for (int num7 = 0; num7 < list.Length; num7++)
			{
				if (list[num7] == 1)
				{
					NativeListAccessExtensions.Get(in Vertices, nativeList[num7]).border = true;
				}
			}
		}
		list.Dispose();
		nativeList.Dispose();
		float3 @float = default(float3);
		float3x3 float3x = default(float3x3);
		for (int num8 = 0; num8 < Triangles.Length; num8++)
		{
			ref NativeMeshSimplifier.Triangle reference5 = ref NativeListAccessExtensions.Get(in Triangles, num8);
			for (int num9 = 0; num9 < 3; num9++)
			{
				ref readonly NativeMeshSimplifier.Vertex readonly6 = ref NativeListAccessExtensions.GetReadonly(in Vertices, reference5.vIndex[num9]);
				float3x[num9] = readonly6.p;
			}
			@float = (reference5.n = math.normalizesafe(math.cross(float3x[1] - float3x[0], float3x[2] - float3x[0]), math.right()));
			for (int num10 = 0; num10 < 3; num10++)
			{
				NativeListAccessExtensions.Get(in Vertices, reference5.vIndex[num10]).q += NativeMeshSimplifier.SymmetricMatrix.Plane(@float.x, @float.y, @float.z, 0f - math.dot(@float, float3x[0]));
			}
		}
		for (int num11 = 0; num11 < Triangles.Length; num11++)
		{
			ref NativeMeshSimplifier.Triangle reference6 = ref NativeListAccessExtensions.Get(in Triangles, num11);
			for (int num12 = 0; num12 < 3; num12++)
			{
				reference6.err[num12] = CalculateError(reference6.vIndex[num12], reference6.vIndex[(num12 + 1) % 3]);
			}
			reference6.err[3] = math.cmin(reference6.err.xyz);
		}
		k_FirstUpdate.End();
	}

	private void CompactMesh()
	{
		for (int i = 0; i < Vertices.Length; i++)
		{
			NativeListAccessExtensions.Get(in Vertices, i).tCount = 0;
		}
		int length = 0;
		for (int j = 0; j < Triangles.Length; j++)
		{
			ref readonly NativeMeshSimplifier.Triangle @readonly = ref NativeListAccessExtensions.GetReadonly(in Triangles, j);
			if (!@readonly.deleted)
			{
				Triangles[length++] = @readonly;
				for (int k = 0; k < 3; k++)
				{
					NativeListAccessExtensions.Get(in Vertices, @readonly.vIndex[k]).tCount = 1;
				}
			}
		}
		Triangles.Length = length;
		length = 0;
		for (int l = 0; l < Vertices.Length; l++)
		{
			ref NativeMeshSimplifier.Vertex reference = ref NativeListAccessExtensions.Get(in Vertices, l);
			if (reference.tCount != 0)
			{
				reference.tStart = length;
				NativeListAccessExtensions.Get(in Vertices, length).p = reference.p;
				length++;
			}
		}
		for (int m = 0; m < Triangles.Length; m++)
		{
			ref NativeMeshSimplifier.Triangle reference2 = ref NativeListAccessExtensions.Get(in Triangles, m);
			for (int n = 0; n < 3; n++)
			{
				reference2.vIndex[n] = Vertices[reference2.vIndex[n]].tStart;
			}
		}
		Vertices.Length = length;
	}
}
