using System;
using Facepunch;

namespace Rust.Ai.Gen2;

[Serializable]
public class State_Howl : State_PlayAnimation
{
	public const string WolfNearbyAlreadyHowled = "WolfNearbyAlreadyHowled";

	public override EFSMStateStatus OnStateEnter()
	{
		if (!base.Senses.FindTarget(out var targetEntity))
		{
			return EFSMStateStatus.Failure;
		}
		if (!base.Agent.CanReach(targetEntity.transform.position, triggerPathFailed: true))
		{
			return EFSMStateStatus.Failure;
		}
		base.Blackboard.Add("WolfNearbyAlreadyHowled");
		using (PooledList<BaseEntity> pooledList = Pool.Get<PooledList<BaseEntity>>())
		{
			base.Senses.GetInitialAllies(pooledList);
			foreach (BaseEntity item in pooledList)
			{
				item.GetComponent<BlackboardComponent>().Add("WolfNearbyAlreadyHowled");
				Wolf2FSM otherWolf = item.GetComponent<Wolf2FSM>();
				Owner.Invoke(delegate
				{
					otherWolf.Howl(targetEntity);
				}, 1f);
			}
		}
		return base.OnStateEnter();
	}
}
