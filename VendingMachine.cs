#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Facepunch;
using Facepunch.Math;
using Facepunch.Rust;
using Network;
using ProtoBuf;
using UnityEngine;
using UnityEngine.Assertions;

public class VendingMachine : StorageContainer, IUGCBrowserEntity
{
	public static class VendingMachineFlags
	{
		public const Flags EmptyInv = Flags.Reserved1;

		public const Flags IsVending = Flags.Reserved2;

		public const Flags Broadcasting = Flags.Reserved4;

		public const Flags OutOfStock = Flags.Reserved5;

		public const Flags NoDirectAccess = Flags.Reserved6;
	}

	private enum HistoryCategory
	{
		History,
		BestSold,
		MostRevenue
	}

	[Serializable]
	public class PurchaseDetails
	{
		public int itemId;

		public int amount;

		public int priceId;

		public int price;

		public int timestamp;

		public bool itemIsBp;

		public bool priceIsBp;
	}

	[Header("VendingMachine")]
	public static readonly Translate.Phrase WaitForVendingMessage = new Translate.Phrase("vendingmachine.wait", "Please wait...");

	public GameObjectRef adminMenuPrefab;

	public string customerPanel = "";

	public ProtoBuf.VendingMachine.SellOrderContainer sellOrders;

	public SoundPlayer buySound;

	public string shopName = "A Shop";

	public int maxCurrencyVolume = 1;

	public GameObjectRef mapMarkerPrefab;

	public bool IsLocalized;

	private Action fullUpdateCached;

	private ulong nameLastEditedBy;

	protected BasePlayer vend_Player;

	private int vend_sellOrderID;

	private int vend_numberOfTransactions;

	protected bool transactionActive;

	private VendingMachineMapMarker myMarker;

	private bool industrialItemIncoming;

	public static readonly Translate.Phrase TooManySellOrders = new Translate.Phrase("error_toomanysellorders", "Too many sell orders");

	[ServerVar]
	public static int max_returned = 100;

	[ServerVar]
	public static int max_processed = 10000;

	[ServerVar]
	public static int max_history = 10000;

	private List<PurchaseDetails> purchaseHistory = new List<PurchaseDetails>();

	private Dictionary<ulong, int> uniqueCustomers = new Dictionary<ulong, int>();

	protected ItemDefinition blueprintBaseDef => ItemManager.blueprintBaseDef;

	public uint[] GetContentCRCs => null;

	public UGCType ContentType => UGCType.VendingMachine;

	public List<ulong> EditingHistory => new List<ulong> { nameLastEditedBy };

	public BaseNetworkable UgcEntity
	{
		get
		{
			if (!(this is NPCVendingMachine))
			{
				return this;
			}
			return null;
		}
	}

	public string ContentString => shopName;

