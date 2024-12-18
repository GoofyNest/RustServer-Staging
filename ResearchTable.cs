#define UNITY_ASSERTIONS
using System;
using ConVar;
using Facepunch;
using Facepunch.Rust;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

public class ResearchTable : StorageContainer
{
	public enum ResearchType
	{
		ResearchTable,
		TechTree
	}

	[NonSerialized]
	public float researchFinishedTime;

	public float researchCostFraction = 1f;

	public float researchDuration = 10f;

	public int requiredPaper = 10;

	public GameObjectRef researchStartEffect;

	public GameObjectRef researchFailEffect;

	public GameObjectRef researchSuccessEffect;

	public ItemDefinition researchResource;

	private BasePlayer user;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("ResearchTable.OnRpcMessage"))
		{
			if (rpc == 3177710095u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - DoResearch ");
				}
				using (TimeWarning.New("DoResearch"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(3177710095u, "DoResearch", this, player, 3f))
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
							DoResearch(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in DoResearch");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void ResetState()
	{
		base.ResetState();
		researchFinishedTime = 0f;
	}

	public override int GetIdealSlot(BasePlayer player, ItemContainer container, Item item)
	{
		if (item.info.shortname == "scrap")
		{
			Item slot = base.inventory.GetSlot(1);
			if (slot == null)
			{
				return 1;
			}
			if (slot.amount < item.MaxStackable())
			{
				return 1;
			}
		}
		return base.GetIdealSlot(player, container, item);
	}

	public bool IsResearching()
	{
		return HasFlag(Flags.On);
	}

	public int RarityMultiplier(Rarity rarity)
	{
		return rarity switch
		{
			Rarity.Common => 20, 
			Rarity.Uncommon => 15, 
			Rarity.Rare => 10, 
			_ => 5, 
		};
	}

	public int GetBlueprintStacksize(Item sourceItem)
	{
		int result = RarityMultiplier(sourceItem.info.rarity);
		if (sourceItem.info.category == ItemCategory.Ammunition)
		{
			result = Mathf.FloorToInt((float)sourceItem.MaxStackable() / (float)sourceItem.info.Blueprint.amountToCreate) * 2;
		}
		return result;
	}

	public static int ScrapForResearch(Item item)
	{
		return ScrapForResearch(item.info);
	}

	public static int ScrapForResearch(ItemDefinition info)
	{
		if (info.isRedirectOf != null)
		{
			return ScrapForResearch(info.isRedirectOf);
		}
		int result = 0;
		if (info.rarity == Rarity.Common)
		{
			result = 20;
		}
		if (info.rarity == Rarity.Uncommon)
		{
			result = 75;
		}
		if (info.rarity == Rarity.Rare)
		{
			result = 125;
		}
		if (info.rarity == Rarity.VeryRare || info.rarity == Rarity.None)
		{
			result = 500;
		}
		ItemBlueprint itemBlueprint = ItemManager.FindBlueprint(info);
		if (itemBlueprint != null && itemBlueprint.defaultBlueprint)
		{
			return ConVar.Server.defaultBlueprintResearchCost;
		}
		return result;
	}

	public bool IsItemResearchable(Item item)
	{
		ItemBlueprint itemBlueprint = ItemManager.FindBlueprint((item.info.isRedirectOf != null) ? item.info.isRedirectOf : item.info);
		if (itemBlueprint != null && itemBlueprint.defaultBlueprint)
		{
			return true;
		}
		if (itemBlueprint == null || !itemBlueprint.isResearchable)
		{
			return false;
		}
		return true;
	}

	public override void ServerInit()
	{
		base.ServerInit();
		base.inventory.canAcceptItem = ItemFilter;
	}

	public override bool ItemFilter(Item item, int targetSlot)
	{
		if (targetSlot == 1 && item.info != researchResource)
		{
			return false;
		}
		return base.ItemFilter(item, targetSlot);
	}

	public Item GetTargetItem()
	{
		return base.inventory.GetSlot(0);
	}

	public Item GetScrapItem()
	{
		Item slot = base.inventory.GetSlot(1);
		if (slot == null || slot.info != researchResource)
		{
			return null;
		}
		return slot;
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		if (HasFlag(Flags.On))
		{
			Invoke(ResearchAttemptFinished, researchDuration);
		}
		base.inventory.SetLocked(isLocked: false);
	}

	public override bool PlayerOpenLoot(BasePlayer player, string panelToOpen = "", bool doPositionChecks = true)
	{
		user = player;
		return base.PlayerOpenLoot(player);
	}

	public override void PlayerStoppedLooting(BasePlayer player)
	{
		user = null;
		base.PlayerStoppedLooting(player);
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void DoResearch(RPCMessage msg)
	{
		if (IsResearching())
		{
			return;
		}
		BasePlayer player = msg.player;
		Item targetItem = GetTargetItem();
		if (targetItem != null && targetItem.amount <= 1 && IsItemResearchable(targetItem))
		{
			targetItem.CollectedForCrafting(player);
			researchFinishedTime = UnityEngine.Time.realtimeSinceStartup + researchDuration;
			Invoke(ResearchAttemptFinished, researchDuration);
			base.inventory.SetLocked(isLocked: true);
			int scrapCost = ScrapForResearch(targetItem);
			Analytics.Azure.OnResearchStarted(player, this, targetItem, scrapCost);
			SetFlag(Flags.On, b: true);
			SendNetworkUpdate();
			player.inventory.loot.SendImmediate();
			if (researchStartEffect.isValid)
			{
				Effect.server.Run(researchStartEffect.resourcePath, this, 0u, Vector3.zero, Vector3.zero);
			}
			msg.player.GiveAchievement("RESEARCH_ITEM");
		}
	}

	public void ResearchAttemptFinished()
	{
		Item targetItem = GetTargetItem();
		Item scrapItem = GetScrapItem();
		if (targetItem != null && scrapItem != null)
		{
			int num = ScrapForResearch(targetItem);
			if (scrapItem.amount >= num)
			{
				if (scrapItem.amount == num)
				{
					base.inventory.Remove(scrapItem);
					scrapItem.RemoveFromContainer();
					scrapItem.Remove();
				}
				else
				{
					scrapItem.UseItem(num);
				}
				base.inventory.Remove(targetItem);
				targetItem.Remove();
				Item item = ItemManager.Create(ItemManager.blueprintBaseDef, 1, 0uL);
				item.blueprintTarget = ((targetItem.info.isRedirectOf != null) ? targetItem.info.isRedirectOf.itemid : targetItem.info.itemid);
				if (!item.MoveToContainer(base.inventory, 0))
				{
					item.Drop(GetDropPosition(), GetDropVelocity());
				}
				if (researchSuccessEffect.isValid)
				{
					Effect.server.Run(researchSuccessEffect.resourcePath, this, 0u, Vector3.zero, Vector3.zero);
				}
			}
		}
		SendNetworkUpdateImmediate();
		if (user != null)
		{
			user.inventory.loot.SendImmediate();
		}
		EndResearch();
	}

	public void CancelResearch()
	{
	}

	public void EndResearch()
	{
		base.inventory.SetLocked(isLocked: false);
		SetFlag(Flags.On, b: false);
		researchFinishedTime = 0f;
		SendNetworkUpdate();
		if (user != null)
		{
			user.inventory.loot.SendImmediate();
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.researchTable = Facepunch.Pool.Get<ProtoBuf.ResearchTable>();
		info.msg.researchTable.researchTimeLeft = researchFinishedTime - UnityEngine.Time.realtimeSinceStartup;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.researchTable != null)
		{
			researchFinishedTime = UnityEngine.Time.realtimeSinceStartup + info.msg.researchTable.researchTimeLeft;
		}
	}
}
