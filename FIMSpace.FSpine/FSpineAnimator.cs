using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FIMSpace.FSpine;

[AddComponentMenu("FImpossible Creations/Spine Animator 2")]
[DefaultExecutionOrder(-11)]
public class FSpineAnimator : MonoBehaviour, IDropHandler, IEventSystemHandler, IFHierarchyIcon, IClientComponent
{
	public enum EFSpineEditorCategory
	{
		Setup,
		Tweak,
		Adjust,
		Physical
	}

	public enum EFDeltaType
	{
		DeltaTime,
		SmoothDeltaTime,
		UnscaledDeltaTime,
		FixedDeltaTime,
		SafeDelta
	}

	public enum EParamChange
	{
		GoBackSpeed,
		SpineAnimatorAmount,
		AngleLimit,
		StraightenSpeed,
		PositionSmoother,
		RotationSmoother
	}

	public class HeadBone
	{
		public Transform baseTransform;

		public Transform transform;

		private Vector3 snapshotPoseBaseTrSpacePosition;

		public Vector3 SnapshotPosition;

		private Quaternion snapshotPoseBaseTrSpaceRotationF;

		private Quaternion snapshotPoseBaseTrSpaceRotationB;

		public Quaternion snapshotPoseLocalRotation;

		public Quaternion SnapshotRotation;

		public Vector3 InitialLocalPosition { get; private set; }

		public Quaternion InitialLocalRotation { get; private set; }

		public HeadBone(Transform t)
		{
			transform = t;
		}

		public void PrepareBone(Transform baseTransform, List<SpineBone> bones, int index)
		{
			TakePoseSnapshot(baseTransform, bones, index);
			InitialLocalPosition = transform.localPosition;
			InitialLocalRotation = transform.localRotation;
		}

		internal Quaternion GetLocalRotationDiff()
		{
			return transform.rotation * Quaternion.Inverse(snapshotPoseLocalRotation);
		}

		public void SetCoordsForFrameForward()
		{
			SnapshotPosition = baseTransform.TransformPoint(snapshotPoseBaseTrSpacePosition);
			SnapshotRotation = baseTransform.rotation * snapshotPoseBaseTrSpaceRotationF;
		}

		public void SetCoordsForFrameBackward()
		{
			SnapshotPosition = baseTransform.TransformPoint(snapshotPoseBaseTrSpacePosition);
			SnapshotRotation = baseTransform.rotation * snapshotPoseBaseTrSpaceRotationB;
		}

		public void TakePoseSnapshot(Transform targetSpace, List<SpineBone> bones, int index)
		{
			baseTransform = targetSpace;
			snapshotPoseBaseTrSpacePosition = targetSpace.InverseTransformPoint(transform.position);
			Vector3 vector2;
			Vector3 vector3;
			if (index == bones.Count - 1)
			{
				Vector3 vector = targetSpace.InverseTransformPoint(transform.position) - targetSpace.InverseTransformPoint(bones[index - 1].transform.position);
				vector2 = snapshotPoseBaseTrSpacePosition + vector;
				vector3 = targetSpace.InverseTransformPoint(bones[index - 1].transform.position);
			}
			else if (index == 0)
			{
				vector2 = targetSpace.InverseTransformPoint(bones[index + 1].transform.position);
				Vector3 vector4 = targetSpace.InverseTransformPoint(transform.position) - targetSpace.InverseTransformPoint(bones[index + 1].transform.position);
				vector3 = snapshotPoseBaseTrSpacePosition + vector4;
			}
			else
			{
				vector2 = targetSpace.InverseTransformPoint(bones[index + 1].transform.position);
				vector3 = targetSpace.InverseTransformPoint(bones[index - 1].transform.position);
			}
			snapshotPoseBaseTrSpaceRotationF = Quaternion.Inverse(targetSpace.rotation) * Quaternion.LookRotation(vector2 - snapshotPoseBaseTrSpacePosition);
			snapshotPoseBaseTrSpaceRotationB = Quaternion.Inverse(targetSpace.rotation) * Quaternion.LookRotation(vector3 - snapshotPoseBaseTrSpacePosition);
			snapshotPoseLocalRotation = Quaternion.Inverse(targetSpace.rotation) * transform.rotation;
		}
	}

	[Serializable]
	public class SpineBone
	{
		public Transform transform;

		public Vector3 ProceduralPosition;

		public Quaternion ProceduralRotation;

		public Vector3 HelperDiffPosition;

		public Quaternion HelperDiffRoation;

		public Vector3 PreviousPosition;

		public Vector3 DefaultForward;

		public float StraightenFactor;

		public float TargetStraightenFactor;

		private float boneLengthB = 0.1f;

		private float boneLengthF = 0.1f;

		private Vector3 boneLocalOffsetB;

		private Vector3 boneLocalOffsetF;

		public float MotionWeight = 1f;

		public Quaternion FinalRotation;

		public Vector3 FinalPosition;

		public Vector3 ManualPosOffset;

		public Quaternion ManualRotOffset;

		public Vector3 ReferencePosition;

		public Vector3 PreviousReferencePosition;

		public Quaternion ReferenceRotation;

		private Quaternion lastKeyframeRotation;

		private Vector3 lastKeyframePosition;

		private Vector3 lastFinalLocalPosition;

		private Quaternion lastFinalLocalRotation;

		public Vector3 forward;

		public Vector3 right;

		public Vector3 up;

		public bool Collide = true;

		public float CollisionRadius = 1f;

		public Vector3 ColliderOffset = Vector3.zero;

		public float BoneLength { get; private set; }

		public Vector3 BoneLocalOffset { get; private set; }

		public Vector3 InitialLocalPosition { get; private set; }

		public Quaternion InitialLocalRotation { get; private set; }

		public void UpdateReferencePosition(Vector3 pos)
		{
			PreviousReferencePosition = ReferencePosition;
			ReferencePosition = pos;
		}

		public void ZeroKeyframeCheck()
		{
			if (lastFinalLocalRotation.QIsSame(transform.localRotation))
			{
				transform.localRotation = lastKeyframeRotation;
			}
			else
			{
				lastKeyframeRotation = transform.localRotation;
			}
			if (lastFinalLocalPosition.VIsSame(transform.localPosition))
			{
				transform.localPosition = lastKeyframePosition;
			}
			else
			{
				lastKeyframePosition = transform.localPosition;
			}
		}

		public void RefreshFinalLocalPose()
		{
			lastFinalLocalPosition = transform.localPosition;
			lastFinalLocalRotation = transform.localRotation;
		}

		public SpineBone(Transform t)
		{
			transform = t;
			ManualPosOffset = Vector3.zero;
			ColliderOffset = Vector3.zero;
			Collide = true;
			CollisionRadius = 1f;
		}

		public void PrepareBone(Transform baseTransform, List<SpineBone> bones, int index)
		{
			InitialLocalPosition = transform.localPosition;
			InitialLocalRotation = transform.localRotation;
			Vector3 vector = ((index != bones.Count - 1) ? bones[index + 1].transform.position : ((bones[index].transform.childCount <= 0) ? bones[index - 1].transform.position : bones[index].transform.GetChild(0).position));
			if (index == 0)
			{
				vector = bones[index + 1].transform.position;
			}
			if (Vector3.Distance(baseTransform.InverseTransformPoint(vector), baseTransform.InverseTransformPoint(bones[index].transform.position)) < 0.01f)
			{
				int num = index + 2;
				if (num < bones.Count)
				{
					DefaultForward = transform.InverseTransformPoint(bones[num].transform.position);
				}
				else
				{
					DefaultForward = transform.InverseTransformPoint(vector - baseTransform.position);
				}
			}
			else
			{
				DefaultForward = transform.InverseTransformPoint(vector);
			}
			boneLengthB = (baseTransform.InverseTransformPoint(transform.position) - baseTransform.InverseTransformPoint(vector)).magnitude;
			boneLocalOffsetB = baseTransform.InverseTransformPoint(vector);
			boneLengthF = (baseTransform.InverseTransformPoint(transform.position) - baseTransform.InverseTransformPoint(vector)).magnitude;
			boneLocalOffsetF = baseTransform.InverseTransformPoint(vector);
			if (ManualPosOffset.sqrMagnitude == 0f)
			{
				ManualPosOffset = Vector3.zero;
			}
			if (ManualRotOffset.eulerAngles.sqrMagnitude == 0f)
			{
				ManualRotOffset = Quaternion.identity;
			}
			SetDistanceForFrameForward();
			PrepareAxes(baseTransform, bones, index);
		}

