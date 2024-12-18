using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Facepunch;
using Newtonsoft.Json;
using Steamworks;
using UnityEngine;

namespace ConVar;

[Factory("inventory")]
public class Inventory : ConsoleSystem
{
	public class SavedLoadout
	{
		public struct SavedItem
		{
			public int id;

			public int amount;

			public ulong skin;

			public int[] containedItems;

			public int blueprintTarget;
		}

		public SavedItem[] belt;

		public SavedItem[] wear;

		public SavedItem[] main;

		public SavedItem[] backpack;

		public int heldItemIndex;

		public SavedLoadout()
		{
		}

		public SavedLoadout(BasePlayer player)
		{
			belt = SaveItems(player.inventory.containerBelt);
			wear = SaveItems(player.inventory.containerWear);
			main = SaveItems(player.inventory.containerMain);
			Item backpackWithInventory = player.inventory.GetBackpackWithInventory();
			if (backpackWithInventory != null)
			{
				backpack = SaveItems(backpackWithInventory.contents);
			}
			heldItemIndex = GetSlotIndex(player);
		}

		public SavedLoadout(PlayerInventoryProperties properties)
		{
			belt = SaveItems(properties.belt);
			wear = SaveItems(properties.wear);
			main = SaveItems(properties.main);
			heldItemIndex = 0;
		}

		private static SavedItem[] SaveItems(ItemContainer itemContainer)
		{
			List<SavedItem> list = new List<SavedItem>();
			for (int i = 0; i < itemContainer.capacity; i++)
			{
				Item slot = itemContainer.GetSlot(i);
				if (slot == null)
				{
					continue;
				}
				SavedItem savedItem = default(SavedItem);
				savedItem.id = slot.info.itemid;
				savedItem.amount = slot.amount;
				savedItem.skin = slot.skin;
				savedItem.blueprintTarget = slot.blueprintTarget;
				SavedItem item = savedItem;
				if (slot.contents != null && slot.contents.itemList != null)
				{
					List<int> list2 = new List<int>();
					foreach (Item item2 in slot.contents.itemList)
					{
						list2.Add(item2.info.itemid);
					}
					item.containedItems = list2.ToArray();
				}
				list.Add(item);
			}
			return list.ToArray();
		}

		private static SavedItem[] SaveItems(List<PlayerInventoryProperties.ItemAmountSkinned> items)
		{
			List<SavedItem> list = new List<SavedItem>();
			foreach (PlayerInventoryProperties.ItemAmountSkinned item2 in items)
			{
				SavedItem savedItem = default(SavedItem);
				savedItem.id = item2.itemid;
				savedItem.amount = (int)item2.amount;
				savedItem.skin = item2.skinOverride;
				SavedItem item = savedItem;
				if (item2.blueprint)
				{
					item.blueprintTarget = item.id;
					item.id = ItemManager.blueprintBaseDef.itemid;
				}
				list.Add(item);
			}
			return list.ToArray();
		}

		public void LoadItemsOnTo(BasePlayer player)
		{
			player.inventory.containerMain.Clear();
			player.inventory.containerBelt.Clear();
			player.inventory.containerWear.Clear();
			ItemManager.DoRemoves();
			LoadItems(belt, player.inventory.containerBelt);
			LoadItems(wear, player.inventory.containerWear);
			LoadItems(main, player.inventory.containerMain);
			if (backpack != null && backpack.Length != 0)
			{
				Item backpackWithInventory = player.inventory.GetBackpackWithInventory();
				if (backpackWithInventory != null)
				{
					backpackWithInventory.contents.Clear();
					LoadItems(backpack, backpackWithInventory.contents);
				}
			}
			EquipItemInSlot(player, heldItemIndex);
			player.inventory.SendSnapshot();
			void LoadItems(SavedItem[] items, ItemContainer container)
			{
				foreach (SavedItem item in items)
				{
					player.inventory.GiveItem(LoadItem(item), container);
				}
			}
		}

