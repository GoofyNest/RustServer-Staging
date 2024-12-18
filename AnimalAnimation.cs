using UnityEngine;

public class AnimalAnimation : MonoBehaviour, IClientComponent
{
	public BaseEntity Entity;

	public BaseNpc Target;

	public Animator Animator;

	public MaterialEffect FootstepEffects;

	public Transform[] Feet;

	public SoundDefinition saddleMovementSoundDef;

	public SoundDefinition saddleMovementSoundDefWood;

	public SoundDefinition saddleMovementSoundDefRoadsign;

	public AnimationCurve saddleMovementGainCurve;

	[Tooltip("Ensure there is a float param called idleOffset if this is enabled")]
	public bool hasIdleOffset;

	[Tooltip("Check animation clip weights when playing sounds so we don't play sounds for clips that are blended out")]
	public bool enforceClipWeights;

	[ReadOnly]
	public string BaseFolder;

	public const BaseEntity.Flags Flag_WoodArmor = BaseEntity.Flags.Reserved5;

	public const BaseEntity.Flags Flag_RoadsignArmor = BaseEntity.Flags.Reserved6;
}