		public void SetDistanceForFrameForward()
		{
			BoneLength = boneLengthF;
			BoneLocalOffset = boneLocalOffsetF;
		}

		public void SetDistanceForFrameBackward()
		{
			BoneLength = boneLengthB;
			BoneLocalOffset = boneLocalOffsetB;
		}

		public float GetUnscalledBoneLength()
		{
			if (boneLengthF > boneLengthB)
			{
				return boneLengthF;
			}
			return boneLengthB;
		}

		private void PrepareAxes(Transform baseTransform, List<SpineBone> bonesList, int index)
		{
			Transform transform;
			Vector3 position;
			Vector3 position2;
			if (index == bonesList.Count - 1)
			{
				if (this.transform.childCount == 1)
				{
					transform = this.transform;
					Transform child = this.transform.GetChild(0);
					position = transform.position;
					position2 = child.position;
				}
				else
				{
					transform = this.transform;
					_ = this.transform;
					position = bonesList[index - 1].transform.position;
					position2 = this.transform.position;
				}
			}
			else
			{
				transform = this.transform;
				Transform obj = bonesList[index + 1].transform;
				position = transform.position;
				position2 = obj.position;
			}
			Vector3 direction = transform.InverseTransformDirection(position2) - transform.InverseTransformDirection(position);
			Vector3 normalized = Vector3.ProjectOnPlane(baseTransform.up, this.transform.TransformDirection(direction).normalized).normalized;
			Vector3 direction2 = transform.InverseTransformDirection(position + normalized) - transform.InverseTransformDirection(position);
			Vector3 vector = Vector3.Cross(this.transform.TransformDirection(direction), this.transform.TransformDirection(direction2));
			right = (transform.InverseTransformDirection(position + vector) - transform.InverseTransformDirection(position)).normalized;
			up = direction2.normalized;
			forward = direction.normalized;
		}

		internal void CalculateDifferencePose(Vector3 upAxis, Vector3 rightAxis)
		{
			HelperDiffPosition = ProceduralPosition - ReferencePosition;
			Quaternion quaternion = ProceduralRotation * Quaternion.FromToRotation(up, upAxis) * Quaternion.FromToRotation(right, rightAxis);
			Quaternion rotation = ReferenceRotation * Quaternion.FromToRotation(up, upAxis) * Quaternion.FromToRotation(right, rightAxis);
			HelperDiffRoation = quaternion * Quaternion.Inverse(rotation);
		}

		internal void ApplyDifferencePose()
		{
			FinalPosition = transform.position + HelperDiffPosition;
			FinalRotation = HelperDiffRoation * transform.rotation;
		}

		public void Editor_SetLength(float length)
		{
			if (!Application.isPlaying)
			{
				BoneLength = length;
			}
		}

		public float GetCollisionRadiusScaled()
		{
			return CollisionRadius * transform.lossyScale.x;
		}
	}

	public enum EFixedMode
	{
		None,
		Basic,
		Late
	}

	private bool collisionInitialized;

	private bool forceRefreshCollidersData;

	[FPD_Percentage(0f, 1f, false, true, "%", false)]
	[Tooltip("You can use this variable to blend intensity of spine animator motion over skeleton animation\n\nValue = 1: Animation with spine Animator motion\nValue = 0: Only skeleton animation")]
	public float SpineAnimatorAmount = 1f;

	private Quaternion Rotate180 = Quaternion.Euler(0f, 180f, 0f);

	private int initAfterTPoseCounter;

	private bool fixedUpdated;

	private bool lateFixedIsRunning;

	private bool fixedAllow = true;

	private bool chainReverseFlag;

	public EFSpineEditorCategory _Editor_Category;

	public bool _Editor_PivotoffsetXYZ;

	private bool _editor_isQuitting;

	private int leadingBoneIndex;

	private int chainIndexDirection = 1;

	private int chainIndexOffset = 1;

	protected float delta = 0.016f;

	protected float unifiedDelta = 0.016f;

	protected float elapsedDeltaHelper;

	protected int updateLoops = 1;

	private bool initialized;

	private Vector3 previousPos;

	private bool wasBlendedOut;

	private List<FSpineBoneConnector> connectors;

	private float referenceDistance = 0.1f;

	public Vector3 ModelForwardAxis = Vector3.forward;

	public Vector3 ModelForwardAxisScaled = Vector3.forward;

	public Vector3 ModelUpAxis = Vector3.up;

	public Vector3 ModelUpAxisScaled = Vector3.up;

	internal Vector3 ModelRightAxis = Vector3.right;

	internal Vector3 ModelRightAxisScaled = Vector3.right;

	public List<SpineBone> SpineBones;

	public List<Transform> SpineTransforms;

	private HeadBone frontHead;

	private HeadBone backHead;

	private HeadBone headBone;

	[Tooltip("Main character object - by default it is game object to which Spine Animator is attached.\n\nYou can use it to control spine of character from different game object.")]
	public Transform BaseTransform;

	public Transform ForwardReference;

	[Tooltip("If your spine lead bone is in beggining of your hierarchy chain then toggle it.\n\nComponent's gizmos can help you out to define which bone should be leading (check head gizmo when you switch this toggle).")]
	public bool LastBoneLeading = true;

	[Tooltip("Sometimes spine chain can face in different direction than desired or you want your characters to move backward with spine motion.")]
	public bool ReverseForward;

	[Tooltip("If you're using 'Animate Physics' on animator you should set this variable to be enabled.")]
	public EFixedMode AnimatePhysics;

	public Transform AnchorRoot;

	[Tooltip("Connecting lead bone position to given transform, useful when it is tail and you already animating spine with other Spine Animator component.")]
	public Transform HeadAnchor;

	[Tooltip("Letting head anchor to animate rotation")]
	public bool AnimateAnchor = true;

	[Tooltip("If you need to offset leading bone rotation.")]
	public Vector3 LeadBoneRotationOffset = Vector3.zero;

	[Tooltip("If Lead Bone Rotation Offset should affect reference pose or bone rotation")]
	public bool LeadBoneOffsetReference = true;

	[Tooltip("List of bone positioning/rotation fixers if using paws positioning with IK controlls disconnected out of arms/legs in the hierarchy")]
	public List<SpineAnimator_FixIKControlledBones> BonesFixers = new List<SpineAnimator_FixIKControlledBones>();

	[Tooltip("Useful when you use few spine animators and want to rely on animated position and rotation by other spine animator.")]
	public bool UpdateAsLast;

	public bool QueueToLastUpdate;

	[Tooltip("If corrections should affect spine chain children.")]
	public bool ManualAffectChain;

	[Tooltip("Often when you drop model to scene, it's initial pose is much different than animations, which causes problems, this toggle solves it at start.")]
	public bool StartAfterTPose = true;

	[Tooltip("If you want spine animator to stop computing when choosed mesh is not visible in any camera view (editor's scene camera is detecting it too)")]
	public Renderer OptimizeWithMesh;

	[Tooltip("Delta Time for Spine Animator calculations")]
	public EFDeltaType DeltaType = EFDeltaType.SafeDelta;

	[Tooltip("Making update rate stable for target rate.\nIf this value is = 0 then update rate is unlimited.")]
	public float UpdateRate;

	[Tooltip("In some cases you need to use chain corrections, it will cost a bit more in performance, not much but always.")]
	public bool UseCorrections;

	[Tooltip("Sometimes offsetting model's pivot position gives better results using spine animator, offset forward axis so front legs are in centrum and see the difference (generating additional transform inside hierarchy)")]
	public Vector3 MainPivotOffset = new Vector3(0f, 0f, 0f);

	[Tooltip("Generating offset runtime only, allows you to adjust it on prefabs on scene")]
	public bool PivotOffsetOnStart = true;

	[Range(0f, 1f)]
	[Tooltip("If animation of changing segments position should be smoothed - creating a little gumy effect.")]
	public float PosSmoother;

