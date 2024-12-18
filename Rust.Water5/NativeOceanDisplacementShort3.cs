using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Rust.Water5;

internal struct NativeOceanDisplacementShort3 : IDisposable
{
	private NativeArray<OceanDisplacementShort3> _arr;

	private int spectrumCount;

	private int frameCount;

	public OceanDisplacementShort3 this[int x, int y, int z]
	{
		get
		{
			return _arr[z * spectrumCount * frameCount + y * spectrumCount + x];
		}
		set
		{
			_arr[z * spectrumCount * frameCount + y * spectrumCount + x] = value;
		}
	}

	public int Length => _arr.Length;

	public static NativeOceanDisplacementShort3 Create(int x, int y, int z)
	{
		NativeOceanDisplacementShort3 result = default(NativeOceanDisplacementShort3);
		result._arr = new NativeArray<OceanDisplacementShort3>(x * y * z, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
		result.spectrumCount = x;
		result.frameCount = y;
		return result;
	}

	public static NativeOceanDisplacementShort3 Create(OceanDisplacementShort3[,,] simData)
	{
		NativeOceanDisplacementShort3 result = default(NativeOceanDisplacementShort3);
		result._arr = new NativeArray<OceanDisplacementShort3>(simData.Length, Allocator.Persistent);
		result.spectrumCount = simData.GetLength(0);
		result.frameCount = simData.GetLength(1);
		for (int i = 0; i < result.spectrumCount; i++)
		{
			for (int j = 0; j < result.frameCount; j++)
			{
				for (int k = 0; k < simData.GetLength(2); k++)
				{
					result._arr[i * result.spectrumCount + j * result.frameCount + k] = simData[i, j, k];
				}
			}
		}
		return result;
	}

	public unsafe OceanDisplacementShort3* GetUnsafePtr()
	{
		if (!_arr.IsCreated)
		{
			return null;
		}
		return (OceanDisplacementShort3*)_arr.GetUnsafePtr();
	}

	public NativeArray<OceanDisplacementShort3>.ReadOnly GetNativeRawReadOnly()
	{
		return _arr.AsReadOnly();
	}

	public NativeArray<OceanDisplacementShort3> GetNativeRaw()
	{
		return _arr;
	}

	public void Dispose()
	{
		_arr.Dispose();
	}
}
