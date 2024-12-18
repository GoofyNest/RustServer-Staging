using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ConVar;
using Facepunch;
using Facepunch.Rust;
using ProtoBuf;
using Rust;
using UnityEngine;

public class NPCVendingMachine : VendingMachine
{
	public class SalesData
	{
		public ulong TotalSales;

		public ulong TotalIntervals;

		public ulong SoldThisInterval;

		public float CurrentMultiplier;

		public bool IsForReceivedCurrency;

		public double GetAverageSalesPerInterval()
		{
			if (TotalSales == 0L || TotalIntervals == 0L)
			{
				return 0.0;
			}
			return (double)TotalSales / (double)TotalIntervals;
		}

		public void RecordSale(int count)
		{
			SoldThisInterval += (ulong)count;
		}

		public void ProcessEndOfInterval()
		{
			double averageSalesPerInterval = GetAverageSalesPerInterval();
			bool flag = TotalIntervals == 0;
			TotalSales += SoldThisInterval;
			TotalIntervals++;
			SoldThisInterval = 0uL;
			float num = 0f;
			num = ((!(GetAverageSalesPerInterval() <= averageSalesPerInterval || flag)) ? PriceIncreaseAmount : (0f - PriceDecreaseAmount));
			if (IsForReceivedCurrency)
			{
				CurrentMultiplier -= num;
			}
			else
			{
				CurrentMultiplier += num;
			}
			CurrentMultiplier = Mathf.Clamp(CurrentMultiplier, MinimumPriceMultiplier, MaximumPriceMultiplier);
		}
	}

	public NPCVendingOrder vendingOrders;

	public Translate.Phrase Phrase;

	public float RefillTime = 1f;

	public int StartingStock = 10;

	public bool BypassDynamicPricing;

	public const int MaxVendingEntries = 7;

	private static ListHashSet<NPCVendingMachine> allNpcVendingMachines = new ListHashSet<NPCVendingMachine>();

	private float[] refillTimes;

	[ServerVar(Saved = true, Help = "Whether to run the the dynamic pricing system")]
	public static bool DynamicPricingEnabled = true;

	[ServerVar(Saved = true, Help = "How many realtime hours are checked when looking for price increases. Max 72 (10 days), min 0.5 (half an hour)", ShowInAdminUI = true)]
	public static float PriceUpdateFrequencyDefault = 3f;

	[ServerVar(Saved = true, Help = "How many realtime hours are checked when looking for price increases. Max 72 (10 days), min 0.5 (half an hour)", ShowInAdminUI = true)]
	public static float PriceUpdateFrequencyBiWeekly = 2f;

	[ServerVar(Saved = true, Help = "How many realtime hours are checked when looking for price increases. Max 72 (10 days), min 0.5 (half an hour)", ShowInAdminUI = true)]
	public static float PriceUpdateFrequencyWeekly = 1f;

	private static bool hasCachedTags = false;

	private static bool cachedBiWeekly;

	private static bool cachedWeekly;

	[ServerVar(Saved = true, Help = "The maximum point that a price can increase to (2 = 200%)")]
	public static float MaximumPriceMultiplier = 2f;

	[ServerVar(Saved = true, Help = "The Minimum point that the price can drop to (0.5 = 50% off)")]
	public static float MinimumPriceMultiplier = 0.5f;

	[ServerVar(Saved = true, Help = "What discount surcharge should be applied to items when the server starts")]
	public static float StartingPriceMultiplier = 2f;

	[ServerVar(Saved = true, Help = "How much to increase the price by if it is selling a lot (0.05 = 5%)")]
	public static float PriceIncreaseAmount = 0.1f;

	[ServerVar(Saved = true, Help = "How much to decrease the price for if it is underselling (0.05 = 5%)")]
	public static float PriceDecreaseAmount = 0.05f;

	private SalesData[] allSalesData;

	private float timeToNextSalesUpdate;

	private bool preserveSalesData;

	private static ItemDefinition _scrapItem = null;

