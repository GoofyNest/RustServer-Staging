using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Facepunch.MarchingCubes;

[BurstCompile]
internal struct MarchJob : IJob
{
	[global::Unity.Collections.ReadOnly]
	public Point3DGrid sampler;

	public NativeList<float3> vertices;

	[WriteOnly]
	public NativeList<int> indices;

	public float3 vertexOffset;

	public float scale;

	public void Execute()
	{
		int width = sampler.Width;
		int height = sampler.Height;
		int depth = sampler.Depth;
		vertices.Clear();
		indices.Clear();
		NativeArray<int3> corners = new NativeArray<int3>(8, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
		NativeArray<float> cornerSamples = new NativeArray<float>(8, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
		for (int i = 0; i < width - 1; i++)
		{
			for (int j = 0; j < height - 1; j++)
			{
				for (int k = 0; k < depth - 1; k++)
				{
					ProcessCube(new int3(i, j, k), corners, cornerSamples, vertices, indices, sampler, vertexOffset, scale);
				}
			}
		}
	}

	private static void ProcessCube(int3 cubeStart, NativeArray<int3> corners, NativeArray<float> cornerSamples, NativeList<float3> vertices, NativeList<int> indices, Point3DGrid sampler, float3 vertexOffset, float scale)
	{
		corners[0] = cubeStart + new int3(0, 0, 0);
		corners[1] = cubeStart + new int3(1, 0, 0);
		corners[2] = cubeStart + new int3(1, 0, 1);
		corners[3] = cubeStart + new int3(0, 0, 1);
		corners[4] = cubeStart + new int3(0, 1, 0);
		corners[5] = cubeStart + new int3(1, 1, 0);
		corners[6] = cubeStart + new int3(1, 1, 1);
		corners[7] = cubeStart + new int3(0, 1, 1);
		int num = 0;
		for (int i = 0; i < corners.Length; i++)
		{
			float num3 = (cornerSamples[i] = sampler.Sample(corners[i]));
			if (num3 > 0f)
			{
				num |= 1 << i;
			}
		}
		int num4 = num * 16;
		for (int j = 0; j < 16; j += 3)
		{
			int num5 = MarchingCubeLookup.triTableFlat[num4 + j];
			if (num5 != -1)
			{
				int num6 = MarchingCubeLookup.triTableFlat[num4 + j + 1];
				int num7 = MarchingCubeLookup.triTableFlat[num4 + j + 2];
				float3 @float = GetVertex(corners[MarchingCubeLookup.cornerIndexAFromEdge[num5]], cornerSamples[MarchingCubeLookup.cornerIndexAFromEdge[num5]], corners[MarchingCubeLookup.cornerIndexBFromEdge[num5]], cornerSamples[MarchingCubeLookup.cornerIndexBFromEdge[num5]]);
				float3 float2 = GetVertex(corners[MarchingCubeLookup.cornerIndexAFromEdge[num6]], cornerSamples[MarchingCubeLookup.cornerIndexAFromEdge[num6]], corners[MarchingCubeLookup.cornerIndexBFromEdge[num6]], cornerSamples[MarchingCubeLookup.cornerIndexBFromEdge[num6]]);
				float3 float3 = GetVertex(corners[MarchingCubeLookup.cornerIndexAFromEdge[num7]], cornerSamples[MarchingCubeLookup.cornerIndexAFromEdge[num7]], corners[MarchingCubeLookup.cornerIndexBFromEdge[num7]], cornerSamples[MarchingCubeLookup.cornerIndexBFromEdge[num7]]);
				int value = vertices.Length;
				float3 value2 = (@float - vertexOffset) * scale;
				vertices.Add(in value2);
				value2 = (float2 - vertexOffset) * scale;
				vertices.Add(in value2);
				value2 = (float3 - vertexOffset) * scale;
				vertices.Add(in value2);
				indices.Add(in value);
				int value3 = value + 1;
				indices.Add(in value3);
				value3 = value + 2;
				indices.Add(in value3);
				continue;
			}
			break;
		}
		static float3 GetVertex(float3 v0, float s0, float3 v1, float s1)
		{
			float s2 = (0f - s0) / (s1 - s0);
			return math.lerp(v0, v1, s2);
		}
	}
}
