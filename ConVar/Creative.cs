using UnityEngine;

namespace ConVar;

public class Creative : ConsoleSystem
{
	[ReplicatedVar(Help = "Apply creative mode to the entire server", Saved = true)]
	public static bool allUsers;

	[ReplicatedVar(Help = "Bypass the 30s repair cooldown when repairing objects", Saved = true)]
	public static bool freeRepair;

	[ReplicatedVar(Help = "Build blocks for free", Saved = true)]
	public static bool freeBuild;

	[ReplicatedVar(Help = "Bypasses all placement checks", Saved = true)]
	public static bool freePlacement;

	[ReplicatedVar(Help = "Bypasses limits on IO length and points", Saved = true)]
	public static bool unlimitedIo;

	[ServerVar]
	public static void toggleCreativeModeUser(Arg arg)
	{
		BasePlayer player = arg.GetPlayer(0);
		bool @bool = arg.GetBool(1);
		if (player == null)
		{
			arg.ReplyWith("Invalid player provided " + arg.GetString(0));
			return;
		}
		player.SetPlayerFlag(BasePlayer.PlayerFlags.CreativeMode, @bool);
		arg.ReplyWith($"{player.displayName} creative mode: {@bool}");
	}
}
