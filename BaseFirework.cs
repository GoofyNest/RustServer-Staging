using System.Collections.Generic;
using Rust;
using UnityEngine;

public class BaseFirework : BaseCombatEntity, IIgniteable
{
	public float fuseLength = 3f;

	public float activityLength = 10f;

	public const Flags Flag_Spent = Flags.Reserved8;

	public float corpseDuration = 15f;

	public bool limitActiveCount;

	[ServerVar]
	public static int maxActiveFireworks = 25;

	public static HashSet<BaseFirework> _activeFireworks = new HashSet<BaseFirework>();

	public bool IsLit()
	{
		return HasFlag(Flags.OnFire);
	}

	public bool IsExhausted()
	{
		return HasFlag(Flags.Reserved8);
	}

	public static int NumActiveFireworks()
	{
		return _activeFireworks.Count;
	}

	public virtual void TryLightFuse()
	{
		if (!IsExhausted() && !IsLit())
		{
			SetFlag(Flags.OnFire, b: true);
			EnableGlobalBroadcast(wants: true);
			Invoke(Begin, fuseLength);
			pickup.enabled = false;
			EnableSaving(wants: false);
		}
	}

	public virtual void Begin()
	{
		SetFlag(Flags.OnFire, b: false);
		SetFlag(Flags.On, b: true, recursive: false, networkupdate: false);
		SendNetworkUpdate_Flags();
		Invoke(OnExhausted, activityLength);
	}

	public virtual void OnExhausted()
	{
		SetFlag(Flags.Reserved8, b: true, recursive: false, networkupdate: false);
		SetFlag(Flags.OnFire, b: false, recursive: false, networkupdate: false);
		SetFlag(Flags.On, b: false, recursive: false, networkupdate: false);
		EnableGlobalBroadcast(wants: false);
		SendNetworkUpdate_Flags();
		Invoke(Cleanup, corpseDuration);
		_activeFireworks.Remove(this);
	}

	public void Cleanup()
	{
		Kill();
	}

	internal override void DoServerDestroy()
	{
		_activeFireworks.Remove(this);
		base.DoServerDestroy();
	}

	public override void OnAttacked(HitInfo info)
	{
		base.OnAttacked(info);
		if (base.isServer && info.damageTypes.Has(DamageType.Heat))
		{
			StaggeredTryLightFuse();
		}
	}

	public void Ignite(Vector3 fromPos)
	{
		StaggeredTryLightFuse();
	}

	public void StaggeredTryLightFuse()
	{
		if (IsExhausted() || (IsLit() && _activeFireworks.Contains(this)))
		{
			return;
		}
		if (limitActiveCount)
		{
			if (NumActiveFireworks() >= maxActiveFireworks)
			{
				SetFlag(Flags.OnFire, b: true);
				Invoke(StaggeredTryLightFuse, 0.35f);
				return;
			}
			_activeFireworks.Add(this);
			SetFlag(Flags.OnFire, b: false, recursive: false, networkupdate: false);
		}
		Invoke(TryLightFuse, Random.Range(0.1f, 0.3f));
	}

	public bool CanIgnite()
	{
		if (!IsExhausted())
		{
			return !IsLit();
		}
		return false;
	}

	public override bool CanPickup(BasePlayer player)
	{
		if (!IsExhausted() && base.CanPickup(player))
		{
			return !IsLit();
		}
		return false;
	}
}