	private TimeSince lastHourCheck;

	protected virtual bool BlockOrderRefreshOnLoad => false;

	public override bool ShouldRecordStats => false;

	private static float ScaledByWipeUpdateFrequency
	{
		get
		{
			if (!hasCachedTags)
			{
				cachedBiWeekly = ConVar.Server.tags.Contains("biweekly", CompareOptions.IgnoreCase);
				cachedWeekly = ConVar.Server.tags.Contains("weekly", CompareOptions.IgnoreCase);
				hasCachedTags = true;
			}
			if (cachedBiWeekly)
			{
				return PriceUpdateFrequencyBiWeekly;
			}
			if (cachedWeekly)
			{
				return PriceUpdateFrequencyWeekly;
			}
			return PriceUpdateFrequencyDefault;
		}
	}

	public static float IntervalSeconds => Mathf.Clamp(ScaledByWipeUpdateFrequency, 0.5f, 72f) * 60f * 60f;

	public static ItemDefinition ScrapItem
	{
		get
		{
			if (_scrapItem == null)
			{
				_scrapItem = ItemManager.FindItemDefinition("scrap");
			}
			return _scrapItem;
		}
	}

	private bool CanApplyDynamicPricing
	{
		get
		{
			if (!BypassDynamicPricing)
			{
				return DynamicPricingEnabled;
			}
			return false;
		}
	}

	public byte GetBPState(bool sellItemAsBP, bool currencyItemAsBP)
	{
		byte result = 0;
		if (sellItemAsBP)
		{
			result = 1;
		}
		if (currencyItemAsBP)
		{
			result = 2;
		}
		if (sellItemAsBP && currencyItemAsBP)
		{
			result = 3;
		}
		return result;
	}

	public override void TakeCurrencyItem(Item takenCurrencyItem)
	{
		takenCurrencyItem.MoveToContainer(base.inventory);
		takenCurrencyItem.RemoveFromContainer();
		takenCurrencyItem.Remove();
	}

	public override void GiveSoldItem(Item soldItem, BasePlayer buyer)
	{
		base.GiveSoldItem(soldItem, buyer);
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		if (!BlockOrderRefreshOnLoad)
		{
			Invoke(InstallFromVendingOrders, 1f);
		}
	}

	public void ChangeRefillTime(float newRefillTime)
	{
		RefillTime = newRefillTime;
		if (IsInvoking(Refill))
		{
			CancelInvoke(Refill);
		}
		InvokeRandomized(Refill, 1f, RefillTime, 0.1f);
	}

	public override void ServerInit()
	{
		base.ServerInit();
		skinID = 861142659uL;
		SendNetworkUpdate();
		if (!BlockOrderRefreshOnLoad || !Rust.Application.isLoadingSave)
		{
			Invoke(InstallFromVendingOrders, 1f);
		}
		if (!IsInvoking(Refill))
		{
			InvokeRandomized(Refill, 1f, RefillTime, 0.1f);
		}
		DynamicPricingServerInit();
		allNpcVendingMachines.TryAdd(this);
	}

	internal override void DoServerDestroy()
	{
		base.DoServerDestroy();
		allNpcVendingMachines.Remove(this);
	}

