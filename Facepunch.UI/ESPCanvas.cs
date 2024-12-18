using UnityEngine;

namespace Facepunch.UI;

public class ESPCanvas : SingletonComponent<ESPCanvas>
{
	[Tooltip("Amount of times per second we should update the visible panels")]
	public float RefreshRate = 5f;

	[Tooltip("This object will be duplicated in place")]
	public ESPPlayerInfo Source;

	private static int NameplateCount = 32;

	[ClientVar(ClientAdmin = true)]
	public static float OverrideMaxDisplayDistance = 0f;

	[ClientVar(ClientAdmin = true)]
	public static bool DisableOcclusionChecks = false;

	[ClientVar(ClientAdmin = true)]
	public static bool ShowHealth = false;

	[ClientVar(ClientAdmin = true)]
	public static bool ColourCodeTeams = false;

	[ClientVar(ClientAdmin = true)]
	public static bool UseRandomTeamColours = false;

	[ReplicatedVar(Help = "Multiply the maximum distance for displaying players nameplates")]
	public static float DisplayDistanceMultiplier = 1f;

	[ClientVar(ClientAdmin = true, Help = "Max amount of nameplates to show at once")]
	public static int MaxNameplates
	{
		get
		{
			return NameplateCount;
		}
		set
		{
			NameplateCount = Mathf.Clamp(value, 16, 150);
		}
	}
}