	[Range(0f, 1f)]
	[Tooltip("If animation of changing segments rotation should be smoothed - making it more soft, but don't overuse it!")]
	public float RotSmoother;

	[Range(0f, 1f)]
	[Tooltip("We stretching segments to bigger value than bones are by default to create some extra effect which looks good but sometimes it can stretch to much if you using position smoothing, you can adjust it here.")]
	public float MaxStretching = 1f;

	[Tooltip("Making algorithm referencing back to static rotation if value = 0f | at 1 motion have more range and is more slithery.")]
	[Range(0f, 1f)]
	public float Slithery = 1f;

	[Range(1f, 91f)]
	[Tooltip("Limiting rotation angle difference between each segment of spine.")]
	public float AngleLimit = 40f;

	[Range(0f, 1f)]
	[Tooltip("Smoothing how fast limiting should make segments go back to marginal pose.")]
	public float LimitSmoother = 0.35f;

	[Range(0f, 15f)]
	[Tooltip("How fast spine should be rotated to straight pose when your character moves.")]
	public float StraightenSpeed = 7.5f;

	public bool TurboStraighten;

	[Tooltip("Spine going back to straight position constantly with choosed speed intensity.")]
	[Range(0f, 1f)]
	public float GoBackSpeed;

	[Tooltip("Elastic spring effect good for tails to make them more 'meaty'.")]
	[Range(0f, 1f)]
	public float Springiness;

	[Tooltip("How much effect on spine chain should have character movement.")]
	[Range(0f, 1f)]
	public float MotionInfluence = 1f;

	[Tooltip("Useful when your creature jumps on moving platform, so when platform moves spine is not reacting, by default world space is used (null).")]
	public Transform MotionSpace;

	[Tooltip("Fade rotations to sides or rotation up/down with this parameter - can be helpful for character jump handling")]
	public Vector2 RotationsFade = Vector2.one;

	[SerializeField]
	[HideInInspector]
	private Transform mainPivotOffsetTransform;

	[Tooltip("<! Most models can not need this !> Offset for bones rotations, thanks to that animation is able to rotate to segments in a correct way, like from center of mass.")]
	public Vector3 SegmentsPivotOffset = new Vector3(0f, 0f, 0f);

	[Tooltip("Multiplies distance value between bones segments - can be useful for use with humanoid skeletons")]
	public float DistancesMultiplier = 1f;

	[Tooltip("Pushing segments in world direction (should have included ground collider to collide with).")]
	public Vector3 GravityPower = Vector3.zero;

	protected Vector3 gravityScale = Vector3.zero;

	[Tooltip("[Experimental] Using some simple calculations to make spine bend on colliders.")]
	public bool UseCollisions;

	public List<Collider> IncludedColliders;

	protected List<FImp_ColliderData_Base> IncludedCollidersData;

	protected List<FImp_ColliderData_Base> CollidersDataToCheck;

	[Tooltip("If disabled Colliders can be offsetted a bit in wrong way - check pink spheres in scene view (playmode, with true positions disabled colliders are fitting to stiff reference pose) - but it gives more stable collision projection! But to avoid stuttery you can increase position smoothing.")]
	public bool UseTruePosition;

	public Vector3 OffsetAllColliders = Vector3.zero;

	public AnimationCurve CollidersScale = AnimationCurve.Linear(0f, 1f, 1f, 1f);

	public float CollidersScaleMul = 6.5f;

	[Range(0f, 1f)]
	public float DifferenceScaleFactor;

	[Tooltip("If you want to continue checking collision if segment collides with one collider (very useful for example when you using gravity power with ground)")]
	public bool DetailedCollision = true;

	[SerializeField]
	[HideInInspector]
	private bool _CheckedPivot;

	private bool updateSpineAnimator;

	private bool callSpineReposeCalculations = true;

	public string EditorIconPath
	{
		get
		{
			if (PlayerPrefs.GetInt("AnimsH", 1) == 0)
			{
				return "";
			}
			return "Spine Animator/SpineAnimator_SmallIcon";
		}
	}

	[Obsolete("Use SpineAnimatorAmount instead, but remember that it works in reversed way -> SpineAnimatorAmount 1 = BlendToOriginal 0  and  SpineAnimatorAmount 0 = BlendToOriginal 1")]
	public float BlendToOriginal
	{
		get
		{
			return 1f - SpineAnimatorAmount;
		}
		set
		{
			SpineAnimatorAmount = 1f - value;
		}
	}

	public bool EndBoneIsHead
	{
		get
		{
			return LastBoneLeading;
		}
		set
		{
			LastBoneLeading = EndBoneIsHead;
		}
	}

	private void RemovePivotOffset()
	{
		if (!Application.isPlaying && (bool)mainPivotOffsetTransform)
		{
			RestoreBasePivotChildren();
		}
	}

	public void UpdatePivotOffsetState()
	{
		if (SpineBones.Count <= 1)
		{
			return;
		}
		if (MainPivotOffset == Vector3.zero)
		{
			if ((bool)mainPivotOffsetTransform && mainPivotOffsetTransform.childCount > 0)
			{
				mainPivotOffsetTransform.localPosition = MainPivotOffset;
				RestoreBasePivotChildren();
			}
			return;
		}
		if (!mainPivotOffsetTransform)
		{
			mainPivotOffsetTransform = new GameObject("Main Pivot Offset-Spine Animator-" + base.name).transform;
			mainPivotOffsetTransform.SetParent(GetBaseTransform(), worldPositionStays: false);
			mainPivotOffsetTransform.localPosition = Vector3.zero;
			mainPivotOffsetTransform.localRotation = Quaternion.identity;
			mainPivotOffsetTransform.localScale = Vector3.one;
		}
		if (mainPivotOffsetTransform.childCount == 0)
		{
			for (int num = GetBaseTransform().childCount - 1; num >= 0; num--)
			{
				if (!(GetBaseTransform().GetChild(num) == mainPivotOffsetTransform))
				{
					GetBaseTransform().GetChild(num).SetParent(mainPivotOffsetTransform, worldPositionStays: true);
				}
			}
		}
		mainPivotOffsetTransform.localPosition = MainPivotOffset;
	}

	private void RestoreBasePivotChildren()
	{
		if (!_editor_isQuitting)
		{
			for (int num = mainPivotOffsetTransform.childCount - 1; num >= 0; num--)
			{
				mainPivotOffsetTransform.GetChild(num).SetParent(mainPivotOffsetTransform.parent, worldPositionStays: true);
			}
		}
	}

	private void PreMotionBoneOffsets()
	{
		if (UseCorrections && ManualAffectChain && callSpineReposeCalculations)
		{
			PreMotionNoHead();
			PreMotionHead();
		}
	}

	private void PreMotionNoHead()
	{
		if (SegmentsPivotOffset.sqrMagnitude != 0f)
		{
			for (int i = 1 - chainIndexOffset; i < SpineBones.Count - chainIndexOffset; i++)
			{
				SegmentPreOffsetWithPivot(i);
			}
		}
		else
		{
			for (int j = 1 - chainIndexOffset; j < SpineBones.Count - chainIndexOffset; j++)
			{
				SegmentPreOffset(j);
			}
		}
	}

	private void PreMotionHead()
	{
		if (SegmentsPivotOffset.sqrMagnitude != 0f)
		{
			SegmentPreOffsetWithPivot(leadingBoneIndex);
		}
		else
		{
			SegmentPreOffset(leadingBoneIndex);
		}
	}

	private void SegmentPreOffset(int i)
	{
		if (SpineBones[i].ManualPosOffset.sqrMagnitude != 0f)
		{
			SpineBones[i].transform.position += SpineBones[i].ProceduralRotation * SpineBones[i].ManualPosOffset;
		}
		SpineBones[i].transform.rotation *= SpineBones[i].ManualRotOffset;
	}

	private void SegmentPreOffsetWithPivot(int i)
	{
		if (SpineBones[i].ManualPosOffset.sqrMagnitude != 0f)
		{
			SpineBones[i].transform.position += SpineBones[i].ProceduralRotation * SpineBones[i].ManualPosOffset;
		}
		SpineBones[i].transform.position += SpineBones[i].ProceduralRotation * (SegmentsPivotOffset * (SpineBones[i].BoneLength * DistancesMultiplier * BaseTransform.lossyScale.z));
		SpineBones[i].transform.rotation *= SpineBones[i].ManualRotOffset;
	}

