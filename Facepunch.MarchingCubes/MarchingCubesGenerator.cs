using System;
using Facepunch.NativeMeshSimplification;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Facepunch.MarchingCubes;

public class MarchingCubesGenerator : IDisposable
{
	private readonly Point3DGrid _sampler;

	private readonly Mesh _meshToUpdate;

	private readonly MeshCollider _meshCollider;

	private readonly NativeMeshSimplifier _simplifier;

	private readonly float3 _offset;

	private readonly float _scale;

	private NativeList<float3> vertices0;

	private NativeList<float3> vertices1;

	private NativeList<float3> vertices2;

	private NativeList<int> indices0;

	private NativeList<int> indices1;

	private NativeList<int> indices2;

	private NativeHashMap<int, int> indexToIndices;

	private float4x4 _transform;

	private static readonly ProfilerMarker p_ApplyUpdate = new ProfilerMarker("MarchingCubes.ApplyUpdate");

	public Mesh Mesh => _meshToUpdate;

	public MeshCollider MeshCollider => _meshCollider;

	public int MeshInstanceId => _meshToUpdate.GetInstanceID();

	public MarchingCubesGenerator(Point3DGrid sampler, Mesh meshToUpdate, MeshCollider meshCollider, float3 offset, float scale)
	{
		_sampler = sampler;
		_meshToUpdate = meshToUpdate;
		_meshCollider = meshCollider;
		_simplifier = new NativeMeshSimplifier();
		_offset = offset;
		_scale = scale;
		vertices0 = new NativeList<float3>(Allocator.Persistent);
		vertices1 = new NativeList<float3>(Allocator.Persistent);
		vertices2 = new NativeList<float3>(Allocator.Persistent);
		indices0 = new NativeList<int>(Allocator.Persistent);
		indices1 = new NativeList<int>(Allocator.Persistent);
		indices2 = new NativeList<int>(Allocator.Persistent);
		indexToIndices = new NativeHashMap<int, int>(0, Allocator.Persistent);
		MarchingCubeManager.Instance.Add(this);
	}

	public JobHandle ScheduleMarch()
	{
		float3 vertexOffset = new float3((float)_sampler.Width * 0.5f, (float)_sampler.Height * 0.5f, (float)_sampler.Depth * 0.5f) + _offset;
		MarchJob marchJob = default(MarchJob);
		marchJob.sampler = _sampler;
		marchJob.vertices = vertices0;
		marchJob.indices = indices0;
		marchJob.vertexOffset = vertexOffset;
		marchJob.scale = _scale;
		MarchJob jobData = marchJob;
		CleanupDuplicateVerticesJob cleanupDuplicateVerticesJob = default(CleanupDuplicateVerticesJob);
		cleanupDuplicateVerticesJob.inputVertices = vertices0;
		cleanupDuplicateVerticesJob.inputIndices = indices0;
		cleanupDuplicateVerticesJob.outputVertices = vertices1;
		cleanupDuplicateVerticesJob.outputIndices = indices1;
		cleanupDuplicateVerticesJob.indexToIndices = indexToIndices;
		cleanupDuplicateVerticesJob.vertexOffset = vertexOffset;
		cleanupDuplicateVerticesJob.invScale = math.rcp(_scale);
		cleanupDuplicateVerticesJob.width = _sampler.Width;
		cleanupDuplicateVerticesJob.widthHeight = _sampler.Height * _sampler.Width;
		CleanupDuplicateVerticesJob jobData2 = cleanupDuplicateVerticesJob;
		JobHandle dependsOn = jobData.Schedule();
		return jobData2.Schedule(dependsOn);
	}

	public JobHandle ScheduleSimplification(JobHandle inputDeps)
	{
		return _simplifier.ScheduleMeshSimplify(0.4f, vertices1, indices1, vertices2, indices2, inputDeps);
	}

	public void ApplyUpdate(bool fromSimplify = false)
	{
		using (p_ApplyUpdate.Auto())
		{
			_meshToUpdate.Clear();
			NativeList<float3> nativeList = (fromSimplify ? vertices2 : vertices1);
			NativeList<int> nativeList2 = (fromSimplify ? indices2 : indices1);
			_meshToUpdate.SetVertices(nativeList.AsArray(), 0, nativeList.Length);
			_meshToUpdate.SetIndices(nativeList2.AsArray(), 0, nativeList2.Length, MeshTopology.Triangles, 0);
			_meshToUpdate.RecalculateBounds();
			_meshToUpdate.RecalculateNormals();
			if (BaseSculpture.LogMeshStats)
			{
				Debug.Log($"{_meshToUpdate.name} : tris({nativeList2.Length / 3}) verts({nativeList.Length})");
			}
		}
	}

	public void EnqueueUpdate()
	{
		MarchingCubeManager.Instance.EnqueueUpdate(this);
	}

	public void Dispose()
	{
		vertices0.Dispose();
		vertices1.Dispose();
		vertices2.Dispose();
		indices0.Dispose();
		indices1.Dispose();
		indices2.Dispose();
		indexToIndices.Dispose();
		_simplifier.Dispose();
		MarchingCubeManager.Instance.Remove(this);
	}
}