		private Item LoadItem(SavedItem item)
		{
			Item item2 = ItemManager.CreateByItemID(item.id, item.amount, item.skin);
			if (item.blueprintTarget != 0)
			{
				item2.blueprintTarget = item.blueprintTarget;
			}
			if (item.containedItems != null && item.containedItems.Length != 0)
			{
				int[] containedItems = item.containedItems;
				foreach (int itemID in containedItems)
				{
					item2.contents.AddItem(ItemManager.FindItemDefinition(itemID), 1, 0uL);
				}
			}
			return item2;
		}
	}

	[ReplicatedVar(Help = "Disables all attire limitations, so NPC clothing and invalid overlaps can be equipped")]
	public static bool disableAttireLimitations;

	private const string LoadoutDirectory = "loadouts";

	[ServerUserVar(Name = "lighttoggle")]
	public static void lighttoggle_sv(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if ((bool)basePlayer && !basePlayer.IsDead() && !basePlayer.IsSleeping() && !basePlayer.InGesture)
		{
			basePlayer.LightToggle();
		}
	}

	[ServerUserVar]
	public static void endloot(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if ((bool)basePlayer && !basePlayer.IsDead() && !basePlayer.IsSleeping())
		{
			basePlayer.inventory.loot.Clear();
		}
	}

	[ServerVar(Help = "{item} {amount} {condition} {skin} {container} {slot}")]
	public static void give(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (!basePlayer)
		{
			return;
		}
		bool flag = arg.HasArg("--silent", remove: true);
		Item item = ItemManager.CreateByPartialName(arg.GetString(0), 1, arg.GetULong(3, 0uL));
		if (item == null)
		{
			arg.ReplyWith("Invalid Item!");
			return;
		}
		int num = (item.amount = arg.GetInt(1, 1));
		float @float = arg.GetFloat(2, 1f);
		item.conditionNormalized = @float;
		item.OnVirginSpawn();
		string @string = arg.GetString(4);
		int num2 = arg.GetInt(5, -1);
		ItemContainer itemContainer = null;
		switch (@string)
		{
		case "0":
		case "main":
			itemContainer = basePlayer.inventory.containerMain;
			break;
		case "1":
		case "belt":
			itemContainer = basePlayer.inventory.containerBelt;
			break;
		case "2":
		case "wear":
			itemContainer = basePlayer.inventory.containerWear;
			break;
		}
		if (itemContainer == null)
		{
			if (!basePlayer.inventory.GiveItem(item))
			{
				item.Remove();
				arg.ReplyWith("Couldn't give item (inventory full?)");
				return;
			}
		}
		else
		{
			if (num2 != -1)
			{
				Item slot = itemContainer.GetSlot(num2);
				if (slot != null && slot.contents != null)
				{
					itemContainer = slot.contents;
					num2 = -1;
				}
			}
			if (!item.MoveToContainer(itemContainer, num2))
			{
				item.Remove();
				arg.ReplyWith("Couldn't give item (inventory full?)");
				return;
			}
		}
		if (!flag)
		{
			basePlayer.Command("note.inv", item.info.itemid, num);
		}
		Debug.Log("giving " + basePlayer.displayName + " " + num + " x " + item.info.displayName.english);
		if (basePlayer.IsDeveloper)
		{
			if (!flag)
			{
				basePlayer.ChatMessage("you silently gave yourself " + num + " x " + item.info.displayName.english);
			}
			return;
		}
		Chat.Broadcast(basePlayer.displayName + " gave themselves " + num + " x " + item.info.displayName.english, "SERVER", "#eee", 0uL);
	}

	[ServerVar]
	public static void resetbp(Arg arg)
	{
		BasePlayer basePlayer = arg.GetPlayer(0);
		if (basePlayer == null)
		{
			if (arg.HasArgs())
			{
				arg.ReplyWith("Can't find player");
				return;
			}
			basePlayer = arg.Player();
		}
		basePlayer.blueprints.Reset();
	}