	private void PostMotionBoneOffsets()
	{
		if (UseCorrections && !ManualAffectChain)
		{
			PostMotionHead();
			PostMotionNoHead();
		}
	}

	private void PostMotionNoHead()
	{
		if (SegmentsPivotOffset.sqrMagnitude != 0f)
		{
			for (int i = 1 - chainIndexOffset; i < SpineBones.Count - chainIndexOffset; i++)
			{
				SegmentPostOffsetWithPivot(i);
			}
		}
		else
		{
			for (int j = 1 - chainIndexOffset; j < SpineBones.Count - chainIndexOffset; j++)
			{
				SegmentPostOffset(j);
			}
		}
	}

	private void PostMotionHead()
	{
		if (SegmentsPivotOffset.sqrMagnitude != 0f)
		{
			SegmentPostOffsetWithPivot(leadingBoneIndex);
		}
		else
		{
			SegmentPostOffset(leadingBoneIndex);
		}
	}

	private void SegmentPostOffset(int i)
	{
		if (SpineBones[i].ManualPosOffset.sqrMagnitude != 0f)
		{
			SpineBones[i].FinalPosition += SpineBones[i].ProceduralRotation * SpineBones[i].ManualPosOffset;
		}
		SpineBones[i].FinalRotation *= SpineBones[i].ManualRotOffset;
	}

	private void SegmentPostOffsetWithPivot(int i)
	{
		if (SpineBones[i].ManualPosOffset.sqrMagnitude != 0f)
		{
			SpineBones[i].FinalPosition += SpineBones[i].ProceduralRotation * SpineBones[i].ManualPosOffset;
		}
		SpineBones[i].FinalPosition += SpineBones[i].ProceduralRotation * (SegmentsPivotOffset * (SpineBones[i].BoneLength * DistancesMultiplier * BaseTransform.lossyScale.z));
		SpineBones[i].FinalRotation *= SpineBones[i].ManualRotOffset;
	}

	private void BeginPhysicsUpdate()
	{
		gravityScale = GravityPower * delta;
		if (!UseCollisions)
		{
			return;
		}
		if (!collisionInitialized)
		{
			InitColliders();
		}
		else
		{
			RefreshCollidersDataList();
		}
		CollidersDataToCheck.Clear();
		for (int i = 0; i < IncludedCollidersData.Count; i++)
		{
			if (IncludedCollidersData[i].Collider == null)
			{
				forceRefreshCollidersData = true;
				break;
			}
			if (IncludedCollidersData[i].Collider.gameObject.activeInHierarchy)
			{
				IncludedCollidersData[i].RefreshColliderData();
				CollidersDataToCheck.Add(IncludedCollidersData[i]);
			}
		}
	}

	public void RefreshCollidersDataList()
	{
		if (IncludedColliders.Count == IncludedCollidersData.Count && !forceRefreshCollidersData)
		{
			return;
		}
		IncludedCollidersData.Clear();
		for (int num = IncludedColliders.Count - 1; num >= 0; num--)
		{
			if (IncludedColliders[num] == null)
			{
				IncludedColliders.RemoveAt(num);
			}
			else
			{
				FImp_ColliderData_Base colliderDataFor = FImp_ColliderData_Base.GetColliderDataFor(IncludedColliders[num]);
				IncludedCollidersData.Add(colliderDataFor);
			}
		}
		forceRefreshCollidersData = false;
	}

	private float GetColliderSphereRadiusFor(int i)
	{
		int index = i - 1;
		if (LastBoneLeading)
		{
			if (i == SpineBones.Count - 1)
			{
				return 0f;
			}
			index = i + 1;
		}
		else if (i == 0)
		{
			return 0f;
		}
		float a = 1f;
		if (SpineBones.Count > 1)
		{
			a = Vector3.Distance(SpineBones[1].transform.position, SpineBones[0].transform.position);
		}
		float num = Mathf.Lerp(a, (SpineBones[i].transform.position - SpineBones[index].transform.position).magnitude * 0.5f, DifferenceScaleFactor);
		float num2 = SpineBones.Count - 1;
		if (num2 <= 0f)
		{
			num2 = 1f;
		}
		float num3 = 1f / num2;
		return 0.5f * num * CollidersScaleMul * CollidersScale.Evaluate(num3 * (float)i);
	}

	public void AddCollider(Collider collider)
	{
		if (!IncludedColliders.Contains(collider))
		{
			IncludedColliders.Add(collider);
		}
	}

	private void InitColliders()
	{
		for (int i = 0; i < SpineBones.Count; i++)
		{
			SpineBones[i].CollisionRadius = GetColliderSphereRadiusFor(i);
		}
		IncludedCollidersData = new List<FImp_ColliderData_Base>();
		RefreshCollidersDataList();
		collisionInitialized = true;
	}

	public void CheckForColliderDuplicates()
	{
		for (int i = 0; i < IncludedColliders.Count; i++)
		{
			Collider col = IncludedColliders[i];
			if (IncludedColliders.Count((Collider o) => o == col) > 1)
			{
				IncludedColliders.RemoveAll((Collider o) => o == col);
				IncludedColliders.Add(col);
			}
		}
	}

	public void PushIfSegmentInsideCollider(SpineBone bone, ref Vector3 targetPoint)
	{
		Vector3 pointOffset;
		if (UseTruePosition)
		{
			Vector3 vector = targetPoint;
			pointOffset = bone.FinalPosition - vector + bone.transform.TransformVector(bone.ColliderOffset + OffsetAllColliders);
		}
		else
		{
			pointOffset = bone.transform.TransformVector(bone.ColliderOffset + OffsetAllColliders);
		}
		if (!DetailedCollision)
		{
			for (int i = 0; i < CollidersDataToCheck.Count && !CollidersDataToCheck[i].PushIfInside(ref targetPoint, bone.GetCollisionRadiusScaled(), pointOffset); i++)
			{
			}
			return;
		}
		for (int j = 0; j < CollidersDataToCheck.Count; j++)
		{
			CollidersDataToCheck[j].PushIfInside(ref targetPoint, bone.GetCollisionRadiusScaled(), pointOffset);
		}
	}

	private void CalculateBonesCoordinates()
	{
		if (LastBoneLeading)
		{
			for (int num = SpineBones.Count - 2; num >= 0; num--)
			{
				CalculateTargetBoneRotation(num);
				CalculateTargetBonePosition(num);
				SpineBones[num].CalculateDifferencePose(ModelUpAxis, ModelRightAxis);
				SpineBones[num].ApplyDifferencePose();
			}
		}
		else
		{
			for (int i = 1; i < SpineBones.Count; i++)
			{
				CalculateTargetBoneRotation(i);
				CalculateTargetBonePosition(i);
				SpineBones[i].CalculateDifferencePose(ModelUpAxis, ModelRightAxis);
				SpineBones[i].ApplyDifferencePose();
			}
		}
	}