	public virtual bool ShouldRecordStats => true;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("VendingMachine.OnRpcMessage"))
		{
			if (rpc == 3011053703u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - BuyItem ");
				}
				using (TimeWarning.New("BuyItem"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3011053703u, "BuyItem", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.IsVisible.Test(3011053703u, "BuyItem", this, player, 3f))
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
							RPCMessage rpc2 = rPCMessage;
							BuyItem(rpc2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in BuyItem");
					}
				}
				return true;
			}
			if (rpc == 1626480840 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_AddSellOrder ");
				}
				using (TimeWarning.New("RPC_AddSellOrder"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(1626480840u, "RPC_AddSellOrder", this, player, 3f))
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
							RPC_AddSellOrder(msg2);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in RPC_AddSellOrder");
					}
				}
				return true;
			}
			if (rpc == 169239598 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_Broadcast ");
				}
				using (TimeWarning.New("RPC_Broadcast"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(169239598u, "RPC_Broadcast", this, player, 3f))
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
							RPC_Broadcast(msg3);
						}
					}
					catch (Exception exception3)
					{
						Debug.LogException(exception3);
						player.Kick("RPC Error in RPC_Broadcast");
					}
				}
				return true;
			}
			if (rpc == 3680901137u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_DeleteSellOrder ");
				}
				using (TimeWarning.New("RPC_DeleteSellOrder"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(3680901137u, "RPC_DeleteSellOrder", this, player, 3f))
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
							RPCMessage msg4 = rPCMessage;
							RPC_DeleteSellOrder(msg4);
						}
					}
					catch (Exception exception4)
					{
						Debug.LogException(exception4);
						player.Kick("RPC Error in RPC_DeleteSellOrder");
					}
				}
				return true;
			}
			if (rpc == 2555993359u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_OpenAdmin ");
				}
				using (TimeWarning.New("RPC_OpenAdmin"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(2555993359u, "RPC_OpenAdmin", this, player, 3f))
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
							RPCMessage msg5 = rPCMessage;
							RPC_OpenAdmin(msg5);
						}
					}
					catch (Exception exception5)
					{
						Debug.LogException(exception5);
						player.Kick("RPC Error in RPC_OpenAdmin");
					}
				}
				return true;
			}
			if (rpc == 36164441 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_OpenShop ");
				}
				using (TimeWarning.New("RPC_OpenShop"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(36164441u, "RPC_OpenShop", this, player, 3f))
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
							RPCMessage msg6 = rPCMessage;
							RPC_OpenShop(msg6);
						}
					}
					catch (Exception exception6)
					{
						Debug.LogException(exception6);
						player.Kick("RPC Error in RPC_OpenShop");
					}
				}
				return true;
			}
			if (rpc == 2947824655u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_OpenShopNoLOS ");
				}
				using (TimeWarning.New("RPC_OpenShopNoLOS"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(2947824655u, "RPC_OpenShopNoLOS", this, player, 3f))
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
							RPCMessage msg7 = rPCMessage;
							RPC_OpenShopNoLOS(msg7);
						}
					}
					catch (Exception exception7)
					{
						Debug.LogException(exception7);
						player.Kick("RPC Error in RPC_OpenShopNoLOS");
					}
				}
				return true;
			}
			if (rpc == 3346513099u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_RotateVM ");
				}
				using (TimeWarning.New("RPC_RotateVM"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(3346513099u, "RPC_RotateVM", this, player, 3f))
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
							RPCMessage msg8 = rPCMessage;
							RPC_RotateVM(msg8);
						}
					}
					catch (Exception exception8)
					{
						Debug.LogException(exception8);
						player.Kick("RPC Error in RPC_RotateVM");
					}
				}
				return true;
			}
			if (rpc == 1012779214 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_UpdateShopName ");
				}
				using (TimeWarning.New("RPC_UpdateShopName"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(1012779214u, "RPC_UpdateShopName", this, player, 3f))
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
							RPCMessage msg9 = rPCMessage;
							RPC_UpdateShopName(msg9);
						}
					}
					catch (Exception exception9)
					{
						Debug.LogException(exception9);
						player.Kick("RPC Error in RPC_UpdateShopName");
					}
				}
				return true;
			}
			if (rpc == 1147600716 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SV_RequestLongTermData ");
				}
				using (TimeWarning.New("SV_RequestLongTermData"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg10 = rPCMessage;
							SV_RequestLongTermData(msg10);
						}
					}
					catch (Exception exception10)
					{
						Debug.LogException(exception10);
						player.Kick("RPC Error in SV_RequestLongTermData");
					}
				}
				return true;
			}
			if (rpc == 3957849636u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SV_RequestPurchaseData ");
				}
				using (TimeWarning.New("SV_RequestPurchaseData"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg11 = rPCMessage;
							SV_RequestPurchaseData(msg11);
						}
					}
					catch (Exception exception11)
					{
						Debug.LogException(exception11);
						player.Kick("RPC Error in SV_RequestPurchaseData");
					}
				}
				return true;
			}
			if (rpc == 3559014831u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - TransactionStart ");
				}
				using (TimeWarning.New("TransactionStart"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(3559014831u, "TransactionStart", this, player, 3f))
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
							RPCMessage rpc3 = rPCMessage;
							TransactionStart(rpc3);
						}
					}
					catch (Exception exception12)
					{
						Debug.LogException(exception12);
						player.Kick("RPC Error in TransactionStart");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.fromDisk && info.msg.vendingMachineStats != null)
		{
			purchaseHistory = GetListFromProto(info.msg.vendingMachineStats.purchaseHistory);
			for (int i = 0; i < info.msg.vendingMachineStats.customers.Count; i++)
			{
				uniqueCustomers.Add(info.msg.vendingMachineStats.customers[i], info.msg.vendingMachineStats.customersVisits[i]);
			}
		}
		if (info.msg.vendingMachine != null)
		{
			if (!IsLocalized)
			{
				shopName = info.msg.vendingMachine.shopName;
			}
			if (info.msg.vendingMachine.sellOrderContainer != null)
			{
				sellOrders = info.msg.vendingMachine.sellOrderContainer;
				sellOrders.ShouldPool = false;
			}
			if (info.fromDisk && base.isServer)
			{
				nameLastEditedBy = info.msg.vendingMachine.nameLastEditedBy;
				RefreshSellOrderStockLevel();
			}
		}
	}

	public static int GetTotalReceivedMerchandiseForOrder(ProtoBuf.VendingMachine.SellOrder order)
	{
		return GetTotalReceivedMerchandiseForOrder(order.itemToSellAmount, order.receivedQuantityMultiplier);
	}

	public static int GetTotalReceivedMerchandiseForOrder(int merchAmountPerOrder, float multiplier)
	{
		float num = ((multiplier != 0f) ? multiplier : 1f);
		return Mathf.Max(Mathf.RoundToInt((float)merchAmountPerOrder * num), 1);
	}

	public static int GetTotalPriceForOrder(ProtoBuf.VendingMachine.SellOrder order)
	{
		return GetTotalPriceForOrder(order.currencyAmountPerItem, order.priceMultiplier);
	}

	public static int GetTotalPriceForOrder(int currencyAmountPerItem, float multiplier)
	{
		float num = ((multiplier != 0f) ? multiplier : 1f);
		return Mathf.Max(Mathf.RoundToInt((float)currencyAmountPerItem * num), 1);
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.vendingMachine = new ProtoBuf.VendingMachine();
		info.msg.vendingMachine.ShouldPool = false;
		info.msg.vendingMachine.shopName = shopName;
		if (info.forDisk)
		{
			info.msg.vendingMachine.nameLastEditedBy = nameLastEditedBy;
			if (ShouldRecordStats)
			{
				info.msg.vendingMachineStats = Facepunch.Pool.Get<VendingMachineStats>();
				info.msg.vendingMachineStats.purchaseHistory = GetEntriesProto(purchaseHistory);
				info.msg.vendingMachineStats.customers = uniqueCustomers.Keys.ToList();
				info.msg.vendingMachineStats.customersVisits = uniqueCustomers.Values.ToList();
			}
		}
		if (this is NPCVendingMachine)
		{
			info.msg.vendingMachine.translationToken = GetTranslationToken();
		}
		if (sellOrders == null)
		{
			return;
		}
		info.msg.vendingMachine.sellOrderContainer = new ProtoBuf.VendingMachine.SellOrderContainer();
		info.msg.vendingMachine.sellOrderContainer.ShouldPool = false;
		info.msg.vendingMachine.sellOrderContainer.sellOrders = new List<ProtoBuf.VendingMachine.SellOrder>();
		foreach (ProtoBuf.VendingMachine.SellOrder sellOrder2 in sellOrders.sellOrders)
		{
			ProtoBuf.VendingMachine.SellOrder sellOrder = new ProtoBuf.VendingMachine.SellOrder
			{
				ShouldPool = false
			};
			sellOrder2.CopyTo(sellOrder);
			info.msg.vendingMachine.sellOrderContainer.sellOrders.Add(sellOrder);
		}
	}

	public override void ServerInit()
	{
		base.ServerInit();
		if (base.isServer)
		{
			InstallDefaultSellOrders();
			SetFlag(Flags.Reserved2, b: false);
			base.inventory.onItemAddedRemoved = OnItemAddedOrRemoved;
			RefreshSellOrderStockLevel();
			ItemContainer itemContainer = base.inventory;
			itemContainer.canAcceptItem = (Func<Item, int, bool>)Delegate.Combine(itemContainer.canAcceptItem, new Func<Item, int, bool>(CanAcceptItem));
			UpdateMapMarker();
			fullUpdateCached = FullUpdate;
		}
	}

	public override void DestroyShared()
	{
		if ((bool)myMarker)
		{
			myMarker.Kill();
			myMarker = null;
		}
		base.DestroyShared();
	}

	public override void OnItemAddedOrRemoved(Item item, bool added)
	{
		base.OnItemAddedOrRemoved(item, added);
	}

	public override bool ShouldUseCastNoClipChecks()
	{
		return true;
	}

	public void FullUpdate()
	{
		RefreshSellOrderStockLevel();
		UpdateMapMarker();
		SendNetworkUpdate();
	}

	protected override void OnInventoryDirty()
	{
		base.OnInventoryDirty();
		CancelInvoke(fullUpdateCached);
		Invoke(fullUpdateCached, 0.2f);
	}

	public void RefreshSellOrderStockLevel(ItemDefinition itemDef = null)
	{
		int num = 0;
		foreach (ProtoBuf.VendingMachine.SellOrder sellOrder in sellOrders.sellOrders)
		{
			if (!(itemDef == null) && itemDef.itemid != sellOrder.itemToSellID)
			{
				continue;
			}
			List<Item> obj = Facepunch.Pool.Get<List<Item>>();
			GetItemsToSell(sellOrder, obj);
			int num2 = sellOrder.itemToSellAmount;
			if (ItemManager.FindItemDefinition(sellOrder.itemToSellID) == NPCVendingMachine.ScrapItem && sellOrder.receivedQuantityMultiplier != 1f)
			{
				num2 = GetTotalPriceForOrder(num2, sellOrder.receivedQuantityMultiplier);
			}
			sellOrder.inStock = ((obj.Count >= 0) ? (obj.Sum((Item x) => x.amount) / num2) : 0);
			float itemCondition = 0f;
			float itemConditionMax = 0f;
			int instanceData = 0;
			List<int> list = Facepunch.Pool.Get<List<int>>();
			int totalAttachmentSlots = 0;
			int ammoType = 0;
			int ammoCount = 0;
			if (obj.Count > 0)
			{
				if (obj[0].hasCondition)
				{
					itemCondition = obj[0].condition;
					itemConditionMax = obj[0].maxCondition;
				}
				if (obj[0].info != null && obj[0].info.amountType == ItemDefinition.AmountType.Genetics && obj[0].instanceData != null)
				{
					instanceData = obj[0].instanceData.dataInt;
					sellOrder.inStock = obj[0].amount;
				}
				if (obj[0].contents != null && obj[0].contents.capacity > 0 && obj[0].contents.HasFlag(ItemContainer.Flag.ShowSlotsOnIcon))
				{
					foreach (Item item in obj[0].contents.itemList)
					{
						list.Add(item.info.itemid);
					}
					totalAttachmentSlots = obj[0].contents.capacity;
				}
				if (obj[0].ammoCount.HasValue)
				{
					ammoCount = obj[0].ammoCount.Value;
					BaseEntity heldEntity = obj[0].GetHeldEntity();
					if ((bool)heldEntity)
					{
						BaseProjectile component = heldEntity.GetComponent<BaseProjectile>();
						if ((bool)component)
						{
							ammoType = component.primaryMagazine.ammoType.itemid;
						}
					}
				}
			}
			sellOrder.ammoType = ammoType;
			sellOrder.ammoCount = ammoCount;
			sellOrder.itemCondition = itemCondition;
			sellOrder.itemConditionMax = itemConditionMax;
			sellOrder.instanceData = instanceData;
			if (sellOrder.attachmentsList != null)
			{
				Facepunch.Pool.FreeUnmanaged(ref sellOrder.attachmentsList);
			}
			sellOrder.attachmentsList = list;
			sellOrder.totalAttachmentSlots = totalAttachmentSlots;
			sellOrder.priceMultiplier = GetDiscountForSlot(num, sellOrder);
			sellOrder.receivedQuantityMultiplier = GetReceivedQuantityMultiplier(num, sellOrder);
			num++;
			Facepunch.Pool.Free(ref obj, freeElements: false);
		}
	}

	protected virtual float GetDiscountForSlot(int sellOrderSlot, ProtoBuf.VendingMachine.SellOrder forOrder)
	{
		return 1f;
	}

	protected virtual float GetReceivedQuantityMultiplier(int sellOrderSlot, ProtoBuf.VendingMachine.SellOrder forOrder)
	{
		return 1f;
	}

	public bool OutOfStock()
	{
		foreach (ProtoBuf.VendingMachine.SellOrder sellOrder in sellOrders.sellOrders)
		{
			if (sellOrder.inStock > 0)
			{
				return true;
			}
		}
		return false;
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		SetFlag(Flags.Reserved2, b: false);
		RefreshSellOrderStockLevel();
		UpdateMapMarker();
	}

	public void UpdateEmptyFlag()
	{
		SetFlag(Flags.Reserved1, base.inventory.itemList.Count == 0);
	}

	public override void PlayerStoppedLooting(BasePlayer player)
	{
		base.PlayerStoppedLooting(player);
		UpdateEmptyFlag();
		if (vend_Player != null && vend_Player == player)
		{
			ClearPendingOrder();
		}
	}

	public virtual void InstallDefaultSellOrders()
	{
		sellOrders = new ProtoBuf.VendingMachine.SellOrderContainer();
		sellOrders.ShouldPool = false;
		sellOrders.sellOrders = new List<ProtoBuf.VendingMachine.SellOrder>();
	}

	public virtual bool HasVendingSounds()
	{
		return true;
	}

	public virtual float GetBuyDuration()
	{
		return 2.5f;
	}

	public void SetPendingOrder(BasePlayer buyer, int sellOrderId, int numberOfTransactions)
	{
		ClearPendingOrder();
		vend_Player = buyer;
		vend_sellOrderID = sellOrderId;
		vend_numberOfTransactions = numberOfTransactions;
		SetFlag(Flags.Reserved2, b: true);
		if (HasVendingSounds())
		{
			ClientRPC(RpcTarget.NetworkGroup("CLIENT_StartVendingSounds"), sellOrderId);
		}
	}

	public void ClearPendingOrder()
	{
		CancelInvoke(CompletePendingOrder);
		vend_Player = null;
		vend_sellOrderID = -1;
		vend_numberOfTransactions = -1;
		SetFlag(Flags.Reserved2, b: false);
		ClientRPC(RpcTarget.NetworkGroup("CLIENT_CancelVendingSounds"));
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	[RPC_Server.CallsPerSecond(5uL)]
	public void BuyItem(RPCMessage rpc)
	{
		if (OccupiedCheck(rpc.player))
		{
			int sellOrderId = rpc.read.Int32();
			int numberOfTransactions = rpc.read.Int32();
			if (IsVending())
			{
				rpc.player.ShowToast(GameTip.Styles.Red_Normal, WaitForVendingMessage, false);
				return;
			}
			SetPendingOrder(rpc.player, sellOrderId, numberOfTransactions);
			Invoke(CompletePendingOrder, GetBuyDuration());
		}
	}

	public virtual void CompletePendingOrder()
	{
		DoTransaction(vend_Player, vend_sellOrderID, vend_numberOfTransactions);
		ClearPendingOrder();
		Decay.RadialDecayTouch(base.transform.position, 40f, 2097408);
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void TransactionStart(RPCMessage rpc)
	{
	}

	private void GetItemsToSell(ProtoBuf.VendingMachine.SellOrder sellOrder, List<Item> items)
	{
		if (sellOrder.itemToSellIsBP)
		{
			foreach (Item item in base.inventory.itemList)
			{
				if (item.info.itemid == blueprintBaseDef.itemid && item.blueprintTarget == sellOrder.itemToSellID)
				{
					items.Add(item);
				}
			}
			return;
		}
		foreach (Item item2 in base.inventory.itemList)
		{
			if (item2.info.itemid == sellOrder.itemToSellID)
			{
				items.Add(item2);
			}
		}
	}

	public bool DoTransaction(BasePlayer buyer, int sellOrderId, int numberOfTransactions = 1, ItemContainer targetContainer = null, Action<BasePlayer, Item> onCurrencyRemoved = null, Action<BasePlayer, Item> onItemPurchased = null, MarketTerminal droneMarketTerminal = null)
	{
		if (sellOrderId < 0 || sellOrderId >= sellOrders.sellOrders.Count)
		{
			return false;
		}
		if (targetContainer == null && Vector3.Distance(buyer.transform.position, base.transform.position) > 4f)
		{
			return false;
		}
		ProtoBuf.VendingMachine.SellOrder sellOrder = sellOrders.sellOrders[sellOrderId];
		List<Item> obj = Facepunch.Pool.Get<List<Item>>();
		GetItemsToSell(sellOrder, obj);
		if (obj == null || obj.Count == 0)
		{
			Facepunch.Pool.FreeUnmanaged(ref obj);
			return false;
		}
		numberOfTransactions = Mathf.Clamp(numberOfTransactions, 1, obj[0].hasCondition ? 1 : 1000000);
		int num = sellOrder.itemToSellAmount * numberOfTransactions;
		if (ItemManager.FindItemDefinition(sellOrder.itemToSellID) == NPCVendingMachine.ScrapItem && sellOrder.receivedQuantityMultiplier != 1f)
		{
			num = GetTotalReceivedMerchandiseForOrder(sellOrder.itemToSellAmount, sellOrder.receivedQuantityMultiplier) * numberOfTransactions;
		}
		int num2 = obj.Sum((Item x) => x.amount);
		if (num > num2)
		{
			Facepunch.Pool.FreeUnmanaged(ref obj);
			return false;
		}
		List<Item> source = buyer.inventory.FindItemsByItemID(sellOrder.currencyID);
		if (sellOrder.currencyIsBP)
		{
			source = (from x in buyer.inventory.FindItemsByItemID(blueprintBaseDef.itemid)
				where x.blueprintTarget == sellOrder.currencyID
				select x).ToList();
		}
		source = (from x in source
			where !x.hasCondition || (x.conditionNormalized >= 0.5f && x.maxConditionNormalized > 0.5f)
			where x.GetItemVolume() <= maxCurrencyVolume
			select x).ToList();
		if (source.Count == 0)
		{
			Facepunch.Pool.FreeUnmanaged(ref obj);
			return false;
		}
		int num3 = source.Sum((Item x) => x.amount);
		int num4 = GetTotalPriceForOrder(sellOrder) * numberOfTransactions;
		if (num3 < num4)
		{
			Facepunch.Pool.FreeUnmanaged(ref obj);
			return false;
		}
		transactionActive = true;
		int num5 = 0;
		foreach (Item item3 in source)
		{
			int num6 = Mathf.Min(num4 - num5, item3.amount);
			Item item = ((item3.amount > num6) ? item3.SplitItem(num6) : item3);
			TakeCurrencyItem(item);
			onCurrencyRemoved?.Invoke(buyer, item);
			num5 += num6;
			if (num5 >= num4)
			{
				break;
			}
		}
		Analytics.Azure.OnBuyFromVendingMachine(buyer, this, sellOrder.itemToSellID, sellOrder.itemToSellAmount * numberOfTransactions, sellOrder.itemToSellIsBP, sellOrder.currencyID, num4, sellOrder.currencyIsBP, numberOfTransactions, sellOrder.priceMultiplier, droneMarketTerminal);
		int num7 = 0;
		foreach (Item item4 in obj)
		{
			int num8 = num - num7;
			Item item2 = ((item4.amount > num8) ? item4.SplitItem(num8) : item4);
			if (item2 == null)
			{
				Debug.LogError("Vending machine error, contact developers!");
			}
			else
			{
				num7 += item2.amount;
				RecordSaleAnalytics(item2, sellOrderId, sellOrder.currencyAmountPerItem);
				if (targetContainer == null)
				{
					GiveSoldItem(item2, buyer);
				}
				else if (!item2.MoveToContainer(targetContainer))
				{
					item2.Drop(targetContainer.dropPosition, targetContainer.dropVelocity);
				}
				if (ShouldRecordStats)
				{
					RegisterCustomer(buyer.userID);
				}
				onItemPurchased?.Invoke(buyer, item2);
			}
			if (num7 >= num)
			{
				break;
			}
		}
		if (ShouldRecordStats)
		{
			AddPurchaseHistory(sellOrder.itemToSellID, sellOrder.currencyAmountPerItem * numberOfTransactions, sellOrder.currencyID, num4, sellOrder.itemToSellIsBP, sellOrder.currencyIsBP);
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		UpdateEmptyFlag();
		transactionActive = false;
		return true;
	}

	protected virtual void RecordSaleAnalytics(Item itemSold, int orderId, int currencyUsed)
	{
		Analytics.Server.VendingMachineTransaction(null, itemSold.info, itemSold.amount);
	}

	public virtual void TakeCurrencyItem(Item takenCurrencyItem)
	{
		if (!takenCurrencyItem.MoveToContainer(base.inventory))
		{
			takenCurrencyItem.Drop(base.inventory.dropPosition, Vector3.zero);
		}
	}

	public virtual void GiveSoldItem(Item soldItem, BasePlayer buyer)
	{
		while (soldItem.amount > soldItem.MaxStackable())
		{
			Item item = soldItem.SplitItem(soldItem.MaxStackable());
			buyer.GiveItem(item, GiveItemReason.PickedUp);
		}
		buyer.GiveItem(soldItem, GiveItemReason.PickedUp);
	}

	public void SendSellOrders(BasePlayer player = null)
	{
		if ((bool)player)
		{
			ClientRPC(RpcTarget.Player("CLIENT_ReceiveSellOrders", player), sellOrders);
		}
		else
		{
			ClientRPC(RpcTarget.NetworkGroup("CLIENT_ReceiveSellOrders"), sellOrders);
		}
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void RPC_Broadcast(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		bool b = msg.read.Bit();
		if (CanPlayerAdmin(player))
		{
			SetFlag(Flags.Reserved4, b);
			UpdateMapMarker();
		}
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void RPC_UpdateShopName(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		string text = msg.read.String(32);
		if (CanPlayerAdmin(player))
		{
			shopName = text;
			nameLastEditedBy = player.userID.Get();
			UpdateMapMarker();
		}
	}

	public void UpdateMapMarkerPosition()
	{
		if (!(myMarker == null))
		{
			myMarker.TryUpdatePosition();
		}
	}

	public void UpdateMapMarker(bool updatePosition = false)
	{
		if (!mapMarkerPrefab.isValid)
		{
			return;
		}
		if (IsBroadcasting())
		{
			bool flag = false;
			if (myMarker == null)
			{
				myMarker = GameManager.server.CreateEntity(mapMarkerPrefab.resourcePath, base.transform.position, Quaternion.identity) as VendingMachineMapMarker;
				flag = true;
			}
			myMarker.SetFlag(Flags.Busy, OutOfStock());
			myMarker.SetVendingMachine(this, shopName);
			if (flag)
			{
				myMarker.Spawn();
			}
			else
			{
				myMarker.SendNetworkUpdate();
			}
		}
		else if ((bool)myMarker)
		{
			myMarker.Kill();
			myMarker = null;
		}
	}

	public void OpenShop(BasePlayer ply)
	{
		SendSellOrders(ply);
		PlayerOpenLoot(ply, customerPanel);
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	public void RPC_OpenShopNoLOS(RPCMessage msg)
	{
		if (OccupiedCheck(msg.player))
		{
			OpenShop(msg.player);
		}
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void RPC_OpenShop(RPCMessage msg)
	{
		if (OccupiedCheck(msg.player))
		{
			OpenShop(msg.player);
		}
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void RPC_OpenAdmin(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (CanPlayerAdmin(player))
		{
			OpenShop(player);
			ClientRPC(RpcTarget.Player("CLIENT_OpenAdminMenu", player));
		}
	}

	public void OnIndustrialItemTransferBegins()
	{
		industrialItemIncoming = true;
	}

	public void OnIndustrialItemTransferEnds()
	{
		industrialItemIncoming = false;
	}

	public bool CanAcceptItem(Item item, int targetSlot)
	{
		BasePlayer basePlayer = item.GetRootContainer()?.GetOwnerPlayer();
		if (transactionActive || industrialItemIncoming)
		{
			return true;
		}
		if (item.parent == null)
		{
			return true;
		}
		if (base.inventory.itemList.Contains(item))
		{
			return true;
		}
		if (basePlayer == null)
		{
			return false;
		}
		return CanPlayerAdmin(basePlayer);
	}

	public override bool CanMoveFrom(BasePlayer player, Item item)
	{
		return CanPlayerAdmin(player);
	}

	public override bool CanOpenLootPanel(BasePlayer player, string panelName)
	{
		if (panelName == customerPanel)
		{
			return true;
		}
		if (base.CanOpenLootPanel(player, panelName))
		{
			return CanPlayerAdmin(player);
		}
		return false;
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void RPC_DeleteSellOrder(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (CanPlayerAdmin(player))
		{
			int num = msg.read.Int32();
			if (num >= 0 && num < sellOrders.sellOrders.Count)
			{
				ProtoBuf.VendingMachine.SellOrder sellOrder = sellOrders.sellOrders[num];
				Analytics.Azure.OnVendingMachineOrderChanged(msg.player, this, sellOrder.itemToSellID, sellOrder.itemToSellAmount, sellOrder.itemToSellIsBP, sellOrder.currencyID, sellOrder.currencyAmountPerItem, sellOrder.currencyIsBP, added: false);
				sellOrders.sellOrders.RemoveAt(num);
			}
			RefreshSellOrderStockLevel();
			UpdateMapMarker();
			SendSellOrders(player);
		}
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void RPC_RotateVM(RPCMessage msg)
	{
		if (CanRotate())
		{
			UpdateEmptyFlag();
			if (msg.player.CanBuild() && IsInventoryEmpty())
			{
				base.transform.rotation = Quaternion.LookRotation(-base.transform.forward, base.transform.up);
				SendNetworkUpdate();
			}
		}
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void RPC_AddSellOrder(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (CanPlayerAdmin(player))
		{
			if (sellOrders.sellOrders.Count >= 7)
			{
				player.ShowToast(GameTip.Styles.Error, TooManySellOrders, true);
				return;
			}
			int num = msg.read.Int32();
			int num2 = msg.read.Int32();
			int num3 = msg.read.Int32();
			int num4 = msg.read.Int32();
			byte b = msg.read.UInt8();
			AddSellOrder(num, num2, num3, num4, b);
			Analytics.Azure.OnVendingMachineOrderChanged(msg.player, this, num, num2, b == 2 || b == 3, num3, num4, b == 1 || b == 3, added: true);
		}
	}

	public void AddSellOrder(int itemToSellID, int itemToSellAmount, int currencyToUseID, int currencyAmount, byte bpState)
	{
		ItemDefinition itemDefinition = ItemManager.FindItemDefinition(itemToSellID);
		ItemDefinition itemDefinition2 = ItemManager.FindItemDefinition(currencyToUseID);
		if (!(itemDefinition == null) && !(itemDefinition2 == null))
		{
			currencyAmount = Mathf.Clamp(currencyAmount, 1, 10000);
			itemToSellAmount = Mathf.Clamp(itemToSellAmount, 1, itemDefinition.stackable);
			ProtoBuf.VendingMachine.SellOrder sellOrder = new ProtoBuf.VendingMachine.SellOrder();
			sellOrder.ShouldPool = false;
			sellOrder.itemToSellID = itemToSellID;
			sellOrder.itemToSellAmount = itemToSellAmount;
			sellOrder.currencyID = currencyToUseID;
			sellOrder.currencyAmountPerItem = currencyAmount;
			sellOrder.currencyIsBP = bpState == 3 || bpState == 2;
			sellOrder.itemToSellIsBP = bpState == 3 || bpState == 1;
			sellOrders.sellOrders.Add(sellOrder);
			RefreshSellOrderStockLevel(itemDefinition);
			UpdateMapMarker();
			SendNetworkUpdate();
		}
	}

	public void RefreshAndSendNetworkUpdate()
	{
		RefreshSellOrderStockLevel();
		SendNetworkUpdate();
	}

	public void UpdateOrCreateSalesSheet()
	{
		ItemDefinition itemDefinition = ItemManager.FindItemDefinition("note");
		List<Item> list = base.inventory.FindItemsByItemID(itemDefinition.itemid);
		Item item = null;
		foreach (Item item4 in list)
		{
			if (item4.text.Length == 0)
			{
				item = item4;
				break;
			}
		}
		if (item == null)
		{
			ItemDefinition itemDefinition2 = ItemManager.FindItemDefinition("paper");
			Item item2 = base.inventory.FindItemByItemID(itemDefinition2.itemid);
			if (item2 != null)
			{
				item = ItemManager.CreateByItemID(itemDefinition.itemid, 1, 0uL);
				if (!item.MoveToContainer(base.inventory))
				{
					item.Drop(GetDropPosition(), GetDropVelocity());
				}
				item2.UseItem();
			}
		}
		if (item == null)
		{
			return;
		}
		foreach (ProtoBuf.VendingMachine.SellOrder sellOrder in sellOrders.sellOrders)
		{
			ItemDefinition itemDefinition3 = ItemManager.FindItemDefinition(sellOrder.itemToSellID);
			Item item3 = item;
			item3.text = item3.text + itemDefinition3.displayName.translated + "\n";
		}
		item.MarkDirty();
	}

	public void ClearContent()
	{
		if (!(this is NPCVendingMachine))
		{
			shopName = "A Shop";
			nameLastEditedBy = 0uL;
			SendNetworkUpdate();
			UpdateMapMarker();
		}
	}

	protected virtual bool CanShop(BasePlayer bp)
	{
		return true;
	}

	protected virtual bool CanRotate()
	{
		return !HasAttachedStorageAdaptor();
	}

	public bool IsBroadcasting()
	{
		return HasFlag(Flags.Reserved4);
	}

	public bool IsInventoryEmpty()
	{
		return HasFlag(Flags.Reserved1);
	}

	public bool IsVending()
	{
		return HasFlag(Flags.Reserved2);
	}

	public bool PlayerBehind(BasePlayer player)
	{
		return Vector3.Dot(base.transform.forward, (player.transform.position - base.transform.position).normalized) <= -0.7f;
	}

	public bool PlayerInfront(BasePlayer player)
	{
		return Vector3.Dot(base.transform.forward, (player.transform.position - base.transform.position).normalized) >= 0.7f;
	}

	public virtual bool CanPlayerAdmin(BasePlayer player)
	{
		if (PlayerBehind(player))
		{
			return OccupiedCheck(player);
		}
		return false;
	}

	public override bool SupportsChildDeployables()
	{
		return true;
	}

	public virtual string GetTranslationToken()
	{
		return "";
	}

	[ServerVar(Help = "Wipe the backend stats data on all vending machines. Slow operation.")]
	public static void ClearAllVendingHistory()
	{
		VendingMachine[] array = UnityEngine.Object.FindObjectsByType<VendingMachine>(FindObjectsSortMode.None);
		foreach (VendingMachine vendingMachine in array)
		{
			if (!vendingMachine.isClient)
			{
				vendingMachine.ClearPurchaseHistory();
			}
		}
	}

	[ServerVar(Help = "Wipe the backend customer stats data on all vending machines. Slow operation.")]
	public static void ClearAllVendingCustomerHistory()
	{
		VendingMachine[] array = UnityEngine.Object.FindObjectsByType<VendingMachine>(FindObjectsSortMode.None);
		foreach (VendingMachine vendingMachine in array)
		{
			if (!vendingMachine.isClient)
			{
				vendingMachine.ClearCustomerHistory();
			}
		}
	}

	[RPC_Server]
	public void SV_RequestLongTermData(RPCMessage msg)
	{
		int seconds = 86400;
		VendingMachineLongTermStats vendingMachineLongTermStats = Facepunch.Pool.Get<VendingMachineLongTermStats>();
		vendingMachineLongTermStats.numberOfPurchases = purchaseHistory.Count;
		vendingMachineLongTermStats.bestSalesHour = GetPeakSaleHourTimestamp(seconds);
		vendingMachineLongTermStats.uniqueCustomers = GetUniqueCustomers();
		vendingMachineLongTermStats.repeatCustomers = GetRepeatCustomers();
		vendingMachineLongTermStats.bestCustomer = GetBestCustomer();
		ClientRPC(RpcTarget.NetworkGroup("CL_ReceiveLongTermData"), vendingMachineLongTermStats);
		vendingMachineLongTermStats.Dispose();
	}

	[RPC_Server]
	public void SV_RequestPurchaseData(RPCMessage msg)
	{
		HistoryCategory historyCategory = (HistoryCategory)msg.read.Int32();
		int minutes = msg.read.Int32();
		VendingMachinePurchaseHistoryMessage proto = GetProto(historyCategory, minutes);
		ClientRPC(RpcTarget.NetworkGroup("CL_ReceivePurchaseData"), (int)historyCategory, proto);
		proto.Dispose();
	}

	public void AddPurchaseHistory(int itemId, int amount, int priceId, int price, bool itemIsBp, bool priceIsBp)
	{
		if (purchaseHistory.Count > max_history)
		{
			purchaseHistory.RemoveAt(0);
		}
		purchaseHistory.Add(new PurchaseDetails
		{
			itemId = itemId,
			amount = amount,
			priceId = priceId,
			price = price,
			timestamp = Epoch.Current,
			itemIsBp = itemIsBp,
			priceIsBp = priceIsBp
		});
	}

	public void RegisterCustomer(ulong userId)
	{
		if (uniqueCustomers.ContainsKey(userId))
		{
			uniqueCustomers[userId]++;
		}
		else
		{
			uniqueCustomers.Add(userId, 1);
		}
	}

	public void RemovePurchaseHistory(int index)
	{
		purchaseHistory.RemoveAt(index);
	}

	public void ClearPurchaseHistory()
	{
		purchaseHistory.Clear();
	}

	public void ClearCustomerHistory()
	{
		uniqueCustomers.Clear();
	}

	private VendingMachinePurchaseHistoryMessage GetProto(HistoryCategory category, int minutes)
	{
		if (minutes == 0)
		{
			minutes = 999999;
		}
		VendingMachinePurchaseHistoryMessage vendingMachinePurchaseHistoryMessage = Facepunch.Pool.Get<VendingMachinePurchaseHistoryMessage>();
		switch (category)
		{
		case HistoryCategory.History:
			vendingMachinePurchaseHistoryMessage.transactions = GetEntriesProto(GetRecentPurchases(minutes * 60));
			break;
		case HistoryCategory.BestSold:
			vendingMachinePurchaseHistoryMessage.smallTransactions = GetEntriesProtoSmall(GetBestSoldItems(minutes * 60));
			break;
		case HistoryCategory.MostRevenue:
			vendingMachinePurchaseHistoryMessage.smallTransactions = GetEntriesProtoSmall(GetMostRevenueGeneratingItems(minutes * 60));
			break;
		}
		return vendingMachinePurchaseHistoryMessage;
	}

	private List<VendingMachinePurchaseHistoryEntryMessage> GetEntriesProto(List<PurchaseDetails> details)
	{
		List<VendingMachinePurchaseHistoryEntryMessage> list = Facepunch.Pool.Get<List<VendingMachinePurchaseHistoryEntryMessage>>();
		foreach (PurchaseDetails detail in details)
		{
			list.Add(GetEntryProto(detail));
		}
		return list;
	}

	private List<PurchaseDetails> GetListFromProto(List<VendingMachinePurchaseHistoryEntryMessage> details)
	{
		List<PurchaseDetails> list = new List<PurchaseDetails>();
		foreach (VendingMachinePurchaseHistoryEntryMessage detail in details)
		{
			list.Add(new PurchaseDetails
			{
				itemId = detail.itemID,
				amount = detail.amount,
				priceId = detail.priceID,
				price = detail.price,
				timestamp = detail.dateTime,
				itemIsBp = detail.itemIsBp,
				priceIsBp = detail.priceIsBp
			});
		}
		return list;
	}

	private List<VendingMachinePurchaseHistoryEntrySmallMessage> GetEntriesProtoSmall(List<PurchaseDetails> details)
	{
		List<VendingMachinePurchaseHistoryEntrySmallMessage> list = Facepunch.Pool.Get<List<VendingMachinePurchaseHistoryEntrySmallMessage>>();
		foreach (PurchaseDetails detail in details)
		{
			list.Add(GetEntryProtoSmall(detail));
		}
		return list;
	}

	private VendingMachinePurchaseHistoryEntryMessage GetEntryProto(PurchaseDetails details)
	{
		VendingMachinePurchaseHistoryEntryMessage vendingMachinePurchaseHistoryEntryMessage = Facepunch.Pool.Get<VendingMachinePurchaseHistoryEntryMessage>();
		vendingMachinePurchaseHistoryEntryMessage.itemID = details.itemId;
		vendingMachinePurchaseHistoryEntryMessage.amount = details.amount;
		vendingMachinePurchaseHistoryEntryMessage.priceID = details.priceId;
		vendingMachinePurchaseHistoryEntryMessage.price = details.price;
		vendingMachinePurchaseHistoryEntryMessage.dateTime = details.timestamp;
		vendingMachinePurchaseHistoryEntryMessage.priceIsBp = details.priceIsBp;
		vendingMachinePurchaseHistoryEntryMessage.itemIsBp = details.itemIsBp;
		return vendingMachinePurchaseHistoryEntryMessage;
	}

	private VendingMachinePurchaseHistoryEntrySmallMessage GetEntryProtoSmall(PurchaseDetails details)
	{
		VendingMachinePurchaseHistoryEntrySmallMessage vendingMachinePurchaseHistoryEntrySmallMessage = Facepunch.Pool.Get<VendingMachinePurchaseHistoryEntrySmallMessage>();
		vendingMachinePurchaseHistoryEntrySmallMessage.itemID = details.itemId;
		vendingMachinePurchaseHistoryEntrySmallMessage.amount = details.amount;
		vendingMachinePurchaseHistoryEntrySmallMessage.priceID = details.priceId;
		vendingMachinePurchaseHistoryEntrySmallMessage.price = details.price;
		vendingMachinePurchaseHistoryEntrySmallMessage.priceIsBp = details.priceIsBp;
		vendingMachinePurchaseHistoryEntrySmallMessage.itemIsBp = details.itemIsBp;
		return vendingMachinePurchaseHistoryEntrySmallMessage;
	}

	public List<PurchaseDetails> GetRecentPurchases(int seconds)
	{
		int currentTime = Epoch.Current;
		return (from p in purchaseHistory
			where currentTime - p.timestamp <= seconds
			orderby p.timestamp descending
			select p).Take(max_returned).ToList();
	}

	public List<PurchaseDetails> GetBestSoldItems(int seconds)
	{
		int currentTime = Epoch.Current;
		return (from p in (from p in purchaseHistory
				where currentTime - p.timestamp <= seconds
				orderby p.timestamp descending
				select p).Take(max_processed)
			group p by new { p.itemId, p.itemIsBp, p.priceIsBp } into @group
			select new PurchaseDetails
			{
				itemId = @group.Key.itemId,
				amount = @group.Sum((PurchaseDetails p) => p.amount),
				priceId = 0,
				price = 0,
				timestamp = 0,
				itemIsBp = @group.Key.itemIsBp,
				priceIsBp = @group.Key.priceIsBp
			} into p
			orderby p.amount descending
			select p).Take(max_returned).ToList();
	}

	public List<PurchaseDetails> GetMostRevenueGeneratingItems(int seconds)
	{
		int currentTime = Epoch.Current;
		return (from p in (from p in purchaseHistory
				where currentTime - p.timestamp <= seconds
				orderby p.timestamp descending
				select p).Take(max_processed)
			group p by new { p.itemId, p.priceId, p.itemIsBp, p.priceIsBp } into @group
			select new PurchaseDetails
			{
				itemId = @group.Key.itemId,
				amount = @group.Sum((PurchaseDetails p) => p.amount),
				priceId = @group.Key.priceId,
				price = @group.Sum((PurchaseDetails p) => p.price),
				timestamp = 0,
				itemIsBp = @group.Key.itemIsBp,
				priceIsBp = @group.Key.priceIsBp
			} into p
			orderby p.price descending
			select p).Take(max_returned).ToList();
	}

	public long GetPeakSaleHourTimestamp(int seconds)
	{
		int currentTime = Epoch.Current;
		return (from p in (from p in purchaseHistory
				where currentTime - p.timestamp <= seconds
				orderby p.timestamp descending
				select p).Take(max_processed)
			group p by p.timestamp into @group
			select new
			{
				Timestamp = @group.Key,
				TotalSales = @group.Sum((PurchaseDetails p) => p.amount)
			} into s
			orderby s.TotalSales descending
			select s).FirstOrDefault()?.Timestamp ?? (-1);
	}

	public int GetUniqueCustomers()
	{
		return uniqueCustomers.Count;
	}

	public int GetRepeatCustomers()
	{
		return uniqueCustomers.Count((KeyValuePair<ulong, int> c) => c.Value > 1);
	}

	public int GetBestCustomer()
	{
		if (uniqueCustomers.Count == 0)
		{
			return 0;
		}
		return uniqueCustomers.Values.Max();
	}
}
