#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ConVar;
using Facepunch;
using Network;
using Newtonsoft.Json;
using ProtoBuf;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

public class IndustrialConveyor : IndustrialEntity
{
	public enum ConveyorMode
	{
		Any,
		And,
		Not
	}

	[JsonModel]
	public struct ItemFilter
	{
		[JsonIgnore]
		public ItemDefinition TargetItem;

		public ItemCategory? TargetCategory;

		public int MaxAmountInOutput;

		public int BufferAmount;

		public int MinAmountInInput;

		public bool IsBlueprint;

		public int BufferTransferRemaining;

		public string TargetItemName
		{
			get
			{
				if (!(TargetItem != null))
				{
					return string.Empty;
				}
				return TargetItem.shortname;
			}
			set
			{
				TargetItem = ItemManager.FindItemDefinition(value);
			}
		}

		public void CopyTo(ProtoBuf.IndustrialConveyor.ItemFilter target)
		{
			if (TargetItem != null)
			{
				target.itemDef = TargetItem.itemid;
			}
			target.maxAmountInDestination = MaxAmountInOutput;
			if (TargetCategory.HasValue)
			{
				target.itemCategory = (int)TargetCategory.Value;
			}
			else
			{
				target.itemCategory = -1;
			}
			target.isBlueprint = (IsBlueprint ? 1 : 0);
			target.bufferAmount = BufferAmount;
			target.retainMinimum = MinAmountInInput;
			target.bufferTransferRemaining = BufferTransferRemaining;
		}

		public ItemFilter(ProtoBuf.IndustrialConveyor.ItemFilter from)
		{
			this = new ItemFilter
			{
				TargetItem = ItemManager.FindItemDefinition(from.itemDef),
				MaxAmountInOutput = from.maxAmountInDestination
			};
			if (from.itemCategory >= 0)
			{
				TargetCategory = (ItemCategory)from.itemCategory;
			}
			else
			{
				TargetCategory = null;
			}
			IsBlueprint = from.isBlueprint == 1;
			BufferAmount = from.bufferAmount;
			MinAmountInInput = from.retainMinimum;
			BufferTransferRemaining = from.bufferTransferRemaining;
		}
	}

	public int MaxStackSizePerMove = 128;

	public GameObjectRef FilterDialog;

	private const float ScreenUpdateRange = 30f;

	public const Flags FilterPassFlag = Flags.Reserved9;

	public const Flags FilterFailFlag = Flags.Reserved10;

	public const int MaxContainerDepth = 32;

	public SoundDefinition transferItemSoundDef;

	public SoundDefinition transferItemStartSoundDef;

	private List<ItemFilter> filterItems = new List<ItemFilter>();

	private ConveyorMode mode;

	public const int MAX_FILTER_SIZE = 30;

	public Image IconTransferImage;

	private bool refreshInputOutputs;

	private IIndustrialStorage workerOutput;

	private Func<IIndustrialStorage, int, bool> filterFunc;

	private List<ContainerInputOutput> splitOutputs = new List<ContainerInputOutput>();

	private List<ContainerInputOutput> splitInputs = new List<ContainerInputOutput>();

	private bool? lastFilterState;

	private Stopwatch transferStopWatch = new Stopwatch();

	private bool multiFrameTransferInProcess;

	private int multiFrameOutputIndex;

	private int multiFrameInputIndex;

	private bool isFirstTransfer = true;