	private void CalculateTargetBonePosition(int index)
	{
		SpineBone spineBone = SpineBones[index - chainIndexDirection];
		SpineBone spineBone2 = SpineBones[index];
		Vector3 targetPoint = spineBone.ProceduralPosition - spineBone2.ProceduralRotation * ModelForwardAxisScaled * (spineBone2.BoneLength * DistancesMultiplier);
		if (spineBone2.Collide)
		{
			targetPoint += gravityScale;
		}
		if (Springiness > 0f && !LastBoneLeading)
		{
			Vector3 vector = spineBone2.ProceduralPosition - spineBone2.PreviousPosition;
			Vector3 vector2 = spineBone2.ProceduralPosition;
			spineBone2.PreviousPosition = spineBone2.ProceduralPosition;
			vector2 += vector * (1f - Mathf.Lerp(0.05f, 0.25f, Springiness));
			float magnitude = (spineBone.ProceduralPosition - vector2).magnitude;
			Matrix4x4 localToWorldMatrix = spineBone.transform.localToWorldMatrix;
			localToWorldMatrix.SetColumn(3, spineBone.ProceduralPosition);
			Vector3 vector3 = localToWorldMatrix.MultiplyPoint3x4(spineBone2.transform.localPosition);
			Vector3 vector4 = vector3 - vector2;
			vector2 += vector4 * Mathf.Lerp(0.05f, 0.2f, Springiness);
			vector4 = vector3 - vector2;
			float magnitude2 = vector4.magnitude;
			float num = magnitude * (1f - Mathf.Lerp(0f, 0.2f, Springiness)) * 2f;
			if (magnitude2 > num)
			{
				vector2 += vector4 * ((magnitude2 - num) / magnitude2);
			}
			if (MaxStretching < 1f)
			{
				float num2 = Vector3.Distance(spineBone2.ProceduralPosition, vector2);
				if (num2 > 0f)
				{
					float num3 = spineBone2.BoneLength * 4f * MaxStretching;
					if (num2 > num3)
					{
						vector2 = Vector3.Lerp(vector2, targetPoint, Mathf.InverseLerp(num2, 0f, num3));
					}
				}
			}
			targetPoint = Vector3.Lerp(targetPoint, vector2, Mathf.Lerp(0.3f, 0.9f, Springiness));
		}
		if (PosSmoother > 0f && MaxStretching < 1f)
		{
			float num4 = Vector3.Distance(spineBone2.ProceduralPosition, targetPoint);
			if (num4 > 0f)
			{
				float num5 = spineBone2.BoneLength * 4f * MaxStretching;
				if (num4 > num5)
				{
					spineBone2.ProceduralPosition = Vector3.Lerp(spineBone2.ProceduralPosition, targetPoint, Mathf.InverseLerp(num4, 0f, num5));
				}
			}
		}
		if (UseCollisions && spineBone2.Collide)
		{
			PushIfSegmentInsideCollider(spineBone2, ref targetPoint);
		}
		if (PosSmoother == 0f)
		{
			spineBone2.ProceduralPosition = targetPoint;
		}
		else
		{
			spineBone2.ProceduralPosition = Vector3.LerpUnclamped(spineBone2.ProceduralPosition, targetPoint, Mathf.LerpUnclamped(1f, unifiedDelta, PosSmoother));
		}
	}

	private void CalculateTargetBoneRotation(int index)
	{
		SpineBone spineBone = SpineBones[index - chainIndexDirection];
		SpineBone spineBone2 = SpineBones[index];
		Quaternion b = ((Slithery >= 1f) ? spineBone.ProceduralRotation : ((!(Slithery > 0f)) ? spineBone2.ReferenceRotation : Quaternion.LerpUnclamped(spineBone2.ReferenceRotation, spineBone.ProceduralRotation, Slithery)));
		Vector3 vector = spineBone.ProceduralPosition - spineBone2.ProceduralPosition;
		if (vector == Vector3.zero)
		{
			vector = spineBone2.transform.rotation * spineBone2.DefaultForward;
		}
		if (RotationsFade != Vector2.one)
		{
			vector.x *= RotationsFade.x;
			vector.z *= RotationsFade.x;
			vector.y *= RotationsFade.y;
		}
		Quaternion quaternion = Quaternion.LookRotation(vector, spineBone.ProceduralRotation * ModelUpAxis);
		if (AngleLimit < 91f)
		{
			float num = Quaternion.Angle(quaternion, b);
			if (num > AngleLimit)
			{
				float num2 = 0f;
				num2 = Mathf.InverseLerp(0f, num, num - AngleLimit);
				Quaternion b2 = Quaternion.LerpUnclamped(quaternion, b, num2);
				float num3 = Mathf.Min(1f, num / (AngleLimit / 0.75f));
				num3 = Mathf.Sqrt(Mathf.Pow(num3, 4f)) * num3;
				quaternion = ((LimitSmoother != 0f) ? Quaternion.LerpUnclamped(quaternion, b2, unifiedDelta * (1f - LimitSmoother) * 50f * num3) : Quaternion.LerpUnclamped(quaternion, b2, num3));
			}
		}
		if (GoBackSpeed <= 0f)
		{
			if (StraightenSpeed > 0f)
			{
				float num4 = (spineBone2.ReferencePosition - spineBone2.PreviousReferencePosition).magnitude / spineBone2.GetUnscalledBoneLength();
				if (num4 > 0.5f)
				{
					num4 = 0.5f;
				}
				float b3 = num4 * (1f + StraightenSpeed / 5f);
				spineBone2.StraightenFactor = Mathf.Lerp(spineBone2.StraightenFactor, b3, unifiedDelta * (7f + StraightenSpeed));
				if (num4 > 0.0001f)
				{
					quaternion = Quaternion.Lerp(quaternion, b, unifiedDelta * spineBone2.StraightenFactor * (StraightenSpeed + 5f) * (TurboStraighten ? 6f : 1f));
				}
			}
		}
		else
		{
			float num5 = 0f;
			if (StraightenSpeed > 0f)
			{
				if (previousPos != RoundPosDiff(SpineBones[leadingBoneIndex].ProceduralPosition))
				{
					spineBone2.TargetStraightenFactor = 1f;
				}
				else if (spineBone2.TargetStraightenFactor > 0f)
				{
					spineBone2.TargetStraightenFactor -= delta * (5f + StraightenSpeed);
				}
				spineBone2.StraightenFactor = Mathf.Lerp(spineBone2.StraightenFactor, spineBone2.TargetStraightenFactor, unifiedDelta * (1f + StraightenSpeed));
				if (spineBone2.StraightenFactor > 0.025f)
				{
					num5 = spineBone2.StraightenFactor * StraightenSpeed * (TurboStraighten ? 6f : 1f);
				}
			}
			quaternion = Quaternion.Lerp(quaternion, b, unifiedDelta * (Mathf.Lerp(0f, 55f, GoBackSpeed) + num5));
		}
		if (RotSmoother == 0f)
		{
			spineBone2.ProceduralRotation = quaternion;
		}
		else
		{
			spineBone2.ProceduralRotation = Quaternion.LerpUnclamped(spineBone2.ProceduralRotation, quaternion, Mathf.LerpUnclamped(0f, Mathf.LerpUnclamped(1f, unifiedDelta, RotSmoother), MotionInfluence));
		}
	}

	private void UpdateChainIndexHelperVariables()
	{
		if (chainReverseFlag == LastBoneLeading)
		{
			return;
		}
		chainReverseFlag = LastBoneLeading;
		if (LastBoneLeading)
		{
			leadingBoneIndex = SpineBones.Count - 1;
			chainIndexDirection = -1;
			chainIndexOffset = 1;
			headBone = backHead;
		}
		else
		{
			leadingBoneIndex = 0;
			chainIndexDirection = 1;
			chainIndexOffset = 0;
			headBone = frontHead;
		}
		if (LastBoneLeading)
		{
			for (int i = 0; i < SpineBones.Count; i++)
			{
				SpineBones[i].SetDistanceForFrameBackward();
			}
		}
		else
		{
			for (int j = 0; j < SpineBones.Count; j++)
			{
				SpineBones[j].SetDistanceForFrameForward();
			}
		}
	}

