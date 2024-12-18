using System;
using UnityEngine;

public class Deployable : PrefabAttribute
{
	public Mesh guideMesh;

	public Vector3 guideMeshScale = Vector3.one;

	public bool overrideRotation;

	public Vector3 guideMeshOrientation = Vector3.zero;

	public bool guideLights = true;

	public bool wantsInstanceData;

	public bool copyInventoryFromItem;

	public bool setSocketParent;

	public bool toSlot;

	public BaseEntity.Slot slot;

	public GameObjectRef placeEffect;

	[Tooltip("Only required if the guideMesh is in a significantly different position or there are multiple meshes")]
	public Transform[] guideTargets;

	[NonSerialized]
	public Bounds bounds;

	protected override void AttributeSetup(GameObject rootObj, string name, bool serverside, bool clientside, bool bundling)
	{
		base.AttributeSetup(rootObj, name, serverside, clientside, bundling);
		bounds = rootObj.GetComponent<BaseEntity>().bounds;
	}

	protected override Type GetIndexedType()
	{
		return typeof(Deployable);
	}

	public bool IsGuideTarget(Transform t)
	{
		if (guideTargets != null)
		{
			Transform[] array = guideTargets;
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] == t)
				{
					return true;
				}
			}
		}
		return false;
	}
}
