using System;
using CompanionServer.Cameras;
using Facepunch.MarchingCubes;
using Facepunch.NativeMeshSimplification;
using Instancing;
using Rust.Water5;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

[Unity.Jobs.DOTSCompilerGenerated]
internal class __JobReflectionRegistrationOutput__1221673671587648887
{
	public static void CreateJobReflectionData()
	{
		try
		{
			IJobExtensions.EarlyJobInit<FishShoal.FishCollisionGatherJob>();
			IJobExtensions.EarlyJobInit<FishShoal.FishCollisionProcessJob>();
			IJobParallelForExtensions.EarlyJobInit<FishShoal.FishUpdateJob>();
			IJobExtensions.EarlyJobInit<FishShoal.KillFish>();
			IJobParallelForTransformExtensions.EarlyJobInit<TransformLineRenderer.LineRendererUpdateJob>();
			IJobExtensions.EarlyJobInit<AddAndBlurSphereJob>();
			IJobForExtensions.EarlyJobInit<BoxBlur3DJob>();
			IJobExtensions.EarlyJobInit<BoxBlurCylinderJob>();
			IJobExtensions.EarlyJobInit<BoxBlurSphereJob>();
			IJobExtensions.EarlyJobInit<CarveAndBlurCylinderJob>();
			IJobExtensions.EarlyJobInit<CarveAndBlurSphereJob>();
			IJobExtensions.EarlyJobInit<CleanFloatingIslandsJob>();
			IJobExtensions.EarlyJobInit<PostCullingJob>();
			IJobExtensions.EarlyJobInit<RaycastSamplePositionsJob>();
			IJobExtensions.EarlyJobInit<RaycastBufferSetupJob>();
			IJobParallelForExtensions.EarlyJobInit<RaycastRaySetupJob>();
			IJobParallelForExtensions.EarlyJobInit<RaycastRayProcessingJob>();
			IJobExtensions.EarlyJobInit<RaycastOutputCompressJob>();
			IJobExtensions.EarlyJobInit<RaycastColliderProcessingJob>();
			IJobExtensions.EarlyJobInit<PreCullingJob>();
			IJobExtensions.EarlyJobInit<CopyBackJob>();
			IJobExtensions.EarlyJobInit<PopulateArraysJob>();
			IJobExtensions.EarlyJobInit<SimplifyMeshJob>();
			IJobParallelForExtensions.EarlyJobInit<BakePhysicsMeshesJob>();
			IJobExtensions.EarlyJobInit<CleanupDuplicateVerticesJob>();
			IJobExtensions.EarlyJobInit<MarchJob>();
			IJobExtensions.EarlyJobInit<GetHeightBatchedJob>();
		}
		catch (Exception ex)
		{
			EarlyInitHelpers.JobReflectionDataCreationFailed(ex);
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
	public static void EarlyInit()
	{
		CreateJobReflectionData();
	}
}
