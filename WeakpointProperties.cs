using System;
using UnityEngine;

public class WeakpointProperties : PrefabAttribute
{
	public Vector3 Position;

	public bool BlockWhenRoofAttached;

	protected override Type GetIndexedType()
	{
		return typeof(WeakpointProperties);
	}
}
