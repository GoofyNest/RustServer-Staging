using UnityEngine;

public class ItemInformationPanel : MonoBehaviour
{
	public bool ForceHidden(ItemDefinition info)
	{
		if (info == null)
		{
			return false;
		}
		return info.GetComponent<ItemModHideInfoPanel>() != null;
	}

	public virtual bool EligableForDisplay(ItemDefinition info)
	{
		Debug.LogWarning("ItemInformationPanel.EligableForDisplay");
		return false;
	}

	public virtual void SetupForItem(ItemDefinition info, Item item = null)
	{
		Debug.LogWarning("ItemInformationPanel.SetupForItem");
	}
}
