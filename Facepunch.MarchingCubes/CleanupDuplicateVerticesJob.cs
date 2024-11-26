using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Facepunch.MarchingCubes;

[BurstCompile]
internal struct CleanupDuplicateVerticesJob : IJob
{
	[ReadOnly]
	public NativeList<float3> inputVertices;

	[ReadOnly]
	public NativeList<int> inputIndices;

	[WriteOnly]
	public NativeList<float3> outputVertices;

	[WriteOnly]
	public NativeList<int> outputIndices;

	public NativeHashMap<int, int> indexToIndices;

	public float3 vertexOffset;

	public float invScale;

	public int width;

	public int widthHeight;

	public void Execute()
	{
		indexToIndices.Clear();
		outputVertices.Clear();
		outputIndices.Clear();
		int value = 0;
		for (int i = 0; i < inputVertices.Length; i++)
		{
			int3 @int = (int3)(inputVertices[i] * invScale + vertexOffset);
			int item = inputIndices[i];
			int key = @int.x + @int.y * width + @int.z * widthHeight;
			if (indexToIndices.TryGetValue(key, out item))
			{
				outputIndices.Add(in item);
				continue;
			}
			indexToIndices.Add(key, value);
			ref NativeList<float3> reference = ref outputVertices;
			float3 value2 = inputVertices[i];
			reference.Add(in value2);
			outputIndices.Add(in value);
			value++;
		}
	}
}