	[ServerVar]
	public static void unlockall(Arg arg)
	{
		BasePlayer basePlayer = arg.GetPlayer(0);
		if (basePlayer == null)
		{
			if (arg.HasArgs())
			{
				arg.ReplyWith("Can't find player");
				return;
			}
			basePlayer = arg.Player();
		}
		basePlayer.blueprints.UnlockAll();
	}

	[ServerVar]
	public static void giveall(Arg arg)
	{
		Item item = null;
		string text = "SERVER";
		if (arg.Player() != null)
		{
			text = arg.Player().displayName;
		}
		foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
		{
			item = ItemManager.CreateByPartialName(arg.GetString(0), 1, 0uL);
			if (item == null)
			{
				arg.ReplyWith("Invalid Item!");
				return;
			}
			int num = (item.amount = arg.GetInt(1, 1));
			item.OnVirginSpawn();
			if (!activePlayer.inventory.GiveItem(item))
			{
				item.Remove();
				arg.ReplyWith("Couldn't give item (inventory full?)");
				continue;
			}
			activePlayer.Command("note.inv", item.info.itemid, num);
			Debug.Log(" [ServerVar] giving " + activePlayer.displayName + " " + item.amount + " x " + item.info.displayName.english);
		}
		if (item != null)
		{
			Chat.Broadcast(text + " gave everyone " + item.amount + " x " + item.info.displayName.english, "SERVER", "#eee", 0uL);
		}
	}

	[ServerVar(Help = "{item} {player} {amount} {skin}")]
	public static void giveto(Arg arg)
	{
		string text = "SERVER";
		if (arg.Player() != null)
		{
			text = arg.Player().displayName;
		}
		BasePlayer basePlayer = BasePlayer.Find(arg.GetString(0));
		if (basePlayer == null)
		{
			arg.ReplyWith("Couldn't find player!");
			return;
		}
		Item item = ItemManager.CreateByPartialName(arg.GetString(1), 1, arg.GetULong(3, 0uL));
		if (item == null)
		{
			arg.ReplyWith("Invalid Item!");
			return;
		}
		int num = (item.amount = arg.GetInt(2, 1));
		item.OnVirginSpawn();
		if (!basePlayer.inventory.GiveItem(item))
		{
			item.Remove();
			arg.ReplyWith("Couldn't give item (inventory full?)");
			return;
		}
		basePlayer.Command("note.inv", item.info.itemid, num);
		Debug.Log(" [ServerVar] giving " + basePlayer.displayName + " " + num + " x " + item.info.displayName.english);
		Chat.Broadcast(text + " gave " + basePlayer.displayName + " " + num + " x " + item.info.displayName.english, "SERVER", "#eee", 0uL);
	}

	[ServerVar(Help = "{itemid} {amount}")]
	public static void giveid(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (!basePlayer)
		{
			return;
		}
		Item item = ItemManager.CreateByItemID(arg.GetInt(0), 1, 0uL);
		if (item == null)
		{
			arg.ReplyWith("Invalid Item!");
			return;
		}
		int num = (item.amount = arg.GetInt(1, 1));
		item.OnVirginSpawn();
		if (!basePlayer.inventory.GiveItem(item))
		{
			item.Remove();
			arg.ReplyWith("Couldn't give item (inventory full?)");
			return;
		}
		basePlayer.Command("note.inv", item.info.itemid, num);
		Debug.Log(" [ServerVar] giving " + basePlayer.displayName + " " + num + " x " + item.info.displayName.english);
		if (basePlayer.IsDeveloper)
		{
			basePlayer.ChatMessage("you silently gave yourself " + num + " x " + item.info.displayName.english);
			return;
		}
		Chat.Broadcast(basePlayer.displayName + " gave themselves " + num + " x " + item.info.displayName.english, "SERVER", "#eee", 0uL);
	}

