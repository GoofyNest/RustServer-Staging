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

	private BufferList<MarchingCubesGenerator> _toAssignPhysics;

	private JobHandle _physicsBakeHandle;

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
		_toAssignPhysics = new BufferList<MarchingCubesGenerator>();
	}

	public void FixedUpdate()
	{
		if (_toAssignPhysics.Count == 0)
		{
			return;
		}
		using (TimeWarning.New("PhysicsBakeComplete"))
		{
			_physicsBakeHandle.Complete();
			_physicsBakeHandle = default(JobHandle);
		}
		using (TimeWarning.New("PhysicsMeshAssign"))
		{
			foreach (MarchingCubesGenerator toAssignPhysic in _toAssignPhysics)
			{
				toAssignPhysic.MeshCollider.sharedMesh = toAssignPhysic.Mesh;
			}
		}
		_toAssignPhysics.Clear();
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

	private void ProcessQueue()
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
				_toAssignPhysics.Add(_cubesWaitingForGeneration[j]);
			}
			_physicsBakeHandle = IJobParallelForExtensions.Schedule(new BakePhysicsMeshesJob
			{
				MeshIds = meshIds
			}, count, 1, _physicsBakeHandle);
			meshIds.Dispose(_physicsBakeHandle);
			_cubesWaitingForGeneration.Clear();
		}
	}
}
