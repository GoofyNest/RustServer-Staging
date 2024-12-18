#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Facepunch.Rust;
using Network;
using ProtoBuf;
using UnityEngine;
using UnityEngine.Assertions;

public class MixingTable : StorageContainer
{
	public GameObject Particles;

	public RecipeList Recipes;

	public bool OnlyAcceptValidIngredients;

	private float lastTickTimestamp;

	private List<Item> inventoryItems = new List<Item>();

	private const float mixTickInterval = 1f;

	private Recipe currentRecipe;

	private int currentQuantity;

	protected ItemDefinition currentProductionItem;

	private static Dictionary<int, int> itemCostCache = new Dictionary<int, int>();

	public float RemainingMixTime { get; private set; }

	public float TotalMixTime { get; private set; }

	public BasePlayer MixStartingPlayer { get; private set; }

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("MixingTable.OnRpcMessage"))
		{
			if (rpc == 4291077201u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SV_FillInventoryForRecipe ");
				}
				using (TimeWarning.New("SV_FillInventoryForRecipe"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(4291077201u, "SV_FillInventoryForRecipe", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.IsVisible.Test(4291077201u, "SV_FillInventoryForRecipe", this, player, 3f))
						{
							return true;
						}
						if (!RPC_Server.MaxDistance.Test(4291077201u, "SV_FillInventoryForRecipe", this, player, 3f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg2 = rPCMessage;
							SV_FillInventoryForRecipe(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in SV_FillInventoryForRecipe");
					}
				}
				return true;
			}
			if (rpc == 4167839872u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SVSwitch ");
				}
				using (TimeWarning.New("SVSwitch"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(4167839872u, "SVSwitch", this, player, 3f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg3 = rPCMessage;
							SVSwitch(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in SVSwitch");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void ServerInit()
	{
		base.ServerInit();
		ItemContainer itemContainer = base.inventory;
		itemContainer.canAcceptItem = (Func<Item, int, bool>)Delegate.Combine(itemContainer.canAcceptItem, new Func<Item, int, bool>(CanAcceptItem));
		base.inventory.onItemAddedRemoved = OnItemAddedOrRemoved;
		RecipeDictionary.CacheRecipes(Recipes);
	}

	private bool CanAcceptItem(Item item, int targetSlot)
	{
		if (item == null)
		{
			return false;
		}
		if (!OnlyAcceptValidIngredients)
		{
			return true;
		}
		if (GetItemWaterAmount(item) > 0)
		{
			item = item.contents.itemList[0];
		}
		if (!(item.info == currentProductionItem))
		{
			return RecipeDictionary.ValidIngredientForARecipe(item, Recipes);
		}
		return true;
	}

	protected override void OnInventoryDirty()
	{
		base.OnInventoryDirty();
		if (IsOn())
		{
			StopMixing();
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	[RPC_Server.CallsPerSecond(5uL)]
	[RPC_Server.IsVisible(3f)]
	private void SV_FillInventoryForRecipe(RPCMessage msg)
	{
		if (msg.player == null)
		{
			return;
		}
		int num = msg.read.Int32();
		if (num >= 0 && num < Recipes.Recipes.Length)
		{
			Recipe recipe = Recipes.Recipes[num];
			if (!(recipe == null))
			{
				int amount = msg.read.Int32();
				TryFillInventoryForRecipe(recipe, msg.player, amount);
			}
		}
	}

	private void TryFillInventoryForRecipe(Recipe recipe, BasePlayer player, int amount)
	{
		if (recipe == null || player == null || amount <= 0)
		{
			return;
		}
		Recipe matchingInventoryRecipe = GetMatchingInventoryRecipe(base.inventory);
		ItemContainer tableContainer = ((matchingInventoryRecipe != recipe) ? base.inventory : null);
		if (!CanPlayerAffordRecipe(player, recipe, tableContainer, amount))
		{
			return;
		}
		if (matchingInventoryRecipe != recipe)
		{
			ReturnInventory(player);
		}
		int num = 0;
		Recipe.RecipeIngredient[] ingredients = recipe.Ingredients;
		for (int i = 0; i < ingredients.Length; i++)
		{
			Recipe.RecipeIngredient recipeIngredient = ingredients[i];
			int num2 = base.inventory.GetSlot(num)?.amount ?? 0;
			int num3 = recipeIngredient.Count * amount;
			int num4 = Mathf.Clamp(recipeIngredient.Ingredient.stackable - num2, 0, recipeIngredient.Ingredient.stackable);
			if (num3 > num4)
			{
				int num5 = num4 / recipeIngredient.Count;
				if (num5 < amount)
				{
					amount = num5;
				}
			}
			num++;
		}
		if (amount <= 0)
		{
			return;
		}
		num = 0;
		ingredients = recipe.Ingredients;
		for (int i = 0; i < ingredients.Length; i++)
		{
			Recipe.RecipeIngredient recipeIngredient2 = ingredients[i];
			int num6 = recipeIngredient2.Count * amount;
			if (player.inventory.Take(null, recipeIngredient2.Ingredient.itemid, num6) >= num6)
			{
				ItemManager.CreateByItemID(recipeIngredient2.Ingredient.itemid, num6, 0uL).MoveToContainer(base.inventory, num);
			}
			num++;
		}
		ItemManager.DoRemoves();
	}

	private void ReturnInventory(BasePlayer player)
	{
		if (player == null)
		{
			return;
		}
		for (int i = 0; i < base.inventory.capacity; i++)
		{
			Item slot = base.inventory.GetSlot(i);
			if (slot != null && !slot.MoveToContainer(player.inventory.containerMain) && !slot.MoveToContainer(player.inventory.containerBelt))
			{
				slot.Drop(base.inventory.dropPosition, base.inventory.dropVelocity);
			}
		}
		ItemManager.DoRemoves();
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	private void SVSwitch(RPCMessage msg)
	{
		bool flag = msg.read.Bit();
		if (flag != IsOn() && !(msg.player == null))
		{
			if (flag)
			{
				StartMixing(msg.player);
			}
			else
			{
				StopMixing();
			}
		}
	}

	private void StartMixing(BasePlayer player)
	{
		if (IsOn() || !CanStartMixing(player))
		{
			return;
		}
		MixStartingPlayer = player;
		bool itemsAreContiguous;
		List<Item> orderedContainerItems = GetOrderedContainerItems(base.inventory, out itemsAreContiguous);
		currentRecipe = RecipeDictionary.GetMatchingRecipeAndQuantity(Recipes, orderedContainerItems, out var quantity);
		currentQuantity = quantity;
		if (!(currentRecipe == null) && itemsAreContiguous && (!currentRecipe.RequiresBlueprint || !(currentRecipe.ProducedItem != null) || player.blueprints.HasUnlocked(currentRecipe.ProducedItem)))
		{
			if (base.isServer)
			{
				lastTickTimestamp = UnityEngine.Time.realtimeSinceStartup;
			}
			RemainingMixTime = currentRecipe.MixingDuration * (float)currentQuantity;
			TotalMixTime = RemainingMixTime;
			ReturnExcessItems(orderedContainerItems, player);
			if (RemainingMixTime == 0f)
			{
				ProduceItem(currentRecipe, currentQuantity);
				return;
			}
			InvokeRepeating(TickMix, 1f, 1f);
			SetFlag(Flags.On, b: true);
			SendNetworkUpdateImmediate();
		}
	}

	protected virtual bool CanStartMixing(BasePlayer player)
	{
		return true;
	}

	public void StopMixing()
	{
		currentRecipe = null;
		currentQuantity = 0;
		RemainingMixTime = 0f;
		CancelInvoke(TickMix);
		if (IsOn())
		{
			SetFlag(Flags.On, b: false);
			SendNetworkUpdateImmediate();
		}
	}

	private void TickMix()
	{
		if (currentRecipe == null)
		{
			StopMixing();
			return;
		}
		if (base.isServer)
		{
			lastTickTimestamp = UnityEngine.Time.realtimeSinceStartup;
			RemainingMixTime -= 1f;
		}
		SendNetworkUpdateImmediate();
		if (RemainingMixTime <= 0f)
		{
			ProduceItem(currentRecipe, currentQuantity);
		}
	}

	private void ProduceItem(Recipe recipe, int quantity)
	{
		StopMixing();
		ConsumeInventory(recipe, quantity);
		CreateRecipeItems(recipe, quantity);
	}

	private void ConsumeInventory(Recipe recipe, int quantity)
	{
		for (int i = 0; i < base.inventory.capacity; i++)
		{
			Item item = base.inventory.GetSlot(i);
			if (item != null)
			{
				if (GetItemWaterAmount(item) > 0)
				{
					item = item.contents.itemList[0];
				}
				int num = recipe.Ingredients[i].Count * quantity;
				if (num > 0)
				{
					Analytics.Azure.OnCraftMaterialConsumed(item.info.shortname, item.amount, MixStartingPlayer, this, inSafezone: false, recipe.ProducedItem?.shortname);
					item.UseItem(num);
				}
			}
		}
		ItemManager.DoRemoves();
	}

	private void ReturnExcessItems(List<Item> orderedContainerItems, BasePlayer player)
	{
		if (player == null || currentRecipe == null || orderedContainerItems == null || orderedContainerItems.Count != currentRecipe.Ingredients.Length)
		{
			return;
		}
		for (int i = 0; i < base.inventory.capacity; i++)
		{
			Item slot = base.inventory.GetSlot(i);
			if (slot == null)
			{
				break;
			}
			int num = slot.amount - currentRecipe.Ingredients[i].Count * currentQuantity;
			if (num > 0)
			{
				Item item = slot.SplitItem(num);
				if (!item.MoveToContainer(player.inventory.containerMain) && !item.MoveToContainer(player.inventory.containerBelt))
				{
					item.Drop(base.inventory.dropPosition, base.inventory.dropVelocity);
				}
			}
		}
		ItemManager.DoRemoves();
	}

	protected virtual void CreateRecipeItems(Recipe recipe, int quantity)
	{
		if (recipe == null || recipe.ProducedItem == null)
		{
			return;
		}
		int num = quantity * recipe.ProducedItemCount;
		int stackable = recipe.ProducedItem.stackable;
		int num2 = Mathf.CeilToInt((float)num / (float)stackable);
		currentProductionItem = recipe.ProducedItem;
		for (int i = 0; i < num2; i++)
		{
			int num3 = ((num > stackable) ? stackable : num);
			Item item = ItemManager.Create(recipe.ProducedItem, num3, 0uL);
			Analytics.Azure.OnCraftItem(item.info.shortname, item.amount, MixStartingPlayer, this, inSafezone: false);
			if (!item.MoveToContainer(base.inventory))
			{
				item.Drop(base.inventory.dropPosition, base.inventory.dropVelocity);
			}
			num -= num3;
			if (num <= 0)
			{
				break;
			}
		}
		currentProductionItem = null;
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.mixingTable = Facepunch.Pool.Get<ProtoBuf.MixingTable>();
		if (info.forDisk)
		{
			info.msg.mixingTable.remainingMixTime = RemainingMixTime;
		}
		else
		{
			info.msg.mixingTable.remainingMixTime = RemainingMixTime - Mathf.Max(UnityEngine.Time.realtimeSinceStartup - lastTickTimestamp, 0f);
		}
		info.msg.mixingTable.totalMixTime = TotalMixTime;
	}

	private int GetItemWaterAmount(Item item)
	{
		if (item == null)
		{
			return 0;
		}
		if (item.contents != null && item.contents.capacity == 1 && item.contents.allowedContents == ItemContainer.ContentsType.Liquid && item.contents.itemList.Count > 0)
		{
			return item.contents.itemList[0].amount;
		}
		return 0;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.mixingTable != null)
		{
			RemainingMixTime = info.msg.mixingTable.remainingMixTime;
			TotalMixTime = info.msg.mixingTable.totalMixTime;
		}
	}

	public Recipe GetMatchingInventoryRecipe(ItemContainer container)
	{
		bool itemsAreContiguous;
		int quantity;
		Recipe matchingRecipeAndQuantity = RecipeDictionary.GetMatchingRecipeAndQuantity(Recipes, GetOrderedContainerItems(container, out itemsAreContiguous), out quantity);
		if (matchingRecipeAndQuantity == null)
		{
			return null;
		}
		if (!itemsAreContiguous)
		{
			return null;
		}
		if (quantity <= 0)
		{
			return null;
		}
		return matchingRecipeAndQuantity;
	}

	public List<Item> GetOrderedContainerItems(ItemContainer container, out bool itemsAreContiguous)
	{
		itemsAreContiguous = true;
		if (container == null)
		{
			return null;
		}
		if (container.itemList == null)
		{
			return null;
		}
		if (container.itemList.Count == 0)
		{
			return null;
		}
		inventoryItems.Clear();
		bool flag = false;
		for (int i = 0; i < container.capacity; i++)
		{
			Item item = container.GetSlot(i);
			if (item != null && flag)
			{
				itemsAreContiguous = false;
				break;
			}
			if (item == null)
			{
				flag = true;
				continue;
			}
			if (GetItemWaterAmount(item) > 0)
			{
				item = item.contents.itemList[0];
			}
			inventoryItems.Add(item);
		}
		return inventoryItems;
	}

	public int GetMaxPlayerCanAfford(BasePlayer player, Recipe recipe, ItemContainer tableContainer)
	{
		if (player == null)
		{
			return 0;
		}
		if (recipe == null)
		{
			return 0;
		}
		ItemContainer itemContainer = ((GetMatchingInventoryRecipe(tableContainer) != recipe) ? tableContainer : null);
		itemCostCache.Clear();
		Recipe.RecipeIngredient[] ingredients = recipe.Ingredients;
		for (int i = 0; i < ingredients.Length; i++)
		{
			Recipe.RecipeIngredient recipeIngredient = ingredients[i];
			if (!itemCostCache.ContainsKey(recipeIngredient.Ingredient.itemid))
			{
				itemCostCache[recipeIngredient.Ingredient.itemid] = 0;
			}
			itemCostCache[recipeIngredient.Ingredient.itemid] += recipeIngredient.Count;
		}
		int num = int.MaxValue;
		foreach (KeyValuePair<int, int> item in itemCostCache)
		{
			int amount = player.inventory.GetAmount(item.Key);
			int num2 = itemContainer?.GetAmount(item.Key, onlyUsableAmounts: true) ?? 0;
			int num3 = (amount + num2) / itemCostCache[item.Key];
			if (num3 < num)
			{
				num = num3;
			}
		}
		return num;
	}

	public bool CanPlayerAffordRecipe(BasePlayer player, Recipe recipe, ItemContainer tableContainer, int amount)
	{
		if (player == null)
		{
			return false;
		}
		if (recipe == null)
		{
			return false;
		}
		itemCostCache.Clear();
		Recipe.RecipeIngredient[] ingredients = recipe.Ingredients;
		for (int i = 0; i < ingredients.Length; i++)
		{
			Recipe.RecipeIngredient recipeIngredient = ingredients[i];
			if (!itemCostCache.ContainsKey(recipeIngredient.Ingredient.itemid))
			{
				itemCostCache[recipeIngredient.Ingredient.itemid] = 0;
			}
			itemCostCache[recipeIngredient.Ingredient.itemid] += recipeIngredient.Count * amount;
		}
		foreach (KeyValuePair<int, int> item in itemCostCache)
		{
			int amount2 = player.inventory.GetAmount(item.Key);
			int num = tableContainer?.GetAmount(item.Key, onlyUsableAmounts: true) ?? 0;
			if (amount2 + num < itemCostCache[item.Key])
			{
				return false;
			}
		}
		return true;
	}
}