	[ServerVar(Help = "{itemid} {amount}")]
	public static void givearm(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (!basePlayer)
		{
			return;
		}
		Item item = ItemManager.CreateByItemID(arg.GetInt(0), 1, 0uL);
		if (item == null)
		{
			arg.ReplyWith("Invalid Item!");
			return;
		}
		int num = (item.amount = arg.GetInt(1, 1));
		item.OnVirginSpawn();
		if (!basePlayer.inventory.GiveItem(item, basePlayer.inventory.containerBelt))
		{
			item.Remove();
			arg.ReplyWith("Couldn't give item (inventory full?)");
			return;
		}
		basePlayer.Command("note.inv", item.info.itemid, num);
		Debug.Log(" [ServerVar] giving " + basePlayer.displayName + " " + item.amount + " x " + item.info.displayName.english);
		if (basePlayer.IsDeveloper)
		{
			basePlayer.ChatMessage("you silently gave yourself " + item.amount + " x " + item.info.displayName.english);
			return;
		}
		Chat.Broadcast(basePlayer.displayName + " gave themselves " + item.amount + " x " + item.info.displayName.english, "SERVER", "#eee", 0uL);
	}

	[ServerVar]
	public static void pipetteid(Arg arg)
	{
		BasePlayer ply = arg.Player();
		int itemId = arg.GetInt(0);
		List<Item> list = ply.inventory.FindItemsByItemID(itemId);
		ulong skinId = arg.GetULong(1, 1uL);
		bool flag = false;
		foreach (Item item in list)
		{
			if (item.skin == skinId)
			{
				flag = true;
			}
		}
		if (!flag)
		{
			ItemDefinition itemDefinition = ItemManager.FindItemDefinition(itemId);
			ply.Command($"give {itemDefinition.shortname} 1 1 {skinId}");
		}
		InvokeHandler.Invoke(ply, delegate
		{
			ply.Command($"inventory.selectitem {itemId} {skinId}");
		}, 0.2f);
	}

	[ServerVar(Help = "Copies the players inventory to the player in front of them")]
	public static void copyTo(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if ((!basePlayer.IsAdmin && !basePlayer.IsDeveloper && !Server.cinematic) || basePlayer == null)
		{
			return;
		}
		BasePlayer basePlayer2 = null;
		if (arg.HasArgs() && arg.GetString(0).ToLower() != "true")
		{
			basePlayer2 = arg.GetPlayer(0);
			if (basePlayer2 == null)
			{
				uint uInt = arg.GetUInt(0);
				basePlayer2 = BasePlayer.FindByID(uInt);
				if (basePlayer2 == null)
				{
					basePlayer2 = BasePlayer.FindBot(uInt);
				}
			}
		}
		else
		{
			basePlayer2 = RelationshipManager.GetLookingAtPlayer(basePlayer);
		}
		if (!(basePlayer2 == null))
		{
			copyTo(basePlayer, basePlayer2);
		}
	}

	public static void copyTo(BasePlayer from, BasePlayer toply)
	{
		toply.inventory.containerBelt.Clear();
		toply.inventory.containerWear.Clear();
		int num = 0;
		foreach (Item item4 in from.inventory.containerBelt.itemList)
		{
			toply.inventory.containerBelt.AddItem(item4.info, item4.amount, item4.skin);
			if (item4.contents != null)
			{
				Item item = toply.inventory.containerBelt.itemList[num];
				foreach (Item item5 in item4.contents.itemList)
				{
					item.contents.AddItem(item5.info, item5.amount, item5.skin);
				}
			}
			num++;
		}
		foreach (Item item6 in from.inventory.containerWear.itemList)
		{
			toply.inventory.containerWear.AddItem(item6.info, item6.amount, item6.skin);
			if (!item6.IsBackpack() || item6.contents == null)
			{
				continue;
			}
			List<Item> itemList = toply.inventory.containerWear.itemList;
			Item item2 = itemList[itemList.Count - 1];
			if (item2 == null)
			{
				continue;
			}
			foreach (Item item7 in item6.contents.itemList)
			{
				item2.contents.AddItem(item7.info, item7.amount, item7.skin);
				if (item7.contents == null)
				{
					continue;
				}
				List<Item> itemList2 = item2.contents.itemList;
				Item item3 = itemList2[itemList2.Count - 1];
				foreach (Item item8 in item7.contents.itemList)
				{
					item3.contents.AddItem(item8.info, item8.amount, item8.skin);
				}
			}
		}
		if (from.IsDeveloper)
		{
			from.ChatMessage("you silently copied items to " + toply.displayName);
		}
		else
		{
			Chat.Broadcast(from.displayName + " copied their inventory to " + toply.displayName, "SERVER", "#eee", 0uL);
		}
	}

