using UnityEngine;

[CreateAssetMenu(menuName = "Rust/Environment Volume Properties")]
public class EnvironmentVolumeProperties : ScriptableObject
{
	[Header("Reflection Probe")]
	public int ReflectionQuality;

	public LayerMask ReflectionCullingFlags;

	[Horizontal(1, 0)]
	public EnvironmentMultiplier[] ReflectionMultipliers;

	public float DefaultReflectionMultiplier = 1f;

	[Header("Ambient Light")]
	[Horizontal(1, 0)]
	public EnvironmentMultiplier[] AmbientMultipliers;

	public float DefaultAmbientMultiplier = 1f;

	public float FindReflectionMultiplier(EnvironmentType type)
	{
		EnvironmentMultiplier[] reflectionMultipliers = ReflectionMultipliers;
		foreach (EnvironmentMultiplier environmentMultiplier in reflectionMultipliers)
		{
			if ((type & environmentMultiplier.Type) != 0)
			{
				return environmentMultiplier.Multiplier;
			}
		}
		return DefaultReflectionMultiplier;
	}

	public float FindAmbientMultiplier(EnvironmentType type)
	{
		EnvironmentMultiplier[] ambientMultipliers = AmbientMultipliers;
		foreach (EnvironmentMultiplier environmentMultiplier in ambientMultipliers)
		{
			if ((type & environmentMultiplier.Type) != 0)
			{
				return environmentMultiplier.Multiplier;
			}
		}
		return DefaultAmbientMultiplier;
	}
}
