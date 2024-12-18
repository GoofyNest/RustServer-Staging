#define UNITY_ASSERTIONS
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace Rust.Water5;

public class OceanSimulation : IDisposable
{
	public const int octaveCount = 3;

	public const int simulationSize = 256;

	public const int physicsSimulationSize = 256;

	public const int physicsFrameRate = 4;

	public const int physicsLooptime = 18;

	public const int physicsFrameCount = 72;

	public const float phsyicsDeltaTime = 0.25f;

	public const float oneOverPhysicsSimulationSize = 0.00390625f;

	public const int physicsFrameSize = 65536;

	public const int physicsSpectrumOffset = 4718592;

	private OceanSettings oceanSettings;

	private float[] spectrumRanges;

	private float distanceAttenuationFactor;

	private float depthAttenuationFactor;

	private static float oneOverOctave0Scale;

	private static float[] beaufortValues;

	private int spectrum0;

	private int spectrum1;

	private float spectrumBlend;

	private int frame0;

	private int frame1;

	private float frameBlend;

	private float currentTime;

	private float prevUpdateComputeTime;

	private float deltaTime;

	private NativeOceanDisplacementShort3 nativeSimData;

	private GetHeightBatchedJob _cachedBatchJob;

	private NativeArray<float3> _batchPositionQueryArr;

	public int Spectrum0 => spectrum0;

	public int Spectrum1 => spectrum1;

	public float SpectrumBlend => spectrumBlend;

	public int Frame0 => frame0;

	public int Frame1 => frame1;

	public float FrameBlend => frameBlend;

	public OceanSimulation(OceanSettings oceanSettings)
	{
		this.oceanSettings = oceanSettings;
		oneOverOctave0Scale = 1f / oceanSettings.octaveScales[0];
		beaufortValues = new float[oceanSettings.spectrumSettings.Length];
		for (int i = 0; i < oceanSettings.spectrumSettings.Length; i++)
		{
			beaufortValues[i] = oceanSettings.spectrumSettings[i].beaufort;
		}
		nativeSimData = oceanSettings.LoadNativeSimData();
		spectrumRanges = oceanSettings.spectrumRanges;
		depthAttenuationFactor = oceanSettings.depthAttenuationFactor;
		distanceAttenuationFactor = oceanSettings.distanceAttenuationFactor;
		_cachedBatchJob = new GetHeightBatchedJob
		{
			SimData = nativeSimData
		};
	}

	public void Update(float time, float dt, float beaufort)
	{
		currentTime = time % 18f;
		deltaTime = dt;
		FindFrames(currentTime, out frame0, out frame1, out frameBlend);
		FindSpectra(beaufort, out spectrum0, out spectrum1, out spectrumBlend);
	}

	private static void FindSpectra(float beaufort, out int spectrum0, out int spectrum1, out float spectrumT)
	{
		beaufort = Mathf.Clamp(beaufort, 0f, 10f);
		spectrum0 = (spectrum1 = 0);
		spectrumT = 0f;
		for (int i = 1; i < beaufortValues.Length; i++)
		{
			float num = beaufortValues[i - 1];
			float num2 = beaufortValues[i];
			if (beaufort >= num && beaufort <= num2)
			{
				spectrum0 = i - 1;
				spectrum1 = i;
				spectrumT = math.remap(num, num2, 0f, 1f, beaufort);
				break;
			}
		}
	}

