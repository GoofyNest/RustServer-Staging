using UnityEngine;

public class RoadFlare : TimedExplosive, SeekerTarget.ISeekerTargetOwner
{
	public override void ServerInit()
	{
		base.ServerInit();
		SeekerTarget.SetSeekerTarget(this, SeekerTarget.SeekerStrength.HIGH);
	}

	public override void OnCollision(Collision collision, BaseEntity hitEntity)
	{
		SeekerTarget.SetSeekerTarget(this, SeekerTarget.SeekerStrength.OFF);
		base.OnCollision(collision, hitEntity);
	}

	public bool IsValidHomingTarget()
	{
		return true;
	}

	internal override void DoServerDestroy()
	{
		SeekerTarget.SetSeekerTarget(this, SeekerTarget.SeekerStrength.OFF);
		base.DoServerDestroy();
	}
}
