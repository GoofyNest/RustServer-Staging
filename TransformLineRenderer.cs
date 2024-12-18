using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

public class TransformLineRenderer : MonoBehaviour, IClientComponent
{
	internal struct LineRendererUpdateJob : IJobParallelForTransform
	{
		[WriteOnly]
		[NativeMatchesParallelForLength]
		public NativeArray<Vector3> ResultWorldPositions;

		public void Execute(int index, [Unity.Collections.ReadOnly] TransformAccess transform)
		{
			ResultWorldPositions[index] = transform.position;
		}
	}

	public Transform[] TransformSequence;

	public LineRenderer TargetRenderer;
}
