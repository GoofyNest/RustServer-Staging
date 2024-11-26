using UnityEngine;

public class BikeVehicleAudio : GroundVehicleAudio
{
	[Header("Engine")]
	[SerializeField]
	private EngineAudioSet engineAudioSet;

	[Header("Suspension")]
	[SerializeField]
	private SoundDefinition suspensionDef;

	[SerializeField]
	private float suspensionMinExtensionDelta = 0.4f;

	[SerializeField]
	private float suspensionMinTimeBetweenSounds = 0.25f;

	[Header("Tires")]
	[SerializeField]
	private SoundDefinition tireDirtSoundDef;

	[SerializeField]
	private SoundDefinition tireGrassSoundDef;

	[SerializeField]
	private SoundDefinition tireSnowSoundDef;

	[SerializeField]
	private SoundDefinition tireWaterSoundDef;

	[SerializeField]
	private AnimationCurve tireGainCurve;

	[Header("Skid")]
	[SerializeField]
	private SoundDefinition skidSoundLoop;

	[SerializeField]
	private SoundDefinition skidSoundDirtLoop;

	[SerializeField]
	private SoundDefinition skidSoundSnowLoop;

	[SerializeField]
	private float skidMinSlip = 10f;

	[SerializeField]
	private float skidMaxSlip = 25f;
}
