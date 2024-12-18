using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
internal class Trans_TargetIsNearFire : FSMTransitionBase
{
	public bool onlySeeFireWhenClose;

	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_TargetIsNearFire"))
		{
			return Test(Owner, base.Senses, onlySeeFireWhenClose);
		}
	}

	public static bool Test(BaseEntity owner, SenseComponent senses, bool onlySeeFireWhenClose = false)
	{
		using (TimeWarning.New("Test"))
		{
			if (!senses.FindTarget(out var target))
			{
				return false;
			}
			if (target.ToNonNpcPlayer(out var player) && SingletonComponent<NpcNoiseManager>.Instance.HasPlayerSpokenNear(owner, player))
			{
				return true;
			}
			if (!senses.FindFire(out var fire))
			{
				return false;
			}
			bool flag = Vector3.Distance(target.transform.position, fire.transform.position) < 16f;
			bool flag2 = Vector3.Distance(owner.transform.position, target.transform.position) < 18f;
			if (onlySeeFireWhenClose)
			{
				return flag && flag2;
			}
			return flag;
		}
	}
}
