using System;
using System.Collections.Generic;
using Facepunch;
using ProtoBuf;
using UnityEngine;

public class VendingMachineMapMarker : MapMarker
{
	public string markerShopName;

	public VendingMachine server_vendingMachine;

	public ProtoBuf.VendingMachine client_vendingMachine;

	[NonSerialized]
	public NetworkableId client_vendingMachineNetworkID;

	public GameObjectRef clusterMarkerObj;

	private UIMapVendingMachineMarker myUIMarker;

	private RectTransform markerTransform;

	public void SetVendingMachine(VendingMachine vm, string shopName)
	{
		_ = vm == null;
		server_vendingMachine = vm;
		markerShopName = shopName;
		if (!IsInvoking(TryUpdatePosition))
		{
			InvokeRandomized(TryUpdatePosition, 30f, 30f, 10f);
		}
	}

	public void TryUpdatePosition()
	{
		if (server_vendingMachine != null && server_vendingMachine.GetParentEntity() != null)
		{
			base.transform.position = server_vendingMachine.transform.position;
			try
			{
				syncPosition = true;
				NetworkPositionTick();
			}
			finally
			{
				syncPosition = false;
			}
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.vendingMachine = Pool.Get<ProtoBuf.VendingMachine>();
		info.msg.vendingMachine.shopName = markerShopName;
		if (server_vendingMachine != null)
		{
			if (server_vendingMachine is NPCVendingMachine { IsLocalized: not false } nPCVendingMachine)
			{
				info.msg.vendingMachine.translationToken = nPCVendingMachine.GetTranslationToken();
			}
			info.msg.vendingMachine.networkID = server_vendingMachine.net.ID;
			info.msg.vendingMachine.sellOrderContainer = server_vendingMachine.sellOrders.Copy();
		}
	}

	public override AppMarker GetAppMarkerData()
	{
		AppMarker appMarkerData = base.GetAppMarkerData();
		appMarkerData.name = markerShopName ?? "";
		appMarkerData.outOfStock = !HasFlag(Flags.Busy);
		if (server_vendingMachine != null)
		{
			appMarkerData.sellOrders = Pool.Get<List<AppMarker.SellOrder>>();
			foreach (ProtoBuf.VendingMachine.SellOrder sellOrder2 in server_vendingMachine.sellOrders.sellOrders)
			{
				AppMarker.SellOrder sellOrder = Pool.Get<AppMarker.SellOrder>();
				sellOrder.itemId = sellOrder2.itemToSellID;
				sellOrder.quantity = sellOrder2.itemToSellAmount;
				sellOrder.currencyId = sellOrder2.currencyID;
				sellOrder.costPerItem = sellOrder2.currencyAmountPerItem;
				sellOrder.amountInStock = sellOrder2.inStock;
				sellOrder.itemIsBlueprint = sellOrder2.itemToSellIsBP;
				sellOrder.currencyIsBlueprint = sellOrder2.currencyIsBP;
				sellOrder.itemCondition = sellOrder2.itemCondition;
				sellOrder.itemConditionMax = sellOrder2.itemConditionMax;
				sellOrder.priceMultiplier = sellOrder2.priceMultiplier;
				appMarkerData.sellOrders.Add(sellOrder);
			}
		}
		return appMarkerData;
	}
}