	public static void FindFrames(float time, out int frame0, out int frame1, out float frameBlend)
	{
		frame0 = (int)math.floor(time * 4f);
		frame1 = (int)math.floor(time * 4f);
		frame1 = (frame1 + 1) % 72;
		frameBlend = math.remap((float)frame0 * 0.25f, (float)(frame0 + 1) * 0.25f, 0f, 1f, time);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Trace(Ray ray, float maxDist, out Vector3 result)
	{
		float num = Mathf.Lerp(spectrumRanges[spectrum0], spectrumRanges[spectrum1], spectrumBlend);
		if (num <= 0.1f)
		{
			if (new Plane(Vector3.up, -0f).Raycast(ray, out var enter) && enter < maxDist)
			{
				result = ray.GetPoint(enter);
				return true;
			}
			result = Vector3.zero;
			return false;
		}
		float num2 = 0f - num;
		Vector3 point = ray.GetPoint(maxDist);
		if (ray.origin.y > num && point.y > num)
		{
			result = Vector3.zero;
			return false;
		}
		if (ray.origin.y < num2 && point.y < num2)
		{
			result = Vector3.zero;
			return false;
		}
		Vector3 vector = ray.origin;
		Vector3 direction = ray.direction;
		float num3 = 0f;
		float num4 = 0f;
		float num5 = 2f / (math.abs(direction.y) + 1f);
		result = vector;
		if (direction.y <= -0.99f)
		{
			result.y = GetHeight(vector);
			return math.lengthsq(result - vector) < maxDist * maxDist;
		}
		if (vector.y >= num + 0f)
		{
			num4 = (num3 = (0f - (vector.y - num - 0f)) / direction.y);
			vector += num3 * direction;
			if (num4 >= maxDist)
			{
				result = Vector3.zero;
				return false;
			}
		}
		int num6 = 0;
		while (true)
		{
			float height = GetHeight(vector);
			num3 = num5 * Mathf.Abs(vector.y - height - 0f);
			vector += num3 * direction;
			num4 += num3;
			if (num6 >= 16 || num3 < 0.1f)
			{
				break;
			}
			if (num4 >= maxDist)
			{
				return false;
			}
			num6++;
		}
		if (num3 < 0.1f && num4 >= 0f)
		{
			result = vector;
			return true;
		}
		if (direction.y < 0f)
		{
			num3 = (0f - (vector.y + num - 0f)) / direction.y;
			Vector3 vector2 = vector;
			Vector3 vector3 = vector + num3 * ray.direction;
			for (int i = 0; i < 16; i++)
			{
				vector = (vector2 + vector3) * 0.5f;
				float height2 = GetHeight(vector);
				if (vector.y - height2 - 0f > 0f)
				{
					vector2 = vector;
				}
				else
				{
					vector3 = vector;
				}
				if (math.abs(vector.y - height2) < 0.1f)
				{
					vector.y = height2;
					break;
				}
			}
			result = vector;
			return true;
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public float MinLevel()
	{
		return 0f - Mathf.Lerp(spectrumRanges[spectrum0], spectrumRanges[spectrum1], spectrumBlend);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public float MaxLevel()
	{
		return Mathf.Lerp(spectrumRanges[spectrum0], spectrumRanges[spectrum1], spectrumBlend);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float GetHeight(Vector3[,,] simData, Vector3 position, float time, float beaufort, float distAttenFactor, float depthAttenFactor)
	{
		float x = TerrainMeta.Position.x;
		float z = TerrainMeta.Position.z;
		float x2 = TerrainMeta.OneOverSize.x;
		float z2 = TerrainMeta.OneOverSize.z;
		float x3 = (position.x - x) * x2;
		float y = (position.z - z) * z2;
		Vector2 uv = new Vector2(x3, y);
		float num = ((TerrainTexturing.Instance != null) ? TerrainTexturing.Instance.GetCoarseDistanceToShore(uv) : 0f);
		float f = ((TerrainMeta.HeightMap != null) ? TerrainMeta.HeightMap.GetHeightFast(uv) : 0f);
		float num2 = Mathf.Clamp01(num / distAttenFactor);
		float num3 = Mathf.Clamp01(Mathf.Abs(f) / depthAttenFactor);
		Vector3 zero = Vector3.zero;
		zero = GetDisplacement(simData, position, time, beaufort);
		zero = GetDisplacement(simData, position - zero, time, beaufort);
		zero = GetDisplacement(simData, position - zero, time, beaufort);
		return GetDisplacement(simData, position - zero, time, beaufort).y * num2 * num3;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector3 GetDisplacement(Vector3[,,] simData, Vector3 position, float time, float beaufort)
	{
		FindFrames(time, out var num, out var num2, out var num3);
		FindSpectra(beaufort, out var num4, out var num5, out var spectrumT);
		return GetDisplacement(simData, position, num, num2, num3, num4, num5, spectrumT);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector3 GetDisplacement(Vector3[,,] simData, Vector3 position, int frame0, int frame1, float frameBlend, int spectrum0, int spectrum1, float spectrumBlend)
	{
		float normX = position.x * oneOverOctave0Scale;
		float normZ = position.z * oneOverOctave0Scale;
		return GetDisplacement(simData, normX, normZ, frame0, frame1, frameBlend, spectrum0, spectrum1, spectrumBlend);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector3 GetDisplacement(Vector3[,,] simData, float normX, float normZ, int frame0, int frame1, float frameBlend, int spectrum0, int spectrum1, float spectrumBlend)
	{
		normX -= Mathf.Floor(normX);
		normZ -= Mathf.Floor(normZ);
		float num = normX * 256f - 0.5f;
		float num2 = normZ * 256f - 0.5f;
		int num3 = Mathf.FloorToInt(num);
		int num4 = Mathf.FloorToInt(num2);
		float t = num - (float)num3;
		float t2 = num2 - (float)num4;
		int x = num3;
		int y = num4;
		int x2 = num3 + 1;
		int y2 = num4 + 1;
		Vector3 displacement = GetDisplacement(simData, x, y, frame0, frame1, frameBlend, spectrum0, spectrum1, spectrumBlend);
		Vector3 displacement2 = GetDisplacement(simData, x2, y, frame0, frame1, frameBlend, spectrum0, spectrum1, spectrumBlend);
		Vector3 displacement3 = GetDisplacement(simData, x, y2, frame0, frame1, frameBlend, spectrum0, spectrum1, spectrumBlend);
		Vector3 displacement4 = GetDisplacement(simData, x2, y2, frame0, frame1, frameBlend, spectrum0, spectrum1, spectrumBlend);
		Vector3 a = Vector3.LerpUnclamped(displacement, displacement2, t);
		Vector3 b = Vector3.LerpUnclamped(displacement3, displacement4, t);
		return Vector3.LerpUnclamped(a, b, t2);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector3 GetDisplacement(Vector3[,,] simData, int x, int y, int frame0, int frame1, float frameBlend, int spectrum0, int spectrum1, float spectrumBlend)
	{
		int num = x * 256 + y;
		Vector3 a = Vector3.LerpUnclamped(simData[spectrum0, frame0, num], simData[spectrum1, frame0, num], spectrumBlend);
		Vector3 b = Vector3.LerpUnclamped(simData[spectrum0, frame1, num], simData[spectrum1, frame1, num], spectrumBlend);
		return Vector3.LerpUnclamped(a, b, frameBlend);
	}

	public void Dispose()
	{
		if (_batchPositionQueryArr.IsCreated)
		{
			_batchPositionQueryArr.Dispose();
		}
		nativeSimData.Dispose();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void GetHeightBatch(Vector2[] positions, float[] heights, float[] shore, float[] terrainHeight)
	{
		Assert.IsTrue(positions.Length == heights.Length);
		PopulateBatchNative(positions);
		_cachedBatchJob.OneOverOctave0Scale = oneOverOctave0Scale;
		_cachedBatchJob.Frame0 = frame0;
		_cachedBatchJob.Frame1 = frame1;
		_cachedBatchJob.Spectrum0 = spectrum0;
		_cachedBatchJob.Spectrum1 = spectrum1;
		_cachedBatchJob.spectrumBlend = spectrumBlend;
		_cachedBatchJob.frameBlend = frameBlend;
		_cachedBatchJob.Positions = _batchPositionQueryArr;
		_cachedBatchJob.Count = positions.Length;
		IJobExtensions.RunByRef(ref _cachedBatchJob);
		for (int i = 0; i < heights.Length; i++)
		{
			heights[i] = _batchPositionQueryArr[i].x * GetHeightAttenuation(shore[i], terrainHeight[i]);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public float GetHeight(Vector3 position)
	{
		float heightAttenuation = GetHeightAttenuation(position);
		_cachedBatchJob.OneOverOctave0Scale = oneOverOctave0Scale;
		_cachedBatchJob.Frame0 = frame0;
		_cachedBatchJob.Frame1 = frame1;
		_cachedBatchJob.Spectrum0 = spectrum0;
		_cachedBatchJob.Spectrum1 = spectrum1;
		_cachedBatchJob.spectrumBlend = spectrumBlend;
		_cachedBatchJob.frameBlend = frameBlend;
		PopulateBatchNative(position);
		_cachedBatchJob.Positions = _batchPositionQueryArr;
		_cachedBatchJob.Count = 1;
		IJobExtensions.RunByRef(ref _cachedBatchJob);
		return _cachedBatchJob.Positions[0].x * heightAttenuation;
	}

	private void PopulateBatchNative(Vector3 position)
	{
		if (!_batchPositionQueryArr.IsCreated || _batchPositionQueryArr.Length < 1)
		{
			if (_batchPositionQueryArr.IsCreated)
			{
				_batchPositionQueryArr.Dispose();
			}
			_batchPositionQueryArr = new NativeArray<float3>(1, Allocator.Persistent);
		}
		_batchPositionQueryArr[0] = position;
	}

	private void PopulateBatchNative(Vector2[] positions)
	{
		if (!_batchPositionQueryArr.IsCreated || _batchPositionQueryArr.Length < positions.Length)
		{
			if (_batchPositionQueryArr.IsCreated)
			{
				_batchPositionQueryArr.Dispose();
			}
			_batchPositionQueryArr = new NativeArray<float3>(positions.Length, Allocator.Persistent);
		}
		for (int i = 0; i < positions.Length; i++)
		{
			_batchPositionQueryArr[i] = positions[i].XZ3D();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public float GetHeightAttenuation(float shore, float terrainHeight)
	{
		float num = Mathf.Clamp01(shore / distanceAttenuationFactor);
		float num2 = Mathf.Clamp01(Mathf.Abs(terrainHeight) / depthAttenuationFactor);
		return num * num2;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public float GetHeightAttenuation(Vector3 position)
	{
		float x = TerrainMeta.Position.x;
		float z = TerrainMeta.Position.z;
		float x2 = TerrainMeta.OneOverSize.x;
		float z2 = TerrainMeta.OneOverSize.z;
		float x3 = (position.x - x) * x2;
		float y = (position.z - z) * z2;
		Vector2 uv = new Vector2(x3, y);
		float num = ((TerrainTexturing.Instance != null) ? TerrainTexturing.Instance.GetCoarseDistanceToShore(uv) : 0f);
		float f = ((TerrainMeta.HeightMap != null) ? TerrainMeta.HeightMap.GetHeightFast(uv) : 0f);
		float num2 = Mathf.Clamp01(num / distanceAttenuationFactor);
		float num3 = Mathf.Clamp01(Mathf.Abs(f) / depthAttenuationFactor);
		return num2 * num3;
	}
}
