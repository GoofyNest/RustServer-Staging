using UnityEngine;

public class ItemModUnwrap : ItemMod
{
	public static readonly Translate.Phrase UnwrapGiftTitle = new Translate.Phrase("unwrap_gift", "Unwrap");

	public static readonly Translate.Phrase UnwrapGiftDesc = new Translate.Phrase("unwrap_gift_desc", "Unwrap the gift");

	public LootSpawn revealList;

	public GameObjectRef successEffect;

	public int minTries = 1;

	public int maxTries = 1;

	public override void ServerCommand(Item item, string command, BasePlayer player)
	{
		if (command == "unwrap" && item.amount > 0)
		{
			item.UseItem();
			int num = Random.Range(minTries, maxTries + 1);
			for (int i = 0; i < num; i++)
			{
				revealList.SpawnIntoContainer(player.inventory.containerMain);
			}
			if (successEffect.isValid)
			{
				Effect.server.Run(successEffect.resourcePath, player.eyes.position);
			}
		}
	}
}
