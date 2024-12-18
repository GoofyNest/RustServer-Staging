using Facepunch;

namespace Rust.Ai.Gen2;

public class Trans_Triggerable_HitInfo : Trans_Triggerable
{
	private HitInfo HitInfo;

	public virtual void Trigger(HitInfo hitInfo)
	{
		if (HitInfo != null)
		{
			Pool.Free(ref HitInfo);
		}
		HitInfo = Pool.Get<HitInfo>();
		HitInfo.CopyFrom(hitInfo);
		Trigger();
	}

	public override void OnTransitionTaken(FSMStateBase from, FSMStateBase to)
	{
		if (base.Triggered && to is IParametrized<HitInfo> parametrized)
		{
			parametrized.SetParameter(HitInfo);
		}
		if (HitInfo != null)
		{
			Pool.Free(ref HitInfo);
		}
	}
}
