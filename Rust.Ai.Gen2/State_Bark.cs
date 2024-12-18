using System;
using Facepunch;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public class State_Bark : State_PlayAnimation
{
	public const string WolfNearbyAlreadyBarked = "WolfNearbyAlreadyBarked";

	public override EFSMStateStatus OnStateEnter()
	{
		if (!base.Senses.FindTarget(out var targetEntity))
		{
			return EFSMStateStatus.Failure;
		}
		base.Blackboard.Add("WolfNearbyAlreadyBarked");
		using (PooledList<BaseEntity> pooledList = Pool.Get<PooledList<BaseEntity>>())
		{
			base.Senses.GetInitialAllies(pooledList);
			foreach (BaseEntity item in pooledList)
			{
				item.GetComponent<BlackboardComponent>().Add("WolfNearbyAlreadyBarked");
				Wolf2FSM otherWolf = item.GetComponent<Wolf2FSM>();
				Owner.Invoke(delegate
				{
					otherWolf.Bark(targetEntity);
				}, Mathf.Max(0f, Animation.length + UnityEngine.Random.Range(-0.5f, 0.5f)));
			}
		}
		return base.OnStateEnter();
	}
}
