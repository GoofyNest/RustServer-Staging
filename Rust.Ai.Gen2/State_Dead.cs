using System;
using Facepunch;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public class State_Dead : FSMStateBase
{
	[SerializeField]
	private string deathStatName;

	[SerializeField]
	private GameObjectRef CorpsePrefab;

	private HitInfo HitInfo;

	public void SetParameter(HitInfo parameter)
	{
		if (HitInfo != null)
		{
			Pool.Free(ref HitInfo);
		}
		if (parameter == null)
		{
			Debug.LogWarning("No parameter set for hurt state");
		}
		HitInfo = Pool.Get<HitInfo>();
		HitInfo.CopyFrom(parameter);
	}

	public override EFSMStateStatus OnStateEnter()
	{
		if (HitInfo != null && HitInfo.InitiatorPlayer != null)
		{
			BasePlayer initiatorPlayer = HitInfo.InitiatorPlayer;
			initiatorPlayer.GiveAchievement("KILL_ANIMAL");
			if (!string.IsNullOrEmpty(deathStatName))
			{
				initiatorPlayer.stats.Add(deathStatName, 1, (Stats)5);
				initiatorPlayer.stats.Save();
			}
			if (Owner is BaseCombatEntity killed)
			{
				initiatorPlayer.LifeStoryKill(killed);
			}
		}
		BaseCorpse baseCorpse = Owner.DropCorpse(CorpsePrefab.resourcePath);
		if ((bool)baseCorpse)
		{
			baseCorpse.Spawn();
			baseCorpse.TakeChildren(Owner);
		}
		Owner.Invoke(Owner.KillMessage, 0.5f);
		return base.OnStateEnter();
	}
}
