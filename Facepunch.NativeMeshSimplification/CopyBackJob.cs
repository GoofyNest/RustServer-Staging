using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Facepunch.NativeMeshSimplification;

[BurstCompile]
internal struct CopyBackJob : IJob
{
	[WriteOnly]
	public NativeList<float3> DstVertices;

	[WriteOnly]
	public NativeList<int> DstIndices;

	[global::Unity.Collections.ReadOnly]
	public NativeList<NativeMeshSimplifier.Triangle> SrcTriangles;

	[global::Unity.Collections.ReadOnly]
	public NativeList<NativeMeshSimplifier.Vertex> SrcVertices;

	public void Execute()
	{
		DstVertices.Clear();
		DstVertices.SetCapacity(SrcVertices.Length);
		for (int i = 0; i < SrcVertices.Length; i++)
		{
			ref NativeList<float3> dstVertices = ref DstVertices;
			NativeMeshSimplifier.Vertex vertex = SrcVertices[i];
			dstVertices.Add(in vertex.p);
		}
		DstIndices.Clear();
		DstIndices.SetCapacity(SrcTriangles.Length * 3);
		for (int j = 0; j < SrcTriangles.Length; j++)
		{
			ref NativeList<int> dstIndices = ref DstIndices;
			int value = SrcTriangles[j].vIndex[0];
			dstIndices.Add(in value);
			ref NativeList<int> dstIndices2 = ref DstIndices;
			value = SrcTriangles[j].vIndex[1];
			dstIndices2.Add(in value);
			ref NativeList<int> dstIndices3 = ref DstIndices;
			value = SrcTriangles[j].vIndex[2];
			dstIndices3.Add(in value);
		}
	}
}
