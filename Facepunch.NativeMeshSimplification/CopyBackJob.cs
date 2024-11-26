using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Facepunch.NativeMeshSimplification;

[BurstCompile]
internal struct CopyBackJob : IJob
{
	public NativeList<float3> VerticesIn;

	public NativeList<int> IndicesIn;

	public NativeList<NativeMeshSimplifier.Triangle> TrianglesOut;

	public NativeList<NativeMeshSimplifier.Vertex> VerticesOut;

	public void Execute()
	{
		VerticesIn.Clear();
		VerticesIn.SetCapacity(VerticesOut.Length);
		for (int i = 0; i < VerticesOut.Length; i++)
		{
			ref NativeList<float3> verticesIn = ref VerticesIn;
			NativeMeshSimplifier.Vertex vertex = VerticesOut[i];
			verticesIn.Add(in vertex.p);
		}
		IndicesIn.Clear();
		IndicesIn.SetCapacity(TrianglesOut.Length * 3);
		for (int j = 0; j < TrianglesOut.Length; j++)
		{
			ref NativeList<int> indicesIn = ref IndicesIn;
			int value = TrianglesOut[j].vIndex[0];
			indicesIn.Add(in value);
			ref NativeList<int> indicesIn2 = ref IndicesIn;
			value = TrianglesOut[j].vIndex[1];
			indicesIn2.Add(in value);
			ref NativeList<int> indicesIn3 = ref IndicesIn;
			value = TrianglesOut[j].vIndex[2];
			indicesIn3.Add(in value);
		}
	}
}
