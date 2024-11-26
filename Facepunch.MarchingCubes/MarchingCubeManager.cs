using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Facepunch.MarchingCubes;

public class MarchingCubeManager : FacepunchBehaviour
{
	private static MarchingCubeManager _instance;

	public List<MarchingCubesGenerator> AllCubesList;

	private ListHashSet<MarchingCubesGenerator> _cubesWaitingForGeneration;

	public static MarchingCubeManager Instance
	{
		get
		{
			if ((object)_instance != null)
			{
				return _instance;
			}
			GameObject obj = new GameObject("MarchingCubeManager");
			Object.DontDestroyOnLoad(obj);
			_instance = obj.AddComponent<MarchingCubeManager>();
			return _instance;
		}
	}

	public void Awake()
	{
		AllCubesList = new List<MarchingCubesGenerator>();
		_cubesWaitingForGeneration = new ListHashSet<MarchingCubesGenerator>();
	}

	public void LateUpdate()
	{
		ProcessQueue();
	}

	public void Add(MarchingCubesGenerator cubes)
	{
		AllCubesList.Add(cubes);
	}

	public void Remove(MarchingCubesGenerator cubes)
	{
		AllCubesList.Remove(cubes);
	}

	public void EnqueueUpdate(MarchingCubesGenerator cubes)
	{
		_cubesWaitingForGeneration.TryAdd(cubes);
	}

	public void ProcessQueue()
	{
		if (_cubesWaitingForGeneration.Count != 0)
		{
			int count = _cubesWaitingForGeneration.Count;
			NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(count, Allocator.Temp);
			for (int i = 0; i < count; i++)
			{
				jobs[i] = _cubesWaitingForGeneration[i].ScheduleMarch();
			}
			JobHandle.CompleteAll(jobs);
			NativeArray<int> meshIds = new NativeArray<int>(_cubesWaitingForGeneration.Count, Allocator.TempJob);
			for (int j = 0; j < count; j++)
			{
				_cubesWaitingForGeneration[j].ApplyUpdate();
				meshIds[j] = _cubesWaitingForGeneration[j].MeshInstanceId;
			}
			BakePhysicsMeshesJob jobData = default(BakePhysicsMeshesJob);
			jobData.MeshIds = meshIds;
			JobHandle inputDeps = IJobParallelForExtensions.Schedule(jobData, count, 1);
			meshIds.Dispose(inputDeps);
			inputDeps.Complete();
			for (int k = 0; k < count; k++)
			{
				_cubesWaitingForGeneration[k].MeshCollider.sharedMesh = _cubesWaitingForGeneration[k].Mesh;
			}
			_cubesWaitingForGeneration.Clear();
		}
	}
}
