namespace Rust.Ai.Gen2;

[SoftRequireComponent(typeof(Wolf2FSM))]
public class Wolf2 : BaseNPC2
{
	private Wolf2FSM FSM;

	public override string Categorize()
	{
		return "Wolf";
	}

	public override void ServerInit()
	{
		base.ServerInit();
		FSM = GetComponent<Wolf2FSM>();
	}

	public override void Hurt(HitInfo hitInfo)
	{
		base.Hurt(hitInfo);
		if (!(FSM == null))
		{
			FSM.Hurt(hitInfo);
		}
	}

	public override void OnKilled(HitInfo hitInfo)
	{
		if (FSM == null)
		{
			base.OnKilled(hitInfo);
		}
		else
		{
			FSM.Die(hitInfo);
		}
	}
}