	private void RefreshReferencePose()
	{
		if ((bool)HeadAnchor && !AnimateAnchor)
		{
			SpineBones[leadingBoneIndex].transform.localRotation = SpineBones[leadingBoneIndex].InitialLocalRotation;
		}
		if (LastBoneLeading)
		{
			headBone.SetCoordsForFrameBackward();
			if (!HeadAnchor)
			{
				SpineBones[leadingBoneIndex].UpdateReferencePosition(headBone.SnapshotPosition);
				SpineBones[leadingBoneIndex].ReferenceRotation = BaseTransform.rotation;
			}
			else
			{
				SpineBones[leadingBoneIndex].UpdateReferencePosition(headBone.transform.position);
				SpineBones[leadingBoneIndex].ReferenceRotation = BaseTransform.rotation;
			}
			if (LeadBoneRotationOffset.sqrMagnitude != 0f && LeadBoneOffsetReference)
			{
				SpineBones[leadingBoneIndex].ReferenceRotation *= Quaternion.Euler(LeadBoneRotationOffset);
			}
			if (ReverseForward)
			{
				SpineBones[leadingBoneIndex].ReferenceRotation *= Rotate180;
			}
			for (int num = SpineBones.Count - 2; num >= 0; num--)
			{
				SpineBones[num].ReferenceRotation = SpineBones[num + 1].ReferenceRotation;
				SpineBones[num].UpdateReferencePosition(SpineBones[num + 1].ReferencePosition - SpineBones[num].ReferenceRotation * ModelForwardAxis * (SpineBones[num].BoneLength * DistancesMultiplier * BaseTransform.lossyScale.x));
			}
		}
		else
		{
			headBone.SetCoordsForFrameForward();
			if (!HeadAnchor)
			{
				SpineBones[leadingBoneIndex].UpdateReferencePosition(headBone.SnapshotPosition);
				SpineBones[leadingBoneIndex].ReferenceRotation = BaseTransform.rotation;
			}
			else
			{
				SpineBones[leadingBoneIndex].UpdateReferencePosition(headBone.transform.position);
				SpineBones[leadingBoneIndex].ReferenceRotation = headBone.GetLocalRotationDiff();
			}
			if (LeadBoneRotationOffset.sqrMagnitude != 0f && LeadBoneOffsetReference)
			{
				SpineBones[leadingBoneIndex].ReferenceRotation *= Quaternion.Euler(LeadBoneRotationOffset);
			}
			if (ReverseForward)
			{
				SpineBones[leadingBoneIndex].ReferenceRotation *= Rotate180;
			}
			for (int i = 1; i < SpineBones.Count; i++)
			{
				SpineBones[i].ReferenceRotation = SpineBones[i - 1].ReferenceRotation;
				SpineBones[i].UpdateReferencePosition(SpineBones[i - 1].ReferencePosition - SpineBones[i].ReferenceRotation * ModelForwardAxis * (SpineBones[i].BoneLength * DistancesMultiplier * BaseTransform.lossyScale.x));
			}
		}
	}

	private void ReposeSpine()
	{
		UpdateChainIndexHelperVariables();
		RefreshReferencePose();
		for (int i = 0; i < SpineBones.Count; i++)
		{
			SpineBones[i].ProceduralPosition = SpineBones[i].ReferencePosition;
			SpineBones[i].ProceduralRotation = SpineBones[i].ReferenceRotation;
			SpineBones[i].PreviousPosition = SpineBones[i].ReferencePosition;
			SpineBones[i].FinalPosition = SpineBones[i].ReferencePosition;
			SpineBones[i].FinalRotation = SpineBones[i].ReferenceRotation;
		}
	}

	private void BeginBaseBonesUpdate()
	{
		if (HeadAnchor != null)
		{
			SpineBones[leadingBoneIndex].ProceduralRotation = headBone.GetLocalRotationDiff();
			SpineBones[leadingBoneIndex].ProceduralPosition = SpineBones[leadingBoneIndex].transform.position;
		}
		else
		{
			SpineBones[leadingBoneIndex].ProceduralPosition = SpineBones[leadingBoneIndex].ReferencePosition;
			SpineBones[leadingBoneIndex].ProceduralRotation = SpineBones[leadingBoneIndex].ReferenceRotation;
		}
		if (LeadBoneRotationOffset.sqrMagnitude != 0f && !LeadBoneOffsetReference)
		{
			SpineBones[leadingBoneIndex].ProceduralRotation *= Quaternion.Euler(LeadBoneRotationOffset);
		}
		SpineBones[leadingBoneIndex].CalculateDifferencePose(ModelUpAxis, ModelRightAxis);
		SpineBones[leadingBoneIndex].ApplyDifferencePose();
	}

	private IEnumerator LateFixed()
	{
		WaitForFixedUpdate fixedWait = CoroutineEx.waitForFixedUpdate;
		lateFixedIsRunning = true;
		do
		{
			yield return fixedWait;
			PreCalibrateBones();
			fixedAllow = true;
		}
		while (lateFixedIsRunning);
	}

	public void OnDestroy()
	{
		RemovePivotOffset();
	}

	private void OnValidate()
	{
		if (!_CheckedPivot)
		{
			if (MainPivotOffset != Vector3.zero)
			{
				PivotOffsetOnStart = false;
			}
			_CheckedPivot = true;
		}
		if (SpineBones == null)
		{
			SpineBones = new List<SpineBone>();
		}
		if (!PivotOffsetOnStart)
		{
			UpdatePivotOffsetState();
		}
		if (UseCollisions)
		{
			CheckForColliderDuplicates();
		}
		if (UpdateRate < 0f)
		{
			UpdateRate = 0f;
		}
		ModelRightAxis = Vector3.Cross(ModelForwardAxis, ModelUpAxis);
	}

	public void AddConnector(FSpineBoneConnector connector)
	{
		if (connectors == null)
		{
			connectors = new List<FSpineBoneConnector>();
		}
		if (!connectors.Contains(connector))
		{
			connectors.Add(connector);
		}
	}

	public void Init()
	{
		if (SpineBones.Count == 0)
		{
			if (SpineTransforms.Count <= 2)
			{
				Debug.Log("[SPINE ANIMATOR] could not initialize Spine Animator inside '" + base.name + "' because there are no bones to animate!");
				return;
			}
			CreateSpineChain(SpineTransforms[0], SpineTransforms[SpineTransforms.Count - 1]);
			Debug.Log("[SPINE ANIMATOR] Auto Bone Conversion from old version of Spine Animator! Please select your objects with Spine Animator to pre-convert it instead of automatically doing it when game Starts! (" + base.name + ")");
		}
		if (initialized)
		{
			Debug.Log("[Spine Animator] " + base.name + " is already initialized!");
			return;
		}
		if (BaseTransform == null)
		{
			BaseTransform = FindBaseTransform();
		}
		for (int i = 0; i < SpineBones.Count; i++)
		{
			if (Vector3.Distance(b: (i != SpineBones.Count - 1) ? SpineBones[i + 1].transform.position : ((SpineBones[i].transform.childCount <= 0) ? (SpineBones[i - 1].transform.position + (SpineBones[i - 1].transform.position - SpineBones[i].transform.position)) : SpineBones[i].transform.GetChild(0).position), a: SpineBones[i].transform.position) < 0.01f)
			{
				float magnitude = (SpineBones[SpineBones.Count - 1].transform.position - SpineBones[SpineBones.Count - 2].transform.parent.position).magnitude;
				Vector3 direction = SpineBones[i].transform.position - BaseTransform.position;
				Vector3 vector = BaseTransform.InverseTransformDirection(direction);
				vector.y = 0f;
				vector.Normalize();
				SpineBones[i + 1].DefaultForward = vector;
				SpineBones[i + 1].transform.position = SpineBones[i + 1].transform.position + BaseTransform.TransformDirection(vector) * magnitude * -0.125f;
			}
		}
		referenceDistance = 0f;
		for (int j = 0; j < SpineBones.Count; j++)
		{
			SpineBones[j].PrepareBone(BaseTransform, SpineBones, j);
			referenceDistance += SpineBones[j].BoneLength;
		}
		referenceDistance /= SpineBones.Count;
		frontHead = new HeadBone(SpineBones[0].transform);
		frontHead.PrepareBone(BaseTransform, SpineBones, 0);
		backHead = new HeadBone(SpineBones[SpineBones.Count - 1].transform);
		backHead.PrepareBone(BaseTransform, SpineBones, SpineBones.Count - 1);
		if (LastBoneLeading)
		{
			headBone = backHead;
		}
		else
		{
			headBone = frontHead;
		}
		CollidersDataToCheck = new List<FImp_ColliderData_Base>();
		chainReverseFlag = !LastBoneLeading;
		UpdateChainIndexHelperVariables();
		ReposeSpine();
		initialized = true;
	}

	public void CreateSpineChain(Transform start, Transform end)
	{
		if (start == null || end == null)
		{
			Debug.Log("[SPINE ANIMATOR] Can't create spine chain if one of the bones is null!");
			return;
		}
		List<Transform> list = new List<Transform>();
		Transform transform = end;
		while (transform != null && !(transform == start))
		{
			list.Add(transform);
			transform = transform.parent;
		}
		if (transform == null)
		{
			Debug.Log("[SPINE ANIMATOR] '" + start.name + "' is not child of '" + end.name + "' !");
			return;
		}
		list.Add(start);
		list.Reverse();
		SpineBones = new List<SpineBone>();
		for (int i = 0; i < list.Count; i++)
		{
			SpineBone item = new SpineBone(list[i]);
			SpineBones.Add(item);
		}
	}

