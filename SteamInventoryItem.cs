using UnityEngine;

[CreateAssetMenu(menuName = "Rust/Skins/Inventory Item")]
public class SteamInventoryItem : ScriptableObject
{
	public enum Category
	{
		None,
		Clothing,
		Weapon,
		Decoration,
		Crate,
		Resource
	}

	public enum SubCategory
	{
		None,
		Shirt,
		Pants,
		Jacket,
		Hat,
		Mask,
		Footwear,
		Weapon,
		Misc,
		Crate,
		Resource,
		CrateUncraftable
	}

	public int id;

	public Sprite icon;

	public Translate.Phrase displayName;

	public Translate.Phrase displayDescription;

	[Header("Steam Inventory")]
	public Category category;

	public SubCategory subcategory;

	public SteamInventoryCategory steamCategory;

	public bool isLimitedTimeOffer = true;

	[Tooltip("Stop this item being broken down into cloth etc")]
	public bool PreventBreakingDown;

	public bool IsTwitchDrop;

	[Header("Meta")]
	public string itemname;

	public ulong workshopID;

	public SteamDLCItem DlcItem;

	[Tooltip("Does nothing currently")]
	public bool forceCraftableItemDesc;

	[Tooltip("If enabled the item store will not show this as a 3d model")]
	public bool forceDisableTurntableInItemStore;

	public ItemDefinition itemDefinition => ItemManager.FindItemDefinition(itemname);

	public virtual bool HasUnlocked(ulong playerId)
	{
		if (DlcItem != null && DlcItem.HasLicense(playerId))
		{
			return true;
		}
		return false;
	}
}
