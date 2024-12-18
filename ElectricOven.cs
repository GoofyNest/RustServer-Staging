using Facepunch;
using ProtoBuf;
using Rust;
using UnityEngine;

public class ElectricOven : BaseOven
{
	public GameObjectRef IoEntity;

	public Transform IoEntityAnchor;

	private EntityRef<IOEntity> spawnedIo;

	protected override bool CanRunWithNoFuel
	{
		get
		{
			if (spawnedIo.IsValid(serverside: true))
			{
				return spawnedIo.Get(serverside: true).IsPowered();
			}
			return false;
		}
	}

	public override void ServerInit()
	{
		base.ServerInit();
		if (!Rust.Application.isLoadingSave)
		{
			SpawnIOEnt();
		}
	}

	private void SpawnIOEnt()
	{
		if (IoEntity.isValid && IoEntityAnchor != null)
		{
			IOEntity iOEntity = GameManager.server.CreateEntity(IoEntity.resourcePath, IoEntityAnchor.position, IoEntityAnchor.rotation) as IOEntity;
			iOEntity.SetParent(this, worldPositionStays: true);
			spawnedIo.Set(iOEntity);
			iOEntity.Spawn();
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		if (info.msg.simpleUID == null)
		{
			info.msg.simpleUID = Pool.Get<SimpleUID>();
		}
		info.msg.simpleUID.uid = spawnedIo.uid;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.simpleUID != null)
		{
			spawnedIo.uid = info.msg.simpleUID.uid;
		}
	}

	public override void OvenFull()
	{
		Invoke(PauseCooking, 0f);
	}

	private void PauseCooking()
	{
		UpdateAttachmentTemperature();
		if (base.inventory != null)
		{
			base.inventory.temperature = 15f;
			foreach (Item item in base.inventory.itemList)
			{
				if (item.HasFlag(Item.Flag.OnFire))
				{
					item.SetFlag(Item.Flag.OnFire, b: false);
					item.MarkDirty();
				}
				if (item.HasFlag(Item.Flag.Cooking))
				{
					item.SetFlag(Item.Flag.Cooking, b: false);
					item.MarkDirty();
				}
			}
		}
		SetFlag(Flags.Reserved8, b: true);
	}

	public override void OnItemAddedOrRemoved(Item item, bool bAdded)
	{
		if (item != null && !bAdded && HasFlag(Flags.Reserved8))
		{
			SetFlag(Flags.Reserved8, b: false);
		}
	}

	protected override bool CanPickupOven()
	{
		return children.Count == 1;
	}
}
