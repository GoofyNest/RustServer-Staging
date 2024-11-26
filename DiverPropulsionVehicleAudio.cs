using UnityEngine;

public class DiverPropulsionVehicleAudio : MonoBehaviour
{
	[Header("Engine")]
	[SerializeField]
	private SoundDefinition engineStartSound;

	[SerializeField]
	private SoundDefinition engineStopSound;

	[SerializeField]
	private SoundDefinition engineStartFailSound;

	[SerializeField]
	private SoundDefinition engineLoopSound;

	[SerializeField]
	private AnimationCurve engineLoopPitchCurve;

	[SerializeField]
	private SoundDefinition engineActiveLoopDef;

	[Header("Propeller")]
	[SerializeField]
	private SoundDefinition propellerLoopSoundDef;

	[SerializeField]
	private AnimationCurve propellerPitchCurve;

	[SerializeField]
	private AnimationCurve propellerGainCurve;

	[Header("Water")]
	[SerializeField]
	private SoundDefinition waterMovementLoopDef;

	[SerializeField]
	private AnimationCurve waterMovementGainCurve;

	[SerializeField]
	private SoundDefinition waterSurfaceLoopDef;

	[SerializeField]
	private float surfaceWaterMovementStartDepth = 0.2f;

	[SerializeField]
	private float surfaceWaterMovementEndDepth = 2f;

	[SerializeField]
	private float waterMovementYSpeedScale = 0.2f;

	[SerializeField]
	private SoundDefinition waterEmergeSoundDef;

	[SerializeField]
	private SoundDefinition waterSubmergeSoundDef;
}
