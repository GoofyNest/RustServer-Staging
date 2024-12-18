using System;
using System.Collections;
using System.Collections.Generic;
using FIMSpace.FTools;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FIMSpace.FLook;

[AddComponentMenu("FImpossible Creations/Look Animator 2")]
[DefaultExecutionOrder(-10)]
public class FLookAnimator : MonoBehaviour, IDropHandler, IEventSystemHandler, IFHierarchyIcon, IClientComponent
{
	[Serializable]
	public class CompensationBone
	{
		public Transform Transform;

		private Vector3 compensatedPosition;

		private Quaternion compensatedRotation;

		private Quaternion lastFinalLocalRotation;

		private Quaternion lastKeyframeLocalRotation;

		private Vector3 lastFinalLocalPosition;

		private Vector3 lastKeyframeLocalPosition;

		public CompensationBone(Transform t)
		{
			Transform = t;
			if ((bool)t)
			{
				lastKeyframeLocalPosition = t.localPosition;
				lastKeyframeLocalRotation = t.localRotation;
			}
		}

		public void RefreshCompensationFrame()
		{
			compensatedPosition = Transform.position;
			compensatedRotation = Transform.rotation;
		}

		public void CheckForZeroKeyframes()
		{
			if (lastFinalLocalRotation.QIsSame(Transform.localRotation))
			{
				Transform.localRotation = lastKeyframeLocalRotation;
				compensatedRotation = Transform.rotation;
			}
			else
			{
				lastKeyframeLocalRotation = Transform.localRotation;
			}
			if (lastFinalLocalPosition.VIsSame(Transform.localPosition))
			{
				Transform.localPosition = lastKeyframeLocalPosition;
				compensatedPosition = Transform.position;
			}
			else
			{
				lastKeyframeLocalPosition = Transform.localPosition;
			}
		}

		public void SetRotationCompensation(float weight)
		{
			if (weight > 0f)
			{
				if (weight >= 1f)
				{
					Transform.rotation = compensatedRotation;
				}
				else
				{
					Transform.rotation = Quaternion.LerpUnclamped(Transform.rotation, compensatedRotation, weight);
				}
				lastFinalLocalRotation = Transform.localRotation;
			}
		}

		public void SetPositionCompensation(float weight)
		{
			if (weight > 0f)
			{
				if (weight >= 1f)
				{
					Transform.position = compensatedPosition;
				}
				else
				{
					Transform.position = Vector3.LerpUnclamped(Transform.position, compensatedPosition, weight);
				}
				lastFinalLocalPosition = Transform.localPosition;
			}
		}
	}

	[Serializable]
	public class LookBone
	{
		public Transform Transform;

		public Quaternion animatedStaticRotation;

		public Quaternion targetStaticRotation;

		public Quaternion localStaticRotation;

		public Quaternion animatedTargetRotation;

		public Quaternion targetRotation;

		public Vector3 correctionOffset;

		public Quaternion finalRotation;

		public Quaternion lastKeyframeRotation;

		public Quaternion lastFinalLocalRotation;

		public Vector3 forward;

		public Vector3 right;

		public Vector3 up;

		public Vector3 initLocalPos = Vector3.zero;

		public Quaternion initLocalRot = Quaternion.identity;

		public Vector3 targetDelayPosition;

		public Vector3 animatedDelayPosition;

		public float lookWeight = 1f;

		public float lookWeightB = 1f;

		public float motionWeight = 1f;

		public Quaternion correctionOffsetQ => Quaternion.Euler(correctionOffset);

		public LookBone(Transform t)
		{
			Transform = t;
			correctionOffset = Vector3.zero;
			if (t != null)
			{
				initLocalPos = t.localPosition;
				initLocalRot = t.localRotation;
			}
		}

		public void RefreshBoneDirections(Transform baseTransform)
		{
			if (!(Transform == null))
			{
				forward = Quaternion.FromToRotation(Transform.InverseTransformDirection(baseTransform.forward), Vector3.forward) * Vector3.forward;
				up = Quaternion.FromToRotation(Transform.InverseTransformDirection(baseTransform.up), Vector3.up) * Vector3.up;
				right = Quaternion.FromToRotation(Transform.InverseTransformDirection(baseTransform.right), Vector3.right) * Vector3.right;
			}
		}

		public void RefreshStaticRotation(bool hard = true)
		{
			targetStaticRotation = Transform.rotation;
			if (initLocalPos == Vector3.zero)
			{
				initLocalPos = Transform.localPosition;
			}
			if (hard)
			{
				animatedStaticRotation = targetStaticRotation;
			}
			localStaticRotation = Transform.localRotation;
		}

		internal void CalculateMotion(Quaternion targetLook, float overallWeightMultiplier, float delta, float mainWeight)
		{
			targetRotation = GetTargetRot(targetLook, motionWeight * overallWeightMultiplier);
			if (delta < 1f)
			{
				animatedTargetRotation = Quaternion.LerpUnclamped(animatedTargetRotation, targetRotation, delta);
			}
			else
			{
				animatedTargetRotation = targetRotation;
			}
			finalRotation = Quaternion.LerpUnclamped(Transform.rotation, animatedTargetRotation * Transform.rotation, mainWeight);
		}

		internal Quaternion GetTargetRot(Quaternion targetLook, float weight)
		{
			return Quaternion.LerpUnclamped(Quaternion.identity, targetLook, weight);
		}
	}

	public enum EEditorLookCategory
	{
		Setup,
		Tweak,
		Limit,
		Features,
		Corrections
	}

	public enum EFAxisFixOrder
	{
		Parental,
		FromBased,
		FullManual,
		ZYX
	}

	public enum EFHeadLookState
	{
		Null,
		Following,
		OutOfMaxRotation,
		ClampedAngle,
		OutOfMaxDistance
	}

	public enum EFFollowMode
	{
		FollowObject,
		LocalOffset,
		WorldOffset,
		ToFollowSpaceOffset,
		FollowJustPosition
	}

	public enum EFDeltaType
	{
		DeltaTime,
		SmoothDeltaTime,
		UnscaledDeltaTime,
		FixedDeltaTime
	}

	public enum EFAnimationStyle
	{
		SmoothDamp,
		FastLerp,
		Linear
	}

	private GameObject generatedMomentTarget;

	private bool wasMomentLookTransform;

	[Tooltip("Enabling laggy movement for head and delaying position")]
	public bool BirdMode;

	private bool birdModeInitialized;

	[FPD_Suffix(0f, 1f, FPD_SuffixAttribute.SuffixMode.From0to100, "%", true, 0)]
	[Tooltip("Bird mode laggy movement for neck amount, lowering this value will cause crossfade motion of laggy movement and basic follow rotation")]
	public float LagRotation = 0.85f;

	[Tooltip("How often should be acquired new target position for laggy movement, time to trigger it will be slightly randomized")]
	[FPD_Suffix(0.1f, 1f, FPD_SuffixAttribute.SuffixMode.FromMinToMax, "sec", true, 0)]
	public float LagEvery = 0.285f;

	[FPD_Percentage(0f, 1f, false, true, "%", false)]
	[Tooltip("Bird mode keeping previous position until distance is reached")]
	public float DelayPosition;

	[Tooltip("How far distance to go back should have head to move (remind movement of pigeons to yourself)")]
	public float DelayMaxDistance = 0.25111f;

	[Tooltip("How quick head and neck should go back to right position after reaching distance")]
	[Range(0f, 1f)]
	public float DelayGoSpeed = 0.6f;

	public Vector3 BirdTargetPosition = Vector3.forward;

	private Vector3 birdTargetPositionMemory = Vector3.forward;

	private float lagTimer;

	private float preWeightFaloff = -1f;

	private float[] baseWeights;

	private float[] targetWeights;

	public bool UseEyes;

	[Tooltip("Target on which eyes will look, set to null if target should be the same as for head target")]
	public Transform EyesTarget;

	[Space(4f)]
	[Tooltip("Eyes transforms / bones (origin should be in center of the sphere")]
	public Transform LeftEye;

	public bool InvertLeftEye;

	[Tooltip("Eyes transforms / bones (origin should be in center of the sphere")]
	public Transform RightEye;

	public bool InvertRightEye;

	[Tooltip("Look clamping reference rotation transform, mostly parent of eye objects. If nothing is assigned then algorithm will use 'Lead Bone' as reference.")]
	public Transform HeadReference;

	public Vector3 EyesOffsetRotation;

	public Vector3 LeftEyeOffsetRotation = Vector3.zero;

	public Vector3 RightEyeOffsetRotation = Vector3.zero;

	[Tooltip("How fast eyes should follow target")]
	[Range(0f, 1f)]
	public float EyesSpeed = 0.5f;

	[FPD_Percentage(0f, 1f, false, true, "%", false)]
	public float EyesBlend = 1f;

	[Tooltip("In what angle eyes should go back to deafult position")]
	[Range(0f, 180f)]
	public Vector2 EyesXRange = new Vector2(-60f, 60f);

	public Vector2 EyesYRange = new Vector2(-50f, 50f);

	[Tooltip("If your eyes don't have baked keyframes in animation this value should be enabled, otherwise eyes would go crazy")]
	public bool EyesNoKeyframes = true;

	public bool CustomEyesLogics;

	private float EyesOutOfRangeBlend = 1f;

	private Transform[] eyes;

	private Vector3[] eyeForwards;

	private Quaternion[] eyesInitLocalRotations;

	private Quaternion[] eyesLerpRotations;

	private float _eyesBlend;

	private Vector3 headForward;

	[Range(-1f, 1f)]
	[Tooltip("When switching targets character will make small nod to make it look more natural, set higher value for toony effect")]
	public float NoddingTransitions;

	public Vector3 NodAxis = Vector3.right;

	[Range(-1f, 1f)]
	[Tooltip("Set zero to use only leading bone, set -1 to 1 to spread this motion over backbones")]
	public float BackBonesNod = 0.15f;

	private float nodProgress;

	private float nodValue;

	private float nodPower;

	private float nodDuration = 1f;

	private float smoothingTimer;

	private float smoothingPower = 1f;

	private float smoothingTime = 1f;

	private float smoothingEffect = 1f;

	public int ParentalOffsetsV = 2;

	private Vector3 lookFreezeFocusPoint;

	private Vector3 smoothLookPosition = Vector3.zero;

	private Vector3 _velo_smoothLookPosition = Vector3.zero;

	private Vector3 finalLookPosition = Vector3.zero;

	private bool usingAxisCorrection;

	private Matrix4x4 axisCorrectionMatrix;

	private float delta;

	[Tooltip("If your neck bones are rotated in a wrong way, you can try putting here parent game object of last back bone in chain")]
	public Transform ParentalReferenceBone;

	private Quaternion _parentalBackParentRot;

	private Vector2 _parentalAngles = Vector2.zero;

	private bool animatePhysicsWorking;

	private bool triggerAnimatePhysics;

	private int startAfterTPoseCounter;

	private Vector3 unclampedLookAngles = Vector3.zero;

	private Vector3 targetLookAngles = Vector3.zero;

	private Vector3 animatedLookAngles = Vector3.zero;

	private Vector3 finalLookAngles = Vector3.zero;

	private Quaternion lastBaseRotation;

	private Vector3 _preLookAboveLookAngles = Vector3.zero;

	private Vector3 _velo_animatedLookAngles = Vector3.zero;

	private float _rememberSideLookHorizontalAngle;

	private Vector3 leadBoneInitLocalOffset = Vector3.zero;

	private EFHeadLookState previousState;

	private bool _stopLooking;

	private Transform activeLookTarget;

	private Vector3 activeLookPosition;

	private Transform preActiveLookTarget;

	private bool isLooking;

	[Tooltip("If moment transform should be destroyed when max distance range is exceed")]
	public bool DestroyMomentTargetOnMaxDistance = true;

	private float whenAboveGoBackDuration;

	private float whenAboveGoBackTimer;

	private float _whenAboveGoBackVelo;

	private float _whenAboveGoBackVerticalVelo;

	private Vector2 whenAboveGoBackAngles;

	[Tooltip("If you want to remove animator's keyframes and replace them by look animation")]
	[Range(0f, 1f)]
	public float OverrideRotations;

	private bool overrideRefInitialized;

	private UniRotateBone headOv;

	private int lastClipHash;

	private bool refreshReferencePose;

	private float monitorTransitionTime = 0.8f;

	private List<Quaternion> _monitorTransitionStart;

	public int BackBonesCount;

	public int _preBackBonesCount;

	public List<LookBone> LookBones;