	public virtual void InstallFromVendingOrders()
	{
		if (vendingOrders == null)
		{
			Debug.LogError("No vending orders!");
			return;
		}
		int count = sellOrders.sellOrders.Count;
		ClearSellOrders();
		base.inventory.Clear();
		ItemManager.DoRemoves();
		if (vendingOrders.orders.Length <= 7)
		{
			if (count == vendingOrders.orders.Length)
			{
				preserveSalesData = true;
			}
			try
			{
				NPCVendingOrder.Entry[] orders = vendingOrders.orders;
				foreach (NPCVendingOrder.Entry ent in orders)
				{
					SmartAddItemForSale(ent);
				}
				return;
			}
			finally
			{
				preserveSalesData = false;
			}
		}
		List<NPCVendingOrder.Entry> obj = Facepunch.Pool.Get<List<NPCVendingOrder.Entry>>();
		vendingOrders.GetRandomEntries(7, obj);
		foreach (NPCVendingOrder.Entry item in obj)
		{
			SmartAddItemForSale(item);
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	private void SmartAddItemForSale(NPCVendingOrder.Entry ent)
	{
		int currencyPerTransaction = ent.currencyAmount;
		if (ent.randomDetails.useRandom)
		{
			currencyPerTransaction = ent.randomDetails.GetRandomPrice();
		}
		AddItemForSale(ent.sellItem.itemid, ent.sellItemAmount, ent.currencyItem.itemid, currencyPerTransaction, GetBPState(ent.sellItemAsBP, ent.currencyAsBP));
	}

	public override void InstallDefaultSellOrders()
	{
		base.InstallDefaultSellOrders();
	}

	public void Refill()
	{
		if (vendingOrders == null || vendingOrders.orders == null || base.inventory == null)
		{
			return;
		}
		if (refillTimes == null)
		{
			refillTimes = new float[vendingOrders.orders.Length];
		}
		for (int i = 0; i < vendingOrders.orders.Length; i++)
		{
			NPCVendingOrder.Entry entry = vendingOrders.orders[i];
			if (!(UnityEngine.Time.realtimeSinceStartup > refillTimes[i]))
			{
				continue;
			}
			int num = 0;
			num = ((!entry.sellItemAsBP) ? Mathf.FloorToInt(base.inventory.GetAmount(entry.sellItem.itemid, onlyUsableAmounts: false) / entry.sellItemAmount) : Mathf.FloorToInt(base.inventory.GetAmount(base.blueprintBaseDef.itemid, entry.sellItem.itemid, onlyUsableAmounts: false) / entry.sellItemAmount));
			int num2 = Mathf.Min(StartingStock - num, entry.refillAmount) * entry.sellItemAmount;
			if (num2 > 0)
			{
				transactionActive = true;
				Item item = null;
				if (entry.sellItemAsBP)
				{
					item = ItemManager.Create(base.blueprintBaseDef, num2, 0uL);
					item.blueprintTarget = entry.sellItem.itemid;
				}
				else
				{
					item = ItemManager.Create(entry.sellItem, num2, 0uL);
				}
				if (!item.MoveToContainer(base.inventory))
				{
					item.Remove();
				}
				transactionActive = false;
			}
			refillTimes[i] = UnityEngine.Time.realtimeSinceStartup + entry.refillDelay;
		}
	}

	public void ClearSellOrders()
	{
		sellOrders.sellOrders.Clear();
	}

	public void AddItemForSale(int itemID, int amountToSell, int currencyID, int currencyPerTransaction, byte bpState)
	{
		AddSellOrder(itemID, amountToSell, currencyID, currencyPerTransaction, bpState);
		transactionActive = true;
		int startingStock = StartingStock;
		if (bpState == 1 || bpState == 3)
		{
			for (int i = 0; i < startingStock; i++)
			{
				Item item = ItemManager.CreateByItemID(base.blueprintBaseDef.itemid, 1, 0uL);
				item.blueprintTarget = itemID;
				base.inventory.Insert(item);
			}
		}
		else
		{
			base.inventory.AddItem(ItemManager.FindItemDefinition(itemID), amountToSell * startingStock, 0uL);
		}
		transactionActive = false;
		RefreshSellOrderStockLevel();
	}

	public void RefreshStock()
	{
	}

	protected override void RecordSaleAnalytics(Item itemSold, int orderId, int currencyUsed)
	{
		RecordSale(orderId, itemSold.amount, currencyUsed);
		Analytics.Server.VendingMachineTransaction(vendingOrders, itemSold.info, itemSold.amount);
	}

	public override string GetTranslationToken()
	{
		return Phrase.token;
	}

	protected override bool CanRotate()
	{
		return false;
	}

	public override bool CanPlayerAdmin(BasePlayer player)
	{
		return false;
	}

	[ServerVar]
	public static void ResetFrequencyTags(ConsoleSystem.Arg arg)
	{
		hasCachedTags = false;
		arg.ReplyWith($"Reset frequency tags. Scaled frequency is now:{ScaledByWipeUpdateFrequency} hours");
	}

	[ServerVar(Help = "Resets the state of all discounts and surcharges from NPC vending machines")]
	public static void resetDynamicPricing()
	{
		foreach (NPCVendingMachine allNpcVendingMachine in allNpcVendingMachines)
		{
			allNpcVendingMachine.ResetDynamicPricing();
		}
	}

	[ServerVar(Help = "Print out all current price changes on the server")]
	public static void printAllPriceChanges(ConsoleSystem.Arg arg)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (NPCVendingMachine allNpcVendingMachine in allNpcVendingMachines)
		{
			TextTable textTable = new TextTable();
			textTable.AddColumns("Item Name", "Original Price", "Discount/Surcharge", "Final Price", "Avg Sales/Interval", "Current Sales/Interval", "Total Sales", "Intervals");
			int num = 0;
			int num2 = 0;
			foreach (ProtoBuf.VendingMachine.SellOrder sellOrder in allNpcVendingMachine.sellOrders.sellOrders)
			{
				if (sellOrder.priceMultiplier != 1f)
				{
					num++;
					ItemDefinition itemDefinition = ItemManager.FindItemDefinition(sellOrder.itemToSellID);
					int totalPriceForOrder = VendingMachine.GetTotalPriceForOrder(sellOrder);
					SalesData salesData = allNpcVendingMachine.allSalesData[num2];
					textTable.AddRow(itemDefinition.shortname, sellOrder.currencyAmountPerItem.ToString(), $"{Mathf.RoundToInt(sellOrder.priceMultiplier * 100f)}%", $"{totalPriceForOrder}", salesData.GetAverageSalesPerInterval().ToString(), salesData.SoldThisInterval.ToString(), salesData.TotalSales.ToString(), salesData.TotalIntervals.ToString());
				}
				num2++;
			}
			if (num > 0)
			{
				stringBuilder.AppendLine(allNpcVendingMachine.shopName);
				stringBuilder.AppendLine("==============");
				stringBuilder.AppendLine(textTable.ToString());
			}
		}
		arg.ReplyWith(stringBuilder.ToString());
	}

	[ServerVar(Help = "Simulates the provided number of hours passing in the vending machine system")]
	public static void addHours(ConsoleSystem.Arg arg)
	{
		float num = (float)arg.GetInt(0) * 60f * 60f;
		foreach (NPCVendingMachine allNpcVendingMachine in allNpcVendingMachines)
		{
			allNpcVendingMachine.lastHourCheck = (float)allNpcVendingMachine.lastHourCheck + num;
			allNpcVendingMachine.HourCheck();
		}
	}

	public void RecordSale(int index, int countReceived, int currencyUsed)
	{
		if (CanApplyDynamicPricing)
		{
			CheckSalesDataLength();
			SalesData obj = allSalesData[index];
			obj.RecordSale(obj.IsForReceivedCurrency ? currencyUsed : countReceived);
		}
	}

	private void CheckSalesDataLength(bool reset = false)
	{
		if (reset)
		{
			allSalesData = null;
		}
		int count = sellOrders.sellOrders.Count;
		if (allSalesData == null || allSalesData.Length != count)
		{
			allSalesData = new SalesData[count];
			for (int i = 0; i < count; i++)
			{
				bool flag = sellOrders.sellOrders[i].itemToSellID == ScrapItem.itemid;
				allSalesData[i] = new SalesData
				{
					IsForReceivedCurrency = flag,
					CurrentMultiplier = (flag ? MinimumPriceMultiplier : StartingPriceMultiplier)
				};
			}
		}
	}

	protected override float GetDiscountForSlot(int sellOrderSlot, ProtoBuf.VendingMachine.SellOrder forOrder)
	{
		if (!CanApplyDynamicPricing)
		{
			return 1f;
		}
		if (!preserveSalesData)
		{
			CheckSalesDataLength();
		}
		if (sellOrderSlot < 0 || sellOrderSlot >= allSalesData.Length)
		{
			return 1f;
		}
		if (forOrder.currencyID != ScrapItem.itemid)
		{
			return 1f;
		}
		return allSalesData[sellOrderSlot].CurrentMultiplier;
	}

	protected override float GetReceivedQuantityMultiplier(int sellOrderSlot, ProtoBuf.VendingMachine.SellOrder forOrder)
	{
		if (!CanApplyDynamicPricing)
		{
			return 1f;
		}
		if (!preserveSalesData)
		{
			CheckSalesDataLength();
		}
		if (sellOrderSlot < 0 || sellOrderSlot >= allSalesData.Length)
		{
			return 1f;
		}
		if (forOrder.itemToSellID != ScrapItem.itemid)
		{
			return 1f;
		}
		return allSalesData[sellOrderSlot].CurrentMultiplier;
	}

	private void DynamicPricingServerInit()
	{
		timeToNextSalesUpdate = IntervalSeconds;
		InvokeRandomized(HourCheck, 1f, 15f, 0.1f);
	}

	private void ResetDynamicPricing()
	{
		CheckSalesDataLength(reset: true);
		timeToNextSalesUpdate = IntervalSeconds;
		HourCheck();
	}

	private void HourCheck()
	{
		if (!CanApplyDynamicPricing)
		{
			return;
		}
		float num = lastHourCheck;
		lastHourCheck = 0f;
		timeToNextSalesUpdate -= num;
		while (timeToNextSalesUpdate < 0f)
		{
			timeToNextSalesUpdate += IntervalSeconds;
			SalesData[] array = allSalesData;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].ProcessEndOfInterval();
			}
			UpdateMapMarker();
			RefreshAndSendNetworkUpdate();
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		if (allSalesData != null && info.forDisk)
		{
			info.msg.vendingDynamicPricing = Facepunch.Pool.Get<VendingDynamicPricing>();
			info.msg.vendingDynamicPricing.allSalesData = Facepunch.Pool.Get<List<ProtoBuf.SalesData>>();
			info.msg.vendingDynamicPricing.timeToNextSalesUpdate = timeToNextSalesUpdate;
			SalesData[] array = allSalesData;
			foreach (SalesData salesData in array)
			{
				ProtoBuf.SalesData salesData2 = Facepunch.Pool.Get<ProtoBuf.SalesData>();
				salesData2.totalSales = salesData.TotalSales;
				salesData2.totalIntervals = salesData.TotalIntervals;
				salesData2.soldThisInterval = salesData.SoldThisInterval;
				salesData2.currentMultiplier = salesData.CurrentMultiplier;
				salesData2.isForReceivedQuantity = salesData.IsForReceivedCurrency;
				info.msg.vendingDynamicPricing.allSalesData.Add(salesData2);
			}
		}
	}

	public override void Load(LoadInfo info)
	{
		if (info.msg.vendingDynamicPricing != null)
		{
			allSalesData = new SalesData[info.msg.vendingDynamicPricing.allSalesData.Count];
			int num = 0;
			timeToNextSalesUpdate = info.msg.vendingDynamicPricing.timeToNextSalesUpdate;
			foreach (ProtoBuf.SalesData allSalesDatum in info.msg.vendingDynamicPricing.allSalesData)
			{
				SalesData salesData = new SalesData
				{
					TotalSales = allSalesDatum.totalSales,
					TotalIntervals = allSalesDatum.totalIntervals,
					SoldThisInterval = allSalesDatum.soldThisInterval,
					CurrentMultiplier = allSalesDatum.currentMultiplier,
					IsForReceivedCurrency = allSalesDatum.isForReceivedQuantity
				};
				allSalesData[num] = salesData;
				num++;
			}
		}
		base.Load(info);
	}
}
