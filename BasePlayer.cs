#define UNITY_ASSERTIONS
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using CompanionServer;
using ConVar;
using Facepunch;
using Facepunch.Extend;
using Facepunch.Math;
using Facepunch.Models;
using Facepunch.Rust;
using JetBrains.Annotations;
using Network;
using Network.Visibility;
using Newtonsoft.Json;
using ProtoBuf;
using ProtoBuf.Nexus;
using Rust;
using Rust.Ai.Gen2;
using SilentOrbit.ProtocolBuffers;
using UnityEngine;
using UnityEngine.Assertions;

public class BasePlayer : BaseCombatEntity, LootPanel.IHasLootPanel, IIdealSlotEntity, PlayerInventory.ICanMoveFrom, ISplashable
{
	public enum CameraMode
	{
		FirstPerson = 0,
		ThirdPerson = 1,
		Eyes = 2,
		FirstPersonWithArms = 3,
		DeathCamClassic = 4,
		Last = 3
	}

	public enum NetworkQueue
	{
		Update,
		UpdateDistance,
		Count
	}

	private class NetworkQueueList
	{
		public HashSet<BaseNetworkable> queueInternal = new HashSet<BaseNetworkable>();

		public int MaxLength;

		public int Length => queueInternal.Count;

		public bool Contains(BaseNetworkable ent)
		{
			return queueInternal.Contains(ent);
		}

		public void Add(BaseNetworkable ent)
		{
			if (!Contains(ent))
			{
				queueInternal.Add(ent);
			}
			MaxLength = Mathf.Max(MaxLength, queueInternal.Count);
		}

		public void Add(BaseNetworkable[] ent)
		{
			foreach (BaseNetworkable ent2 in ent)
			{
				Add(ent2);
			}
		}

		public void Clear(Group group)
		{
			using (TimeWarning.New("NetworkQueueList.Clear"))
			{
				if (group != null)
				{
					if (group.isGlobal)
					{
						return;
					}
					List<BaseNetworkable> obj = Facepunch.Pool.Get<List<BaseNetworkable>>();
					foreach (BaseNetworkable item in queueInternal)
					{
						if (item == null || item.net?.group == null || item.net.group == group)
						{
							obj.Add(item);
						}
					}
					foreach (BaseNetworkable item2 in obj)
					{
						queueInternal.Remove(item2);
					}
					Facepunch.Pool.FreeUnmanaged(ref obj);
				}
				else
				{
					queueInternal.RemoveWhere((BaseNetworkable x) => x == null || x.net?.group == null || !x.net.group.isGlobal);
				}
			}
		}
	}

	[Flags]
	public enum PlayerFlags
	{
		Unused1 = 1,
		Unused2 = 2,
		IsAdmin = 4,
		ReceivingSnapshot = 8,
		Sleeping = 0x10,
		Spectating = 0x20,
		Wounded = 0x40,
		IsDeveloper = 0x80,
		Connected = 0x100,
		ThirdPersonViewmode = 0x400,
		EyesViewmode = 0x800,
		ChatMute = 0x1000,
		NoSprint = 0x2000,
		Aiming = 0x4000,
		DisplaySash = 0x8000,
		Relaxed = 0x10000,
		SafeZone = 0x20000,
		ServerFall = 0x40000,
		Incapacitated = 0x80000,
		Workbench1 = 0x100000,
		Workbench2 = 0x200000,
		Workbench3 = 0x400000,
		VoiceRangeBoost = 0x800000,
		ModifyClan = 0x1000000,
		LoadingAfterTransfer = 0x2000000,
		NoRespawnZone = 0x4000000,
		IsInTutorial = 0x8000000,
		IsRestrained = 0x10000000,
		CreativeMode = 0x20000000,
		WaitingForGestureInteraction = 0x40000000
	}

	private enum RPSWinState
	{
		Win,
		Loss,
		Draw
	}

	public static class GestureIds
	{
		public const uint FlashBlindId = 235662700u;
	}

	public enum GestureStartSource
	{
		ServerAction,
		Player
	}

	public enum MapNoteType
	{
		Death,
		PointOfInterest
	}

	public enum PingType
	{
		Hostile = 0,
		GoTo = 1,
		Dollar = 2,
		Loot = 3,
		Node = 4,
		Gun = 5,
		Build = 6,
		LAST = 6
	}

	public struct PingStyle
	{
		public int IconIndex;

		public int ColourIndex;

		public Translate.Phrase PingTitle;

		public Translate.Phrase PingDescription;

		public PingType Type;

		public PingStyle(int icon, int colour, Translate.Phrase title, Translate.Phrase desc, PingType pType)
		{
			IconIndex = icon;
			ColourIndex = colour;
			PingTitle = title;
			PingDescription = desc;
			Type = pType;
		}
	}

	[JsonModel]
	public struct FiredProjectileUpdate
	{
		public Vector3 OldPosition;

		public Vector3 NewPosition;

		public Vector3 OldVelocity;

		public Vector3 NewVelocity;

		public float Mismatch;

		public float PartialTime;
	}

	public class FiredProjectile : Facepunch.Pool.IPooled
	{
		public ItemDefinition itemDef;

		public ItemModProjectile itemMod;

		public Projectile projectilePrefab;

		public float firedTime;

		public float travelTime;

		public float partialTime;

		public AttackEntity weaponSource;

		public AttackEntity weaponPrefab;

		public Projectile.Modifier projectileModifier;

		public Item pickupItem;

		public float integrity;

		public float trajectoryMismatch;

		public float startPointMismatch;

		public float endPointMismatch;

		public float entityDistance;

		public Vector3 position;

		public Vector3 initialPositionOffset;

		public Vector3 positionOffset;

		public Vector3 velocity;

		public Vector3 initialPosition;

		public Vector3 initialVelocity;

		public Vector3 inheritedVelocity;

		public int protection;

		public int ricochets;

		public int hits;

		public BaseEntity lastEntityHit;

		public float desyncLifeTime;

		public int id;

		public BasePlayer attacker;

		public List<FiredProjectileUpdate> updates = new List<FiredProjectileUpdate>();

		public List<Vector3> simulatedPositions = new List<Vector3>();

		public void EnterPool()
		{
			itemDef = null;
			itemMod = null;
			projectilePrefab = null;
			firedTime = 0f;
			travelTime = 0f;
			partialTime = 0f;
			weaponSource = null;
			weaponPrefab = null;
			projectileModifier = default(Projectile.Modifier);
			pickupItem = null;
			integrity = 0f;
			trajectoryMismatch = 0f;
			startPointMismatch = 0f;
			endPointMismatch = 0f;
			entityDistance = 0f;
			position = default(Vector3);
			velocity = default(Vector3);
			initialPosition = default(Vector3);
			initialVelocity = default(Vector3);
			inheritedVelocity = default(Vector3);
			protection = 0;
			ricochets = 0;
			hits = 0;
			lastEntityHit = null;
			desyncLifeTime = 0f;
			id = 0;
			attacker = null;
			updates.Clear();
			simulatedPositions.Clear();
		}

		public void LeavePool()
		{
		}
	}

	public class SpawnPoint
	{
		public Vector3 pos;

		public Quaternion rot;
	}

	internal struct DeathBlow
	{
		public BaseEntity Initiator;

		public BaseEntity WeaponPrefab;

		public uint HitBone;

		public bool IsValid;

		public static void From(HitInfo hitInfo, out DeathBlow deathBlow)
		{
			deathBlow = default(DeathBlow);
			deathBlow.IsValid = hitInfo != null;
			if (deathBlow.IsValid)
			{
				deathBlow.Initiator = hitInfo.Initiator;
				deathBlow.WeaponPrefab = hitInfo.WeaponPrefab;
				deathBlow.HitBone = hitInfo.HitBone;
			}
			else
			{
				deathBlow.IsValid = false;
				deathBlow.Initiator = null;
				deathBlow.WeaponPrefab = null;
			}
		}

		public static void Reset(ref DeathBlow deathBlow)
		{
			deathBlow.IsValid = false;
			deathBlow.Initiator = null;
			deathBlow.WeaponPrefab = null;
			deathBlow.HitBone = 0u;
		}
	}

	public enum TimeCategory
	{
		Wilderness = 1,
		Monument = 2,
		Base = 4,
		Flying = 8,
		Boating = 0x10,
		Swimming = 0x20,
		Driving = 0x40
	}

	public class LifeStoryWorkQueue : ObjectWorkQueue<BasePlayer>
	{
		protected override void RunJob(BasePlayer entity)
		{
			entity.UpdateTimeCategory();
		}

		protected override bool ShouldAdd(BasePlayer entity)
		{
			if (base.ShouldAdd(entity))
			{
				return entity.IsValid();
			}
			return false;
		}
	}

	private class NearbyStash
	{
		public StashContainer Entity;

		public float LookingAtTime;

		public NearbyStash(StashContainer stash)
		{
			Entity = stash;
			LookingAtTime = 0f;
		}
	}

	internal struct CachedState
	{
		public WaterLevel.WaterInfo WaterInfo;

		public float WaterFactor;

		public bool IsSwimming;

		public bool IsValid;
	}

	public enum TutorialItemAllowance
	{
		AlwaysAllowed = -1,
		None = 0,
		Level1_HatchetPickaxe = 10,
		Level2_Planner = 20,
		Level3_Bag_TC_Door = 30,
		Level3_Hammer = 35,
		Level4_Spear_Fire = 40,
		Level5_PrepareForCombat = 50,
		Level6_Furnace = 60,
		Level7_WorkBench = 70,
		Level8_Kayak = 80
	}

	[Serializable]
	public struct CapsuleColliderInfo
	{
		public float height;

		public float radius;

		public Vector3 center;

		public CapsuleColliderInfo(float height, float radius, Vector3 center)
		{
			this.height = height;
			this.radius = radius;
			this.center = center;
		}
	}

	public sealed class HiddenValue<T> : Facepunch.Pool.IPooled, IDisposable where T : class
	{
		private T _value;

		public HiddenValue()
			: this((T)null)
		{
		}

		public HiddenValue(T value)
		{
			_value = value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T Get()
		{
			return _value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public HiddenValue<T> Set(T value)
		{
			_value = value;
			return this;
		}

		void Facepunch.Pool.IPooled.EnterPool()
		{
			Set(null);
		}

		void Facepunch.Pool.IPooled.LeavePool()
		{
		}

		public void Dispose()
		{
			Set(null);
			HiddenValue<T> obj = this;
			Facepunch.Pool.Free(ref obj);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator T(HiddenValue<T> hidden)
		{
			return hidden.Get();
		}
	}

	public struct HiddenValue_Internal_EncryptedValue<TInner> where TInner : unmanaged
	{
		private TInner _value;

		private int _padding;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TInner Get()
		{
			return _value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(TInner value)
		{
			_value = value;
		}

		public override string ToString()
		{
			return Get().ToString();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator HiddenValue_Internal_EncryptedValue<TInner>(TInner value)
		{
			HiddenValue_Internal_EncryptedValue<TInner> result = default(HiddenValue_Internal_EncryptedValue<TInner>);
			result.Set(value);
			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator TInner(HiddenValue_Internal_EncryptedValue<TInner> encrypted)
		{
			return encrypted.Get();
		}
	}

	public struct EncryptedValue<TInner> where TInner : unmanaged
	{
		private TInner _value;

		private int _padding;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TInner Get()
		{
			return _value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(TInner value)
		{
			_value = value;
		}

		public override string ToString()
		{
			return Get().ToString();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator EncryptedValue<TInner>(TInner value)
		{
			EncryptedValue<TInner> result = default(EncryptedValue<TInner>);
			result.Set(value);
			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator TInner(EncryptedValue<TInner> encrypted)
		{
			return encrypted.Get();
		}
	}

	[NonSerialized]
	public bool isInAir;

	[NonSerialized]
	public bool isOnPlayer;

	[NonSerialized]
	public float violationLevel;

	[NonSerialized]
	public float lastViolationTime;

	[NonSerialized]
	public float lastAdminCheatTime;

	[NonSerialized]
	public AntiHackType lastViolationType;

	[NonSerialized]
	public float vehiclePauseTime;

	[NonSerialized]
	public float speedhackPauseTime;

	[NonSerialized]
	public float speedhackDistance;

	[NonSerialized]
	public float flyhackPauseTime;

	[NonSerialized]
	public float flyhackDistanceVertical;

	[NonSerialized]
	public float flyhackDistanceHorizontal;

	[NonSerialized]
	public Vector3 lastGroundedPosition;

	[NonSerialized]
	public float fallingVelocity;

	[NonSerialized]
	public float fallingDistance;

	[NonSerialized]
	public float timeInAir;

	[NonSerialized]
	public float waterDelay;

	[NonSerialized]
	public Vector3 initialVelocity;

	[NonSerialized]
	public float tickDistancePausetime;

	[NonSerialized]
	public float lastInAirTime;

	[NonSerialized]
	public TimeAverageValueLookup<uint> rpcHistory = new TimeAverageValueLookup<uint>();

	public static readonly Translate.Phrase ClanInviteSuccess = new Translate.Phrase("clan.action.invite.success", "Invited {name} to your clan.");

	public static readonly Translate.Phrase ClanInviteFailure = new Translate.Phrase("clan.action.invite.failure", "Failed to invite {name} to your clan. Please wait a minute and try again.");

	public static readonly Translate.Phrase ClanInviteFull = new Translate.Phrase("clan.action.invite.full", "Cannot invite {name} to your clan because your clan is full.");

	[NonSerialized]
	public long clanId;

	[NonSerialized]
	public IClan serverClan;

	public ViewModel GestureViewModel;

	private const float drinkRange = 1.5f;

	private const float drinkMovementSpeed = 0.1f;

	[NonSerialized]
	private NetworkQueueList[] networkQueue = new NetworkQueueList[2]
	{
		new NetworkQueueList(),
		new NetworkQueueList()
	};

	[NonSerialized]
	private NetworkQueueList SnapshotQueue = new NetworkQueueList();

	public const string GestureCancelString = "cancel";

	private TimeUntil gestureFinishedTime;

	private TimeSince blockHeldInputTimer;

	private GestureConfig currentGesture;

	public static Translate.Phrase WinRPSPhrase = new Translate.Phrase("rps_win", "You win the game!");

	public static Translate.Phrase LoseRPSPhrase = new Translate.Phrase("rps_lose", "You lose the game!");

	public static Translate.Phrase DrawRPSPhrase = new Translate.Phrase("rps_draw", "The game was a draw!");

	private HashSet<NetworkableId> recentWaveTargets = new HashSet<NetworkableId>();

	public const string WAVED_PLAYERS_STAT = "waved_at_players";

	private NetworkableId rpsTarget;

	private int selectedRpsOption = -1;

	public const float RPSWaitTime = 10f;

	private TimeSince interactiveGestureStartTime;

	public ulong currentTeam;

	public static readonly Translate.Phrase MaxTeamSizeToast = new Translate.Phrase("maxteamsizetip", "Your team is full. Remove a member to invite another player.");

	private bool sentInstrumentTeamAchievement;

	private bool sentSummerTeamAchievement;

	private const int TEAMMATE_INSTRUMENT_COUNT_ACHIEVEMENT = 4;

	private const int TEAMMATE_SUMMER_FLOATING_COUNT_ACHIEVEMENT = 4;

	private const string TEAMMATE_INSTRUMENT_ACHIEVEMENT = "TEAM_INSTRUMENTS";

	private const string TEAMMATE_SUMMER_ACHIEVEMENT = "SUMMER_INFLATABLE";

	public static Translate.Phrase MarkerLimitPhrase = new Translate.Phrase("map.marker.limited", "Cannot place more than {0} markers.");

	public const int MaxMapNoteLabelLength = 10;

	public List<BaseMission.MissionInstance> missions = new List<BaseMission.MissionInstance>();

	private float thinkEvery = 1f;

	private float timeSinceMissionThink;

	private BaseMission followupMission;

	private IMissionProvider followupMissionProvider;

	private int _activeMission = -1;

	[NonSerialized]
	public ModelState modelState = new ModelState();

	[NonSerialized]
	private bool wantsSendModelState;

	[NonSerialized]
	private float nextModelStateUpdate;

	[NonSerialized]
	private EntityRef mounted;

	private float nextSeatSwapTime;

	public BaseEntity PetEntity;

	[NonSerialized]
	public IPet Pet;

	private float lastPetCommandIssuedTime;

	private static readonly Translate.Phrase HostileTitle = new Translate.Phrase("ping_hostile", "Hostile");

	private static readonly Translate.Phrase HostileDesc = new Translate.Phrase("ping_hostile_desc", "Danger in area");

	private static readonly PingStyle HostileMarker = new PingStyle(4, 3, HostileTitle, HostileDesc, PingType.Hostile);

	private static readonly Translate.Phrase GoToTitle = new Translate.Phrase("ping_goto", "Go To");

	private static readonly Translate.Phrase GoToDesc = new Translate.Phrase("ping_goto_desc", "Look at this");

	private static readonly PingStyle GoToMarker = new PingStyle(0, 2, GoToTitle, GoToDesc, PingType.GoTo);

	private static readonly Translate.Phrase DollarTitle = new Translate.Phrase("ping_dollar", "Value");

	private static readonly Translate.Phrase DollarDesc = new Translate.Phrase("ping_dollar_desc", "Something valuable is here");

	private static readonly PingStyle DollarMarker = new PingStyle(1, 1, DollarTitle, DollarDesc, PingType.Dollar);

	private static readonly Translate.Phrase LootTitle = new Translate.Phrase("ping_loot", "Loot");

	private static readonly Translate.Phrase LootDesc = new Translate.Phrase("ping_loot_desc", "Loot is here");

	private static readonly PingStyle LootMarker = new PingStyle(11, 0, LootTitle, LootDesc, PingType.Loot);

	private static readonly Translate.Phrase NodeTitle = new Translate.Phrase("ping_node", "Node");

	private static readonly Translate.Phrase NodeDesc = new Translate.Phrase("ping_node_desc", "An ore node is here");

	private static readonly PingStyle NodeMarker = new PingStyle(10, 4, NodeTitle, NodeDesc, PingType.Node);

	private static readonly Translate.Phrase GunTitle = new Translate.Phrase("ping_gun", "Weapon");

	private static readonly Translate.Phrase GunDesc = new Translate.Phrase("ping_weapon_desc", "A dropped weapon is here");

	private static readonly PingStyle GunMarker = new PingStyle(9, 5, GunTitle, GunDesc, PingType.Gun);

	private static readonly PingStyle BuildMarker = new PingStyle(12, 5, new Translate.Phrase(), new Translate.Phrase(), PingType.Build);

	private TimeSince lastTick;

	private List<(ItemDefinition item, PingType pingType)> tutorialDesiredResource = new List<(ItemDefinition, PingType)>();

	private List<(NetworkableId id, PingType pingType)> pingedEntities = new List<(NetworkableId, PingType)>();

	private TimeSince lastResourcePingUpdate;

	private bool _playerStateDirty;

	private string _wipeId;

	private BaseEntity cachedPrivilegeFromOther;

	private float cachedPrivilegeFromOtherTime;

	[NonSerialized]
	public Dictionary<int, FiredProjectile> firedProjectiles = new Dictionary<int, FiredProjectile>();

	private const float radiationDamageTime = 1f;

	private const float radiationDamageThreshold = 2500f;

	private const float radiationRatioAdjustment = 0.05f;

	private const float containerCheckRadTime = 2500f;

	private const float containerRadRatioAdjustment = 0.05f;

	private Action inflictInventoryRadsAction;

	private float inventoryRads;

	private bool hasOpenedLoot;

	private List<ItemContainer> radiationCheckContainers = new List<ItemContainer>();

	private float containerRads;

	private Action inflictRadsAction;

	private Action checkRadsAction;

	[NonSerialized]
	public PlayerStatistics stats;

	[NonSerialized]
	public GameObjectRef DeathIconOverride;

	[NonSerialized]
	public ItemId svActiveItemID;

	[NonSerialized]
	public float NextChatTime;

	[NonSerialized]
	public float nextSuicideTime;

	[NonSerialized]
	public float nextRespawnTime;

	[NonSerialized]
	public string respawnId;

	private RealTimeUntil timeUntilLoadingExpires;

	public Dictionary<BaseNetworkable, float> lastPlayerVisibility = new Dictionary<BaseNetworkable, float>();

	protected Vector3 viewAngles;

	private float lastSubscriptionTick;

	private float lastPlayerTick;

	private float sleepStartTime = -1f;

	private float fallTickRate = 0.1f;

	private float lastFallTime;

	private float fallVelocity;

	private DeathBlow cachedNonSuicideHit;

	public static ListHashSet<BasePlayer> activePlayerList = new ListHashSet<BasePlayer>();

	public static ListHashSet<BasePlayer> sleepingPlayerList = new ListHashSet<BasePlayer>();

	public static ListHashSet<BasePlayer> bots = new ListHashSet<BasePlayer>();

	private float cachedCraftLevel;

	private float nextCheckTime;

	private Workbench _cachedWorkbench;

	private PersistantPlayer cachedPersistantPlayer;

	private static OceanPaths cachedOceanPaths = null;

	private const int WILDERNESS = 1;

	private const int MONUMENT = 2;

	private const int BASE = 4;

	private const int FLYING = 8;

	private const int BOATING = 16;

	private const int SWIMMING = 32;

	private const int DRIVING = 64;

	[ServerVar]
	[Help("How many milliseconds to budget for processing life story updates per frame")]
	public static float lifeStoryFramebudgetms = 0.25f;

	[NonSerialized]
	public PlayerLifeStory lifeStory;

	[NonSerialized]
	public PlayerLifeStory previousLifeStory;

	private const float TimeCategoryUpdateFrequency = 7f;

	private float nextTimeCategoryUpdate;

	private bool hasSentPresenceState;

	private bool LifeStoryInWilderness;

	private bool LifeStoryInMonument;

	private bool LifeStoryInBase;

	private bool LifeStoryFlying;

	private bool LifeStoryBoating;

	private bool LifeStorySwimming;

	private bool LifeStoryDriving;

	private bool waitingForLifeStoryUpdate;

	public static LifeStoryWorkQueue lifeStoryQueue = new LifeStoryWorkQueue();

	[CanBeNull]
	private PlayerLifeStory.DeathInfo cachedOverrideDeathInfo;

	private bool IsSpectatingTeamInfo;

	private TimeSince lastSpectateTeamInfoUpdate;

	private int SpectateOffset = 1000000;

	private string spectateFilter = "";

	private List<NearbyStash> nearbyStashes = new List<NearbyStash>();

	private float lastUpdateTime = float.NegativeInfinity;

	private float cachedThreatLevel;

	[NonSerialized]
	public float weaponDrawnDuration;

	[NonSerialized]
	private float lastTickTime;

	[NonSerialized]
	private float lastStallTime;

	[NonSerialized]
	private float stallProtectionTime;

	[NonSerialized]
	private float lastInputTime;

	[NonSerialized]
	private float tutorialKickTime;

	[NonSerialized]
	public ItemId? restraintItemId;

	private PlayerTick lastReceivedTick = new PlayerTick();

	private float tickDeltaTime;

	private bool tickNeedsFinalizing;

	private readonly TimeAverageValue ticksPerSecond = new TimeAverageValue();

	private readonly TimeAverageValue rawTicksPerSecond = new TimeAverageValue();

	private readonly TickInterpolator tickInterpolator = new TickInterpolator();

	public Deque<Vector3> eyeHistory = new Deque<Vector3>();

	public TickHistory tickHistory = new TickHistory();

	private float startTutorialCooldown;

	private float nextUnderwearValidationTime;

	private uint lastValidUnderwearSkin;

	private float woundedDuration;

	private float lastWoundedStartTime = float.NegativeInfinity;

	private float healingWhileCrawling;

	private bool woundedByFallDamage;

	private const float INCAPACITATED_HEALTH_MIN = 2f;

	private const float INCAPACITATED_HEALTH_MAX = 6f;

	public const int MaxBotIdRange = 10000000;

	[Header("BasePlayer")]
	public GameObjectRef fallDamageEffect;

	public GameObjectRef drownEffect;

	[InspectorFlags]
	public PlayerFlags playerFlags;

	private HiddenValue<PlayerEyes> eyesValue = Facepunch.Pool.Get<HiddenValue<PlayerEyes>>();

	private HiddenValue<PlayerInventory> inventoryValue = Facepunch.Pool.Get<HiddenValue<PlayerInventory>>();

	[NonSerialized]
	public PlayerBlueprints blueprints;

	[NonSerialized]
	public PlayerMetabolism metabolism;

	[NonSerialized]
	public PlayerModifiers modifiers;

	private HiddenValue<CapsuleCollider> colliderValue = Facepunch.Pool.Get<HiddenValue<CapsuleCollider>>();

	public PlayerBelt Belt;

	private Rigidbody playerRigidbody;

	[NonSerialized]
	public EncryptedValue<ulong> userID = 0uL;

	[NonSerialized]
	public string UserIDString;

	[NonSerialized]
	public int gamemodeteam = -1;

	[NonSerialized]
	public int reputation;

	protected string _displayName;

	private string _lastSetName;

	public const float crouchSpeed = 1.7f;

	public const float walkSpeed = 2.8f;

	public const float runSpeed = 5.5f;

	public const float crawlSpeed = 0.72f;

	private CapsuleColliderInfo playerColliderStanding;

	private CapsuleColliderInfo playerColliderDucked;

	private CapsuleColliderInfo playerColliderCrawling;

	private CapsuleColliderInfo playerColliderLyingDown;

	private ProtectionProperties cachedProtection;

	private float nextColliderRefreshTime = -1f;

	public float weaponMoveSpeedScale = 1f;

	public bool clothingBlocksAiming;

	public float clothingMoveSpeedReduction;

	public float clothingWaterSpeedBonus;

	public float clothingAccuracyBonus;

	public bool equippingBlocked;

	public float eggVision;

	private PhoneController activeTelephone;

	public BaseEntity designingAIEntity;

	public Translate.Phrase LootPanelTitle => displayName;

	public bool IsReceivingSnapshot => HasPlayerFlag(PlayerFlags.ReceivingSnapshot);

	public bool IsAdmin => HasPlayerFlag(PlayerFlags.IsAdmin);

	public bool IsDeveloper => HasPlayerFlag(PlayerFlags.IsDeveloper);

	public bool IsInCreativeMode
	{
		get
		{
			if (!Creative.allUsers)
			{
				return HasPlayerFlag(PlayerFlags.CreativeMode);
			}
			return true;
		}
	}

	public bool UnlockAllSkins
	{
		get
		{
			if (!IsDeveloper)
			{
				return false;
			}
			if (base.isServer)
			{
				return net.connection.info.GetBool("client.unlock_all_skins");
			}
			return false;
		}
	}

	public bool IsAiming => HasPlayerFlag(PlayerFlags.Aiming);

	public bool IsFlying
	{
		get
		{
			if (modelState == null)
			{
				return false;
			}
			return modelState.flying;
		}
	}

	public bool IsConnected
	{
		get
		{
			if (base.isServer)
			{
				if (Network.Net.sv == null)
				{
					return false;
				}
				if (net == null)
				{
					return false;
				}
				if (net.connection == null)
				{
					return false;
				}
				return true;
			}
			return false;
		}
	}

	public bool IsInTutorial => HasPlayerFlag(PlayerFlags.IsInTutorial);

	public bool IsRestrained
	{
		get
		{
			if (IsAlive())
			{
				return HasPlayerFlag(PlayerFlags.IsRestrained);
			}
			return false;
		}
	}

	public bool IsRestrainedOrSurrendering
	{
		get
		{
			if (!IsRestrained)
			{
				return CurrentGestureIsSurrendering;
			}
			return true;
		}
	}

	public bool InGesture
	{
		get
		{
			if (currentGesture != null)
			{
				if (!((float)gestureFinishedTime > 0f))
				{
					return currentGesture.animationType == GestureConfig.AnimationType.Loop;
				}
				return true;
			}
			return false;
		}
	}

	private bool CurrentGestureBlocksMovement
	{
		get
		{
			if (InGesture)
			{
				return currentGesture.movementMode == GestureConfig.MovementCapabilities.NoMovement;
			}
			return false;
		}
	}

	public bool CurrentGestureIsDance
	{
		get
		{
			if (InGesture)
			{
				return currentGesture.actionType == GestureConfig.GestureActionType.DanceAchievement;
			}
			return false;
		}
	}

	public bool CurrentGestureIsFullBody
	{
		get
		{
			if (InGesture)
			{
				return currentGesture.playerModelLayer == GestureConfig.PlayerModelLayer.FullBody;
			}
			return false;
		}
	}

	public bool CurrentGestureIsUpperBody
	{
		get
		{
			if (InGesture)
			{
				return currentGesture.playerModelLayer == GestureConfig.PlayerModelLayer.UpperBody;
			}
			return false;
		}
	}

	public bool CurrentGestureIsSurrendering
	{
		get
		{
			if (InGesture)
			{
				return currentGesture.actionType == GestureConfig.GestureActionType.Surrender;
			}
			return false;
		}
	}

	private bool InGestureCancelCooldown => (float)blockHeldInputTimer < 0.5f;

	public RelationshipManager.PlayerTeam Team
	{
		get
		{
			if (RelationshipManager.ServerInstance == null)
			{
				return null;
			}
			return RelationshipManager.ServerInstance.FindTeam(currentTeam);
		}
	}

	public MapNote ServerCurrentDeathNote
	{
		get
		{
			return State.deathMarker;
		}
		set
		{
			State.deathMarker = value;
		}
	}

	public bool HasPendingFollowupMission => IsInvoking(AssignFollowUpMission);

	public ModelState modelStateTick { get; private set; }

	public bool isMounted => mounted.IsValid(base.isServer);

	public bool isMountingHidingWeapon
	{
		get
		{
			if (isMounted)
			{
				return GetMounted().CanHoldItems();
			}
			return false;
		}
	}

	private int TotalPingCount
	{
		get
		{
			if (State.pings == null)
			{
				return 0;
			}
			return State.pings.Count;
		}
	}

	public PlayerState State
	{
		get
		{
			if ((ulong)userID == 0L)
			{
				throw new InvalidOperationException("Cannot get player state without a SteamID");
			}
			return SingletonComponent<ServerMgr>.Instance.playerStateManager.Get(userID);
		}
	}

	public string WipeId
	{
		get
		{
			if (_wipeId == null)
			{
				_wipeId = SingletonComponent<ServerMgr>.Instance.persistance.GetUserWipeId(userID);
			}
			return _wipeId;
		}
	}

	public virtual BaseNpc.AiStatistics.FamilyEnum Family => BaseNpc.AiStatistics.FamilyEnum.Player;

	protected override float PositionTickRate => -1f;

	public int DebugMapMarkerIndex { get; set; }

	public bool PlayHeavyLandingAnimation { get; set; }

	public ServerOcclusion.Grid Chunk => ServerOcclusion.GetGrid(GetCenter(ducked: false));

	public ServerOcclusion.SubGrid SubGrid => ServerOcclusion.GetSubGrid(GetCenter(ducked: false));

	public Vector3? RcEntityPosition { get; private set; }

	public Vector3 estimatedVelocity { get; private set; }

	public Vector3 estimatedVelocityClamped => Vector3.ClampMagnitude(estimatedVelocity, GetMaxSpeed());

	public float estimatedSpeed { get; private set; }

	public float estimatedSpeed2D { get; private set; }

	public int secondsConnected { get; private set; }

	public float desyncTimeRaw { get; private set; }

	public float desyncTimeClamped { get; private set; }

	public float secondsSleeping
	{
		get
		{
			if (sleepStartTime == -1f || !IsSleeping())
			{
				return 0f;
			}
			return UnityEngine.Time.time - sleepStartTime;
		}
	}

	public static IEnumerable<BasePlayer> allPlayerList
	{
		get
		{
			foreach (BasePlayer sleepingPlayer in sleepingPlayerList)
			{
				yield return sleepingPlayer;
			}
			foreach (BasePlayer activePlayer in activePlayerList)
			{
				yield return activePlayer;
			}
		}
	}

	public float currentCraftLevel
	{
		get
		{
			if (triggers == null)
			{
				_cachedWorkbench = null;
				return 0f;
			}
			if (nextCheckTime > UnityEngine.Time.realtimeSinceStartup)
			{
				return cachedCraftLevel;
			}
			_cachedWorkbench = null;
			nextCheckTime = UnityEngine.Time.realtimeSinceStartup + UnityEngine.Random.Range(0.4f, 0.5f);
			float num = 0f;
			for (int i = 0; i < triggers.Count; i++)
			{
				TriggerWorkbench triggerWorkbench = triggers[i] as TriggerWorkbench;
				if (!(triggerWorkbench == null) && !(triggerWorkbench.parentBench == null) && triggerWorkbench.parentBench.IsVisible(eyes.position))
				{
					_cachedWorkbench = triggerWorkbench.parentBench;
					float num2 = triggerWorkbench.WorkbenchLevel();
					if (num2 > num)
					{
						num = num2;
					}
				}
			}
			cachedCraftLevel = num;
			return num;
		}
	}

	public float currentComfort
	{
		get
		{
			float num = 0f;
			if (isMounted)
			{
				num = GetMounted().GetComfort();
			}
			if (triggers == null)
			{
				return num;
			}
			for (int i = 0; i < triggers.Count; i++)
			{
				TriggerComfort triggerComfort = triggers[i] as TriggerComfort;
				if (!(triggerComfort == null))
				{
					float num2 = triggerComfort.CalculateComfort(base.transform.position, this);
					if (num2 > num)
					{
						num = num2;
					}
				}
			}
			return num;
		}
	}

	public PersistantPlayer PersistantPlayerInfo
	{
		get
		{
			if (cachedPersistantPlayer == null)
			{
				cachedPersistantPlayer = SingletonComponent<ServerMgr>.Instance.persistance.GetPlayerInfo(userID);
			}
			return cachedPersistantPlayer;
		}
		set
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			cachedPersistantPlayer = value;
			SingletonComponent<ServerMgr>.Instance.persistance.SetPlayerInfo(userID, value);
		}
	}

	public bool hasPreviousLife => previousLifeStory != null;

	public int currentTimeCategory { get; private set; }

	public bool IsBeingSpectated { get; private set; }

	public InputState serverInput { get; private set; } = new InputState();


	public float timeSinceLastTick
	{
		get
		{
			if (lastTickTime == 0f)
			{
				return 0f;
			}
			return UnityEngine.Time.time - lastTickTime;
		}
	}

	public float timeSinceLastStall
	{
		get
		{
			if (lastStallTime == 0f)
			{
				return 60f;
			}
			return UnityEngine.Time.time - lastStallTime;
		}
	}

	public float IdleTime
	{
		get
		{
			if (lastInputTime == 0f)
			{
				return 0f;
			}
			return UnityEngine.Time.time - lastInputTime;
		}
	}

	public bool isStalled
	{
		get
		{
			if (IsDead() || IsSleeping())
			{
				lastStallTime = 0f;
				return false;
			}
			if (stallProtectionTime <= 0f && timeSinceLastTick != 0f && timeSinceLastTick > ConVar.AntiHack.rpcstallthreshold)
			{
				lastStallTime = UnityEngine.Time.time;
				return true;
			}
			return false;
		}
	}

	public bool wasStalled
	{
		get
		{
			if (stallProtectionTime <= 0f)
			{
				if (!isStalled)
				{
					return timeSinceLastStall < ConVar.AntiHack.rpcstallfade;
				}
				return true;
			}
			return false;
		}
	}

	public Vector3 tickViewAngles { get; private set; }

	public Vector3 tickMouseDelta { get; private set; }

	public int tickHistoryCapacity => Mathf.Max(1, Mathf.CeilToInt((float)ticksPerSecond.Calculate() * ConVar.AntiHack.tickhistorytime));

	public Matrix4x4 tickHistoryMatrix
	{
		get
		{
			if (!base.transform.parent)
			{
				return Matrix4x4.identity;
			}
			return base.transform.parent.localToWorldMatrix;
		}
	}

	public ulong rawTickCount { get; set; }

	public TutorialItemAllowance CurrentTutorialAllowance { get; private set; }

	public float TimeSinceWoundedStarted => UnityEngine.Time.realtimeSinceStartup - lastWoundedStartTime;

	public Network.Connection Connection
	{
		get
		{
			if (net != null)
			{
				return net.connection;
			}
			return null;
		}
	}

	public bool IsBot => (ulong)userID < 10000000;

	public PlayerEyes eyes
	{
		get
		{
			return eyesValue?.Get();
		}
		set
		{
			eyesValue.Set(value);
		}
	}

	public PlayerInventory inventory => inventoryValue?.Get();

	private CapsuleCollider playerCollider => colliderValue?.Get();

	public virtual string displayName
	{
		get
		{
			return NameHelper.Get(userID, _displayName, base.isClient);
		}
		set
		{
			if (!(_lastSetName == value))
			{
				_lastSetName = value;
				_displayName = SanitizePlayerNameString(value, userID);
			}
		}
	}

	public override TraitFlag Traits => base.Traits | TraitFlag.Human | TraitFlag.Food | TraitFlag.Meat | TraitFlag.Alive;

	public bool HasActiveTelephone => activeTelephone != null;

	public bool IsDesigningAI => designingAIEntity != null;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("BasePlayer.OnRpcMessage"))
		{
			if (rpc == 935768323 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - ClientKeepConnectionAlive ");
				}
				using (TimeWarning.New("ClientKeepConnectionAlive"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.FromOwner.Test(935768323u, "ClientKeepConnectionAlive", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg2 = rPCMessage;
							ClientKeepConnectionAlive(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in ClientKeepConnectionAlive");
					}
				}
				return true;
			}
			if (rpc == 3782818894u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - ClientLoadingComplete ");
				}
				using (TimeWarning.New("ClientLoadingComplete"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.FromOwner.Test(3782818894u, "ClientLoadingComplete", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg3 = rPCMessage;
							ClientLoadingComplete(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in ClientLoadingComplete");
					}
				}
				return true;
			}
			if (rpc == 1497207530 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - IssuePetCommand ");
				}
				using (TimeWarning.New("IssuePetCommand"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg4 = rPCMessage;
							IssuePetCommand(msg4);
						}
					}
					catch (Exception exception3)
					{
						Debug.LogException(exception3);
						player.Kick("RPC Error in IssuePetCommand");
					}
				}
				return true;
			}
			if (rpc == 2041023702 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - IssuePetCommandRaycast ");
				}
				using (TimeWarning.New("IssuePetCommandRaycast"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg5 = rPCMessage;
							IssuePetCommandRaycast(msg5);
						}
					}
					catch (Exception exception4)
					{
						Debug.LogException(exception4);
						player.Kick("RPC Error in IssuePetCommandRaycast");
					}
				}
				return true;
			}
			if (rpc == 495414158 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - NotifyDebugCameraEnded ");
				}
				using (TimeWarning.New("NotifyDebugCameraEnded"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg6 = rPCMessage;
							NotifyDebugCameraEnded(msg6);
						}
					}
					catch (Exception exception5)
					{
						Debug.LogException(exception5);
						player.Kick("RPC Error in NotifyDebugCameraEnded");
					}
				}
				return true;
			}
			if (rpc == 3441821928u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - OnFeedbackReport ");
				}
				using (TimeWarning.New("OnFeedbackReport"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3441821928u, "OnFeedbackReport", this, player, 1uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(3441821928u, "OnFeedbackReport", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg7 = rPCMessage;
							OnFeedbackReport(msg7);
						}
					}
					catch (Exception exception6)
					{
						Debug.LogException(exception6);
						player.Kick("RPC Error in OnFeedbackReport");
					}
				}
				return true;
			}
			if (rpc == 1998170713 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - OnPlayerLanded ");
				}
				using (TimeWarning.New("OnPlayerLanded"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.FromOwner.Test(1998170713u, "OnPlayerLanded", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg8 = rPCMessage;
							OnPlayerLanded(msg8);
						}
					}
					catch (Exception exception7)
					{
						Debug.LogException(exception7);
						player.Kick("RPC Error in OnPlayerLanded");
					}
				}
				return true;
			}
			if (rpc == 2147041557 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - OnPlayerReported ");
				}
				using (TimeWarning.New("OnPlayerReported"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(2147041557u, "OnPlayerReported", this, player, 1uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(2147041557u, "OnPlayerReported", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg9 = rPCMessage;
							OnPlayerReported(msg9);
						}
					}
					catch (Exception exception8)
					{
						Debug.LogException(exception8);
						player.Kick("RPC Error in OnPlayerReported");
					}
				}
				return true;
			}
			if (rpc == 363681694 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - OnProjectileAttack ");
				}
				using (TimeWarning.New("OnProjectileAttack"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.FromOwner.Test(363681694u, "OnProjectileAttack", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg10 = rPCMessage;
							OnProjectileAttack(msg10);
						}
					}
					catch (Exception exception9)
					{
						Debug.LogException(exception9);
						player.Kick("RPC Error in OnProjectileAttack");
					}
				}
				return true;
			}
			if (rpc == 1500391289 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - OnProjectileRicochet ");
				}
				using (TimeWarning.New("OnProjectileRicochet"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.FromOwner.Test(1500391289u, "OnProjectileRicochet", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg11 = rPCMessage;
							OnProjectileRicochet(msg11);
						}
					}
					catch (Exception exception10)
					{
						Debug.LogException(exception10);
						player.Kick("RPC Error in OnProjectileRicochet");
					}
				}
				return true;
			}
			if (rpc == 2324190493u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - OnProjectileUpdate ");
				}
				using (TimeWarning.New("OnProjectileUpdate"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.FromOwner.Test(2324190493u, "OnProjectileUpdate", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg12 = rPCMessage;
							OnProjectileUpdate(msg12);
						}
					}
					catch (Exception exception11)
					{
						Debug.LogException(exception11);
						player.Kick("RPC Error in OnProjectileUpdate");
					}
				}
				return true;
			}
			if (rpc == 3167788018u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - PerformanceReport ");
				}
				using (TimeWarning.New("PerformanceReport"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3167788018u, "PerformanceReport", this, player, 1uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(3167788018u, "PerformanceReport", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg13 = rPCMessage;
							PerformanceReport(msg13);
						}
					}
					catch (Exception exception12)
					{
						Debug.LogException(exception12);
						player.Kick("RPC Error in PerformanceReport");
					}
				}
				return true;
			}
			if (rpc == 420048204 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - PerformanceReport_Frametime ");
				}
				using (TimeWarning.New("PerformanceReport_Frametime"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(420048204u, "PerformanceReport_Frametime", this, player, 1uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(420048204u, "PerformanceReport_Frametime", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg14 = rPCMessage;
							PerformanceReport_Frametime(msg14);
						}
					}
					catch (Exception exception13)
					{
						Debug.LogException(exception13);
						player.Kick("RPC Error in PerformanceReport_Frametime");
					}
				}
				return true;
			}
			if (rpc == 4081064578u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - PlayerRequestedTutorialStart ");
				}
				using (TimeWarning.New("PlayerRequestedTutorialStart"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(4081064578u, "PlayerRequestedTutorialStart", this, player, 1uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(4081064578u, "PlayerRequestedTutorialStart", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg15 = rPCMessage;
							PlayerRequestedTutorialStart(msg15);
						}
					}
					catch (Exception exception14)
					{
						Debug.LogException(exception14);
						player.Kick("RPC Error in PlayerRequestedTutorialStart");
					}
				}
				return true;
			}
			if (rpc == 56793194 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RequestJoinGesture ");
				}
				using (TimeWarning.New("RequestJoinGesture"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(56793194u, "RequestJoinGesture", this, player, 3f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg16 = rPCMessage;
							RequestJoinGesture(msg16);
						}
					}
					catch (Exception exception15)
					{
						Debug.LogException(exception15);
						player.Kick("RPC Error in RequestJoinGesture");
					}
				}
				return true;
			}
			if (rpc == 1024003327 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RequestParachuteDeploy ");
				}
				using (TimeWarning.New("RequestParachuteDeploy"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(1024003327u, "RequestParachuteDeploy", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(1024003327u, "RequestParachuteDeploy", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg17 = rPCMessage;
							RequestParachuteDeploy(msg17);
						}
					}
					catch (Exception exception16)
					{
						Debug.LogException(exception16);
						player.Kick("RPC Error in RequestParachuteDeploy");
					}
				}
				return true;
			}
			if (rpc == 52352806 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RequestRespawnInformation ");
				}
				using (TimeWarning.New("RequestRespawnInformation"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(52352806u, "RequestRespawnInformation", this, player, 1uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(52352806u, "RequestRespawnInformation", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg18 = rPCMessage;
							RequestRespawnInformation(msg18);
						}
					}
					catch (Exception exception17)
					{
						Debug.LogException(exception17);
						player.Kick("RPC Error in RequestRespawnInformation");
					}
				}
				return true;
			}
			if (rpc == 1774681338 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RequestServerEmoji ");
				}
				using (TimeWarning.New("RequestServerEmoji"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(1774681338u, "RequestServerEmoji", this, player, 1uL))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RequestServerEmoji();
						}
					}
					catch (Exception exception18)
					{
						Debug.LogException(exception18);
						player.Kick("RPC Error in RequestServerEmoji");
					}
				}
				return true;
			}
			if (rpc == 970468557 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_Assist ");
				}
				using (TimeWarning.New("RPC_Assist"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(970468557u, "RPC_Assist", this, player, 3f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg19 = rPCMessage;
							RPC_Assist(msg19);
						}
					}
					catch (Exception exception19)
					{
						Debug.LogException(exception19);
						player.Kick("RPC Error in RPC_Assist");
					}
				}
				return true;
			}
			if (rpc == 3263238541u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_KeepAlive ");
				}
				using (TimeWarning.New("RPC_KeepAlive"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(3263238541u, "RPC_KeepAlive", this, player, 3f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg20 = rPCMessage;
							RPC_KeepAlive(msg20);
						}
					}
					catch (Exception exception20)
					{
						Debug.LogException(exception20);
						player.Kick("RPC Error in RPC_KeepAlive");
					}
				}
				return true;
			}
			if (rpc == 3692395068u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_LootPlayer ");
				}
				using (TimeWarning.New("RPC_LootPlayer"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(3692395068u, "RPC_LootPlayer", this, player, 3f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg21 = rPCMessage;
							RPC_LootPlayer(msg21);
						}
					}
					catch (Exception exception21)
					{
						Debug.LogException(exception21);
						player.Kick("RPC Error in RPC_LootPlayer");
					}
				}
				return true;
			}
			if (rpc == 2659547586u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_ReqDoRestrainedPush ");
				}
				using (TimeWarning.New("RPC_ReqDoRestrainedPush"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(2659547586u, "RPC_ReqDoRestrainedPush", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.IsVisible.Test(2659547586u, "RPC_ReqDoRestrainedPush", this, player, 3f))
						{
							return true;
						}
						if (!RPC_Server.MaxDistance.Test(2659547586u, "RPC_ReqDoRestrainedPush", this, player, 3f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage rpc2 = rPCMessage;
							RPC_ReqDoRestrainedPush(rpc2);
						}
					}
					catch (Exception exception22)
					{
						Debug.LogException(exception22);
						player.Kick("RPC Error in RPC_ReqDoRestrainedPush");
					}
				}
				return true;
			}
			if (rpc == 3974264977u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_ReqEquipHood ");
				}
				using (TimeWarning.New("RPC_ReqEquipHood"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3974264977u, "RPC_ReqEquipHood", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.IsVisible.Test(3974264977u, "RPC_ReqEquipHood", this, player, 3f))
						{
							return true;
						}
						if (!RPC_Server.MaxDistance.Test(3974264977u, "RPC_ReqEquipHood", this, player, 3f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage rpc3 = rPCMessage;
							RPC_ReqEquipHood(rpc3);
						}
					}
					catch (Exception exception23)
					{
						Debug.LogException(exception23);
						player.Kick("RPC Error in RPC_ReqEquipHood");
					}
				}
				return true;
			}
			if (rpc == 4144905368u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_ReqForceMountNearest ");
				}
				using (TimeWarning.New("RPC_ReqForceMountNearest"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(4144905368u, "RPC_ReqForceMountNearest", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.IsVisible.Test(4144905368u, "RPC_ReqForceMountNearest", this, player, 3f))
						{
							return true;
						}
						if (!RPC_Server.MaxDistance.Test(4144905368u, "RPC_ReqForceMountNearest", this, player, 3f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage rpc4 = rPCMessage;
							RPC_ReqForceMountNearest(rpc4);
						}
					}
					catch (Exception exception24)
					{
						Debug.LogException(exception24);
						player.Kick("RPC Error in RPC_ReqForceMountNearest");
					}
				}
				return true;
			}
			if (rpc == 3816898909u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_ReqForceSwapSeat ");
				}
				using (TimeWarning.New("RPC_ReqForceSwapSeat"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3816898909u, "RPC_ReqForceSwapSeat", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.IsVisible.Test(3816898909u, "RPC_ReqForceSwapSeat", this, player, 3f))
						{
							return true;
						}
						if (!RPC_Server.MaxDistance.Test(3816898909u, "RPC_ReqForceSwapSeat", this, player, 3f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage rpc5 = rPCMessage;
							RPC_ReqForceSwapSeat(rpc5);
						}
					}
					catch (Exception exception25)
					{
						Debug.LogException(exception25);
						player.Kick("RPC Error in RPC_ReqForceSwapSeat");
					}
				}
				return true;
			}
			if (rpc == 626234931 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_ReqRemoveCuffs ");
				}
				using (TimeWarning.New("RPC_ReqRemoveCuffs"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(626234931u, "RPC_ReqRemoveCuffs", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.IsVisible.Test(626234931u, "RPC_ReqRemoveCuffs", this, player, 3f))
						{
							return true;
						}
						if (!RPC_Server.MaxDistance.Test(626234931u, "RPC_ReqRemoveCuffs", this, player, 3f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage rpc6 = rPCMessage;
							RPC_ReqRemoveCuffs(rpc6);
						}
					}
					catch (Exception exception26)
					{
						Debug.LogException(exception26);
						player.Kick("RPC Error in RPC_ReqRemoveCuffs");
					}
				}
				return true;
			}
			if (rpc == 2289764809u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_ReqRemoveHood ");
				}
				using (TimeWarning.New("RPC_ReqRemoveHood"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(2289764809u, "RPC_ReqRemoveHood", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.IsVisible.Test(2289764809u, "RPC_ReqRemoveHood", this, player, 3f))
						{
							return true;
						}
						if (!RPC_Server.MaxDistance.Test(2289764809u, "RPC_ReqRemoveHood", this, player, 3f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage rpc7 = rPCMessage;
							RPC_ReqRemoveHood(rpc7);
						}
					}
					catch (Exception exception27)
					{
						Debug.LogException(exception27);
						player.Kick("RPC Error in RPC_ReqRemoveHood");
					}
				}
				return true;
			}
			if (rpc == 1539133504 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_StartClimb ");
				}
				using (TimeWarning.New("RPC_StartClimb"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg22 = rPCMessage;
							RPC_StartClimb(msg22);
						}
					}
					catch (Exception exception28)
					{
						Debug.LogException(exception28);
						player.Kick("RPC Error in RPC_StartClimb");
					}
				}
				return true;
			}
			if (rpc == 1777651896 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SelectedRPSOption ");
				}
				using (TimeWarning.New("SelectedRPSOption"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg23 = rPCMessage;
							SelectedRPSOption(msg23);
						}
					}
					catch (Exception exception29)
					{
						Debug.LogException(exception29);
						player.Kick("RPC Error in SelectedRPSOption");
					}
				}
				return true;
			}
			if (rpc == 3047177092u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_AddMarker ");
				}
				using (TimeWarning.New("Server_AddMarker"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3047177092u, "Server_AddMarker", this, player, 8uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(3047177092u, "Server_AddMarker", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg24 = rPCMessage;
							Server_AddMarker(msg24);
						}
					}
					catch (Exception exception30)
					{
						Debug.LogException(exception30);
						player.Kick("RPC Error in Server_AddMarker");
					}
				}
				return true;
			}
			if (rpc == 3618659425u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_AddPing ");
				}
				using (TimeWarning.New("Server_AddPing"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3618659425u, "Server_AddPing", this, player, 3uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(3618659425u, "Server_AddPing", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg25 = rPCMessage;
							Server_AddPing(msg25);
						}
					}
					catch (Exception exception31)
					{
						Debug.LogException(exception31);
						player.Kick("RPC Error in Server_AddPing");
					}
				}
				return true;
			}
			if (rpc == 1005040107 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_CancelGesture ");
				}
				using (TimeWarning.New("Server_CancelGesture"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(1005040107u, "Server_CancelGesture", this, player, 10uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(1005040107u, "Server_CancelGesture", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							Server_CancelGesture();
						}
					}
					catch (Exception exception32)
					{
						Debug.LogException(exception32);
						player.Kick("RPC Error in Server_CancelGesture");
					}
				}
				return true;
			}
			if (rpc == 706157120 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_ClearMapMarkers ");
				}
				using (TimeWarning.New("Server_ClearMapMarkers"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(706157120u, "Server_ClearMapMarkers", this, player, 1uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(706157120u, "Server_ClearMapMarkers", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg26 = rPCMessage;
							Server_ClearMapMarkers(msg26);
						}
					}
					catch (Exception exception33)
					{
						Debug.LogException(exception33);
						player.Kick("RPC Error in Server_ClearMapMarkers");
					}
				}
				return true;
			}
			if (rpc == 1032755717 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_RemovePing ");
				}
				using (TimeWarning.New("Server_RemovePing"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(1032755717u, "Server_RemovePing", this, player, 3uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(1032755717u, "Server_RemovePing", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg27 = rPCMessage;
							Server_RemovePing(msg27);
						}
					}
					catch (Exception exception34)
					{
						Debug.LogException(exception34);
						player.Kick("RPC Error in Server_RemovePing");
					}
				}
				return true;
			}
			if (rpc == 31713840 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_RemovePointOfInterest ");
				}
				using (TimeWarning.New("Server_RemovePointOfInterest"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(31713840u, "Server_RemovePointOfInterest", this, player, 10uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(31713840u, "Server_RemovePointOfInterest", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg28 = rPCMessage;
							Server_RemovePointOfInterest(msg28);
						}
					}
					catch (Exception exception35)
					{
						Debug.LogException(exception35);
						player.Kick("RPC Error in Server_RemovePointOfInterest");
					}
				}
				return true;
			}
			if (rpc == 2567683804u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_RequestMarkers ");
				}
				using (TimeWarning.New("Server_RequestMarkers"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(2567683804u, "Server_RequestMarkers", this, player, 1uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(2567683804u, "Server_RequestMarkers", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg29 = rPCMessage;
							Server_RequestMarkers(msg29);
						}
					}
					catch (Exception exception36)
					{
						Debug.LogException(exception36);
						player.Kick("RPC Error in Server_RequestMarkers");
					}
				}
				return true;
			}
			if (rpc == 1572722245 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_StartGesture ");
				}
				using (TimeWarning.New("Server_StartGesture"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(1572722245u, "Server_StartGesture", this, player, 1uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(1572722245u, "Server_StartGesture", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg30 = rPCMessage;
							Server_StartGesture(msg30);
						}
					}
					catch (Exception exception37)
					{
						Debug.LogException(exception37);
						player.Kick("RPC Error in Server_StartGesture");
					}
				}
				return true;
			}
			if (rpc == 1180369886 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_UpdateMarker ");
				}
				using (TimeWarning.New("Server_UpdateMarker"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(1180369886u, "Server_UpdateMarker", this, player, 1uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(1180369886u, "Server_UpdateMarker", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg31 = rPCMessage;
							Server_UpdateMarker(msg31);
						}
					}
					catch (Exception exception38)
					{
						Debug.LogException(exception38);
						player.Kick("RPC Error in Server_UpdateMarker");
					}
				}
				return true;
			}
			if (rpc == 2192544725u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - ServerRequestEmojiData ");
				}
				using (TimeWarning.New("ServerRequestEmojiData"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(2192544725u, "ServerRequestEmojiData", this, player, 3uL))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg32 = rPCMessage;
							ServerRequestEmojiData(msg32);
						}
					}
					catch (Exception exception39)
					{
						Debug.LogException(exception39);
						player.Kick("RPC Error in ServerRequestEmojiData");
					}
				}
				return true;
			}
			if (rpc == 3635568749u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - ServerRPC_UnderwearChange ");
				}
				using (TimeWarning.New("ServerRPC_UnderwearChange"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg33 = rPCMessage;
							ServerRPC_UnderwearChange(msg33);
						}
					}
					catch (Exception exception40)
					{
						Debug.LogException(exception40);
						player.Kick("RPC Error in ServerRPC_UnderwearChange");
					}
				}
				return true;
			}
			if (rpc == 3222472445u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - StartTutorial ");
				}
				using (TimeWarning.New("StartTutorial"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg34 = rPCMessage;
							StartTutorial(msg34);
						}
					}
					catch (Exception exception41)
					{
						Debug.LogException(exception41);
						player.Kick("RPC Error in StartTutorial");
					}
				}
				return true;
			}
			if (rpc == 970114602 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SV_Drink ");
				}
				using (TimeWarning.New("SV_Drink"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg35 = rPCMessage;
							SV_Drink(msg35);
						}
					}
					catch (Exception exception42)
					{
						Debug.LogException(exception42);
						player.Kick("RPC Error in SV_Drink");
					}
				}
				return true;
			}
			if (rpc == 1361044246 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - UpdateSpectatePositionFromDebugCamera ");
				}
				using (TimeWarning.New("UpdateSpectatePositionFromDebugCamera"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(1361044246u, "UpdateSpectatePositionFromDebugCamera", this, player, 10uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(1361044246u, "UpdateSpectatePositionFromDebugCamera", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg36 = rPCMessage;
							UpdateSpectatePositionFromDebugCamera(msg36);
						}
					}
					catch (Exception exception43)
					{
						Debug.LogException(exception43);
						player.Kick("RPC Error in UpdateSpectatePositionFromDebugCamera");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public void ToggleShowFSMStateDebugInfo()
	{
		if (!IsInvoking(ShowStateDebugInfo))
		{
			InvokeRepeating(ShowStateDebugInfo, 0f, 0.1f);
		}
		else
		{
			CancelInvoke(ShowStateDebugInfo);
		}
	}

	private void ShowStateDebugInfo()
	{
		FSMComponent.ShowDebugInfoAroundLocation(this);
	}

	public bool TriggeredAntiHack(float seconds = 1f, float score = float.PositiveInfinity)
	{
		if (!(UnityEngine.Time.realtimeSinceStartup - lastViolationTime < seconds))
		{
			return violationLevel > score;
		}
		return true;
	}

	public bool UsedAdminCheat(float seconds = 2f)
	{
		return UnityEngine.Time.realtimeSinceStartup - lastAdminCheatTime < seconds;
	}

	public void PauseVehicleNoClipDetection(float seconds = 1f)
	{
		vehiclePauseTime = Mathf.Max(vehiclePauseTime, seconds);
	}

	public void PauseFlyHackDetection(float seconds = 1f)
	{
		flyhackPauseTime = Mathf.Max(flyhackPauseTime, seconds);
	}

	public void PauseSpeedHackDetection(float seconds = 1f)
	{
		speedhackPauseTime = Mathf.Max(speedhackPauseTime, seconds);
	}

	public void PauseTickDistanceDetection(float seconds = 1f)
	{
		tickDistancePausetime = Mathf.Max(tickDistancePausetime, seconds);
	}

	public bool RecentlyInAir(float seconds = 1f)
	{
		return UnityEngine.Time.realtimeSinceStartup - lastInAirTime < seconds;
	}

	public int GetAntiHackKicks()
	{
		return AntiHack.GetKickRecord(this);
	}

	public void ResetAntiHack()
	{
		violationLevel = 0f;
		lastViolationTime = 0f;
		lastAdminCheatTime = 0f;
		speedhackPauseTime = 0f;
		speedhackDistance = 0f;
		flyhackPauseTime = 0f;
		flyhackDistanceVertical = 0f;
		flyhackDistanceHorizontal = 0f;
		tickDistancePausetime = 0f;
		lastInAirTime = 0f;
		rpcHistory.Clear();
	}

	public bool CanModifyClan()
	{
		if (!Clan.editsRequireClanTable)
		{
			return true;
		}
		if (base.isServer)
		{
			if (triggers == null || ClanManager.ServerInstance == null)
			{
				return false;
			}
			foreach (TriggerBase trigger in triggers)
			{
				if (trigger is TriggerClanModify)
				{
					return true;
				}
			}
			return false;
		}
		return false;
	}

	public void LoadClanInfo()
	{
		ClanManager clanManager = ClanManager.ServerInstance;
		if (!(clanManager == null))
		{
			LoadClanInfoImpl();
		}
		async void LoadClanInfoImpl()
		{
			try
			{
				ClanValueResult<IClan> clanValueResult = await clanManager.Backend.GetByMember(userID);
				if (!clanValueResult.IsSuccess)
				{
					if (clanValueResult.Result != ClanResult.NoClan)
					{
						Debug.LogError($"Failed to find clan for {userID.Get()}: {clanValueResult.Result}");
						Invoke(LoadClanInfo, 45 + UnityEngine.Random.Range(0, 30));
						return;
					}
					serverClan = null;
					clanId = 0L;
				}
				else
				{
					serverClan = clanValueResult.Value;
					clanId = serverClan.ClanId;
				}
				SendNetworkUpdate();
				if (net?.connection != null)
				{
					UpdateClanLastSeen();
					if (clanId != 0L)
					{
						clanManager.ClanMemberConnectionsChanged(clanId);
					}
				}
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}
	}

	public void UpdateClanLastSeen()
	{
		ClanManager clanManager = ClanManager.ServerInstance;
		if (!(clanManager == null) && clanId != 0L)
		{
			UpdateClanLastSeenImpl();
		}
		async void UpdateClanLastSeenImpl()
		{
			_ = 1;
			try
			{
				ClanValueResult<IClan> clanValueResult = await clanManager.Backend.Get(clanId);
				if (!clanValueResult.IsSuccess)
				{
					LoadClanInfo();
				}
				else
				{
					ClanResult clanResult = await clanValueResult.Value.UpdateLastSeen(userID);
					if (clanResult != ClanResult.Success)
					{
						Debug.LogWarning($"Couldn't update clan last seen for {userID.Get()}: {clanResult}");
					}
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"Failed to update clan last seen for {userID.Get()}: {arg}");
			}
		}
	}

	public void AddClanScore(ClanScoreEventType type, int multiplier = 1, BasePlayer otherPlayer = null, IClan otherClan = null, string arg1 = null, string arg2 = null)
	{
		ClanManager serverInstance = ClanManager.ServerInstance;
		if (!(serverInstance == null) && serverClan != null && !IsBot && !IsNpc && multiplier != 0)
		{
			int scoreForEvent = Clan.GetScoreForEvent(type);
			if (scoreForEvent != 0)
			{
				bool flag = otherPlayer != null && !otherPlayer.IsBot && !otherPlayer.IsNpc;
				serverInstance.AddScore(serverClan, new ClanScoreEvent
				{
					Type = type,
					SteamId = userID,
					Score = scoreForEvent,
					Multiplier = multiplier,
					OtherSteamId = (flag ? new ulong?(otherPlayer.userID) : null),
					OtherClanId = ((otherClan != null && otherClan != serverClan) ? new long?(otherClan.ClanId) : ((flag && otherPlayer.clanId != 0L) ? new long?(otherPlayer.clanId) : null)),
					Arg1 = arg1,
					Arg2 = arg2
				});
			}
		}
	}

	private void HandleClanPlayerKilled(BasePlayer killedByPlayer)
	{
		if (serverClan != null && killedByPlayer.serverClan != null && serverClan != killedByPlayer.serverClan)
		{
			AddClanScore(ClanScoreEventType.ClanPlayerDied, 1, killedByPlayer);
			killedByPlayer.AddClanScore(ClanScoreEventType.ClanPlayerKilled, 1, this);
		}
		if (!HasPlayerFlag(PlayerFlags.DisplaySash) && killedByPlayer.serverClan != null)
		{
			killedByPlayer.AddClanScore(ClanScoreEventType.UnarmedPlayerKilled, 1, this);
		}
	}

	public override bool CanBeLooted(BasePlayer player)
	{
		if (player == this)
		{
			return false;
		}
		if ((IsWounded() || IsSleeping() || CurrentGestureIsSurrendering || IsRestrainedOrSurrendering) && !IsLoadingAfterTransfer())
		{
			return !IsTransferring();
		}
		return false;
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void RPC_LootPlayer(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if ((bool)player && player.CanInteract() && CanBeLooted(player) && player.inventory.loot.StartLootingEntity(this))
		{
			player.inventory.loot.AddContainer(inventory.containerMain);
			player.inventory.loot.AddContainer(inventory.containerWear);
			player.inventory.loot.AddContainer(inventory.containerBelt);
			player.inventory.loot.SendImmediate();
			player.RadioactiveLootCheck(player.inventory.loot.containers);
			player.ClientRPC(RpcTarget.Player("RPC_OpenLootPanel", player), "player_corpse");
		}
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void RPC_Assist(RPCMessage msg)
	{
		if (msg.player.CanInteract() && !(msg.player == this) && IsWounded())
		{
			StopWounded(msg.player);
			msg.player.stats.Add("wounded_assisted", 1, (Stats)5);
			stats.Add("wounded_healed", 1);
		}
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void RPC_KeepAlive(RPCMessage msg)
	{
		if (msg.player.CanInteract() && !(msg.player == this) && IsWounded())
		{
			ProlongWounding(10f);
		}
	}

	[RPC_Server]
	private void SV_Drink(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		Vector3 vector = msg.read.Vector3();
		if (!vector.IsNaNOrInfinity() && (bool)player && player.metabolism.CanConsume() && !(Vector3.Distance(player.transform.position, vector) > 5f) && WaterLevel.Test(vector, waves: true, volumes: true, this) && (!isMounted || GetMounted().canDrinkWhileMounted))
		{
			ItemDefinition itemDefinition = WaterResource.SV_GetAtPoint(vector);
			ItemModConsumable component = itemDefinition.GetComponent<ItemModConsumable>();
			Item item = ItemManager.Create(itemDefinition, component.amountToConsume, 0uL);
			ItemModConsume component2 = item.info.GetComponent<ItemModConsume>();
			if (component2.CanDoAction(item, player))
			{
				component2.DoAction(item, player);
			}
			item?.Remove();
			player.metabolism.MarkConsumption();
		}
	}

	[RPC_Server]
	public void RPC_StartClimb(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		bool flag = msg.read.Bit();
		Vector3 vector = msg.read.Vector3();
		NetworkableId networkableId = msg.read.EntityID();
		BaseNetworkable baseNetworkable = BaseNetworkable.serverEntities.Find(networkableId);
		Vector3 vector2 = (flag ? baseNetworkable.transform.TransformPoint(vector) : vector);
		if (!player.isMounted || player.Distance(vector2) > 5f || !GamePhysics.LineOfSight(player.eyes.position, vector2, 1218519041) || !GamePhysics.LineOfSight(vector2, vector2 + player.eyes.offset, 1218519041))
		{
			return;
		}
		Vector3 end = vector2 - (vector2 - player.eyes.position).normalized * 0.25f;
		if (!GamePhysics.CheckCapsule(player.eyes.position, end, 0.25f, 1218519041) && !AntiHack.TestNoClipping(player, vector2 + player.NoClipOffset(), vector2 + player.NoClipOffset(), player.NoClipRadius(ConVar.AntiHack.noclip_margin), ConVar.AntiHack.noclip_backtracking, out var _))
		{
			player.EnsureDismounted();
			player.transform.position = vector2;
			Collider component = player.GetComponent<Collider>();
			component.enabled = false;
			component.enabled = true;
			player.ForceUpdateTriggers();
			if (flag)
			{
				player.ClientRPC(RpcTarget.Player("ForcePositionToParentOffset", player), vector, networkableId);
			}
			else
			{
				player.ClientRPC(RpcTarget.Player("ForcePositionTo", player), vector2);
			}
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(1uL)]
	private void RequestServerEmoji()
	{
		RustEmojiLibrary.FindAllServerEmoji();
		if (RustEmojiLibrary.allServerEmoji.Count > 0)
		{
			ClientRPCPlayerList(null, this, "ClientReceiveEmojiList", RustEmojiLibrary.cachedServerList);
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(3uL)]
	private void ServerRequestEmojiData(RPCMessage msg)
	{
		string text = msg.read.String();
		if (RustEmojiLibrary.allServerEmoji.TryGetValue(text, out var value))
		{
			byte[] array = FileStorage.server.Get(value.CRC, value.FileType, RustEmojiLibrary.EmojiStorageNetworkId);
			ClientRPC(RpcTarget.Player("ClientReceiveEmojiData", msg.player), (uint)array.Length, array, text, value.CRC, (int)value.FileType);
		}
	}

	public int GetQueuedUpdateCount(NetworkQueue queue)
	{
		return networkQueue[(int)queue].Length;
	}

	public void SendSnapshots(ListHashSet<Networkable> ents)
	{
		using (TimeWarning.New("SendSnapshots"))
		{
			int count = ents.Values.Count;
			Networkable[] buffer = ents.Values.Buffer;
			for (int i = 0; i < count; i++)
			{
				SnapshotQueue.Add(buffer[i].handler as BaseNetworkable);
			}
		}
	}

	public void QueueUpdate(NetworkQueue queue, BaseNetworkable ent)
	{
		if (!IsConnected)
		{
			return;
		}
		switch (queue)
		{
		case NetworkQueue.Update:
			networkQueue[0].Add(ent);
			break;
		case NetworkQueue.UpdateDistance:
			if (!IsReceivingSnapshot && !networkQueue[1].Contains(ent) && !networkQueue[0].Contains(ent))
			{
				NetworkQueueList networkQueueList = networkQueue[1];
				if (Distance(ent as BaseEntity) < 20f)
				{
					QueueUpdate(NetworkQueue.Update, ent);
				}
				else
				{
					networkQueueList.Add(ent);
				}
			}
			break;
		}
	}

	public void SendEntityUpdate()
	{
		using (TimeWarning.New("SendEntityUpdate"))
		{
			SendEntityUpdates(SnapshotQueue);
			SendEntityUpdates(networkQueue[0]);
			SendEntityUpdates(networkQueue[1]);
		}
	}

	public void ClearEntityQueue(Group group = null)
	{
		SnapshotQueue.Clear(group);
		networkQueue[0].Clear(group);
		networkQueue[1].Clear(group);
	}

	private void SendEntityUpdates(NetworkQueueList queue)
	{
		if (queue.queueInternal.Count == 0)
		{
			return;
		}
		int num = (IsReceivingSnapshot ? ConVar.Server.updatebatchspawn : ConVar.Server.updatebatch);
		List<BaseNetworkable> obj = Facepunch.Pool.Get<List<BaseNetworkable>>();
		using (TimeWarning.New("SendEntityUpdates.SendEntityUpdates"))
		{
			int num2 = 0;
			foreach (BaseNetworkable item in queue.queueInternal)
			{
				SendEntitySnapshot(item);
				obj.Add(item);
				num2++;
				if (num2 > num)
				{
					break;
				}
			}
		}
		if (num > queue.queueInternal.Count)
		{
			queue.queueInternal.Clear();
		}
		else
		{
			using (TimeWarning.New("SendEntityUpdates.Remove"))
			{
				for (int i = 0; i < obj.Count; i++)
				{
					queue.queueInternal.Remove(obj[i]);
				}
			}
		}
		if (queue.queueInternal.Count == 0 && queue.MaxLength > 2048)
		{
			queue.queueInternal.Clear();
			queue.queueInternal = new HashSet<BaseNetworkable>();
			queue.MaxLength = 0;
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	private void SendEntitySnapshot(BaseNetworkable ent)
	{
		using (TimeWarning.New("SendEntitySnapshot"))
		{
			if (!(ent == null) && ent.net != null && ent.ShouldNetworkTo(this))
			{
				NetWrite netWrite = Network.Net.sv.StartWrite();
				net.connection.validate.entityUpdates++;
				SaveInfo saveInfo = default(SaveInfo);
				saveInfo.forConnection = net.connection;
				saveInfo.forDisk = false;
				SaveInfo saveInfo2 = saveInfo;
				netWrite.PacketID(Message.Type.Entities);
				netWrite.UInt32(net.connection.validate.entityUpdates);
				ent.ToStreamForNetwork(netWrite, saveInfo2);
				netWrite.Send(new SendInfo(net.connection));
			}
		}
	}

	public bool HasPlayerFlag(PlayerFlags f)
	{
		return (playerFlags & f) == f;
	}

	public void SetPlayerFlag(PlayerFlags f, bool b)
	{
		if (b)
		{
			if (HasPlayerFlag(f))
			{
				return;
			}
			playerFlags |= f;
		}
		else
		{
			if (!HasPlayerFlag(f))
			{
				return;
			}
			playerFlags &= ~f;
		}
		SendNetworkUpdate();
	}

	public void LightToggle(bool mask = true)
	{
		Item activeItem = GetActiveItem();
		if (activeItem != null)
		{
			BaseEntity heldEntity = activeItem.GetHeldEntity();
			if (heldEntity != null)
			{
				HeldEntity component = heldEntity.GetComponent<HeldEntity>();
				if ((bool)component)
				{
					component.SendMessage("SetLightsOn", mask && !component.LightsOn(), SendMessageOptions.DontRequireReceiver);
				}
			}
		}
		foreach (Item item in inventory.containerWear.itemList)
		{
			ItemModWearable component2 = item.info.GetComponent<ItemModWearable>();
			if ((bool)component2 && component2.emissive)
			{
				item.SetFlag(Item.Flag.IsOn, mask && !item.HasFlag(Item.Flag.IsOn));
				item.MarkDirty();
			}
		}
		if (isMounted)
		{
			GetMounted().LightToggle(this);
		}
	}

	private RPSWinState Opposite(RPSWinState state)
	{
		return state switch
		{
			RPSWinState.Win => RPSWinState.Loss, 
			RPSWinState.Loss => RPSWinState.Win, 
			_ => state, 
		};
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(1uL)]
	private void Server_StartGesture(RPCMessage msg)
	{
		if (!IsGestureBlocked())
		{
			uint id = msg.read.UInt32();
			GestureConfig toPlay = GestureCollection.Instance.IdToGesture(id);
			Server_StartGesture(toPlay);
		}
	}

	public void Server_StartGesture(uint gestureId)
	{
		GestureConfig toPlay = GestureCollection.Instance.IdToGesture(gestureId);
		Server_StartGesture(toPlay);
	}

	public void Server_StartGesture(GestureConfig toPlay, GestureStartSource startSource = GestureStartSource.Player, bool bypassOwnershipCheck = false)
	{
		if ((toPlay != null && toPlay.hideInWheel && startSource == GestureStartSource.Player && !ConVar.Server.cinematic) || !(toPlay != null) || (!bypassOwnershipCheck && startSource != 0 && !toPlay.IsOwnedBy(this)) || !toPlay.CanBeUsedBy(this))
		{
			return;
		}
		if (toPlay.animationType == GestureConfig.AnimationType.OneShot)
		{
			Invoke(TimeoutGestureServer, toPlay.duration);
		}
		else if (toPlay.animationType == GestureConfig.AnimationType.Loop)
		{
			InvokeRepeating(MonitorLoopingGesture, 0f, 0f);
		}
		ClientRPC(RpcTarget.NetworkGroup("Client_StartGesture"), toPlay.gestureId);
		gestureFinishedTime = toPlay.duration;
		currentGesture = toPlay;
		switch (toPlay.actionType)
		{
		case GestureConfig.GestureActionType.Surrender:
			inventory.SetLockedByRestraint(flag: true);
			break;
		case GestureConfig.GestureActionType.ShowNameTag:
			if (GameInfo.HasAchievements)
			{
				int val = CountWaveTargets(base.transform.position, 4f, 0.6f, eyes.HeadForward(), recentWaveTargets, 5);
				stats.Add("waved_at_players", val);
				stats.Save(forceSteamSave: true);
			}
			break;
		case GestureConfig.GestureActionType.DanceAchievement:
		{
			TriggerDanceAchievement triggerDanceAchievement = FindTrigger<TriggerDanceAchievement>();
			if (triggerDanceAchievement != null)
			{
				triggerDanceAchievement.NotifyDanceStarted();
			}
			break;
		}
		}
		if (startSource == GestureStartSource.Player && toPlay.hasMultiplayerInteraction)
		{
			SetPlayerFlag(PlayerFlags.WaitingForGestureInteraction, b: true);
		}
		if (toPlay.animationType == GestureConfig.AnimationType.Loop)
		{
			SendNetworkUpdate();
		}
	}

	private void TimeoutGestureServer()
	{
		currentGesture = null;
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(10uL)]
	public void Server_CancelGesture()
	{
		if (currentGesture != null && currentGesture.actionType == GestureConfig.GestureActionType.Surrender)
		{
			Handcuffs handcuffs = GetHeldEntity() as Handcuffs;
			if (handcuffs == null || !handcuffs.Locked)
			{
				inventory.SetLockedByRestraint(flag: false);
			}
		}
		currentGesture = null;
		blockHeldInputTimer = 0f;
		SetPlayerFlag(PlayerFlags.WaitingForGestureInteraction, b: false);
		ClientRPC(RpcTarget.NetworkGroup("Client_RemoteCancelledGesture"));
		CancelInvoke(MonitorLoopingGesture);
		CancelInvoke(TimeoutGestureServer);
	}

	private void MonitorLoopingGesture()
	{
		bool flag = currentGesture != null && currentGesture.canDuckDuringGesture;
		if (modelState == null || (!flag && modelState.ducked) || modelState.sleeping || IsWounded() || IsSwimming() || IsDead() || (isMounted && GetMounted().allowedGestures == BaseMountable.MountGestureType.UpperBody && !CurrentGestureIsUpperBody) || (isMounted && GetMounted().allowedGestures == BaseMountable.MountGestureType.None))
		{
			Server_CancelGesture();
		}
	}

	private void NotifyGesturesNewItemEquipped()
	{
		if (InGesture)
		{
			Server_CancelGesture();
		}
	}

	public int CountWaveTargets(Vector3 position, float distance, float minimumDot, Vector3 forward, HashSet<NetworkableId> workingList, int maxCount)
	{
		float sqrDistance = distance * distance;
		Group group = net.group;
		if (group == null)
		{
			return 0;
		}
		List<Network.Connection> subscribers = group.subscribers;
		int num = 0;
		for (int i = 0; i < subscribers.Count; i++)
		{
			Network.Connection connection = subscribers[i];
			if (!connection.active)
			{
				continue;
			}
			BasePlayer basePlayer = connection.player as BasePlayer;
			if (CheckPlayer(basePlayer))
			{
				workingList.Add(basePlayer.net.ID);
				num++;
				if (num >= maxCount)
				{
					break;
				}
			}
		}
		return num;
		bool CheckPlayer(BasePlayer player)
		{
			if (player == null)
			{
				return false;
			}
			if (player == this)
			{
				return false;
			}
			if (player.SqrDistance(position) > sqrDistance)
			{
				return false;
			}
			if (Vector3.Dot((player.transform.position - position).normalized, forward) < minimumDot)
			{
				return false;
			}
			if (workingList.Contains(player.net.ID))
			{
				return false;
			}
			return true;
		}
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	private void RequestJoinGesture(RPCMessage msg)
	{
		NetworkableId uid = msg.read.EntityID();
		BasePlayer basePlayer = BaseNetworkable.serverEntities.Find(uid) as BasePlayer;
		interactiveGestureStartTime = 0f;
		if (msg.player != basePlayer || !(basePlayer != null))
		{
			return;
		}
		SetPlayerFlag(PlayerFlags.WaitingForGestureInteraction, b: false);
		if (currentGesture.actionType == GestureConfig.GestureActionType.RockPaperScissors)
		{
			rpsTarget = uid;
			basePlayer.rpsTarget = net.ID;
			basePlayer.Server_StartGesture(GestureCollection.Instance.StringToGesture("rps"), GestureStartSource.ServerAction);
			ClientRPC(RpcTarget.Player("PromptToPickRPSHand", basePlayer), 10f);
			ClientRPC(RpcTarget.Player("PromptToPickRPSHand", this), 10f);
			if (basePlayer.IsBot)
			{
				basePlayer.Invoke(BotRPSRandomise, 2f);
			}
			if (IsBot)
			{
				Invoke(BotRPSRandomise, 2f);
			}
			InvokeRepeating(MonitorRPSGame, 0f, 0f);
		}
	}

	private void BotRPSRandomise()
	{
		selectedRpsOption = UnityEngine.Random.Range(0, 3);
		Debug.Log($"Bot randomly selected {selectedRpsOption}");
	}

	[RPC_Server]
	private void SelectedRPSOption(RPCMessage msg)
	{
		selectedRpsOption = msg.read.Int32();
	}

	private void MonitorRPSGame()
	{
		bool flag = false;
		BasePlayer basePlayer = BaseNetworkable.serverEntities.Find(rpsTarget) as BasePlayer;
		if (basePlayer == null || Distance(basePlayer) > 5f || IsWounded() || basePlayer.IsWounded() || IsDead() || basePlayer.IsDead())
		{
			flag = true;
		}
		if (!flag && (float)interactiveGestureStartTime > 10f)
		{
			flag = true;
		}
		if (flag)
		{
			ClientRPC(RpcTarget.Player("CancelRPSGame", this));
			Server_CancelGesture();
			if (basePlayer != null)
			{
				ClientRPC(RpcTarget.Player("CancelRPSGame", basePlayer));
				basePlayer.Server_CancelGesture();
			}
			CancelInvoke(MonitorRPSGame);
		}
		if (basePlayer != null && basePlayer.selectedRpsOption != -1 && selectedRpsOption != -1)
		{
			RPSWinState rPSWinState = (((selectedRpsOption != 0 || basePlayer.selectedRpsOption != 2) && (selectedRpsOption != 1 || basePlayer.selectedRpsOption != 0) && (selectedRpsOption != 2 || basePlayer.selectedRpsOption != 1)) ? RPSWinState.Loss : RPSWinState.Win);
			if (selectedRpsOption == basePlayer.selectedRpsOption)
			{
				rPSWinState = RPSWinState.Draw;
			}
			ClientRPC(RpcTarget.NetworkGroup("OnRPSResult"), (int)rPSWinState, selectedRpsOption);
			basePlayer.ClientRPC(RpcTarget.NetworkGroup("OnRPSResult"), (int)Opposite(rPSWinState), basePlayer.selectedRpsOption);
			basePlayer.selectedRpsOption = -1;
			basePlayer.rpsTarget = default(NetworkableId);
			selectedRpsOption = -1;
			rpsTarget = default(NetworkableId);
			CancelInvoke(MonitorRPSGame);
			float time = ((rPSWinState == RPSWinState.Draw) ? 2.5f : 5f);
			Invoke(Server_CancelGesture, time);
			basePlayer.Invoke(basePlayer.Server_CancelGesture, time);
		}
	}

	private bool IsGestureBlocked()
	{
		if (isMounted && GetMounted().allowedGestures == BaseMountable.MountGestureType.None)
		{
			return true;
		}
		if ((bool)GetHeldEntity() && GetHeldEntity().BlocksGestures())
		{
			return true;
		}
		bool flag = currentGesture != null;
		if (flag && currentGesture.gestureType == GestureConfig.GestureType.Cinematic)
		{
			flag = false;
		}
		if (!(IsWounded() || flag) && !IsDead() && !IsSleeping())
		{
			return IsRestrained;
		}
		return true;
	}

	public void DelayedTeamUpdate()
	{
		UpdateTeam(currentTeam);
	}

	public void TeamUpdate()
	{
		TeamUpdate(fullTeamUpdate: false);
	}

	public void TeamUpdate(bool fullTeamUpdate)
	{
		if (!RelationshipManager.TeamsEnabled() || !IsConnected || currentTeam == 0L)
		{
			return;
		}
		RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindTeam(currentTeam);
		if (playerTeam == null)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		using PlayerTeam playerTeam2 = Facepunch.Pool.Get<PlayerTeam>();
		playerTeam2.teamLeader = playerTeam.teamLeader;
		playerTeam2.teamID = playerTeam.teamID;
		playerTeam2.teamName = playerTeam.teamName;
		playerTeam2.members = Facepunch.Pool.Get<List<PlayerTeam.TeamMember>>();
		playerTeam2.teamLifetime = playerTeam.teamLifetime;
		playerTeam2.teamPings = Facepunch.Pool.Get<List<MapNote>>();
		foreach (ulong member in playerTeam.members)
		{
			BasePlayer basePlayer = RelationshipManager.FindByID(member);
			if ((bool)basePlayer && basePlayer.IsInTutorial)
			{
				continue;
			}
			PlayerTeam.TeamMember teamMember = Facepunch.Pool.Get<PlayerTeam.TeamMember>();
			teamMember.displayName = ((basePlayer != null) ? basePlayer.displayName : (SingletonComponent<ServerMgr>.Instance.persistance.GetPlayerName(member) ?? "DEAD"));
			teamMember.healthFraction = ((basePlayer != null && basePlayer.IsAlive()) ? basePlayer.healthFraction : 0f);
			teamMember.position = ((basePlayer != null) ? basePlayer.transform.position : Vector3.zero);
			teamMember.online = basePlayer != null && !basePlayer.IsSleeping();
			teamMember.wounded = basePlayer != null && basePlayer.IsWounded();
			if ((!sentInstrumentTeamAchievement || !sentSummerTeamAchievement) && basePlayer != null)
			{
				if ((bool)basePlayer.GetHeldEntity() && basePlayer.GetHeldEntity().IsInstrument())
				{
					num++;
				}
				if (basePlayer.isMounted)
				{
					if (basePlayer.GetMounted().IsInstrument())
					{
						num++;
					}
					if (basePlayer.GetMounted().IsSummerDlcVehicle)
					{
						num2++;
					}
				}
				if (num >= 4 && !sentInstrumentTeamAchievement)
				{
					GiveAchievement("TEAM_INSTRUMENTS");
					sentInstrumentTeamAchievement = true;
				}
				if (num2 >= 4)
				{
					GiveAchievement("SUMMER_INFLATABLE");
					sentSummerTeamAchievement = true;
				}
			}
			teamMember.userID = member;
			playerTeam2.members.Add(teamMember);
			if (basePlayer != null)
			{
				if (basePlayer.State.pings != null && basePlayer.State.pings.Count > 0 && basePlayer != this)
				{
					playerTeam2.teamPings.AddRange(basePlayer.State.pings);
				}
				if (fullTeamUpdate && basePlayer != this)
				{
					basePlayer.TeamUpdate(fullTeamUpdate: false);
				}
			}
		}
		playerTeam2.leaderMapNotes = Facepunch.Pool.Get<List<MapNote>>();
		PlayerState playerState = SingletonComponent<ServerMgr>.Instance.playerStateManager.Get(playerTeam.teamLeader);
		if (playerState?.pointsOfInterest != null)
		{
			foreach (MapNote item in playerState.pointsOfInterest)
			{
				playerTeam2.leaderMapNotes.Add(item);
			}
		}
		ClientRPC(RpcTarget.PlayerAndSpectators("CLIENT_ReceiveTeamInfo", this), playerTeam2);
		if (playerTeam2.leaderMapNotes != null)
		{
			playerTeam2.leaderMapNotes.Clear();
		}
		if (playerTeam2.teamPings != null)
		{
			playerTeam2.teamPings.Clear();
		}
		BasePlayer basePlayer2 = FindByID(playerTeam.teamLeader);
		if (fullTeamUpdate && basePlayer2 != null && basePlayer2 != this)
		{
			basePlayer2.TeamUpdate(fullTeamUpdate: false);
		}
	}

	public void UpdateTeam(ulong newTeam)
	{
		currentTeam = newTeam;
		SendNetworkUpdate();
		if (RelationshipManager.ServerInstance.FindTeam(newTeam) == null)
		{
			ClearTeam();
		}
		else
		{
			TeamUpdate();
		}
	}

	public void ClearTeam()
	{
		currentTeam = 0uL;
		ClientRPC(RpcTarget.PlayerAndSpectators("CLIENT_ClearTeam", this));
		SendNetworkUpdate();
	}

	public void ClearPendingInvite()
	{
		ClientRPC(RpcTarget.Player("CLIENT_PendingInvite", this), "", 0);
	}

	public HeldEntity GetHeldEntity()
	{
		if (base.isServer)
		{
			Item activeItem = GetActiveItem();
			if (activeItem == null)
			{
				return null;
			}
			return activeItem.GetHeldEntity() as HeldEntity;
		}
		return null;
	}

	public bool IsHoldingEntity<T>()
	{
		HeldEntity heldEntity = GetHeldEntity();
		if (heldEntity == null)
		{
			return false;
		}
		return heldEntity is T;
	}

	public bool IsHostileItem(Item item)
	{
		if (!item.info.isHoldable)
		{
			return false;
		}
		ItemModEntity component = item.info.GetComponent<ItemModEntity>();
		if (component == null)
		{
			return false;
		}
		GameObject gameObject = component.entityPrefab.Get();
		if (gameObject == null)
		{
			return false;
		}
		AttackEntity component2 = gameObject.GetComponent<AttackEntity>();
		if (component2 == null)
		{
			return false;
		}
		return component2.hostile;
	}

	public bool IsItemHoldRestricted(Item item)
	{
		if (IsNpc)
		{
			return false;
		}
		if (InSafeZone() && item != null && IsHostileItem(item))
		{
			return true;
		}
		return false;
	}

	public void ClearDeathMarker(bool sendToClient = false)
	{
		if (!IsNpc)
		{
			if (ServerCurrentDeathNote != null)
			{
				Facepunch.Pool.Free(ref State.deathMarker);
			}
			DirtyPlayerState();
			if (sendToClient)
			{
				SendMarkersToClient();
			}
		}
	}

	public void Server_LogDeathMarker(Vector3 position)
	{
		if (!IsNpc)
		{
			if (ServerCurrentDeathNote == null)
			{
				ServerCurrentDeathNote = Facepunch.Pool.Get<MapNote>();
				ServerCurrentDeathNote.noteType = 0;
			}
			ServerCurrentDeathNote.worldPosition = position;
			ClientRPC(RpcTarget.Player("Client_AddNewDeathMarker", this), ServerCurrentDeathNote);
			DirtyPlayerState();
		}
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(8uL)]
	public void Server_AddMarker(RPCMessage msg)
	{
		if (State.pointsOfInterest == null)
		{
			State.pointsOfInterest = Facepunch.Pool.Get<List<MapNote>>();
		}
		if (State.pointsOfInterest.Count >= ConVar.Server.maximumMapMarkers)
		{
			msg.player.ShowToast(GameTip.Styles.Blue_Short, MarkerLimitPhrase, false, ConVar.Server.maximumMapMarkers.ToString());
			return;
		}
		MapNote mapNote = MapNote.Deserialize(msg.read);
		if (mapNote.label == "auto-name")
		{
			int num = FindUnusedNumberName();
			if (num != -1)
			{
				mapNote.label = num.ToString();
			}
		}
		ValidateMapNote(mapNote);
		if (mapNote.colourIndex == -1)
		{
			mapNote.colourIndex = FindUnusedPointOfInterestColour();
		}
		State.pointsOfInterest.Add(mapNote);
		DirtyPlayerState();
		SendMarkersToClient();
		TeamUpdate();
	}

	private int FindUnusedNumberName(int maxToCheck = 100)
	{
		List<MapNote> pointsOfInterest = State.pointsOfInterest;
		for (int i = 1; i < maxToCheck; i++)
		{
			bool flag = false;
			foreach (MapNote item in pointsOfInterest)
			{
				if (item.label == i.ToString())
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return i;
			}
		}
		return -1;
	}

	private int FindUnusedPointOfInterestColour()
	{
		if (State.pointsOfInterest == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < 6; i++)
		{
			if (HasColour(num))
			{
				num++;
			}
		}
		return num;
		bool HasColour(int index)
		{
			foreach (MapNote item in State.pointsOfInterest)
			{
				if (item.colourIndex == index)
				{
					return true;
				}
			}
			return false;
		}
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(1uL)]
	public void Server_UpdateMarker(RPCMessage msg)
	{
		if (State.pointsOfInterest == null)
		{
			State.pointsOfInterest = Facepunch.Pool.Get<List<MapNote>>();
		}
		int num = msg.read.Int32();
		if (State.pointsOfInterest.Count <= num)
		{
			return;
		}
		using MapNote mapNote = MapNote.Deserialize(msg.read);
		ValidateMapNote(mapNote);
		mapNote.CopyTo(State.pointsOfInterest[num]);
		DirtyPlayerState();
		SendMarkersToClient();
		TeamUpdate();
	}

	private void ValidateMapNote(MapNote n)
	{
		if (n.label != null)
		{
			n.label = n.label.Truncate(10).ToUpperInvariant();
		}
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(10uL)]
	public void Server_RemovePointOfInterest(RPCMessage msg)
	{
		int num = msg.read.Int32();
		if (State.pointsOfInterest != null && State.pointsOfInterest.Count > num && num >= 0)
		{
			State.pointsOfInterest[num].Dispose();
			State.pointsOfInterest.RemoveAt(num);
			DirtyPlayerState();
			SendMarkersToClient();
			TeamUpdate();
		}
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(1uL)]
	public void Server_RequestMarkers(RPCMessage msg)
	{
		SendMarkersToClient();
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(1uL)]
	public void Server_ClearMapMarkers(RPCMessage msg)
	{
		ServerCurrentDeathNote?.Dispose();
		ServerCurrentDeathNote = null;
		if (State.pointsOfInterest != null)
		{
			foreach (MapNote item in State.pointsOfInterest)
			{
				item?.Dispose();
			}
			State.pointsOfInterest.Clear();
		}
		DirtyPlayerState();
		TeamUpdate();
	}

	private void SendMarkersToClient()
	{
		using MapNoteList mapNoteList = Facepunch.Pool.Get<MapNoteList>();
		mapNoteList.notes = Facepunch.Pool.Get<List<MapNote>>();
		if (ServerCurrentDeathNote != null)
		{
			mapNoteList.notes.Add(ServerCurrentDeathNote);
		}
		if (State.pointsOfInterest != null)
		{
			mapNoteList.notes.AddRange(State.pointsOfInterest);
		}
		ClientRPC(RpcTarget.Player("Client_ReceiveMarkers", this), mapNoteList);
		mapNoteList.notes.Clear();
	}

	public bool HasAttemptedMission(uint missionID)
	{
		foreach (BaseMission.MissionInstance mission in missions)
		{
			if (mission.missionID == missionID)
			{
				return true;
			}
		}
		return false;
	}

	public bool CanAcceptMission(BaseMission mission)
	{
		return CanAcceptMission(mission.id);
	}

	public bool CanAcceptMission(uint missionID)
	{
		if (HasActiveMission())
		{
			return false;
		}
		if (!BaseMission.missionsenabled)
		{
			return false;
		}
		BaseMission fromID = MissionManifest.GetFromID(missionID);
		if (fromID == null)
		{
			Debug.LogError("MISSION NOT FOUND IN MANIFEST, ID :" + missionID);
			return false;
		}
		if (fromID.acceptDependancies != null && fromID.acceptDependancies.Length != 0)
		{
			BaseMission.MissionDependancy[] acceptDependancies = fromID.acceptDependancies;
			foreach (BaseMission.MissionDependancy missionDependancy in acceptDependancies)
			{
				if (missionDependancy.everAttempted)
				{
					continue;
				}
				bool flag = false;
				foreach (BaseMission.MissionInstance mission in missions)
				{
					if (mission.missionID == missionDependancy.targetMissionID && mission.status == missionDependancy.targetMissionDesiredStatus)
					{
						flag = true;
					}
				}
				if (!flag)
				{
					return false;
				}
			}
		}
		if (IsMissionActive(missionID))
		{
			return false;
		}
		if (fromID.isRepeatable)
		{
			bool num = HasCompletedMission(missionID);
			bool flag2 = HasFailedMission(missionID);
			if (num && fromID.repeatDelaySecondsSuccess <= -1)
			{
				return false;
			}
			if (flag2 && fromID.repeatDelaySecondsFailed <= -1)
			{
				return false;
			}
			foreach (BaseMission.MissionInstance mission2 in missions)
			{
				if (mission2.missionID == missionID)
				{
					float num2 = 0f;
					if (mission2.status == BaseMission.MissionStatus.Completed)
					{
						num2 = fromID.repeatDelaySecondsSuccess;
					}
					else if (mission2.status == BaseMission.MissionStatus.Failed)
					{
						num2 = fromID.repeatDelaySecondsFailed;
					}
					float endTime = mission2.endTime;
					if (UnityEngine.Time.time - endTime < num2)
					{
						return false;
					}
				}
			}
		}
		BaseMission.PositionGenerator[] positionGenerators = fromID.positionGenerators;
		for (int i = 0; i < positionGenerators.Length; i++)
		{
			if (!positionGenerators[i].Validate(this, fromID))
			{
				return false;
			}
		}
		return true;
	}

	public bool IsMissionActive(uint missionID)
	{
		foreach (BaseMission.MissionInstance mission in missions)
		{
			if (mission.missionID == missionID && (mission.status == BaseMission.MissionStatus.Active || mission.status == BaseMission.MissionStatus.Accomplished))
			{
				return true;
			}
		}
		return false;
	}

	public bool HasCompletedMission(uint missionID)
	{
		foreach (BaseMission.MissionInstance mission in missions)
		{
			if (mission.missionID == missionID && mission.status == BaseMission.MissionStatus.Completed)
			{
				return true;
			}
		}
		return false;
	}

	public bool HasFailedMission(uint missionID)
	{
		foreach (BaseMission.MissionInstance mission in missions)
		{
			if (mission.missionID == missionID && mission.status == BaseMission.MissionStatus.Failed)
			{
				return true;
			}
		}
		return false;
	}

	private void WipeMissions()
	{
		if (missions.Count > 0)
		{
			for (int num = missions.Count - 1; num >= 0; num--)
			{
				BaseMission.MissionInstance obj = missions[num];
				if (obj != null)
				{
					obj.GetMission().MissionFailed(obj, this, BaseMission.MissionFailReason.ResetPlayerState);
					Facepunch.Pool.Free(ref obj);
				}
			}
		}
		missions.Clear();
		SetActiveMission(-1);
		MissionDirty();
	}

	public void AbandonActiveMission()
	{
		if (HasActiveMission())
		{
			int activeMission = GetActiveMission();
			if (activeMission != -1 && activeMission < missions.Count)
			{
				BaseMission.MissionInstance missionInstance = missions[activeMission];
				missionInstance.GetMission().MissionFailed(missionInstance, this, BaseMission.MissionFailReason.Abandon);
			}
		}
	}

	public void ThinkMissions(float delta)
	{
		if (!BaseMission.missionsenabled)
		{
			return;
		}
		if (timeSinceMissionThink < thinkEvery)
		{
			timeSinceMissionThink += delta;
			return;
		}
		foreach (BaseMission.MissionInstance mission in missions)
		{
			mission.Think(this, timeSinceMissionThink);
		}
		timeSinceMissionThink = 0f;
	}

	public void MissionDirty(bool shouldSendNetworkUpdate = true)
	{
		if (BaseMission.missionsenabled)
		{
			State.missions = SaveMissions();
			DirtyPlayerState();
			if (shouldSendNetworkUpdate)
			{
				SendNetworkUpdate();
			}
		}
	}

	public void ProcessMissionEvent(BaseMission.MissionEventType type, uint identifier, float amount)
	{
		ProcessMissionEvent(type, new BaseMission.MissionEventPayload
		{
			UintIdentifier = identifier
		}, amount);
	}

	public void ProcessMissionEvent(BaseMission.MissionEventType type, uint identifier, float amount, Vector3 worldPos)
	{
		ProcessMissionEvent(type, new BaseMission.MissionEventPayload
		{
			UintIdentifier = identifier,
			WorldPosition = worldPos
		}, amount);
	}

	public void ProcessMissionEvent(BaseMission.MissionEventType type, int identifier, float amount)
	{
		ProcessMissionEvent(type, new BaseMission.MissionEventPayload
		{
			IntIdentifier = identifier
		}, amount);
	}

	public void ProcessMissionEvent(BaseMission.MissionEventType type, NetworkableId identifier, float amount)
	{
		ProcessMissionEvent(type, new BaseMission.MissionEventPayload
		{
			NetworkIdentifier = identifier
		}, amount);
	}

	public void ProcessMissionEvent(BaseMission.MissionEventType type, BaseMission.MissionEventPayload payload, float amount)
	{
		if (!BaseMission.missionsenabled)
		{
			return;
		}
		foreach (BaseMission.MissionInstance mission in missions)
		{
			mission.ProcessMissionEvent(this, type, payload, amount);
		}
	}

	private void AssignFollowUpMission()
	{
		if (followupMission != null && followupMissionProvider != null)
		{
			BaseMission.AssignMission(this, followupMissionProvider, followupMission);
		}
		followupMission = null;
		followupMissionProvider = null;
	}

	public void RegisterFollowupMission(BaseMission targetMission, IMissionProvider provider)
	{
		followupMission = targetMission;
		followupMissionProvider = provider;
		if (followupMission != null && followupMissionProvider != null)
		{
			Invoke(AssignFollowUpMission, 1.5f);
		}
	}

	private Missions SaveMissions()
	{
		Missions missions = Facepunch.Pool.Get<Missions>();
		missions.missions = Facepunch.Pool.Get<List<MissionInstance>>();
		missions.activeMission = GetActiveMission();
		missions.protocol = 262;
		missions.seed = World.Seed;
		missions.saveCreatedTime = Epoch.FromDateTime(SaveRestore.SaveCreatedTime);
		foreach (BaseMission.MissionInstance mission in this.missions)
		{
			MissionInstance missionInstance = Facepunch.Pool.Get<MissionInstance>();
			missionInstance.missionID = mission.missionID;
			missionInstance.missionStatus = (uint)mission.status;
			missions.missions.Add(missionInstance);
			if (mission.status != BaseMission.MissionStatus.Active && mission.status != BaseMission.MissionStatus.Accomplished)
			{
				continue;
			}
			MissionInstanceData missionInstanceData = (missionInstance.instanceData = Facepunch.Pool.Get<MissionInstanceData>());
			missionInstanceData.providerID = mission.providerID;
			missionInstanceData.startTime = UnityEngine.Time.time - mission.startTime;
			missionInstanceData.endTime = mission.endTime;
			missionInstanceData.missionLocation = mission.missionLocation;
			missionInstanceData.missionPoints = Facepunch.Pool.Get<List<ProtoBuf.MissionPoint>>();
			foreach (KeyValuePair<string, Vector3> missionPoint2 in mission.missionPoints)
			{
				ProtoBuf.MissionPoint missionPoint = Facepunch.Pool.Get<ProtoBuf.MissionPoint>();
				missionPoint.identifier = missionPoint2.Key;
				missionPoint.location = missionPoint2.Value;
				missionInstanceData.missionPoints.Add(missionPoint);
			}
			missionInstanceData.objectiveStatuses = Facepunch.Pool.Get<List<ObjectiveStatus>>();
			BaseMission.MissionInstance.ObjectiveStatus[] objectiveStatuses = mission.objectiveStatuses;
			foreach (BaseMission.MissionInstance.ObjectiveStatus objectiveStatus in objectiveStatuses)
			{
				ObjectiveStatus objectiveStatus2 = Facepunch.Pool.Get<ObjectiveStatus>();
				objectiveStatus2.completed = objectiveStatus.completed;
				objectiveStatus2.failed = objectiveStatus.failed;
				objectiveStatus2.started = objectiveStatus.started;
				objectiveStatus2.progressCurrent = objectiveStatus.progressCurrent;
				objectiveStatus2.progressTarget = objectiveStatus.progressTarget;
				missionInstanceData.objectiveStatuses.Add(objectiveStatus2);
			}
			missionInstanceData.missionEntities = Facepunch.Pool.Get<List<ProtoBuf.MissionEntity>>();
			foreach (KeyValuePair<string, MissionEntity> missionEntity2 in mission.missionEntities)
			{
				BaseEntity baseEntity = ((missionEntity2.Value != null) ? missionEntity2.Value.GetEntity() : null);
				if (baseEntity.IsValid())
				{
					ProtoBuf.MissionEntity missionEntity = Facepunch.Pool.Get<ProtoBuf.MissionEntity>();
					missionEntity.identifier = missionEntity2.Key;
					missionEntity.entityID = baseEntity.net.ID;
					missionInstanceData.missionEntities.Add(missionEntity);
				}
			}
		}
		return missions;
	}

	public void SetActiveMission(int index)
	{
		_ = _activeMission;
		_activeMission = index;
		if (IsInTutorial && GetCurrentTutorialIsland() != null)
		{
			GetCurrentTutorialIsland().OnPlayerStartedMission(this);
		}
	}

	public int GetActiveMission()
	{
		return _activeMission;
	}

	public bool HasActiveMission()
	{
		return GetActiveMission() != -1;
	}

	public BaseMission.MissionInstance GetActiveMissionInstance()
	{
		int activeMission = GetActiveMission();
		if (activeMission >= 0 && activeMission < missions.Count)
		{
			return missions[activeMission];
		}
		return null;
	}

	private void LoadMissions(Missions loadedMissions)
	{
		if (missions.Count > 0)
		{
			for (int num = missions.Count - 1; num >= 0; num--)
			{
				BaseMission.MissionInstance obj = missions[num];
				if (obj != null)
				{
					Facepunch.Pool.Free(ref obj);
				}
			}
		}
		missions.Clear();
		if (base.isServer && loadedMissions != null)
		{
			int protocol = loadedMissions.protocol;
			uint seed = loadedMissions.seed;
			int saveCreatedTime = loadedMissions.saveCreatedTime;
			int num2 = Epoch.FromDateTime(SaveRestore.SaveCreatedTime);
			if (262 != protocol || World.Seed != seed || num2 != saveCreatedTime)
			{
				Debug.Log("Missions were from old protocol or different seed, or not from a loaded save. Clearing");
				loadedMissions.activeMission = -1;
				SetActiveMission(-1);
				State.missions = SaveMissions();
				return;
			}
		}
		if (loadedMissions != null && loadedMissions.missions.Count > 0)
		{
			foreach (MissionInstance mission2 in loadedMissions.missions)
			{
				if (MissionManifest.GetFromID(mission2.missionID) == null)
				{
					continue;
				}
				BaseMission.MissionInstance missionInstance = Facepunch.Pool.Get<BaseMission.MissionInstance>();
				missionInstance.missionID = mission2.missionID;
				missionInstance.status = (BaseMission.MissionStatus)mission2.missionStatus;
				MissionInstanceData instanceData = mission2.instanceData;
				if (instanceData != null)
				{
					missionInstance.providerID = instanceData.providerID;
					missionInstance.startTime = UnityEngine.Time.time - instanceData.startTime;
					missionInstance.endTime = instanceData.endTime;
					missionInstance.missionLocation = instanceData.missionLocation;
					if (base.isServer && instanceData.missionPoints != null)
					{
						foreach (ProtoBuf.MissionPoint missionPoint in instanceData.missionPoints)
						{
							missionInstance.missionPoints.Add(missionPoint.identifier, missionPoint.location);
							BaseMission.AddBlocker(missionPoint.location);
						}
					}
					missionInstance.objectiveStatuses = new BaseMission.MissionInstance.ObjectiveStatus[instanceData.objectiveStatuses.Count];
					for (int i = 0; i < instanceData.objectiveStatuses.Count; i++)
					{
						ObjectiveStatus objectiveStatus = instanceData.objectiveStatuses[i];
						BaseMission.MissionInstance.ObjectiveStatus objectiveStatus2 = new BaseMission.MissionInstance.ObjectiveStatus();
						objectiveStatus2.completed = objectiveStatus.completed;
						objectiveStatus2.failed = objectiveStatus.failed;
						objectiveStatus2.started = objectiveStatus.started;
						objectiveStatus2.progressCurrent = objectiveStatus.progressCurrent;
						objectiveStatus2.progressTarget = objectiveStatus.progressTarget;
						missionInstance.objectiveStatuses[i] = objectiveStatus2;
					}
					if (base.isServer && instanceData.missionEntities != null)
					{
						missionInstance.missionEntities.Clear();
						BaseMission mission = missionInstance.GetMission();
						foreach (ProtoBuf.MissionEntity missionEntity2 in instanceData.missionEntities)
						{
							MissionEntity missionEntity = null;
							BaseNetworkable baseNetworkable = ((missionEntity2.entityID != default(NetworkableId)) ? BaseNetworkable.serverEntities.Find(missionEntity2.entityID) : null);
							if (baseNetworkable != null)
							{
								missionEntity = (baseNetworkable.gameObject.TryGetComponent<MissionEntity>(out var component) ? component : baseNetworkable.gameObject.AddComponent<MissionEntity>());
								BaseMission.MissionEntityEntry missionEntityEntry = ((mission != null) ? ((IReadOnlyCollection<BaseMission.MissionEntityEntry>)(object)mission.missionEntities).FindWith((BaseMission.MissionEntityEntry ed) => ed.identifier, missionEntity2.identifier) : null);
								missionEntity.Setup(this, missionInstance, missionEntity2.identifier, missionEntityEntry?.cleanupOnMissionSuccess ?? true, missionEntityEntry?.cleanupOnMissionFailed ?? true);
							}
							missionInstance.missionEntities.Add(missionEntity2.identifier, missionEntity);
						}
					}
				}
				missions.Add(missionInstance);
			}
			SetActiveMission(loadedMissions.activeMission);
		}
		else
		{
			SetActiveMission(-1);
		}
		if (base.isServer)
		{
			GetActiveMissionInstance()?.PostServerLoad(this);
		}
	}

	private void UpdateModelState()
	{
		if (!IsDead() && !IsSpectating())
		{
			wantsSendModelState = true;
		}
	}

	public void SendModelState(bool force = false)
	{
		if (force || (wantsSendModelState && !(nextModelStateUpdate > UnityEngine.Time.time)))
		{
			wantsSendModelState = false;
			nextModelStateUpdate = UnityEngine.Time.time + 0.1f;
			if (!IsDead() && !IsSpectating())
			{
				modelState.sleeping = IsSleeping();
				modelState.mounted = isMounted;
				modelState.relaxed = IsRelaxed();
				modelState.onPhone = HasActiveTelephone && !activeTelephone.IsMobile;
				modelState.crawling = IsCrawling();
				modelState.loading = IsLoadingAfterTransfer();
				ClientRPC(RpcTarget.NetworkGroup("OnModelState"), modelState);
			}
		}
	}

	public BaseMountable GetMounted()
	{
		return mounted.Get(base.isServer) as BaseMountable;
	}

	public BaseVehicle GetMountedVehicle()
	{
		BaseMountable baseMountable = GetMounted();
		if (!baseMountable.IsValid())
		{
			return null;
		}
		return baseMountable.VehicleParent();
	}

	public void MarkSwapSeat()
	{
		nextSeatSwapTime = UnityEngine.Time.time + 0.75f;
	}

	public bool SwapSeatCooldown()
	{
		return UnityEngine.Time.time < nextSeatSwapTime;
	}

	public bool CanMountMountablesNow()
	{
		if (!IsDead())
		{
			return !IsWounded();
		}
		return false;
	}

	public void SetMounted(BaseMountable mount)
	{
		mounted.Set(mount);
	}

	public void EnsureDismounted()
	{
		if (isMounted)
		{
			GetMounted().DismountPlayer(this);
		}
	}

	public virtual void DismountObject()
	{
		mounted.Set(null);
		SendNetworkUpdate();
		PauseSpeedHackDetection(5f);
	}

	public void HandleMountedOnLoad()
	{
		if (!mounted.IsValid(base.isServer))
		{
			return;
		}
		BaseMountable baseMountable = mounted.Get(base.isServer) as BaseMountable;
		if (baseMountable != null)
		{
			baseMountable.MountPlayer(this);
			if (!AllowSleeperMounting(baseMountable))
			{
				baseMountable.DismountPlayer(this);
			}
		}
		else
		{
			mounted.Set(null);
		}
	}

	public bool AllowSleeperMounting(BaseMountable mountable)
	{
		if (mountable.allowSleeperMounting)
		{
			return true;
		}
		if (!IsLoadingAfterTransfer())
		{
			return IsTransferProtected();
		}
		return true;
	}

	public PlayerSecondaryData SaveSecondaryData()
	{
		PlayerSecondaryData playerSecondaryData = Facepunch.Pool.Get<PlayerSecondaryData>();
		playerSecondaryData.userId = userID;
		PlayerState playerState = State.Copy();
		if (playerState.pointsOfInterest != null)
		{
			Facepunch.Pool.Free(ref playerState.pointsOfInterest, freeElements: true);
		}
		if (playerState.pings != null)
		{
			Facepunch.Pool.Free(ref playerState.pings, freeElements: true);
		}
		playerState.deathMarker?.Dispose();
		playerState.deathMarker = null;
		playerState.missions?.Dispose();
		playerState.missions = null;
		playerSecondaryData.playerState = playerState;
		if (currentTeam != 0L)
		{
			RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindTeam(currentTeam);
			if (playerTeam != null)
			{
				playerSecondaryData.teamId = playerTeam.teamID;
				playerSecondaryData.isTeamLeader = playerTeam.teamLeader == (ulong)userID;
			}
		}
		playerSecondaryData.relationships = Facepunch.Pool.Get<List<PlayerSecondaryData.RelationshipData>>();
		foreach (RelationshipManager.PlayerRelationshipInfo value in RelationshipManager.ServerInstance.GetRelationships(userID).relations.Values)
		{
			PlayerSecondaryData.RelationshipData relationshipData = Facepunch.Pool.Get<PlayerSecondaryData.RelationshipData>();
			relationshipData.info = value.ToProto();
			relationshipData.mugshotData = GetPoolableMugshotData(value);
			playerSecondaryData.relationships.Add(relationshipData);
		}
		return playerSecondaryData;
		ArraySegment<byte> GetPoolableMugshotData(RelationshipManager.PlayerRelationshipInfo relationshipInfo)
		{
			if (relationshipInfo.mugshotCrc == 0)
			{
				return default(ArraySegment<byte>);
			}
			try
			{
				uint steamIdHash = RelationshipManager.GetSteamIdHash(userID, relationshipInfo.player);
				byte[] array = FileStorage.server.Get(relationshipInfo.mugshotCrc, FileStorage.Type.jpg, RelationshipManager.ServerInstance.net.ID, steamIdHash);
				if (array == null)
				{
					return default(ArraySegment<byte>);
				}
				byte[] array2 = System.Buffers.ArrayPool<byte>.Shared.Rent(array.Length);
				new Span<byte>(array).CopyTo(array2);
				return new ArraySegment<byte>(array2, 0, array.Length);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				return default(ArraySegment<byte>);
			}
		}
	}

	public void LoadSecondaryData(PlayerSecondaryData data)
	{
		if (data == null)
		{
			return;
		}
		if (data.userId != (ulong)userID)
		{
			Debug.LogError($"Attempted to load secondary data with an incorrect userID! Expected {data.userId} but player has {userID.Get()}, not loading it.");
			return;
		}
		if (data.playerState != null)
		{
			State.unHostileTimestamp = data.playerState.unHostileTimestamp;
			DirtyPlayerState();
		}
		if (data.relationships == null)
		{
			return;
		}
		RelationshipManager.PlayerRelationships relationships = RelationshipManager.ServerInstance.GetRelationships(userID);
		relationships.relations.Clear();
		foreach (PlayerSecondaryData.RelationshipData relationship in data.relationships)
		{
			if (relationship.mugshotData.Count > 0)
			{
				try
				{
					byte[] array = new byte[relationship.mugshotData.Count];
					relationship.mugshotData.AsSpan().CopyTo(array);
					uint steamIdHash = RelationshipManager.GetSteamIdHash(userID, relationship.info.playerID);
					uint num = FileStorage.server.Store(array, FileStorage.Type.jpg, RelationshipManager.ServerInstance.net.ID, steamIdHash);
					if (num != relationship.info.mugshotCrc)
					{
						Debug.LogWarning($"Mugshot data for {userID.Get()}->{relationship.info.playerID} had a CRC mismatch, updating it");
						relationship.info.mugshotCrc = num;
					}
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}
			relationships.relations.Add(relationship.info.playerID, RelationshipManager.PlayerRelationshipInfo.FromProto(relationship.info));
		}
		RelationshipManager.ServerInstance.MarkRelationshipsDirtyFor(this);
	}

	public override void DisableTransferProtection()
	{
		BaseVehicle vehicleParent = GetVehicleParent();
		if (vehicleParent != null && vehicleParent.IsTransferProtected())
		{
			vehicleParent.DisableTransferProtection();
		}
		BaseMountable baseMountable = GetMounted();
		if (baseMountable != null && baseMountable.IsTransferProtected())
		{
			baseMountable.DisableTransferProtection();
		}
		base.DisableTransferProtection();
	}

	public void KickAfterServerTransfer()
	{
		if (IsConnected)
		{
			Kick("Redirecting to another zone...");
		}
		Kill();
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(5uL)]
	private void RequestParachuteDeploy(RPCMessage msg)
	{
		RequestParachuteDeploy();
	}

	public void RequestParachuteDeploy()
	{
		if (isMounted || !CheckParachuteClearance())
		{
			return;
		}
		Item slot = inventory.containerWear.GetSlot(7);
		if (slot == null || !(slot.conditionNormalized > 0f) || slot.isBroken || !slot.info.TryGetComponent<ItemModParachute>(out var component))
		{
			return;
		}
		Parachute parachute = GameManager.server.CreateEntity(component.ParachuteVehiclePrefab.resourcePath, base.transform.position, eyes.rotation) as Parachute;
		if (parachute != null)
		{
			parachute.skinID = slot.skin;
			parachute.Spawn();
			parachute.SetHealth(parachute.MaxHealth() * slot.conditionNormalized);
			parachute.AttemptMount(this);
			if (isMounted)
			{
				slot.Remove();
				ItemManager.DoRemoves();
				SendNetworkUpdate();
			}
			else
			{
				parachute.Kill();
			}
		}
	}

	public bool CheckParachuteClearance()
	{
		Vector3 position = base.transform.position;
		if (!WaterLevel.Test(position - Vector3.up * 5f, waves: false, volumes: true, this) && !GamePhysics.Trace(new Ray(position, -Vector3.up), 1f, out var _, 6f, 1218543873))
		{
			return !GamePhysics.CheckSphere(position + Vector3.up * 3.5f, 2f, 1218543873);
		}
		return false;
	}

	public bool HasValidParachuteEquipped()
	{
		if (inventory == null || inventory.containerWear == null)
		{
			return false;
		}
		Item slot = inventory.containerWear.GetSlot(7);
		if (slot != null && slot.conditionNormalized > 0f && !slot.isBroken && slot.info.TryGetComponent<ItemModParachute>(out var _))
		{
			return true;
		}
		return false;
	}

	public void ClearClientPetLink()
	{
		ClientRPC(RpcTarget.Player("CLIENT_SetPetPrefabID", this), 0, 0);
	}

	public void SendClientPetLink()
	{
		if (PetEntity == null && BasePet.ActivePetByOwnerID.TryGetValue(userID, out var value) && value.Brain != null)
		{
			value.Brain.SetOwningPlayer(this);
		}
		ClientRPC(RpcTarget.Player("CLIENT_SetPetPrefabID", this), (PetEntity != null) ? PetEntity.prefabID : 0u, (PetEntity != null) ? PetEntity.net.ID : default(NetworkableId));
		if (PetEntity != null)
		{
			SendClientPetStateIndex();
		}
	}

	public void SendClientPetStateIndex()
	{
		BasePet basePet = PetEntity as BasePet;
		if (!(basePet == null))
		{
			ClientRPC(RpcTarget.Player("CLIENT_SetPetPetLoadedStateIndex", this), basePet.Brain.LoadedDesignIndex());
		}
	}

	[RPC_Server]
	private void IssuePetCommand(RPCMessage msg)
	{
		ParsePetCommand(msg, raycast: false);
	}

	[RPC_Server]
	private void IssuePetCommandRaycast(RPCMessage msg)
	{
		ParsePetCommand(msg, raycast: true);
	}

	private void ParsePetCommand(RPCMessage msg, bool raycast)
	{
		if (UnityEngine.Time.time - lastPetCommandIssuedTime <= 1f)
		{
			return;
		}
		lastPetCommandIssuedTime = UnityEngine.Time.time;
		if (!(msg.player == null) && Pet != null && Pet.IsOwnedBy(msg.player))
		{
			int cmd = msg.read.Int32();
			int param = msg.read.Int32();
			if (raycast)
			{
				Ray value = msg.read.Ray();
				Pet.IssuePetCommand((PetCommandType)cmd, param, value);
			}
			else
			{
				Pet.IssuePetCommand((PetCommandType)cmd, param, null);
			}
		}
	}

	public bool CanPing(bool disregardHeldEntity = false)
	{
		BaseGameMode activeGameMode = BaseGameMode.GetActiveGameMode(base.isServer);
		if (activeGameMode != null && !activeGameMode.allowPings)
		{
			return false;
		}
		if ((disregardHeldEntity || GetHeldEntity() is Binocular || (isMounted && GetMounted() is ComputerStation computerStation && computerStation.AllowPings())) && IsAlive() && !IsWounded())
		{
			return !IsSpectating();
		}
		return false;
	}

	public static PingStyle GetPingStyle(PingType t)
	{
		PingStyle pingStyle = default(PingStyle);
		return t switch
		{
			PingType.Hostile => HostileMarker, 
			PingType.GoTo => GoToMarker, 
			PingType.Dollar => DollarMarker, 
			PingType.Loot => LootMarker, 
			PingType.Node => NodeMarker, 
			PingType.Gun => GunMarker, 
			PingType.Build => BuildMarker, 
			_ => pingStyle, 
		};
	}

	private void ApplyPingStyle(MapNote note, PingType type)
	{
		PingStyle pingStyle = GetPingStyle(type);
		note.colourIndex = pingStyle.ColourIndex;
		note.icon = pingStyle.IconIndex;
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(3uL)]
	private void Server_AddPing(RPCMessage msg)
	{
		if (State.pings == null)
		{
			State.pings = new List<MapNote>();
		}
		if (ConVar.Server.maximumPings == 0 || !CanPing())
		{
			return;
		}
		Vector3 vector = msg.read.Vector3();
		PingType pingType = (PingType)Mathf.Clamp(msg.read.Int32(), 0, 6);
		bool wasViaWheel = msg.read.Bit();
		PingStyle pingStyle = GetPingStyle(pingType);
		foreach (MapNote ping in State.pings)
		{
			if (ping.icon == pingStyle.IconIndex && (ping.worldPosition - vector).sqrMagnitude < 0.75f)
			{
				return;
			}
		}
		if (State.pings.Count >= ConVar.Server.maximumPings)
		{
			State.pings.RemoveAt(0);
		}
		MapNote mapNote = Facepunch.Pool.Get<MapNote>();
		mapNote.worldPosition = vector;
		mapNote.isPing = true;
		mapNote.timeRemaining = (mapNote.totalDuration = ConVar.Server.pingDuration);
		ApplyPingStyle(mapNote, pingType);
		State.pings.Add(mapNote);
		DirtyPlayerState();
		SendPingsToClient();
		TeamUpdate(fullTeamUpdate: true);
		Analytics.Azure.OnPlayerPinged(this, pingType, wasViaWheel);
	}

	public void AddPingAtLocation(PingType type, Vector3 location, float time, NetworkableId associatedId)
	{
		if (State.pings != null)
		{
			PingStyle pingStyle = GetPingStyle(type);
			foreach (MapNote ping in State.pings)
			{
				if (ping.icon == pingStyle.IconIndex && Vector3.Distance(location, ping.worldPosition) < 0.25f)
				{
					return;
				}
			}
		}
		if (State.pings == null)
		{
			State.pings = new List<MapNote>();
		}
		MapNote mapNote = Facepunch.Pool.Get<MapNote>();
		mapNote.worldPosition = location;
		mapNote.isPing = true;
		mapNote.timeRemaining = (mapNote.totalDuration = time);
		mapNote.associatedId = associatedId;
		ApplyPingStyle(mapNote, type);
		State.pings.Add(mapNote);
		DirtyPlayerState();
		SendPingsToClient();
		TeamUpdate(fullTeamUpdate: false);
	}

	public void RemovePingAtLocation(PingType type, Vector3 location, float tolerance, NetworkableId associatedId)
	{
		if (State.pings == null)
		{
			return;
		}
		PingStyle pingStyle = GetPingStyle(type);
		for (int i = 0; i < State.pings.Count; i++)
		{
			MapNote mapNote = State.pings[i];
			if (mapNote.icon == pingStyle.IconIndex && Vector3.Distance(location, mapNote.worldPosition) < tolerance)
			{
				State.pings.RemoveAt(i);
				DirtyPlayerState();
				SendPingsToClient();
				TeamUpdate(fullTeamUpdate: false);
			}
		}
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(3uL)]
	private void Server_RemovePing(RPCMessage msg)
	{
		if (State.pings == null)
		{
			State.pings = new List<MapNote>();
		}
		int num = msg.read.Int32();
		if (num >= 0 && num < State.pings.Count)
		{
			State.pings.RemoveAt(num);
			DirtyPlayerState();
			SendPingsToClient();
			TeamUpdate(fullTeamUpdate: true);
		}
	}

	private void SendPingsToClient()
	{
		using MapNoteList mapNoteList = Facepunch.Pool.Get<MapNoteList>();
		mapNoteList.notes = Facepunch.Pool.Get<List<MapNote>>();
		mapNoteList.notes.AddRange(State.pings);
		ClientRPC(RpcTarget.Player("Client_ReceivePings", this), mapNoteList);
		mapNoteList.notes.Clear();
	}

	private void TickPings()
	{
		if ((float)lastTick < 0.5f)
		{
			return;
		}
		TimeSince timeSince = lastTick;
		lastTick = 0f;
		UpdateResourcePings();
		if (State.pings == null)
		{
			return;
		}
		List<MapNote> obj = Facepunch.Pool.Get<List<MapNote>>();
		foreach (MapNote ping in State.pings)
		{
			ping.timeRemaining -= timeSince;
			if (ping.timeRemaining <= 0f)
			{
				obj.Add(ping);
			}
		}
		int count = obj.Count;
		foreach (MapNote item in obj)
		{
			if (State.pings.Contains(item))
			{
				State.pings.Remove(item);
			}
		}
		Facepunch.Pool.Free(ref obj, freeElements: false);
		if (count > 0)
		{
			DirtyPlayerState();
			SendPingsToClient();
			TeamUpdate(fullTeamUpdate: true);
		}
	}

	public void RegisterPingedEntity(BaseEntity entity, PingType type)
	{
		if (!pingedEntities.Contains((entity.net.ID, type)))
		{
			pingedEntities.Add((entity.net.ID, type));
		}
	}

	public void DeregisterPingedEntitiesOfType(PingType type)
	{
		List<(NetworkableId, PingType)> obj = Facepunch.Pool.Get<List<(NetworkableId, PingType)>>();
		foreach (var pingedEntity in pingedEntities)
		{
			if (pingedEntity.pingType == type)
			{
				obj.Add(pingedEntity);
			}
		}
		foreach (var item in obj)
		{
			pingedEntities.Remove(item);
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	public void DeregisterPingedEntity(NetworkableId id, PingType type)
	{
		if (!pingedEntities.Contains((id, type)))
		{
			return;
		}
		pingedEntities.Remove((id, type));
		for (int i = 0; i < State.pings.Count; i++)
		{
			if (State.pings[i].associatedId == id)
			{
				State.pings.RemoveAt(i);
				break;
			}
		}
		DirtyPlayerState();
		SendPingsToClient();
	}

	public void EnableResourcePings(ItemDefinition forItem, PingType pingType)
	{
		if (!tutorialDesiredResource.Contains((forItem, pingType)))
		{
			tutorialDesiredResource.Add((forItem, pingType));
		}
	}

	private void UpdateResourcePings()
	{
		if (State == null || (float)lastResourcePingUpdate < 3f || !IsInTutorial)
		{
			return;
		}
		lastResourcePingUpdate = 0f;
		if (State.pings == null)
		{
			State.pings = new List<MapNote>();
		}
		List<BaseEntity> obj = Facepunch.Pool.Get<List<BaseEntity>>();
		List<(BaseEntity, PingType)> list = Facepunch.Pool.Get<List<(BaseEntity, PingType)>>();
		List<BaseEntity> obj2 = Facepunch.Pool.Get<List<BaseEntity>>();
		foreach (var item2 in tutorialDesiredResource)
		{
			obj2.Clear();
			foreach (Networkable networkable in net.group.networkables)
			{
				BaseEntity baseEntity = BaseNetworkable.serverEntities.Find(networkable.ID) as BaseEntity;
				if (baseEntity != null && Distance(baseEntity) < 128f && baseEntity.isServer)
				{
					if (baseEntity.TryGetComponent<ResourceDispenser>(out var component) && component.HasItemToDispense(item2.item))
					{
						obj2.Add(baseEntity);
					}
					else if (baseEntity is CollectibleEntity collectibleEntity && collectibleEntity.HasItem(item2.item))
					{
						obj2.Add(baseEntity);
					}
					else if (baseEntity is StorageContainer { inventory: not null } storageContainer && storageContainer.inventory.HasItem(item2.item))
					{
						obj2.Add(baseEntity);
					}
				}
			}
			if (obj2.Count <= 0)
			{
				continue;
			}
			float num = float.MaxValue;
			BaseEntity baseEntity2 = null;
			foreach (BaseEntity item3 in obj2)
			{
				float num2 = Distance(item3);
				if (num2 < num)
				{
					num = num2;
					baseEntity2 = item3;
				}
			}
			if (baseEntity2 != null)
			{
				list.Add((baseEntity2, item2.pingType));
			}
		}
		List<(NetworkableId, PingType)> obj3 = Facepunch.Pool.Get<List<(NetworkableId, PingType)>>();
		foreach (var pingedEntity in pingedEntities)
		{
			BaseNetworkable baseNetworkable = BaseNetworkable.serverEntities.Find(pingedEntity.id);
			if (baseNetworkable != null && !baseNetworkable.IsDestroyed)
			{
				list.Add((baseNetworkable as BaseEntity, pingedEntity.pingType));
			}
			else
			{
				obj3.Add(pingedEntity);
			}
		}
		foreach (var item4 in obj3)
		{
			pingedEntities.Remove(item4);
		}
		Facepunch.Pool.FreeUnmanaged(ref obj3);
		Facepunch.Pool.FreeUnmanaged(ref obj2);
		List<MapNote> obj4 = Facepunch.Pool.Get<List<MapNote>>();
		foreach (MapNote ping in State.pings)
		{
			if (ping.associatedId.Value == 0L)
			{
				continue;
			}
			bool flag = false;
			foreach (var item5 in list)
			{
				if (ping.associatedId == item5.Item1.net.ID)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				BaseNetworkable baseNetworkable2 = BaseNetworkable.serverEntities.Find(ping.associatedId);
				if (baseNetworkable2 != null && baseNetworkable2 is IEntityPingSource entityPingSource && entityPingSource.IsPingValid(ping))
				{
					flag = true;
				}
			}
			if (!flag)
			{
				obj4.Add(ping);
			}
		}
		bool flag2 = obj4.Count > 0;
		foreach (MapNote item6 in obj4)
		{
			if (State.pings.Contains(item6))
			{
				State.pings.Remove(item6);
			}
		}
		Facepunch.Pool.Free(ref obj4, freeElements: false);
		foreach (var item7 in list)
		{
			if (HasPingForEntity(item7.Item1))
			{
				continue;
			}
			PingType item = item7.Item2;
			foreach (var pingedEntity2 in pingedEntities)
			{
				if (pingedEntity2.id == item7.Item1.net.ID)
				{
					item = pingedEntity2.pingType;
				}
			}
			State.pings.Add(CreatePingForEntity(item7.Item1, item));
			flag2 = true;
		}
		if (flag2)
		{
			DirtyPlayerState();
			SendPingsToClient();
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	private MapNote CreatePingForEntity(BaseEntity baseEntity, PingType type)
	{
		MapNote mapNote = Facepunch.Pool.Get<MapNote>();
		mapNote.worldPosition = baseEntity.transform.position;
		mapNote.isPing = true;
		mapNote.timeRemaining = (mapNote.totalDuration = 30f);
		mapNote.associatedId = baseEntity.net.ID;
		ApplyPingStyle(mapNote, type);
		return mapNote;
	}

	private bool HasPingForEntity(BaseEntity ent)
	{
		return HasPingForEntity(ent.net.ID);
	}

	private bool HasPingForEntity(NetworkableId id)
	{
		foreach (MapNote ping in State.pings)
		{
			if (ping.associatedId == id)
			{
				return true;
			}
		}
		return false;
	}

	public void DisableResourcePings(ItemDefinition forItem, PingType type)
	{
		if (tutorialDesiredResource.Contains((forItem, type)))
		{
			tutorialDesiredResource.Remove((forItem, type));
		}
		if (tutorialDesiredResource.Count == 0)
		{
			UpdateResourcePings();
		}
	}

	private void ClearAllPings()
	{
		if (State != null && State.pings != null)
		{
			State.pings.Clear();
		}
		tutorialDesiredResource.Clear();
		pingedEntities.Clear();
	}

	public void DirtyPlayerState()
	{
		_playerStateDirty = true;
	}

	public void SavePlayerState()
	{
		if (_playerStateDirty)
		{
			_playerStateDirty = false;
			SingletonComponent<ServerMgr>.Instance.playerStateManager.Save(userID);
		}
	}

	public void ResetPlayerState()
	{
		SingletonComponent<ServerMgr>.Instance.playerStateManager.Reset(userID);
		ClientRPC(RpcTarget.Player("SetHostileLength", this), 0f);
		SendMarkersToClient();
		WipeMissions();
		MissionDirty();
	}

	public bool IsSleeping()
	{
		return HasPlayerFlag(PlayerFlags.Sleeping);
	}

	public bool IsSpectating()
	{
		return HasPlayerFlag(PlayerFlags.Spectating);
	}

	public bool IsRelaxed()
	{
		return HasPlayerFlag(PlayerFlags.Relaxed);
	}

	public bool IsServerFalling()
	{
		return HasPlayerFlag(PlayerFlags.ServerFall);
	}

	public bool IsLoadingAfterTransfer()
	{
		return HasPlayerFlag(PlayerFlags.LoadingAfterTransfer);
	}

	public bool CanBuild()
	{
		if (IsBuildingBlockedByVehicle())
		{
			return false;
		}
		if (IsBuildingBlockedByEntity())
		{
			return false;
		}
		BuildingPrivlidge buildingPrivilege = GetBuildingPrivilege();
		if (buildingPrivilege == null)
		{
			return true;
		}
		return buildingPrivilege.IsAuthed(this);
	}

	public bool CanBuild(Vector3 position, Quaternion rotation, Bounds bounds)
	{
		OBB obb = new OBB(position, rotation, bounds);
		if (IsBuildingBlockedByVehicle(obb))
		{
			return false;
		}
		if (IsBuildingBlockedByEntity(obb))
		{
			return false;
		}
		BuildingPrivlidge buildingPrivilege = GetBuildingPrivilege(obb);
		if (buildingPrivilege == null)
		{
			return true;
		}
		return buildingPrivilege.IsAuthed(this);
	}

	public bool CanBuild(OBB obb)
	{
		if (IsBuildingBlockedByVehicle(obb))
		{
			return false;
		}
		if (IsBuildingBlockedByEntity(obb))
		{
			return false;
		}
		BuildingPrivlidge buildingPrivilege = GetBuildingPrivilege(obb);
		if (buildingPrivilege == null)
		{
			return true;
		}
		return buildingPrivilege.IsAuthed(this);
	}

	public bool IsBuildingBlocked()
	{
		if (IsBuildingBlockedByVehicle())
		{
			return true;
		}
		if (IsBuildingBlockedByEntity())
		{
			return true;
		}
		BuildingPrivlidge buildingPrivilege = GetBuildingPrivilege();
		if (buildingPrivilege == null)
		{
			return false;
		}
		return !buildingPrivilege.IsAuthed(this);
	}

	public bool IsBuildingBlocked(Vector3 position, Quaternion rotation, Bounds bounds)
	{
		OBB obb = new OBB(position, rotation, bounds);
		if (IsBuildingBlockedByVehicle(obb))
		{
			return true;
		}
		if (IsBuildingBlockedByEntity(obb))
		{
			return true;
		}
		BuildingPrivlidge buildingPrivilege = GetBuildingPrivilege(obb);
		if (buildingPrivilege == null)
		{
			return false;
		}
		return !buildingPrivilege.IsAuthed(this);
	}

	public bool IsBuildingBlocked(OBB obb)
	{
		if (IsBuildingBlockedByVehicle(obb))
		{
			return true;
		}
		if (IsBuildingBlockedByEntity(obb))
		{
			return true;
		}
		BuildingPrivlidge buildingPrivilege = GetBuildingPrivilege(obb);
		if (buildingPrivilege == null)
		{
			return false;
		}
		return !buildingPrivilege.IsAuthed(this);
	}

	public bool IsBuildingAuthed()
	{
		if (IsBuildingBlockedByVehicle())
		{
			return false;
		}
		if (IsBuildingBlockedByEntity())
		{
			return false;
		}
		BuildingPrivlidge buildingPrivilege = GetBuildingPrivilege();
		if (buildingPrivilege == null)
		{
			return false;
		}
		return buildingPrivilege.IsAuthed(this);
	}

	public bool IsBuildingAuthed(Vector3 position, Quaternion rotation, Bounds bounds)
	{
		OBB obb = new OBB(position, rotation, bounds);
		if (IsBuildingBlockedByVehicle(obb))
		{
			return false;
		}
		if (IsBuildingBlockedByEntity(obb))
		{
			return false;
		}
		BuildingPrivlidge buildingPrivilege = GetBuildingPrivilege(obb);
		if (buildingPrivilege == null)
		{
			return false;
		}
		return buildingPrivilege.IsAuthed(this);
	}

	public bool IsBuildingAuthed(OBB obb)
	{
		if (IsBuildingBlockedByVehicle(obb))
		{
			return false;
		}
		if (IsBuildingBlockedByEntity(obb))
		{
			return false;
		}
		BuildingPrivlidge buildingPrivilege = GetBuildingPrivilege(obb);
		if (buildingPrivilege == null)
		{
			return false;
		}
		return buildingPrivilege.IsAuthed(this);
	}

	public bool CanPlaceBuildingPrivilege()
	{
		if (IsBuildingBlockedByVehicle())
		{
			return false;
		}
		if (IsBuildingBlockedByEntity())
		{
			return false;
		}
		return GetBuildingPrivilege() == null;
	}

	public bool CanPlaceBuildingPrivilege(Vector3 position, Quaternion rotation, Bounds bounds)
	{
		OBB obb = new OBB(position, rotation, bounds);
		if (IsBuildingBlockedByVehicle(obb))
		{
			return false;
		}
		if (IsBuildingBlockedByEntity(obb))
		{
			return false;
		}
		return GetBuildingPrivilege(obb) == null;
	}

	public bool CanPlaceBuildingPrivilege(OBB obb)
	{
		if (IsBuildingBlockedByVehicle(obb))
		{
			return false;
		}
		if (IsBuildingBlockedByEntity(obb))
		{
			return false;
		}
		return GetBuildingPrivilege(obb) == null;
	}

	public bool IsNearEnemyBase()
	{
		if (IsBuildingBlockedByVehicle())
		{
			return true;
		}
		if (IsBuildingBlockedByEntity())
		{
			return true;
		}
		BuildingPrivlidge buildingPrivilege = GetBuildingPrivilege();
		if (buildingPrivilege == null)
		{
			return false;
		}
		if (!buildingPrivilege.IsAuthed(this))
		{
			return buildingPrivilege.AnyAuthed();
		}
		return false;
	}

	public bool IsNearEnemyBase(Vector3 position, Quaternion rotation, Bounds bounds)
	{
		OBB obb = new OBB(position, rotation, bounds);
		if (IsBuildingBlockedByVehicle(obb))
		{
			return true;
		}
		if (IsBuildingBlockedByEntity(obb))
		{
			return true;
		}
		BuildingPrivlidge buildingPrivilege = GetBuildingPrivilege(obb);
		if (buildingPrivilege == null)
		{
			return false;
		}
		if (!buildingPrivilege.IsAuthed(this))
		{
			return buildingPrivilege.AnyAuthed();
		}
		return false;
	}

	public bool IsNearEnemyBase(OBB obb)
	{
		if (IsBuildingBlockedByVehicle(obb))
		{
			return true;
		}
		if (IsBuildingBlockedByEntity(obb))
		{
			return true;
		}
		BuildingPrivlidge buildingPrivilege = GetBuildingPrivilege(obb);
		if (buildingPrivilege == null)
		{
			return false;
		}
		if (!buildingPrivilege.IsAuthed(this))
		{
			return buildingPrivilege.AnyAuthed();
		}
		return false;
	}

	public bool IsBuildingBlockedByVehicle()
	{
		return IsBuildingBlockedByVehicle(WorldSpaceBounds());
	}

	public bool IsBuildingBlockedByEntity()
	{
		return IsBuildingBlockedByEntity(WorldSpaceBounds());
	}

	public bool HasPrivilegeFromOther()
	{
		if (UnityEngine.Time.time - cachedPrivilegeFromOtherTime > 1f)
		{
			cachedPrivilegeFromOtherTime = UnityEngine.Time.time;
			cachedPrivilegeFromOther = null;
			IsBuildingBlockedByEntity(WorldSpaceBounds());
			IsBuildingBlockedByVehicle(WorldSpaceBounds());
		}
		return cachedPrivilegeFromOther != null;
	}

	private bool IsBuildingBlockedByVehicle(OBB obb)
	{
		List<BaseVehicle> obj = Facepunch.Pool.Get<List<BaseVehicle>>();
		Vis.Entities(obb.position, 2f + obb.extents.magnitude, obj, 134217728);
		for (int i = 0; i < obj.Count; i++)
		{
			BaseVehicle baseVehicle = obj[i];
			if (baseVehicle.isServer == base.isServer && !baseVehicle.IsDead() && !(obb.Distance(baseVehicle.WorldSpaceBounds()) > 2f))
			{
				if (!baseVehicle.IsAuthed(this))
				{
					Facepunch.Pool.FreeUnmanaged(ref obj);
					return true;
				}
				cachedPrivilegeFromOther = baseVehicle;
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return false;
	}

	private bool IsBuildingBlockedByEntity(OBB obb)
	{
		List<BaseEntity> obj = Facepunch.Pool.Get<List<BaseEntity>>();
		Vis.Entities(obb.position, 3f + obb.extents.magnitude, obj, 2097152);
		for (int i = 0; i < obj.Count; i++)
		{
			BaseEntity baseEntity = obj[i];
			if (baseEntity.isServer != base.isServer || obb.Distance(baseEntity.WorldSpaceBounds()) > 3f)
			{
				continue;
			}
			EntityPrivilege entityBuildingPrivilege = baseEntity.GetEntityBuildingPrivilege();
			if (!(entityBuildingPrivilege == null))
			{
				if (!entityBuildingPrivilege.IsAuthed(this))
				{
					Facepunch.Pool.FreeUnmanaged(ref obj);
					return true;
				}
				cachedPrivilegeFromOther = baseEntity;
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return false;
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	public void OnProjectileAttack(RPCMessage msg)
	{
		PlayerProjectileAttack playerProjectileAttack = PlayerProjectileAttack.Deserialize(msg.read);
		if (playerProjectileAttack == null)
		{
			return;
		}
		PlayerAttack playerAttack = playerProjectileAttack.playerAttack;
		HitInfo hitInfo = new HitInfo();
		hitInfo.LoadFromAttack(playerAttack.attack, serverSide: true);
		hitInfo.Initiator = this;
		hitInfo.ProjectileID = playerAttack.projectileID;
		hitInfo.ProjectileDistance = playerProjectileAttack.hitDistance;
		hitInfo.ProjectileVelocity = playerProjectileAttack.hitVelocity;
		hitInfo.ProjectileTravelTime = playerProjectileAttack.travelTime;
		hitInfo.Predicted = msg.connection;
		if (hitInfo.IsNaNOrInfinity() || float.IsNaN(playerProjectileAttack.travelTime) || float.IsInfinity(playerProjectileAttack.travelTime))
		{
			AntiHack.Log(this, AntiHackType.ProjectileHack, "Contains NaN (" + playerAttack.projectileID + ")");
			playerProjectileAttack.ResetToPool();
			playerProjectileAttack = null;
			stats.combat.LogInvalid(hitInfo, "projectile_nan");
			return;
		}
		if (!firedProjectiles.TryGetValue(playerAttack.projectileID, out var value))
		{
			AntiHack.Log(this, AntiHackType.ProjectileHack, "Missing ID (" + playerAttack.projectileID + ")", logToAnalytics: false);
			playerProjectileAttack.ResetToPool();
			playerProjectileAttack = null;
			stats.combat.LogInvalid(hitInfo, "projectile_invalid");
			return;
		}
		hitInfo.ProjectileHits = value.hits;
		hitInfo.ProjectileIntegrity = value.integrity;
		hitInfo.ProjectileTrajectoryMismatch = value.trajectoryMismatch;
		if (value.integrity <= 0f)
		{
			AntiHack.Log(this, AntiHackType.ProjectileHack, "Integrity is zero (" + playerAttack.projectileID + ")");
			Analytics.Azure.OnProjectileHackViolation(value);
			playerProjectileAttack.ResetToPool();
			playerProjectileAttack = null;
			stats.combat.LogInvalid(hitInfo, "projectile_integrity");
			return;
		}
		if (value.firedTime < UnityEngine.Time.realtimeSinceStartup - 8f)
		{
			AntiHack.Log(this, AntiHackType.ProjectileHack, "Lifetime is zero (" + playerAttack.projectileID + ")");
			Analytics.Azure.OnProjectileHackViolation(value);
			playerProjectileAttack.ResetToPool();
			playerProjectileAttack = null;
			stats.combat.LogInvalid(hitInfo, "projectile_lifetime");
			return;
		}
		if (value.ricochets > 0)
		{
			AntiHack.Log(this, AntiHackType.ProjectileHack, "Projectile is ricochet (" + playerAttack.projectileID + ")");
			Analytics.Azure.OnProjectileHackViolation(value);
			playerProjectileAttack.ResetToPool();
			playerProjectileAttack = null;
			stats.combat.LogInvalid(hitInfo, "projectile_ricochet");
			return;
		}
		hitInfo.Weapon = value.weaponSource;
		hitInfo.WeaponPrefab = value.weaponPrefab;
		hitInfo.ProjectilePrefab = value.projectilePrefab;
		hitInfo.damageProperties = value.projectilePrefab.damageProperties;
		Vector3 position = value.position;
		Vector3 initialPositionOffset = value.initialPositionOffset;
		Vector3 positionOffset = value.positionOffset;
		Vector3 velocity = value.velocity;
		float partialTime = value.partialTime;
		float travelTime = value.travelTime;
		float num = Mathf.Clamp(playerProjectileAttack.travelTime, value.travelTime, 8f);
		Vector3 gravity = UnityEngine.Physics.gravity * value.projectilePrefab.gravityModifier;
		float drag = value.projectilePrefab.drag;
		BaseEntity hitEntity = hitInfo.HitEntity;
		BasePlayer basePlayer = hitEntity as BasePlayer;
		bool flag = basePlayer != null;
		bool flag2 = flag && basePlayer.IsSleeping();
		bool flag3 = flag && basePlayer.IsWounded();
		bool flag4 = flag && basePlayer.isMounted;
		bool flag5 = flag && basePlayer.HasParent();
		bool flag6 = hitEntity != null;
		bool flag7 = flag6 && hitEntity.IsNpc;
		bool flag8 = hitInfo.HitMaterial == Projectile.WaterMaterialID();
		bool flag9;
		int num15;
		Vector3 position2;
		Vector3 pointStart;
		Vector3 hitPositionWorld;
		Vector3 vector;
		bool flag10;
		int num32;
		if (value.protection > 0)
		{
			flag9 = true;
			float num2 = 1f + ConVar.AntiHack.projectile_forgiveness;
			float num3 = 1f - ConVar.AntiHack.projectile_forgiveness;
			float projectile_clientframes = ConVar.AntiHack.projectile_clientframes;
			float projectile_serverframes = ConVar.AntiHack.projectile_serverframes;
			float num4 = Mathx.Decrement(value.firedTime);
			float num5 = Mathf.Clamp(Mathx.Increment(UnityEngine.Time.realtimeSinceStartup) - num4, 0f, 8f);
			float num6 = num;
			float num7 = (value.desyncLifeTime = Mathf.Abs(num5 - num6));
			float num8 = Mathf.Min(num5, num6);
			float num9 = projectile_clientframes / 60f;
			float num10 = projectile_serverframes * Mathx.Max(UnityEngine.Time.deltaTime, UnityEngine.Time.smoothDeltaTime, UnityEngine.Time.fixedDeltaTime);
			float num11 = (desyncTimeClamped + num8 + num9 + num10) * num2;
			float num12 = ((value.protection >= 6) ? ((desyncTimeClamped + num9 + num10) * num2) : num11);
			float num13 = (num5 - desyncTimeClamped - num9 - num10) * num3;
			float num14 = Vector3.Distance(value.initialPosition, hitInfo.HitPositionWorld);
			num15 = 1075904512;
			if (ConVar.AntiHack.projectile_terraincheck)
			{
				num15 |= 0x800000;
			}
			if (ConVar.AntiHack.projectile_vehiclecheck)
			{
				num15 |= 0x8000000;
			}
			if (flag6 && net.group != null && hitEntity.net != null && hitEntity.net.group != null && !net.subscriber.IsSubscribed(hitEntity.net.group))
			{
				AntiHack.Log(this, AntiHackType.ProjectileHack, "Entity out of network range");
				stats.combat.LogInvalid(hitInfo, "projectile_network_range");
				flag9 = false;
			}
			if (flag && hitInfo.boneArea == (HitArea)(-1))
			{
				string text = hitInfo.ProjectilePrefab.name;
				string text2 = (flag6 ? hitEntity.ShortPrefabName : "world");
				AntiHack.Log(this, AntiHackType.ProjectileHack, "Bone is invalid (" + text + " on " + text2 + " bone " + hitInfo.HitBone + ")");
				stats.combat.LogInvalid(hitInfo, "projectile_bone");
				flag9 = false;
			}
			if (flag8)
			{
				if (flag6)
				{
					string text3 = hitInfo.ProjectilePrefab.name;
					string text4 = (flag6 ? hitEntity.ShortPrefabName : "world");
					AntiHack.Log(this, AntiHackType.ProjectileHack, "Projectile water hit on entity (" + text3 + " on " + text4 + ")");
					Analytics.Azure.OnProjectileHackViolation(value);
					stats.combat.LogInvalid(hitInfo, "water_entity");
					flag9 = false;
				}
				if (!WaterLevel.Test(hitInfo.HitPositionWorld - 0.5f * Vector3.up, waves: true, volumes: true, this))
				{
					string text5 = hitInfo.ProjectilePrefab.name;
					string text6 = (flag6 ? hitEntity.ShortPrefabName : "world");
					AntiHack.Log(this, AntiHackType.ProjectileHack, "Projectile water level (" + text5 + " on " + text6 + ")");
					Analytics.Azure.OnProjectileHackViolation(value);
					stats.combat.LogInvalid(hitInfo, "water_level");
					flag9 = false;
				}
			}
			if (value.protection >= 2)
			{
				if (flag6 || (value.protection < 6 && flag))
				{
					float num16 = hitEntity.MaxVelocity() + hitEntity.GetParentVelocity().magnitude;
					float num17 = hitEntity.BoundsPadding() + num12 * num16;
					float num18 = (value.entityDistance = hitEntity.Distance(hitInfo.HitPositionWorld));
					if (num18 > num17)
					{
						string text7 = hitInfo.ProjectilePrefab.name;
						string shortPrefabName = hitEntity.ShortPrefabName;
						AntiHack.Log(this, AntiHackType.ProjectileHack, "Entity too far away (" + text7 + " on " + shortPrefabName + " with " + num18 + "m > " + num17 + "m in " + num12 + "s)");
						Analytics.Azure.OnProjectileHackViolation(value);
						stats.combat.LogInvalid(hitInfo, "entity_distance");
						flag9 = false;
					}
				}
				if (value.protection >= 6 && flag9 && flag && !flag7 && !flag2 && !flag3 && !flag4 && !flag5)
				{
					float magnitude = basePlayer.GetParentVelocity().magnitude;
					float num19 = basePlayer.BoundsPadding() + num12 * magnitude + ConVar.AntiHack.tickhistoryforgiveness;
					float num20 = (value.entityDistance = basePlayer.tickHistory.Distance(basePlayer, hitInfo.HitPositionWorld));
					if (num20 > num19)
					{
						string text8 = hitInfo.ProjectilePrefab.name;
						string shortPrefabName2 = basePlayer.ShortPrefabName;
						AntiHack.Log(this, AntiHackType.ProjectileHack, "Player too far away (" + text8 + " on " + shortPrefabName2 + " with " + num20 + "m > " + num19 + "m in " + num12 + "s)");
						Analytics.Azure.OnProjectileHackViolation(value);
						stats.combat.LogInvalid(hitInfo, "player_distance");
						flag9 = false;
					}
				}
			}
			if (value.protection >= 1)
			{
				float num21 = (flag6 ? (hitEntity.MaxVelocity() + hitEntity.GetParentVelocity().magnitude) : 0f);
				float num22 = (flag6 ? (num12 * num21) : 0f);
				float magnitude2 = value.initialVelocity.magnitude;
				float num23 = hitInfo.ProjectilePrefab.initialDistance + num11 * magnitude2;
				float num24 = hitInfo.ProjectileDistance + 1f + positionOffset.magnitude + num22 + estimatedVelocity.magnitude;
				if (num14 > num23)
				{
					string text9 = hitInfo.ProjectilePrefab.name;
					string text10 = (flag6 ? hitEntity.ShortPrefabName : "world");
					AntiHack.Log(this, AntiHackType.ProjectileHack, "Projectile too fast (" + text9 + " on " + text10 + " with " + num14 + "m > " + num23 + "m in " + num11 + "s)");
					Analytics.Azure.OnProjectileHackViolation(value);
					stats.combat.LogInvalid(hitInfo, "projectile_maxspeed");
					flag9 = false;
				}
				if (num14 > num24)
				{
					string text11 = hitInfo.ProjectilePrefab.name;
					string text12 = (flag6 ? hitEntity.ShortPrefabName : "world");
					AntiHack.Log(this, AntiHackType.ProjectileHack, "Projectile too far away (" + text11 + " on " + text12 + " with " + num14 + "m > " + num24 + "m in " + num11 + "s)");
					Analytics.Azure.OnProjectileHackViolation(value);
					stats.combat.LogInvalid(hitInfo, "projectile_distance");
					flag9 = false;
				}
				if (num7 > ConVar.AntiHack.projectile_desync)
				{
					string text13 = hitInfo.ProjectilePrefab.name;
					string text14 = (flag6 ? hitEntity.ShortPrefabName : "world");
					AntiHack.Log(this, AntiHackType.ProjectileHack, "Projectile desync (" + text13 + " on " + text14 + " with " + num7 + "s > " + ConVar.AntiHack.projectile_desync + "s)");
					Analytics.Azure.OnProjectileHackViolation(value);
					stats.combat.LogInvalid(hitInfo, "projectile_desync");
					flag9 = false;
				}
			}
			if (value.protection >= 4)
			{
				float num25 = 0f;
				if (flag6)
				{
					float num26 = hitEntity.GetParentVelocity().magnitude;
					if (hitEntity is CargoShip || hitEntity is Tugboat)
					{
						num26 += hitEntity.MaxVelocity();
					}
					num25 = num12 * num26;
				}
				SimulateProjectile(ref position, ref velocity, ref partialTime, num - travelTime, gravity, drag, out var prevPosition, out var prevVelocity);
				Line line = new Line(prevPosition - prevVelocity, position + prevVelocity);
				float num27 = (value.startPointMismatch = Mathf.Max(line.Distance(hitInfo.PointStart) - initialPositionOffset.magnitude - num25, 0f));
				float num28 = (value.endPointMismatch = Mathf.Max(line.Distance(hitInfo.HitPositionWorld) - initialPositionOffset.magnitude - num25, 0f));
				if (num27 > ConVar.AntiHack.projectile_trajectory)
				{
					string text15 = value.projectilePrefab.name;
					string text16 = (flag6 ? hitEntity.ShortPrefabName : "world");
					AntiHack.Log(this, AntiHackType.ProjectileHack, "Start position trajectory (" + text15 + " on " + text16 + " with " + num27 + "m > " + ConVar.AntiHack.projectile_trajectory + "m)");
					Analytics.Azure.OnProjectileHackViolation(value);
					stats.combat.LogInvalid(hitInfo, "trajectory_start");
					flag9 = false;
				}
				if (num28 > ConVar.AntiHack.projectile_trajectory)
				{
					string text17 = value.projectilePrefab.name;
					string text18 = (flag6 ? hitEntity.ShortPrefabName : "world");
					AntiHack.Log(this, AntiHackType.ProjectileHack, "End position trajectory (" + text17 + " on " + text18 + " with " + num28 + "m > " + ConVar.AntiHack.projectile_trajectory + "m)");
					Analytics.Azure.OnProjectileHackViolation(value);
					stats.combat.LogInvalid(hitInfo, "trajectory_end");
					flag9 = false;
				}
				if (hitInfo.ProjectileTrajectoryMismatch > ConVar.AntiHack.projectile_trajectory_update)
				{
					string text19 = value.projectilePrefab.name;
					string text20 = (flag6 ? hitEntity.ShortPrefabName : "world");
					AntiHack.Log(this, AntiHackType.ProjectileHack, "Update position trajectory (" + text19 + " on " + text20 + " with " + hitInfo.ProjectileTrajectoryMismatch + "m > " + ConVar.AntiHack.projectile_trajectory + "m)");
					Analytics.Azure.OnProjectileHackViolation(value);
					stats.combat.LogInvalid(hitInfo, "trajectory_update_total");
					flag9 = false;
				}
				hitInfo.ProjectileVelocity = velocity;
				if (playerProjectileAttack.hitVelocity != Vector3.zero && velocity != Vector3.zero)
				{
					float num29 = Vector3.Angle(playerProjectileAttack.hitVelocity, velocity);
					float num30 = playerProjectileAttack.hitVelocity.magnitude / velocity.magnitude;
					if (num29 > ConVar.AntiHack.projectile_anglechange)
					{
						string text21 = value.projectilePrefab.name;
						string text22 = (flag6 ? hitEntity.ShortPrefabName : "world");
						AntiHack.Log(this, AntiHackType.ProjectileHack, "Trajectory angle change (" + text21 + " on " + text22 + " with " + num29 + "deg > " + ConVar.AntiHack.projectile_anglechange + "deg)");
						Analytics.Azure.OnProjectileHackViolation(value);
						stats.combat.LogInvalid(hitInfo, "angle_change");
						flag9 = false;
					}
					if (num30 > ConVar.AntiHack.projectile_velocitychange)
					{
						string text23 = value.projectilePrefab.name;
						string text24 = (flag6 ? hitEntity.ShortPrefabName : "world");
						AntiHack.Log(this, AntiHackType.ProjectileHack, "Trajectory velocity change (" + text23 + " on " + text24 + " with " + num30 + " > " + ConVar.AntiHack.projectile_velocitychange + ")");
						Analytics.Azure.OnProjectileHackViolation(value);
						stats.combat.LogInvalid(hitInfo, "velocity_change");
						flag9 = false;
					}
				}
				float magnitude3 = velocity.magnitude;
				float num31 = num13 * magnitude3;
				if (num14 < num31)
				{
					string text25 = hitInfo.ProjectilePrefab.name;
					string text26 = (flag6 ? hitEntity.ShortPrefabName : "world");
					AntiHack.Log(this, AntiHackType.ProjectileHack, "Projectile too slow (" + text25 + " on " + text26 + " with " + num14 + "m < " + num31 + "m in " + num13 + "s)");
					Analytics.Azure.OnProjectileHackViolation(value);
					stats.combat.LogInvalid(hitInfo, "projectile_minspeed");
					flag9 = false;
				}
			}
			if (value.protection >= 3)
			{
				position2 = value.position;
				pointStart = hitInfo.PointStart;
				hitPositionWorld = hitInfo.HitPositionWorld;
				if (!flag8)
				{
					hitPositionWorld -= hitInfo.ProjectileVelocity.normalized * 0.001f;
				}
				vector = hitInfo.PositionOnRay(hitPositionWorld);
				Vector3 vector2 = Vector3.zero;
				Vector3 vector3 = Vector3.zero;
				if (ConVar.AntiHack.projectile_backtracking > 0f)
				{
					vector2 = (pointStart - position2).normalized * ConVar.AntiHack.projectile_backtracking;
					vector3 = (vector - pointStart).normalized * ConVar.AntiHack.projectile_backtracking;
				}
				flag10 = GamePhysics.LineOfSight(position2 - vector2, pointStart + vector2, num15, value.lastEntityHit) && GamePhysics.LineOfSight(pointStart - vector3, vector, num15, value.lastEntityHit) && GamePhysics.LineOfSight(vector, hitPositionWorld, num15, value.lastEntityHit);
				bool flag11 = true;
				if (flag10)
				{
					flag11 = GamePhysics.LineOfSight(position2, hitPositionWorld, num15, value.lastEntityHit) && GamePhysics.LineOfSight(hitPositionWorld, position2, num15, value.lastEntityHit);
				}
				bool flag12 = true;
				if (flag10)
				{
					List<Vector3> simulatedPositions = value.simulatedPositions;
					if (simulatedPositions.Count > ConVar.AntiHack.projectile_update_limit)
					{
						flag12 = false;
					}
					else
					{
						simulatedPositions.Add(position2);
						for (int i = 1; i < simulatedPositions.Count; i++)
						{
							if (!GamePhysics.LineOfSight(simulatedPositions[i - 1], simulatedPositions[i], num15, value.lastEntityHit) || !GamePhysics.LineOfSight(simulatedPositions[i], simulatedPositions[i - 1], num15, value.lastEntityHit))
							{
								flag12 = false;
								break;
							}
						}
					}
				}
				if (flag10)
				{
					if (!(value.simulatedPositions.Count > 1 && flag12))
					{
						num32 = ((value.simulatedPositions.Count <= 1 && flag11) ? 1 : 0);
						if (num32 == 0)
						{
							goto IL_12c5;
						}
					}
					else
					{
						num32 = 1;
					}
					stats.Add("hit_" + (flag6 ? hitEntity.Categorize() : "world") + "_direct_los", 1, Stats.Server);
					goto IL_1323;
				}
				num32 = 0;
				goto IL_12c5;
			}
			goto IL_1595;
		}
		goto IL_15ae;
		IL_15ae:
		value.position = hitInfo.HitPositionWorld;
		value.velocity = playerProjectileAttack.hitVelocity;
		value.travelTime = num;
		value.partialTime = partialTime;
		value.hits++;
		value.lastEntityHit = hitEntity;
		value.simulatedPositions.Clear();
		value.simulatedPositions.Add(position);
		hitInfo.ProjectilePrefab.CalculateDamage(hitInfo, value.projectileModifier, value.integrity);
		if (flag8)
		{
			if (hitInfo.ProjectilePrefab.waterIntegrityLoss > 0f)
			{
				value.integrity = Mathf.Clamp01(value.integrity - hitInfo.ProjectilePrefab.waterIntegrityLoss);
			}
		}
		else if (hitInfo.ProjectilePrefab.penetrationPower <= 0f || !flag6)
		{
			value.integrity = 0f;
		}
		else
		{
			float num33 = hitEntity.PenetrationResistance(hitInfo) / hitInfo.ProjectilePrefab.penetrationPower;
			value.integrity = Mathf.Clamp01(value.integrity - num33);
		}
		if (flag6)
		{
			stats.Add(value.itemMod.category + "_hit_" + hitEntity.Categorize(), 1);
		}
		if (value.integrity <= 0f)
		{
			if (value.hits <= ConVar.AntiHack.projectile_impactspawndepth)
			{
				value.itemMod.ServerProjectileHit(hitInfo);
			}
			if (hitInfo.ProjectilePrefab.remainInWorld)
			{
				CreateWorldProjectile(hitInfo, value.itemDef, value.itemMod, hitInfo.ProjectilePrefab, value.pickupItem);
			}
		}
		else if (value.hits == ConVar.AntiHack.projectile_impactspawndepth)
		{
			value.itemMod.ServerProjectileHit(hitInfo);
		}
		firedProjectiles[playerAttack.projectileID] = value;
		if (flag6)
		{
			if (value.hits <= ConVar.AntiHack.projectile_damagedepth)
			{
				hitEntity.OnAttacked(hitInfo);
			}
			else
			{
				stats.combat.LogInvalid(hitInfo, "ricochet");
			}
		}
		hitInfo.DoHitEffects = hitInfo.ProjectilePrefab.doDefaultHitEffects;
		Effect.server.ImpactEffect(hitInfo);
		SingletonComponent<NpcNoiseManager>.Instance.OnProjectileHit(this, hitInfo);
		playerProjectileAttack.ResetToPool();
		playerProjectileAttack = null;
		return;
		IL_12c5:
		stats.Add("hit_" + (flag6 ? hitEntity.Categorize() : "world") + "_indirect_los", 1, Stats.Server);
		goto IL_1323;
		IL_1323:
		if (num32 == 0)
		{
			string text27 = hitInfo.ProjectilePrefab.name;
			string text28 = (flag6 ? hitEntity.ShortPrefabName : "world");
			string description = ((!flag10) ? "projectile_los" : "projectile_los_detailed");
			string[] obj = new string[12]
			{
				"Line of sight (", text27, " on ", text28, ") ", null, null, null, null, null,
				null, null
			};
			Vector3 vector4 = position2;
			obj[5] = vector4.ToString();
			obj[6] = " ";
			vector4 = pointStart;
			obj[7] = vector4.ToString();
			obj[8] = " ";
			vector4 = vector;
			obj[9] = vector4.ToString();
			obj[10] = " ";
			vector4 = hitPositionWorld;
			obj[11] = vector4.ToString();
			AntiHack.Log(this, AntiHackType.ProjectileHack, string.Concat(obj));
			Analytics.Azure.OnProjectileHackViolation(value);
			stats.combat.LogInvalid(hitInfo, description);
			flag9 = false;
		}
		if (flag9 && flag && !flag7)
		{
			Vector3 hitPositionWorld2 = hitInfo.HitPositionWorld;
			Vector3 position3 = basePlayer.eyes.position;
			Vector3 vector5 = basePlayer.CenterPoint();
			float projectile_losforgiveness = ConVar.AntiHack.projectile_losforgiveness;
			bool flag13 = GamePhysics.LineOfSight(hitPositionWorld2, position3, num15, 0f, projectile_losforgiveness) && GamePhysics.LineOfSight(position3, hitPositionWorld2, num15, projectile_losforgiveness, 0f);
			if (!flag13)
			{
				flag13 = GamePhysics.LineOfSight(hitPositionWorld2, vector5, num15, 0f, projectile_losforgiveness) && GamePhysics.LineOfSight(vector5, hitPositionWorld2, num15, projectile_losforgiveness, 0f);
			}
			if (!flag13)
			{
				string text29 = hitInfo.ProjectilePrefab.name;
				string text30 = (flag6 ? hitEntity.ShortPrefabName : "world");
				string[] obj2 = new string[12]
				{
					"Line of sight (", text29, " on ", text30, ") ", null, null, null, null, null,
					null, null
				};
				Vector3 vector4 = hitPositionWorld2;
				obj2[5] = vector4.ToString();
				obj2[6] = " ";
				vector4 = position3;
				obj2[7] = vector4.ToString();
				obj2[8] = " or ";
				vector4 = hitPositionWorld2;
				obj2[9] = vector4.ToString();
				obj2[10] = " ";
				vector4 = vector5;
				obj2[11] = vector4.ToString();
				AntiHack.Log(this, AntiHackType.ProjectileHack, string.Concat(obj2));
				Analytics.Azure.OnProjectileHackViolation(value);
				stats.combat.LogInvalid(hitInfo, "projectile_los");
				flag9 = false;
			}
		}
		goto IL_1595;
		IL_1595:
		if (!flag9)
		{
			AntiHack.AddViolation(this, AntiHackType.ProjectileHack, ConVar.AntiHack.projectile_penalty);
			playerProjectileAttack.ResetToPool();
			playerProjectileAttack = null;
			return;
		}
		goto IL_15ae;
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	public void OnProjectileRicochet(RPCMessage msg)
	{
		PlayerProjectileRicochet playerProjectileRicochet = PlayerProjectileRicochet.Deserialize(msg.read);
		if (playerProjectileRicochet != null)
		{
			FiredProjectile value;
			if (playerProjectileRicochet.hitPosition.IsNaNOrInfinity() || playerProjectileRicochet.inVelocity.IsNaNOrInfinity() || playerProjectileRicochet.outVelocity.IsNaNOrInfinity() || playerProjectileRicochet.hitNormal.IsNaNOrInfinity() || float.IsNaN(playerProjectileRicochet.travelTime) || float.IsInfinity(playerProjectileRicochet.travelTime))
			{
				AntiHack.Log(this, AntiHackType.ProjectileHack, "Contains NaN (" + playerProjectileRicochet.projectileID + ")");
				playerProjectileRicochet.ResetToPool();
				playerProjectileRicochet = null;
			}
			else if (!firedProjectiles.TryGetValue(playerProjectileRicochet.projectileID, out value))
			{
				AntiHack.Log(this, AntiHackType.ProjectileHack, "Missing ID (" + playerProjectileRicochet.projectileID + ")", logToAnalytics: false);
				playerProjectileRicochet.ResetToPool();
				playerProjectileRicochet = null;
			}
			else if (value.firedTime < UnityEngine.Time.realtimeSinceStartup - 8f)
			{
				AntiHack.Log(this, AntiHackType.ProjectileHack, "Lifetime is zero (" + playerProjectileRicochet.projectileID + ")");
				playerProjectileRicochet.ResetToPool();
				playerProjectileRicochet = null;
			}
			else
			{
				value.ricochets++;
				firedProjectiles[playerProjectileRicochet.projectileID] = value;
				playerProjectileRicochet.ResetToPool();
				playerProjectileRicochet = null;
			}
		}
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	public void OnProjectileUpdate(RPCMessage msg)
	{
		PlayerProjectileUpdate playerProjectileUpdate = PlayerProjectileUpdate.Deserialize(msg.read);
		if (playerProjectileUpdate == null)
		{
			return;
		}
		if (playerProjectileUpdate.curPosition.IsNaNOrInfinity() || playerProjectileUpdate.curVelocity.IsNaNOrInfinity() || float.IsNaN(playerProjectileUpdate.travelTime) || float.IsInfinity(playerProjectileUpdate.travelTime))
		{
			AntiHack.Log(this, AntiHackType.ProjectileHack, "Contains NaN (" + playerProjectileUpdate.projectileID + ")");
			playerProjectileUpdate.ResetToPool();
			playerProjectileUpdate = null;
			return;
		}
		if (!firedProjectiles.TryGetValue(playerProjectileUpdate.projectileID, out var value))
		{
			AntiHack.Log(this, AntiHackType.ProjectileHack, "Missing ID (" + playerProjectileUpdate.projectileID + ")", logToAnalytics: false);
			playerProjectileUpdate.ResetToPool();
			playerProjectileUpdate = null;
			return;
		}
		if (value.firedTime < UnityEngine.Time.realtimeSinceStartup - 8f)
		{
			AntiHack.Log(this, AntiHackType.ProjectileHack, "Lifetime is zero (" + playerProjectileUpdate.projectileID + ")");
			Analytics.Azure.OnProjectileHackViolation(value);
			playerProjectileUpdate.ResetToPool();
			playerProjectileUpdate = null;
			return;
		}
		if (value.ricochets > 0)
		{
			AntiHack.Log(this, AntiHackType.ProjectileHack, "Projectile is ricochet (" + playerProjectileUpdate.projectileID + ")");
			Analytics.Azure.OnProjectileHackViolation(value);
			playerProjectileUpdate.ResetToPool();
			playerProjectileUpdate = null;
			return;
		}
		Vector3 position = value.position;
		Vector3 positionOffset = value.positionOffset;
		Vector3 velocity = value.velocity;
		float num = value.trajectoryMismatch;
		float partialTime = value.partialTime;
		float travelTime = value.travelTime;
		float num2 = Mathf.Clamp(playerProjectileUpdate.travelTime, value.travelTime, 8f);
		Vector3 vector = UnityEngine.Physics.gravity * value.projectilePrefab.gravityModifier;
		float drag = value.projectilePrefab.drag;
		if (value.protection > 0)
		{
			float num3 = 1f - ConVar.AntiHack.projectile_forgiveness;
			float num4 = 1f + ConVar.AntiHack.projectile_forgiveness;
			float projectile_clientframes = ConVar.AntiHack.projectile_clientframes;
			float projectile_serverframes = ConVar.AntiHack.projectile_serverframes;
			float num5 = Mathx.Decrement(value.firedTime);
			float num6 = Mathf.Clamp(Mathx.Increment(UnityEngine.Time.realtimeSinceStartup) - num5, 0f, 8f);
			float num7 = num2;
			float num8 = (value.desyncLifeTime = Mathf.Abs(num6 - num7));
			float num9 = Mathf.Min(num6, num7);
			float num10 = projectile_clientframes / 60f;
			float num11 = projectile_serverframes * Mathx.Max(UnityEngine.Time.deltaTime, UnityEngine.Time.smoothDeltaTime, UnityEngine.Time.fixedDeltaTime);
			float num12 = (num9 + desyncTimeClamped + num10 + num11) * num4;
			float num13 = Mathf.Max(0f, (num9 - desyncTimeClamped - num10 - num11) * num3);
			int num14 = 1075904512;
			if (ConVar.AntiHack.projectile_terraincheck)
			{
				num14 |= 0x800000;
			}
			if (ConVar.AntiHack.projectile_vehiclecheck)
			{
				num14 |= 0x8000000;
			}
			if (value.protection >= 1)
			{
				float num15 = value.projectilePrefab.initialDistance + num12 * value.initialVelocity.magnitude;
				float num16 = Vector3.Distance(value.initialPosition, playerProjectileUpdate.curPosition);
				if (num16 > num15)
				{
					string text = value.projectilePrefab.name;
					AntiHack.Log(this, AntiHackType.ProjectileHack, "Projectile distance (" + text + " with " + num16 + "m > " + num15 + "m in " + num12 + "s)");
					Analytics.Azure.OnProjectileHackViolation(value);
					playerProjectileUpdate.ResetToPool();
					playerProjectileUpdate = null;
					return;
				}
				if (num8 > ConVar.AntiHack.projectile_desync)
				{
					string text2 = value.projectilePrefab.name;
					AntiHack.Log(this, AntiHackType.ProjectileHack, "Projectile desync (" + text2 + " with " + num8 + "s > " + ConVar.AntiHack.projectile_desync + "s)");
					Analytics.Azure.OnProjectileHackViolation(value);
					playerProjectileUpdate.ResetToPool();
					playerProjectileUpdate = null;
					return;
				}
				Vector3 curVelocity = playerProjectileUpdate.curVelocity;
				Vector3 vector2 = value.initialVelocity;
				Vector3 vector3 = ((value.hits == 0) ? vector2 : value.velocity);
				float num17 = drag * (1f / 32f);
				Vector3 vector4 = vector * (1f / 32f);
				int num18 = Mathf.FloorToInt(num13 / (1f / 32f));
				int num19 = Mathf.CeilToInt(num12 / (1f / 32f));
				for (int i = 0; i < num18; i++)
				{
					vector2 += vector4;
					vector2 -= vector2 * num17;
					vector3 += vector4;
					vector3 -= vector3 * num17;
				}
				float magnitude = curVelocity.magnitude;
				float magnitude2 = vector2.magnitude;
				float magnitude3 = vector3.magnitude;
				for (int j = num18; j < num19; j++)
				{
					vector2 += vector4;
					vector2 -= vector2 * num17;
					vector3 += vector4;
					vector3 -= vector3 * num17;
				}
				magnitude3 = Mathf.Min(magnitude3, vector3.magnitude);
				magnitude2 = Mathf.Max(magnitude2, vector2.magnitude);
				if (magnitude < magnitude3 * num3)
				{
					string text3 = value.projectilePrefab.name;
					AntiHack.Log(this, AntiHackType.ProjectileHack, "Projectile velocity too low (" + text3 + " with " + magnitude + " < " + magnitude3 + ")");
					Analytics.Azure.OnProjectileHackViolation(value);
					playerProjectileUpdate.ResetToPool();
					playerProjectileUpdate = null;
					return;
				}
				if (magnitude > magnitude2 * num4)
				{
					string text4 = value.projectilePrefab.name;
					AntiHack.Log(this, AntiHackType.ProjectileHack, "Projectile velocity too high (" + text4 + " with " + magnitude + " > " + magnitude2 + ")");
					Analytics.Azure.OnProjectileHackViolation(value);
					playerProjectileUpdate.ResetToPool();
					playerProjectileUpdate = null;
					return;
				}
			}
			if (value.protection >= 3)
			{
				Vector3 position2 = value.position;
				Vector3 curPosition = playerProjectileUpdate.curPosition;
				Vector3 vector5 = Vector3.zero;
				if (ConVar.AntiHack.projectile_backtracking > 0f)
				{
					vector5 = (curPosition - position2).normalized * ConVar.AntiHack.projectile_backtracking;
				}
				if (!GamePhysics.LineOfSight(position2 - vector5, curPosition + vector5, num14, value.lastEntityHit))
				{
					string text5 = value.projectilePrefab.name;
					string[] obj = new string[6] { "Line of sight (", text5, " on update) ", null, null, null };
					Vector3 vector6 = position2;
					obj[3] = vector6.ToString();
					obj[4] = " ";
					vector6 = curPosition;
					obj[5] = vector6.ToString();
					AntiHack.Log(this, AntiHackType.ProjectileHack, string.Concat(obj));
					Analytics.Azure.OnProjectileHackViolation(value);
					playerProjectileUpdate.ResetToPool();
					playerProjectileUpdate = null;
					return;
				}
			}
			if (value.protection >= 4)
			{
				SimulateProjectile(ref position, ref velocity, ref partialTime, num2 - travelTime, vector, drag, out var prevPosition, out var prevVelocity);
				value.simulatedPositions.Add(position);
				num += Mathf.Max(new Line(prevPosition - prevVelocity, position + prevVelocity).Distance(playerProjectileUpdate.curPosition) - positionOffset.magnitude, 0f);
			}
			if (value.protection >= 5)
			{
				if (value.inheritedVelocity != Vector3.zero)
				{
					Vector3 curVelocity2 = value.inheritedVelocity + velocity;
					Vector3 curVelocity3 = playerProjectileUpdate.curVelocity;
					if (curVelocity3.magnitude > 2f * curVelocity2.magnitude || curVelocity3.magnitude < 0.5f * curVelocity2.magnitude)
					{
						playerProjectileUpdate.curVelocity = curVelocity2;
					}
					value.inheritedVelocity = Vector3.zero;
				}
				else
				{
					playerProjectileUpdate.curVelocity = velocity;
				}
			}
		}
		value.updates.Add(new FiredProjectileUpdate
		{
			OldPosition = value.position,
			NewPosition = playerProjectileUpdate.curPosition,
			OldVelocity = value.velocity,
			NewVelocity = playerProjectileUpdate.curVelocity,
			Mismatch = num,
			PartialTime = partialTime
		});
		value.position = playerProjectileUpdate.curPosition;
		value.velocity = playerProjectileUpdate.curVelocity;
		value.travelTime = playerProjectileUpdate.travelTime;
		value.partialTime = partialTime;
		value.trajectoryMismatch = num;
		value.positionOffset = default(Vector3);
		firedProjectiles[playerProjectileUpdate.projectileID] = value;
		playerProjectileUpdate.ResetToPool();
		playerProjectileUpdate = null;
	}

	private void SimulateProjectile(ref Vector3 position, ref Vector3 velocity, ref float partialTime, float travelTime, Vector3 gravity, float drag, out Vector3 prevPosition, out Vector3 prevVelocity)
	{
		float num = 1f / 32f;
		prevPosition = position;
		prevVelocity = velocity;
		if (partialTime > Mathf.Epsilon)
		{
			float num2 = num - partialTime;
			if (travelTime < num2)
			{
				prevPosition = position;
				prevVelocity = velocity;
				position += velocity * travelTime;
				partialTime += travelTime;
				return;
			}
			prevPosition = position;
			prevVelocity = velocity;
			position += velocity * num2;
			velocity += gravity * num;
			velocity -= velocity * (drag * num);
			travelTime -= num2;
		}
		int num3 = Mathf.FloorToInt(travelTime / num);
		for (int i = 0; i < num3; i++)
		{
			prevPosition = position;
			prevVelocity = velocity;
			position += velocity * num;
			velocity += gravity * num;
			velocity -= velocity * (drag * num);
		}
		partialTime = travelTime - num * (float)num3;
		if (partialTime > Mathf.Epsilon)
		{
			prevPosition = position;
			prevVelocity = velocity;
			position += velocity * partialTime;
		}
	}

	protected virtual void CreateWorldProjectile(HitInfo info, ItemDefinition itemDef, ItemModProjectile itemMod, Projectile projectilePrefab, Item recycleItem)
	{
		Vector3 projectileVelocity = info.ProjectileVelocity;
		Item item = ((recycleItem != null) ? recycleItem : ItemManager.Create(itemDef, 1, 0uL));
		BaseEntity baseEntity = null;
		if (!info.DidHit)
		{
			baseEntity = item.CreateWorldObject(info.HitPositionWorld, Quaternion.LookRotation(projectileVelocity.normalized));
			baseEntity.Kill(DestroyMode.Gib);
			return;
		}
		if (projectilePrefab.breakProbability > 0f && UnityEngine.Random.value <= projectilePrefab.breakProbability)
		{
			baseEntity = item.CreateWorldObject(info.HitPositionWorld, Quaternion.LookRotation(projectileVelocity.normalized));
			baseEntity.Kill(DestroyMode.Gib);
			return;
		}
		if (projectilePrefab.conditionLoss > 0f)
		{
			item.LoseCondition(projectilePrefab.conditionLoss * 100f);
			if (item.isBroken)
			{
				baseEntity = item.CreateWorldObject(info.HitPositionWorld, Quaternion.LookRotation(projectileVelocity.normalized));
				baseEntity.Kill(DestroyMode.Gib);
				return;
			}
		}
		if (projectilePrefab.stickProbability > 0f && UnityEngine.Random.value <= projectilePrefab.stickProbability)
		{
			baseEntity = ((info.HitEntity == null) ? item.CreateWorldObject(info.HitPositionWorld, Quaternion.LookRotation(projectileVelocity.normalized)) : ((info.HitBone != 0) ? item.CreateWorldObject(info.HitPositionLocal, Quaternion.LookRotation(info.HitNormalLocal * -1f), info.HitEntity, info.HitBone) : item.CreateWorldObject(info.HitPositionLocal, Quaternion.LookRotation(info.HitEntity.transform.InverseTransformDirection(projectileVelocity.normalized)), info.HitEntity)));
			DroppedItem droppedItem = baseEntity as DroppedItem;
			if (droppedItem != null)
			{
				droppedItem.StickIn();
			}
			else
			{
				baseEntity.GetComponent<Rigidbody>().isKinematic = true;
			}
		}
		else
		{
			baseEntity = item.CreateWorldObject(info.HitPositionWorld, Quaternion.LookRotation(projectileVelocity.normalized));
			Rigidbody component = baseEntity.GetComponent<Rigidbody>();
			component.AddForce(projectileVelocity.normalized * 200f);
			component.WakeUp();
		}
	}

	public void CleanupExpiredProjectiles()
	{
		foreach (KeyValuePair<int, FiredProjectile> item in firedProjectiles.Where((KeyValuePair<int, FiredProjectile> x) => x.Value.firedTime < UnityEngine.Time.realtimeSinceStartup - 8f - 1f).ToList())
		{
			Analytics.Azure.OnFiredProjectileRemoved(this, item.Value);
			firedProjectiles.Remove(item.Key);
			FiredProjectile obj = item.Value;
			Facepunch.Pool.Free(ref obj);
		}
	}

	public bool HasFiredProjectile(int id)
	{
		return firedProjectiles.ContainsKey(id);
	}

	public void NoteFiredProjectile(int projectileid, Vector3 startPos, Vector3 startVel, AttackEntity attackEnt, ItemDefinition firedItemDef, Guid projectileGroupId, Vector3 positionOffset, Item pickupItem = null)
	{
		BaseProjectile baseProjectile = attackEnt as BaseProjectile;
		ItemModProjectile component = firedItemDef.GetComponent<ItemModProjectile>();
		Projectile component2 = component.projectileObject.Get().GetComponent<Projectile>();
		if (startPos.IsNaNOrInfinity() || startVel.IsNaNOrInfinity())
		{
			string text = component2.name;
			AntiHack.Log(this, AntiHackType.ProjectileHack, "Contains NaN (" + text + ")");
			stats.combat.LogInvalid(this, baseProjectile, "projectile_nan");
			return;
		}
		int projectile_protection = ConVar.AntiHack.projectile_protection;
		Vector3 inheritedVelocity = ((attackEnt != null) ? attackEnt.GetInheritedVelocity(this, startVel.normalized) : Vector3.zero);
		if (projectile_protection >= 1)
		{
			float num = 1f - ConVar.AntiHack.projectile_forgiveness;
			float num2 = 1f + ConVar.AntiHack.projectile_forgiveness;
			float magnitude = startVel.magnitude;
			float num3 = component.GetMinVelocity();
			float num4 = component.GetMaxVelocity();
			BaseProjectile baseProjectile2 = attackEnt as BaseProjectile;
			if ((bool)baseProjectile2)
			{
				num3 *= baseProjectile2.GetProjectileVelocityScale();
				num4 *= baseProjectile2.GetProjectileVelocityScale(getMax: true);
			}
			num3 *= num;
			num4 *= num2;
			if (magnitude < num3)
			{
				string text2 = component2.name;
				AntiHack.Log(this, AntiHackType.ProjectileHack, "Velocity (" + text2 + " with " + magnitude + " < " + num3 + ")");
				stats.combat.LogInvalid(this, baseProjectile, "projectile_minvelocity");
				return;
			}
			if (magnitude > num4)
			{
				string text3 = component2.name;
				AntiHack.Log(this, AntiHackType.ProjectileHack, "Velocity (" + text3 + " with " + magnitude + " > " + num4 + ")");
				stats.combat.LogInvalid(this, baseProjectile, "projectile_maxvelocity");
				return;
			}
		}
		FiredProjectile firedProjectile = Facepunch.Pool.Get<FiredProjectile>();
		firedProjectile.itemDef = firedItemDef;
		firedProjectile.itemMod = component;
		firedProjectile.projectilePrefab = component2;
		firedProjectile.firedTime = UnityEngine.Time.realtimeSinceStartup;
		firedProjectile.travelTime = 0f;
		firedProjectile.weaponSource = attackEnt;
		firedProjectile.weaponPrefab = ((attackEnt == null) ? null : GameManager.server.FindPrefab(StringPool.Get(attackEnt.prefabID)).GetComponent<AttackEntity>());
		firedProjectile.projectileModifier = ((baseProjectile == null) ? Projectile.Modifier.Default : baseProjectile.GetProjectileModifier());
		firedProjectile.pickupItem = pickupItem;
		firedProjectile.integrity = 1f;
		firedProjectile.position = startPos;
		firedProjectile.initialPositionOffset = positionOffset;
		firedProjectile.positionOffset = positionOffset;
		firedProjectile.velocity = startVel;
		firedProjectile.initialPosition = startPos;
		firedProjectile.initialVelocity = startVel;
		firedProjectile.inheritedVelocity = inheritedVelocity;
		firedProjectile.protection = projectile_protection;
		firedProjectile.ricochets = 0;
		firedProjectile.hits = 0;
		firedProjectile.id = projectileid;
		firedProjectile.attacker = this;
		firedProjectile.simulatedPositions.Add(startPos);
		firedProjectiles.Add(projectileid, firedProjectile);
		Analytics.Azure.OnFiredProjectile(this, firedProjectile, projectileGroupId);
	}

	public void ServerNoteFiredProjectile(int projectileid, Vector3 startPos, Vector3 startVel, AttackEntity attackEnt, ItemDefinition firedItemDef, Item pickupItem = null)
	{
		BaseProjectile baseProjectile = attackEnt as BaseProjectile;
		ItemModProjectile component = firedItemDef.GetComponent<ItemModProjectile>();
		Projectile component2 = component.projectileObject.Get().GetComponent<Projectile>();
		int protection = 0;
		Vector3 zero = Vector3.zero;
		if (!startPos.IsNaNOrInfinity() && !startVel.IsNaNOrInfinity())
		{
			FiredProjectile firedProjectile = Facepunch.Pool.Get<FiredProjectile>();
			firedProjectile.itemDef = firedItemDef;
			firedProjectile.itemMod = component;
			firedProjectile.projectilePrefab = component2;
			firedProjectile.firedTime = UnityEngine.Time.realtimeSinceStartup;
			firedProjectile.travelTime = 0f;
			firedProjectile.weaponSource = attackEnt;
			firedProjectile.weaponPrefab = ((attackEnt == null) ? null : GameManager.server.FindPrefab(StringPool.Get(attackEnt.prefabID)).GetComponent<AttackEntity>());
			firedProjectile.projectileModifier = ((baseProjectile == null) ? Projectile.Modifier.Default : baseProjectile.GetProjectileModifier());
			firedProjectile.pickupItem = pickupItem;
			firedProjectile.integrity = 1f;
			firedProjectile.trajectoryMismatch = 0f;
			firedProjectile.position = startPos;
			firedProjectile.positionOffset = Vector3.zero;
			firedProjectile.velocity = startVel;
			firedProjectile.initialPosition = startPos;
			firedProjectile.initialVelocity = startVel;
			firedProjectile.inheritedVelocity = zero;
			firedProjectile.protection = protection;
			firedProjectile.ricochets = 0;
			firedProjectile.hits = 0;
			firedProjectile.id = projectileid;
			firedProjectile.attacker = this;
			firedProjectiles.Add(projectileid, firedProjectile);
		}
	}

	public void ApplyRadiation(float radsAmount, bool protection = true)
	{
		if (IsAlive() && !IsSleeping() && !InSafeZone())
		{
			float num = 0f;
			num = (protection ? Radiation.GetRadiationAfterProtection(radsAmount, RadiationProtection()) : Mathf.Max(0f, radsAmount));
			metabolism.ApplyChange(MetabolismAttribute.Type.Radiation, num, 0f);
		}
	}

	public void PlayerInventoryRadioactivityChange(float radAmount, bool hasRads)
	{
		if (!Radiation.water_inventory_damage)
		{
			return;
		}
		if (inflictInventoryRadsAction == null)
		{
			inflictInventoryRadsAction = InflictRadsFromInventory;
		}
		inventoryRads = radAmount;
		if (!hasRads || radAmount < 2500f)
		{
			if (IsInvoking(inflictInventoryRadsAction))
			{
				CancelInvoke(inflictInventoryRadsAction);
			}
		}
		else if (!IsInvoking(inflictInventoryRadsAction))
		{
			InvokeRepeating(inflictInventoryRadsAction, 1f, 1f);
		}
	}

	private void InflictRadsFromInventory()
	{
		if (Radiation.water_inventory_damage)
		{
			float num = inventoryRads * Radiation.MaterialToRadsRatio;
			num *= 0.05f;
			ApplyRadiation(num);
		}
	}

	public void RadioactiveLootCheck(List<ItemContainer> containerRefs)
	{
		radiationCheckContainers.Clear();
		radiationCheckContainers.AddRange(containerRefs);
		HasOpenedLoot();
	}

	private void HasOpenedLoot()
	{
		if (Radiation.water_loot_damage)
		{
			hasOpenedLoot = true;
			CheckRadsInContainer();
			InflictRadsFromContainer();
			if (inflictRadsAction == null)
			{
				inflictRadsAction = InflictRadsFromContainer;
			}
			if (checkRadsAction == null)
			{
				checkRadsAction = CheckRadsInContainer;
			}
			if (!IsInvoking(checkRadsAction))
			{
				InvokeRepeating(checkRadsAction, 1f, 2500f);
			}
			if (!IsInvoking(inflictRadsAction))
			{
				InvokeRepeating(inflictRadsAction, 1f, 1f);
			}
		}
	}

	public void HasClosedLoot()
	{
		if (IsInvoking(inflictRadsAction))
		{
			CancelInvoke(inflictRadsAction);
		}
		hasOpenedLoot = false;
	}

	private void InflictRadsFromContainer()
	{
		if (!Radiation.water_loot_damage)
		{
			return;
		}
		if (!hasOpenedLoot)
		{
			if (IsInvoking(checkRadsAction))
			{
				CancelInvoke(checkRadsAction);
			}
			if (IsInvoking(inflictRadsAction))
			{
				CancelInvoke(inflictRadsAction);
			}
		}
		else
		{
			ApplyRadiation(containerRads);
		}
	}

	private void CheckRadsInContainer()
	{
		if (!hasOpenedLoot)
		{
			return;
		}
		containerRads = 0f;
		foreach (ItemContainer radiationCheckContainer in radiationCheckContainers)
		{
			containerRads += radiationCheckContainer.GetRadioactiveMaterialInContainer() * Radiation.MaterialToRadsRatio;
		}
		containerRads *= 0.05f;
	}

	public override bool CanUseNetworkCache(Network.Connection connection)
	{
		if (net == null)
		{
			return true;
		}
		if (connection.authLevel != 0)
		{
			return false;
		}
		if (net.connection != connection)
		{
			return true;
		}
		return false;
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		HandleMountedOnLoad();
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		bool flag = net != null && net.connection == info.forConnection;
		bool flag2 = !info.forDisk && info.forConnection.player != null && info.forConnection.player is BasePlayer basePlayer && basePlayer.IsAdmin;
		info.msg.basePlayer = Facepunch.Pool.Get<ProtoBuf.BasePlayer>();
		info.msg.basePlayer.userid = userID;
		info.msg.basePlayer.name = displayName;
		info.msg.basePlayer.playerFlags = (int)playerFlags;
		info.msg.basePlayer.currentTeam = currentTeam;
		info.msg.basePlayer.heldEntity = svActiveItemID;
		info.msg.basePlayer.reputation = reputation;
		if (!info.forDisk && currentGesture != null && currentGesture.animationType == GestureConfig.AnimationType.Loop)
		{
			info.msg.basePlayer.loopingGesture = currentGesture.gestureId;
		}
		if (IsConnected && (IsAdmin || IsDeveloper))
		{
			info.msg.basePlayer.skinCol = net.connection.info.GetFloat("global.skincol", -1f);
			info.msg.basePlayer.skinTex = net.connection.info.GetFloat("global.skintex", -1f);
			info.msg.basePlayer.skinMesh = net.connection.info.GetFloat("global.skinmesh", -1f);
		}
		else
		{
			info.msg.basePlayer.skinCol = -1f;
			info.msg.basePlayer.skinTex = -1f;
			info.msg.basePlayer.skinMesh = -1f;
		}
		info.msg.basePlayer.underwear = GetUnderwearSkin();
		if (info.forDisk || flag)
		{
			info.msg.basePlayer.metabolism = metabolism.Save();
			info.msg.basePlayer.modifiers = null;
			if (modifiers != null)
			{
				info.msg.basePlayer.modifiers = modifiers.Save();
			}
		}
		if (!info.forDisk && !flag)
		{
			info.msg.basePlayer.playerFlags &= -5;
			info.msg.basePlayer.playerFlags &= -129;
			if (info.msg.baseCombat != null && !flag2)
			{
				info.msg.baseCombat.health = 100f;
			}
		}
		info.msg.basePlayer.inventory = inventory.Save(info.forDisk || flag);
		modelState.sleeping = IsSleeping();
		modelState.relaxed = IsRelaxed();
		modelState.crawling = IsCrawling();
		modelState.loading = IsLoadingAfterTransfer();
		info.msg.basePlayer.modelState = modelState.Copy();
		if (info.forDisk)
		{
			BaseEntity baseEntity = mounted.Get(base.isServer);
			if (baseEntity.IsValid())
			{
				if (baseEntity.enableSaving)
				{
					info.msg.basePlayer.mounted = mounted.uid;
				}
				else
				{
					BaseVehicle mountedVehicle = GetMountedVehicle();
					if (mountedVehicle.IsValid() && mountedVehicle.enableSaving)
					{
						info.msg.basePlayer.mounted = mountedVehicle.net.ID;
					}
				}
			}
			info.msg.basePlayer.respawnId = respawnId;
		}
		else
		{
			info.msg.basePlayer.mounted = mounted.uid;
		}
		if (flag)
		{
			info.msg.basePlayer.persistantData = PersistantPlayerInfo.Copy();
			if (!info.forDisk && State.missions != null)
			{
				info.msg.basePlayer.missions = State.missions.Copy();
			}
		}
		info.msg.basePlayer.bagCount = SleepingBag.GetSleepingBagCount(userID);
		info.msg.basePlayer.shelterCount = LegacyShelter.GetShelterCount(userID);
		if (info.forDisk)
		{
			info.msg.basePlayer.loadingTimeout = timeUntilLoadingExpires;
			info.msg.basePlayer.currentLife = lifeStory;
			info.msg.basePlayer.previousLife = previousLifeStory;
		}
		if (!info.forDisk)
		{
			info.msg.basePlayer.clanId = clanId;
		}
		if (info.forDisk)
		{
			info.msg.basePlayer.itemCrafter = inventory.crafting.Save();
		}
		if (info.forDisk)
		{
			SavePlayerState();
		}
		info.msg.basePlayer.tutorialAllowance = (int)CurrentTutorialAllowance;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.basePlayer == null)
		{
			return;
		}
		ProtoBuf.BasePlayer basePlayer = info.msg.basePlayer;
		userID = basePlayer.userid;
		UserIDString = userID.Get().ToString();
		if (basePlayer.name != null)
		{
			displayName = basePlayer.name;
		}
		_ = playerFlags;
		playerFlags = (PlayerFlags)basePlayer.playerFlags;
		currentTeam = basePlayer.currentTeam;
		reputation = basePlayer.reputation;
		if (basePlayer.metabolism != null)
		{
			metabolism.Load(basePlayer.metabolism);
		}
		if (basePlayer.modifiers != null && modifiers != null)
		{
			modifiers.Load(basePlayer.modifiers);
		}
		if (basePlayer.inventory != null)
		{
			inventory.Load(basePlayer.inventory);
		}
		if (basePlayer.modelState != null)
		{
			if (modelState != null)
			{
				modelState.ResetToPool();
				modelState = null;
			}
			modelState = basePlayer.modelState;
			basePlayer.modelState = null;
		}
		if (info.fromDisk)
		{
			timeUntilLoadingExpires = info.msg.basePlayer.loadingTimeout;
			if ((float)timeUntilLoadingExpires > 0f)
			{
				float time = Mathf.Clamp(timeUntilLoadingExpires, 0f, Nexus.loadingTimeout);
				Invoke(RemoveLoadingPlayerFlag, time);
			}
			lifeStory = info.msg.basePlayer.currentLife;
			if (lifeStory != null)
			{
				lifeStory.ShouldPool = false;
			}
			previousLifeStory = info.msg.basePlayer.previousLife;
			if (previousLifeStory != null)
			{
				previousLifeStory.ShouldPool = false;
			}
			SetPlayerFlag(PlayerFlags.Sleeping, b: false);
			StartSleeping();
			SetPlayerFlag(PlayerFlags.Connected, b: false);
			if (lifeStory == null && IsAlive())
			{
				LifeStoryStart();
			}
			mounted.uid = info.msg.basePlayer.mounted;
			if (IsWounded())
			{
				Die();
			}
			respawnId = info.msg.basePlayer.respawnId;
			if (info.msg.basePlayer.itemCrafter?.queue != null)
			{
				inventory.crafting.Load(info.msg.basePlayer.itemCrafter);
			}
		}
		if (!info.fromDisk)
		{
			clanId = info.msg.basePlayer.clanId;
		}
		CurrentTutorialAllowance = (TutorialItemAllowance)info.msg.basePlayer.tutorialAllowance;
	}

	public void SetRcEntityPosition(Vector3? pos)
	{
		RcEntityPosition = pos;
	}

	internal override void OnParentRemoved()
	{
		if (IsNpc)
		{
			base.OnParentRemoved();
		}
		else
		{
			SetParent(null, worldPositionStays: true, sendImmediate: true);
		}
	}

	public override void OnParentChanging(BaseEntity oldParent, BaseEntity newParent)
	{
		if (oldParent != null)
		{
			TransformState(oldParent.transform.localToWorldMatrix);
		}
		if (newParent != null)
		{
			TransformState(newParent.transform.worldToLocalMatrix);
		}
	}

	private void TransformState(Matrix4x4 matrix)
	{
		tickInterpolator.TransformEntries(matrix);
		tickHistory.TransformEntries(matrix);
		Vector3 euler = new Vector3(0f, matrix.rotation.eulerAngles.y, 0f);
		eyes.bodyRotation = Quaternion.Euler(euler) * eyes.bodyRotation;
	}

	public bool CanSuicide()
	{
		if (IsAdmin || IsDeveloper)
		{
			return true;
		}
		return UnityEngine.Time.realtimeSinceStartup > nextSuicideTime;
	}

	public void MarkSuicide()
	{
		nextSuicideTime = UnityEngine.Time.realtimeSinceStartup + 60f;
	}

	public bool CanRespawn()
	{
		return UnityEngine.Time.realtimeSinceStartup > nextRespawnTime;
	}

	public void MarkRespawn(float nextSpawnDelay = 5f)
	{
		nextRespawnTime = UnityEngine.Time.realtimeSinceStartup + nextSpawnDelay;
	}

	public Item GetActiveItem()
	{
		if (!svActiveItemID.IsValid)
		{
			return null;
		}
		if (IsDead())
		{
			return null;
		}
		if (inventory == null || inventory.containerBelt == null)
		{
			return null;
		}
		return inventory.containerBelt.FindItemByUID(svActiveItemID);
	}

	public void MovePosition(Vector3 newPos)
	{
		base.transform.position = newPos;
		if (parentEntity.Get(base.isServer) != null)
		{
			tickInterpolator.Reset(parentEntity.Get(base.isServer).transform.InverseTransformPoint(newPos));
		}
		else
		{
			tickInterpolator.Reset(newPos);
		}
		ticksPerSecond.Increment();
		tickHistory.AddPoint(newPos, tickHistoryCapacity);
		NetworkPositionTick();
	}

	public void OverrideViewAngles(Vector3 newAng)
	{
		viewAngles = newAng;
	}

	public override void ServerInit()
	{
		stats = new PlayerStatistics(this);
		if ((ulong)userID == 0L)
		{
			userID = (ulong)UnityEngine.Random.Range(0, 10000000);
			UserIDString = userID.Get().ToString();
			displayName = UserIDString;
			bots.Add(this);
		}
		EnablePlayerCollider();
		SetPlayerRigidbodyState(!IsSleeping());
		base.ServerInit();
		eyes.bodyRotation = base.transform.rotation;
		Query.Server.AddPlayer(this);
		inventory.ServerInit(this);
		metabolism.ServerInit(this);
		if (modifiers != null)
		{
			modifiers.ServerInit(this);
		}
		if (recentWaveTargets != null)
		{
			recentWaveTargets.Clear();
		}
	}

	internal override void DoServerDestroy()
	{
		base.DoServerDestroy();
		Query.Server.RemovePlayer(this);
		if ((bool)inventory)
		{
			inventory.DoDestroy();
		}
		sleepingPlayerList.Remove(this);
		if (IsBot)
		{
			bots.Remove(this);
		}
		SavePlayerState();
		if (cachedPersistantPlayer != null)
		{
			Facepunch.Pool.Free(ref cachedPersistantPlayer);
		}
	}

	protected void ServerUpdate(float deltaTime)
	{
		if (!Network.Net.sv.IsConnected())
		{
			return;
		}
		LifeStoryUpdate(deltaTime, IsOnGround() ? estimatedSpeed : 0f);
		FinalizeTick(deltaTime);
		if (ServerOcclusion.OcclusionEnabled)
		{
			List<Network.Connection> subscribers = GetSubscribers();
			if (subscribers != null && subscribers.Count > 0)
			{
				foreach (Network.Connection item in subscribers)
				{
					BasePlayer basePlayer = item.player as BasePlayer;
					if (!(basePlayer == null))
					{
						ShouldNetworkTo(basePlayer);
					}
				}
			}
		}
		ThinkMissions(deltaTime);
		desyncTimeRaw = Mathf.Max(timeSinceLastTick - deltaTime, 0f);
		desyncTimeClamped = Mathf.Min(desyncTimeRaw, ConVar.AntiHack.maxdesync);
		if (ConVar.AntiHack.terrain_protection > 0 && UnityEngine.Time.frameCount % ConVar.AntiHack.terrain_timeslice == (uint)net.ID.Value % ConVar.AntiHack.terrain_timeslice && !AntiHack.ShouldIgnore(this))
		{
			bool flag = false;
			if (AntiHack.IsInsideTerrain(this))
			{
				flag = true;
				AntiHack.AddViolation(this, AntiHackType.InsideTerrain, ConVar.AntiHack.terrain_penalty);
			}
			else if (ConVar.AntiHack.terrain_check_geometry && AntiHack.IsInsideMesh(eyes.position))
			{
				flag = true;
				AntiHack.AddViolation(this, AntiHackType.InsideGeometry, ConVar.AntiHack.terrain_penalty);
				AntiHack.Log(this, AntiHackType.InsideGeometry, "Seems to be clipped inside " + AntiHack.isInsideRayHit.collider.name);
			}
			if (flag && ConVar.AntiHack.terrain_kill)
			{
				Analytics.Azure.OnTerrainHackViolation(this);
				Hurt(1000f, DamageType.Suicide, this, useProtection: false);
				return;
			}
		}
		float serverTickInterval = Player.serverTickInterval;
		if (!(UnityEngine.Time.realtimeSinceStartup < lastPlayerTick + serverTickInterval))
		{
			if (lastPlayerTick < UnityEngine.Time.realtimeSinceStartup - serverTickInterval * 100f)
			{
				lastPlayerTick = UnityEngine.Time.realtimeSinceStartup - UnityEngine.Random.Range(0f, serverTickInterval);
			}
			while (lastPlayerTick < UnityEngine.Time.realtimeSinceStartup)
			{
				lastPlayerTick += serverTickInterval;
			}
			if (IsConnected)
			{
				ConnectedPlayerUpdate(serverTickInterval);
			}
			if (!IsNpc)
			{
				TickPings();
			}
		}
	}

	private void ServerUpdateBots(float deltaTime)
	{
		RefreshColliderSize(forced: false);
	}

	private void ConnectedPlayerUpdate(float deltaTime)
	{
		if (IsReceivingSnapshot)
		{
			net.UpdateSubscriptions(int.MaxValue, int.MaxValue);
		}
		else if (UnityEngine.Time.realtimeSinceStartup > lastSubscriptionTick + ConVar.Server.entitybatchtime && net.UpdateSubscriptions(ConVar.Server.entitybatchsize * 2, ConVar.Server.entitybatchsize))
		{
			lastSubscriptionTick = UnityEngine.Time.realtimeSinceStartup;
		}
		SendEntityUpdate();
		if (IsReceivingSnapshot)
		{
			if (SnapshotQueue.Length == 0 && EACServer.IsAuthenticated(net.connection))
			{
				EnterGame();
			}
			return;
		}
		if (IsAlive())
		{
			metabolism.ServerUpdate(this, deltaTime);
			if (modifiers != null && !IsReceivingSnapshot)
			{
				modifiers.ServerUpdate(this);
			}
			if (InSafeZone())
			{
				float num = 0f;
				HeldEntity heldEntity = GetHeldEntity();
				if ((bool)heldEntity && heldEntity.hostile)
				{
					num = deltaTime;
				}
				if (num == 0f)
				{
					MarkWeaponDrawnDuration(0f);
				}
				else
				{
					AddWeaponDrawnDuration(num);
				}
				if (weaponDrawnDuration >= 8f)
				{
					MarkHostileFor(30f);
				}
			}
			else
			{
				MarkWeaponDrawnDuration(0f);
			}
			if (PlayHeavyLandingAnimation && !modelState.mounted && modelState.onground && Parachute.LandingAnimations)
			{
				Server_StartGesture(GestureCollection.HeavyLandingId);
				PlayHeavyLandingAnimation = false;
			}
			if (timeSinceLastTick > (float)ConVar.Server.playertimeout)
			{
				lastTickTime = 0f;
				Kick("Unresponsive");
				return;
			}
		}
		if (stallProtectionTime > 0f)
		{
			stallProtectionTime -= UnityEngine.Time.deltaTime;
		}
		int num2 = (int)net.connection.GetSecondsConnected();
		int num3 = num2 - secondsConnected;
		if (num3 > 0)
		{
			stats.Add("time", num3, Stats.Server);
			secondsConnected = num2;
		}
		if (IsLoadingAfterTransfer())
		{
			Debug.LogWarning("Force removing loading flag for player (sanity check failed)", this);
			SetPlayerFlag(PlayerFlags.LoadingAfterTransfer, b: false);
		}
		RefreshColliderSize(forced: false);
		SendModelState();
	}

	private void EnterGame()
	{
		SetPlayerFlag(PlayerFlags.ReceivingSnapshot, b: false);
		bool flag = false;
		if (IsLoadingAfterTransfer())
		{
			SetPlayerFlag(PlayerFlags.LoadingAfterTransfer, b: false);
			EndSleeping();
			flag = true;
		}
		if (IsTransferProtected())
		{
			BaseVehicle vehicleParent = GetVehicleParent();
			if (vehicleParent == null || vehicleParent.ShouldDisableTransferProtectionOnLoad(this))
			{
				DisableTransferProtection();
				flag = true;
			}
		}
		if (flag)
		{
			SendNetworkUpdateImmediate();
		}
		ClientRPC(RpcTarget.Player("FinishLoading", this));
		Invoke(DelayedTeamUpdate, 1f);
		LoadMissions(State.missions);
		MissionDirty();
		double num = State.unHostileTimestamp - TimeEx.currentTimestamp;
		if (num > 0.0)
		{
			ClientRPC(RpcTarget.Player("SetHostileLength", this), (float)num);
		}
		if (IsTransferProtected() && base.TransferProtectionRemaining > 0f)
		{
			ClientRPC(RpcTarget.Player("SetTransferProtectionDuration", this), base.TransferProtectionRemaining);
		}
		if (modifiers != null)
		{
			modifiers.ResetTicking();
		}
		if (net != null)
		{
			EACServer.OnFinishLoading(net.connection);
		}
		Debug.Log($"{this} has spawned");
		if ((Demo.recordlistmode == 0) ? Demo.recordlist.Contains(UserIDString) : (!Demo.recordlist.Contains(UserIDString)))
		{
			StartDemoRecording();
		}
		SendClientPetLink();
		ClientRPC(RpcTarget.Player("ForceViewAnglesTo", this), base.transform.forward);
		HandleTutorialOnGameEnter();
	}

	private void HandleTutorialOnGameEnter()
	{
		if (TutorialIsland.ShouldPlayerResumeTutorial(this) && TutorialIsland.RestoreOrCreateIslandForPlayer(this, triggerAnalytics: false) == null)
		{
			ClearTutorial();
			Hurt(999999f);
			ClearTutorial_PostDeath();
		}
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	private void ClientKeepConnectionAlive(RPCMessage msg)
	{
		lastTickTime = UnityEngine.Time.time;
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	private void ClientLoadingComplete(RPCMessage msg)
	{
	}

	public void PlayerInit(Network.Connection c)
	{
		using (TimeWarning.New("PlayerInit", 10))
		{
			CancelInvoke(base.KillMessage);
			SetPlayerFlag(PlayerFlags.Connected, b: true);
			activePlayerList.Add(this);
			bots.Remove(this);
			userID = c.userid;
			UserIDString = userID.Get().ToString();
			displayName = c.username;
			c.player = this;
			secondsConnected = 0;
			currentTeam = RelationshipManager.ServerInstance.FindPlayersTeam(userID)?.teamID ?? 0;
			SingletonComponent<ServerMgr>.Instance.persistance.SetPlayerName(userID, displayName);
			tickInterpolator.Reset(base.transform.position);
			tickHistory.Reset(base.transform.position);
			eyeHistory.Clear();
			lastTickTime = 0f;
			lastInputTime = 0f;
			SetPlayerFlag(PlayerFlags.ReceivingSnapshot, b: true);
			stats.Init();
			InvokeRandomized(StatSave, UnityEngine.Random.Range(5f, 10f), 30f, UnityEngine.Random.Range(0f, 6f));
			previousLifeStory = SingletonComponent<ServerMgr>.Instance.persistance.GetLastLifeStory(userID);
			SetPlayerFlag(PlayerFlags.IsAdmin, c.authLevel != 0);
			SetPlayerFlag(PlayerFlags.IsDeveloper, DeveloperList.IsDeveloper(this));
			if (IsDead() && net.SwitchGroup(BaseNetworkable.LimboNetworkGroup))
			{
				SendNetworkGroupChange();
			}
			net.OnConnected(c);
			net.StartSubscriber();
			SendAsSnapshot(net.connection);
			GlobalNetworkHandler.server.StartSendingSnapshot(this);
			ClientRPC(RpcTarget.Player("StartLoading", this));
			if ((bool)BaseGameMode.GetActiveGameMode(serverside: true))
			{
				BaseGameMode.GetActiveGameMode(serverside: true).OnPlayerConnected(this);
			}
			if (net != null)
			{
				EACServer.OnStartLoading(net.connection);
			}
			if (IsAdmin)
			{
				if (ConVar.AntiHack.noclip_protection <= 0)
				{
					ChatMessage("antihack.noclip_protection is disabled!");
				}
				if (ConVar.AntiHack.speedhack_protection <= 0)
				{
					ChatMessage("antihack.speedhack_protection is disabled!");
				}
				if (ConVar.AntiHack.flyhack_protection <= 0)
				{
					ChatMessage("antihack.flyhack_protection is disabled!");
				}
				if (ConVar.AntiHack.projectile_protection <= 0)
				{
					ChatMessage("antihack.projectile_protection is disabled!");
				}
				if (ConVar.AntiHack.melee_protection <= 0)
				{
					ChatMessage("antihack.melee_protection is disabled!");
				}
				if (ConVar.AntiHack.eye_protection <= 0)
				{
					ChatMessage("antihack.eye_protection is disabled!");
				}
			}
			inventory.crafting.SendToOwner();
			if (TerrainMeta.Path != null && TerrainMeta.Path.OceanPatrolFar != null)
			{
				SendCargoPatrolPath();
			}
		}
	}

	public void StatSave()
	{
		if (stats != null)
		{
			stats.Save();
		}
	}

	public void SendDeathInformation()
	{
		ClientRPC(RpcTarget.Player("OnDied", this));
	}

	public void SendRespawnOptions()
	{
		if (NexusServer.Started && ZoneController.Instance.CanRespawnAcrossZones(this))
		{
			CollectExternalAndSend();
			return;
		}
		List<RespawnInformation.SpawnOptions> spawnOptions2 = Facepunch.Pool.Get<List<RespawnInformation.SpawnOptions>>();
		GetRespawnOptionsForPlayer(spawnOptions2, userID);
		SendToPlayer(spawnOptions2, loading: false);
		async void CollectExternalAndSend()
		{
			List<RespawnInformation.SpawnOptions> list = Facepunch.Pool.Get<List<RespawnInformation.SpawnOptions>>();
			GetRespawnOptionsForPlayer(list, userID);
			List<RespawnInformation.SpawnOptions> allSpawnOptions = Facepunch.Pool.Get<List<RespawnInformation.SpawnOptions>>();
			foreach (RespawnInformation.SpawnOptions item in list)
			{
				allSpawnOptions.Add(item.Copy());
			}
			SendToPlayer(list, loading: true);
			try
			{
				Request request = Facepunch.Pool.Get<Request>();
				request.spawnOptions = Facepunch.Pool.Get<SpawnOptionsRequest>();
				request.spawnOptions.userId = userID;
				using (NexusRpcResult nexusRpcResult = await NexusServer.BroadcastRpc(request, 10f))
				{
					foreach (KeyValuePair<string, Response> response in nexusRpcResult.Responses)
					{
						string key = response.Key;
						SpawnOptionsResponse spawnOptions3 = response.Value.spawnOptions;
						if (spawnOptions3 != null && spawnOptions3.spawnOptions.Count != 0)
						{
							foreach (RespawnInformation.SpawnOptions spawnOption in spawnOptions3.spawnOptions)
							{
								RespawnInformation.SpawnOptions spawnOptions4 = spawnOption.Copy();
								spawnOptions4.nexusZone = key;
								allSpawnOptions.Add(spawnOptions4);
							}
						}
					}
				}
				SendToPlayer(allSpawnOptions, loading: false);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}
		void SendToPlayer(List<RespawnInformation.SpawnOptions> spawnOptions, bool loading)
		{
			using RespawnInformation respawnInformation = Facepunch.Pool.Get<RespawnInformation>();
			respawnInformation.spawnOptions = spawnOptions;
			respawnInformation.loading = loading;
			if (LegacyShelter.max_shelters == LegacyShelter.FpShelterDefault && LegacyShelter.SheltersPerPlayer.ContainsKey(userID) && LegacyShelter.SheltersPerPlayer[userID].Count > 0)
			{
				respawnInformation.shelterPositions = Facepunch.Pool.Get<List<Vector3>>();
				foreach (LegacyShelter item2 in LegacyShelter.SheltersPerPlayer[userID])
				{
					respawnInformation.shelterPositions.Add(item2.transform.position);
				}
			}
			if (IsDead())
			{
				respawnInformation.previousLife = previousLifeStory;
				if (!ConVar.Server.skipDeathScreenFade)
				{
					respawnInformation.fadeIn = previousLifeStory != null && previousLifeStory.timeDied > Epoch.Current - 5;
				}
				else
				{
					respawnInformation.fadeIn = false;
				}
			}
			ClientRPC(RpcTarget.Player("OnRespawnInformation", this), respawnInformation);
		}
	}

	public static void GetRespawnOptionsForPlayer(List<RespawnInformation.SpawnOptions> spawnOptions, ulong userID)
	{
		BasePlayer basePlayer = FindByID(userID);
		SleepingBag[] array = SleepingBag.FindForPlayer(userID, ignoreTimers: true);
		foreach (SleepingBag sleepingBag in array)
		{
			if (!(basePlayer != null) || basePlayer.IsInTutorial == sleepingBag.IsTutorialBag)
			{
				RespawnInformation.SpawnOptions spawnOptions2 = Facepunch.Pool.Get<RespawnInformation.SpawnOptions>();
				spawnOptions2.id = sleepingBag.net.ID;
				spawnOptions2.name = sleepingBag.niceName;
				spawnOptions2.worldPosition = sleepingBag.transform.position;
				spawnOptions2.type = (sleepingBag.isStatic ? RespawnInformation.SpawnOptions.RespawnType.Static : sleepingBag.RespawnType);
				spawnOptions2.unlockSeconds = sleepingBag.GetUnlockSeconds(userID);
				spawnOptions2.respawnState = sleepingBag.GetRespawnState(userID);
				spawnOptions2.mobile = sleepingBag.IsMobile();
				spawnOptions.Add(spawnOptions2);
			}
		}
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(1uL)]
	private void RequestRespawnInformation(RPCMessage msg)
	{
		SendRespawnOptions();
	}

	public void ScheduledDeath()
	{
		PlayerLifeStory.DeathInfo deathInfo = Facepunch.Pool.Get<PlayerLifeStory.DeathInfo>();
		deathInfo.attackerName = "safezone";
		Kill();
		lifeStory.deathInfo = deathInfo;
		lifeStory.timeDied = (uint)Epoch.Current;
		LifeStoryEnd();
	}

	public virtual void StartSleeping()
	{
		if (!IsSleeping())
		{
			if (IsRestrained)
			{
				inventory.SetLockedByRestraint(flag: false);
			}
			if (InSafeZone() && !IsInvoking(ScheduledDeath))
			{
				Invoke(ScheduledDeath, NPCAutoTurret.sleeperhostiledelay);
			}
			BaseMountable baseMountable = GetMounted();
			if (baseMountable != null && !AllowSleeperMounting(baseMountable))
			{
				EnsureDismounted();
			}
			SetPlayerFlag(PlayerFlags.Sleeping, b: true);
			sleepStartTime = UnityEngine.Time.time;
			sleepingPlayerList.TryAdd(this);
			bots.Remove(this);
			CancelInvoke(InventoryUpdate);
			CancelInvoke(TeamUpdate);
			CancelInvoke(UpdateClanLastSeen);
			inventory.loot.Clear();
			inventory.containerMain.OnChanged();
			inventory.containerBelt.OnChanged();
			inventory.containerWear.OnChanged();
			EnablePlayerCollider();
			if (!IsLoadingAfterTransfer())
			{
				RemovePlayerRigidbody();
				TurnOffAllLights();
			}
			SetServerFall(wantsOn: true);
		}
	}

	private void TurnOffAllLights()
	{
		LightToggle(mask: false);
		HeldEntity heldEntity = GetHeldEntity();
		if (heldEntity != null)
		{
			TorchWeapon component = heldEntity.GetComponent<TorchWeapon>();
			if (component != null)
			{
				component.SetIsOn(isOn: false);
			}
		}
	}

	private void OnPhysicsNeighbourChanged()
	{
		if (IsSleeping() || IsIncapacitated())
		{
			Invoke(DelayedServerFall, 0.05f);
		}
	}

	private void DelayedServerFall()
	{
		SetServerFall(wantsOn: true);
	}

	public void SetServerFall(bool wantsOn)
	{
		if (wantsOn && ConVar.Server.playerserverfall)
		{
			if (!IsInvoking(ServerFall))
			{
				SetPlayerFlag(PlayerFlags.ServerFall, b: true);
				lastFallTime = UnityEngine.Time.time - fallTickRate;
				InvokeRandomized(ServerFall, 0f, fallTickRate, fallTickRate * 0.1f);
				fallVelocity = estimatedVelocity.y;
			}
		}
		else
		{
			CancelInvoke(ServerFall);
			SetPlayerFlag(PlayerFlags.ServerFall, b: false);
		}
	}

	public void ServerFall()
	{
		if (IsDead() || HasParent() || (!IsIncapacitated() && !IsSleeping()))
		{
			SetServerFall(wantsOn: false);
			return;
		}
		float num = UnityEngine.Time.time - lastFallTime;
		lastFallTime = UnityEngine.Time.time;
		float radius = GetRadius();
		float num2 = GetHeight(ducked: true) * 0.5f;
		float num3 = 2.5f;
		float num4 = 0.5f;
		fallVelocity += UnityEngine.Physics.gravity.y * num3 * num4 * num;
		float num5 = Mathf.Abs(fallVelocity * num);
		Vector3 origin = base.transform.position + Vector3.up * (radius + num2);
		Vector3 position = base.transform.position;
		Vector3 position2 = base.transform.position;
		if (UnityEngine.Physics.SphereCast(origin, radius, Vector3.down, out var hitInfo, num5 + num2, 1537286401, QueryTriggerInteraction.Ignore))
		{
			SetServerFall(wantsOn: false);
			if (hitInfo.distance > num2)
			{
				position2 += Vector3.down * (hitInfo.distance - num2);
			}
			ApplyFallDamageFromVelocity(fallVelocity);
			UpdateEstimatedVelocity(position2, position2, num);
			fallVelocity = 0f;
		}
		else if (UnityEngine.Physics.Raycast(origin, Vector3.down, out hitInfo, num5 + radius + num2, 1537286401, QueryTriggerInteraction.Ignore))
		{
			SetServerFall(wantsOn: false);
			if (hitInfo.distance > num2 - radius)
			{
				position2 += Vector3.down * (hitInfo.distance - num2 - radius);
			}
			ApplyFallDamageFromVelocity(fallVelocity);
			UpdateEstimatedVelocity(position2, position2, num);
			fallVelocity = 0f;
		}
		else
		{
			position2 += Vector3.down * num5;
			UpdateEstimatedVelocity(position, position2, num);
			if (WaterLevel.Test(position2, waves: true, volumes: true, this) || AntiHack.TestInsideTerrain(position2))
			{
				SetServerFall(wantsOn: false);
			}
		}
		MovePosition(position2);
	}

	public void DelayedRigidbodyDisable()
	{
		RemovePlayerRigidbody();
	}

	public virtual void EndSleeping()
	{
		if (IsSleeping())
		{
			if (IsRestrained)
			{
				inventory.SetLockedByRestraint(flag: true);
			}
			SetPlayerFlag(PlayerFlags.Sleeping, b: false);
			sleepStartTime = -1f;
			sleepingPlayerList.Remove(this);
			if ((ulong)userID < 10000000 && !bots.Contains(this))
			{
				bots.Add(this);
			}
			CancelInvoke(ScheduledDeath);
			InvokeRepeating(InventoryUpdate, 1f, 0.1f * UnityEngine.Random.Range(0.99f, 1.01f));
			if (RelationshipManager.TeamsEnabled())
			{
				InvokeRandomized(TeamUpdate, 1f, 4f, 1f);
			}
			InvokeRandomized(UpdateClanLastSeen, 300f, 300f, 60f);
			EnablePlayerCollider();
			AddPlayerRigidbody();
			SetServerFall(wantsOn: false);
			if (HasParent())
			{
				SetParent(null, worldPositionStays: true);
				RemoveFromTriggers();
				ForceUpdateTriggers();
			}
			inventory.containerMain.OnChanged();
			inventory.containerBelt.OnChanged();
			inventory.containerWear.OnChanged();
			EACServer.LogPlayerSpawn(this);
			if (TotalPingCount > 0)
			{
				SendPingsToClient();
			}
			if (TutorialIsland.ShouldPlayerBeAskedToStartTutorial(this))
			{
				ClientRPC(RpcTarget.Player("PromptToStartTutorial", this));
			}
		}
	}

	public virtual void EndLooting()
	{
		if ((bool)inventory.loot)
		{
			inventory.loot.Clear();
		}
	}

	public virtual void OnDisconnected()
	{
		startTutorialCooldown = 0f;
		stats.Save(forceSteamSave: true);
		EndLooting();
		ClearDesigningAIEntity();
		if (IsAlive() || IsSleeping())
		{
			StartSleeping();
		}
		else
		{
			Invoke(base.KillMessage, 0f);
		}
		activePlayerList.Remove(this);
		SetPlayerFlag(PlayerFlags.Connected, b: false);
		StopDemoRecording();
		if (net != null)
		{
			net.OnDisconnected();
		}
		ResetAntiHack();
		RefreshColliderSize(forced: true);
		if ((bool)BaseGameMode.GetActiveGameMode(serverside: true))
		{
			BaseGameMode.GetActiveGameMode(serverside: true).OnPlayerDisconnected(this);
		}
		BaseMission.PlayerDisconnected(this);
		ClanManager serverInstance = ClanManager.ServerInstance;
		if (clanId != 0L && serverInstance != null)
		{
			serverInstance.ClanMemberConnectionsChanged(clanId);
		}
		UpdateClanLastSeen();
	}

	private void InventoryUpdate()
	{
		if (IsConnected && !IsDead())
		{
			inventory.ServerUpdate(0.1f);
		}
	}

	public void ApplyFallDamageFromVelocity(float velocity)
	{
		if (IsGod())
		{
			return;
		}
		float num = Mathf.InverseLerp(-15f, -100f, velocity);
		if (num != 0f)
		{
			metabolism.bleeding.Add(num * 0.5f);
			float num2 = num * 500f;
			Analytics.Azure.OnFallDamage(this, velocity, num2);
			Hurt(num2, DamageType.Fall);
			if (num2 > 20f && fallDamageEffect.isValid)
			{
				Effect.server.Run(fallDamageEffect.resourcePath, base.transform.position, Vector3.zero);
			}
		}
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	private void OnPlayerLanded(RPCMessage msg)
	{
		float num = msg.read.Float();
		if (!float.IsNaN(num) && !float.IsInfinity(num) && !ConVar.AntiHack.serverside_fall_damage)
		{
			ApplyFallDamageFromVelocity(num);
			fallVelocity = 0f;
		}
	}

	public void SendGlobalSnapshot()
	{
		using (TimeWarning.New("SendGlobalSnapshot", 10))
		{
			EnterVisibility(Network.Net.sv.visibility.Get(0u));
		}
	}

	public void SendFullSnapshot()
	{
		using (TimeWarning.New("SendFullSnapshot"))
		{
			foreach (Group item in net.subscriber.subscribed)
			{
				if (item.ID != 0)
				{
					EnterVisibility(item);
				}
			}
		}
	}

	public override void OnNetworkGroupLeave(Group group)
	{
		base.OnNetworkGroupLeave(group);
		LeaveVisibility(group);
	}

	private void LeaveVisibility(Group group)
	{
		ServerMgr.OnLeaveVisibility(net.connection, group);
		ClearEntityQueue(group);
	}

	public override void OnNetworkGroupEnter(Group group)
	{
		base.OnNetworkGroupEnter(group);
		EnterVisibility(group);
	}

	private void EnterVisibility(Group group)
	{
		ServerMgr.OnEnterVisibility(net.connection, group);
		SendSnapshots(group.networkables);
	}

	public void CheckDeathCondition(HitInfo info = null)
	{
		Assert.IsTrue(base.isServer, "CheckDeathCondition called on client!");
		if (!IsSpectating() && !IsDead() && metabolism.ShouldDie())
		{
			Die(info);
		}
	}

	public virtual BaseCorpse CreateCorpse(PlayerFlags flagsOnDeath, Vector3 posOnDeath, Quaternion rotOnDeath, List<TriggerBase> triggersOnDeath, bool forceServerSide = false)
	{
		using (TimeWarning.New("Create corpse"))
		{
			string strCorpsePrefab = ((!(ConVar.Physics.serversideragdolls || forceServerSide)) ? "assets/prefabs/player/player_corpse.prefab" : "assets/prefabs/player/player_corpse_new.prefab");
			bool flag = false;
			if (ConVar.Global.cinematicGingerbreadCorpses)
			{
				foreach (Item item in inventory.containerWear.itemList)
				{
					if (item != null && item.info.TryGetComponent<ItemCorpseOverride>(out var component))
					{
						strCorpsePrefab = ((GetFloatBasedOnUserID(userID, 4332uL) > 0.5f) ? component.FemaleCorpse.resourcePath : component.MaleCorpse.resourcePath);
						flag = component.BlockWearableCopy;
						break;
					}
				}
			}
			PlayerCorpse playerCorpse = DropCorpse(strCorpsePrefab, posOnDeath, rotOnDeath, flagsOnDeath, modelState) as PlayerCorpse;
			if ((bool)playerCorpse)
			{
				playerCorpse.SetFlag(Flags.Reserved5, HasPlayerFlag(PlayerFlags.DisplaySash));
				if (!flag)
				{
					playerCorpse.TakeFrom(this, inventory.containerMain, inventory.containerWear, inventory.containerBelt);
				}
				playerCorpse.playerName = displayName;
				playerCorpse.streamerName = RandomUsernames.Get(userID);
				playerCorpse.playerSteamID = userID;
				playerCorpse.underwearSkin = GetUnderwearSkin();
				if (!triggersOnDeath.IsNullOrEmpty())
				{
					foreach (TriggerBase item2 in triggersOnDeath)
					{
						if (item2 is TriggerParent triggerParent)
						{
							triggerParent.ForceParentEarly(playerCorpse);
						}
					}
				}
				playerCorpse.Spawn();
				playerCorpse.TakeChildren(this);
				ResourceDispenser component2 = playerCorpse.GetComponent<ResourceDispenser>();
				int num = 2;
				if (lifeStory != null)
				{
					num += Mathf.Clamp(Mathf.FloorToInt(lifeStory.secondsAlive / 180f), 0, 20);
				}
				component2.containedItems.Add(new ItemAmount(ItemManager.FindItemDefinition("fat.animal"), num));
				return playerCorpse;
			}
		}
		return null;
		static float GetFloatBasedOnUserID(ulong steamid, ulong seed)
		{
			UnityEngine.Random.State state = UnityEngine.Random.state;
			UnityEngine.Random.InitState((int)(seed + steamid));
			float result = UnityEngine.Random.Range(0f, 1f);
			UnityEngine.Random.state = state;
			return result;
		}
	}

	public override void OnKilled(HitInfo info)
	{
		PlayerFlags flagsOnDeath = playerFlags;
		Vector3 position = base.transform.position;
		List<TriggerBase> obj = Facepunch.Pool.Get<List<TriggerBase>>();
		if (triggers != null)
		{
			foreach (TriggerBase trigger in triggers)
			{
				if (trigger != null)
				{
					obj.Add(trigger);
				}
			}
		}
		BaseMountable baseMountable = GetMounted();
		Vector3 vector = Vector3.zero;
		Quaternion rotOnDeath;
		if (baseMountable.IsValid())
		{
			rotOnDeath = baseMountable.mountAnchor.rotation;
			vector = baseMountable.GetMountRagdollVelocity(this);
		}
		else
		{
			rotOnDeath = Quaternion.Euler(base.transform.eulerAngles.x, eyes.bodyRotation.eulerAngles.y, base.transform.eulerAngles.z);
		}
		SetPlayerFlag(PlayerFlags.Unused2, b: false);
		SetPlayerFlag(PlayerFlags.Unused1, b: false);
		EnsureDismounted();
		EndSleeping();
		EndLooting();
		stats.Add("deaths", 1, Stats.All);
		if (info != null && info.InitiatorPlayer != null && !info.InitiatorPlayer.IsNpc && !IsNpc)
		{
			RelationshipManager.ServerInstance.SetSeen(info.InitiatorPlayer, this);
			RelationshipManager.ServerInstance.SetSeen(this, info.InitiatorPlayer);
			RelationshipManager.ServerInstance.SetRelationship(this, info.InitiatorPlayer, RelationshipManager.RelationshipType.Enemy);
			HandleClanPlayerKilled(info.InitiatorPlayer);
		}
		if ((bool)BaseGameMode.GetActiveGameMode(serverside: true))
		{
			BasePlayer instigator = info?.InitiatorPlayer;
			BaseGameMode.GetActiveGameMode(serverside: true).OnPlayerDeath(instigator, this, info);
		}
		BaseMission.PlayerKilled(this);
		DisablePlayerCollider();
		RemovePlayerRigidbody();
		List<BasePlayer> obj2 = Facepunch.Pool.Get<List<BasePlayer>>();
		if (IsIncapacitated())
		{
			foreach (BasePlayer activePlayer in activePlayerList)
			{
				if (activePlayer != null && activePlayer.inventory != null && activePlayer.inventory.loot != null && activePlayer.inventory.loot.entitySource == this)
				{
					obj2.Add(activePlayer);
				}
			}
		}
		bool flag = IsWounded();
		StopWounded();
		if (inventory.crafting != null)
		{
			inventory.crafting.CancelAll();
		}
		EACServer.LogPlayerDespawn(this);
		bool flag2 = eyes.HeadRay().direction.y > 0.8f;
		bool flag3 = false;
		if (flag2)
		{
			Vector3 direction = -eyes.MovementForward();
			if (GamePhysics.Trace(new Ray(eyes.position, direction), 0f, out var _, 1f, 2097152))
			{
				flag3 = true;
			}
		}
		BaseCorpse baseCorpse = CreateCorpse(flagsOnDeath, position, rotOnDeath, obj, flag2 && flag3);
		if (baseCorpse != null)
		{
			if (baseCorpse.CorpseIsRagdoll && baseMountable != null)
			{
				BaseVehicle baseVehicle = baseMountable.VehicleParent();
				if (baseVehicle != null && baseVehicle.mountedPlayerRagdolls == BaseVehicle.RagdollMode.FallThrough)
				{
					baseCorpse.gameObject.SetIgnoreCollisions(baseVehicle.gameObject, ignore: true);
				}
			}
			if (info != null)
			{
				Rigidbody component = baseCorpse.GetComponent<Rigidbody>();
				if (component != null)
				{
					float num = (baseCorpse.CorpseIsRagdoll ? 5f : 1f);
					Vector3 vector2 = (info.attackNormal + Vector3.up * 0.5f).normalized * num;
					component.AddForce(vector2 + vector, ForceMode.VelocityChange);
				}
			}
			if (baseCorpse is PlayerCorpse { containers: not null } playerCorpse)
			{
				foreach (BasePlayer item in obj2)
				{
					if (item == null)
					{
						continue;
					}
					item.inventory.loot.StartLootingEntity(playerCorpse);
					ItemContainer[] containers = playerCorpse.containers;
					foreach (ItemContainer itemContainer in containers)
					{
						if (itemContainer != null)
						{
							item.inventory.loot.AddContainer(itemContainer);
						}
					}
					item.inventory.loot.SendImmediate();
				}
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj2);
		inventory.Strip();
		DeathBlow deathBlow;
		if (flag && lastDamage == DamageType.Suicide && cachedNonSuicideHit.IsValid)
		{
			deathBlow = cachedNonSuicideHit;
			DeathBlow.Reset(ref cachedNonSuicideHit);
			lastDamage = info.damageTypes.GetMajorityDamageType();
		}
		else
		{
			DeathBlow.From(info, out deathBlow);
		}
		if (lastDamage == DamageType.Fall)
		{
			stats.Add("death_fall", 1);
		}
		string text = "";
		string text2 = "";
		if (info != null)
		{
			if ((bool)info.Initiator)
			{
				if (info.Initiator == this)
				{
					text = ToString() + " was killed by " + lastDamage.ToString() + " at " + base.transform.position.ToString();
					text2 = "You died: killed by " + lastDamage;
					if (lastDamage == DamageType.Suicide)
					{
						Analytics.Server.Death("suicide", ServerPosition);
						stats.Add("death_suicide", 1, Stats.All);
					}
					else
					{
						Analytics.Server.Death("selfinflicted", ServerPosition);
						stats.Add("death_selfinflicted", 1);
					}
				}
				else
				{
					if (info.Initiator is BasePlayer)
					{
						BasePlayer basePlayer = info.Initiator.ToPlayer();
						text = ToString() + " was killed by " + basePlayer.ToString() + " at " + base.transform.position.ToString();
						text2 = "You died: killed by " + basePlayer.displayName + " (" + basePlayer.userID.Get() + ")";
						basePlayer.stats.Add("kill_player", 1, Stats.All);
						basePlayer.LifeStoryKill(this);
						OnKilledByPlayer(basePlayer);
						if (lastDamage == DamageType.Fun_Water)
						{
							basePlayer.GiveAchievement("SUMMER_LIQUIDATOR");
							LiquidWeapon liquidWeapon = basePlayer.GetHeldEntity() as LiquidWeapon;
							if (liquidWeapon != null && liquidWeapon.RequiresPumping && liquidWeapon.PressureFraction <= liquidWeapon.MinimumPressureFraction)
							{
								basePlayer.GiveAchievement("SUMMER_NO_PRESSURE");
							}
						}
						else if (GameInfo.HasAchievements && lastDamage == DamageType.Explosion && info.WeaponPrefab != null && info.WeaponPrefab.ShortPrefabName.Contains("mlrs") && basePlayer != null)
						{
							basePlayer.stats.Add("mlrs_kills", 1, Stats.All);
							basePlayer.stats.Save(forceSteamSave: true);
						}
						Analytics.Azure.OnPlayerDeath(this, basePlayer);
					}
					else
					{
						text = ToString() + " was killed by " + info.Initiator.ShortPrefabName + " (" + info.Initiator.Categorize() + ") at " + base.transform.position.ToString();
						text2 = "You died: killed by " + info.Initiator.Categorize();
						stats.Add("death_" + info.Initiator.Categorize(), 1);
					}
					if (!IsNpc)
					{
						Analytics.Server.Death(info.Initiator, info.WeaponPrefab, ServerPosition);
					}
				}
			}
			else if (lastDamage == DamageType.Fall)
			{
				text = ToString() + " was killed by fall at " + base.transform.position.ToString();
				text2 = "You died: killed by fall";
				Analytics.Server.Death("fall", ServerPosition);
			}
			else
			{
				text = ToString() + " was killed by " + info.damageTypes.GetMajorityDamageType().ToString() + " at " + base.transform.position.ToString();
				text2 = "You died: " + info.damageTypes.GetMajorityDamageType();
			}
		}
		else
		{
			text = ToString() + " died (" + lastDamage.ToString() + ")";
			text2 = "You died: " + lastDamage;
		}
		using (TimeWarning.New("LogMessage"))
		{
			DebugEx.Log(text);
			ConsoleMessage(text2);
		}
		if (net.connection == null && info?.Initiator != null && info.Initiator != this)
		{
			CompanionServer.Util.SendDeathNotification(this, info.Initiator);
		}
		SendNetworkUpdateImmediate();
		LifeStoryLogDeath(in deathBlow, lastDamage);
		Server_LogDeathMarker(base.transform.position);
		LifeStoryEnd();
		if (net.connection == null)
		{
			Invoke(base.KillMessage, 0f);
		}
		else
		{
			SendRespawnOptions();
			SendDeathInformation();
			stats.Save();
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	public void RespawnAt(Vector3 position, Quaternion rotation, BaseEntity spawnPointEntity = null)
	{
		BaseGameMode activeGameMode = BaseGameMode.GetActiveGameMode(serverside: true);
		if ((bool)activeGameMode && !activeGameMode.CanPlayerRespawn(this))
		{
			return;
		}
		SetPlayerFlag(PlayerFlags.Wounded, b: false);
		SetPlayerFlag(PlayerFlags.Unused2, b: false);
		SetPlayerFlag(PlayerFlags.Unused1, b: false);
		SetPlayerFlag(PlayerFlags.ReceivingSnapshot, b: true);
		SetPlayerFlag(PlayerFlags.DisplaySash, b: false);
		respawnId = Guid.NewGuid().ToString("N");
		ServerPerformance.spawns++;
		SetParent(null, worldPositionStays: true);
		base.transform.SetPositionAndRotation(position, rotation);
		tickInterpolator.Reset(position);
		tickHistory.Reset(position);
		eyeHistory.Clear();
		estimatedVelocity = Vector3.zero;
		estimatedSpeed = 0f;
		estimatedSpeed2D = 0f;
		lastTickTime = 0f;
		StopWounded();
		ResetWoundingVars();
		StopSpectating();
		UpdateNetworkGroup();
		EnablePlayerCollider();
		RemovePlayerRigidbody();
		StartSleeping();
		LifeStoryStart();
		metabolism.Reset();
		if (modifiers != null)
		{
			modifiers.RemoveAll();
		}
		InitializeHealth(StartHealth(), StartMaxHealth());
		bool flag = false;
		if (ConVar.Server.respawnWithLoadout)
		{
			string infoString = GetInfoString("client.respawnloadout", string.Empty);
			if (!string.IsNullOrEmpty(infoString) && Inventory.LoadLoadout(infoString, out var so))
			{
				so.LoadItemsOnTo(this);
				flag = true;
			}
		}
		if (!flag)
		{
			inventory.GiveDefaultItems();
		}
		SendNetworkUpdateImmediate();
		ClientRPC(RpcTarget.Player("StartLoading", this));
		Analytics.Azure.OnPlayerRespawned(this, spawnPointEntity);
		if ((bool)activeGameMode)
		{
			BaseGameMode.GetActiveGameMode(serverside: true).OnPlayerRespawn(this);
		}
		if (IsConnected)
		{
			EACServer.OnStartLoading(net.connection);
		}
		ProcessMissionEvent(BaseMission.MissionEventType.RESPAWN, 0, 0f);
	}

	public void Respawn()
	{
		SpawnPoint spawnPoint = ServerMgr.FindSpawnPoint(this);
		if (ConVar.Server.respawnAtDeathPosition && ServerCurrentDeathNote != null)
		{
			spawnPoint.pos = ServerCurrentDeathNote.worldPosition;
		}
		RespawnAt(spawnPoint.pos, spawnPoint.rot);
	}

	public bool IsImmortalTo(HitInfo info)
	{
		if (IsGod())
		{
			return true;
		}
		if (WoundingCausingImmortality(info))
		{
			return true;
		}
		BaseVehicle mountedVehicle = GetMountedVehicle();
		if (mountedVehicle != null && mountedVehicle.ignoreDamageFromOutside)
		{
			BasePlayer initiatorPlayer = info.InitiatorPlayer;
			if (initiatorPlayer != null && initiatorPlayer.GetMountedVehicle() != mountedVehicle)
			{
				return true;
			}
		}
		if (IsInTutorial)
		{
			_ = info.InitiatorPlayer != this;
			return false;
		}
		return false;
	}

	public float TimeAlive()
	{
		return lifeStory.secondsAlive;
	}

	public override void Hurt(HitInfo info)
	{
		if (IsDead() || IsTransferProtected() || (IsImmortalTo(info) && info.damageTypes.Total() >= 0f))
		{
			return;
		}
		bool wasWounded = IsWounded();
		if (ConVar.Server.pve && !IsNpc && (bool)info.Initiator && info.Initiator is BasePlayer && info.Initiator != this)
		{
			(info.Initiator as BasePlayer).Hurt(info.damageTypes.Total(), DamageType.Generic);
			return;
		}
		if (info.damageTypes.Has(DamageType.Fun_Water))
		{
			bool flag = true;
			Item activeItem = GetActiveItem();
			if (activeItem != null && (activeItem.info.shortname == "gun.water" || activeItem.info.shortname == "pistol.water"))
			{
				float value = metabolism.wetness.value;
				metabolism.wetness.Add(ConVar.Server.funWaterWetnessGain);
				bool flag2 = metabolism.wetness.value >= ConVar.Server.funWaterDamageThreshold;
				flag = !flag2;
				if (info.InitiatorPlayer != null)
				{
					if (flag2 && value < ConVar.Server.funWaterDamageThreshold)
					{
						info.InitiatorPlayer.GiveAchievement("SUMMER_SOAKED");
					}
					if (metabolism.radiation_level.Fraction() > 0.2f && !string.IsNullOrEmpty("SUMMER_RADICAL"))
					{
						info.InitiatorPlayer.GiveAchievement("SUMMER_RADICAL");
					}
				}
			}
			if (flag)
			{
				info.damageTypes.Scale(DamageType.Fun_Water, 0f);
			}
		}
		if (info.damageTypes.Get(DamageType.Drowned) > 5f && drownEffect.isValid)
		{
			Effect.server.Run(drownEffect.resourcePath, this, StringPool.Get("head"), Vector3.zero, Vector3.zero);
		}
		if (modifiers != null)
		{
			if (info.damageTypes.Has(DamageType.Radiation))
			{
				info.damageTypes.Scale(DamageType.Radiation, 1f - Mathf.Clamp01(modifiers.GetValue(Modifier.ModifierType.Radiation_Resistance)));
			}
			if (info.damageTypes.Has(DamageType.RadiationExposure))
			{
				info.damageTypes.Scale(DamageType.RadiationExposure, 1f - Mathf.Clamp01(modifiers.GetValue(Modifier.ModifierType.Radiation_Exposure_Resistance)));
			}
		}
		metabolism.pending_health.Subtract(info.damageTypes.Total() * 10f);
		BasePlayer initiatorPlayer = info.InitiatorPlayer;
		if ((bool)initiatorPlayer && initiatorPlayer != this)
		{
			if (initiatorPlayer.InSafeZone() || InSafeZone())
			{
				initiatorPlayer.MarkHostileFor(300f);
			}
			if (initiatorPlayer.InSafeZone() && !initiatorPlayer.IsNpc)
			{
				info.damageTypes.ScaleAll(0f);
				return;
			}
			if (initiatorPlayer.IsNpc && initiatorPlayer.Family == BaseNpc.AiStatistics.FamilyEnum.Murderer && info.damageTypes.Get(DamageType.Explosion) > 0f)
			{
				info.damageTypes.ScaleAll(Halloween.scarecrow_beancan_vs_player_dmg_modifier);
			}
		}
		base.Hurt(info);
		if ((bool)BaseGameMode.GetActiveGameMode(serverside: true))
		{
			BasePlayer instigator = info?.InitiatorPlayer;
			BaseGameMode.GetActiveGameMode(serverside: true).OnPlayerHurt(instigator, this, info);
		}
		if (IsRestrained && info.damageTypes.GetMajorityDamageType().InterruptsRestraintMinigame())
		{
			Handcuffs handcuffs = GetHeldEntity() as Handcuffs;
			if (handcuffs != null)
			{
				handcuffs.InterruptUnlockMiniGame(wasPushedOrDamaged: true);
			}
		}
		EACServer.LogPlayerTakeDamage(this, info, wasWounded);
		metabolism.SendChangesToClient();
		if (info.PointStart != Vector3.zero && (info.damageTypes.Total() >= 0f || IsGod()))
		{
			int arg = (int)info.damageTypes.GetMajorityDamageType();
			if (info.Weapon != null && info.damageTypes.Has(DamageType.Bullet))
			{
				BaseProjectile component = info.Weapon.GetComponent<BaseProjectile>();
				if (component != null && component.IsSilenced())
				{
					arg = 12;
				}
			}
			ClientRPC(RpcTarget.PlayerAndSpectators("DirectionalDamage", this), info.PointStart, arg, Mathf.CeilToInt(info.damageTypes.Total()));
		}
		DeathBlow.From(info, out cachedNonSuicideHit);
	}

	public override void Heal(float amount)
	{
		if (IsCrawling())
		{
			float num = base.health;
			base.Heal(amount);
			healingWhileCrawling += base.health - num;
		}
		else
		{
			base.Heal(amount);
		}
		ProcessMissionEvent(BaseMission.MissionEventType.HEAL, 0, amount);
	}

	public static BasePlayer FindBot(ulong userId)
	{
		foreach (BasePlayer bot in bots)
		{
			if ((ulong)bot.userID == userId)
			{
				return bot;
			}
		}
		return FindBotClosestMatch(userId.ToString());
	}

	public static BasePlayer FindBotClosestMatch(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		foreach (BasePlayer bot in bots)
		{
			if (bot.displayName.Contains(name))
			{
				return bot;
			}
		}
		return null;
	}

	public static BasePlayer FindByID(ulong userID)
	{
		using (TimeWarning.New("BasePlayer.FindByID"))
		{
			foreach (BasePlayer activePlayer in activePlayerList)
			{
				if ((ulong)activePlayer.userID == userID)
				{
					return activePlayer;
				}
			}
			return null;
		}
	}

	public static bool TryFindByID(ulong userID, out BasePlayer basePlayer)
	{
		basePlayer = FindByID(userID);
		return basePlayer != null;
	}

	public static BasePlayer FindSleeping(ulong userID)
	{
		using (TimeWarning.New("BasePlayer.FindSleeping"))
		{
			foreach (BasePlayer sleepingPlayer in sleepingPlayerList)
			{
				if ((ulong)sleepingPlayer.userID == userID)
				{
					return sleepingPlayer;
				}
			}
			return null;
		}
	}

	public static BasePlayer FindAwakeOrSleepingByID(ulong userID)
	{
		if (userID == 0L)
		{
			return null;
		}
		BasePlayer basePlayer = FindByID(userID);
		if (!(basePlayer != null))
		{
			return FindSleeping(userID);
		}
		return basePlayer;
	}

	public void Command(string strCommand, params object[] arguments)
	{
		if (net.connection != null)
		{
			ConsoleNetwork.SendClientCommand(net.connection, strCommand, arguments);
		}
	}

	public override void OnInvalidPosition()
	{
		if (!IsDead())
		{
			Die();
		}
	}

	private static BasePlayer Find(string strNameOrIDOrIP, IEnumerable<BasePlayer> list)
	{
		BasePlayer basePlayer = list.FirstOrDefault((BasePlayer x) => x.UserIDString == strNameOrIDOrIP);
		if ((bool)basePlayer)
		{
			return basePlayer;
		}
		BasePlayer basePlayer2 = list.FirstOrDefault((BasePlayer x) => x.displayName.StartsWith(strNameOrIDOrIP, StringComparison.CurrentCultureIgnoreCase));
		if ((bool)basePlayer2)
		{
			return basePlayer2;
		}
		BasePlayer basePlayer3 = list.FirstOrDefault((BasePlayer x) => x.net != null && x.net.connection != null && x.net.connection.ipaddress == strNameOrIDOrIP);
		if ((bool)basePlayer3)
		{
			return basePlayer3;
		}
		return null;
	}

	public static BasePlayer Find(string strNameOrIDOrIP)
	{
		return Find(strNameOrIDOrIP, activePlayerList);
	}

	public static BasePlayer FindSleeping(string strNameOrIDOrIP)
	{
		return Find(strNameOrIDOrIP, sleepingPlayerList);
	}

	public static BasePlayer FindAwakeOrSleeping(string strNameOrIDOrIP)
	{
		return Find(strNameOrIDOrIP, allPlayerList);
	}

	public void SendConsoleCommand(string command, params object[] obj)
	{
		ConsoleNetwork.SendClientCommand(net.connection, command, obj);
	}

	public void UpdateRadiation(float fAmount)
	{
		metabolism.radiation_level.Increase(fAmount);
	}

	public override float RadiationExposureFraction()
	{
		float num = Mathf.Clamp(baseProtection.amounts[17], 0f, 1f);
		return 1f - num;
	}

	public override float RadiationProtection()
	{
		return baseProtection.amounts[17] * 100f;
	}

	public override void OnHealthChanged(float oldvalue, float newvalue)
	{
		base.OnHealthChanged(oldvalue, newvalue);
		if (base.isServer)
		{
			if (oldvalue > newvalue)
			{
				LifeStoryHurt(oldvalue - newvalue);
			}
			else
			{
				LifeStoryHeal(newvalue - oldvalue);
			}
			metabolism.isDirty = true;
		}
	}

	public void SV_ClothingChanged()
	{
		UpdateProtectionFromClothing();
		UpdateMoveSpeedFromClothing();
	}

	public bool IsNoob()
	{
		return !HasPlayerFlag(PlayerFlags.DisplaySash);
	}

	public bool HasHostileItem()
	{
		using (TimeWarning.New("BasePlayer.HasHostileItem"))
		{
			foreach (Item item in inventory.containerBelt.itemList)
			{
				if (IsHostileItem(item))
				{
					return true;
				}
			}
			foreach (Item item2 in inventory.containerMain.itemList)
			{
				if (IsHostileItem(item2))
				{
					return true;
				}
			}
			return false;
		}
	}

	public override void GiveItem(Item item, GiveItemReason reason = GiveItemReason.Generic)
	{
		if (reason == GiveItemReason.ResourceHarvested)
		{
			stats.Add($"harvest.{item.info.shortname}", item.amount, (Stats)6);
		}
		if (reason == GiveItemReason.ResourceHarvested || reason == GiveItemReason.Crafted)
		{
			ProcessMissionEvent(BaseMission.MissionEventType.HARVEST, item.info.itemid, item.amount);
		}
		int amount = item.amount;
		if (inventory.GiveItem(item))
		{
			bool infoBool = GetInfoBool("global.streamermode", defaultVal: false);
			string text = item.GetName(infoBool);
			if (!string.IsNullOrEmpty(text))
			{
				Command("note.inv", item.info.itemid, amount, text, (int)reason);
			}
			else
			{
				Command("note.inv", item.info.itemid, amount, string.Empty, (int)reason);
			}
		}
		else
		{
			item.Drop(inventory.containerMain.dropPosition, inventory.containerMain.dropVelocity);
		}
	}

	public override void AttackerInfo(PlayerLifeStory.DeathInfo info)
	{
		info.attackerName = displayName;
		info.attackerSteamID = userID;
	}

	public void InvalidateWorkbenchCache()
	{
		nextCheckTime = 0f;
	}

	public Workbench GetCachedCraftLevelWorkbench()
	{
		return _cachedWorkbench;
	}

	public virtual bool ShouldDropActiveItem()
	{
		return true;
	}

	public override void Die(HitInfo info = null)
	{
		using (TimeWarning.New("Player.Die"))
		{
			if (!IsDead())
			{
				Handcuffs restraintItem = Belt.GetRestraintItem();
				if (restraintItem != null)
				{
					restraintItem.HeldWhenOwnerDied(this);
				}
				if (InGesture)
				{
					Server_CancelGesture();
				}
				if (Belt != null && ShouldDropActiveItem())
				{
					Vector3 vector = new Vector3(UnityEngine.Random.Range(-2f, 2f), 0.2f, UnityEngine.Random.Range(-2f, 2f));
					Belt.DropActive(GetDropPosition(), GetInheritedDropVelocity() + vector.normalized * 3f);
					inventory.DropBackpackOnDeath();
				}
				if (!WoundInsteadOfDying(info))
				{
					SleepingBag.OnPlayerDeath(this);
					base.Die(info);
				}
			}
		}
	}

	public void Kick(string reason, bool reserveSlot = true)
	{
		if (IsConnected)
		{
			net.connection.canReserveSlot = reserveSlot;
			Network.Net.sv.Kick(net.connection, reason);
		}
	}

	public override Vector3 GetDropPosition()
	{
		return eyes.position;
	}

	public override Vector3 GetDropVelocity()
	{
		return GetInheritedDropVelocity() + eyes.BodyForward() * 4f + Vector3Ex.Range(-0.5f, 0.5f);
	}

	public override void ApplyInheritedVelocity(Vector3 velocity)
	{
		BaseEntity baseEntity = GetParentEntity();
		if (baseEntity != null)
		{
			ClientRPC(RpcTarget.Player("SetInheritedVelocity", this), baseEntity.transform.InverseTransformDirection(velocity), baseEntity.net.ID);
		}
		else
		{
			ClientRPC(RpcTarget.Player("SetInheritedVelocity", this), velocity);
		}
		PauseSpeedHackDetection();
	}

	public virtual void SetInfo(string key, string val)
	{
		if (IsConnected)
		{
			net.connection.info.Set(key, val);
		}
	}

	public virtual int GetInfoInt(string key, int defaultVal)
	{
		if (!IsConnected)
		{
			return defaultVal;
		}
		return net.connection.info.GetInt(key, defaultVal);
	}

	public virtual bool GetInfoBool(string key, bool defaultVal)
	{
		if (!IsConnected)
		{
			return defaultVal;
		}
		return net.connection.info.GetBool(key, defaultVal);
	}

	public virtual string GetInfoString(string key, string defaultVal)
	{
		if (!IsConnected)
		{
			return defaultVal;
		}
		return net.connection.info.GetString(key, defaultVal);
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(1uL)]
	public void PerformanceReport(RPCMessage msg)
	{
		string text = msg.read.String();
		string text2 = msg.read.StringRaw();
		ClientPerformanceReport clientPerformanceReport = JsonConvert.DeserializeObject<ClientPerformanceReport>(text2);
		if (clientPerformanceReport.user_id != UserIDString)
		{
			DebugEx.Log($"Client performance report from {this} has incorrect user_id ({UserIDString})");
			return;
		}
		switch (text)
		{
		case "json":
			DebugEx.Log(text2);
			break;
		case "legacy":
		{
			string text3 = (clientPerformanceReport.memory_managed_heap + "MB").PadRight(9);
			string text4 = (clientPerformanceReport.memory_system + "MB").PadRight(9);
			string text5 = (clientPerformanceReport.fps.ToString("0") + "FPS").PadRight(8);
			string text6 = NumberExtensions.FormatSeconds(clientPerformanceReport.fps).PadRight(9);
			string text7 = UserIDString.PadRight(20);
			string text8 = clientPerformanceReport.streamer_mode.ToString().PadRight(7);
			DebugEx.Log(text3 + text4 + text5 + text6 + text8 + text7 + displayName);
			break;
		}
		case "rcon":
			RCon.Broadcast(RCon.LogType.ClientPerf, text2);
			break;
		default:
			Debug.LogError("Unknown PerformanceReport format '" + text + "'");
			break;
		case "none":
			break;
		}
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(1uL)]
	public void PerformanceReport_Frametime(RPCMessage msg)
	{
		int request_id = msg.read.Int32();
		int start_frame = msg.read.Int32();
		int num = msg.read.Int32();
		List<int> list = Facepunch.Pool.Get<List<int>>();
		for (int i = 0; i < num; i++)
		{
			list.Add(ProtocolParser.ReadInt32(msg.read));
		}
		ClientFrametimeReport obj = new ClientFrametimeReport
		{
			frame_times = list,
			request_id = request_id,
			start_frame = start_frame
		};
		DebugEx.Log(JsonConvert.SerializeObject(obj));
		Facepunch.Pool.FreeUnmanaged(ref obj.frame_times);
	}

	public override bool ShouldNetworkTo(BasePlayer player)
	{
		if (player == this)
		{
			return true;
		}
		if (IsSpectating() && player != this)
		{
			return false;
		}
		bool flag = base.ShouldNetworkTo(player);
		if (ServerOcclusion.OcclusionEnabled && flag)
		{
			flag = player.IsBot || player.IsNpc || OcclusionLineOfSight(player);
		}
		return flag;
	}

	private bool OcclusionLineOfSight(BasePlayer player)
	{
		if (OcclusionGetRecentlySeen(player))
		{
			return true;
		}
		if (OcclusionShouldSeeAllPlayers())
		{
			OcclusionPlayerFound(this, player, cache: false);
			return true;
		}
		if (SubGrid.GetDistance(player.SubGrid) < ServerOcclusion.MinOcclusionDistance)
		{
			OcclusionPlayerFound(this, player, cache: false);
			return true;
		}
		if (player.SubGrid.Equals(default(ServerOcclusion.SubGrid)))
		{
			OcclusionPlayerFound(this, player, cache: false);
			return true;
		}
		if (ServerOcclusion.OcclusionCache.TryGetValue((SubGrid, player.SubGrid), out var value))
		{
			if (value)
			{
				OcclusionPlayerFound(this, player);
				return true;
			}
			OcclusionPlayerLost(this, player);
			return false;
		}
		ServerOcclusion.CalculatePathBetweenGrids(SubGrid, player.SubGrid, out var directPath, out var anyPath);
		if (directPath || anyPath)
		{
			OcclusionPlayerFound(this, player);
			return true;
		}
		OcclusionPlayerLost(this, player);
		return false;
	}

	private static void OcclusionPlayerFound(BasePlayer player1, BasePlayer player2, bool cache = true)
	{
		UpdateVisibility(player1, player2);
		if (!player1.OcclusionShouldSeeAllPlayers())
		{
			UpdateVisibility(player2, player1);
		}
		if (cache)
		{
			ServerOcclusion.OcclusionCache.TryAdd((player1.SubGrid, player2.SubGrid), value: true);
			ServerOcclusion.OcclusionCache.TryAdd((player2.SubGrid, player1.SubGrid), value: true);
		}
		static void UpdateVisibility(BasePlayer src, BasePlayer dst)
		{
			float networkTime = src.GetNetworkTime();
			if (src.lastPlayerVisibility.TryAdd(dst, networkTime))
			{
				if (src.net.connection != null)
				{
					dst.SendAsSnapshot(src.net.connection);
					foreach (BaseEntity child in dst.children)
					{
						if (child.ShouldNetworkTo(src))
						{
							child.SendAsSnapshot(src.net.connection);
						}
					}
				}
			}
			else
			{
				src.lastPlayerVisibility[dst] = networkTime;
			}
		}
	}

	private static void OcclusionPlayerLost(BasePlayer player1, BasePlayer player2, bool cache = true)
	{
		UpdateVisibility(player1, player2);
		if (!player2.OcclusionShouldSeeAllPlayers())
		{
			UpdateVisibility(player2, player1);
		}
		if (cache)
		{
			ServerOcclusion.OcclusionCache.TryAdd((player1.SubGrid, player2.SubGrid), value: false);
			ServerOcclusion.OcclusionCache.TryAdd((player2.SubGrid, player1.SubGrid), value: false);
		}
		static void UpdateVisibility(BasePlayer src, BasePlayer dst)
		{
			if (src.lastPlayerVisibility.Remove(dst) && src.net.connection != null)
			{
				dst.DestroyOnClient(src.net.connection);
			}
		}
	}

	private bool OcclusionGetRecentlySeen(BasePlayer player)
	{
		if (lastPlayerVisibility.TryGetValue(player, out var value))
		{
			return GetNetworkTime() - value < ServerOcclusion.OcclusionPollRate;
		}
		return false;
	}

	private bool OcclusionShouldSeeAllPlayers()
	{
		if (IsSpectating())
		{
			return true;
		}
		if (ConVar.AntiHack.server_occlusion_admin_bypass && (IsAdmin || IsDeveloper))
		{
			return true;
		}
		return false;
	}

	internal void GiveAchievement(string name, bool allowTutorial = false)
	{
		if (GameInfo.HasAchievements && (!IsInTutorial || allowTutorial))
		{
			ClientRPC(RpcTarget.Player("RecieveAchievement", this), name);
		}
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(1uL)]
	public void OnPlayerReported(RPCMessage msg)
	{
		string text = msg.read.String();
		string message = msg.read.StringMultiLine();
		string type = msg.read.String();
		string text2 = msg.read.String();
		string text3 = msg.read.String();
		DebugEx.Log($"[PlayerReport] {this} reported {text3}[{text2}] - \"{text}\"");
		RCon.Broadcast(RCon.LogType.Report, new
		{
			PlayerId = UserIDString,
			PlayerName = displayName,
			TargetId = text2,
			TargetName = text3,
			Subject = text,
			Message = message,
			Type = type
		});
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(1uL)]
	public void OnFeedbackReport(RPCMessage msg)
	{
		if (ConVar.Server.printReportsToConsole || !string.IsNullOrEmpty(ConVar.Server.reportsServerEndpoint))
		{
			string text = msg.read.String();
			string text2 = msg.read.StringMultiLine();
			ReportType reportType = (ReportType)Mathf.Clamp(msg.read.Int32(), 0, 5);
			if (ConVar.Server.printReportsToConsole)
			{
				DebugEx.Log($"[FeedbackReport] {this} reported {reportType} - \"{text}\" \"{text2}\"");
				RCon.Broadcast(RCon.LogType.Report, new
				{
					PlayerId = UserIDString,
					PlayerName = displayName,
					Subject = text,
					Message = text2,
					Type = reportType
				});
			}
			if (!string.IsNullOrEmpty(ConVar.Server.reportsServerEndpoint) && reportType != ReportType.BreakingServerRules)
			{
				string image = msg.read.StringMultiLine(60000);
				Facepunch.Models.Feedback feedback = default(Facepunch.Models.Feedback);
				feedback.Type = reportType;
				feedback.Message = text2;
				feedback.Subject = text;
				Facepunch.Models.Feedback feedback2 = feedback;
				feedback2.AppInfo.Image = image;
				Facepunch.Feedback.ServerReport(ConVar.Server.reportsServerEndpoint, userID, ConVar.Server.reportsServerEndpointKey, feedback2);
			}
		}
	}

	public void StartDemoRecording()
	{
		if (net != null && net.connection != null && !net.connection.IsRecording)
		{
			string text = $"demos/{UserIDString}/{DateTime.Now:yyyy-MM-dd-hhmmss}.dem";
			Debug.Log(ToString() + " recording started: " + text);
			net.connection.StartRecording(text, new Demo.Header
			{
				version = Demo.Version,
				level = UnityEngine.Application.loadedLevelName,
				levelSeed = World.Seed,
				levelSize = World.Size,
				checksum = World.Checksum,
				localclient = userID,
				position = eyes.position,
				rotation = eyes.HeadForward(),
				levelUrl = World.Url,
				recordedTime = DateTime.Now.ToBinary()
			});
			SendNetworkUpdateImmediate();
			SendGlobalSnapshot();
			SendFullSnapshot();
			SendEntityUpdate();
			TreeManager.SendSnapshot(this);
			ServerMgr.SendReplicatedVars(net.connection);
			InvokeRepeating(MonitorDemoRecording, 10f, 10f);
		}
	}

	public void StopDemoRecording()
	{
		if (net != null && net.connection != null && net.connection.IsRecording)
		{
			Debug.Log(ToString() + " recording stopped: " + net.connection.RecordFilename);
			net.connection.StopRecording();
			CancelInvoke(MonitorDemoRecording);
		}
	}

	public void MonitorDemoRecording()
	{
		if (net != null && net.connection != null && net.connection.IsRecording && (net.connection.RecordTimeElapsed.TotalSeconds >= (double)Demo.splitseconds || (float)net.connection.RecordFilesize >= Demo.splitmegabytes * 1024f * 1024f))
		{
			StopDemoRecording();
			StartDemoRecording();
		}
	}

	public void InvalidateCachedPeristantPlayer()
	{
		cachedPersistantPlayer = null;
	}

	public bool IsPlayerVisibleToUs(BasePlayer otherPlayer, Vector3 fromOffset, int layerMask)
	{
		if (otherPlayer == null)
		{
			return false;
		}
		Vector3 vector = (isMounted ? eyes.worldMountedPosition : (IsDucked() ? eyes.worldCrouchedPosition : ((!IsCrawling()) ? eyes.worldStandingPosition : eyes.worldCrawlingPosition)));
		vector += fromOffset;
		if (!otherPlayer.IsVisibleSpecificLayers(vector, otherPlayer.CenterPoint(), layerMask) && !otherPlayer.IsVisibleSpecificLayers(vector, otherPlayer.transform.position, layerMask) && !otherPlayer.IsVisibleSpecificLayers(vector, otherPlayer.eyes.position, layerMask))
		{
			return false;
		}
		if (!IsVisibleSpecificLayers(otherPlayer.CenterPoint(), vector, layerMask) && !IsVisibleSpecificLayers(otherPlayer.transform.position, vector, layerMask) && !IsVisibleSpecificLayers(otherPlayer.eyes.position, vector, layerMask))
		{
			return false;
		}
		return true;
	}

	protected virtual void OnKilledByPlayer(BasePlayer p)
	{
	}

	public int GetIdealSlot(BasePlayer player, ItemContainer container, Item item)
	{
		if (container.HasFlag(ItemContainer.Flag.Clothing))
		{
			if (item.IsBackpack())
			{
				return 7;
			}
			if (!item.info.isWearable)
			{
				return -1;
			}
			foreach (Item item2 in container.itemList)
			{
				if (!item2.info.ItemModWearable.CanExistWith(item.info.ItemModWearable) && item2.position == 7 == item.IsBackpack())
				{
					return item2.position;
				}
			}
		}
		return -1;
	}

	public ItemContainerId GetIdealContainer(BasePlayer looter, Item item, ItemMoveModifier modifier)
	{
		bool flag = !modifier.HasFlag(ItemMoveModifier.Alt) && looter.inventory.loot.containers.Count > 0;
		ItemContainer parent = item.parent;
		Item activeItem = looter.GetActiveItem();
		Item backpackWithInventory = inventory.GetBackpackWithInventory();
		bool flag2 = backpackWithInventory != null && backpackWithInventory == item.parentItem;
		bool flag3 = false;
		if (modifier.HasFlag(ItemMoveModifier.BackpackOpen) && looter == this && backpackWithInventory != null)
		{
			if (backpackWithInventory.contents.HasSpaceFor(item))
			{
				if (!flag)
				{
					if (item.parentItem == null || !item.parentItem.IsBackpack() || item.parentItem.parent != inventory.containerWear)
					{
						return backpackWithInventory.contents.uid;
					}
				}
				else if (inventory.loot.FindItem(item.uid) != null && !inventory.containerMain.HasSpaceFor(item))
				{
					return backpackWithInventory.contents.uid;
				}
			}
			else
			{
				flag3 = true;
			}
		}
		if (activeItem != null && !flag3 && !flag && activeItem.contents != null && activeItem.contents != item.parent && activeItem.contents.capacity > 0 && activeItem.contents.CanAcceptItem(item, -1) == ItemContainer.CanAcceptResult.CanAccept)
		{
			return activeItem.contents.uid;
		}
		if (item.info.isWearable && item.info.ItemModWearable.equipOnRightClick && item.parent != inventory.containerWear && !flag && !flag2)
		{
			if (flag3)
			{
				return ItemContainerId.Invalid;
			}
			if (backpackWithInventory == null || item.parent != backpackWithInventory.contents)
			{
				return inventory.containerWear.uid;
			}
		}
		if (parent == inventory.containerMain)
		{
			if (flag)
			{
				return default(ItemContainerId);
			}
			return inventory.containerBelt.uid;
		}
		if (parent == inventory.containerWear)
		{
			return inventory.containerMain.uid;
		}
		if (parent == inventory.containerBelt)
		{
			return inventory.containerMain.uid;
		}
		return default(ItemContainerId);
	}

	private BaseVehicle GetVehicleParent()
	{
		BaseVehicle mountedVehicle = GetMountedVehicle();
		if (mountedVehicle != null)
		{
			return mountedVehicle;
		}
		BaseEntity baseEntity = GetParentEntity();
		if (baseEntity != null && baseEntity is BaseVehicle result)
		{
			return result;
		}
		return null;
	}

	private void RemoveLoadingPlayerFlag()
	{
		if (IsLoadingAfterTransfer())
		{
			SetPlayerFlag(PlayerFlags.LoadingAfterTransfer, b: false);
			if (IsSleeping())
			{
				SetPlayerFlag(PlayerFlags.Sleeping, b: false);
				StartSleeping();
			}
		}
	}

	public bool InNoRespawnZone()
	{
		bool flag = false;
		Vector3 position = base.transform.position;
		if (triggers != null)
		{
			for (int i = 0; i < triggers.Count; i++)
			{
				TriggerNoRespawnZone triggerNoRespawnZone = triggers[i] as TriggerNoRespawnZone;
				if (!(triggerNoRespawnZone == null))
				{
					flag = triggerNoRespawnZone.InNoRespawnZone(position, checkRadius: false);
					if (flag)
					{
						break;
					}
				}
			}
		}
		return flag;
	}

	private void SendCargoPatrolPath()
	{
		if (!BaseBoat.generate_paths)
		{
			return;
		}
		if (cachedOceanPaths == null)
		{
			cachedOceanPaths = Facepunch.Pool.Get<OceanPaths>();
			cachedOceanPaths.cargoPatrolPath = TerrainMeta.Path.OceanPatrolFar;
			cachedOceanPaths.harborApproaches = new List<VectorList>();
			for (int i = 0; i < CargoShip.TotalAvailableHarborDockingPaths; i++)
			{
				VectorList vectorList = new VectorList();
				vectorList.vectorPoints = CargoShip.GetCargoApproachPath(i);
				cachedOceanPaths.harborApproaches.Add(vectorList);
			}
		}
		ClientRPCPlayer(null, this, "ReceiveCargoPatrolPath", cachedOceanPaths);
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(5uL)]
	[RPC_Server.MaxDistance(3f)]
	[RPC_Server.IsVisible(3f)]
	private void RPC_ReqDoRestrainedPush(RPCMessage rpc)
	{
		if (IsSleeping() || IsDead() || !IsRestrained)
		{
			return;
		}
		BasePlayer player = rpc.player;
		if (player == null || player == this)
		{
			return;
		}
		Handcuffs handcuffs = GetHeldEntity() as Handcuffs;
		if (handcuffs != null)
		{
			handcuffs.InterruptUnlockMiniGame(wasPushedOrDamaged: true);
			handcuffs.RepairOnPush();
		}
		if (isMounted)
		{
			BaseMountable baseMountable = GetMounted();
			if (baseMountable != null)
			{
				baseMountable.DismountPlayer(this);
				return;
			}
		}
		Vector3 arg = player.eyes.BodyForward() * 10f;
		arg.y = 0f;
		arg += Vector3.up * 3f;
		ClientRPC(RpcTarget.Player("RPC_DoRestrainedPush", this), arg);
		Hurt(Handcuffs.restrainedPushDamage, DamageType.Generic, player, useProtection: false);
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(5uL)]
	[RPC_Server.MaxDistance(3f)]
	[RPC_Server.IsVisible(3f)]
	private void RPC_ReqRemoveCuffs(RPCMessage rpc)
	{
		if (IsDead() || !IsRestrained)
		{
			return;
		}
		BasePlayer player = rpc.player;
		if (!(player == null))
		{
			Handcuffs handcuffs = GetHeldEntity() as Handcuffs;
			if (handcuffs != null)
			{
				handcuffs.UnlockAndReturnToPlayer(player);
			}
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(5uL)]
	[RPC_Server.MaxDistance(3f)]
	[RPC_Server.IsVisible(3f)]
	private void RPC_ReqRemoveHood(RPCMessage rpc)
	{
		BasePlayer player = rpc.player;
		if (!(player == null))
		{
			RemoveAndReturnPrisonerHood(player);
		}
	}

	private void RemoveAndReturnPrisonerHood(BasePlayer returnToPlayer)
	{
		if (!(returnToPlayer == null) && !IsDead() && IsRestrained)
		{
			Item equippedPrisonerHoodItem = inventory.GetEquippedPrisonerHoodItem();
			if (equippedPrisonerHoodItem != null)
			{
				bool locked = inventory.containerWear.IsLocked();
				inventory.containerWear.SetLocked(isLocked: false);
				returnToPlayer.GiveItem(equippedPrisonerHoodItem);
				inventory.containerWear.SetLocked(locked);
			}
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(5uL)]
	[RPC_Server.MaxDistance(3f)]
	[RPC_Server.IsVisible(3f)]
	private void RPC_ReqEquipHood(RPCMessage rpc)
	{
		BasePlayer player = rpc.player;
		if (!(player == null))
		{
			EquipPrisonerHood(player);
		}
	}

	private void EquipPrisonerHood(BasePlayer placingPlayer)
	{
		if (placingPlayer == null || IsDead() || !IsRestrained || inventory == null || inventory.GetEquippedPrisonerHoodItem() != null)
		{
			return;
		}
		Item usableHoodItem = placingPlayer.inventory.GetUsableHoodItem();
		if (usableHoodItem == null)
		{
			return;
		}
		inventory.SetLockedByRestraint(flag: false);
		if (!usableHoodItem.MoveToContainer(inventory.containerBelt))
		{
			Item slot = inventory.containerBelt.GetSlot(0);
			if (slot != null && slot == Belt.GetRestraintItem()?.GetItem())
			{
				slot = inventory.containerBelt.GetSlot(1);
			}
			if (slot != null)
			{
				if (!slot.MoveToContainer(inventory.containerMain))
				{
					slot.DropAndTossUpwards(base.transform.position);
				}
				usableHoodItem.MoveToContainer(inventory.containerBelt);
			}
		}
		inventory.SetLockedByRestraint(flag: true);
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(5uL)]
	[RPC_Server.MaxDistance(3f)]
	[RPC_Server.IsVisible(3f)]
	private void RPC_ReqForceMountNearest(RPCMessage rpc)
	{
		BasePlayer player = rpc.player;
		if (!(player == null))
		{
			ForceRestrainedMountNearest(player);
		}
	}

	private void ForceRestrainedMountNearest(BasePlayer forcingPlayer)
	{
		if (forcingPlayer == null || isMounted || !IsRestrained || IsDead() || IsSleeping() || IsWounded())
		{
			return;
		}
		List<BaseMountable> obj = Facepunch.Pool.Get<List<BaseMountable>>();
		Vis.Entities(base.transform.position, 2f, obj);
		obj.Sort((BaseMountable a, BaseMountable b) => (base.transform.position - a.transform.position).sqrMagnitude.CompareTo((base.transform.position - b.transform.position).sqrMagnitude));
		foreach (BaseMountable item in obj)
		{
			if (item.isClient || !item.AllowForceMountWhenRestrained || item.VehicleParent() != null || !item.DirectlyMountable() || item.Distance(eyes.position) > 3f || !GamePhysics.LineOfSight(eyes.center, eyes.position, 1218519041) || (!item.IsVisible(eyes.HeadRay(), 1218519041, 3f) && !item.IsVisible(eyes.position, 3f)))
			{
				continue;
			}
			bool flag = false;
			ModularCar modularCar = item as ModularCar;
			if (modularCar != null && modularCar.CarLock.HasALock)
			{
				flag = !modularCar.CarLock.HasLockPermission(this);
				if (modularCar.CarLock.HasLockPermission(forcingPlayer))
				{
					modularCar.CarLock.TryAddPlayer(userID);
				}
			}
			item.AttemptMount(this);
			if (modularCar != null && modularCar.CarLock.HasALock && flag)
			{
				modularCar.CarLock.TryRemovePlayer(userID);
			}
			if (isMounted)
			{
				break;
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(5uL)]
	[RPC_Server.MaxDistance(3f)]
	[RPC_Server.IsVisible(3f)]
	private void RPC_ReqForceSwapSeat(RPCMessage rpc)
	{
		if (!isMounted || !IsRestrained || IsDead() || IsSleeping() || IsWounded() || rpc.player == null)
		{
			return;
		}
		BasePlayer player = rpc.player;
		BaseMountable baseMountable = GetMounted();
		if (baseMountable == null)
		{
			return;
		}
		BaseVehicle baseVehicle = baseMountable.GetComponent<BaseVehicle>();
		if (baseVehicle == null)
		{
			baseVehicle = baseMountable.VehicleParent();
		}
		if (baseVehicle == null)
		{
			return;
		}
		bool flag = false;
		ModularCar modularCar = baseVehicle as ModularCar;
		if (modularCar != null && modularCar.CarLock.HasALock)
		{
			flag = !modularCar.CarLock.HasLockPermission(this);
			if (modularCar.CarLock.HasLockPermission(player))
			{
				modularCar.CarLock.TryAddPlayer(userID);
			}
		}
		baseVehicle.SwapSeats(this, 0, forcingRestrainedPlayer: true);
		if (modularCar != null && modularCar.CarLock.HasALock && flag)
		{
			modularCar.CarLock.TryRemovePlayer(userID);
		}
	}

	public bool CanMoveFrom(BasePlayer player, Item item)
	{
		if (IsRestrainedOrSurrendering)
		{
			ItemContainer itemContainer = item?.parent;
			if (itemContainer == null)
			{
				return true;
			}
			if (itemContainer.IsLocked())
			{
				return false;
			}
			if (itemContainer == inventory.containerBelt && item.IsOn() && item.info.GetComponent<ItemModRestraint>() != null)
			{
				return false;
			}
		}
		return true;
	}

	internal void LifeStoryStart()
	{
		if (lifeStory != null)
		{
			lifeStory = null;
		}
		lifeStory = new PlayerLifeStory
		{
			ShouldPool = false
		};
		lifeStory.timeBorn = (uint)Epoch.Current;
		hasSentPresenceState = false;
	}

	internal void LifeStoryEnd()
	{
		SingletonComponent<ServerMgr>.Instance.persistance.AddLifeStory(userID, lifeStory);
		previousLifeStory = lifeStory;
		lifeStory = null;
	}

	internal void LifeStoryUpdate(float deltaTime, float moveSpeed)
	{
		if (lifeStory != null)
		{
			lifeStory.secondsAlive += deltaTime;
			nextTimeCategoryUpdate -= deltaTime * ((moveSpeed > 0.1f) ? 1f : 0.25f);
			if (nextTimeCategoryUpdate <= 0f && !waitingForLifeStoryUpdate)
			{
				nextTimeCategoryUpdate = 7f + 7f * UnityEngine.Random.Range(0.2f, 1f);
				waitingForLifeStoryUpdate = true;
				lifeStoryQueue.Add(this);
			}
			if (LifeStoryInWilderness)
			{
				lifeStory.secondsWilderness += deltaTime;
			}
			if (LifeStoryInMonument)
			{
				lifeStory.secondsInMonument += deltaTime;
			}
			if (LifeStoryInBase)
			{
				lifeStory.secondsInBase += deltaTime;
			}
			if (LifeStoryFlying)
			{
				lifeStory.secondsFlying += deltaTime;
			}
			if (LifeStoryBoating)
			{
				lifeStory.secondsBoating += deltaTime;
			}
			if (LifeStorySwimming)
			{
				lifeStory.secondsSwimming += deltaTime;
			}
			if (LifeStoryDriving)
			{
				lifeStory.secondsDriving += deltaTime;
			}
			if (IsSleeping())
			{
				lifeStory.secondsSleeping += deltaTime;
			}
			else if (IsRunning())
			{
				lifeStory.metersRun += moveSpeed * deltaTime;
			}
			else
			{
				lifeStory.metersWalked += moveSpeed * deltaTime;
			}
		}
	}

	public void UpdateTimeCategory()
	{
		using (TimeWarning.New("UpdateTimeCategory"))
		{
			waitingForLifeStoryUpdate = false;
			int num = currentTimeCategory;
			currentTimeCategory = 1;
			if (IsBuildingAuthed())
			{
				currentTimeCategory = 4;
			}
			Vector3 position = base.transform.position;
			if (TerrainMeta.TopologyMap != null && ((uint)TerrainMeta.TopologyMap.GetTopology(position) & 0x400u) != 0)
			{
				foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
				{
					if (monument.shouldDisplayOnMap && monument.IsInBounds(position))
					{
						currentTimeCategory = 2;
						break;
					}
				}
			}
			if (IsSwimming())
			{
				currentTimeCategory |= 32;
			}
			if (isMounted)
			{
				BaseMountable baseMountable = GetMounted();
				if (baseMountable.mountTimeStatType == BaseMountable.MountStatType.Boating)
				{
					currentTimeCategory |= 16;
				}
				else if (baseMountable.mountTimeStatType == BaseMountable.MountStatType.Flying)
				{
					currentTimeCategory |= 8;
				}
				else if (baseMountable.mountTimeStatType == BaseMountable.MountStatType.Driving)
				{
					currentTimeCategory |= 64;
				}
			}
			else if (HasParent() && GetParentEntity() is BaseMountable baseMountable2)
			{
				if (baseMountable2.mountTimeStatType == BaseMountable.MountStatType.Boating)
				{
					currentTimeCategory |= 16;
				}
				else if (baseMountable2.mountTimeStatType == BaseMountable.MountStatType.Flying)
				{
					currentTimeCategory |= 8;
				}
				else if (baseMountable2.mountTimeStatType == BaseMountable.MountStatType.Driving)
				{
					currentTimeCategory |= 64;
				}
			}
			if (num != currentTimeCategory || !hasSentPresenceState)
			{
				LifeStoryInWilderness = (1 & currentTimeCategory) != 0;
				LifeStoryInMonument = (2 & currentTimeCategory) != 0;
				LifeStoryInBase = (4 & currentTimeCategory) != 0;
				LifeStoryFlying = (8 & currentTimeCategory) != 0;
				LifeStoryBoating = (0x10 & currentTimeCategory) != 0;
				LifeStorySwimming = (0x20 & currentTimeCategory) != 0;
				LifeStoryDriving = (0x40 & currentTimeCategory) != 0;
				ClientRPC(RpcTarget.Player("UpdateRichPresenceState", this), currentTimeCategory);
				hasSentPresenceState = true;
			}
		}
	}

	public void LifeStoryShotFired(BaseEntity withWeapon)
	{
		if (lifeStory == null)
		{
			return;
		}
		if (lifeStory.weaponStats == null)
		{
			lifeStory.weaponStats = Facepunch.Pool.Get<List<PlayerLifeStory.WeaponStats>>();
		}
		foreach (PlayerLifeStory.WeaponStats weaponStat in lifeStory.weaponStats)
		{
			if (weaponStat.weaponName == withWeapon.ShortPrefabName)
			{
				weaponStat.shotsFired++;
				return;
			}
		}
		PlayerLifeStory.WeaponStats weaponStats = Facepunch.Pool.Get<PlayerLifeStory.WeaponStats>();
		weaponStats.weaponName = withWeapon.ShortPrefabName;
		weaponStats.shotsFired++;
		lifeStory.weaponStats.Add(weaponStats);
	}

	public void LifeStoryShotHit(BaseEntity withWeapon)
	{
		if (lifeStory == null || withWeapon == null)
		{
			return;
		}
		if (lifeStory.weaponStats == null)
		{
			lifeStory.weaponStats = Facepunch.Pool.Get<List<PlayerLifeStory.WeaponStats>>();
		}
		foreach (PlayerLifeStory.WeaponStats weaponStat in lifeStory.weaponStats)
		{
			if (weaponStat.weaponName == withWeapon.ShortPrefabName)
			{
				weaponStat.shotsHit++;
				return;
			}
		}
		PlayerLifeStory.WeaponStats weaponStats = Facepunch.Pool.Get<PlayerLifeStory.WeaponStats>();
		weaponStats.weaponName = withWeapon.ShortPrefabName;
		weaponStats.shotsHit++;
		lifeStory.weaponStats.Add(weaponStats);
	}

	public void LifeStoryKill(BaseCombatEntity killed)
	{
		if (lifeStory != null)
		{
			if (killed is ScientistNPC)
			{
				lifeStory.killedScientists++;
			}
			else if (killed is BasePlayer)
			{
				lifeStory.killedPlayers++;
			}
			else if (killed is BaseAnimalNPC)
			{
				lifeStory.killedAnimals++;
			}
		}
	}

	public void LifeStoryGenericStat(string key, int value)
	{
		if (lifeStory == null)
		{
			return;
		}
		if (lifeStory.genericStats == null)
		{
			lifeStory.genericStats = Facepunch.Pool.Get<List<PlayerLifeStory.GenericStat>>();
		}
		foreach (PlayerLifeStory.GenericStat genericStat2 in lifeStory.genericStats)
		{
			if (genericStat2.key == key)
			{
				genericStat2.value += value;
				return;
			}
		}
		PlayerLifeStory.GenericStat genericStat = Facepunch.Pool.Get<PlayerLifeStory.GenericStat>();
		genericStat.key = key;
		genericStat.value = value;
		lifeStory.genericStats.Add(genericStat);
	}

	public void LifeStoryHurt(float amount)
	{
		if (lifeStory != null)
		{
			lifeStory.totalDamageTaken += amount;
		}
	}

	public void LifeStoryHeal(float amount)
	{
		if (lifeStory != null)
		{
			lifeStory.totalHealing += amount;
		}
	}

	public void SetOverrideDeathBlow(PlayerLifeStory.DeathInfo info)
	{
		cachedOverrideDeathInfo = info;
	}

	internal void LifeStoryLogDeath(in DeathBlow deathBlow, DamageType lastDamage)
	{
		if (lifeStory == null)
		{
			return;
		}
		lifeStory.timeDied = (uint)Epoch.Current;
		PlayerLifeStory.DeathInfo deathInfo = cachedOverrideDeathInfo ?? Facepunch.Pool.Get<PlayerLifeStory.DeathInfo>();
		deathInfo.lastDamageType = (int)lastDamage;
		cachedOverrideDeathInfo = null;
		if (deathBlow.IsValid)
		{
			if (deathBlow.Initiator != null)
			{
				deathBlow.Initiator.AttackerInfo(deathInfo);
				deathInfo.attackerDistance = Distance(deathBlow.Initiator);
			}
			if (deathBlow.WeaponPrefab != null)
			{
				deathInfo.inflictorName = deathBlow.WeaponPrefab.ShortPrefabName;
			}
			if (deathBlow.HitBone != 0)
			{
				deathInfo.hitBone = StringPool.Get(deathBlow.HitBone);
			}
			else
			{
				deathInfo.hitBone = "";
			}
		}
		else if (base.SecondsSinceAttacked <= 60f && lastAttacker != null)
		{
			lastAttacker.AttackerInfo(deathInfo);
		}
		lifeStory.deathInfo = deathInfo;
	}

	public void SetSpectateTeamInfo(bool state)
	{
		IsSpectatingTeamInfo = state;
	}

	private void Tick_Spectator()
	{
		int num = 0;
		if (serverInput.WasJustPressed(BUTTON.JUMP))
		{
			num++;
		}
		if (serverInput.WasJustPressed(BUTTON.DUCK))
		{
			num--;
		}
		if (num != 0)
		{
			SpectateOffset += num;
			using (TimeWarning.New("UpdateSpectateTarget"))
			{
				UpdateSpectateTarget(spectateFilter);
			}
		}
		if (!((float)lastSpectateTeamInfoUpdate > 0.5f) || !IsSpectatingTeamInfo)
		{
			return;
		}
		lastSpectateTeamInfoUpdate = 0f;
		SpectateTeamInfo spectateTeamInfo = Facepunch.Pool.Get<SpectateTeamInfo>();
		spectateTeamInfo.teams = Facepunch.Pool.Get<List<SpectateTeam>>();
		spectateTeamInfo.teams.Clear();
		foreach (KeyValuePair<ulong, RelationshipManager.PlayerTeam> team in RelationshipManager.ServerInstance.teams)
		{
			SpectateTeam spectateTeam = Facepunch.Pool.Get<SpectateTeam>();
			spectateTeam.teamId = team.Key;
			spectateTeam.teamMembers = Facepunch.Pool.Get<List<PlayerTeam.TeamMember>>();
			spectateTeam.teamMembers.Clear();
			foreach (ulong member in team.Value.members)
			{
				PlayerTeam.TeamMember teamMember = Facepunch.Pool.Get<PlayerTeam.TeamMember>();
				teamMember.userID = member;
				BasePlayer basePlayer = RelationshipManager.FindByID(member);
				teamMember.displayName = ((basePlayer != null) ? basePlayer.displayName : (SingletonComponent<ServerMgr>.Instance.persistance.GetPlayerName(member) ?? "DEAD"));
				teamMember.healthFraction = ((basePlayer != null && basePlayer.IsAlive()) ? basePlayer.healthFraction : 0f);
				teamMember.position = ((basePlayer != null) ? basePlayer.transform.position : Vector3.zero);
				teamMember.online = basePlayer != null && !basePlayer.IsSleeping();
				teamMember.wounded = basePlayer != null && basePlayer.IsWounded();
				spectateTeam.teamMembers.Add(teamMember);
			}
			spectateTeamInfo.teams.Add(spectateTeam);
		}
		ClientRPC(RpcTarget.Player("ReceiveSpectateTeamInfo", this), spectateTeamInfo);
	}

	public void UpdateSpectateTarget(string strName)
	{
		spectateFilter = strName;
		IEnumerable<BaseEntity> enumerable = null;
		if (spectateFilter.StartsWith("@"))
		{
			string filter = spectateFilter.Substring(1);
			enumerable = (from x in BaseNetworkable.serverEntities
				where x.name.Contains(filter, CompareOptions.IgnoreCase)
				where x != this
				select x).Cast<BaseEntity>();
		}
		else
		{
			IEnumerable<BasePlayer> source = activePlayerList.Where((BasePlayer x) => !x.IsSpectating() && !x.IsDead() && !x.IsSleeping());
			if (strName.Length > 0)
			{
				source = from x in source
					where x.displayName.Contains(spectateFilter, CompareOptions.IgnoreCase) || x.UserIDString.Contains(spectateFilter)
					where x != this
					select x;
			}
			source = source.OrderBy((BasePlayer x) => x.displayName);
			enumerable = source.Cast<BaseEntity>();
		}
		BaseEntity[] array = enumerable.ToArray();
		if (array.Length == 0)
		{
			ChatMessage("No valid spectate targets!");
			return;
		}
		BaseEntity baseEntity = array[SpectateOffset % array.Length];
		if (baseEntity != null)
		{
			SpectatePlayer(baseEntity);
		}
	}

	public void UpdateSpectateTarget(ulong id)
	{
		foreach (BasePlayer activePlayer in activePlayerList)
		{
			if (activePlayer != null && (ulong)activePlayer.userID == id)
			{
				spectateFilter = string.Empty;
				SpectatePlayer(activePlayer);
				break;
			}
		}
	}

	private void SpectatePlayer(BaseEntity target)
	{
		if (target is BasePlayer)
		{
			ChatMessage("Spectating: " + (target as BasePlayer).displayName);
		}
		else
		{
			ChatMessage("Spectating: " + target.ToString());
		}
		using (TimeWarning.New("SendEntitySnapshot"))
		{
			SendEntitySnapshot(target);
		}
		base.gameObject.Identity();
		using (TimeWarning.New("SetParent"))
		{
			SetParent(target);
		}
	}

	public void StartSpectating()
	{
		if (!IsSpectating())
		{
			SetPlayerFlag(PlayerFlags.Spectating, b: true);
			base.gameObject.SetLayerRecursive(10);
			CancelInvoke(InventoryUpdate);
			ChatMessage("Becoming Spectator");
			UpdateSpectateTarget(spectateFilter);
		}
	}

	public void StopSpectating()
	{
		if (IsSpectating())
		{
			SetParent(null);
			SetPlayerFlag(PlayerFlags.Spectating, b: false);
			base.gameObject.SetLayerRecursive(17);
		}
	}

	public void Teleport(BasePlayer player)
	{
		Teleport(player.transform.position);
	}

	public void Teleport(string strName, bool playersOnly)
	{
		BaseEntity[] array = Util.FindTargets(strName, playersOnly);
		if (array != null && array.Length != 0)
		{
			BaseEntity baseEntity = array[UnityEngine.Random.Range(0, array.Length)];
			Teleport(baseEntity.transform.position);
		}
	}

	public void Teleport(Vector3 position)
	{
		MovePosition(position);
		ClientRPC(RpcTarget.Player("ForcePositionTo", this), position);
	}

	public void CopyRotation(BasePlayer player)
	{
		viewAngles = player.viewAngles;
		SendNetworkUpdate_Position();
	}

	protected override void OnChildAdded(BaseEntity child)
	{
		base.OnChildAdded(child);
		if (child is BasePlayer)
		{
			IsBeingSpectated = true;
		}
	}

	protected override void OnChildRemoved(BaseEntity child)
	{
		base.OnChildRemoved(child);
		if (!(child is BasePlayer))
		{
			return;
		}
		IsBeingSpectated = false;
		foreach (BaseEntity child2 in children)
		{
			if (child2 is BasePlayer)
			{
				IsBeingSpectated = true;
			}
		}
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(10uL)]
	private void UpdateSpectatePositionFromDebugCamera(RPCMessage msg)
	{
		if (IsSpectating() && ConVar.Global.updateNetworkPositionWithDebugCameraWhileSpectating)
		{
			Vector3 position = msg.read.Vector3();
			base.transform.position = position;
			SetParent(null);
		}
	}

	[RPC_Server]
	private void NotifyDebugCameraEnded(RPCMessage msg)
	{
		if (IsSpectating() && ConVar.Global.updateNetworkPositionWithDebugCameraWhileSpectating)
		{
			UpdateSpectateTarget(spectateFilter);
		}
	}

	public bool WantsSplash(ItemDefinition splashType, int amount)
	{
		if (IsSleeping())
		{
			return false;
		}
		if (!IsAlive())
		{
			return false;
		}
		if (InSafeZone())
		{
			return false;
		}
		if (splashType == null || splashType.shortname == null)
		{
			return false;
		}
		return splashType == WaterTypes.RadioactiveWaterItemDef;
	}

	public int DoSplash(ItemDefinition splashType, int amount)
	{
		if (splashType == WaterTypes.RadioactiveWaterItemDef)
		{
			float a = (float)amount * Radiation.MaterialToRadsRatio;
			a = Mathf.Max(a, 0.5f);
			ApplyRadiation(a);
		}
		return amount;
	}

	public void AddNeabyStash(StashContainer newStash)
	{
		if (newStash == null)
		{
			return;
		}
		foreach (NearbyStash nearbyStash in nearbyStashes)
		{
			if (nearbyStash.Entity == newStash)
			{
				return;
			}
		}
		if (nearbyStashes.Count == 0)
		{
			InvokeRepeating(CheckStashRevealInvoke, 0f, StashContainer.PlayerDetectionTickRate);
		}
		nearbyStashes.Add(new NearbyStash(newStash));
	}

	public void RemoveNearbyStash(StashContainer stash)
	{
		for (int i = 0; i < nearbyStashes.Count; i++)
		{
			if (!(nearbyStashes[i].Entity != stash))
			{
				nearbyStashes.RemoveAt(i);
				break;
			}
		}
		if (nearbyStashes.Count == 0)
		{
			CancelInvoke(CheckStashRevealInvoke);
		}
	}

	private void CheckStashRevealInvoke()
	{
		for (int i = 0; i < nearbyStashes.Count; i++)
		{
			NearbyStash nearbyStash = nearbyStashes[i];
			if (nearbyStash.Entity == null || nearbyStash.Entity.IsDestroyed)
			{
				nearbyStashes.RemoveAt(i);
			}
			else if (nearbyStash.Entity.IsHidden() && nearbyStash.Entity.PlayerInRange(this))
			{
				nearbyStash.LookingAtTime += StashContainer.PlayerDetectionTickRate;
				if (nearbyStash.LookingAtTime >= nearbyStash.Entity.uncoverTime)
				{
					nearbyStash.Entity.SetHidden(isHidden: false);
					Analytics.Azure.OnStashRevealed(this, nearbyStash.Entity);
				}
			}
			else
			{
				nearbyStash.LookingAtTime = 0f;
			}
		}
	}

	public override float GetThreatLevel()
	{
		EnsureUpdated();
		return cachedThreatLevel;
	}

	public void EnsureUpdated()
	{
		if (UnityEngine.Time.realtimeSinceStartup - lastUpdateTime < 30f)
		{
			return;
		}
		lastUpdateTime = UnityEngine.Time.realtimeSinceStartup;
		cachedThreatLevel = 0f;
		if (IsSleeping())
		{
			return;
		}
		if (inventory.containerWear.itemList.Count > 2)
		{
			cachedThreatLevel += 1f;
		}
		foreach (Item item in inventory.containerBelt.itemList)
		{
			BaseEntity heldEntity = item.GetHeldEntity();
			if ((bool)heldEntity && heldEntity is BaseProjectile && !(heldEntity is BowWeapon))
			{
				cachedThreatLevel += 2f;
				break;
			}
		}
	}

	public override bool IsHostile()
	{
		return State.unHostileTimestamp > TimeEx.currentTimestamp;
	}

	public virtual float GetHostileDuration()
	{
		return Mathf.Clamp((float)(State.unHostileTimestamp - TimeEx.currentTimestamp), 0f, float.PositiveInfinity);
	}

	public override void MarkHostileFor(float duration = 60f)
	{
		double currentTimestamp = TimeEx.currentTimestamp;
		double val = currentTimestamp + (double)duration;
		State.unHostileTimestamp = Math.Max(State.unHostileTimestamp, val);
		DirtyPlayerState();
		double num = Math.Max(State.unHostileTimestamp - currentTimestamp, 0.0);
		ClientRPC(RpcTarget.Player("SetHostileLength", this), (float)num);
	}

	public void MarkWeaponDrawnDuration(float newDuration)
	{
		float num = weaponDrawnDuration;
		weaponDrawnDuration = newDuration;
		if ((float)Mathf.FloorToInt(newDuration) != num)
		{
			ClientRPC(RpcTarget.Player("SetWeaponDrawnDuration", this), weaponDrawnDuration);
		}
	}

	public void AddWeaponDrawnDuration(float duration)
	{
		MarkWeaponDrawnDuration(weaponDrawnDuration + duration);
	}

	public void OnReceivedTick(Stream stream)
	{
		using (TimeWarning.New("OnReceiveTickFromStream"))
		{
			PlayerTick playerTick = null;
			using (TimeWarning.New("PlayerTick.Deserialize"))
			{
				playerTick = PlayerTick.Deserialize(stream, lastReceivedTick, isDelta: true);
			}
			using (TimeWarning.New("RecordPacket"))
			{
				net.connection.RecordPacket(15, playerTick);
			}
			using (TimeWarning.New("PlayerTick.Copy"))
			{
				lastReceivedTick = playerTick.Copy();
			}
			using (TimeWarning.New("OnReceiveTick"))
			{
				OnReceiveTick(playerTick, wasStalled);
			}
			lastTickTime = UnityEngine.Time.time;
			rawTicksPerSecond.Increment();
			rawTickCount = rawTicksPerSecond.Calculate();
			playerTick.Dispose();
		}
	}

	public void OnReceivedVoice(byte[] data)
	{
		NetWrite netWrite = Network.Net.sv.StartWrite();
		netWrite.PacketID(Message.Type.VoiceData);
		netWrite.EntityID(net.ID);
		netWrite.BytesWithSize(data);
		float num = 0f;
		if (HasPlayerFlag(PlayerFlags.VoiceRangeBoost))
		{
			num = Voice.voiceRangeBoostAmount;
		}
		netWrite.Send(new SendInfo(BaseNetworkable.GetConnectionsWithin(base.transform.position, 100f + num, addSecondaryConnections: true))
		{
			priority = Priority.Immediate
		});
		if (activeTelephone != null)
		{
			activeTelephone.OnReceivedVoiceFromUser(data);
		}
		if (SingletonComponent<NpcNoiseManager>.Instance != null)
		{
			SingletonComponent<NpcNoiseManager>.Instance.OnVoiceChat(this);
		}
	}

	public void ResetInputIdleTime()
	{
		lastInputTime = UnityEngine.Time.time;
	}

	private void EACStateUpdate(in CachedState tickState)
	{
		if (!IsReceivingSnapshot)
		{
			EACServer.LogPlayerTick(this, in tickState);
		}
	}

	private void OnReceiveTick(PlayerTick msg, bool wasPlayerStalled)
	{
		if (msg.inputState != null)
		{
			serverInput.Flip(msg.inputState);
		}
		if (serverInput.current.buttons != serverInput.previous.buttons)
		{
			ResetInputIdleTime();
		}
		if (IsReceivingSnapshot)
		{
			return;
		}
		if (IsSpectating())
		{
			using (TimeWarning.New("Tick_Spectator"))
			{
				Tick_Spectator();
				return;
			}
		}
		if (IsDead())
		{
			return;
		}
		if (IsSleeping())
		{
			if (serverInput.WasJustPressed(BUTTON.FIRE_PRIMARY) || serverInput.WasJustPressed(BUTTON.FIRE_SECONDARY) || serverInput.WasJustPressed(BUTTON.JUMP) || serverInput.WasJustPressed(BUTTON.DUCK))
			{
				EndSleeping();
				SendNetworkUpdateImmediate();
			}
			UpdateActiveItem(default(ItemId));
			return;
		}
		if (IsRestrained && restraintItemId.HasValue && restraintItemId.HasValue)
		{
			UpdateActiveItem(restraintItemId.Value);
		}
		else if (!Belt.CanHoldItem())
		{
			UpdateActiveItem(default(ItemId));
		}
		else
		{
			UpdateActiveItem(msg.activeItem);
		}
		UpdateModelStateFromTick(msg);
		if (float.IsNaN(modelState.ducking) || float.IsInfinity(modelState.ducking))
		{
			Kick("Kicked: invalid modelstate");
			return;
		}
		modelState.ducking = Mathf.Clamp01(modelState.ducking);
		if (IsIncapacitated())
		{
			return;
		}
		if (isMounted)
		{
			GetMounted().PlayerServerInput(serverInput, this);
		}
		UpdatePositionFromTick(msg, wasPlayerStalled);
		UpdateRotationFromTick(msg);
		int activeMission = GetActiveMission();
		if (activeMission >= 0 && activeMission < missions.Count)
		{
			BaseMission.MissionInstance missionInstance = missions[activeMission];
			if (missionInstance.status == BaseMission.MissionStatus.Active && missionInstance.NeedsPlayerInput())
			{
				ProcessMissionEvent(BaseMission.MissionEventType.PLAYER_TICK, net.ID, 0f);
			}
		}
		if (!TutorialIsland.EnforceTrespassChecks || IsAdmin || IsNpc || net == null || net.group == null)
		{
			return;
		}
		if (net.group.restricted)
		{
			bool flag = false;
			if (!IsInTutorial)
			{
				flag = true;
			}
			else
			{
				TutorialIsland currentTutorialIsland = GetCurrentTutorialIsland();
				if (currentTutorialIsland == null || currentTutorialIsland.net.group != net.group)
				{
					flag = true;
				}
			}
			if (flag)
			{
				tutorialKickTime += UnityEngine.Time.deltaTime;
				if (tutorialKickTime > 3f)
				{
					Debug.LogWarning($"Killing player {displayName}/{userID.Get()} as they are on a tutorial island that doesn't belong them");
					Hurt(999f);
					tutorialKickTime = 0f;
				}
			}
			else
			{
				tutorialKickTime = 0f;
			}
		}
		else
		{
			if (!IsInTutorial || net.group.restricted)
			{
				return;
			}
			bool flag2 = false;
			TutorialIsland currentTutorialIsland2 = GetCurrentTutorialIsland();
			if (currentTutorialIsland2 == null || currentTutorialIsland2.net.group != net.group)
			{
				flag2 = true;
			}
			if (flag2)
			{
				tutorialKickTime += UnityEngine.Time.deltaTime;
				if (tutorialKickTime > 3f)
				{
					Debug.LogWarning($"Killing player {displayName}/{userID.Get()} as they are no longer on a tutorial island and are marked as being in a tutorial");
					Hurt(999f);
					tutorialKickTime = 0f;
				}
			}
			else
			{
				tutorialKickTime = 0f;
			}
		}
	}

	public void ApplyStallProtection(float time)
	{
		stallProtectionTime = Mathf.Max(time, stallProtectionTime);
	}

	public void UpdateActiveItem(ItemId itemID)
	{
		Assert.IsTrue(base.isServer, "Realm should be server!");
		if (svActiveItemID == itemID)
		{
			return;
		}
		if (equippingBlocked)
		{
			itemID = default(ItemId);
		}
		Item item = inventory.containerBelt.FindItemByUID(itemID);
		if (IsItemHoldRestricted(item))
		{
			itemID = default(ItemId);
		}
		Item activeItem = GetActiveItem();
		svActiveItemID = default(ItemId);
		if (activeItem != null)
		{
			HeldEntity heldEntity = activeItem.GetHeldEntity() as HeldEntity;
			if (heldEntity != null)
			{
				heldEntity.SetHeld(bHeld: false);
			}
		}
		svActiveItemID = itemID;
		SendNetworkUpdate();
		Item activeItem2 = GetActiveItem();
		if (activeItem2 != null)
		{
			HeldEntity heldEntity2 = activeItem2.GetHeldEntity() as HeldEntity;
			if (heldEntity2 != null)
			{
				heldEntity2.SetHeld(bHeld: true);
			}
			NotifyGesturesNewItemEquipped();
		}
		inventory.UpdatedVisibleHolsteredItems();
	}

	internal void UpdateModelStateFromTick(PlayerTick tick)
	{
		if (tick.modelState != null && !ModelState.Equal(modelStateTick, tick.modelState))
		{
			if (modelStateTick != null)
			{
				modelStateTick.ResetToPool();
			}
			modelStateTick = tick.modelState;
			tick.modelState = null;
			tickNeedsFinalizing = true;
		}
	}

	internal void UpdatePositionFromTick(PlayerTick tick, bool wasPlayerStalled)
	{
		if (tick.position.IsNaNOrInfinity() || tick.eyePos.IsNaNOrInfinity())
		{
			Kick("Kicked: Invalid Position");
		}
		else
		{
			if (tick.parentID != parentEntity.uid)
			{
				return;
			}
			tickDistancePausetime = Mathf.Max(0f, tickDistancePausetime - tickDeltaTime);
			if (isMounted || (modelState != null && modelState.mounted) || (modelStateTick != null && modelStateTick.mounted) || (IsWounded() && IsRestrained))
			{
				return;
			}
			if (wasPlayerStalled)
			{
				float num = Vector3.Distance(tick.position, tickInterpolator.EndPoint);
				if (num > 0.01f)
				{
					AntiHack.ResetTimer(this);
				}
				if (num > 0.5f)
				{
					ClientRPC(RpcTarget.Player("ForcePositionToParentOffset", this), tickInterpolator.EndPoint, parentEntity.uid);
				}
				return;
			}
			if (!AntiHack.ShouldIgnore(this))
			{
				float num2 = Vector3.Distance(tick.position, tickInterpolator.EndPoint);
				float tick_max_distance = ConVar.AntiHack.tick_max_distance;
				float f = ((isInAir || RecentlyInAir()) ? ConVar.AntiHack.tick_max_distance_falling : tick_max_distance);
				float f2 = (HasParent() ? ConVar.AntiHack.tick_max_distance_parented : tick_max_distance);
				float f3 = ((tickDistancePausetime > 0f) ? ConVar.AntiHack.tick_distance_forgiveness : tick_max_distance);
				float num3 = Mathx.Max(tick_max_distance, f, f2, f3);
				if (num2 > num3)
				{
					AntiHack.Log(this, AntiHackType.Ticks, "moved too far between ticks: " + num2 + " units. Max dist: " + num3 + " ");
					AntiHack.ResetTimer(this);
					ClientRPC(RpcTarget.Player("ForcePositionToParentOffset", this), tickInterpolator.EndPoint, parentEntity.uid);
					return;
				}
			}
			tickInterpolator.AddPoint(tick.position);
			tickNeedsFinalizing = true;
		}
	}

	internal void UpdateRotationFromTick(PlayerTick tick)
	{
		if (tick.inputState != null)
		{
			if (tick.inputState.aimAngles.IsNaNOrInfinity())
			{
				Kick("Kicked: Invalid Rotation");
				return;
			}
			if (tick.inputState.mouseDelta.IsNaNOrInfinity())
			{
				Kick("Kicked: Invalid Rotation");
				return;
			}
			tickMouseDelta = tick.inputState.mouseDelta;
			tickViewAngles = tick.inputState.aimAngles;
			tickNeedsFinalizing = true;
		}
	}

	public void UpdateEstimatedVelocity(Vector3 lastPos, Vector3 currentPos, float deltaTime)
	{
		estimatedVelocity = (currentPos - lastPos) / deltaTime;
		estimatedSpeed = estimatedVelocity.magnitude;
		estimatedSpeed2D = estimatedVelocity.Magnitude2D();
		if (estimatedSpeed < 0.01f)
		{
			estimatedSpeed = 0f;
		}
		if (estimatedSpeed2D < 0.01f)
		{
			estimatedSpeed2D = 0f;
		}
	}

	private void FinalizeTick(float deltaTime)
	{
		tickDeltaTime += deltaTime;
		if (IsReceivingSnapshot || !tickNeedsFinalizing)
		{
			return;
		}
		tickNeedsFinalizing = false;
		using (TimeWarning.New("ModelState"))
		{
			if (modelStateTick != null)
			{
				if (modelStateTick.inheritedVelocity != Vector3.zero && FindTrigger<TriggerForce>() == null)
				{
					modelStateTick.inheritedVelocity = Vector3.zero;
				}
				if (modelState != null)
				{
					if (ConVar.AntiHack.modelstate && TriggeredAntiHack())
					{
						modelStateTick.ducked = modelState.ducked;
					}
					modelState.ResetToPool();
					modelState = null;
				}
				modelState = modelStateTick;
				modelStateTick = null;
				UpdateModelState();
			}
		}
		CachedState tickState = default(CachedState);
		using (TimeWarning.New("CachingPlayerState"))
		{
			tickState.IsValid = true;
			tickState.WaterFactor = WaterFactor(out tickState.WaterInfo);
			tickState.IsSwimming = IsSwimming(tickState.WaterFactor);
		}
		using (TimeWarning.New("Transform"))
		{
			UpdateEstimatedVelocity(tickInterpolator.StartPoint, tickInterpolator.EndPoint, tickDeltaTime);
			bool flag = tickInterpolator.StartPoint != tickInterpolator.EndPoint;
			bool flag2 = tickViewAngles != viewAngles;
			if (flag)
			{
				if (AntiHack.ValidateMove(this, tickInterpolator, tickDeltaTime, in tickState))
				{
					using (TimeWarning.New("SetPosition"))
					{
						base.transform.localPosition = tickInterpolator.EndPoint;
						ticksPerSecond.Increment();
						tickHistory.AddPoint(tickInterpolator.EndPoint, tickHistoryCapacity);
					}
					using (TimeWarning.New("RecachingPlayerState"))
					{
						tickState.IsValid = true;
						tickState.WaterFactor = WaterFactor(out tickState.WaterInfo);
						tickState.IsSwimming = IsSwimming(tickState.WaterFactor);
					}
					AntiHack.FadeViolations(this, tickDeltaTime);
				}
				else
				{
					flag = false;
					if (ConVar.AntiHack.forceposition)
					{
						ClientRPC(RpcTarget.Player("ForcePositionToParentOffset", this), base.transform.localPosition, parentEntity.uid);
					}
				}
			}
			tickInterpolator.Reset(base.transform.localPosition);
			if (flag2)
			{
				viewAngles = tickViewAngles;
				if (!isMounted || !GetMounted().isMobile)
				{
					base.transform.rotation = Quaternion.identity;
				}
				base.transform.hasChanged = true;
			}
			if (flag || flag2)
			{
				eyes.NetworkUpdate(Quaternion.Euler(viewAngles));
				NetworkPositionTick();
				using (TimeWarning.New("AnalyticsTick"))
				{
					Analytics.Azure.OnPlayerTick(this);
				}
			}
			AntiHack.ValidateEyeHistory(this);
		}
		using (TimeWarning.New("ModelState"))
		{
			if (modelState != null)
			{
				modelState.waterLevel = tickState.WaterFactor;
			}
		}
		using (TimeWarning.New("EACStateUpdate"))
		{
			EACStateUpdate(in tickState);
		}
		using (TimeWarning.New("AntiHack.EnforceViolations"))
		{
			AntiHack.EnforceViolations(this);
		}
		tickDeltaTime = 0f;
	}

	public bool IsCraftingTutorialBlocked(ItemDefinition def, out bool forceUnlock)
	{
		forceUnlock = false;
		if (!IsInTutorial)
		{
			return false;
		}
		if (def.tutorialAllowance == TutorialItemAllowance.None)
		{
			return true;
		}
		bool num = CurrentTutorialAllowance >= def.tutorialAllowance;
		if (num && def.Blueprint != null && !def.Blueprint.defaultBlueprint)
		{
			forceUnlock = true;
		}
		return !num;
	}

	public bool CanModifyCraftAmountDuringTutorial()
	{
		if (IsInTutorial)
		{
			return CurrentTutorialAllowance >= TutorialItemAllowance.Level4_Spear_Fire;
		}
		return false;
	}

	public TutorialIsland GetCurrentTutorialIsland()
	{
		if (!IsInTutorial)
		{
			return null;
		}
		foreach (TutorialIsland tutorial in TutorialIsland.GetTutorialList(base.isServer))
		{
			if (tutorial.ForPlayer.Get(base.isServer) == this)
			{
				return tutorial;
			}
		}
		return null;
	}

	public void ClearTutorial()
	{
		SetPlayerFlag(PlayerFlags.IsInTutorial, b: false);
		SleepingBag.ClearTutorialBagsForPlayer(userID);
	}

	public void ClearTutorial_PostDeath()
	{
		ClearAllPings();
		ClearDeathMarker();
		WipeMissions();
		SendPingsToClient();
		SendMarkersToClient();
	}

	public void OnStartedTutorial()
	{
		ClearAllPings();
		WipeMissions();
	}

	public void SetTutorialAllowance(TutorialItemAllowance newAllowance)
	{
		if (newAllowance >= CurrentTutorialAllowance)
		{
			CurrentTutorialAllowance = newAllowance;
			SendNetworkUpdate();
		}
	}

	[RPC_Server]
	private void StartTutorial(RPCMessage msg)
	{
		if (!(msg.player != this))
		{
			StartTutorial(triggerAnalytics: true);
		}
	}

	public void StartTutorial(bool triggerAnalytics)
	{
		if (ConVar.Server.tutorialEnabled)
		{
			if (!TutorialIsland.HasAvailableTutorialIsland)
			{
				ShowToast(GameTip.Styles.Red_Normal, TutorialIsland.NoTutorialIslandsAvailablePhrase, false);
			}
			else if (startTutorialCooldown > UnityEngine.Time.realtimeSinceStartup)
			{
				int num = Mathf.CeilToInt(startTutorialCooldown - UnityEngine.Time.realtimeSinceStartup);
				ShowToast(GameTip.Styles.Red_Normal, TutorialIsland.TutorialIslandStartCooldown, false, num.ToString());
			}
			else
			{
				startTutorialCooldown = UnityEngine.Time.realtimeSinceStartup + (float)Debugging.tutorial_start_cooldown;
				Hurt(99999f);
				Respawn();
				TutorialIsland.RestoreOrCreateIslandForPlayer(this, triggerAnalytics);
			}
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(1uL)]
	[RPC_Server.FromOwner]
	private void PlayerRequestedTutorialStart(RPCMessage msg)
	{
		if (ConVar.Server.tutorialEnabled)
		{
			if (!TutorialIsland.HasAvailableTutorialIsland)
			{
				ShowToast(GameTip.Styles.Red_Normal, TutorialIsland.NoTutorialIslandsAvailablePhrase, false);
			}
			else
			{
				ClientRPC(RpcTarget.Player("PromptToStartTutorial", this));
			}
		}
	}

	public uint GetUnderwearSkin()
	{
		uint infoInt = (uint)GetInfoInt("client.underwearskin", 0);
		if (infoInt != lastValidUnderwearSkin && UnityEngine.Time.time > nextUnderwearValidationTime)
		{
			UnderwearManifest underwearManifest = UnderwearManifest.Get();
			nextUnderwearValidationTime = UnityEngine.Time.time + 0.2f;
			Underwear underwear = underwearManifest.GetUnderwear(infoInt);
			if (underwear == null)
			{
				lastValidUnderwearSkin = 0u;
			}
			else if (Underwear.Validate(underwear, this))
			{
				lastValidUnderwearSkin = infoInt;
			}
		}
		return lastValidUnderwearSkin;
	}

	[RPC_Server]
	public void ServerRPC_UnderwearChange(RPCMessage msg)
	{
		if (!(msg.player != this))
		{
			uint num = lastValidUnderwearSkin;
			uint underwearSkin = GetUnderwearSkin();
			if (num != underwearSkin)
			{
				SendNetworkUpdate();
			}
		}
	}

	public bool IsWounded()
	{
		return HasPlayerFlag(PlayerFlags.Wounded);
	}

	public bool IsCrawling()
	{
		if (HasPlayerFlag(PlayerFlags.Wounded))
		{
			return !HasPlayerFlag(PlayerFlags.Incapacitated);
		}
		return false;
	}

	public bool IsIncapacitated()
	{
		return HasPlayerFlag(PlayerFlags.Incapacitated);
	}

	private bool WoundInsteadOfDying(HitInfo info)
	{
		if (!EligibleForWounding(info))
		{
			return false;
		}
		BecomeWounded(info);
		return true;
	}

	private void ResetWoundingVars()
	{
		CancelInvoke(WoundingTick);
		woundedDuration = 0f;
		lastWoundedStartTime = float.NegativeInfinity;
		healingWhileCrawling = 0f;
		woundedByFallDamage = false;
	}

	public virtual bool EligibleForWounding(HitInfo info)
	{
		if (!ConVar.Server.woundingenabled)
		{
			return false;
		}
		if (IsWounded())
		{
			return false;
		}
		if (IsSleeping())
		{
			return false;
		}
		if (isMounted)
		{
			return false;
		}
		if (info == null)
		{
			return false;
		}
		if (!IsWounded() && UnityEngine.Time.realtimeSinceStartup - lastWoundedStartTime < ConVar.Server.rewounddelay)
		{
			return false;
		}
		BaseGameMode activeGameMode = BaseGameMode.GetActiveGameMode(serverside: true);
		if ((bool)activeGameMode && !activeGameMode.allowWounding)
		{
			return false;
		}
		if (triggers != null)
		{
			for (int i = 0; i < triggers.Count; i++)
			{
				if (triggers[i] is IHurtTrigger)
				{
					return false;
				}
			}
		}
		if (info.WeaponPrefab is BaseMelee)
		{
			return true;
		}
		if (info.WeaponPrefab is BaseProjectile)
		{
			return !info.isHeadshot;
		}
		return info.damageTypes.GetMajorityDamageType() switch
		{
			DamageType.Suicide => false, 
			DamageType.Fall => true, 
			DamageType.Bite => true, 
			DamageType.Bleeding => true, 
			DamageType.Hunger => true, 
			DamageType.Thirst => true, 
			DamageType.Poison => true, 
			_ => false, 
		};
	}

	public void BecomeWounded(HitInfo info = null)
	{
		if (IsWounded())
		{
			return;
		}
		bool flag = info != null && info.damageTypes.GetMajorityDamageType() == DamageType.Fall;
		if (IsCrawling())
		{
			woundedByFallDamage |= flag;
			GoToIncapacitated(info);
			return;
		}
		woundedByFallDamage = flag;
		if (flag || !ConVar.Server.crawlingenabled)
		{
			GoToIncapacitated(info);
		}
		else
		{
			GoToCrawling(info);
		}
	}

	public void StopWounded(BasePlayer source = null)
	{
		if (IsWounded())
		{
			RecoverFromWounded();
			CancelInvoke(WoundingTick);
			EACServer.LogPlayerRevive(source, this);
		}
	}

	public void ProlongWounding(float delay)
	{
		if (!IsRestrained)
		{
			woundedDuration = Mathf.Max(woundedDuration, Mathf.Min(TimeSinceWoundedStarted + delay, woundedDuration + delay));
			SendWoundedInformation(woundedDuration);
		}
	}

	public void SendWoundedInformation(float timeLeft)
	{
		float recoveryChance = GetRecoveryChance();
		ClientRPC(RpcTarget.Player("CLIENT_GetWoundedInformation", this), recoveryChance, timeLeft, woundedDuration);
	}

	public float GetRecoveryChance()
	{
		float num = (IsIncapacitated() ? ConVar.Server.incapacitatedrecoverchance : ConVar.Server.woundedrecoverchance);
		float num2 = Mathf.Lerp(t: (metabolism.hydration.Fraction() + metabolism.calories.Fraction()) / 2f, a: 0f, b: ConVar.Server.woundedmaxfoodandwaterbonus);
		float result = Mathf.Clamp01(num + num2);
		ItemDefinition itemDefinition = ItemManager.FindItemDefinition("largemedkit");
		if (inventory.containerBelt.FindItemByItemID(itemDefinition.itemid) != null && !woundedByFallDamage)
		{
			return 1f;
		}
		return result;
	}

	private void WoundingTick()
	{
		using (TimeWarning.New("WoundingTick"))
		{
			if (IsDead())
			{
				return;
			}
			if (!Player.woundforever && TimeSinceWoundedStarted >= woundedDuration)
			{
				float num = (IsIncapacitated() ? ConVar.Server.incapacitatedrecoverchance : ConVar.Server.woundedrecoverchance);
				float num2 = Mathf.Lerp(t: (metabolism.hydration.Fraction() + metabolism.calories.Fraction()) / 2f, a: 0f, b: ConVar.Server.woundedmaxfoodandwaterbonus);
				float num3 = Mathf.Clamp01(num + num2);
				if (UnityEngine.Random.value < num3)
				{
					RecoverFromWounded();
					return;
				}
				if (woundedByFallDamage)
				{
					Die();
					return;
				}
				ItemDefinition itemDefinition = ItemManager.FindItemDefinition("largemedkit");
				Item item = inventory.containerBelt.FindItemByItemID(itemDefinition.itemid);
				if (item != null)
				{
					item.UseItem();
					RecoverFromWounded();
				}
				else
				{
					Die();
				}
			}
			else
			{
				if (IsSwimming() && IsCrawling())
				{
					GoToIncapacitated(null);
				}
				Invoke(WoundingTick, 1f);
			}
		}
	}

	private void GoToCrawling(HitInfo info)
	{
		base.health = UnityEngine.Random.Range(ConVar.Server.crawlingminimumhealth, ConVar.Server.crawlingmaximumhealth);
		metabolism.bleeding.value = 0f;
		healingWhileCrawling = 0f;
		WoundedStartSharedCode(info);
		StartWoundedTick(40, 50);
		SendWoundedInformation(woundedDuration);
		SendNetworkUpdateImmediate();
	}

	public void GoToIncapacitated(HitInfo info)
	{
		if (!IsWounded())
		{
			WoundedStartSharedCode(info);
		}
		base.health = UnityEngine.Random.Range(2f, 6f);
		metabolism.bleeding.value = 0f;
		healingWhileCrawling = 0f;
		SetPlayerFlag(PlayerFlags.Incapacitated, b: true);
		SetServerFall(wantsOn: true);
		inventory.DropBackpackOnDeath();
		StartWoundedTick(10, 25);
		SendWoundedInformation(woundedDuration);
		SendNetworkUpdateImmediate();
	}

	private void WoundedStartSharedCode(HitInfo info)
	{
		stats.Add("wounded", 1, (Stats)5);
		SetPlayerFlag(PlayerFlags.Wounded, b: true);
		if ((bool)BaseGameMode.GetActiveGameMode(base.isServer))
		{
			BaseGameMode.GetActiveGameMode(base.isServer).OnPlayerWounded(info.InitiatorPlayer, this, info);
		}
	}

	private void StartWoundedTick(int minTime, int maxTime)
	{
		woundedDuration = UnityEngine.Random.Range(minTime, maxTime + 1);
		ApplyWoundedStartTime();
		Invoke(WoundingTick, 1f);
	}

	public void ApplyWoundedStartTime()
	{
		lastWoundedStartTime = UnityEngine.Time.realtimeSinceStartup;
	}

	private void RecoverFromWounded()
	{
		if (IsCrawling())
		{
			base.health = UnityEngine.Random.Range(2f, 6f) + healingWhileCrawling;
		}
		healingWhileCrawling = 0f;
		SetPlayerFlag(PlayerFlags.Wounded, b: false);
		SetPlayerFlag(PlayerFlags.Incapacitated, b: false);
		if ((bool)BaseGameMode.GetActiveGameMode(base.isServer))
		{
			BaseGameMode.GetActiveGameMode(base.isServer).OnPlayerRevived(null, this);
		}
	}

	private bool WoundingCausingImmortality(HitInfo info)
	{
		if (!IsWounded())
		{
			return false;
		}
		if (TimeSinceWoundedStarted > 0.25f)
		{
			return false;
		}
		if (info != null && info.damageTypes.GetMajorityDamageType() == DamageType.Fall)
		{
			return false;
		}
		return true;
	}

	public override BasePlayer ToPlayer()
	{
		return this;
	}

	public static string SanitizePlayerNameString(string playerName, ulong userId)
	{
		playerName = playerName.ToPrintable(32).EscapeRichText().Trim();
		if (string.IsNullOrWhiteSpace(playerName))
		{
			playerName = userId.ToString();
		}
		return playerName;
	}

	public bool IsGod()
	{
		if (base.isServer && (IsAdmin || IsDeveloper) && IsConnected && net.connection != null && net.connection.info.GetBool("global.god"))
		{
			return true;
		}
		return false;
	}

	public override Quaternion GetNetworkRotation()
	{
		if (base.isServer)
		{
			return Quaternion.Euler(viewAngles);
		}
		return Quaternion.identity;
	}

	public bool CanInteract()
	{
		return CanInteract(usableWhileCrawling: false);
	}

	public bool CanInteract(bool usableWhileCrawling)
	{
		bool flag = CurrentGestureIsSurrendering;
		if (!flag && IsRestrained)
		{
			Handcuffs restraintItem = Belt.GetRestraintItem();
			flag = restraintItem != null && restraintItem.BlockUse;
		}
		if (!IsDead() && !IsSleeping() && !IsSpectating() && (usableWhileCrawling ? (!IsIncapacitated()) : (!IsWounded())) && !HasActiveTelephone)
		{
			return !flag;
		}
		return false;
	}

	public override float StartHealth()
	{
		return UnityEngine.Random.Range(50f, 60f);
	}

	public override float StartMaxHealth()
	{
		return 100f;
	}

	public override float MaxHealth()
	{
		return 100f * (1f + ((modifiers != null) ? modifiers.GetValue(Modifier.ModifierType.Max_Health) : 0f));
	}

	public override float MaxVelocity()
	{
		if (IsSleeping())
		{
			return 0f;
		}
		if (isMounted)
		{
			return GetMounted().MaxVelocity();
		}
		return GetMaxSpeed();
	}

	public override OBB WorldSpaceBounds()
	{
		if (IsSleeping())
		{
			Vector3 center = bounds.center;
			Vector3 size = bounds.size;
			center.y /= 2f;
			size.y /= 2f;
			return new OBB(base.transform.position, base.transform.lossyScale, base.transform.rotation, new Bounds(center, size));
		}
		return base.WorldSpaceBounds();
	}

	public Vector3 GetMountVelocity()
	{
		BaseMountable baseMountable = GetMounted();
		if (!(baseMountable != null))
		{
			return Vector3.zero;
		}
		return baseMountable.GetWorldVelocity();
	}

	public override Vector3 GetInheritedProjectileVelocity(Vector3 direction)
	{
		BaseMountable baseMountable = GetMounted();
		if (!baseMountable)
		{
			return base.GetInheritedProjectileVelocity(direction);
		}
		return baseMountable.GetInheritedProjectileVelocity(direction);
	}

	public override Vector3 GetInheritedThrowVelocity(Vector3 direction)
	{
		BaseMountable baseMountable = GetMounted();
		if (!baseMountable)
		{
			return base.GetInheritedThrowVelocity(direction);
		}
		return baseMountable.GetInheritedThrowVelocity(direction);
	}

	public override Vector3 GetInheritedDropVelocity()
	{
		BaseMountable baseMountable = GetMounted();
		if (!baseMountable)
		{
			return base.GetInheritedDropVelocity();
		}
		return baseMountable.GetInheritedDropVelocity();
	}

	public override void PreInitShared()
	{
		base.PreInitShared();
		cachedProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
		baseProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
		inventoryValue.Set(GetComponent<PlayerInventory>());
		blueprints = GetComponent<PlayerBlueprints>();
		metabolism = GetComponent<PlayerMetabolism>();
		modifiers = GetComponent<PlayerModifiers>();
		colliderValue.Set(GetComponent<CapsuleCollider>());
		eyesValue.Set(GetComponent<PlayerEyes>());
		playerColliderStanding = new CapsuleColliderInfo(playerCollider.height, playerCollider.radius, playerCollider.center);
		playerColliderDucked = new CapsuleColliderInfo(1.5f, playerCollider.radius, Vector3.up * 0.75f);
		playerColliderCrawling = new CapsuleColliderInfo(playerCollider.radius, playerCollider.radius, Vector3.up * playerCollider.radius);
		playerColliderLyingDown = new CapsuleColliderInfo(0.4f, playerCollider.radius, Vector3.up * 0.2f);
		Belt = new PlayerBelt(this);
	}

	public override void DestroyShared()
	{
		UnityEngine.Object.Destroy(cachedProtection);
		UnityEngine.Object.Destroy(baseProtection);
		base.DestroyShared();
	}

	public override bool InSafeZone()
	{
		if (base.isServer)
		{
			return base.InSafeZone();
		}
		return false;
	}

	public bool IsInNoRespawnZone()
	{
		if (base.isServer)
		{
			return InNoRespawnZone();
		}
		return false;
	}

	public bool IsOnATugboat()
	{
		if (GetMountedVehicle() is Tugboat)
		{
			return true;
		}
		if (GetParentEntity() is Tugboat)
		{
			return true;
		}
		return false;
	}

	public static void ServerCycle(float deltaTime)
	{
		for (int i = 0; i < activePlayerList.Values.Count; i++)
		{
			if (activePlayerList.Values[i] == null)
			{
				activePlayerList.RemoveAt(i--);
			}
		}
		List<BasePlayer> obj = Facepunch.Pool.Get<List<BasePlayer>>();
		for (int j = 0; j < activePlayerList.Count; j++)
		{
			obj.Add(activePlayerList[j]);
		}
		for (int k = 0; k < obj.Count; k++)
		{
			if (!(obj[k] == null))
			{
				obj[k].ServerUpdate(deltaTime);
			}
		}
		for (int l = 0; l < bots.Count; l++)
		{
			bots[l].ServerUpdateBots(deltaTime);
		}
		if (ConVar.Server.idlekick > 0 && ((SingletonComponent<ServerMgr>.Instance.AvailableSlots <= 0 && ConVar.Server.idlekickmode == 1) || ConVar.Server.idlekickmode == 2))
		{
			for (int m = 0; m < obj.Count; m++)
			{
				if (!(obj[m].IdleTime < (float)(ConVar.Server.idlekick * 60)) && (!obj[m].IsAdmin || ConVar.Server.idlekickadmins != 0) && (!obj[m].IsDeveloper || ConVar.Server.idlekickadmins != 0))
				{
					obj[m].Kick("Idle for " + ConVar.Server.idlekick + " minutes");
				}
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	private bool ManuallyCheckSafezone()
	{
		if (!base.isServer)
		{
			return false;
		}
		List<Collider> obj = Facepunch.Pool.Get<List<Collider>>();
		Vis.Colliders(base.transform.position, 0f, obj);
		foreach (Collider item in obj)
		{
			if (item.GetComponent<TriggerSafeZone>() != null)
			{
				Facepunch.Pool.FreeUnmanaged(ref obj);
				return true;
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return false;
	}

	public override bool OnStartBeingLooted(BasePlayer baseEntity)
	{
		if ((baseEntity.InSafeZone() || InSafeZone() || ManuallyCheckSafezone()) && (ulong)baseEntity.userID != (ulong)userID)
		{
			return false;
		}
		if (RelationshipManager.ServerInstance != null)
		{
			if ((IsSleeping() || IsIncapacitated()) && !RelationshipManager.ServerInstance.HasRelations(baseEntity.userID, userID))
			{
				RelationshipManager.ServerInstance.SetRelationship(baseEntity, this, RelationshipManager.RelationshipType.Acquaintance);
			}
			RelationshipManager.ServerInstance.SetSeen(baseEntity, this);
		}
		if (IsCrawling())
		{
			GoToIncapacitated(null);
		}
		if (inventory.crafting != null)
		{
			inventory.crafting.CancelAll();
		}
		return base.OnStartBeingLooted(baseEntity);
	}

	public Bounds GetBounds(bool ducked)
	{
		return new Bounds(base.transform.position + GetOffset(ducked), GetSize(ducked));
	}

	public Bounds GetBounds()
	{
		return GetBounds(modelState.ducked);
	}

	public Vector3 GetCenter(bool ducked)
	{
		return base.transform.position + GetOffset(ducked);
	}

	public Vector3 GetCenter()
	{
		return GetCenter(modelState.ducked);
	}

	public Vector3 GetOffset(bool ducked)
	{
		if (ducked)
		{
			return new Vector3(0f, 0.55f, 0f);
		}
		return new Vector3(0f, 0.9f, 0f);
	}

	public Vector3 GetOffset()
	{
		return GetOffset(modelState.ducked);
	}

	public Vector3 GetSize(bool ducked)
	{
		if (ducked)
		{
			return new Vector3(1f, 1.1f, 1f);
		}
		return new Vector3(1f, 1.8f, 1f);
	}

	public Vector3 GetSize()
	{
		return GetSize(modelState.ducked);
	}

	public float GetHeight(bool ducked)
	{
		if (ducked)
		{
			return 1.1f;
		}
		return 1.8f;
	}

	public float GetHeight()
	{
		return GetHeight(modelState.ducked);
	}

	public float GetRadius()
	{
		return 0.5f;
	}

	public float GetJumpHeight()
	{
		return 1.5f;
	}

	public override Vector3 TriggerPoint()
	{
		return base.transform.position + NoClipOffset();
	}

	public Vector3 NoClipOffset()
	{
		return new Vector3(0f, GetHeight(ducked: true) - GetRadius(), 0f);
	}

	public float NoClipRadius(float margin)
	{
		return GetRadius() - margin;
	}

	public float MaxDeployDistance(Item item)
	{
		return 8f;
	}

	public float GetMinSpeed()
	{
		return GetSpeed(0f, 0f, 1f);
	}

	public float GetMaxSpeed()
	{
		return GetSpeed(1f, 0f, 0f);
	}

	public float GetSpeed(float running, float ducking, float crawling)
	{
		return GetSpeed(running, ducking, crawling, IsSwimming());
	}

	internal float GetSpeed(float running, float ducking, float crawling, bool isSwimming)
	{
		float num = 1f;
		num -= clothingMoveSpeedReduction;
		if (isSwimming)
		{
			num += clothingWaterSpeedBonus;
		}
		if (crawling > 0f)
		{
			return Mathf.Lerp(2.8f, 0.72f, crawling) * num;
		}
		return Mathf.Lerp(Mathf.Lerp(2.8f, 5.5f, running), 1.7f, ducking) * num * weaponMoveSpeedScale;
	}

	public override void OnAttacked(HitInfo info)
	{
		float oldHealth = base.health;
		if (InSafeZone() && !IsHostile() && info.Initiator != null && info.Initiator != this)
		{
			info.damageTypes.ScaleAll(0f);
		}
		if (base.isServer)
		{
			HitArea boneArea = info.boneArea;
			if (boneArea != (HitArea)(-1))
			{
				List<Item> obj = Facepunch.Pool.Get<List<Item>>();
				obj.AddRange(inventory.containerWear.itemList);
				for (int i = 0; i < obj.Count; i++)
				{
					Item item = obj[i];
					if (item != null)
					{
						ItemModWearable component = item.info.GetComponent<ItemModWearable>();
						if (!(component == null) && component.ProtectsArea(boneArea))
						{
							item.OnAttacked(info);
						}
					}
				}
				Facepunch.Pool.Free(ref obj, freeElements: false);
				inventory.ServerUpdate(0f);
			}
		}
		base.OnAttacked(info);
		if (base.isServer && base.isServer && info.hasDamage)
		{
			if (!info.damageTypes.Has(DamageType.Bleeding) && info.damageTypes.IsBleedCausing() && !IsWounded() && !IsImmortalTo(info))
			{
				metabolism.bleeding.Add(info.damageTypes.Total() * 0.2f);
			}
			if (isMounted)
			{
				GetMounted().MounteeTookDamage(this, info);
			}
			CheckDeathCondition(info);
			if (net != null && net.connection != null)
			{
				ClientRPC(RpcTarget.Player("TakeDamageHit", this));
			}
			string text = StringPool.Get(info.HitBone);
			bool flag = Vector3.Dot((info.PointEnd - info.PointStart).normalized, eyes.BodyForward()) > 0.4f;
			BasePlayer initiatorPlayer = info.InitiatorPlayer;
			if ((bool)initiatorPlayer && !info.damageTypes.IsMeleeType())
			{
				initiatorPlayer.LifeStoryShotHit(info.Weapon);
			}
			if (info.isHeadshot)
			{
				if (flag)
				{
					SignalBroadcast(Signal.Flinch_RearHead, string.Empty);
				}
				else
				{
					SignalBroadcast(Signal.Flinch_Head, string.Empty);
				}
				Effect.server.Run("assets/bundled/prefabs/fx/headshot.prefab", this, 0u, new Vector3(0f, 2f, 0f), Vector3.zero, (initiatorPlayer != null) ? initiatorPlayer.net.connection : null);
				if ((bool)initiatorPlayer)
				{
					initiatorPlayer.stats.Add("headshot", 1, (Stats)5);
					if (initiatorPlayer.IsBeingSpectated)
					{
						foreach (BaseEntity child in initiatorPlayer.children)
						{
							if (child is BasePlayer basePlayer)
							{
								basePlayer.ClientRPC(RpcTarget.Player("SpectatedPlayerHeadshot", basePlayer));
							}
						}
					}
				}
			}
			else if (flag)
			{
				SignalBroadcast(Signal.Flinch_RearTorso, string.Empty);
			}
			else if (text == "spine" || text == "spine2")
			{
				SignalBroadcast(Signal.Flinch_Stomach, string.Empty);
			}
			else
			{
				SignalBroadcast(Signal.Flinch_Chest, string.Empty);
			}
		}
		if (stats != null)
		{
			if (IsWounded())
			{
				stats.combat.LogAttack(info, "wounded", oldHealth);
			}
			else if (IsDead())
			{
				stats.combat.LogAttack(info, "killed", oldHealth);
			}
			else
			{
				stats.combat.LogAttack(info, "", oldHealth);
			}
		}
		if (ConVar.Global.cinematicGingerbreadCorpses)
		{
			info.HitMaterial = ConVar.Global.GingerbreadMaterialID();
		}
	}

	public void EnablePlayerCollider()
	{
		if (!playerCollider.enabled)
		{
			RefreshColliderSize(forced: true);
			playerCollider.enabled = true;
		}
	}

	public void DisablePlayerCollider()
	{
		if (playerCollider.enabled)
		{
			RemoveFromTriggers();
			playerCollider.enabled = false;
		}
	}

	private void RefreshColliderSize(bool forced)
	{
		if (forced || (playerCollider.enabled && !(UnityEngine.Time.time < nextColliderRefreshTime)))
		{
			nextColliderRefreshTime = UnityEngine.Time.time + 0.25f + UnityEngine.Random.Range(-0.05f, 0.05f);
			BaseMountable baseMountable = GetMounted();
			CapsuleColliderInfo capsuleColliderInfo = ((baseMountable != null && baseMountable.IsValid()) ? ((!baseMountable.modifiesPlayerCollider) ? playerColliderStanding : baseMountable.customPlayerCollider) : ((!IsIncapacitated() && !IsSleeping()) ? (IsCrawling() ? playerColliderCrawling : ((!modelState.ducked && !IsSwimming()) ? playerColliderStanding : playerColliderDucked)) : playerColliderLyingDown));
			if (playerCollider.height != capsuleColliderInfo.height || playerCollider.radius != capsuleColliderInfo.radius || playerCollider.center != capsuleColliderInfo.center)
			{
				playerCollider.height = capsuleColliderInfo.height;
				playerCollider.radius = capsuleColliderInfo.radius;
				playerCollider.center = capsuleColliderInfo.center;
			}
		}
	}

	private void SetPlayerRigidbodyState(bool isEnabled)
	{
		if (isEnabled)
		{
			AddPlayerRigidbody();
		}
		else
		{
			RemovePlayerRigidbody();
		}
	}

	private void AddPlayerRigidbody()
	{
		if (playerRigidbody == null)
		{
			playerRigidbody = base.gameObject.GetComponent<Rigidbody>();
		}
		if (playerRigidbody == null)
		{
			playerRigidbody = base.gameObject.AddComponent<Rigidbody>();
			playerRigidbody.useGravity = false;
			playerRigidbody.isKinematic = true;
			playerRigidbody.mass = 1f;
			playerRigidbody.interpolation = RigidbodyInterpolation.None;
			playerRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
		}
	}

	private void RemovePlayerRigidbody()
	{
		if (playerRigidbody == null)
		{
			playerRigidbody = base.gameObject.GetComponent<Rigidbody>();
		}
		if (playerRigidbody != null)
		{
			RemoveFromTriggers();
			UnityEngine.Object.DestroyImmediate(playerRigidbody);
			playerRigidbody = null;
		}
	}

	public bool IsEnsnared()
	{
		if (triggers == null)
		{
			return false;
		}
		for (int i = 0; i < triggers.Count; i++)
		{
			if (triggers[i] is TriggerEnsnare)
			{
				return true;
			}
		}
		return false;
	}

	public bool IsAttacking()
	{
		HeldEntity heldEntity = GetHeldEntity();
		if (heldEntity == null)
		{
			return false;
		}
		AttackEntity attackEntity = heldEntity as AttackEntity;
		if (attackEntity == null)
		{
			return false;
		}
		return attackEntity.NextAttackTime - UnityEngine.Time.time > attackEntity.repeatDelay - 1f;
	}

	public bool CanAttack()
	{
		HeldEntity heldEntity = GetHeldEntity();
		if (heldEntity == null)
		{
			return false;
		}
		bool flag = IsSwimming();
		bool flag2 = heldEntity.CanBeUsedInWater();
		if (modelState.onLadder)
		{
			return false;
		}
		if (!flag && !modelState.onground)
		{
			return false;
		}
		if (flag && !flag2)
		{
			return false;
		}
		if (IsEnsnared())
		{
			return false;
		}
		return true;
	}

	public bool OnLadder()
	{
		if (modelState.onLadder && !IsWounded())
		{
			return FindTrigger<TriggerLadder>();
		}
		return false;
	}

	public bool IsSwimming()
	{
		float num = 0f;
		num = WaterFactor();
		return IsSwimming(num);
	}

	private bool IsSwimming(float waterFactor)
	{
		return waterFactor >= 0.65f;
	}

	public bool IsHeadUnderwater()
	{
		return WaterFactor() > 0.75f;
	}

	public virtual bool IsOnGround()
	{
		return modelState.onground;
	}

	public bool IsRunning()
	{
		if (modelState != null)
		{
			return modelState.sprinting;
		}
		return false;
	}

	public bool IsDucked()
	{
		if (modelState != null)
		{
			return modelState.ducking > 0.5f;
		}
		return false;
	}

	public void ShowToast(GameTip.Styles style, Translate.Phrase phrase, bool overlay = false, params string[] arguments)
	{
		if (base.isServer)
		{
			SendConsoleCommand("gametip.showtoast_translated", (int)style, phrase.token, phrase.english, overlay, arguments);
		}
	}

	public void ChatMessage(string msg)
	{
		if (base.isServer)
		{
			SendConsoleCommand("chat.add", 2, 0, msg);
		}
	}

	public void ConsoleMessage(string msg)
	{
		if (base.isServer)
		{
			SendConsoleCommand("echo " + msg);
		}
	}

	public override float PenetrationResistance(HitInfo info)
	{
		return 100f;
	}

	public override void ScaleDamage(HitInfo info)
	{
		if (isMounted)
		{
			GetMounted().ScaleDamageForPlayer(this, info);
		}
		if (info.UseProtection)
		{
			HitArea boneArea = info.boneArea;
			if (boneArea != (HitArea)(-1))
			{
				cachedProtection.Clear();
				cachedProtection.Add(inventory.containerWear.itemList, boneArea);
				cachedProtection.Multiply(DamageType.Arrow, ConVar.Server.arrowarmor);
				cachedProtection.Multiply(DamageType.Bullet, ConVar.Server.bulletarmor);
				cachedProtection.Multiply(DamageType.Slash, ConVar.Server.meleearmor);
				cachedProtection.Multiply(DamageType.Blunt, ConVar.Server.meleearmor);
				cachedProtection.Multiply(DamageType.Stab, ConVar.Server.meleearmor);
				cachedProtection.Multiply(DamageType.Bleeding, ConVar.Server.bleedingarmor);
				cachedProtection.Scale(info.damageTypes);
			}
			else
			{
				baseProtection.Scale(info.damageTypes);
			}
		}
		if ((bool)info.damageProperties)
		{
			info.damageProperties.ScaleDamage(info);
		}
	}

	public void ResetWeaponMoveSpeedScale()
	{
		weaponMoveSpeedScale = 1f;
	}

	private void UpdateMoveSpeedFromClothing()
	{
		float num = 0f;
		float num2 = 0f;
		float num3 = 0f;
		bool flag = false;
		bool flag2 = false;
		float num4 = 0f;
		eggVision = 0f;
		base.Weight = 0f;
		foreach (Item item in inventory.containerWear.itemList)
		{
			ItemModWearable component = item.info.GetComponent<ItemModWearable>();
			if ((bool)component)
			{
				if (component.blocksAiming)
				{
					flag = true;
				}
				if (component.blocksEquipping)
				{
					flag2 = true;
				}
				num4 += component.accuracyBonus;
				eggVision += component.eggVision;
				base.Weight += component.weight;
				if (component.movementProperties != null)
				{
					num2 += component.movementProperties.speedReduction;
					num = Mathf.Max(num, component.movementProperties.minSpeedReduction);
					num3 += component.movementProperties.waterSpeedBonus;
				}
			}
		}
		clothingAccuracyBonus = num4;
		clothingMoveSpeedReduction = Mathf.Max(num2, num);
		clothingBlocksAiming = flag;
		clothingWaterSpeedBonus = num3;
		equippingBlocked = flag2;
		if (base.isServer && equippingBlocked)
		{
			UpdateActiveItem(default(ItemId));
		}
	}

	public virtual void UpdateProtectionFromClothing()
	{
		baseProtection.Clear();
		baseProtection.Add(inventory.containerWear.itemList);
		float num = 1f / 6f;
		for (int i = 0; i < baseProtection.amounts.Length; i++)
		{
			switch (i)
			{
			case 22:
				baseProtection.amounts[i] = 1f;
				break;
			default:
				baseProtection.amounts[i] *= num;
				break;
			case 17:
				break;
			}
		}
	}

	public override string Categorize()
	{
		return "player";
	}

	public override string ToString()
	{
		if (_name == null)
		{
			if (base.isServer)
			{
				_name = $"{displayName}[{userID.Get()}]";
			}
			else
			{
				_name = base.ShortPrefabName;
			}
		}
		return _name;
	}

	public string GetDebugStatus()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendFormat("Entity: {0}\n", ToString());
		stringBuilder.AppendFormat("Name: {0}\n", displayName);
		stringBuilder.AppendFormat("SteamID: {0}\n", userID.Get());
		foreach (PlayerFlags value in Enum.GetValues(typeof(PlayerFlags)))
		{
			stringBuilder.AppendFormat("{1}: {0}\n", HasPlayerFlag(value), value);
		}
		return stringBuilder.ToString();
	}

	public override Item GetItem(ItemId itemId)
	{
		if (inventory == null)
		{
			return null;
		}
		return inventory.FindItemByUID(itemId);
	}

	public override float WaterFactor()
	{
		WaterLevel.WaterInfo info;
		return WaterFactor(out info);
	}

	internal float WaterFactor(out WaterLevel.WaterInfo info)
	{
		info = default(WaterLevel.WaterInfo);
		if (GetMounted().IsValid())
		{
			return GetMounted().WaterFactorForPlayer(this);
		}
		if (GetParentEntity() != null && GetParentEntity().BlocksWaterFor(this))
		{
			return 0f;
		}
		float radius = playerCollider.radius;
		float num = playerCollider.height * 0.5f;
		Vector3 start = playerCollider.transform.position + playerCollider.transform.rotation * (playerCollider.center - Vector3.up * (num - radius));
		Vector3 end = playerCollider.transform.position + playerCollider.transform.rotation * (playerCollider.center + Vector3.up * (num - radius));
		info = WaterLevel.GetWaterInfo(start, end, radius, waves: true, volumes: true, this);
		return WaterLevel.Factor(in info, start, end, radius);
	}

	public override float AirFactor()
	{
		float num = ((WaterFactor() >= 1f) ? 0f : 1f);
		BaseMountable baseMountable = GetMounted();
		if (baseMountable.IsValid() && baseMountable.BlocksWaterFor(this))
		{
			float num2 = baseMountable.AirFactor();
			if (num2 < num)
			{
				num = num2;
			}
		}
		return num;
	}

	public float GetOxygenTime(out ItemModGiveOxygen.AirSupplyType airSupplyType)
	{
		BaseVehicle mountedVehicle = GetMountedVehicle();
		if (mountedVehicle.IsValid() && mountedVehicle is IAirSupply airSupply)
		{
			float airTimeRemaining = airSupply.GetAirTimeRemaining(null);
			if (airTimeRemaining > 0f)
			{
				airSupplyType = airSupply.AirType;
				return airTimeRemaining;
			}
		}
		foreach (Item item in inventory.containerWear.itemList)
		{
			IAirSupply componentInChildren = item.info.GetComponentInChildren<IAirSupply>();
			if (componentInChildren != null)
			{
				float airTimeRemaining2 = componentInChildren.GetAirTimeRemaining(item);
				if (airTimeRemaining2 > 0f)
				{
					airSupplyType = componentInChildren.AirType;
					return airTimeRemaining2;
				}
			}
		}
		airSupplyType = ItemModGiveOxygen.AirSupplyType.Lungs;
		if (metabolism.oxygen.value > 0.5f)
		{
			float num = Mathf.InverseLerp(0.5f, 1f, metabolism.oxygen.value);
			return 5f * num;
		}
		return 0f;
	}

	public override bool ShouldInheritNetworkGroup()
	{
		return IsSpectating();
	}

	public static bool AnyPlayersVisibleToEntity(Vector3 pos, float radius, BaseEntity source, Vector3 entityEyePos, bool ignorePlayersWithPriv = false)
	{
		List<RaycastHit> obj = Facepunch.Pool.Get<List<RaycastHit>>();
		List<BasePlayer> obj2 = Facepunch.Pool.Get<List<BasePlayer>>();
		Vis.Entities(pos, radius, obj2, 131072);
		bool flag = false;
		foreach (BasePlayer item in obj2)
		{
			if (item.IsSleeping() || !item.IsAlive() || (item.IsBuildingAuthed() && ignorePlayersWithPriv))
			{
				continue;
			}
			obj.Clear();
			GamePhysics.TraceAll(new Ray(item.eyes.position, (entityEyePos - item.eyes.position).normalized), 0f, obj, 9f, 1218519297);
			for (int i = 0; i < obj.Count; i++)
			{
				BaseEntity entity = obj[i].GetEntity();
				if (entity != null && (entity == source || entity.EqualNetID(source)))
				{
					flag = true;
					break;
				}
				if (!(entity != null) || entity.ShouldBlockProjectiles())
				{
					break;
				}
			}
			if (flag)
			{
				break;
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		Facepunch.Pool.FreeUnmanaged(ref obj2);
		return flag;
	}

	public bool IsStandingOnEntity(BaseEntity standingOn, int layerMask)
	{
		if (!IsOnGround())
		{
			return false;
		}
		if (UnityEngine.Physics.SphereCast(base.transform.position + Vector3.up * (0.25f + GetRadius()), GetRadius() * 0.95f, Vector3.down, out var hitInfo, 4f, layerMask))
		{
			BaseEntity entity = hitInfo.GetEntity();
			if (entity != null)
			{
				if (entity.EqualNetID(standingOn))
				{
					return true;
				}
				BaseEntity baseEntity = entity.GetParentEntity();
				if (baseEntity != null && baseEntity.EqualNetID(standingOn))
				{
					return true;
				}
			}
		}
		return false;
	}

	public void SetActiveTelephone(PhoneController t)
	{
		activeTelephone = t;
	}

	public void ClearDesigningAIEntity()
	{
		if (IsDesigningAI)
		{
			designingAIEntity.GetComponent<IAIDesign>()?.StopDesigning();
		}
		designingAIEntity = null;
	}
}