	[Tooltip("When target to follow is null then head will stop moving instead of going back to look in forward direction")]
	public bool NoTargetHeadStops;

	private Quaternion targetLookRotation;

	private float finalMotionWeight = 1f;

	private float animatedMotionWeight = 1f;

	private float _velo_animatedMotionWeight = 1f;

	private float changeTargetSmootherWeight;

	private float changeTargetSmootherBones;

	private Vector3 preLookDir;

	public bool _editor_hideEyes;

	public string _editor_displayName = "Look Animator 2";

	public EEditorLookCategory _Editor_Category;

	[Tooltip("Lead / Head bone - head of look chain")]
	public Transform LeadBone;

	[Tooltip("Base root transform - object which moves / rotates - character transform / game object")]
	public Transform BaseTransform;

	[Tooltip("Faloff value of how weight of animation should be spread over bones")]
	public float FaloffValue = 0.35f;

	public float FaloffValueB = 1.1f;

	[Tooltip("When character is looking far back in big angle or far high, you can automate weights falloff value")]
	public bool BigAngleAutomation;

	[Tooltip("When character is looking far back in big angle or far high, you can automate compensation values")]
	public bool BigAngleAutomationCompensation;

	[Tooltip("If bone weights spread should be computed automatically or by hand")]
	public bool AutoBackbonesWeights = true;

	[Tooltip("When you want use curve for more custom falloff or define it by simple slider - 'FaloffValue'")]
	public bool CurveSpread;

