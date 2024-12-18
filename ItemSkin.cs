using Rust.Workshop;
using UnityEngine;

[CreateAssetMenu(menuName = "Rust/Skins/ItemSkin")]
public class ItemSkin : SteamInventoryItem
{
	public Skinnable Skinnable;

	public Material[] Materials;

	[Tooltip("If set, whenever we make an item with this skin, we'll spawn this item without a skin instead")]
	public ItemDefinition Redirect;

	public SteamInventoryItem UnlockedViaSteamItem;

	public bool UnlockedByDefault;

	public void ApplySkin(GameObject obj)
	{
		if (!(Skinnable == null))
		{
			Skin.Apply(obj, Skinnable, Materials);
		}
	}

	public override bool HasUnlocked(ulong playerId)
	{
		if (UnlockedByDefault)
		{
			return true;
		}
		if (Redirect != null && Redirect.isRedirectOf != null && Redirect.isRedirectOf.steamItem != null)
		{
			BasePlayer basePlayer = BasePlayer.FindByID(playerId);
			if (basePlayer != null && basePlayer.blueprints.CheckSkinOwnership(Redirect.isRedirectOf.steamItem.id, basePlayer.userID))
			{
				return true;
			}
		}
		if (UnlockedViaSteamItem != null)
		{
			BasePlayer basePlayer2 = BasePlayer.FindByID(playerId);
			if (basePlayer2 != null && basePlayer2.blueprints.CheckSkinOwnership(UnlockedViaSteamItem.id, basePlayer2.userID))
			{
				return true;
			}
		}
		return base.HasUnlocked(playerId);
	}
}
