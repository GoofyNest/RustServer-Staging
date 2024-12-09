using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Facepunch.MarchingCubes;

[BurstCompile]
internal struct BakePhysicsMeshesJob : IJobParallelFor
{
	[global::Unity.Collections.ReadOnly]
	public NativeArray<int> MeshIds;

	public void Execute(int index)
	{
		Physics.BakeMesh(MeshIds[index], convex: false);
	}
}
