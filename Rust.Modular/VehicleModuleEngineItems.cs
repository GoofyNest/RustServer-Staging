using System.Collections.Generic;
using Facepunch;
using UnityEngine;

namespace Rust.Modular;

[CreateAssetMenu(fileName = "Vehicle Module Engine Items", menuName = "Scriptable Object/Vehicles/Module Engine Items")]
public class VehicleModuleEngineItems : ScriptableObject
{
	[SerializeField]
	private ItemModEngineItem[] engineItems;

	public bool TryGetItem(int tier, EngineStorage.EngineItemTypes type, out ItemModEngineItem output)
	{
		List<ItemModEngineItem> obj = Pool.Get<List<ItemModEngineItem>>();
		bool result = false;
		output = null;
		ItemModEngineItem[] array = engineItems;
		foreach (ItemModEngineItem itemModEngineItem in array)
		{
			if (itemModEngineItem.tier == tier && itemModEngineItem.engineItemType == type)
			{
				obj.Add(itemModEngineItem);
			}
		}
		if (obj.Count > 0)
		{
			output = obj.GetRandom();
			result = true;
		}
		Pool.FreeUnmanaged(ref obj);
		return result;
	}
}