	[Tooltip("Configurable rotation weight placed over back bones - when you will use for example spine bones, here you can define how much will they rotate towards target in reference to other animated bones")]
	public AnimationCurve BackBonesFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0.1f);

	[Header("If you don't want arms to be rotated when spine", order = 1)]
	[Header("bone is rotated by script (drag & drop here)", order = 3)]
	public List<CompensationBone> CompensationBones = new List<CompensationBone>();

	[Range(0f, 1f)]
	public float CompensationWeight = 0.5f;

	[Range(0f, 1f)]
	public float CompensationWeightB = 0.5f;

	[Range(0f, 1f)]
	public float CompensatePositions;

	[Range(0f, 1f)]
	public float CompensatePositionsB;

	private float targetCompensationWeight = 0.5f;

	private float targetCompensationPosWeight;

	[Tooltip("Making script start after first frame so initialization will not catch TPose initial bones rotations, which can cause some wrong offsets for rotations")]
	public bool StartAfterTPose = true;

	[Tooltip("Update with waiting for fixed update clock")]
	public bool AnimatePhysics;

	[Tooltip("If you want look animator to stop computing when choosed mesh is not visible in any camera view (editor's scene camera is detecting it too)")]
	public Renderer OptimizeWithMesh;

	[Tooltip("Object which will be main target of look.\n\nYou can use feature called 'Moment Target' to look at other object for a moment then look back on ObjectToFollow - check LookAnimator.SetMomentLookTarget()")]
	public Transform ObjectToFollow;

	[Tooltip("Position offset on 'ObjectToFollow'")]
	public Vector3 FollowOffset;

	[Tooltip("If 'FollowOffset' should be world position translation\n\nor target object local space translation\n\nor we don't want to use ObjectToFollow and use just 'FollowOffset' position.")]
	public EFFollowMode FollowMode;

	[Range(0f, 2.5f)]
	[Tooltip("How fast character should rotate towards focus direction.\n\nRotationSpeed = 2.5 -> Instant rotation\n\nIt is speed of transition for look direction (no bones rotations smoothing)")]
	public float RotationSpeed = 0.65f;

	private bool instantRotation;

	[Range(0f, 1f)]
	[Tooltip("This variable is making rotation animation become very smooth (but also slower).\nIt is enabling smooth rotation transition in bone rotations")]
	public float UltraSmoother;

	[Header("Look forward if this angle is exceeded", order = 1)]
	[Range(25f, 180f)]
	[Tooltip("If target is too much after transform's back we smooth rotating head back to default animation's rotation")]
	public float StopLookingAbove = 180f;

	[Tooltip("If object in rotation range should be detected only when is nearer than 'StopLookingAbove' to avoid stuttery target changes")]
	[Range(0.1f, 1f)]
	public float StopLookingAboveFactor = 1f;

	[Range(0f, 1f)]
	[Tooltip("If your character moves head too fast when loosing / changing target, here you can adjust it")]
	public float ChangeTargetSmoothing;

	[Tooltip("Switch to enable advanced settings for back bones falloff")]
	public bool AdvancedFalloff;

	[Tooltip("Max distance to target object to lost interest in it.\nValue = 0 -> Not using distance limits.\nWhen you have moment target - after exceeding distance moment target will be forgotten!")]
	public float MaximumDistance;

	[Tooltip("When Character is looking at something on his back but more on his right he look to right, when target suddenly goes more on his left and again to right very frequently you can set with this variable range from which rotating head to opposide shoulder side should be triggered to prevent strange looking behaviour when looking at dynamic objects")]
	[FPD_Suffix(0f, 45f, FPD_SuffixAttribute.SuffixMode.FromMinToMaxRounded, "°", true, 0)]
	public float HoldRotateToOppositeUntil;

	[Tooltip("If object in range should be detected only when is nearer than 'MaxDistance' to avoid stuttery target changes")]
	[Range(0f, 1f)]
	public float MaxOutDistanceFactor;

	[Tooltip("If distance should be measured not using Up (y) axis")]
	public bool Distance2D;

	[Tooltip("Offsetting point from which we want to measure distance to target")]
	public Vector3 DistanceMeasurePoint;

	[Tooltip("Minimum angle needed to trigger head follow movement. Can be useful to make eyes move first and then head when angle is bigger")]
	[FPD_Suffix(0f, 45f, FPD_SuffixAttribute.SuffixMode.FromMinToMaxRounded, "°", true, 0)]
	public float LookWhenAbove;

	private float animatedLookWhenAbove;

	[Tooltip("Separated start look angle for vertical look axis\n\nWhen Zero it will have same value as 'LookWhenAbove'")]
	[FPD_Suffix(0f, 45f, FPD_SuffixAttribute.SuffixMode.FromMinToMaxRounded, "°", true, 0)]
	public float LookWhenAboveVertical;

	private float animatedLookWhenAboveVertical;

	[Tooltip("Head going back looking in front of target after this amount of seconds")]
	[FPD_Suffix(0f, 3f, FPD_SuffixAttribute.SuffixMode.FromMinToMax, "sec", true, 0)]
	public float WhenAboveGoBackAfter;

	[Tooltip("Head going back looking in front of target after this amount of seconds")]
	[FPD_Suffix(0f, 3f, FPD_SuffixAttribute.SuffixMode.FromMinToMax, "sec", true, 0)]
	public float WhenAboveGoBackAfterVertical;

	[Tooltip("Head going back looking in front of target after this amount of seconds")]
	[FPD_Suffix(0.05f, 1f, FPD_SuffixAttribute.SuffixMode.FromMinToMax, "sec", true, 0)]
	public float WhenAboveGoBackDuration = 0.2f;

	[Tooltip("Rotating towards target slower when target don't need much angle to look at")]
	[FPD_Suffix(0f, 90f, FPD_SuffixAttribute.SuffixMode.FromMinToMaxRounded, "°", true, 0)]
	public float StartLookElasticRangeX;

	[Tooltip("Separated elastic start angle for vertical look axis\n\nIf zero then value will be same like 'StartLookElasticRange'")]
	[FPD_Suffix(0f, 90f, FPD_SuffixAttribute.SuffixMode.FromMinToMaxRounded, "°", true, 0)]
	public float StartLookElasticRangeY;

	[Header("Limits for rotation | Horizontal: X Vertical: Y")]
	public Vector2 XRotationLimits = new Vector2(-80f, 80f);

	[Tooltip("Making clamp ranges elastic, so when it starts to reach clamp value it slows like muscles needs more effort")]
	[FPD_Suffix(0f, 60f, FPD_SuffixAttribute.SuffixMode.FromMinToMaxRounded, "°", true, 0)]
	public float XElasticRange = 20f;

	[Tooltip("When head want go back to default state of looking, it will blend with default animation instead of changing values of rotation variables to go back")]
	public bool LimitHolder = true;

	public Vector2 YRotationLimits = new Vector2(-50f, 50f);

	[Tooltip("Making clamp ranges elastic, so when it starts to reach clamp value it slows like muscles needs more effort")]
	[FPD_Suffix(0f, 45f, FPD_SuffixAttribute.SuffixMode.FromMinToMaxRounded, "°", true, 0)]
	public float YElasticRange = 15f;

	[FPD_Percentage(0f, 1f, false, true, "%", false)]
	[Tooltip("You can use this variable to blend intensity of look animator motion over skeleton animation\n\nValue = 1: Animation with Look Animator motion\nValue = 0: Only skeleton animation")]
	public float LookAnimatorAmount = 1f;

	[Tooltip("If head look seems to be calculated like it is not looking from center of head but far from bottom or over it - you can adjust it - check scene view gizmos")]
	public Vector3 StartLookPointOffset;

	[Tooltip("Freezes reference start look position in x and z axes to avoid re-reaching max rotation limits when hips etc. are rotating in animation clip.\n\nIf your character is crouching or so, you would like to have this parameter disabled")]
	public bool AnchorStartLookPoint = true;

	[Tooltip("In some cases you'll want to refresh anchor position during gameplay to make it more fitting to character's animation poses")]
	public bool RefreshStartLookPoint = true;

	[Tooltip("[When some of your bones are rotating making circles]\n\nDon't set hard rotations for bones, use animation rotation and add rotation offset to bones so animation's rotations are animated correctly (useful when using attack animations for example)")]
	public bool SyncWithAnimator = true;

	[Tooltip("When using above action, we need to keep remembered rotations of animation clip from first frame, with monitoring we will remember root rotations from each new animation played")]
	public bool MonitorAnimator;

	private Quaternion rootStaticRotation;

	[FPD_Percentage(0f, 3f, true, true, "%", false)]
	[Tooltip("When you want create strange effects - this variable will overrotate bones")]
	public float WeightsMultiplier = 1f;

	[Range(0.1f, 2.5f)]
	[Tooltip("If speed of looking toward target should be limited then lower this value")]
	public float MaxRotationSpeed = 2.5f;

	[Range(0f, 1f)]
	[Tooltip("When character is rotating and head is rotating with it instead of keep focusing on target, change this value higher")]
	public float BaseRotationCompensation;

	[Tooltip("If your skeleton have not animated keyframes in animation clip then bones would start doing circles with this option disabled\n\nIn most cases all keyframes are filled, if you're sure for baked keyframes you can disable this option to avoid some not needed calculations")]
	public bool DetectZeroKeyframes = true;

	[Range(0f, 1f)]
	[Tooltip("Target position to look can be smoothed out instead of immediate position changes")]
	public float LookAtPositionSmoother;

	[Tooltip("Delta Time for Look Animator calculations")]
	public EFDeltaType DeltaType;

	[Tooltip("Multiplier for delta time resulting in changed speed of calculations for Look Animator")]
	public float SimulationSpeed = 1f;

	[Tooltip("It will make head animation stiff but perfectly looking at target")]
	[Range(0f, 1f)]
	public float OverrideHeadForPerfectLookDirection;

	[Tooltip("Resetting bones before animators update to avoid bones twisting if bones are not animated using unity animator")]
	public bool Calibration = true;

	[Tooltip("With crazy flipped axes from models done in different modelling softwares, sometimes you have to change axes order for Quaternion.LookRotation to work correctly")]
	public EFAxisFixOrder FixingPreset;

	[Tooltip("If your model is not facing 'Z' axis (blue) you can adjust it with this value")]
	public Vector3 ModelForwardAxis = Vector3.forward;

	[Tooltip("If your model is not pointing up 'Y' axis (green) you can adjust it with this value")]
	public Vector3 ModelUpAxis = Vector3.up;

	[Tooltip("Defines model specific bones orientation in order to fix Quaternion.LookRotation axis usage")]
	public Vector3 ManualFromAxis = Vector3.forward;

	public Vector3 ManualToAxis = Vector3.forward;

	public Vector3 FromAuto;

	public Vector3 OffsetAuto;

	public Vector3 parentalReferenceLookForward;

	public Vector3 parentalReferenceUp;

	public Vector3 DynamicReferenceUp;

	[Tooltip("Additional degrees of rotations for head look - for simple correction, sometimes you have just to rotate head in y axis by 90 degrees")]
	public Vector3 RotationOffset = new Vector3(0f, 0f, 0f);

	[Tooltip("Additional degrees of rotations for backones - for example when you have wolf and his neck is going up in comparison to keyfarmed animation\nVariable name 'BackBonesAddOffset'")]
	public Vector3 BackBonesAddOffset = new Vector3(0f, 0f, 0f);

	[Tooltip("[ADVANCED] Axes multiplier for custom fixing flipped armature rotations")]
	public Vector3 RotCorrectionMultiplier = new Vector3(1f, 1f, 1f);

	[Tooltip("View debug rays in scene window")]
	public bool DebugRays;

	[Tooltip("Animation curve mode for rotating toward target")]
	public EFAnimationStyle AnimationStyle;

	[Tooltip("Updating reference axis for parental look rotation mode every frame")]
	public bool ConstantParentalAxisUpdate = true;

	private bool updateLookAnimator = true;

	private bool wasUpdating;

	public Transform MomentLookTransform { get; private set; }

	public EFHeadLookState LookState { get; protected set; }

	public bool initialized { get; protected set; }

	public string EditorIconPath
	{
		get
		{
			if (PlayerPrefs.GetInt("AnimsH", 1) == 0)
			{
				return "";
			}
			return "Look Animator/LookAnimator_SmallIcon";
		}
	}

	public bool UseBoneOffsetRotation => SyncWithAnimator;

	[Obsolete("Use LookAnimatorAmount instead, but remember that it works in reversed way -> LookAnimatorAmount 1 = BlendToOriginal 0  and  LookAnimatorAmount 0 = BlendToOriginal 1, simply you can replace it by using '1 - LookAnimatorAmount'")]
	public float BlendToOriginal
	{
		get
		{
			return 1f - LookAnimatorAmount;
		}
		set
		{
			LookAnimatorAmount = 1f - value;
		}
	}

	[Obsolete("Now using StartLookPointOffset as more responsive naming")]
	public Vector3 LookReferenceOffset
	{
		get
		{
			return StartLookPointOffset;
		}
		set
		{
			StartLookPointOffset = value;
		}
	}

	[Obsolete("Now using AnchorStartLookPoint as more responsive naming")]
	public bool AnchorReferencePoint
	{
		get
		{
			return AnchorStartLookPoint;
		}
		set
		{
			AnchorStartLookPoint = value;
		}
	}

	[Obsolete("Now using RefreshStartLookPoint as more responsive naming")]
	public bool RefreshAnchor
	{
		get
		{
			return RefreshStartLookPoint;
		}
		set
		{
			RefreshStartLookPoint = value;
		}
	}

	[Obsolete("Now using LookWhenAbove as more responsive naming")]
	public float MinHeadLookAngle
	{
		get
		{
			return LookWhenAbove;
		}
		set
		{
			LookWhenAbove = value;
		}
	}

	[Obsolete("Now using StopLookingAbove as more responsive naming")]
	public float MaxRotationDiffrence
	{
		get
		{
			return StopLookingAbove;
		}
		set
		{
			StopLookingAbove = value;
		}
	}

	[Obsolete("Now using SyncWithAnimator as more responsive naming")]
	public bool AnimateWithSource
	{
		get
		{
			return SyncWithAnimator;
		}
		set
		{
			SyncWithAnimator = value;
		}
	}

	public void SwitchLooking(bool? enableLooking = null, float transitionTime = 0.2f, Action callback = null)
	{
		bool enableAnimation = true;
		if (!enableLooking.HasValue)
		{
			if (LookAnimatorAmount > 0.5f)
			{
				enableAnimation = false;
			}
		}
		else if (enableLooking == false)
		{
			enableAnimation = false;
		}
		StopAllCoroutines();
		StartCoroutine(SwitchLookingTransition(transitionTime, enableAnimation, callback));
	}

	public void SwitchLooking(bool enable = true)
	{
		SwitchLooking(enable, 0.5f);
	}

	public void SetLookTarget(Transform transform)
	{
		ObjectToFollow = transform;
		MomentLookTransform = null;
	}

	public void SetLookPosition(Vector3 targetPosition)
	{
		FollowMode = EFFollowMode.FollowJustPosition;
		FollowOffset = targetPosition;
	}

	public Vector2 GetUnclampedLookAngles()
	{
		return unclampedLookAngles;
	}

	public Vector2 GetLookAngles()
	{
		return animatedLookAngles;
	}

	public Vector2 GetTargetLookAngles()
	{
		return targetLookAngles;
	}

	public EFHeadLookState GetCurrentLookState()
	{
		return LookState;
	}

	public Vector2 ComputeAnglesTowards(Vector3 worldPosition)
	{
		Vector3 normalized = (worldPosition - GetLookStartMeasurePosition()).normalized;
		if (usingAxisCorrection)
		{
			normalized = axisCorrectionMatrix.inverse.MultiplyVector(normalized).normalized;
			normalized = WrapVector(Quaternion.LookRotation(normalized, axisCorrectionMatrix.MultiplyVector(ModelUpAxis).normalized).eulerAngles);
		}
		else
		{
			normalized = BaseTransform.InverseTransformDirection(normalized);
			normalized = WrapVector(Quaternion.LookRotation(normalized, BaseTransform.TransformDirection(ModelUpAxis)).eulerAngles);
		}
		return normalized;
	}

	public GameObject SetMomentLookTarget(Transform parent = null, Vector3? position = null, float? destroyTimer = 3f, bool worldPosition = false)
	{
		_ = MomentLookTransform;
		if ((bool)MomentLookTransform)
		{
			_ = MomentLookTransform.parent;
		}
		GameObject gameObject;
		if (!destroyTimer.HasValue)
		{
			if (!generatedMomentTarget)
			{
				generatedMomentTarget = new GameObject(base.transform.gameObject.name + "-MomentLookTarget " + Time.frameCount);
			}
			else
			{
				generatedMomentTarget.name = base.transform.gameObject.name + "-MomentLookTarget " + Time.frameCount;
			}
			gameObject = generatedMomentTarget;
		}
		else
		{
			gameObject = new GameObject(base.transform.gameObject.name + "-MomentLookTarget " + Time.frameCount);
		}
		if (parent != null)
		{
			gameObject.transform.SetParent(parent);
			if (position.HasValue)
			{
				if (worldPosition)
				{
					gameObject.transform.position = position.Value;
				}
				else
				{
					gameObject.transform.localPosition = position.Value;
				}
			}
			else
			{
				gameObject.transform.localPosition = Vector3.zero;
			}
		}
		else if (position.HasValue)
		{
			gameObject.transform.position = position.Value;
		}
		MomentLookTransform = gameObject.transform;
		wasMomentLookTransform = true;
		TargetChangedMeasures();
		if (destroyTimer.HasValue)
		{
			UnityEngine.Object.Destroy(gameObject, destroyTimer.Value);
		}
		return gameObject;
	}

	public void SetMomentLookTransform(Transform transform, float timeToLeft = 0f)
	{
		MomentLookTransform = transform;
		wasMomentLookTransform = true;
		TargetChangedMeasures();
		if (timeToLeft > 0f)
		{
			StartCoroutine(CResetMomentLookTransform(null, timeToLeft));
		}
	}

	public void ForceDestroyMomentTarget()
	{
		if ((bool)generatedMomentTarget)
		{
			UnityEngine.Object.Destroy(generatedMomentTarget);
		}
		else if ((bool)MomentLookTransform)
		{
			MomentLookTransform = null;
		}
	}

	private void InitBirdMode()
	{
		if (!birdModeInitialized)
		{
			lagTimer = 0f;
			birdTargetPositionMemory = GetLookAtPosition();
			BirdTargetPosition = birdTargetPositionMemory;
			birdModeInitialized = true;
		}
	}

	private void CalculateBirdMode()
	{
		lagTimer -= delta;
		if (lagTimer < 0f)
		{
			birdTargetPositionMemory = smoothLookPosition;
		}
		if (LagRotation >= 1f)
		{
			BirdTargetPosition = birdTargetPositionMemory;
		}
		else
		{
			BirdTargetPosition = Vector3.Lerp(smoothLookPosition, birdTargetPositionMemory, LagRotation);
		}
		if (lagTimer < 0f)
		{
			lagTimer = UnityEngine.Random.Range(LagEvery * 0.85f, LagEvery * 1.15f);
		}
		if (!(DelayPosition > 0f))
		{
			return;
		}
		for (int i = 0; i < LookBones.Count; i++)
		{
			LookBones[i].Transform.localPosition = LookBones[i].initLocalPos;
		}
		float num = Vector3.Distance(Vector3.Scale(LookBones[0].targetDelayPosition, new Vector3(1f, 0f, 1f)), Vector3.Scale(LeadBone.position, new Vector3(1f, 0f, 1f)));
		float num2 = Mathf.Abs(LookBones[0].targetDelayPosition.y - LeadBone.position.y);
		if (num > DelayMaxDistance || num2 > DelayMaxDistance / 1.65f)
		{
			for (int num3 = LookBones.Count - 1; num3 >= 0; num3--)
			{
				LookBones[num3].targetDelayPosition = LookBones[num3].Transform.position;
			}
		}
		for (int num4 = LookBones.Count - 1; num4 >= 0; num4--)
		{
			LookBones[num4].animatedDelayPosition = Vector3.Lerp(LookBones[num4].animatedDelayPosition, LookBones[num4].targetDelayPosition, delta * Mathf.Lerp(5f, 30f, DelayGoSpeed));
			LookBones[num4].Transform.position = Vector3.Lerp(LookBones[num4].Transform.position, LookBones[num4].animatedDelayPosition, LookBones[num4].lookWeight * DelayPosition * finalMotionWeight);
		}
	}

	public void SetAutoWeightsDefault()
	{
		CalculateRotationWeights(FaloffValue);
		if (!BigAngleAutomation)
		{
			for (int i = 1; i < LookBones.Count; i++)
			{
				LookBones[i].lookWeight = targetWeights[i];
				LookBones[i].motionWeight = targetWeights[i];
			}
		}
		else
		{
			for (int j = 1; j < LookBones.Count; j++)
			{
				LookBones[j].lookWeight = targetWeights[j];
			}
		}
	}

	public void UpdateAutomationWeights()
	{
		float t = Mathf.InverseLerp(45f, 170f, Mathf.Abs(unclampedLookAngles.y));
		for (int i = 0; i < LookBones.Count; i++)
		{
			LookBones[i].motionWeight = Mathf.LerpUnclamped(LookBones[i].lookWeight, LookBones[i].lookWeightB, t);
		}
	}

	public void RefreshBoneMotionWeights()
	{
		for (int i = 1; i < LookBones.Count; i++)
		{
			LookBones[i].motionWeight = LookBones[i].lookWeight;
		}
	}

	public float[] CalculateRotationWeights(float falloff)
	{
		if (LookBones.Count > 1)
		{
			float num = 0f;
			if (baseWeights == null)
			{
				baseWeights = new float[LookBones.Count];
			}
			if (baseWeights.Length != LookBones.Count)
			{
				baseWeights = new float[LookBones.Count];
			}
			if (targetWeights == null)
			{
				targetWeights = new float[LookBones.Count];
			}
			if (targetWeights.Length != LookBones.Count)
			{
				targetWeights = new float[LookBones.Count];
			}
			if (BackBonesFalloff.length < 2 || !CurveSpread)
			{
				CalculateWeights(baseWeights);
				for (int i = 0; i < baseWeights.Length; i++)
				{
					num += baseWeights[i];
				}
				float b = 1f / (float)(LookBones.Count - 1);
				for (int j = 1; j < LookBones.Count; j++)
				{
					targetWeights[j] = Mathf.LerpUnclamped(baseWeights[j - 1], b, falloff * 1.25f);
				}
			}
			else
			{
				num = 0f;
				float num2 = 1f;
				float num3 = 1f / (float)(LookBones.Count - 1);
				for (int k = 1; k < LookBones.Count; k++)
				{
					targetWeights[k] = BackBonesFalloff.Evaluate(num3 * (float)k) / num2;
					num += targetWeights[k];
				}
				for (int l = 1; l < LookBones.Count; l++)
				{
					targetWeights[l] /= num;
				}
			}
		}
		return targetWeights;
	}

	private void CalculateWeights(float[] weights)
	{
		float num = 1f;
		float num2 = 0.75f;
		float num3 = num;
		weights[0] = num * num2 * 0.65f;
		num3 -= weights[0];
		for (int i = 1; i < weights.Length - 1; i++)
		{
			num3 -= (weights[i] = num3 / (1f + (1f - num2)) * num2);
		}
		weights[^1] = num3;
		num3 = 0f;
	}

	public Transform GetHeadReference()
	{
		if (HeadReference != null)
		{
			return HeadReference;
		}
		return LeadBone;
	}

	public Transform GetEyesTarget()
	{
		if (EyesTarget == null)
		{
			return GetLookAtTransform();
		}
		return EyesTarget;
	}

	[Obsolete("Now please use GetEyesTarget() or GetLookAtTransform() methods")]
	public Transform GetCurrentTarget()
	{
		return GetEyesTarget();
	}

	public Vector3 GetEyesTargetPosition()
	{
		if (EyesTarget == null)
		{
			return GetLookAtPosition();
		}
		return EyesTarget.position;
	}

	private void InitEyesModule()
	{
		eyes = new Transform[0];
		if (LeftEye != null || RightEye != null)
		{
			if (LeftEye != null && RightEye != null)
			{
				eyes = new Transform[2] { LeftEye, RightEye };
			}
			else if (LeftEye != null)
			{
				eyes = new Transform[1] { LeftEye };
			}
			else
			{
				eyes = new Transform[1] { RightEye };
			}
		}
		eyeForwards = new Vector3[eyes.Length];
		eyesInitLocalRotations = new Quaternion[eyes.Length];
		eyesLerpRotations = new Quaternion[eyes.Length];
		for (int i = 0; i < eyeForwards.Length; i++)
		{
			Vector3 position = eyes[i].position + Vector3.Scale(BaseTransform.forward, eyes[i].transform.lossyScale);
			Vector3 position2 = eyes[i].position;
			eyeForwards[i] = (eyes[i].InverseTransformPoint(position) - eyes[i].InverseTransformPoint(position2)).normalized;
			eyesInitLocalRotations[i] = eyes[i].localRotation;
			eyesLerpRotations[i] = eyes[i].rotation;
		}
		headForward = Quaternion.FromToRotation(GetHeadReference().InverseTransformDirection(BaseTransform.forward), Vector3.forward) * Vector3.forward;
	}

	private void UpdateEyesLogics()
	{
		if (CustomEyesLogics)
		{
			return;
		}
		if (EyesNoKeyframes)
		{
			for (int i = 0; i < eyeForwards.Length; i++)
			{
				eyes[i].localRotation = eyesInitLocalRotations[i];
			}
		}
		Transform transform = EyesTarget;
		if (transform == null)
		{
			transform = ((!(MomentLookTransform != null)) ? ObjectToFollow : MomentLookTransform);
		}
		bool flag = false;
		if (transform == null)
		{
			flag = true;
		}
		else if (EyesTarget == null && LookState != EFHeadLookState.ClampedAngle && LookState != EFHeadLookState.Following)
		{
			flag = true;
		}
		if (flag)
		{
			EyesOutOfRangeBlend = Mathf.Max(0f, EyesOutOfRangeBlend - delta);
		}
		else
		{
			EyesOutOfRangeBlend = Mathf.Min(1f, EyesOutOfRangeBlend + delta);
		}
		_eyesBlend = EyesBlend * EyesOutOfRangeBlend * LookAnimatorAmount;
		if (_eyesBlend <= 0f || !(transform != null))
		{
			return;
		}
		Vector3 lookStartMeasurePosition = GetLookStartMeasurePosition();
		Vector3 eulerAngles = Quaternion.LookRotation(transform.position - lookStartMeasurePosition).eulerAngles;
		Vector3 eulerAngles2 = (GetHeadReference().rotation * Quaternion.FromToRotation(headForward, Vector3.forward)).eulerAngles;
		Vector2 vector = new Vector3(Mathf.DeltaAngle(eulerAngles.x, eulerAngles2.x), Mathf.DeltaAngle(eulerAngles.y, eulerAngles2.y));
		if (vector.x > EyesYRange.y)
		{
			eulerAngles.x = eulerAngles2.x - EyesYRange.y;
		}
		else if (vector.x < EyesYRange.x)
		{
			eulerAngles.x = eulerAngles2.x - EyesYRange.x;
		}
		if (vector.y > 0f - EyesXRange.x)
		{
			eulerAngles.y = eulerAngles2.y - EyesXRange.y;
		}
		else if (vector.y < 0f - EyesXRange.y)
		{
			eulerAngles.y = eulerAngles2.y + EyesXRange.y;
		}
		for (int j = 0; j < eyes.Length; j++)
		{
			Quaternion rotation = eyes[j].rotation;
			Quaternion rotation2 = Quaternion.Euler(eulerAngles);
			float num = 1f;
			if (eyes[j] == LeftEye)
			{
				if (InvertLeftEye)
				{
					num = -1f;
				}
			}
			else if (eyes[j] == RightEye && InvertRightEye)
			{
				num = -1f;
			}
			rotation2 *= Quaternion.FromToRotation(eyeForwards[j], Vector3.forward * num);
			rotation2 *= eyesInitLocalRotations[j];
			eyes[j].rotation = rotation2;
			eyes[j].rotation *= Quaternion.Inverse(eyesInitLocalRotations[j]);
			if (EyesOffsetRotation != Vector3.zero)
			{
				eyes[j].rotation *= Quaternion.Euler(EyesOffsetRotation);
			}
			switch (j)
			{
			case 0:
				if (LeftEyeOffsetRotation != Vector3.zero)
				{
					eyes[j].rotation *= Quaternion.Euler(LeftEyeOffsetRotation);
				}
				break;
			case 1:
				if (RightEyeOffsetRotation != Vector3.zero)
				{
					eyes[j].rotation *= Quaternion.Euler(RightEyeOffsetRotation);
				}
				break;
			}
			rotation2 = eyes[j].rotation;
			eyesLerpRotations[j] = Quaternion.Slerp(eyesLerpRotations[j], rotation2, delta * Mathf.Lerp(2f, 40f, EyesSpeed));
			eyes[j].rotation = Quaternion.Slerp(rotation, eyesLerpRotations[j], _eyesBlend);
		}
	}

	private void NoddingChangeTargetCalculations(float angleAmount)
	{
		if ((nodProgress < nodDuration / 10f || nodProgress > nodDuration * 0.85f) && NoddingTransitions != 0f)
		{
			nodProgress = 0f;
			nodDuration = Mathf.Lerp(1f, 0.45f, RotationSpeed / 2.5f);
			if (ChangeTargetSmoothing > 0f)
			{
				nodDuration *= Mathf.Lerp(1f, 1.55f, ChangeTargetSmoothing);
			}
			nodDuration *= Mathf.Lerp(0.8f, 1.4f, Mathf.InverseLerp(10f, 140f, angleAmount));
			nodPower = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(8f, 55f, angleAmount));
		}
	}

	private void NoddingCalculations()
	{
		if (nodProgress < nodDuration)
		{
			if (nodProgress < nodDuration)
			{
				nodProgress += delta;
			}
			else
			{
				nodProgress = nodDuration;
			}
			float value = nodProgress / nodDuration;
			value = FEasing.EaseOutCubic(0f, 1f, value);
			if (value >= 1f)
			{
				nodValue = 0f;
			}
			else
			{
				nodValue = Mathf.Sin(value * MathF.PI);
			}
		}
	}

	public void SetRotationSmoothing(float smoothingDuration = 0.5f, float smoothingPower = 2f)
	{
		if (!(smoothingDuration <= 0f))
		{
			smoothingTimer = smoothingDuration;
			smoothingTime = smoothingDuration;
			this.smoothingPower = smoothingPower;
		}
	}

	private void UpdateSmoothing()
	{
		if (smoothingTimer > 0f)
		{
			smoothingTimer -= delta;
			smoothingEffect = 1f + smoothingTimer / smoothingTime * smoothingPower;
		}
		else
		{
			smoothingEffect = 1f;
		}
	}

	private void AnimateBonesUnsynced(Quaternion diffOnMain, Quaternion backTarget, float d)
	{
		if (nodValue > 0f && BackBonesNod != 0f)
		{
			backTarget *= Quaternion.Euler(NodAxis * (nodValue * nodPower * (0f - NoddingTransitions) * 40f) * BackBonesNod);
		}
		Quaternion targetLook;
		for (int i = 1; i < LookBones.Count; i++)
		{
			LookBones[i].Transform.localRotation = LookBones[i].localStaticRotation;
			targetLook = backTarget * LookBones[i].correctionOffsetQ * Quaternion.Inverse(diffOnMain * LookBones[i].animatedStaticRotation);
			LookBones[i].CalculateMotion(targetLook, WeightsMultiplier, d, finalMotionWeight);
		}
		LookBones[0].Transform.localRotation = LookBones[0].localStaticRotation;
		targetLook = ((!(nodValue <= 0f)) ? (targetLookRotation * LookBones[0].correctionOffsetQ * Quaternion.Euler(NodAxis * (nodValue * nodPower * (0f - NoddingTransitions) * 40f)) * Quaternion.Inverse(diffOnMain * LookBones[0].animatedStaticRotation)) : (targetLookRotation * LookBones[0].correctionOffsetQ * Quaternion.Inverse(diffOnMain * LookBones[0].animatedStaticRotation)));
		LookBones[0].CalculateMotion(targetLook, WeightsMultiplier, d, finalMotionWeight);
	}

	private void AnimateBonesSynced(Quaternion diffOnMain, Quaternion backTarget, float d)
	{
		if (nodValue > 0f && BackBonesNod != 0f)
		{
			backTarget *= Quaternion.Euler(NodAxis * (nodValue * nodPower * (0f - NoddingTransitions) * 40f) * BackBonesNod);
		}
		Quaternion targetLook;
		for (int num = LookBones.Count - 1; num >= 1; num--)
		{
			targetLook = backTarget * LookBones[num].correctionOffsetQ * Quaternion.Inverse(diffOnMain * LookBones[num].animatedStaticRotation);
			LookBones[num].CalculateMotion(targetLook, WeightsMultiplier, d, finalMotionWeight);
		}
		targetLook = ((!(nodValue <= 0f)) ? (targetLookRotation * (LookBones[0].correctionOffsetQ * Quaternion.Euler(NodAxis * (nodValue * nodPower * (0f - NoddingTransitions) * 40f))) * Quaternion.Inverse(diffOnMain * LookBones[0].animatedStaticRotation)) : (targetLookRotation * LookBones[0].correctionOffsetQ * Quaternion.Inverse(diffOnMain * LookBones[0].animatedStaticRotation)));
		LookBones[0].CalculateMotion(targetLook, WeightsMultiplier, d, finalMotionWeight);
	}

	private void AnimateBonesParental(float d)
	{
		float num = nodValue * nodPower * (0f - NoddingTransitions) * 40f;
		float num2 = num * BackBonesNod;
		bool flag = false;
		if (BackBonesAddOffset != Vector3.zero || NoddingTransitions != 0f)
		{
			flag = true;
		}
		for (int num3 = LookBones.Count - 1; num3 >= 1; num3--)
		{
			Quaternion identity = Quaternion.identity;
			if (flag || LookBones[num3].correctionOffset != Vector3.zero)
			{
				if (ParentalOffsetsV == 2)
				{
					identity *= Quaternion.AngleAxis(BackBonesAddOffset.x + LookBones[num3].correctionOffset.x + NodAxis.x * num2, LookBones[num3].right);
					identity *= Quaternion.AngleAxis(BackBonesAddOffset.y + LookBones[num3].correctionOffset.y + NodAxis.y * num2, LookBones[num3].up);
					identity *= Quaternion.AngleAxis(BackBonesAddOffset.z + LookBones[num3].correctionOffset.z + NodAxis.z * num2, LookBones[num3].forward);
				}
				else if (ParentalOffsetsV == 1)
				{
					identity *= Quaternion.AngleAxis(BackBonesAddOffset.x + LookBones[num3].correctionOffset.x + NodAxis.x * num2, LookBones[num3].Transform.right);
					identity *= Quaternion.AngleAxis(BackBonesAddOffset.y + LookBones[num3].correctionOffset.y + NodAxis.y * num2, LookBones[num3].Transform.up);
					identity *= Quaternion.AngleAxis(BackBonesAddOffset.z + LookBones[num3].correctionOffset.z + NodAxis.z * num2, LookBones[num3].Transform.forward);
				}
				else
				{
					identity *= Quaternion.AngleAxis(BackBonesAddOffset.x + LookBones[num3].correctionOffset.x + NodAxis.x * num2, BaseTransform.right);
					identity *= Quaternion.AngleAxis(BackBonesAddOffset.y + LookBones[num3].correctionOffset.y + NodAxis.y * num2, BaseTransform.up);
					identity *= Quaternion.AngleAxis(BackBonesAddOffset.z + LookBones[num3].correctionOffset.z + NodAxis.z * num2, BaseTransform.forward);
				}
			}
			LookBones[num3].CalculateMotion(targetLookRotation * identity, WeightsMultiplier, d, finalMotionWeight);
		}
		Quaternion identity2 = Quaternion.identity;
		if (LookBones[0].correctionOffset != Vector3.zero || NoddingTransitions != 0f)
		{
			if (ParentalOffsetsV == 2)
			{
				identity2 *= Quaternion.AngleAxis(LookBones[0].correctionOffset.x + NodAxis.x * num, LookBones[0].Transform.right);
				identity2 *= Quaternion.AngleAxis(LookBones[0].correctionOffset.y + NodAxis.y * num, LookBones[0].Transform.up);
				identity2 *= Quaternion.AngleAxis(LookBones[0].correctionOffset.z + NodAxis.z * num, LookBones[0].Transform.forward);
			}
			else if (ParentalOffsetsV == 1)
			{
				identity2 *= Quaternion.AngleAxis(LookBones[0].correctionOffset.x + NodAxis.x * num, LookBones[0].right);
				identity2 *= Quaternion.AngleAxis(LookBones[0].correctionOffset.y + NodAxis.y * num, LookBones[0].up);
				identity2 *= Quaternion.AngleAxis(LookBones[0].correctionOffset.z + NodAxis.z * num, LookBones[0].forward);
			}
			else
			{
				identity2 *= Quaternion.AngleAxis(LookBones[0].correctionOffset.x + NodAxis.x * num, BaseTransform.right);
				identity2 *= Quaternion.AngleAxis(LookBones[0].correctionOffset.y + NodAxis.y * num, BaseTransform.up);
				identity2 *= Quaternion.AngleAxis(LookBones[0].correctionOffset.z + NodAxis.z * num, BaseTransform.forward);
			}
		}
		LookBones[0].CalculateMotion(targetLookRotation * identity2, WeightsMultiplier, d, finalMotionWeight);
	}

	private void CalculateLookAnimation()
	{
		_stopLooking = false;
		if (FollowMode != EFFollowMode.FollowJustPosition && ObjectToFollow == null && MomentLookTransform == null)
		{
			_stopLooking = true;
		}
		LookPositionUpdate();
		LookWhenAboveGoBackCalculations();
		if (_stopLooking)
		{
			finalLookPosition = base.transform.TransformPoint(lookFreezeFocusPoint);
		}
		else if (!BirdMode)
		{
			finalLookPosition = smoothLookPosition;
		}
		else
		{
			finalLookPosition = BirdTargetPosition;
		}
		Vector3 rotations;
		if (FixingPreset != 0)
		{
			if (LookState == EFHeadLookState.OutOfMaxDistance)
			{
				targetLookAngles = Vector3.MoveTowards(targetLookAngles, Vector3.zero, 1f + RotationSpeed);
			}
			else
			{
				Vector3 normalized = (finalLookPosition - GetLookStartMeasurePosition()).normalized;
				if (usingAxisCorrection)
				{
					normalized = axisCorrectionMatrix.inverse.MultiplyVector(normalized).normalized;
					normalized = WrapVector(Quaternion.LookRotation(normalized, axisCorrectionMatrix.MultiplyVector(ModelUpAxis).normalized).eulerAngles);
				}
				else
				{
					normalized = BaseTransform.InverseTransformDirection(normalized);
					normalized = WrapVector(Quaternion.LookRotation(normalized, BaseTransform.TransformDirection(ModelUpAxis)).eulerAngles);
				}
				targetLookAngles = normalized;
			}
			Vector2 angles = targetLookAngles;
			angles = LimitAnglesCalculations(angles);
			AnimateAnglesTowards(angles);
			if (usingAxisCorrection)
			{
				Quaternion quaternion = Quaternion.FromToRotation(Vector3.right, Vector3.Cross(Vector3.up, ModelForwardAxis));
				rotations = (Quaternion.Euler(finalLookAngles) * quaternion * BaseTransform.rotation).eulerAngles;
			}
			else
			{
				rotations = finalLookAngles + BaseTransform.eulerAngles;
			}
			rotations += RotationOffset;
			rotations = ConvertFlippedAxes(rotations);
		}
		else
		{
			rotations = LookRotationParental((finalLookPosition - GetLookStartMeasurePosition()).normalized).eulerAngles;
		}
		if (!_stopLooking)
		{
			lookFreezeFocusPoint = BaseTransform.InverseTransformPoint(finalLookPosition);
		}
		targetLookRotation = Quaternion.Euler(rotations);
		SetTargetBonesRotations();
	}

	private void SetTargetBonesRotations()
	{
		if (FixingPreset == EFAxisFixOrder.Parental)
		{
			if (UltraSmoother <= 0f)
			{
				AnimateBonesParental(1f);
			}
			else
			{
				AnimateBonesParental(delta * Mathf.Lerp(21f, 3f, UltraSmoother));
			}
			return;
		}
		Quaternion backTarget = targetLookRotation * Quaternion.Euler(BackBonesAddOffset);
		Quaternion diffOnMain = BaseTransform.rotation * Quaternion.Inverse(rootStaticRotation);
		if (UseBoneOffsetRotation)
		{
			if (UltraSmoother <= 0f)
			{
				AnimateBonesSynced(diffOnMain, backTarget, 1f);
			}
			else
			{
				AnimateBonesSynced(diffOnMain, backTarget, delta * Mathf.Lerp(21f, 3f, UltraSmoother));
			}
		}
		else if (UltraSmoother <= 0f)
		{
			AnimateBonesUnsynced(diffOnMain, backTarget, 1f);
		}
		else
		{
			AnimateBonesUnsynced(diffOnMain, backTarget, delta * Mathf.Lerp(21f, 3f, UltraSmoother));
		}
	}

	private Quaternion LookRotationParental(Vector3 direction)
	{
		if (!SyncWithAnimator)
		{
			for (int i = 0; i < LookBones.Count; i++)
			{
				LookBones[i].Transform.localRotation = LookBones[i].localStaticRotation;
			}
		}
		if (ParentalReferenceBone == null)
		{
			_parentalBackParentRot = LeadBone.parent.rotation;
		}
		else
		{
			_parentalBackParentRot = ParentalReferenceBone.rotation;
		}
		Vector3 vector = Quaternion.Inverse(_parentalBackParentRot) * direction.normalized;
		_parentalAngles.y = AngleAroundAxis(parentalReferenceLookForward, vector, parentalReferenceUp);
		Vector3 axis = Vector3.Cross(parentalReferenceUp, vector);
		Vector3 firstDirection = vector - Vector3.Project(vector, parentalReferenceUp);
		_parentalAngles.x = AngleAroundAxis(firstDirection, vector, axis);
		_parentalAngles = LimitAnglesCalculations(_parentalAngles);
		_parentalAngles = AnimateAnglesTowards(_parentalAngles);
		Vector3 referenceRightDir = Vector3.Cross(parentalReferenceUp, parentalReferenceLookForward);
		if (NoddingTransitions != 0f)
		{
			float num = nodValue * nodPower * 40f;
			_parentalAngles.x += num * BackBonesNod;
		}
		if (RotationOffset != Vector3.zero)
		{
			_parentalAngles += new Vector2(RotationOffset.x, RotationOffset.y);
		}
		return ParentalRotationMaths(referenceRightDir, _parentalAngles.x, _parentalAngles.y);
	}

	private Quaternion ParentalRotationMaths(Vector3 referenceRightDir, float xAngle, float yAngle)
	{
		Vector3 normal = Quaternion.AngleAxis(yAngle, parentalReferenceUp) * Quaternion.AngleAxis(xAngle, referenceRightDir) * parentalReferenceLookForward;
		Vector3 tangent = parentalReferenceUp;
		Vector3.OrthoNormalize(ref normal, ref tangent);
		Vector3 normal2 = normal;
		DynamicReferenceUp = tangent;
		Vector3.OrthoNormalize(ref normal2, ref DynamicReferenceUp);
		return _parentalBackParentRot * Quaternion.LookRotation(normal2, DynamicReferenceUp) * Quaternion.Inverse(_parentalBackParentRot * Quaternion.LookRotation(parentalReferenceLookForward, parentalReferenceUp));
	}

	private void UpdateCorrectionMatrix()
	{
		if (ModelUpAxis != Vector3.up || ModelForwardAxis != Vector3.forward)
		{
			usingAxisCorrection = true;
			axisCorrectionMatrix = Matrix4x4.TRS(BaseTransform.position, Quaternion.LookRotation(BaseTransform.TransformDirection(ModelForwardAxis), BaseTransform.TransformDirection(ModelUpAxis)), BaseTransform.lossyScale);
		}
		else
		{
			usingAxisCorrection = false;
		}
	}

	private IEnumerator AnimatePhysicsClock()
	{
		animatePhysicsWorking = true;
		while (true)
		{
			yield return CoroutineEx.waitForFixedUpdate;
			triggerAnimatePhysics = true;
		}
	}

	private IEnumerator SwitchLookingTransition(float transitionTime, bool enableAnimation, Action callback = null)
	{
		float time = 0f;
		float startBlend = LookAnimatorAmount;
		while (time < transitionTime)
		{
			time += delta;
			float t = time / transitionTime;
			if (enableAnimation)
			{
				LookAnimatorAmount = Mathf.Lerp(startBlend, 1f, t);
			}
			else
			{
				LookAnimatorAmount = Mathf.Lerp(startBlend, 0f, t);
			}
			yield return null;
		}
		callback?.Invoke();
	}

	private IEnumerator CResetMomentLookTransform(Transform transform, float time)
	{
		yield return null;
		yield return null;
		yield return CoroutineEx.waitForSeconds(time);
		yield return null;
		MomentLookTransform = transform;
	}

	public Vector2 LimitAnglesCalculations(Vector2 angles)
	{
		if (LookState == EFHeadLookState.OutOfMaxDistance)
		{
			angles = Vector2.MoveTowards(angles, Vector2.zero, 2.75f + RotationSpeed);
			return angles;
		}
		unclampedLookAngles = angles;
		if (LookState != EFHeadLookState.OutOfMaxRotation)
		{
			if (Mathf.Abs(angles.y) > StopLookingAbove)
			{
				LookState = EFHeadLookState.OutOfMaxRotation;
				return angles;
			}
		}
		else
		{
			if (!(Mathf.Abs(angles.y) <= StopLookingAbove * StopLookingAboveFactor))
			{
				angles = Vector3.MoveTowards(angles, Vector2.zero, 2.75f + RotationSpeed);
				return angles;
			}
			LookState = EFHeadLookState.Null;
		}
		if (LookState == EFHeadLookState.Null)
		{
			LookState = EFHeadLookState.Following;
		}
		if (LookState == EFHeadLookState.Following || LookState == EFHeadLookState.ClampedAngle)
		{
			if (angles.y < XRotationLimits.x)
			{
				angles.y = GetClampedAngle(unclampedLookAngles.y, XRotationLimits.x, XElasticRange, -1f);
				if (angles.y < unclampedLookAngles.y)
				{
					angles.y = unclampedLookAngles.y;
				}
				LookState = EFHeadLookState.ClampedAngle;
			}
			else if (angles.y > XRotationLimits.y)
			{
				angles.y = GetClampedAngle(unclampedLookAngles.y, XRotationLimits.y, XElasticRange);
				if (angles.y > unclampedLookAngles.y)
				{
					angles.y = unclampedLookAngles.y;
				}
				LookState = EFHeadLookState.ClampedAngle;
			}
			else
			{
				LookState = EFHeadLookState.Following;
			}
			if (angles.x < YRotationLimits.x)
			{
				angles.x = GetClampedAngle(angles.x, YRotationLimits.x, YElasticRange, -1f);
				if (angles.x < unclampedLookAngles.x)
				{
					angles.x = unclampedLookAngles.x;
				}
				LookState = EFHeadLookState.ClampedAngle;
			}
			else if (angles.x > YRotationLimits.y)
			{
				angles.x = GetClampedAngle(angles.x, YRotationLimits.y, YElasticRange);
				if (angles.x > unclampedLookAngles.x)
				{
					angles.x = unclampedLookAngles.x;
				}
				LookState = EFHeadLookState.ClampedAngle;
			}
			else if (LookState != EFHeadLookState.ClampedAngle)
			{
				LookState = EFHeadLookState.Following;
			}
		}
		if (StartLookElasticRangeX > 0f)
		{
			float t = Mathf.Abs(angles.y) / StartLookElasticRangeX;
			angles.y = Mathf.Lerp(0f, angles.y, t);
		}
		if (StartLookElasticRangeY > 0f)
		{
			float t2 = Mathf.Abs(angles.x) / StartLookElasticRangeY;
			angles.x = Mathf.Lerp(0f, angles.x, t2);
		}
		if (HoldRotateToOppositeUntil > 0f)
		{
			int num = 0;
			if (_rememberSideLookHorizontalAngle > 0f && unclampedLookAngles.y < 0f)
			{
				num = 1;
			}
			else if (_rememberSideLookHorizontalAngle < 0f && unclampedLookAngles.y > 0f)
			{
				num = -1;
			}
			if (num != 0)
			{
				if (num < 0)
				{
					if (unclampedLookAngles.y < 180f - HoldRotateToOppositeUntil)
					{
						_rememberSideLookHorizontalAngle = angles.y;
					}
					else
					{
						angles.y = _rememberSideLookHorizontalAngle;
					}
				}
				else if (0f - unclampedLookAngles.y < 180f - HoldRotateToOppositeUntil)
				{
					_rememberSideLookHorizontalAngle = angles.y;
				}
				else
				{
					angles.y = _rememberSideLookHorizontalAngle;
				}
			}
			else
			{
				_rememberSideLookHorizontalAngle = angles.y;
			}
		}
		if (LookWhenAbove > 0f)
		{
			whenAboveGoBackAngles = angles;
			float num2 = Mathf.Abs(Mathf.DeltaAngle(_preLookAboveLookAngles.y, angles.y));
			if (num2 < animatedLookWhenAbove)
			{
				angles.y = _preLookAboveLookAngles.y;
			}
			else
			{
				angles.y = Mathf.LerpUnclamped(_preLookAboveLookAngles.y, angles.y, (num2 - animatedLookWhenAbove) / num2);
				_preLookAboveLookAngles.y = angles.y;
			}
			float num3 = ((animatedLookWhenAboveVertical > 0f) ? animatedLookWhenAboveVertical : animatedLookWhenAbove);
			num2 = Mathf.Abs(Mathf.DeltaAngle(_preLookAboveLookAngles.x, angles.x));
			if (num2 < num3)
			{
				angles.x = _preLookAboveLookAngles.x;
			}
			else
			{
				angles.x = Mathf.LerpUnclamped(_preLookAboveLookAngles.x, angles.x, (num2 - num3) / num2);
				_preLookAboveLookAngles.x = angles.x;
			}
		}
		return angles;
	}

	public Vector2 AnimateAnglesTowards(Vector2 angles)
	{
		if (!usingAxisCorrection)
		{
			Vector3 eulerAngles = (BaseTransform.rotation * Quaternion.Inverse(lastBaseRotation)).eulerAngles;
			eulerAngles = WrapVector(eulerAngles) * BaseRotationCompensation;
			animatedLookAngles -= eulerAngles;
		}
		if (!instantRotation)
		{
			switch (AnimationStyle)
			{
			case EFAnimationStyle.SmoothDamp:
			{
				float num4 = ((RotationSpeed < 0.8f) ? Mathf.Lerp(0.4f, 0.18f, RotationSpeed / 0.8f) : ((RotationSpeed < 1.7f) ? Mathf.Lerp(0.18f, 0.1f, (RotationSpeed - 0.8f) / 0.90000004f) : ((!(RotationSpeed < 2.15f)) ? Mathf.Lerp(0.05f, 0.02f, (RotationSpeed - 2.15f) / 0.3499999f) : Mathf.Lerp(0.1f, 0.05f, (RotationSpeed - 1.7f) / 0.45000005f))));
				num4 *= smoothingEffect;
				animatedLookAngles = Vector3.SmoothDamp(maxSpeed: (MaxRotationSpeed >= 2.5f) ? float.PositiveInfinity : ((MaxRotationSpeed < 0.8f) ? Mathf.Lerp(100f, 430f, MaxRotationSpeed / 0.8f) : ((!(MaxRotationSpeed < 1.7f)) ? Mathf.Lerp(685f, 1250f, (MaxRotationSpeed - 1.7f) / 0.79999995f) : Mathf.Lerp(430f, 685f, (MaxRotationSpeed - 0.8f) / 0.90000004f))), current: animatedLookAngles, target: angles, currentVelocity: ref _velo_animatedLookAngles, smoothTime: num4, deltaTime: delta);
				break;
			}
			case EFAnimationStyle.FastLerp:
			{
				float num = ((RotationSpeed < 0.8f) ? Mathf.Lerp(2.85f, 4.5f, RotationSpeed / 0.8f) : ((RotationSpeed < 1.7f) ? Mathf.Lerp(4.5f, 10f, (RotationSpeed - 0.8f) / 0.90000004f) : ((!(RotationSpeed < 2.15f)) ? Mathf.Lerp(14f, 25f, (RotationSpeed - 2.15f) / 0.3499999f) : Mathf.Lerp(10f, 14f, (RotationSpeed - 1.7f) / 0.45000005f))));
				num /= smoothingEffect;
				Vector3 a = Vector3.Lerp(animatedLookAngles, angles, num * delta);
				if (MaxRotationSpeed < 2.5f)
				{
					float num2 = ((MaxRotationSpeed < 1.1f) ? Mathf.Lerp(5f, 9f, MaxRotationSpeed / 1.1f) : ((!(MaxRotationSpeed < 1.7f)) ? Mathf.Lerp(20f, 45f, (MaxRotationSpeed - 1.7f) / 0.79999995f) : Mathf.Lerp(9f, 20f, (MaxRotationSpeed - 1.1f) / 0.6f)));
					float num3 = Vector3.Distance(a, animatedLookAngles);
					if (num3 > num2)
					{
						num /= 1f + (num3 - num2) / 3f;
					}
					a = Vector3.Lerp(animatedLookAngles, angles, num * delta);
				}
				animatedLookAngles = a;
				break;
			}
			case EFAnimationStyle.Linear:
				animatedLookAngles = Vector3.MoveTowards(animatedLookAngles, angles, delta * (0.2f + RotationSpeed) * 300f);
				break;
			}
		}
		else
		{
			animatedLookAngles = angles;
		}
		finalLookAngles = Vector3.LerpUnclamped(Vector3.zero, animatedLookAngles, finalMotionWeight);
		return finalLookAngles;
	}

	public Vector3 GetDistanceMeasurePosition()
	{
		return BaseTransform.position + BaseTransform.TransformVector(DistanceMeasurePoint);
	}

	public Vector3 GetLookStartMeasurePosition()
	{
		_LOG_NoRefs();
		if (AnchorStartLookPoint)
		{
			if (usingAxisCorrection)
			{
				if (!Application.isPlaying)
				{
					UpdateCorrectionMatrix();
				}
				if (leadBoneInitLocalOffset == Vector3.zero)
				{
					return LeadBone.position + axisCorrectionMatrix.MultiplyVector(StartLookPointOffset);
				}
				return axisCorrectionMatrix.MultiplyPoint(leadBoneInitLocalOffset) + axisCorrectionMatrix.MultiplyVector(StartLookPointOffset);
			}
			if (leadBoneInitLocalOffset == Vector3.zero)
			{
				return LeadBone.position + BaseTransform.TransformVector(StartLookPointOffset);
			}
			return BaseTransform.TransformPoint(leadBoneInitLocalOffset) + BaseTransform.TransformVector(StartLookPointOffset);
		}
		if (!Application.isPlaying)
		{
			LookBones[0].finalRotation = LeadBone.transform.rotation;
		}
		return LeadBone.position + LookBones[0].finalRotation * StartLookPointOffset;
	}

	public void RefreshLookStartPositionAnchor()
	{
		if (!usingAxisCorrection)
		{
			leadBoneInitLocalOffset = BaseTransform.InverseTransformPoint(LeadBone.position);
		}
		else
		{
			leadBoneInitLocalOffset = axisCorrectionMatrix.inverse.MultiplyPoint(LeadBone.position);
		}
		RefreshStartLookPoint = false;
	}

	private float GetDistanceMeasure(Vector3 targetPosition)
	{
		if (Distance2D)
		{
			Vector3 distanceMeasurePosition = GetDistanceMeasurePosition();
			return Vector2.Distance(new Vector2(distanceMeasurePosition.x, distanceMeasurePosition.z), new Vector2(targetPosition.x, targetPosition.z));
		}
		return Vector3.Distance(GetDistanceMeasurePosition(), targetPosition);
	}

	private void UpdateLookAnimatorAmountWeight()
	{
		if (!_stopLooking && (LookState == EFHeadLookState.OutOfMaxDistance || LookState == EFHeadLookState.OutOfMaxRotation || LookState == EFHeadLookState.Null))
		{
			_stopLooking = true;
		}
		float num = (BirdMode ? RotationSpeed : 1f);
		if (_stopLooking)
		{
			animatedMotionWeight = Mathf.SmoothDamp(animatedMotionWeight, 0f, ref _velo_animatedMotionWeight, Mathf.Lerp(0.5f, 0.25f, RotationSpeed / 2.5f), float.PositiveInfinity, delta * num);
		}
		else
		{
			if (previousState == EFHeadLookState.OutOfMaxRotation)
			{
				OnRangeStateChanged();
			}
			animatedMotionWeight = Mathf.SmoothDamp(animatedMotionWeight, 1f, ref _velo_animatedMotionWeight, Mathf.Lerp(0.3f, 0.125f, RotationSpeed / 2.5f), float.PositiveInfinity, delta * num);
		}
		finalMotionWeight = animatedMotionWeight * LookAnimatorAmount;
		if (finalMotionWeight > 0.999f)
		{
			finalMotionWeight = 1f;
		}
	}

	private void EndUpdate()
	{
		preActiveLookTarget = activeLookTarget;
		preWeightFaloff = FaloffValue;
		lastBaseRotation = BaseTransform.rotation;
		preLookDir = GetCurrentHeadForwardDirection();
	}

	private void LookPositionUpdate()
	{
		if (LookAtPositionSmoother > 0f)
		{
			smoothLookPosition = Vector3.SmoothDamp(smoothLookPosition, activeLookPosition, ref _velo_smoothLookPosition, LookAtPositionSmoother / 2f, float.PositiveInfinity, delta);
		}
		else
		{
			smoothLookPosition = activeLookPosition;
		}
	}

	private void TargetingUpdate()
	{
		activeLookTarget = GetLookAtTransform();
		activeLookPosition = GetLookAtPosition();
		if (preActiveLookTarget != activeLookTarget)
		{
			OnTargetChanged();
		}
	}

	public Vector3 GetLookAtPosition()
	{
		_LOG_NoRefs();
		if (FollowMode == EFFollowMode.FollowJustPosition)
		{
			return FollowOffset;
		}
		Transform lookAtTransform = activeLookTarget;
		if (lookAtTransform == null)
		{
			lookAtTransform = GetLookAtTransform();
		}
		if (!lookAtTransform)
		{
			return LeadBone.position + BaseTransform.TransformVector(ModelForwardAxis) * Vector3.Distance(LeadBone.position, BaseTransform.position);
		}
		if (FollowMode == EFFollowMode.ToFollowSpaceOffset)
		{
			return lookAtTransform.position + lookAtTransform.TransformVector(FollowOffset);
		}
		if (FollowMode == EFFollowMode.WorldOffset)
		{
			return lookAtTransform.position + FollowOffset;
		}
		if (FollowMode == EFFollowMode.LocalOffset)
		{
			return lookAtTransform.position + BaseTransform.TransformVector(FollowOffset);
		}
		return lookAtTransform.position;
	}

	public Transform GetLookAtTransform()
	{
		if ((bool)MomentLookTransform)
		{
			if (!wasMomentLookTransform)
			{
				OnTargetChanged();
				wasMomentLookTransform = true;
			}
			return MomentLookTransform;
		}
		if (!MomentLookTransform)
		{
			if (wasMomentLookTransform)
			{
				OnTargetChanged();
				wasMomentLookTransform = false;
			}
			if ((bool)ObjectToFollow)
			{
				return ObjectToFollow;
			}
		}
		return null;
	}

	public Vector3 GetForwardPosition()
	{
		return LeadBone.position + BaseTransform.TransformDirection(ModelForwardAxis);
	}

	protected void TargetChangedMeasures()
	{
		Vector3 currentHeadForwardDirection = GetCurrentHeadForwardDirection();
		Vector3 normalized = preLookDir.normalized;
		Vector3 vector = Quaternion.LookRotation(currentHeadForwardDirection).eulerAngles;
		Vector3 vector2 = ((normalized == Vector3.zero) ? Vector3.zero : Quaternion.LookRotation(normalized).eulerAngles);
		Vector3 eulerAngles = Quaternion.LookRotation(base.transform.TransformVector(ModelForwardAxis)).eulerAngles;
		Vector2 vector3 = new Vector3(Mathf.DeltaAngle(vector.x, eulerAngles.x), Mathf.DeltaAngle(vector.y, eulerAngles.y));
		float num = StopLookingAbove;
		if (Mathf.Abs(XRotationLimits.x) > StopLookingAbove)
		{
			num = Mathf.Abs(XRotationLimits.x);
		}
		if (Mathf.Abs(vector3.y) > num)
		{
			vector = eulerAngles;
		}
		else
		{
			if (vector3.y < XRotationLimits.x)
			{
				vector.y = eulerAngles.y + XRotationLimits.y;
			}
			if (vector3.y > XRotationLimits.y)
			{
				vector.y = eulerAngles.y + XRotationLimits.x;
			}
			if (vector3.x < YRotationLimits.x)
			{
				vector.x = eulerAngles.x + XRotationLimits.x;
			}
			if (vector3.x > YRotationLimits.y)
			{
				vector.x = eulerAngles.x + XRotationLimits.y;
			}
		}
		Vector2 vector4 = new Vector3(Mathf.DeltaAngle(vector.x, vector2.x), Mathf.DeltaAngle(vector.y, vector2.y));
		float num2 = Mathf.Abs(vector4.x) + Mathf.Abs(vector4.y);
		if (ChangeTargetSmoothing > 0f && num2 > 20f)
		{
			SetRotationSmoothing(Mathf.Lerp(0.15f + ChangeTargetSmoothing * 0.25f, 0.4f + ChangeTargetSmoothing * 0.2f, Mathf.InverseLerp(20f, 180f, num2)), Mathf.Lerp(0.7f, 3f, ChangeTargetSmoothing));
		}
		NoddingChangeTargetCalculations(num2);
	}

	private void MaxDistanceCalculations()
	{
		if (MaximumDistance > 0f)
		{
			if (isLooking)
			{
				if (GetDistanceMeasure(activeLookPosition) > MaximumDistance + MaximumDistance * MaxOutDistanceFactor)
				{
					LookState = EFHeadLookState.OutOfMaxDistance;
					OnRangeStateChanged();
					if (DestroyMomentTargetOnMaxDistance)
					{
						ForceDestroyMomentTarget();
					}
				}
			}
			else if (LookState == EFHeadLookState.OutOfMaxDistance && GetDistanceMeasure(activeLookPosition) <= MaximumDistance)
			{
				LookState = EFHeadLookState.Null;
				OnRangeStateChanged();
			}
		}
		else if (LookState == EFHeadLookState.OutOfMaxDistance)
		{
			LookState = EFHeadLookState.Null;
		}
	}

	protected virtual void OnTargetChanged()
	{
		TargetChangedMeasures();
	}

	protected virtual void OnRangeStateChanged()
	{
		TargetChangedMeasures();
	}

	private void BeginStateCheck()
	{
		if (activeLookTarget == null)
		{
			LookState = EFHeadLookState.Null;
		}
		else if (LookState == EFHeadLookState.Null)
		{
			LookState = EFHeadLookState.Following;
		}
		previousState = LookState;
		isLooking = LookState != EFHeadLookState.OutOfMaxDistance && LookState != EFHeadLookState.OutOfMaxRotation;
	}

	private void LookWhenAboveGoBackCalculations()
	{
		if (whenAboveGoBackDuration > 0f)
		{
			if (WhenAboveGoBackAfter > 0f)
			{
				animatedLookWhenAbove = Mathf.SmoothDamp(animatedLookWhenAbove, 0f, ref _whenAboveGoBackVelo, whenAboveGoBackDuration, float.PositiveInfinity, delta);
				if (animatedLookWhenAbove <= 0.001f)
				{
					whenAboveGoBackDuration = 0f;
				}
				if (LookWhenAboveVertical <= 0f)
				{
					animatedLookWhenAboveVertical = animatedLookWhenAbove;
				}
				else
				{
					animatedLookWhenAboveVertical = Mathf.SmoothDamp(animatedLookWhenAboveVertical, 0f, ref _whenAboveGoBackVerticalVelo, whenAboveGoBackDuration, float.PositiveInfinity, delta);
				}
			}
			return;
		}
		if (animatedLookWhenAbove < LookWhenAbove)
		{
			animatedLookWhenAbove = Mathf.SmoothDamp(animatedLookWhenAbove, LookWhenAbove, ref _whenAboveGoBackVelo, whenAboveGoBackDuration, float.PositiveInfinity, delta);
		}
		if (LookWhenAboveVertical <= 0f)
		{
			animatedLookWhenAboveVertical = animatedLookWhenAbove;
		}
		else if (animatedLookWhenAboveVertical < LookWhenAboveVertical)
		{
			animatedLookWhenAboveVertical = Mathf.SmoothDamp(animatedLookWhenAboveVertical, LookWhenAboveVertical, ref _whenAboveGoBackVerticalVelo, whenAboveGoBackDuration, float.PositiveInfinity, delta);
		}
		if (WhenAboveGoBackAfter > 0f)
		{
			float value = Mathf.Abs(_preLookAboveLookAngles.x - whenAboveGoBackAngles.x) + Mathf.Abs(_preLookAboveLookAngles.y - whenAboveGoBackAngles.y);
			whenAboveGoBackTimer += delta * Mathf.Lerp(0f, 1f, Mathf.InverseLerp(LookWhenAbove / 5f, LookWhenAbove, value));
			if (whenAboveGoBackTimer > WhenAboveGoBackAfter)
			{
				whenAboveGoBackTimer = 0f;
				whenAboveGoBackDuration = WhenAboveGoBackDuration;
			}
		}
	}

	private void PreCalibrateBones()
	{
		if (Calibration)
		{
			for (int i = 0; i < LookBones.Count; i++)
			{
				LookBones[i].Transform.localRotation = LookBones[i].initLocalRot;
			}
		}
	}

	private void CalibrateBones()
	{
		if (OverrideRotations > 0f)
		{
			for (int i = 0; i < LookBones.Count; i++)
			{
				LookBones[i].Transform.localRotation = Quaternion.LerpUnclamped(LookBones[i].Transform.localRotation, LookBones[i].initLocalRot, OverrideRotations * LookAnimatorAmount);
			}
		}
		if (ConstantParentalAxisUpdate)
		{
			RefreshParentalLookReferenceAxis();
		}
		if (RotationSpeed >= 2.5f)
		{
			instantRotation = true;
		}
		else
		{
			instantRotation = false;
		}
		if (refreshReferencePose)
		{
			RefreshReferencePose();
		}
		if (_preBackBonesCount != BackBonesCount)
		{
			if (BackBonesCount > _preBackBonesCount)
			{
				for (int j = _preBackBonesCount; j < LookBones.Count; j++)
				{
					LookBones[j].RefreshStaticRotation();
				}
			}
			preWeightFaloff = FaloffValue - 0.001f;
			_preBackBonesCount = BackBonesCount;
		}
		for (int k = 0; k < CompensationBones.Count; k++)
		{
			if (!(CompensationBones[k].Transform == null))
			{
				CompensationBones[k].RefreshCompensationFrame();
				CompensationBones[k].CheckForZeroKeyframes();
			}
		}
		if (!BigAngleAutomation)
		{
			if (AutoBackbonesWeights)
			{
				if (FaloffValue != preWeightFaloff)
				{
					SetAutoWeightsDefault();
				}
			}
			else
			{
				RefreshBoneMotionWeights();
			}
			LookBones[0].motionWeight = LookBones[0].lookWeight;
		}
		else
		{
			UpdateAutomationWeights();
		}
		switch (DeltaType)
		{
		case EFDeltaType.DeltaTime:
			delta = Time.deltaTime;
			break;
		case EFDeltaType.SmoothDeltaTime:
			delta = Time.smoothDeltaTime;
			break;
		case EFDeltaType.UnscaledDeltaTime:
			delta = Time.unscaledDeltaTime;
			break;
		case EFDeltaType.FixedDeltaTime:
			delta = Time.fixedDeltaTime;
			break;
		}
		delta *= SimulationSpeed;
		if (RefreshStartLookPoint)
		{
			RefreshLookStartPositionAnchor();
		}
		changeTargetSmootherWeight = Mathf.Min(1f, changeTargetSmootherWeight + delta * 0.6f);
		changeTargetSmootherBones = Mathf.Min(1f, changeTargetSmootherBones + delta * 0.6f);
	}

	private void ChangeBonesRotations()
	{
		for (int i = 0; i < LookBones.Count; i++)
		{
			LookBones[i].Transform.rotation = LookBones[i].finalRotation;
		}
		LookBones[0].Transform.rotation = LookBones[0].finalRotation;
		if (BigAngleAutomationCompensation)
		{
			float t = Mathf.InverseLerp(45f, 170f, Mathf.Abs(unclampedLookAngles.y));
			targetCompensationWeight = Mathf.Lerp(CompensationWeight, CompensationWeightB, t);
			targetCompensationPosWeight = Mathf.Lerp(CompensatePositions, CompensatePositionsB, t);
		}
		else
		{
			targetCompensationWeight = CompensationWeight;
			targetCompensationPosWeight = CompensatePositions;
		}
		for (int j = 0; j < CompensationBones.Count; j++)
		{
			if (!(CompensationBones[j].Transform == null))
			{
				CompensationBones[j].SetRotationCompensation(targetCompensationWeight);
				CompensationBones[j].SetPositionCompensation(targetCompensationPosWeight);
			}
		}
		if (UseBoneOffsetRotation)
		{
			for (int k = 0; k < LookBones.Count; k++)
			{
				LookBones[k].lastFinalLocalRotation = LookBones[k].Transform.localRotation;
			}
		}
	}

	private void CheckOverrideReference()
	{
		if (!overrideRefInitialized)
		{
			GameObject gameObject = new GameObject(LookBones[0].Transform.name + "-Overr");
			gameObject.transform.SetParent(LookBones[0].Transform);
			gameObject.transform.localRotation = Quaternion.identity;
			gameObject.transform.localPosition = Vector3.zero;
			headOv = new UniRotateBone(gameObject.transform, BaseTransform);
			headOv.RefreshCustomAxis(Vector3.up, Vector3.forward);
			overrideRefInitialized = true;
		}
	}

	private void PostAnimatingTweaks()
	{
		if (OverrideHeadForPerfectLookDirection > 0f)
		{
			CheckOverrideReference();
			Quaternion rotation = LookBones[0].Transform.rotation;
			headOv.transform.localRotation = headOv.initialLocalRotation;
			Vector3 direction = activeLookPosition - headOv.transform.position;
			Vector2 customLookAngles = headOv.GetCustomLookAngles(direction, headOv);
			headOv.transform.rotation = headOv.RotateCustomAxis(customLookAngles.x + RotationOffset.x, customLookAngles.y + RotationOffset.y, headOv) * headOv.transform.rotation;
			LookBones[0].Transform.rotation = Quaternion.Lerp(rotation, headOv.transform.rotation, OverrideHeadForPerfectLookDirection);
		}
	}

	private void ResetBones(bool onlyIfNull = false)
	{
		if (UseBoneOffsetRotation)
		{
			for (int i = 0; i < LookBones.Count; i++)
			{
				LookBones[i].animatedTargetRotation = Quaternion.identity;
				LookBones[i].targetRotation = LookBones[i].animatedTargetRotation;
				LookBones[i].finalRotation = LookBones[i].animatedTargetRotation;
			}
		}
		else
		{
			for (int j = 0; j < LookBones.Count; j++)
			{
				LookBones[j].animatedTargetRotation = LookBones[j].Transform.rotation;
				LookBones[j].targetRotation = LookBones[j].animatedTargetRotation;
				LookBones[j].finalRotation = LookBones[j].animatedTargetRotation;
			}
		}
	}

	internal void RefreshLookBones()
	{
		if (LookBones == null)
		{
			LookBones = new List<LookBone>();
			LookBones.Add(new LookBone(null));
		}
		if (LookBones.Count == 0)
		{
			LookBones.Add(new LookBone(null));
		}
		if (LookBones.Count > BackBonesCount + 1)
		{
			LookBones.RemoveRange(BackBonesCount + 1, LookBones.Count - (BackBonesCount + 1));
		}
		if ((bool)LeadBone)
		{
			if (LookBones[0].Transform != LeadBone)
			{
				LookBones[0] = new LookBone(LeadBone);
				if ((bool)BaseTransform)
				{
					LookBones[0].RefreshBoneDirections(BaseTransform);
				}
			}
			for (int i = 1; i < 1 + BackBonesCount; i++)
			{
				if (i >= LookBones.Count)
				{
					LookBone lookBone = new LookBone(LookBones[i - 1].Transform.parent);
					LookBones.Add(lookBone);
					if ((bool)BaseTransform)
					{
						lookBone.RefreshBoneDirections(BaseTransform);
					}
				}
				else if (LookBones[i] == null || LookBones[i].Transform == null)
				{
					LookBones[i] = new LookBone(LookBones[i - 1].Transform.parent);
					if ((bool)BaseTransform)
					{
						LookBones[i].RefreshBoneDirections(BaseTransform);
					}
				}
			}
		}
		else
		{
			while (LookBones.Count > 1)
			{
				LookBones.RemoveAt(LookBones.Count - 1);
			}
		}
	}

	private void RefreshReferencePose()
	{
		for (int i = 0; i < LookBones.Count; i++)
		{
			LookBones[i].RefreshStaticRotation(!MonitorAnimator);
		}
		if (MonitorAnimator)
		{
			StopCoroutine(CRefreshReferencePose());
			StartCoroutine(CRefreshReferencePose());
		}
		refreshReferencePose = false;
	}

	private IEnumerator CRefreshReferencePose()
	{
		yield return null;
		yield return CoroutineEx.waitForSecondsRealtime(0.05f);
		if (_monitorTransitionStart == null)
		{
			_monitorTransitionStart = new List<Quaternion>();
		}
		if (_monitorTransitionStart.Count != LookBones.Count)
		{
			for (int i = 0; i < LookBones.Count; i++)
			{
				_monitorTransitionStart.Add(LookBones[i].animatedStaticRotation);
			}
		}
		for (int j = 0; j < LookBones.Count; j++)
		{
			LookBones[j].RefreshStaticRotation(hard: false);
		}
		float elapsed = 0f;
		while (elapsed < monitorTransitionTime)
		{
			elapsed += delta;
			float t = FEasing.EaseInOutCubic(0f, 1f, elapsed / monitorTransitionTime);
			for (int k = 0; k < LookBones.Count; k++)
			{
				LookBones[k].animatedStaticRotation = Quaternion.Slerp(_monitorTransitionStart[k], LookBones[k].targetStaticRotation, t);
			}
			yield return null;
		}
		for (int l = 0; l < LookBones.Count; l++)
		{
			LookBones[l].animatedStaticRotation = LookBones[l].targetStaticRotation;
		}
	}

	public void InitializeBaseVariables()
	{
		_LOG_NoRefs();
		LookState = EFHeadLookState.Null;
		if (AutoBackbonesWeights)
		{
			SetAutoWeightsDefault();
		}
		ComputeBonesRotationsFixVariables();
		InitBirdMode();
		ResetBones();
		smoothLookPosition = GetForwardPosition();
		lookFreezeFocusPoint = BaseTransform.InverseTransformPoint(smoothLookPosition);
		refreshReferencePose = true;
		RefreshStartLookPoint = true;
		rootStaticRotation = BaseTransform.rotation;
		_preBackBonesCount = BackBonesCount;
		lastBaseRotation = BaseTransform.rotation;
		for (int i = 0; i < LookBones.Count; i++)
		{
			if (LookBones[i].correctionOffset == Vector3.zero)
			{
				LookBones[i].correctionOffset = Vector3.zero;
			}
			LookBones[i].lastKeyframeRotation = LookBones[i].Transform.localRotation;
			LookBones[i].RefreshBoneDirections(BaseTransform);
		}
		CheckOverrideReference();
		if (UseEyes)
		{
			InitEyesModule();
		}
		initialized = true;
	}

	public void FindBaseTransform()
	{
		BaseTransform = base.transform;
		if (!GetComponentInChildren<Animator>() && !GetComponentInChildren<Animation>())
		{
			Debug.LogWarning(base.gameObject.name + " don't have animator. '" + base.name + "' is it root transform for your character?");
		}
	}

	public Vector3 TryFindHeadPositionInTarget(Transform other)
	{
		FLookAnimator component = other.GetComponent<FLookAnimator>();
		if ((bool)component && (bool)component.LeadBone)
		{
			return component.GetLookStartMeasurePosition();
		}
		Animator componentInChildren = other.GetComponentInChildren<Animator>();
		if ((bool)componentInChildren && componentInChildren.isHuman)
		{
			if ((bool)componentInChildren.GetBoneTransform(HumanBodyBones.LeftEye))
			{
				return componentInChildren.GetBoneTransform(HumanBodyBones.LeftEye).position;
			}
			if ((bool)componentInChildren.GetBoneTransform(HumanBodyBones.Head))
			{
				return componentInChildren.GetBoneTransform(HumanBodyBones.Head).position;
			}
		}
		Renderer componentInChildren2 = other.GetComponentInChildren<Renderer>();
		if (!componentInChildren2 && other.childCount > 0)
		{
			componentInChildren2 = other.GetChild(0).GetComponentInChildren<Renderer>();
		}
		if ((bool)componentInChildren2)
		{
			return other.position + other.TransformVector(Vector3.up * (componentInChildren2.bounds.max.y * 0.9f)) + other.TransformVector(Vector3.forward * (componentInChildren2.bounds.max.z * 0.75f));
		}
		return other.position;
	}

	public void OnDrop(PointerEventData data)
	{
	}

	private float GetClampedAngle(float current, float limit, float elastic, float sign = 1f)
	{
		if (elastic <= 0f)
		{
			return limit;
		}
		float num = 0f;
		if (elastic > 0f)
		{
			num = FEasing.EaseOutCubic(0f, elastic, (current * sign - limit * sign) / (180f + limit * sign));
		}
		return limit + num * sign;
	}

	private void ComputeBonesRotationsFixVariables()
	{
		if (BaseTransform != null)
		{
			Quaternion rotation = BaseTransform.rotation;
			BaseTransform.rotation = Quaternion.identity;
			FromAuto = LeadBone.rotation * -Vector3.forward;
			float angle = Quaternion.Angle(Quaternion.identity, LeadBone.rotation);
			OffsetAuto = Quaternion.AngleAxis(angle, (LeadBone.rotation * Quaternion.Inverse(Quaternion.FromToRotation(FromAuto, ModelForwardAxis))).eulerAngles.normalized).eulerAngles;
			BaseTransform.rotation = rotation;
			RefreshParentalLookReferenceAxis();
			headForward = Quaternion.FromToRotation(LeadBone.InverseTransformDirection(BaseTransform.TransformDirection(ModelForwardAxis.normalized)), Vector3.forward) * Vector3.forward;
		}
		else
		{
			Debug.LogWarning("Base Transform isn't defined, so we can't use auto correction!");
		}
	}

	private void RefreshParentalLookReferenceAxis()
	{
		parentalReferenceLookForward = Quaternion.Inverse(LeadBone.parent.rotation) * BaseTransform.rotation * ModelForwardAxis.normalized;
		parentalReferenceUp = Quaternion.Inverse(LeadBone.parent.rotation) * BaseTransform.rotation * ModelUpAxis.normalized;
	}

	public Vector3 GetCurrentHeadForwardDirection()
	{
		return LeadBone.rotation * Quaternion.FromToRotation(headForward, Vector3.forward) * Vector3.forward;
	}

	private void _LOG_NoRefs()
	{
	}

	private void _Debug_Rays()
	{
		if (DebugRays)
		{
			Debug.DrawRay(GetLookStartMeasurePosition() + Vector3.up * 0.01f, Quaternion.Euler(finalLookAngles) * BaseTransform.TransformDirection(ModelForwardAxis), Color.cyan);
		}
	}

	private Vector3 WrapVector(Vector3 v)
	{
		return new Vector3(FLogicMethods.WrapAngle(v.x), FLogicMethods.WrapAngle(v.y), FLogicMethods.WrapAngle(v.z));
	}

	private Vector3 ConvertFlippedAxes(Vector3 rotations)
	{
		if (FixingPreset != 0)
		{
			if (FixingPreset == EFAxisFixOrder.FromBased)
			{
				rotations += OffsetAuto;
				rotations = (Quaternion.Euler(rotations) * Quaternion.FromToRotation(FromAuto, ModelForwardAxis)).eulerAngles;
			}
			else
			{
				if (FixingPreset == EFAxisFixOrder.FullManual)
				{
					rotations.x *= RotCorrectionMultiplier.x;
					rotations.y *= RotCorrectionMultiplier.y;
					rotations.z *= RotCorrectionMultiplier.z;
					return (Quaternion.Euler(rotations) * Quaternion.FromToRotation(ManualFromAxis, ManualToAxis)).eulerAngles;
				}
				if (FixingPreset == EFAxisFixOrder.ZYX)
				{
					return Quaternion.Euler(rotations.z, rotations.y - 90f, 0f - rotations.x - 90f).eulerAngles;
				}
			}
		}
		return rotations;
	}

	public static float AngleAroundAxis(Vector3 firstDirection, Vector3 secondDirection, Vector3 axis)
	{
		firstDirection -= Vector3.Project(firstDirection, axis);
		secondDirection -= Vector3.Project(secondDirection, axis);
		return Vector3.Angle(firstDirection, secondDirection) * (float)((!(Vector3.Dot(axis, Vector3.Cross(firstDirection, secondDirection)) < 0f)) ? 1 : (-1));
	}

	private void Reset()
	{
		FindBaseTransform();
	}

	private void Awake()
	{
		_LOG_NoRefs();
	}

	protected virtual void Start()
	{
		initialized = false;
		if (!StartAfterTPose)
		{
			InitializeBaseVariables();
		}
		else
		{
			startAfterTPoseCounter = 0;
		}
	}

	private void OnDisable()
	{
		wasUpdating = false;
		animatePhysicsWorking = false;
	}

	public void ResetLook()
	{
		ResetBones();
		finalMotionWeight = 0f;
		_velo_animatedMotionWeight = 0f;
		animatedMotionWeight = 0f;
	}

	private void Update()
	{
		using (TimeWarning.New("FLookAnimator:Update"))
		{
			if (!initialized)
			{
				if (StartAfterTPose)
				{
					startAfterTPoseCounter++;
					if (startAfterTPoseCounter > 6)
					{
						InitializeBaseVariables();
					}
				}
				updateLookAnimator = false;
				return;
			}
			if (OptimizeWithMesh != null && !OptimizeWithMesh.isVisible)
			{
				updateLookAnimator = false;
				wasUpdating = false;
				return;
			}
			if (!wasUpdating)
			{
				ResetLook();
				wasUpdating = true;
			}
			if (AnimatePhysics)
			{
				if (!animatePhysicsWorking)
				{
					StartCoroutine(AnimatePhysicsClock());
				}
				if (!triggerAnimatePhysics)
				{
					updateLookAnimator = false;
					return;
				}
				triggerAnimatePhysics = false;
			}
			if (finalMotionWeight < 0.01f)
			{
				animatedLookAngles = Vector3.zero;
				if (LookAnimatorAmount <= 0f)
				{
					updateLookAnimator = false;
					return;
				}
			}
			UpdateCorrectionMatrix();
			updateLookAnimator = true;
			if (!AnimatePhysics)
			{
				PreCalibrateBones();
			}
		}
	}

	private void FixedUpdate()
	{
		using (TimeWarning.New("FLookAnimator:FixedUpdate"))
		{
			if (updateLookAnimator && AnimatePhysics)
			{
				PreCalibrateBones();
			}
		}
	}

	public virtual void LateUpdate()
	{
		using (TimeWarning.New("FLookAnimator:LateUpdate"))
		{
			if (updateLookAnimator)
			{
				CalibrateBones();
				TargetingUpdate();
				BeginStateCheck();
				UpdateSmoothing();
				MaxDistanceCalculations();
				NoddingCalculations();
				CalculateLookAnimation();
				UpdateLookAnimatorAmountWeight();
				ChangeBonesRotations();
				_Debug_Rays();
				if (BirdMode)
				{
					CalculateBirdMode();
				}
				if (UseEyes)
				{
					UpdateEyesLogics();
				}
				EndUpdate();
				PostAnimatingTweaks();
			}
		}
	}
}
