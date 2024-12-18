using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Facepunch.MarchingCubes;

[GenerateTestsForBurstCompatibility]
public struct Point3DGrid : IDisposable
{
	private readonly NativeBitArray _array;

	private readonly int _width;

	private readonly int _height;

	private readonly int _depth;

	private readonly int3 _bounds;

	private readonly int _widthHeight;

	private bool _hasDisposed;

	public int Width => _width;

	public int Height => _height;

	public int Depth => _depth;

	public int Length => _array.Length;

	public int3 Bounds => _bounds;

	public bool this[int directIndex]
	{
		get
		{
			return _array.IsSet(directIndex);
		}
		set
		{
			_array.Set(directIndex, value);
		}
	}

	public bool this[int x, int y, int z]
	{
		get
		{
			return _array.IsSet(ToIndex(x, y, z));
		}
		set
		{
			_array.Set(ToIndex(x, y, z), value);
		}
	}

	public bool this[int3 p]
	{
		get
		{
			return this[p.x, p.y, p.z];
		}
		set
		{
			this[p.x, p.y, p.z] = value;
		}
	}

	public Point3DGrid(int width, int height, int depth)
	{
		_width = width;
		_height = height;
		_depth = depth;
		_bounds = new int3(_width, _height, _depth);
		_widthHeight = _width * _height;
		_array = new NativeBitArray(_widthHeight * _depth, Allocator.Persistent);
		_hasDisposed = false;
	}

	public void Clear()
	{
		_array.Clear();
	}

	public void CopyToByteArray(ref byte[] arr)
	{
		NativeArray<byte> nativeArray = _array.AsNativeArray<byte>();
		if (arr.Length < nativeArray.Length)
		{
			arr = new byte[nativeArray.Length];
		}
		nativeArray.CopyTo(arr);
	}

	public unsafe void CopyFromByteArray(byte[] arr, int count)
	{
		NativeArray<byte> nativeArray = _array.AsNativeArray<byte>();
		if (count != nativeArray.Length)
		{
			Debug.LogError("Trying to load non-matching sized grid");
			return;
		}
		fixed (byte* source = arr)
		{
			UnsafeUtility.MemCpy(nativeArray.GetUnsafePtr(), source, count * UnsafeUtility.SizeOf<byte>());
		}
	}

	public void CopyFromNativeBitArray(ref NativeBitArray other)
	{
		_array.Copy(0, ref other, 0, _array.Length);
	}

	public bool InBounds(int3 p)
	{
		if (!math.any(p < 0))
		{
			return !math.any(p >= _bounds);
		}
		return false;
	}

	public bool InBoundsNotTouching(int3 p)
	{
		if (!math.any(p < 1))
		{
			return !math.any(p >= _bounds - new int3(1));
		}
		return false;
	}

	public int ToIndex(int3 p)
	{
		return ToIndex(p.x, p.y, p.z);
	}

	public int ToIndex(int x, int y, int z)
	{
		return x + y * Width + z * _widthHeight;
	}

	public float Sample(int3 localPosition)
	{
		if (!this[localPosition])
		{
			return 0f;
		}
		return 1f;
	}

	public void Dispose()
	{
		if (!_hasDisposed)
		{
			_array.Dispose();
			_hasDisposed = true;
		}
	}
}
