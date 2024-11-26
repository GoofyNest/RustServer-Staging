using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Facepunch.NativeMeshSimplification;

[BurstCompile]
internal struct PopulateArraysJob : IJob
{
	public NativeList<float3> VerticesIn;

	public NativeList<int> IndicesIn;

	public NativeList<NativeMeshSimplifier.Triangle> TrianglesOut;

	public NativeList<NativeMeshSimplifier.Vertex> VerticesOut;

	public void Execute()
	{
		if (VerticesOut.Capacity < VerticesIn.Length)
		{
			VerticesOut.SetCapacity(VerticesIn.Length);
		}
		VerticesOut.Clear();
		for (int i = 0; i < VerticesIn.Length; i++)
		{
			ref NativeList<NativeMeshSimplifier.Vertex> verticesOut = ref VerticesOut;
			NativeMeshSimplifier.Vertex value = new NativeMeshSimplifier.Vertex
			{
				p = VerticesIn[i]
			};
			verticesOut.Add(in value);
		}
		if (TrianglesOut.Capacity < IndicesIn.Length / 3)
		{
			TrianglesOut.SetCapacity(IndicesIn.Length / 3);
		}
		TrianglesOut.Clear();
		for (int j = 0; j < IndicesIn.Length; j += 3)
		{
			ref NativeList<NativeMeshSimplifier.Triangle> trianglesOut = ref TrianglesOut;
			NativeMeshSimplifier.Triangle value2 = new NativeMeshSimplifier.Triangle
			{
				vIndex = new int3(IndicesIn[j], IndicesIn[j + 1], IndicesIn[j + 2])
			};
			trianglesOut.Add(in value2);
		}
	}
}
