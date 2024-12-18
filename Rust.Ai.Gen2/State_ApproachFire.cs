using System;

namespace Rust.Ai.Gen2;

[Serializable]
public class State_ApproachFire : State_CircleDynamic
{
	public override EFSMStateStatus OnStateEnter()
	{
		EFSMStateStatus result = base.OnStateEnter();
		base.Agent.deceleration.Value = 6f;
		distanceSpeedRange.x = 16f;
		return result;
	}

	public override void OnStateExit()
	{
		base.Agent.deceleration.Reset();
		base.OnStateExit();
	}
}
