using System;
using System.Runtime.CompilerServices;
using Facepunch.Rust;
using UnityEngine;

public class PlayerBelt
{
	public struct EncryptedValue<TInner> where TInner : unmanaged
	{
		private TInner _value;

		private int _padding;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TInner Get()
		{
			return _value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(TInner value)
		{
			_value = value;
		}

		public override string ToString()
		{
			return Get().ToString();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator EncryptedValue<TInner>(TInner value)
		{
			EncryptedValue<TInner> result = default(EncryptedValue<TInner>);
			result.Set(value);
			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator TInner(EncryptedValue<TInner> encrypted)
		{
			return encrypted.Get();
		}
	}

	public static int ClientAutoSelectSlot = -1;

	public static uint ClientAutoSeletItemUID = 0u;

	public static EncryptedValue<int> SelectedSlot = -1;

	protected BasePlayer player;

	public static int MaxBeltSlots => 6;

	public PlayerBelt(BasePlayer player)
	{
		this.player = player;
	}

	public void DropActive(Vector3 position, Vector3 velocity)
	{
		Item activeItem = player.GetActiveItem();
		if (activeItem == null)
		{
			return;
		}
		using (TimeWarning.New("PlayerBelt.DropActive"))
		{
			DroppedItem droppedItem = activeItem.Drop(position, velocity) as DroppedItem;
			if (droppedItem != null)
			{
				droppedItem.DropReason = DroppedItem.DropReasonEnum.Death;
				droppedItem.DroppedBy = player.userID;
				droppedItem.DroppedTime = DateTime.UtcNow;
				Analytics.Azure.OnItemDropped(player, droppedItem, DroppedItem.DropReasonEnum.Death);
			}
			player.svActiveItemID = default(ItemId);
			player.SendNetworkUpdate();
		}
	}

	public Item GetItemInSlot(int slot)
	{
		if (player == null)
		{
			return null;
		}
		if (player.inventory == null)
		{
			return null;
		}
		if (player.inventory.containerBelt == null)
		{
			return null;
		}
		return player.inventory.containerBelt.GetSlot(slot);
	}

	public Handcuffs GetRestraintItem()
	{
		if (player == null)
		{
			return null;
		}
		if (player.inventory == null)
		{
			return null;
		}
		if (player.inventory.containerBelt == null)
		{
			return null;
		}
		foreach (Item item in player.inventory.containerBelt.itemList)
		{
			if (item != null)
			{
				Handcuffs handcuffs = item.GetHeldEntity() as Handcuffs;
				if (!(handcuffs == null) && handcuffs.Locked)
				{
					return handcuffs;
				}
			}
		}
		return null;
	}

	public bool CanHoldItem()
	{
		if (player == null)
		{
			return false;
		}
		if (player.IsWounded())
		{
			return false;
		}
		if (player.IsSleeping())
		{
			return false;
		}
		if (player.isMounted && !player.GetMounted().CanHoldItems())
		{
			return false;
		}
		return true;
	}
}
