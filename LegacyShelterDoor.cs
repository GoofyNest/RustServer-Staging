using Rust;

public class LegacyShelterDoor : Door
{
	public GameObjectRef includedLockPrefab;

	private LegacyShelter shelter;

	public void SetupDoor(LegacyShelter shelter)
	{
		this.shelter = shelter;
	}

	public override void DecayTick()
	{
	}

	protected override void OnChildAdded(BaseEntity child)
	{
		base.OnChildAdded(child);
		if (Application.isLoadingSave && child.prefabID == includedLockPrefab.GetEntity().prefabID && child.IsValid())
		{
			BaseLock baseLock = (BaseLock)child;
			if (baseLock != null)
			{
				baseLock.CanRemove = false;
			}
		}
	}

	protected override void OnPlayerOpenedDoor(BasePlayer p)
	{
		base.OnPlayerOpenedDoor(p);
		if (shelter != null)
		{
			shelter.HasInteracted();
		}
	}

	public override void OnRepair()
	{
		base.OnRepair();
		UpdateShelterHp();
	}

	public override void OnRepairFinished()
	{
		base.OnRepairFinished();
		UpdateShelterHp();
	}

	public override void Hurt(HitInfo info)
	{
		if (HasParent() && shelter != null)
		{
			shelter.ProtectedHurt(info);
		}
	}

	public override void OnKilled(HitInfo info)
	{
		base.OnKilled(info);
		if (shelter != null && !shelter.IsDead())
		{
			shelter.Die();
		}
	}

	public void ProtectedHurt(HitInfo info)
	{
		info.HitEntity = this;
		base.Hurt(info);
	}

	private void UpdateShelterHp()
	{
		if (HasParent() && shelter != null)
		{
			shelter.SetHealth(base.health);
		}
	}
}
