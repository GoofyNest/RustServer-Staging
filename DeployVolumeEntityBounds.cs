using System;
using System.Collections.Generic;
using UnityEngine;

public class DeployVolumeEntityBounds : DeployVolume
{
	private Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

	protected override bool Check(Vector3 position, Quaternion rotation, int mask = -1)
	{
		position += rotation * bounds.center;
		if (DeployVolume.CheckOBB(new OBB(position, bounds.size, rotation), (int)layers & mask, this))
		{
			return true;
		}
		return false;
	}

	protected override bool Check(Vector3 position, Quaternion rotation, List<Type> allowedTypes, int mask = -1)
	{
		return Check(position, rotation, mask);
	}

	protected override bool Check(Vector3 position, Quaternion rotation, OBB obb, int mask = -1)
	{
		return false;
	}

	protected override void AttributeSetup(GameObject rootObj, string name, bool serverside, bool clientside, bool bundling)
	{
		bounds = rootObj.GetComponent<BaseEntity>().bounds;
	}
}
