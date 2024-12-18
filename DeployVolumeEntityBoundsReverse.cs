using System;
using System.Collections.Generic;
using Facepunch;
using UnityEngine;

public class DeployVolumeEntityBoundsReverse : DeployVolume
{
	private Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

	private int layer;

	protected override bool Check(Vector3 position, Quaternion rotation, int mask = -1)
	{
		position += rotation * bounds.center;
		OBB test = new OBB(position, bounds.size, rotation);
		List<BaseEntity> obj = Pool.Get<List<BaseEntity>>();
		Vis.Entities(position, test.extents.magnitude, obj, (int)layers & mask);
		foreach (BaseEntity item in obj)
		{
			DeployVolume[] volumes = PrefabAttribute.server.FindAll<DeployVolume>(item.prefabID);
			if (DeployVolume.Check(item.transform.position, item.transform.rotation, volumes, test, 1 << layer))
			{
				Pool.FreeUnmanaged(ref obj);
				return true;
			}
		}
		Pool.FreeUnmanaged(ref obj);
		return false;
	}

	protected override bool Check(Vector3 position, Quaternion rotation, List<Type> allowedTypes, int mask = -1)
	{
		return Check(position, rotation, mask);
	}

	protected override bool Check(Vector3 position, Quaternion rotation, OBB test, int mask = -1)
	{
		return false;
	}

	protected override void AttributeSetup(GameObject rootObj, string name, bool serverside, bool clientside, bool bundling)
	{
		bounds = rootObj.GetComponent<BaseEntity>().bounds;
		layer = rootObj.layer;
	}
}
