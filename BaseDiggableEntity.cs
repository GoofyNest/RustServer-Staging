using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Network;
using UnityEngine;

public class BaseDiggableEntity : BaseCombatEntity
{
	public bool RequiresShovel;

	public Vector3 DropOffset;

	public Vector3 DropVelocity;

	public int RequiredDigCount = 3;

	public bool DestroyOnDug = true;

	[Header("Loot")]
	public List<DiggableEntityLoot> LootLists = new List<DiggableEntityLoot>();

	protected int digsRemaining;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("BaseDiggableEntity.OnRpcMessage"))
		{
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void ServerInit()
	{
		base.ServerInit();
		digsRemaining = RequiredDigCount;
		_maxHealth = RequiredDigCount;
	}

	public override void Hurt(HitInfo info)
	{
		if (!(info.InitiatorPlayer == null) && (!RequiresShovel || (!(info.Weapon == null) && info.Weapon is Shovel)) && info.damageTypes.IsMeleeType())
		{
			Dig(info.InitiatorPlayer);
		}
	}

	public virtual void Dig(BasePlayer player)
	{
		if (digsRemaining == RequiredDigCount)
		{
			OnFirstDig(player);
		}
		ClientRPC(RpcTarget.NetworkGroup("RPC_OnDig"), RequiredDigCount - digsRemaining, RequiredDigCount);
		digsRemaining--;
		base.health = digsRemaining;
		SendNetworkUpdate();
		OnSingleDig(player);
		if (digsRemaining <= 0)
		{
			OnFullyDug(player);
			if (DestroyOnDug)
			{
				Kill();
			}
		}
	}

	public virtual void OnFirstDig(BasePlayer player)
	{
	}

	public virtual void OnSingleDig(BasePlayer player)
	{
	}

	public virtual void OnFullyDug(BasePlayer player)
	{
		SpawnItem();
	}

	public BaseEntity SpawnItem()
	{
		DiggableEntityLoot.ItemEntry? item = GetItem(base.transform.position);
		if (!item.HasValue || !item.HasValue)
		{
			return null;
		}
		Item item2 = ItemManager.Create(item.Value.Item, Random.Range(item.Value.Min, item.Value.Max + 1), 0uL);
		DroppedItem droppedItem = null;
		if (item2 != null)
		{
			if (item2.hasCondition)
			{
				item2.condition = Random.Range(item2.info.condition.foundCondition.fractionMin, item2.info.condition.foundCondition.fractionMax) * item2.info.condition.max;
			}
			droppedItem = item2.Drop(base.transform.position + DropOffset, DropVelocity) as DroppedItem;
			if (droppedItem != null)
			{
				droppedItem.NeverCombine = true;
				droppedItem.SetAngularVelocity(new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * 720f);
			}
		}
		return droppedItem;
	}

	private DiggableEntityLoot.ItemEntry? GetItem(Vector3 digWorldPos)
	{
		if (LootLists == null)
		{
			return null;
		}
		List<DiggableEntityLoot.ItemEntry> obj = Pool.Get<List<DiggableEntityLoot.ItemEntry>>();
		obj.Clear();
		foreach (DiggableEntityLoot lootList in LootLists)
		{
			if (lootList.VerifyLootListForWorldPosition(digWorldPos))
			{
				obj.AddRange(lootList.Items);
			}
		}
		DiggableEntityLoot.ItemEntry? result = null;
		if (obj.Count != 0)
		{
			int num = obj.Sum((DiggableEntityLoot.ItemEntry x) => x.Weight);
			int num2 = Random.Range(0, num);
			for (int i = 0; i < obj.Count; i++)
			{
				result = obj[i];
				if (result.HasValue)
				{
					num -= result.Value.Weight;
					if (num2 >= num)
					{
						break;
					}
				}
			}
		}
		Pool.FreeUnmanaged(ref obj);
		return result;
	}
}