	private void PreCalibrateBones()
	{
		for (int i = 0; i < SpineBones.Count; i++)
		{
			SpineBones[i].transform.localPosition = SpineBones[i].InitialLocalPosition;
			SpineBones[i].transform.localRotation = SpineBones[i].InitialLocalRotation;
		}
		if (BonesFixers.Count > 0)
		{
			for (int j = 0; j < BonesFixers.Count; j++)
			{
				BonesFixers[j].Calibration();
			}
		}
	}

	private void CalibrateBones()
	{
		if (BonesFixers.Count > 0)
		{
			for (int i = 0; i < BonesFixers.Count; i++)
			{
				BonesFixers[i].UpdateOnAnimator();
			}
		}
		if (connectors != null)
		{
			for (int j = 0; j < connectors.Count; j++)
			{
				connectors[j].RememberAnimatorState();
			}
		}
		ModelForwardAxisScaled = Vector3.Scale(ModelForwardAxis, BaseTransform.localScale);
		ModelUpAxisScaled = Vector3.Scale(ModelUpAxis, BaseTransform.localScale);
	}

	private void DeltaTimeCalculations()
	{
		switch (DeltaType)
		{
		case EFDeltaType.SafeDelta:
			delta = Mathf.Lerp(delta, GetClampedSmoothDelta(), 0.05f);
			break;
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
		unifiedDelta = Mathf.Pow(delta, 0.1f) * 0.04f;
	}

	private void StableUpdateRateCalculations()
	{
		updateLoops = 1;
		float num = 1f / UpdateRate;
		elapsedDeltaHelper += delta;
		updateLoops = 0;
		while (elapsedDeltaHelper >= num)
		{
			elapsedDeltaHelper -= num;
			if (++updateLoops >= 3)
			{
				elapsedDeltaHelper = 0f;
				break;
			}
		}
	}

	private void ApplyNewBonesCoordinates()
	{
		if (SpineAnimatorAmount >= 1f)
		{
			SpineBones[leadingBoneIndex].transform.position = SpineBones[leadingBoneIndex].FinalPosition;
			SpineBones[leadingBoneIndex].transform.rotation = SpineBones[leadingBoneIndex].FinalRotation;
			for (int i = 1 - chainIndexOffset; i < SpineBones.Count - chainIndexOffset; i++)
			{
				SpineBones[i].transform.position = SpineBones[i].FinalPosition;
				SpineBones[i].transform.rotation = SpineBones[i].FinalRotation;
				SpineBones[i].RefreshFinalLocalPose();
			}
			SpineBones[leadingBoneIndex].RefreshFinalLocalPose();
		}
		else
		{
			SpineBones[leadingBoneIndex].transform.position = Vector3.LerpUnclamped(SpineBones[leadingBoneIndex].transform.position, SpineBones[leadingBoneIndex].FinalPosition, SpineAnimatorAmount * SpineBones[leadingBoneIndex].MotionWeight);
			SpineBones[leadingBoneIndex].transform.rotation = Quaternion.LerpUnclamped(SpineBones[leadingBoneIndex].transform.rotation, SpineBones[leadingBoneIndex].FinalRotation, SpineAnimatorAmount * SpineBones[leadingBoneIndex].MotionWeight);
			for (int j = 1 - chainIndexOffset; j < SpineBones.Count - chainIndexOffset; j++)
			{
				SpineBones[j].transform.position = Vector3.LerpUnclamped(SpineBones[j].transform.position, SpineBones[j].FinalPosition, SpineAnimatorAmount * SpineBones[j].MotionWeight);
				SpineBones[j].transform.rotation = Quaternion.LerpUnclamped(SpineBones[j].transform.rotation, SpineBones[j].FinalRotation, SpineAnimatorAmount * SpineBones[j].MotionWeight);
				SpineBones[j].RefreshFinalLocalPose();
			}
			SpineBones[leadingBoneIndex].RefreshFinalLocalPose();
		}
	}

	private void EndUpdate()
	{
		previousPos = SpineBones[leadingBoneIndex].ProceduralPosition;
		if (connectors != null)
		{
			for (int i = 0; i < connectors.Count; i++)
			{
				connectors[i].RefreshAnimatorState();
			}
		}
		if (BonesFixers.Count > 0)
		{
			for (int j = 0; j < BonesFixers.Count; j++)
			{
				BonesFixers[j].UpdateAfterProcedural();
			}
		}
	}

	public void OnDrop(PointerEventData data)
	{
	}

	public Transform FindBaseTransform()
	{
		Transform result = base.transform;
		Transform transform = base.transform.parent;
		FSpineAnimator fSpineAnimator = null;
		if (transform != null)
		{
			for (int i = 0; i < 32; i++)
			{
				Transform parent = transform.parent;
				fSpineAnimator = transform.GetComponent<FSpineAnimator>();
				if ((bool)fSpineAnimator)
				{
					break;
				}
				transform = parent;
				if (parent == null)
				{
					break;
				}
			}
		}
		if (fSpineAnimator != null)
		{
			result = ((!(fSpineAnimator.BaseTransform != null)) ? fSpineAnimator.transform : fSpineAnimator.BaseTransform);
			if (fSpineAnimator.transform != base.transform)
			{
				UpdateAsLast = true;
			}
		}
		return result;
	}

	public SpineBone GetLeadingBone()
	{
		if (SpineBones == null || SpineBones.Count == 0)
		{
			return null;
		}
		if (LastBoneLeading)
		{
			return SpineBones[SpineBones.Count - 1];
		}
		return SpineBones[0];
	}

	public SpineBone GetEndBone()
	{
		if (SpineBones == null || SpineBones.Count == 0)
		{
			return null;
		}
		if (LastBoneLeading)
		{
			return SpineBones[0];
		}
		return SpineBones[SpineBones.Count - 1];
	}

	public Transform GetHeadBone()
	{
		if (SpineBones.Count <= 0)
		{
			return base.transform;
		}
		if (LastBoneLeading)
		{
			return SpineBones[SpineBones.Count - 1].transform;
		}
		return SpineBones[0].transform;
	}

	public SpineBone GetLeadBone()
	{
		if (LastBoneLeading)
		{
			return SpineBones[SpineBones.Count - 1];
		}
		return SpineBones[0];
	}

	public Transform GetBaseTransform()
	{
		if (BaseTransform == null)
		{
			return base.transform;
		}
		return BaseTransform;
	}

	private Vector3 RoundPosDiff(Vector3 pos, int digits = 1)
	{
		return new Vector3((float)Math.Round(pos.x, digits), (float)Math.Round(pos.y, digits), (float)Math.Round(pos.z, digits));
	}

	private Vector3 RoundToBiggestValue(Vector3 vec)
	{
		int num = 0;
		if (Mathf.Abs(vec.y) > Mathf.Abs(vec.x))
		{
			num = 1;
			if (Mathf.Abs(vec.z) > Mathf.Abs(vec.y))
			{
				num = 2;
			}
		}
		else if (Mathf.Abs(vec.z) > Mathf.Abs(vec.x))
		{
			num = 2;
		}
		vec = num switch
		{
			0 => new Vector3(Mathf.Round(vec.x), 0f, 0f), 
			1 => new Vector3(0f, Mathf.Round(vec.y), 0f), 
			_ => new Vector3(0f, 0f, Mathf.Round(vec.z)), 
		};
		return vec;
	}

	private float GetClampedSmoothDelta()
	{
		return Mathf.Clamp(Time.smoothDeltaTime, 0f, 0.1f);
	}

	public List<Transform> GetOldSpineTransforms()
	{
		return SpineTransforms;
	}

	public void ClearOldSpineTransforms()
	{
		if (SpineTransforms != null)
		{
			SpineTransforms.Clear();
		}
	}

	public void User_ChangeParameter(EParamChange parameter, float to, float transitionDuration, float executionDelay = 0f)
	{
		if (transitionDuration <= 0f && executionDelay <= 0f)
		{
			SetValue(parameter, to);
		}
		else
		{
			StartCoroutine(IEChangeValue(parameter, to, transitionDuration, executionDelay));
		}
	}

	public void User_ChangeParameterAndRestore(EParamChange parameter, float to, float transitionDuration, float restoreAfter = 0f)
	{
		float value = GetValue(parameter);
		StartCoroutine(IEChangeValue(parameter, to, transitionDuration, 0f));
		StartCoroutine(IEChangeValue(parameter, value, transitionDuration, transitionDuration + restoreAfter));
	}

	public void User_ResetBones()
	{
		_ResetBones();
	}

	private IEnumerator IEChangeValue(EParamChange param, float to, float duration, float executionDelay)
	{
		if (executionDelay > 0f)
		{
			yield return CoroutineEx.waitForSeconds(executionDelay);
		}
		if (duration > 0f)
		{
			float elapsed = 0f;
			float startVal = GetValue(param);
			while (elapsed < duration)
			{
				elapsed += Time.deltaTime;
				float num = elapsed / duration;
				if (num > 1f)
				{
					num = 1f;
				}
				SetValue(param, Mathf.LerpUnclamped(startVal, to, num));
				yield return null;
			}
		}
		SetValue(param, to);
	}

	private float GetValue(EParamChange param)
	{
		return param switch
		{
			EParamChange.GoBackSpeed => GoBackSpeed, 
			EParamChange.SpineAnimatorAmount => SpineAnimatorAmount, 
			EParamChange.AngleLimit => AngleLimit, 
			EParamChange.StraightenSpeed => StraightenSpeed, 
			EParamChange.PositionSmoother => PosSmoother, 
			EParamChange.RotationSmoother => RotSmoother, 
			_ => 0f, 
		};
	}

	private void SetValue(EParamChange param, float val)
	{
		switch (param)
		{
		case EParamChange.GoBackSpeed:
			GoBackSpeed = val;
			break;
		case EParamChange.SpineAnimatorAmount:
			SpineAnimatorAmount = val;
			break;
		case EParamChange.AngleLimit:
			AngleLimit = val;
			break;
		case EParamChange.StraightenSpeed:
			StraightenSpeed = val;
			break;
		case EParamChange.PositionSmoother:
			PosSmoother = val;
			break;
		case EParamChange.RotationSmoother:
			RotSmoother = val;
			break;
		}
	}

	private void _ResetBones()
	{
		if (!LastBoneLeading)
		{
			for (int num = SpineBones.Count - 1; num >= 0; num--)
			{
				SpineBones[num].ProceduralPosition = SpineBones[num].ReferencePosition;
				SpineBones[num].ProceduralRotation = SpineBones[num].ReferenceRotation;
				SpineBones[num].PreviousPosition = SpineBones[num].ReferencePosition;
				SpineBones[num].FinalPosition = SpineBones[num].ReferencePosition;
				SpineBones[num].FinalRotation = SpineBones[num].ReferenceRotation;
			}
		}
		else
		{
			for (int i = 0; i < SpineBones.Count; i++)
			{
				SpineBones[i].ProceduralPosition = SpineBones[i].ReferencePosition;
				SpineBones[i].ProceduralRotation = SpineBones[i].ReferenceRotation;
				SpineBones[i].PreviousPosition = SpineBones[i].ReferencePosition;
				SpineBones[i].FinalPosition = SpineBones[i].ReferencePosition;
				SpineBones[i].FinalRotation = SpineBones[i].ReferenceRotation;
			}
		}
		float goBackSpeed = GoBackSpeed;
		GoBackSpeed = 10f;
		Update();
		FixedUpdate();
		delta = 0.25f;
		LateUpdate();
		GoBackSpeed = goBackSpeed;
	}

	private void Reset()
	{
		BaseTransform = FindBaseTransform();
		_CheckedPivot = true;
	}

	private void Start()
	{
		if (UpdateAsLast)
		{
			base.enabled = false;
			base.enabled = true;
		}
		if (BaseTransform == null)
		{
			BaseTransform = base.transform;
		}
		initialized = false;
		if (PivotOffsetOnStart && mainPivotOffsetTransform == null)
		{
			UpdatePivotOffsetState();
		}
		if (!StartAfterTPose)
		{
			Init();
		}
		else
		{
			initAfterTPoseCounter = 0;
		}
	}

	internal void Update()
	{
		using (TimeWarning.New("FSpineAnimator:Update"))
		{
			if (!initialized)
			{
				if (!StartAfterTPose)
				{
					updateSpineAnimator = false;
					return;
				}
				if (initAfterTPoseCounter <= 5)
				{
					initAfterTPoseCounter++;
					updateSpineAnimator = false;
					return;
				}
				Init();
			}
			if (OptimizeWithMesh != null && !OptimizeWithMesh.isVisible)
			{
				updateSpineAnimator = false;
				return;
			}
			if (delta <= Mathf.Epsilon)
			{
				updateSpineAnimator = false;
			}
			if (SpineBones.Count == 0)
			{
				Debug.LogError("[SPINE ANIMATOR] No spine bones defined in " + base.name + " !");
				initialized = false;
				updateSpineAnimator = false;
				return;
			}
			if (BaseTransform == null)
			{
				BaseTransform = base.transform;
			}
			UpdateChainIndexHelperVariables();
			if (SpineAnimatorAmount <= 0.01f)
			{
				wasBlendedOut = true;
				updateSpineAnimator = false;
				return;
			}
			if (wasBlendedOut)
			{
				ReposeSpine();
				wasBlendedOut = false;
			}
			updateSpineAnimator = true;
			if (AnimatePhysics == EFixedMode.None)
			{
				PreCalibrateBones();
				callSpineReposeCalculations = true;
			}
		}
	}

	internal void FixedUpdate()
	{
		using (TimeWarning.New("FSpineAnimator:FixedUpdate"))
		{
			if (updateSpineAnimator && AnimatePhysics == EFixedMode.Basic)
			{
				PreCalibrateBones();
				callSpineReposeCalculations = true;
				fixedUpdated = true;
			}
		}
	}

	internal void LateUpdate()
	{
		using (TimeWarning.New("FSpineAnimator:LateUpdate"))
		{
			if (!updateSpineAnimator)
			{
				return;
			}
			if (AnimatePhysics == EFixedMode.Late)
			{
				if (!lateFixedIsRunning)
				{
					StartCoroutine(LateFixed());
				}
				if (fixedAllow)
				{
					fixedAllow = false;
					callSpineReposeCalculations = true;
				}
			}
			else
			{
				if (lateFixedIsRunning)
				{
					lateFixedIsRunning = false;
				}
				if (AnimatePhysics == EFixedMode.Basic)
				{
					if (!fixedUpdated)
					{
						return;
					}
					fixedUpdated = false;
				}
			}
			CalibrateBones();
			DeltaTimeCalculations();
			if (UpdateRate > 0f)
			{
				StableUpdateRateCalculations();
				unifiedDelta = delta;
				if (UseCorrections && ManualAffectChain)
				{
					if (updateLoops > 0)
					{
						PreMotionNoHead();
					}
					PreMotionHead();
				}
				RefreshReferencePose();
				BeginBaseBonesUpdate();
				for (int i = 0; i < updateLoops; i++)
				{
					BeginPhysicsUpdate();
					if (callSpineReposeCalculations)
					{
						CalculateBonesCoordinates();
					}
				}
				if (UseCorrections && !ManualAffectChain && callSpineReposeCalculations)
				{
					if (updateLoops > 0)
					{
						PostMotionNoHead();
					}
					PostMotionHead();
				}
				if (callSpineReposeCalculations)
				{
					callSpineReposeCalculations = false;
				}
			}
			else
			{
				RefreshReferencePose();
				PreMotionBoneOffsets();
				BeginPhysicsUpdate();
				BeginBaseBonesUpdate();
				if (callSpineReposeCalculations)
				{
					CalculateBonesCoordinates();
					PostMotionBoneOffsets();
					callSpineReposeCalculations = false;
				}
			}
			ApplyNewBonesCoordinates();
			EndUpdate();
		}
	}
}