	private bool wasOnWhenPowerLost;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("IndustrialConveyor.OnRpcMessage"))
		{
			if (rpc == 617569194 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					UnityEngine.Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_ChangeFilters ");
				}
				using (TimeWarning.New("RPC_ChangeFilters"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(617569194u, "RPC_ChangeFilters", this, player, 1uL))
						{
							return true;
						}
						if (!RPC_Server.MaxDistance.Test(617569194u, "RPC_ChangeFilters", this, player, 3f))
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
							RPC_ChangeFilters(msg2);
						}
					}
					catch (Exception exception)
					{
						UnityEngine.Debug.LogException(exception);
						player.Kick("RPC Error in RPC_ChangeFilters");
					}
				}
				return true;
			}
			if (rpc == 3731379386u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					UnityEngine.Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_RequestUpToDateFilters ");
				}
				using (TimeWarning.New("Server_RequestUpToDateFilters"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3731379386u, "Server_RequestUpToDateFilters", this, player, 1uL))
						{
							return true;
						}
						if (!RPC_Server.IsVisible.Test(3731379386u, "Server_RequestUpToDateFilters", this, player, 3f))
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
							Server_RequestUpToDateFilters(msg3);
						}
					}
					catch (Exception exception2)
					{
						UnityEngine.Debug.LogException(exception2);
						player.Kick("RPC Error in Server_RequestUpToDateFilters");
					}
				}
				return true;
			}
			if (rpc == 4167839872u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					UnityEngine.Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SvSwitch ");
				}
				using (TimeWarning.New("SvSwitch"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(4167839872u, "SvSwitch", this, player, 2uL))
						{
							return true;
						}
						if (!RPC_Server.IsVisible.Test(4167839872u, "SvSwitch", this, player, 3f))
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
							SvSwitch(msg4);
						}
					}
					catch (Exception exception3)
					{
						UnityEngine.Debug.LogException(exception3);
						player.Kick("RPC Error in SvSwitch");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void OnFlagsChanged(Flags old, Flags next)
	{
		base.OnFlagsChanged(old, next);
		bool flag = next.HasFlag(Flags.On);
		if (old.HasFlag(Flags.On) != flag && base.isServer)
		{
			float conveyorMoveFrequency = ConVar.Server.conveyorMoveFrequency;
			if (flag && conveyorMoveFrequency > 0f)
			{
				InvokeRandomized(ScheduleMove, conveyorMoveFrequency, conveyorMoveFrequency, conveyorMoveFrequency * 0.5f);
			}
			else
			{
				CancelInvoke(ScheduleMove);
			}
		}
	}

	private void ScheduleMove()
	{
		IndustrialEntity.Queue.Add(this);
	}

	private Item GetItemToMove(IIndustrialStorage storage, out ItemFilter associatedFilter, int slot, ItemContainer targetContainer = null)
	{
		associatedFilter = default(ItemFilter);
		(ItemFilter, int) tuple = default((ItemFilter, int));
		if (storage == null || storage.Container == null)
		{
			return null;
		}
		if (storage.Container.IsEmpty())
		{
			return null;
		}
		Vector2i vector2i = storage.OutputSlotRange(slot);
		for (int i = vector2i.x; i <= vector2i.y; i++)
		{
			Item slot2 = storage.Container.GetSlot(i);
			tuple = default((ItemFilter, int));
			if (slot2 != null && (filterItems.Count == 0 || FilterHasItem(slot2, out tuple)))
			{
				(associatedFilter, _) = tuple;
				if (targetContainer == null || !(associatedFilter.TargetItem != null) || associatedFilter.MaxAmountInOutput <= 0 || targetContainer.GetTotalItemAmount(slot2, vector2i.x, vector2i.y) < associatedFilter.MaxAmountInOutput)
				{
					return slot2;
				}
			}
		}
		return null;
	}

	private bool FilterHasItem(Item item, out (ItemFilter filter, int index) filter)
	{
		filter = default((ItemFilter, int));
		for (int i = 0; i < filterItems.Count; i++)
		{
			ItemFilter itemFilter = filterItems[i];
			if (FilterMatches(itemFilter, item))
			{
				filter = (filter: itemFilter, index: i);
				return true;
			}
		}
		return false;
	}

	private bool FilterMatches(ItemFilter filter, Item item)
	{
		if (item.IsBlueprint() && filter.IsBlueprint && item.blueprintTargetDef == filter.TargetItem)
		{
			return true;
		}
		if (filter.TargetItem == item.info && !filter.IsBlueprint)
		{
			return true;
		}
		if (filter.TargetItem != null && item.info.isRedirectOf == filter.TargetItem)
		{
			return true;
		}
		if (filter.TargetCategory.HasValue && item.info.category == filter.TargetCategory)
		{
			return true;
		}
		return false;
	}

	private bool FilterContainerInput(IIndustrialStorage storage, int slot)
	{
		ItemFilter associatedFilter;
		return GetItemToMove(storage, out associatedFilter, slot, workerOutput?.Container) != null;
	}

	protected override void RunJob()
	{
		base.RunJob();
		if (ConVar.Server.conveyorMoveFrequency <= 0f)
		{
			return;
		}
		if (filterFunc == null)
		{
			filterFunc = FilterContainerInput;
		}
		if (refreshInputOutputs)
		{
			refreshInputOutputs = false;
			splitInputs.Clear();
			splitOutputs.Clear();
			List<IOEntity> obj = Facepunch.Pool.Get<List<IOEntity>>();
			FindContainerSource(splitInputs, 32, input: true, obj);
			obj.Clear();
			FindContainerSource(splitOutputs, 32, input: false, obj, -1, MaxStackSizePerMove);
			Facepunch.Pool.FreeUnmanaged(ref obj);
			multiFrameTransferInProcess = false;
			multiFrameInputIndex = 0;
			multiFrameOutputIndex = 0;
		}
		bool hasItems = CheckIfAnyInputPassesFilters(splitInputs);
		if ((!lastFilterState.HasValue || hasItems != lastFilterState) && !hasItems)
		{
			UpdateFilterPassthroughs();
		}
		if (!hasItems)
		{
			return;
		}
		transferStopWatch.Restart();
		IndustrialConveyorTransfer transfer = Facepunch.Pool.Get<IndustrialConveyorTransfer>();
		try
		{
			bool flag = false;
			bool flag2 = false;
			transfer.ItemTransfers = Facepunch.Pool.Get<List<IndustrialConveyorTransfer.ItemTransfer>>();
			transfer.inputEntities = Facepunch.Pool.Get<List<NetworkableId>>();
			transfer.outputEntities = Facepunch.Pool.Get<List<NetworkableId>>();
			List<int> obj2 = Facepunch.Pool.Get<List<int>>();
			int num = 0;
			int count = splitOutputs.Count;
			bool flag3 = false;
			foreach (ContainerInputOutput splitOutput in splitOutputs)
			{
				workerOutput = splitOutput.Storage;
				if (multiFrameTransferInProcess && multiFrameOutputIndex > num)
				{
					num++;
					continue;
				}
				int num2 = 0;
				foreach (ContainerInputOutput splitInput in splitInputs)
				{
					int num3 = 0;
					num2++;
					if (multiFrameTransferInProcess && num2 < multiFrameInputIndex)
					{
						continue;
					}
					if (multiFrameTransferInProcess)
					{
						multiFrameTransferInProcess = false;
					}
					IIndustrialStorage storage = splitInput.Storage;
					if (storage == null || splitOutput.Storage == null || splitInput.Storage.IndustrialEntity == splitOutput.Storage.IndustrialEntity)
					{
						continue;
					}
					ItemContainer container = storage.Container;
					ItemContainer container2 = splitOutput.Storage.Container;
					if (container == null || container2 == null || storage.Container == null || storage.Container.IsEmpty())
					{
						continue;
					}
					(ItemFilter, int) filter2 = default((ItemFilter, int));
					Vector2i vector2i = storage.OutputSlotRange(splitInput.SlotIndex);
					for (int i = vector2i.x; i <= vector2i.y; i++)
					{
						Vector2i range = splitOutput.Storage.InputSlotRange(splitOutput.SlotIndex);
						Item slot = storage.Container.GetSlot(i);
						if (slot == null)
						{
							continue;
						}
						bool flag4 = true;
						if (filterItems.Count > 0)
						{
							if (mode == ConveyorMode.Any || mode == ConveyorMode.And)
							{
								flag4 = FilterHasItem(slot, out filter2);
							}
							if (mode == ConveyorMode.Not)
							{
								flag4 = !FilterHasItem(slot, out filter2);
							}
						}
						if (!flag4)
						{
							continue;
						}
						bool flag5 = mode == ConveyorMode.And || mode == ConveyorMode.Any;
						if (flag5 && filter2.Item1.TargetItem != null && filter2.Item1.MaxAmountInOutput > 0 && splitOutput.Storage.Container.GetTotalItemAmount(slot, range.x, range.y) >= filter2.Item1.MaxAmountInOutput)
						{
							flag = true;
							continue;
						}
						int num4 = (int)((float)Mathf.Min(MaxStackSizePerMove, slot.info.stackable) / (float)count);
						if (flag5 && filter2.Item1.MinAmountInInput > 0)
						{
							if (filter2.Item1.TargetItem != null && FilterMatchItem(filter2.Item1, slot))
							{
								int totalItemAmount = container.GetTotalItemAmount(slot, vector2i.x, vector2i.y);
								num4 = Mathf.Min(num4, totalItemAmount - filter2.Item1.MinAmountInInput);
							}
							else if (filter2.Item1.TargetCategory.HasValue)
							{
								num4 = Mathf.Min(num4, container.GetTotalCategoryAmount(filter2.Item1.TargetCategory.Value, range.x, range.y) - filter2.Item1.MinAmountInInput);
							}
							if (num4 == 0)
							{
								continue;
							}
						}
						if (slot.amount == 1 || (num4 <= 0 && slot.amount > 0))
						{
							num4 = 1;
						}
						if (flag5 && filter2.Item1.BufferAmount > 0)
						{
							num4 = Mathf.Min(num4, filter2.Item1.BufferTransferRemaining);
						}
						if (flag5 && filter2.Item1.MaxAmountInOutput > 0)
						{
							if (filter2.Item1.TargetItem != null && FilterMatchItem(filter2.Item1, slot))
							{
								num4 = Mathf.Min(num4, filter2.Item1.MaxAmountInOutput - container2.GetTotalItemAmount(slot, range.x, range.y));
							}
							else if (filter2.Item1.TargetCategory.HasValue)
							{
								num4 = Mathf.Min(num4, filter2.Item1.MaxAmountInOutput - container2.GetTotalCategoryAmount(filter2.Item1.TargetCategory.Value, range.x, range.y));
							}
							if ((float)num4 <= 0f)
							{
								flag = true;
							}
						}
						float num5 = Mathf.Min(slot.amount, num4);
						if (num5 > 0f && num5 < 1f)
						{
							num5 = 1f;
						}
						num4 = (int)num5;
						if (num4 <= 0 || !container2.QuickIndustrialPreCheck(slot, range, out var foundSlot))
						{
							continue;
						}
						splitOutput.Storage.OnStorageItemTransferBegin();
						bool flag6 = false;
						int num6 = slot.amount;
						Item slot2 = container2.GetSlot(foundSlot);
						if (ConVar.Server.industrialAllowQuickMove && foundSlot >= 0 && slot2 != null && !slot2.IsRemoved() && slot2.info.itemid == slot.info.itemid && slot2 != slot && slot.CanStack(slot2))
						{
							int num7 = Mathf.Min(num4, slot2.info.stackable - slot2.amount);
							slot2.amount += num7;
							slot.UseItem(num7);
							num6 = num7;
							slot2.MarkDirty();
							flag6 = true;
							if (slot.amount <= 0)
							{
								flag2 = true;
							}
						}
						Item item2 = null;
						if (!flag6 && slot.amount > num4)
						{
							item2 = slot.SplitItem(num4);
							num6 = item2.amount;
						}
						if (!flag6)
						{
							for (int j = range.x; j <= range.y; j++)
							{
								Item slot3 = container2.GetSlot(j);
								if ((slot3 == null || (slot3.info == slot.info && slot3.condition == slot.condition)) && (item2 ?? slot).MoveToContainer(container2, j, allowStack: true, ignoreStackLimit: false, null, allowSwap: false))
								{
									flag6 = true;
									break;
								}
							}
						}
						if (filter2.Item1.BufferTransferRemaining > 0)
						{
							var (value, _) = filter2;
							value.BufferTransferRemaining -= num6;
							filterItems[filter2.Item2] = value;
						}
						if (!flag6 && item2 != null)
						{
							slot.amount += item2.amount;
							slot.MarkDirty();
							item2.Remove();
							item2 = null;
						}
						if (flag6)
						{
							num3++;
							if (item2 != null)
							{
								AddTransfer(item2.info.itemid, num6, splitInput.Storage.IndustrialEntity, splitOutput.Storage.IndustrialEntity);
							}
							else
							{
								AddTransfer(slot.info.itemid, num6, splitInput.Storage.IndustrialEntity, splitOutput.Storage.IndustrialEntity);
							}
						}
						else if (!obj2.Contains(num))
						{
							obj2.Add(num);
						}
						splitOutput.Storage.OnStorageItemTransferEnd();
						if ((ConVar.Server.industrialTransferStrictTimeLimits && transferStopWatch.Elapsed.TotalMilliseconds >= (double)(ConVar.Server.industrialFrameBudgetMs * 3f) && !isFirstTransfer) || num3 >= ConVar.Server.maxItemStacksMovedPerTickIndustrial)
						{
							break;
						}
					}
					if (flag3)
					{
						break;
					}
				}
				if (flag3 || (ConVar.Server.industrialTransferStrictTimeLimits && !flag3 && transferStopWatch.Elapsed.TotalMilliseconds >= (double)(ConVar.Server.industrialFrameBudgetMs * 3f) && !isFirstTransfer))
				{
					break;
				}
				num++;
			}
			if (transfer.ItemTransfers.Count == 0 && hasItems && flag)
			{
				hasItems = false;
			}
			if (!lastFilterState.HasValue || hasItems != lastFilterState)
			{
				UpdateFilterPassthroughs();
			}
			Facepunch.Pool.FreeUnmanaged(ref obj2);
			if (flag2)
			{
				ItemManager.DoRemoves();
			}
			if (transfer.ItemTransfers.Count > 0)
			{
				List<Connection> obj3 = Facepunch.Pool.Get<List<Connection>>();
				BaseNetworkable.GetCloseConnections(base.transform.position, 30f, obj3);
				ClientRPC(RpcTarget.Players("ReceiveItemTransferDetails", obj3), transfer);
				Facepunch.Pool.FreeUnmanaged(ref obj3);
			}
			isFirstTransfer = false;
		}
		finally
		{
			if (transfer != null)
			{
				((IDisposable)transfer).Dispose();
			}
		}
		if (multiFrameTransferInProcess && multiFrameOutputIndex == splitOutputs.Count)
		{
			multiFrameTransferInProcess = false;
		}
		void AddTransfer(int itemId, int amount, BaseEntity fromEntity, BaseEntity toEntity)
		{
			if (transfer != null && transfer.ItemTransfers != null)
			{
				if (fromEntity != null && !transfer.inputEntities.Contains(fromEntity.net.ID))
				{
					transfer.inputEntities.Add(fromEntity.net.ID);
				}
				if (toEntity != null && !transfer.outputEntities.Contains(toEntity.net.ID))
				{
					transfer.outputEntities.Add(toEntity.net.ID);
				}
				for (int k = 0; k < transfer.ItemTransfers.Count; k++)
				{
					IndustrialConveyorTransfer.ItemTransfer value2 = transfer.ItemTransfers[k];
					if (value2.itemId == itemId)
					{
						value2.amount += amount;
						transfer.ItemTransfers[k] = value2;
						return;
					}
				}
				IndustrialConveyorTransfer.ItemTransfer itemTransfer = default(IndustrialConveyorTransfer.ItemTransfer);
				itemTransfer.itemId = itemId;
				itemTransfer.amount = amount;
				IndustrialConveyorTransfer.ItemTransfer item3 = itemTransfer;
				transfer.ItemTransfers.Add(item3);
			}
		}
		static bool FilterMatchItem(ItemFilter filter, Item item)
		{
			if (filter.TargetItem != null && (filter.TargetItem == item.info || (item.IsBlueprint() == filter.IsBlueprint && filter.TargetItem == item.blueprintTargetDef)))
			{
				return true;
			}
			return false;
		}
		void UpdateFilterPassthroughs()
		{
			lastFilterState = hasItems;
			SetFlag(Flags.Reserved9, hasItems, recursive: false, networkupdate: false);
			SetFlag(Flags.Reserved10, !hasItems);
			ensureOutputsUpdated = true;
			MarkDirty();
		}
	}

	protected override void OnIndustrialNetworkChanged()
	{
		base.OnIndustrialNetworkChanged();
		refreshInputOutputs = true;
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		refreshInputOutputs = true;
	}

	private bool CheckIfAnyInputPassesFilters(List<ContainerInputOutput> inputs)
	{
		if (filterItems.Count == 0)
		{
			foreach (ContainerInputOutput input in inputs)
			{
				if (GetItemToMove(input.Storage, out var _, input.SlotIndex) != null)
				{
					return true;
				}
			}
		}
		else
		{
			int num = 0;
			int num2 = 0;
			if (mode == ConveyorMode.And)
			{
				foreach (ItemFilter filterItem in filterItems)
				{
					if (filterItem.BufferTransferRemaining > 0)
					{
						num2++;
					}
				}
			}
			for (int i = 0; i < filterItems.Count; i++)
			{
				ItemFilter itemFilter = filterItems[i];
				int num3 = 0;
				int num4 = 0;
				foreach (ContainerInputOutput input2 in inputs)
				{
					Vector2i vector2i = input2.Storage.OutputSlotRange(input2.SlotIndex);
					for (int j = vector2i.x; j <= vector2i.y; j++)
					{
						Item slot = input2.Storage.Container.GetSlot(j);
						if (slot == null)
						{
							continue;
						}
						bool flag = FilterMatches(itemFilter, slot);
						if (mode == ConveyorMode.Not)
						{
							flag = !flag;
						}
						if (!flag)
						{
							continue;
						}
						if (itemFilter.BufferAmount > 0)
						{
							num3 += slot.amount;
							if (itemFilter.BufferTransferRemaining > 0)
							{
								num++;
								break;
							}
							if (num3 >= itemFilter.BufferAmount + itemFilter.MinAmountInInput)
							{
								if (mode != ConveyorMode.And)
								{
									itemFilter.BufferTransferRemaining = itemFilter.BufferAmount;
									filterItems[i] = itemFilter;
								}
								num++;
								break;
							}
						}
						if (itemFilter.MinAmountInInput > 0)
						{
							num4 += slot.amount;
							if (num4 > itemFilter.MinAmountInInput + itemFilter.BufferAmount)
							{
								num++;
								break;
							}
						}
						if (itemFilter.BufferAmount == 0 && itemFilter.MinAmountInInput == 0)
						{
							num++;
							break;
						}
					}
					if ((mode == ConveyorMode.Any || mode == ConveyorMode.Not) && num > 0)
					{
						return true;
					}
					if (itemFilter.MinAmountInInput > 0)
					{
						num4 = 0;
					}
				}
				if (itemFilter.BufferTransferRemaining > 0 && num3 == 0)
				{
					itemFilter.BufferTransferRemaining = 0;
					filterItems[i] = itemFilter;
				}
			}
			if (mode == ConveyorMode.And && num > 0 && (num == filterItems.Count || num == num2))
			{
				if (num2 == 0)
				{
					for (int k = 0; k < filterItems.Count; k++)
					{
						ItemFilter value = filterItems[k];
						value.BufferTransferRemaining = value.BufferAmount;
						filterItems[k] = value;
					}
				}
				return true;
			}
		}
		return false;
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		if (filterItems.Count == 0)
		{
			return;
		}
		info.msg.industrialConveyor = Facepunch.Pool.Get<ProtoBuf.IndustrialConveyor>();
		info.msg.industrialConveyor.filters = Facepunch.Pool.Get<List<ProtoBuf.IndustrialConveyor.ItemFilter>>();
		info.msg.industrialConveyor.conveyorMode = (int)mode;
		foreach (ItemFilter filterItem in filterItems)
		{
			ProtoBuf.IndustrialConveyor.ItemFilter itemFilter = Facepunch.Pool.Get<ProtoBuf.IndustrialConveyor.ItemFilter>();
			filterItem.CopyTo(itemFilter);
			info.msg.industrialConveyor.filters.Add(itemFilter);
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	[RPC_Server.CallsPerSecond(1uL)]
	private void RPC_ChangeFilters(RPCMessage msg)
	{
		if (msg.player == null || !msg.player.CanBuild())
		{
			return;
		}
		mode = (ConveyorMode)msg.read.Int32();
		filterItems.Clear();
		ProtoBuf.IndustrialConveyor.ItemFilterList itemFilterList = ProtoBuf.IndustrialConveyor.ItemFilterList.Deserialize(msg.read);
		if (itemFilterList.filters == null)
		{
			return;
		}
		int num = Mathf.Min(itemFilterList.filters.Count, 60);
		for (int i = 0; i < num; i++)
		{
			if (filterItems.Count >= 30)
			{
				break;
			}
			ItemFilter item = new ItemFilter(itemFilterList.filters[i]);
			if (item.TargetItem != null || item.TargetCategory.HasValue)
			{
				filterItems.Add(item);
			}
		}
		SendNetworkUpdate();
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	[RPC_Server.CallsPerSecond(2uL)]
	private void SvSwitch(RPCMessage msg)
	{
		SetSwitch(!IsOn());
	}

	public virtual void SetSwitch(bool wantsOn)
	{
		if (wantsOn == IsOn())
		{
			return;
		}
		SetFlag(Flags.On, wantsOn);
		SetFlag(Flags.Busy, b: true);
		SetFlag(Flags.Reserved10, b: false);
		SetFlag(Flags.Reserved9, b: false);
		if (!wantsOn)
		{
			lastFilterState = null;
		}
		ensureOutputsUpdated = true;
		Invoke(Unbusy, 0.5f);
		for (int i = 0; i < filterItems.Count; i++)
		{
			ItemFilter value = filterItems[i];
			if (value.BufferTransferRemaining > 0)
			{
				value.BufferTransferRemaining = 0;
				filterItems[i] = value;
			}
		}
		SendNetworkUpdateImmediate();
		MarkDirty();
	}

	public void Unbusy()
	{
		SetFlag(Flags.Busy, b: false);
	}

	public override void UpdateHasPower(int inputAmount, int inputSlot)
	{
	}

	public override void IOStateChanged(int inputAmount, int inputSlot)
	{
		base.IOStateChanged(inputAmount, inputSlot);
		if (inputSlot == 1)
		{
			bool flag = inputAmount >= ConsumptionAmount() && inputAmount > 0;
			if (IsPowered() && IsOn() && !flag)
			{
				wasOnWhenPowerLost = true;
			}
			SetFlag(Flags.Reserved8, flag);
			if (!flag)
			{
				SetFlag(Flags.Reserved9, b: false);
				SetFlag(Flags.Reserved10, b: false);
			}
			currentEnergy = inputAmount;
			ensureOutputsUpdated = true;
			if (inputAmount <= 0 && IsOn())
			{
				SetSwitch(wantsOn: false);
			}
			if (inputAmount > 0 && wasOnWhenPowerLost && !IsOn())
			{
				SetSwitch(wantsOn: true);
				wasOnWhenPowerLost = false;
			}
			MarkDirty();
		}
		if (inputSlot == 2 && !IsOn() && inputAmount > 0 && IsPowered())
		{
			SetSwitch(wantsOn: true);
		}
		if (inputSlot == 3 && IsOn() && inputAmount > 0)
		{
			SetSwitch(wantsOn: false);
		}
	}

	public override int GetPassthroughAmount(int outputSlot = 0)
	{
		int result = Mathf.Min(1, GetCurrentEnergy());
		switch (outputSlot)
		{
		case 2:
			if (!HasFlag(Flags.Reserved10))
			{
				return 0;
			}
			return result;
		case 3:
			if (!HasFlag(Flags.Reserved9))
			{
				return 0;
			}
			return result;
		case 1:
			return GetCurrentEnergy();
		default:
			return 0;
		}
	}

	public override bool ShouldDrainBattery(IOEntity battery)
	{
		return IsOn();
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	[RPC_Server.CallsPerSecond(1uL)]
	private void Server_RequestUpToDateFilters(RPCMessage msg)
	{
		if (!IsOn())
		{
			return;
		}
		using ProtoBuf.IndustrialConveyor.ItemFilterList itemFilterList = Facepunch.Pool.Get<ProtoBuf.IndustrialConveyor.ItemFilterList>();
		itemFilterList.filters = Facepunch.Pool.Get<List<ProtoBuf.IndustrialConveyor.ItemFilter>>();
		foreach (ItemFilter filterItem in filterItems)
		{
			ProtoBuf.IndustrialConveyor.ItemFilter itemFilter = Facepunch.Pool.Get<ProtoBuf.IndustrialConveyor.ItemFilter>();
			filterItem.CopyTo(itemFilter);
			itemFilterList.filters.Add(itemFilter);
		}
		ClientRPC(RpcTarget.Player("Client_ReceiveBufferInfo", msg.player), itemFilterList);
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		filterItems.Clear();
		if (info.msg.industrialConveyor?.filters == null)
		{
			return;
		}
		mode = (ConveyorMode)info.msg.industrialConveyor.conveyorMode;
		foreach (ProtoBuf.IndustrialConveyor.ItemFilter filter in info.msg.industrialConveyor.filters)
		{
			ItemFilter item = new ItemFilter(filter);
			if (item.TargetItem != null || item.TargetCategory.HasValue)
			{
				filterItems.Add(item);
			}
		}
	}
}
