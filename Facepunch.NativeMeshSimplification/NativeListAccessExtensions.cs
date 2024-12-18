using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Facepunch.NativeMeshSimplification;

public static class NativeListAccessExtensions
{
	[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
	private static void ArrayBoundsCheck<T>(in NativeList<T> list, int i) where T : unmanaged
	{
		if (i < 0 || i >= list.Length)
		{
			throw new IndexOutOfRangeException();
		}
	}

	public unsafe static ref T Get<T>(this in NativeList<T> list, int index) where T : unmanaged
	{
		return ref list.GetUnsafePtr()[index];
	}

	public unsafe static ref readonly T GetReadonly<T>(this in NativeList<T> list, int index) where T : unmanaged
	{
		return ref list.GetUnsafeReadOnlyPtr()[index];
	}
}
