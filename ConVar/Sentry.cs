namespace ConVar;

[Factory("sentry")]
public class Sentry : ConsoleSystem
{
	[ServerVar(Help = "target everyone regardless of authorization")]
	public static bool targetall = false;

	[ServerVar(Help = "how long until something is considered hostile after it attacked")]
	public static float hostileduration = 120f;

	[ReplicatedVar(Help = "radius to check for other turrets")]
	public static float interferenceradius = 40f;

	[ReplicatedVar(Help = "max interference from other turrets")]
	public static int maxinterference = 12;

	[ServerVar(Help = "Prevents auto turrets getting added more than once to the IO queue")]
	public static bool debugPreventDuplicates = true;
}