	[ServerVar(Help = "Deploys a loadout to players in a radius eg. inventory.deployLoadoutInRange testloadout 30")]
	public static void deployLoadoutInRange(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if ((!basePlayer.IsAdmin && !basePlayer.IsDeveloper && !Server.cinematic) || basePlayer == null)
		{
			return;
		}
		string @string = arg.GetString(0);
		if (!LoadLoadout(@string, out var so))
		{
			arg.ReplyWith("Can't find loadout: " + @string);
			return;
		}
		float @float = arg.GetFloat(1);
		List<BasePlayer> obj = Facepunch.Pool.Get<List<BasePlayer>>();
		global::Vis.Entities(basePlayer.transform.position, @float, obj, 131072);
		int num = 0;
		foreach (BasePlayer item in obj)
		{
			if (!(item == basePlayer) && !item.isClient)
			{
				so.LoadItemsOnTo(item);
				num++;
			}
		}
		arg.ReplyWith($"Applied loadout {@string} to {num} players");
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	[ServerVar(Help = "Deploys the given loadout to a target player. eg. inventory.deployLoadout testloadout jim")]
	public static void deployLoadout(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (!(basePlayer == null) && (basePlayer.IsAdmin || basePlayer.IsDeveloper || Server.cinematic))
		{
			string @string = arg.GetString(0);
			BasePlayer basePlayer2 = (string.IsNullOrEmpty(arg.GetString(1)) ? null : arg.GetPlayerOrSleeperOrBot(1));
			if (basePlayer2 == null)
			{
				basePlayer2 = basePlayer;
			}
			SavedLoadout so;
			if (basePlayer2 == null)
			{
				arg.ReplyWith("Could not find player " + arg.GetString(1) + " and no local player available");
			}
			else if (LoadLoadout(@string, out so))
			{
				so.LoadItemsOnTo(basePlayer2);
				arg.ReplyWith("Deployed loadout " + @string + " to " + basePlayer2.displayName);
			}
			else
			{
				arg.ReplyWith("Could not find loadout " + @string);
			}
		}
	}

	[ServerVar(Help = "Clears the inventory of a target player. eg. inventory.clearInventory jim. Can take container names as arguments: --belt --wear --backpack")]
	public static void clearInventory(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (basePlayer == null || (!basePlayer.IsAdmin && !basePlayer.IsDeveloper && !Server.cinematic))
		{
			return;
		}
		BasePlayer basePlayer2 = basePlayer;
		StringBuilder stringBuilder = new StringBuilder();
		if (arg.Args == null || arg.Args.Length == 0)
		{
			basePlayer2.inventory.containerMain.Clear();
			basePlayer2.inventory.containerBelt.Clear();
			basePlayer2.inventory.containerWear.Clear();
			basePlayer2.inventory.GetContainer(PlayerInventory.Type.BackpackContents)?.Clear();
			stringBuilder.AppendLine("Whole inventory cleared");
		}
		else
		{
			int num = 0;
			basePlayer2 = arg.GetPlayerOrSleeperOrBot(0);
			if (basePlayer2 == null)
			{
				switch (arg.GetString(0).ToLower())
				{
				case "--main":
				case "--belt":
				case "--wear":
				case "--backpack":
					break;
				default:
					arg.ReplyWith("Could not find player '" + arg.GetString(0) + "'");
					return;
				}
				basePlayer2 = basePlayer;
				num = 0;
			}
			else
			{
				num = 1;
			}
			bool flag = false;
			bool flag2 = false;
			bool flag3 = false;
			bool flag4 = false;
			for (int i = num; i < arg.Args.Length; i++)
			{
				switch (arg.GetString(i).ToLower())
				{
				case "--main":
					flag = true;
					break;
				case "--belt":
					flag2 = true;
					break;
				case "--wear":
					flag3 = true;
					break;
				case "--backpack":
					flag4 = true;
					break;
				}
			}
			if (flag)
			{
				basePlayer2.inventory.containerMain.Clear();
				stringBuilder.AppendLine("Cleared " + basePlayer2.displayName + "'s main inventory");
			}
			if (flag2)
			{
				basePlayer2.inventory.containerBelt.Clear();
				stringBuilder.AppendLine("Cleared " + basePlayer2.displayName + "'s belt");
			}
			if (flag3)
			{
				basePlayer2.inventory.containerWear.Clear();
				stringBuilder.AppendLine("Cleared " + basePlayer2.displayName + "'s clothings");
			}
			if (flag4 && basePlayer2.inventory.GetContainer(PlayerInventory.Type.BackpackContents) != null)
			{
				basePlayer2.inventory.GetContainer(PlayerInventory.Type.BackpackContents).Clear();
				stringBuilder.AppendLine("Cleared " + basePlayer2.displayName + "'s backpack");
			}
		}
		arg.ReplyWith(stringBuilder.ToString());
		ItemManager.DoRemoves();
	}

	private static string GetLoadoutPath(string loadoutName)
	{
		return Server.GetServerFolder("loadouts") + "/" + loadoutName + ".ldt";
	}

	[ServerVar(Help = "Saves the current equipped loadout of the calling player. eg. inventory.saveLoadout loaduoutname")]
	public static void saveloadout(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (!(basePlayer == null) && (basePlayer.IsAdmin || basePlayer.IsDeveloper || Server.cinematic))
		{
			string @string = arg.GetString(0);
			string contents = JsonConvert.SerializeObject(new SavedLoadout(basePlayer), Formatting.Indented);
			string loadoutPath = GetLoadoutPath(@string);
			File.WriteAllText(loadoutPath, contents);
			arg.ReplyWith("Saved loadout to " + loadoutPath);
		}
	}

	public static bool LoadLoadout(string name, out SavedLoadout so)
	{
		PlayerInventoryProperties inventoryConfig = PlayerInventoryProperties.GetInventoryConfig(name);
		if (inventoryConfig != null)
		{
			Debug.Log("Found builtin config!");
			so = new SavedLoadout(inventoryConfig);
			return true;
		}
		so = new SavedLoadout();
		string loadoutPath = GetLoadoutPath(name);
		if (!File.Exists(loadoutPath))
		{
			return false;
		}
		so = JsonConvert.DeserializeObject<SavedLoadout>(File.ReadAllText(loadoutPath));
		if (so == null)
		{
			return false;
		}
		return true;
	}

	[ServerVar(Help = "Prints all saved inventory loadouts")]
	public static void listloadouts(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (basePlayer == null || (!basePlayer.IsAdmin && !basePlayer.IsDeveloper && !Server.cinematic))
		{
			return;
		}
		string serverFolder = Server.GetServerFolder("loadouts");
		StringBuilder stringBuilder = new StringBuilder();
		foreach (string item in Directory.EnumerateFiles(serverFolder))
		{
			stringBuilder.AppendLine(item);
		}
		arg.ReplyWith(stringBuilder.ToString());
	}

	[ClientVar]
	[ServerVar]
	public static void defs(Arg arg)
	{
		if (Steamworks.SteamInventory.Definitions == null)
		{
			arg.ReplyWith("no definitions");
			return;
		}
		if (Steamworks.SteamInventory.Definitions.Length == 0)
		{
			arg.ReplyWith("0 definitions");
			return;
		}
		string[] obj = Steamworks.SteamInventory.Definitions.Select((InventoryDef x) => x.Name).ToArray();
		arg.ReplyWith(obj);
	}

	[ClientVar]
	[ServerVar]
	public static void reloaddefs(Arg arg)
	{
		Steamworks.SteamInventory.LoadItemDefinitions();
	}

	[ServerVar]
	public static void equipslottarget(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if ((basePlayer.IsAdmin || basePlayer.IsDeveloper || Server.cinematic) && !(basePlayer == null))
		{
			BasePlayer lookingAtPlayer = RelationshipManager.GetLookingAtPlayer(basePlayer);
			if (!(lookingAtPlayer == null))
			{
				int @int = arg.GetInt(0);
				EquipItemInSlot(lookingAtPlayer, @int);
				arg.ReplyWith($"Equipped slot {@int} on player {lookingAtPlayer.displayName}");
			}
		}
	}

	[ServerVar]
	public static void equipslot(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if ((!basePlayer.IsAdmin && !basePlayer.IsDeveloper && !Server.cinematic) || basePlayer == null)
		{
			return;
		}
		BasePlayer basePlayer2 = null;
		if (arg.HasArgs(2))
		{
			basePlayer2 = arg.GetPlayer(1);
			if (basePlayer2 == null)
			{
				uint uInt = arg.GetUInt(1);
				basePlayer2 = BasePlayer.FindByID(uInt);
				if (basePlayer2 == null)
				{
					basePlayer2 = BasePlayer.FindBot(uInt);
				}
			}
		}
		if (!(basePlayer2 == null))
		{
			int @int = arg.GetInt(0);
			EquipItemInSlot(basePlayer2, @int);
			Debug.Log($"Equipped slot {@int} on player {basePlayer2.displayName}");
		}
	}

	public static void EquipItemInSlot(BasePlayer player, int slot)
	{
		ItemId itemID = default(ItemId);
		for (int i = 0; i < player.inventory.containerBelt.itemList.Count; i++)
		{
			if (player.inventory.containerBelt.itemList[i] != null && i == slot)
			{
				itemID = player.inventory.containerBelt.itemList[i].uid;
				break;
			}
		}
		player.UpdateActiveItem(itemID);
	}

	private static int GetSlotIndex(BasePlayer player)
	{
		if (player.GetActiveItem() == null)
		{
			return -1;
		}
		ItemId uid = player.GetActiveItem().uid;
		for (int i = 0; i < player.inventory.containerBelt.itemList.Count; i++)
		{
			if (player.inventory.containerBelt.itemList[i] != null && player.inventory.containerBelt.itemList[i].uid == uid)
			{
				return i;
			}
		}
		return -1;
	}

	[ServerVar]
	public static void giveBp(Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (!basePlayer)
		{
			return;
		}
		ItemDefinition itemDefinition = ItemManager.FindDefinitionByPartialName(arg.GetString(0));
		if (itemDefinition == null)
		{
			arg.ReplyWith("Could not find item: " + arg.GetString(0));
			return;
		}
		if (itemDefinition.Blueprint == null)
		{
			arg.ReplyWith(itemDefinition.shortname + " has no blueprint!");
			return;
		}
		Item item = ItemManager.Create(ItemManager.blueprintBaseDef, 1, 0uL);
		item.blueprintTarget = itemDefinition.itemid;
		item.OnVirginSpawn();
		if (!basePlayer.inventory.GiveItem(item))
		{
			item.Remove();
			arg.ReplyWith("Couldn't give item (inventory full?)");
			return;
		}
		basePlayer.Command("note.inv", item.info.itemid, 1);
		Debug.Log("giving " + basePlayer.displayName + " 1 x " + item.blueprintTargetDef.shortname + " blueprint");
		if (basePlayer.IsDeveloper)
		{
			basePlayer.ChatMessage("you silently gave yourself 1 x " + item.blueprintTargetDef.shortname + " blueprint");
		}
		else
		{
			Chat.Broadcast(basePlayer.displayName + " gave themselves 1 x " + item.blueprintTargetDef.shortname + " blueprint", "SERVER", "#eee", 0uL);
		}
	}
}
