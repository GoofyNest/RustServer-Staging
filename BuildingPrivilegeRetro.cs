using System;
using System.Collections.Generic;
using Facepunch;
using ProtoBuf;
using UnityEngine;

public class BuildingPrivilegeRetro : BuildingPrivlidge
{
	[Serializable]
	public struct ToolSetting
	{
		public ItemDefinition item;

		public Transform[] parents;
	}

	[Serializable]
	public struct ToolModel
	{
		public ItemDefinition item;

		public GameObjectRef model;
	}

	public BuildingPrivilegeRetroScreen screens;

	public GameObjectRef[] boxPrefabs;

	public GameObjectRef[] doubleBoxPrefabs;

	public int boxesAmount = 12;

	public Transform boxesParent;

	public Vector3 boxSpacing = new Vector3(0.33f, 0.3f, 0.3f);

	public ToolSetting[] toolSettings;

	public ToolModel[] toolCustomModels;

	public Material hammerOriginalMaterial;

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.buildingPrivilegeRetro = Pool.Get<ProtoBuf.BuildingPrivilegeRetro>();
		if (info.forDisk)
		{
			return;
		}
		List<float> list = Pool.Get<List<float>>();
		list.Add(GetResourceProportion(-151838493));
		list.Add(GetResourceProportion(-2099697608));
		list.Add(GetResourceProportion(69511070));
		list.Add(GetResourceProportion(317398316));
		info.msg.buildingPrivilegeRetro.resources = list;
		info.msg.buildingPrivilegeRetro.tools = Pool.Get<List<BuildingPrivilegeRetroTool>>();
		for (int i = 24; i <= 28; i++)
		{
			Item slot = base.inventory.GetSlot(i);
			BuildingPrivilegeRetroTool buildingPrivilegeRetroTool = new BuildingPrivilegeRetroTool();
			if (slot != null)
			{
				foreach (ItemDefinition allowedConstructionItem in allowedConstructionItems)
				{
					if (slot.info.itemid == allowedConstructionItem.itemid)
					{
						buildingPrivilegeRetroTool.itemID = allowedConstructionItem.itemid;
						buildingPrivilegeRetroTool.skinid = slot.skin;
					}
				}
			}
			info.msg.buildingPrivilegeRetro.tools.Add(buildingPrivilegeRetroTool);
		}
	}

	private float GetResourceProportion(int id)
	{
		int amount = base.inventory.GetAmount(id, onlyUsableAmounts: false);
		float num = ItemManager.FindItemDefinition(id).stackable;
		return (float)amount / ((float)(base.inventory.capacity - 5) * num);
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		_ = info.msg.buildingPrivilegeRetro;
	}
}
