public class XmasDungeon : HalloweenDungeon
{
	[ServerVar(Help = "Population active on the server", ShowInAdminUI = true)]
	public static float xmaspopulation = 0f;

	[ServerVar(Help = "How long each active dungeon should last before dying", ShowInAdminUI = true)]
	public static float xmaslifetime = 600f;

	public override float GetLifetime()
	{
		return HalloweenDungeon.lifetime;
	}
}
