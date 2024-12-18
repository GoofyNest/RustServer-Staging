using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Rust.Water5;

[BurstCompile]
internal struct GetHeightBatchedJob : IJob
{
	public NativeArray<float3> Positions;

	public int Count;

	public float OneOverOctave0Scale;

	public NativeOceanDisplacementShort3 SimData;

	public int Spectrum0;

	public int Spectrum1;

	public int Frame0;

	public int Frame1;

	public float spectrumBlend;

	public float frameBlend;

	public void Execute()
	{
		for (int i = 0; i < Count; i++)
		{
			Positions[i] = GetHeightRaw(Positions[i].xyz);
		}
	}

	private float GetHeightRaw(float3 position)
	{
		float3 zero = float3.zero;
		zero = GetDisplacement(position);
		zero = GetDisplacement(position - zero);
		zero = GetDisplacement(position - zero);
		return GetDisplacement(position - zero).y;
	}

	private Vector3 GetDisplacement(float3 position)
	{
		float normX = position.x * OneOverOctave0Scale;
		float normZ = position.z * OneOverOctave0Scale;
		return GetDisplacement(normX, normZ);
	}

	private float3 GetDisplacement(float normX, float normZ)
	{
		normX -= math.floor(normX);
		normZ -= math.floor(normZ);
		float num = normX * 256f - 0.5f;
		float num2 = normZ * 256f - 0.5f;
		int num3 = (int)math.floor(num);
		int num4 = (int)math.floor(num2);
		float s = num - (float)num3;
		float s2 = num2 - (float)num4;
		int num5 = num3 % 256;
		int num6 = num4 % 256;
		int x = (num5 + 256) % 256;
		int z = (num6 + 256) % 256;
		int x2 = (num5 + 1 + 256) % 256;
		int z2 = (num6 + 1 + 256) % 256;
		float3 displacementFromSimData = GetDisplacementFromSimData(x, z);
		float3 displacementFromSimData2 = GetDisplacementFromSimData(x2, z);
		float3 displacementFromSimData3 = GetDisplacementFromSimData(x, z2);
		float3 displacementFromSimData4 = GetDisplacementFromSimData(x2, z2);
		float3 x3 = math.lerp(displacementFromSimData, displacementFromSimData2, s);
		float3 y = math.lerp(displacementFromSimData3, displacementFromSimData4, s);
		return math.lerp(x3, y, s2);
	}

	private float3 GetDisplacementFromSimData(int x, int z)
	{
		int z2 = x * 256 + z;
		float3 x2 = math.lerp(SimData[Spectrum0, Frame0, z2], SimData[Spectrum1, Frame0, z2], spectrumBlend);
		float3 y = math.lerp(SimData[Spectrum0, Frame1, z2], SimData[Spectrum1, Frame1, z2], spectrumBlend);
		return math.lerp(x2, y, frameBlend);
	}
}
