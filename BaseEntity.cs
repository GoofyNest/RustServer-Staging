#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using ConVar;
using Facepunch;
using Facepunch.Extend;
using Network;
using ProtoBuf;
using Rust;
using Rust.Workshop;
using Spatial;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

public class BaseEntity : BaseNetworkable, IOnParentSpawning, IPrefabPreProcess
{
	public class Menu : Attribute
	{
		[Serializable]
		public struct Option
		{
			public Translate.Phrase name;

			public Translate.Phrase description;

			public Sprite icon;

			public int order;

			public bool usableWhileWounded;
		}

		public class Description : Attribute
		{
			public string token;

			public string english;

			public Description(string t, string e)
			{
				token = t;
				english = e;
			}
		}

		public class Icon : Attribute
		{
			public string icon;

			public Icon(string i)
			{
				icon = i;
			}
		}

		public class ShowIf : Attribute
		{
			public string functionName;

			public ShowIf(string testFunc)
			{
				functionName = testFunc;
			}
		}

		public class Priority : Attribute
		{
			public string functionName;

			public Priority(string priorityFunc)
			{
				functionName = priorityFunc;
			}
		}

		public class UsableWhileWounded : Attribute
		{
		}

		public string TitleToken;

		public string TitleEnglish;

		public string UseVariable;

		public int Order;

		public string ProxyFunction;

		public float Time;

		public string OnStart;

		public string OnProgress;

		public bool LongUseOnly;

		public bool PrioritizeIfNotWhitelisted;

		public bool PrioritizeIfUnlocked;

		public Menu()
		{
		}

		public Menu(string menuTitleToken, string menuTitleEnglish)
		{
			TitleToken = menuTitleToken;
			TitleEnglish = menuTitleEnglish;
		}
	}

	[Serializable]
	public struct MovementModify
	{
		public float drag;
	}

	public enum GiveItemReason
	{
		Generic,
		ResourceHarvested,
		PickedUp,
		Crafted
	}

	[Flags]
	public enum Flags
	{
		Placeholder = 1,
		On = 2,
		OnFire = 4,
		Open = 8,
		Locked = 0x10,
		Debugging = 0x20,
		Disabled = 0x40,
		Reserved1 = 0x80,
		Reserved2 = 0x100,
		Reserved3 = 0x200,
		Reserved4 = 0x400,
		Reserved5 = 0x800,
		Broken = 0x1000,
		Busy = 0x2000,
		Reserved6 = 0x4000,
		Reserved7 = 0x8000,
		Reserved8 = 0x10000,
		Reserved9 = 0x20000,
		Reserved10 = 0x40000,
		Reserved11 = 0x80000,
		InUse = 0x100000,
		Reserved12 = 0x200000,
		Reserved13 = 0x400000,
		Unused23 = 0x800000,
		Protected = 0x1000000,
		Transferring = 0x2000000
	}

	private readonly struct QueuedFileRequest : IEquatable<QueuedFileRequest>
	{
		public readonly BaseEntity Entity;

		public readonly FileStorage.Type Type;

		public readonly uint Part;

		public readonly uint Crc;

		public readonly uint ResponseFunction;

		public readonly bool? RespondIfNotFound;

		public QueuedFileRequest(BaseEntity entity, FileStorage.Type type, uint part, uint crc, uint responseFunction, bool? respondIfNotFound)
		{
			Entity = entity;
			Type = type;
			Part = part;
			Crc = crc;
			ResponseFunction = responseFunction;
			RespondIfNotFound = respondIfNotFound;
		}

		public bool Equals(QueuedFileRequest other)
		{
			if (object.Equals(Entity, other.Entity) && Type == other.Type && Part == other.Part && Crc == other.Crc && ResponseFunction == other.ResponseFunction)
			{
				return RespondIfNotFound == other.RespondIfNotFound;
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is QueuedFileRequest other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return (int)(((((((((uint)(((Entity != null) ? Entity.GetHashCode() : 0) * 397) ^ (uint)Type) * 397) ^ Part) * 397) ^ Crc) * 397) ^ ResponseFunction) * 397) ^ RespondIfNotFound.GetHashCode();
		}
	}

	private readonly struct PendingFileRequest : IEquatable<PendingFileRequest>
	{
		public readonly FileStorage.Type Type;

		public readonly uint NumId;

		public readonly uint Crc;

		public readonly IServerFileReceiver Receiver;

		public readonly float Time;

		public PendingFileRequest(FileStorage.Type type, uint numId, uint crc, IServerFileReceiver receiver)
		{
			Type = type;
			NumId = numId;
			Crc = crc;
			Receiver = receiver;
			Time = UnityEngine.Time.realtimeSinceStartup;
		}

		public bool Equals(PendingFileRequest other)
		{
			if (Type == other.Type && NumId == other.NumId && Crc == other.Crc)
			{
				return object.Equals(Receiver, other.Receiver);
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is PendingFileRequest other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return (int)(((((uint)((int)Type * 397) ^ NumId) * 397) ^ Crc) * 397) ^ ((Receiver != null) ? Receiver.GetHashCode() : 0);
		}
	}

	public static class Query
	{
		public enum DistanceCheckType
		{
			None,
			OnlyCenter,
			Bounds
		}

		public class EntityTree
		{
			private Grid<BaseEntity> Grid;

			private Grid<BasePlayer> PlayerGrid;

			private Grid<BaseEntity> BrainGrid;

			public EntityTree(float worldSize)
			{
				Grid = new Grid<BaseEntity>(32, worldSize);
				PlayerGrid = new Grid<BasePlayer>(32, worldSize);
				BrainGrid = new Grid<BaseEntity>(32, worldSize);
			}

			public void Add(BaseEntity ent)
			{
				Vector3 position = ent.transform.position;
				Grid.Add(ent, position.x, position.z);
			}

			public void AddPlayer(BasePlayer player)
			{
				Vector3 position = player.transform.position;
				PlayerGrid.Add(player, position.x, position.z);
			}

			public void AddBrain(BaseEntity entity)
			{
				Vector3 position = entity.transform.position;
				BrainGrid.Add(entity, position.x, position.z);
			}

			public void Remove(BaseEntity ent, bool isPlayer = false)
			{
				Grid.Remove(ent);
				if (isPlayer)
				{
					BasePlayer basePlayer = ent as BasePlayer;
					if (basePlayer != null)
					{
						PlayerGrid.Remove(basePlayer);
					}
				}
			}

			public void RemovePlayer(BasePlayer player)
			{
				PlayerGrid.Remove(player);
			}

			public void RemoveBrain(BaseEntity entity)
			{
				if (!(entity == null))
				{
					BrainGrid.Remove(entity);
				}
			}

			public void Move(BaseEntity ent)
			{
				Vector3 position = ent.transform.position;
				Grid.Move(ent, position.x, position.z);
				BasePlayer basePlayer = ent as BasePlayer;
				if (basePlayer != null)
				{
					MovePlayer(basePlayer);
				}
				if (ent.HasBrain)
				{
					MoveBrain(ent);
				}
			}

			public void MovePlayer(BasePlayer player)
			{
				Vector3 position = player.transform.position;
				PlayerGrid.Move(player, position.x, position.z);
			}

			public void MoveBrain(BaseEntity entity)
			{
				Vector3 position = entity.transform.position;
				BrainGrid.Move(entity, position.x, position.z);
			}

			public void GetInSphere<T>(Vector3 position, float distance, List<T> results, DistanceCheckType distanceCheckType = DistanceCheckType.OnlyCenter) where T : BaseEntity
			{
				using (TimeWarning.New("GetInSphereList"))
				{
					Grid.Query(position.x, position.z, distance, results);
					if (distanceCheckType != 0)
					{
						NarrowPhaseReduce(position, distance, results, distanceCheckType == DistanceCheckType.OnlyCenter);
					}
				}
			}

			public int GetInSphere(Vector3 position, float distance, BaseEntity[] results, Func<BaseEntity, bool> filter = null)
			{
				int broadCount = Grid.Query(position.x, position.z, distance, results, filter);
				return NarrowPhaseReduce(position, distance, results, broadCount);
			}

			public int GetInSphereFast(Vector3 position, float distance, BaseEntity[] results, Func<BaseEntity, bool> filter = null)
			{
				return Grid.Query(position.x, position.z, distance, results, filter);
			}

			public void GetPlayersInSphere(Vector3 position, float distance, List<BasePlayer> results, DistanceCheckType distanceCheckType = DistanceCheckType.OnlyCenter)
			{
				using (TimeWarning.New("GetPlayersInSphereList"))
				{
					PlayerGrid.Query(position.x, position.z, distance, results);
					if (distanceCheckType != 0)
					{
						NarrowPhaseReduce(position, distance, results, distanceCheckType == DistanceCheckType.OnlyCenter);
					}
				}
			}

			public int GetPlayersInSphere(Vector3 position, float distance, BasePlayer[] results, Func<BasePlayer, bool> filter = null)
			{
				int broadCount = PlayerGrid.Query(position.x, position.z, distance, results, filter);
				return NarrowPhaseReduce(position, distance, results, broadCount);
			}

			public int GetPlayersInSphereFast(Vector3 position, float distance, BasePlayer[] results, Func<BasePlayer, bool> filter = null)
			{
				return PlayerGrid.Query(position.x, position.z, distance, results, filter);
			}

			public void GetBrainsInSphere<T>(Vector3 position, float distance, List<T> results, bool filterPastDistance = true) where T : BaseEntity
			{
				using (TimeWarning.New("GetBrainsInSphereList"))
				{
					BrainGrid.Query(position.x, position.z, distance, results);
					if (filterPastDistance)
					{
						NarrowPhaseReduce(position, distance, results);
					}
				}
			}

			public int GetBrainsInSphere(Vector3 position, float distance, BaseEntity[] results, Func<BaseEntity, bool> filter = null)
			{
				int broadCount = BrainGrid.Query(position.x, position.z, distance, results, filter);
				return NarrowPhaseReduce(position, distance, results, broadCount);
			}

			public int GetBrainsInSphereFast(Vector3 position, float distance, BaseEntity[] results, Func<BaseEntity, bool> filter = null)
			{
				return BrainGrid.Query(position.x, position.z, distance, results, filter);
			}

			public void GetPlayersAndBrainsInSphere(Vector3 position, float distance, List<BaseEntity> results, DistanceCheckType distanceCheckType = DistanceCheckType.OnlyCenter)
			{
				using (TimeWarning.New("GetPlayersAndBrainsInSphereList"))
				{
					PlayerGrid.Query(position.x, position.z, distance, results);
					BrainGrid.Query(position.x, position.z, distance, results);
					if (distanceCheckType != 0)
					{
						NarrowPhaseReduce(position, distance, results, distanceCheckType == DistanceCheckType.OnlyCenter);
					}
				}
			}

			private int NarrowPhaseReduce<T>(Vector3 position, float radius, T[] results, int broadCount) where T : BaseEntity
			{
				using (TimeWarning.New("NarrowPhaseReduce"))
				{
					int num = broadCount;
					float num2 = radius * radius;
					for (int i = 0; i < num; i++)
					{
						if ((results[i].WorldSpaceBounds().ClosestPoint(position) - position).sqrMagnitude > num2)
						{
							results[i] = results[num - 1];
							num--;
							i--;
						}
					}
					return num;
				}
			}

			private static void NarrowPhaseReduce<T>(Vector3 position, float radius, List<T> results, bool onlyConsiderCenter = true) where T : BaseEntity
			{
				using (TimeWarning.New("NarrowPhaseReduceList"))
				{
					float num = radius * radius;
					for (int num2 = results.Count - 1; num2 >= 0; num2--)
					{
						T val = results[num2];
						if (((onlyConsiderCenter ? val.transform.position : val.WorldSpaceBounds().ClosestPoint(position)) - position).sqrMagnitude > num)
						{
							results.RemoveAt(num2);
						}
					}
				}
			}

			private static bool IsEntityInRadius<T>(Vector3 position, float radiusSq, T entity) where T : BaseEntity
			{
				using (TimeWarning.New("IsEntityInRadius"))
				{
					return (entity.WorldSpaceBounds().ClosestPoint(position) - position).sqrMagnitude < radiusSq;
				}
			}
		}

		public static EntityTree Server;
	}

	public class RPC_Shared : Attribute
	{
	}

	public struct RPCMessage
	{
		public Connection connection;

		public BasePlayer player;

		public NetRead read;
	}

	public class RPC_Server : RPC_Shared
	{
		public abstract class Conditional : Attribute
		{
			public virtual string GetArgs()
			{
				return null;
			}
		}

		public class MaxDistance : Conditional
		{
			private float maximumDistance;

			public bool CheckParent { get; set; }

			public MaxDistance(float maxDist)
			{
				maximumDistance = maxDist;
			}

			public override string GetArgs()
			{
				return maximumDistance.ToString("0.00f") + (CheckParent ? ", true" : "");
			}

			public static bool Test(uint id, string debugName, BaseEntity ent, BasePlayer player, float maximumDistance, bool checkParent = false)
			{
				if (ent == null || player == null)
				{
					return false;
				}
				bool flag = ent.Distance(player.eyes.position) <= maximumDistance;
				if (checkParent && !flag)
				{
					BaseEntity parentEntity = ent.GetParentEntity();
					flag = parentEntity != null && parentEntity.Distance(player.eyes.position) <= maximumDistance;
				}
				return flag;
			}
		}

		public class IsVisible : Conditional
		{
			private float maximumDistance;

			public IsVisible(float maxDist)
			{
				maximumDistance = maxDist;
			}

			public override string GetArgs()
			{
				return maximumDistance.ToString("0.00f");
			}

			public static bool Test(uint id, string debugName, BaseEntity ent, BasePlayer player, float maximumDistance)
			{
				if (ent == null || player == null)
				{
					return false;
				}
				if (GamePhysics.LineOfSight(player.eyes.center, player.eyes.position, 1218519041))
				{
					if (!ent.IsVisible(player.eyes.HeadRay(), 1218519041, maximumDistance))
					{
						return ent.IsVisible(player.eyes.position, maximumDistance);
					}
					return true;
				}
				return false;
			}
		}

		public class FromOwner : Conditional
		{
			public static bool Test(uint id, string debugName, BaseEntity ent, BasePlayer player)
			{
				if (ent == null || player == null)
				{
					return false;
				}
				if (ent.net == null || player.net == null)
				{
					return false;
				}
				if (ent.net.ID == player.net.ID)
				{
					return true;
				}
				if (ent.parentEntity.uid != player.net.ID)
				{
					BaseEntity parentEntity = ent.GetParentEntity();
					if (parentEntity != null && parentEntity.parentEntity.uid == player.net.ID)
					{
						return true;
					}
					return false;
				}
				return true;
			}
		}

		public class IsActiveItem : Conditional
		{
			public static bool Test(uint id, string debugName, BaseEntity ent, BasePlayer player)
			{
				if (ent == null || player == null)
				{
					return false;
				}
				if (ent.net == null || player.net == null)
				{
					return false;
				}
				if (ent.net.ID == player.net.ID)
				{
					return true;
				}
				if (ent.parentEntity.uid != player.net.ID)
				{
					return false;
				}
				Item activeItem = player.GetActiveItem();
				if (activeItem == null)
				{
					return false;
				}
				if (activeItem.GetHeldEntity() != ent)
				{
					return false;
				}
				return true;
			}
		}

		public class CallsPerSecond : Conditional
		{
			private ulong callsPerSecond;

			public CallsPerSecond(ulong limit)
			{
				callsPerSecond = limit;
			}

			public override string GetArgs()
			{
				return callsPerSecond.ToString();
			}

			public static bool Test(uint id, string debugName, BaseEntity ent, BasePlayer player, ulong callsPerSecond)
			{
				if (ent == null || player == null)
				{
					return false;
				}
				return player.rpcHistory.TryIncrement(id, callsPerSecond);
			}
		}
	}

	public enum Signal
	{
		Attack,
		Alt_Attack,
		DryFire,
		Reload,
		Deploy,
		Flinch_Head,
		Flinch_Chest,
		Flinch_Stomach,
		Flinch_RearHead,
		Flinch_RearTorso,
		Throw,
		Relax,
		Gesture,
		PhysImpact,
		Eat,
		Startled,
		Admire
	}

	public enum Slot
	{
		Lock,
		FireMod,
		UpperModifier,
		MiddleModifier,
		LowerModifier,
		CenterDecoration,
		LowerCenterDecoration,
		StorageMonitor,
		Count
	}

	[Flags]
	public enum TraitFlag
	{
		None = 0,
		Alive = 1,
		Animal = 2,
		Human = 4,
		Interesting = 8,
		Food = 0x10,
		Meat = 0x20,
		Water = 0x20
	}

	public static class Util
	{
		public static BaseEntity[] FindTargets(string strFilter, bool onlyPlayers)
		{
			return (from x in BaseNetworkable.serverEntities.Where(delegate(BaseNetworkable x)
				{
					if (x is BasePlayer)
					{
						BasePlayer basePlayer = x as BasePlayer;
						if (string.IsNullOrEmpty(strFilter))
						{
							return true;
						}
						if (strFilter == "!alive" && basePlayer.IsAlive())
						{
							return true;
						}
						if (strFilter == "!sleeping" && basePlayer.IsSleeping())
						{
							return true;
						}
						if (strFilter[0] != '!' && !basePlayer.displayName.Contains(strFilter, CompareOptions.IgnoreCase) && !basePlayer.UserIDString.Contains(strFilter))
						{
							return false;
						}
						return true;
					}
					if (onlyPlayers)
					{
						return false;
					}
					if (string.IsNullOrEmpty(strFilter))
					{
						return false;
					}
					return x.ShortPrefabName.Contains(strFilter) ? true : false;
				})
				select x as BaseEntity).ToArray();
		}

		public static BaseEntity[] FindTargetsOwnedBy(ulong ownedBy, string strFilter)
		{
			bool hasFilter = !string.IsNullOrEmpty(strFilter);
			return (from x in BaseNetworkable.serverEntities.Where(delegate(BaseNetworkable x)
				{
					if (x is BaseEntity baseEntity)
					{
						if (baseEntity.OwnerID != ownedBy)
						{
							return false;
						}
						if (!hasFilter || baseEntity.ShortPrefabName.Contains(strFilter))
						{
							return true;
						}
					}
					return false;
				})
				select x as BaseEntity).ToArray();
		}

		public static BaseEntity[] FindTargetsAuthedTo(ulong authId, string strFilter)
		{
			bool hasFilter = !string.IsNullOrEmpty(strFilter);
			return (from x in BaseNetworkable.serverEntities.Where(delegate(BaseNetworkable x)
				{
					if (x is BuildingPrivlidge buildingPrivlidge)
					{
						if (!buildingPrivlidge.IsAuthed(authId))
						{
							return false;
						}
						if (!hasFilter || x.ShortPrefabName.Contains(strFilter))
						{
							return true;
						}
					}
					else if (x is AutoTurret autoTurret)
					{
						if (!autoTurret.IsAuthed(authId))
						{
							return false;
						}
						if (!hasFilter || x.ShortPrefabName.Contains(strFilter))
						{
							return true;
						}
					}
					else if (x is CodeLock codeLock)
					{
						if (!codeLock.whitelistPlayers.Contains(authId))
						{
							return false;
						}
						if (!hasFilter || x.ShortPrefabName.Contains(strFilter))
						{
							return true;
						}
					}
					return false;
				})
				select x as BaseEntity).ToArray();
		}

		public static T[] FindAll<T>() where T : BaseEntity
		{
			return BaseNetworkable.serverEntities.OfType<T>().ToArray();
		}
	}

	[Header("BaseEntity")]
	public Bounds bounds;

	public GameObjectRef impactEffect;

	public bool enableSaving = true;

	public bool syncPosition;

	public Model model;

	public Flags flags;

	[NonSerialized]
	public uint parentBone;

	[NonSerialized]
	public ulong skinID;

	private List<EntityComponentBase> _components;

	[HideInInspector]
	public bool HasBrain;

	private float nextHeightCheckTime;

	private bool cachedUnderground;

	[NonSerialized]
	protected string _name;

	private static Queue<BaseEntity> globalBroadcastQueue = new Queue<BaseEntity>();

	private static uint globalBroadcastProtocol = 0u;

	private uint broadcastProtocol;

	private List<EntityLink> links = new List<EntityLink>();

	private bool linkedToNeighbours;

	private TimeUntil _transferProtectionRemaining;

	private Action _disableTransferProtectionAction;

	public const string RpcClientDeprecationNotice = "Use ClientRPC( RpcTarget ) overloads";

	private Spawnable _spawnable;

	public static HashSet<BaseEntity> saveList = new HashSet<BaseEntity>();

	[NonSerialized]
	public BaseEntity creatorEntity;

	private bool couldSaveOriginally;

	private int ticksSinceStopped;

	private bool isCallingUpdateNetworkGroup;

	private EntityRef[] entitySlots = new EntityRef[8];

	protected List<TriggerBase> triggers;

	protected bool isVisible = true;

	protected bool isAnimatorVisible = true;

	protected bool isShadowVisible = true;

	protected OccludeeSphere localOccludee = new OccludeeSphere(-1);

	public virtual float RealisticMass => 100f;

	public List<EntityComponentBase> Components
	{
		get
		{
			if (_components == null)
			{
				_components = new List<EntityComponentBase>();
				GetComponentsInChildren(includeInactive: true, _components);
			}
			return _components;
		}
	}

	public virtual bool IsNpc => false;

	public ulong OwnerID { get; set; }

	protected float TransferProtectionRemaining => _transferProtectionRemaining;

	protected Action DisableTransferProtectionAction => _disableTransferProtectionAction ?? (_disableTransferProtectionAction = DisableTransferProtection);

	public virtual bool ShouldTransferAssociatedFiles => false;

	protected virtual float PositionTickRate => 0.1f;

	protected virtual bool PositionTickFixedTime => false;

	public virtual Vector3 ServerPosition
	{
		get
		{
			return base.transform.localPosition;
		}
		set
		{
			if (!(base.transform.localPosition == value))
			{
				base.transform.localPosition = value;
				base.transform.hasChanged = true;
			}
		}
	}

	public virtual Quaternion ServerRotation
	{
		get
		{
			return base.transform.localRotation;
		}
		set
		{
			if (!(base.transform.localRotation == value))
			{
				base.transform.localRotation = value;
				base.transform.hasChanged = true;
			}
		}
	}

	public float radiationLevel
	{
		get
		{
			if (triggers == null)
			{
				return 0f;
			}
			float num = 0f;
			for (int i = 0; i < triggers.Count; i++)
			{
				TriggerRadiation triggerRadiation = triggers[i] as TriggerRadiation;
				if (!(triggerRadiation == null))
				{
					Vector3 position = GetNetworkPosition();
					BaseEntity baseEntity = GetParentEntity();
					if (baseEntity != null)
					{
						position = baseEntity.transform.TransformPoint(position);
					}
					num = Mathf.Max(num, triggerRadiation.GetRadiation(position, RadiationProtection()));
				}
			}
			return num;
		}
	}

	public float currentTemperature
	{
		get
		{
			float num = Climate.GetTemperature(base.transform.position);
			if (triggers == null)
			{
				return num;
			}
			for (int i = 0; i < triggers.Count; i++)
			{
				TriggerTemperature triggerTemperature = triggers[i] as TriggerTemperature;
				if (!(triggerTemperature == null))
				{
					num = triggerTemperature.WorkoutTemperature(base.transform.position, num);
				}
			}
			return num;
		}
	}

	public float currentEnvironmentalWetness
	{
		get
		{
			if (triggers == null)
			{
				return 0f;
			}
			float num = 0f;
			Vector3 networkPosition = GetNetworkPosition();
			foreach (TriggerBase trigger in triggers)
			{
				if (trigger is TriggerWetness triggerWetness)
				{
					num += triggerWetness.WorkoutWetness(networkPosition);
				}
			}
			return Mathf.Clamp01(num);
		}
	}

	public virtual TraitFlag Traits => TraitFlag.None;

	public float Weight { get; protected set; }

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("BaseEntity.OnRpcMessage"))
		{
			if (rpc == 1552640099 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - BroadcastSignalFromClient ");
				}
				using (TimeWarning.New("BroadcastSignalFromClient"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.FromOwner.Test(1552640099u, "BroadcastSignalFromClient", this, player))
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
							BroadcastSignalFromClient(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in BroadcastSignalFromClient");
					}
				}
				return true;
			}
			if (rpc == 3645147041u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SV_RequestFile ");
				}
				using (TimeWarning.New("SV_RequestFile"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg3 = rPCMessage;
							SV_RequestFile(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in SV_RequestFile");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public virtual void OnCollision(Collision collision, BaseEntity hitEntity)
	{
		throw new NotImplementedException();
	}

	protected void ReceiveCollisionMessages(bool b)
	{
		if (b)
		{
			base.gameObject.transform.GetOrAddComponent<EntityCollisionMessage>();
		}
		else
		{
			base.gameObject.transform.RemoveComponent<EntityCollisionMessage>();
		}
	}

	public virtual BasePlayer ToPlayer()
	{
		return null;
	}

	public override void InitShared()
	{
		base.InitShared();
		InitEntityLinks();
		if (Components == null)
		{
			return;
		}
		for (int i = 0; i < Components.Count; i++)
		{
			if (!(Components[i] == null))
			{
				Components[i].InitShared();
			}
		}
	}

	public override void DestroyShared()
	{
		base.DestroyShared();
		FreeEntityLinks();
		if (Components == null)
		{
			return;
		}
		for (int i = 0; i < Components.Count; i++)
		{
			if (!(Components[i] == null))
			{
				Components[i].DestroyShared();
			}
		}
	}

	public override void ResetState()
	{
		base.ResetState();
		parentBone = 0u;
		OwnerID = 0uL;
		flags = (Flags)0;
		parentEntity = default(EntityRef);
		if (base.isServer)
		{
			_spawnable = null;
		}
		if (Components == null)
		{
			return;
		}
		for (int i = 0; i < Components.Count; i++)
		{
			if (!(Components[i] == null))
			{
				Components[i].ResetState();
			}
		}
	}

	public virtual float InheritedVelocityScale()
	{
		return 0f;
	}

	public virtual bool InheritedVelocityDirection()
	{
		return true;
	}

	public virtual Vector3 GetInheritedProjectileVelocity(Vector3 direction)
	{
		BaseEntity baseEntity = parentEntity.Get(base.isServer);
		if (baseEntity == null)
		{
			return Vector3.zero;
		}
		if (baseEntity.InheritedVelocityDirection())
		{
			return GetParentVelocity() * baseEntity.InheritedVelocityScale();
		}
		return Mathf.Max(Vector3.Dot(GetParentVelocity() * baseEntity.InheritedVelocityScale(), direction), 0f) * direction;
	}

	public virtual Vector3 GetInheritedThrowVelocity(Vector3 direction)
	{
		return GetParentVelocity();
	}

	public virtual Vector3 GetInheritedDropVelocity()
	{
		BaseEntity baseEntity = parentEntity.Get(base.isServer);
		if (!(baseEntity != null))
		{
			return Vector3.zero;
		}
		return baseEntity.GetWorldVelocity();
	}

	public Vector3 GetParentVelocity()
	{
		BaseEntity baseEntity = parentEntity.Get(base.isServer);
		if (!(baseEntity != null))
		{
			return Vector3.zero;
		}
		return baseEntity.GetWorldVelocity() + (baseEntity.GetAngularVelocity() * base.transform.localPosition - base.transform.localPosition);
	}

	public Vector3 GetWorldVelocity()
	{
		BaseEntity baseEntity = parentEntity.Get(base.isServer);
		if (!(baseEntity != null))
		{
			return GetLocalVelocity();
		}
		return baseEntity.GetWorldVelocity() + (baseEntity.GetAngularVelocity() * base.transform.localPosition - base.transform.localPosition) + baseEntity.transform.TransformDirection(GetLocalVelocity());
	}

	public Vector3 GetLocalVelocity()
	{
		if (base.isServer)
		{
			return GetLocalVelocityServer();
		}
		return Vector3.zero;
	}

	public Quaternion GetAngularVelocity()
	{
		if (base.isServer)
		{
			return GetAngularVelocityServer();
		}
		return Quaternion.identity;
	}

	public virtual OBB WorldSpaceBounds()
	{
		return new OBB(base.transform.position, base.transform.lossyScale, base.transform.rotation, bounds);
	}

	public Vector3 PivotPoint()
	{
		return base.transform.position;
	}

	public Vector3 CenterPoint()
	{
		return WorldSpaceBounds().position;
	}

	public Vector3 ClosestPoint(Vector3 position)
	{
		return WorldSpaceBounds().ClosestPoint(position);
	}

	public virtual Vector3 TriggerPoint()
	{
		return CenterPoint();
	}

	public float Distance(Vector3 position)
	{
		return (ClosestPoint(position) - position).magnitude;
	}

	public float SqrDistance(Vector3 position)
	{
		return (ClosestPoint(position) - position).sqrMagnitude;
	}

	public float Distance(BaseEntity other)
	{
		return Distance(other.transform.position);
	}

	public float SqrDistance(BaseEntity other)
	{
		return SqrDistance(other.transform.position);
	}

	public float Distance2D(Vector3 position)
	{
		return (ClosestPoint(position) - position).Magnitude2D();
	}

	public float SqrDistance2D(Vector3 position)
	{
		return (ClosestPoint(position) - position).SqrMagnitude2D();
	}

	public float Distance2D(BaseEntity other)
	{
		return Distance(other.transform.position);
	}

	public float SqrDistance2D(BaseEntity other)
	{
		return SqrDistance(other.transform.position);
	}

	public bool IsVisible(Ray ray, int layerMask, float maxDistance)
	{
		if (ray.origin.IsNaNOrInfinity())
		{
			return false;
		}
		if (ray.direction.IsNaNOrInfinity())
		{
			return false;
		}
		if (ray.direction == Vector3.zero)
		{
			return false;
		}
		if (!WorldSpaceBounds().Trace(ray, out var hit, maxDistance))
		{
			return false;
		}
		if (GamePhysics.Trace(ray, 0f, out var hitInfo, maxDistance, layerMask))
		{
			BaseEntity entity = hitInfo.GetEntity();
			if (entity == this)
			{
				return true;
			}
			if (entity != null && (bool)GetParentEntity() && GetParentEntity().EqualNetID(entity) && hitInfo.IsOnLayer(Rust.Layer.Vehicle_Detailed))
			{
				return true;
			}
			if (hitInfo.distance <= hit.distance)
			{
				return false;
			}
		}
		return true;
	}

	public bool IsVisibleSpecificLayers(Vector3 position, Vector3 target, int layerMask, float maxDistance = float.PositiveInfinity)
	{
		Vector3 vector = target - position;
		float magnitude = vector.magnitude;
		if (magnitude < Mathf.Epsilon)
		{
			return true;
		}
		Vector3 vector2 = vector / magnitude;
		Vector3 vector3 = vector2 * Mathf.Min(magnitude, 0.01f);
		return IsVisible(new Ray(position + vector3, vector2), layerMask, maxDistance);
	}

	public bool IsVisible(Vector3 position, Vector3 target, float maxDistance = float.PositiveInfinity)
	{
		Vector3 vector = target - position;
		float magnitude = vector.magnitude;
		if (magnitude < Mathf.Epsilon)
		{
			return true;
		}
		Vector3 vector2 = vector / magnitude;
		Vector3 vector3 = vector2 * Mathf.Min(magnitude, 0.01f);
		maxDistance = Mathf.Min(maxDistance, magnitude + 0.2f);
		return IsVisible(new Ray(position + vector3, vector2), 1218519041, maxDistance);
	}

	public bool IsVisible(Vector3 position, float maxDistance = float.PositiveInfinity)
	{
		Vector3 target = CenterPoint();
		if (IsVisible(position, target, maxDistance))
		{
			return true;
		}
		Vector3 target2 = ClosestPoint(position);
		if (IsVisible(position, target2, maxDistance))
		{
			return true;
		}
		return false;
	}

	public bool IsVisibleAndCanSee(Vector3 position)
	{
		Vector3 vector = CenterPoint();
		if (IsVisible(position, vector) && CanSee(vector, position))
		{
			return true;
		}
		Vector3 vector2 = ClosestPoint(position);
		if (IsVisible(position, vector2) && CanSee(vector2, position))
		{
			return true;
		}
		return false;
	}

	public bool IsVisibleAndCanSeeLegacy(Vector3 position, float maxDistance = float.PositiveInfinity)
	{
		Vector3 vector = CenterPoint();
		if (IsVisible(position, vector, maxDistance) && IsVisible(vector, position, maxDistance))
		{
			return true;
		}
		Vector3 vector2 = ClosestPoint(position);
		if (IsVisible(position, vector2, maxDistance) && IsVisible(vector2, position, maxDistance))
		{
			return true;
		}
		return false;
	}

	public bool CanSee(Vector3 fromPos, Vector3 targetPos)
	{
		return GamePhysics.LineOfSight(fromPos, targetPos, 1218519041, this);
	}

	public bool IsOlderThan(BaseEntity other)
	{
		if (other == null)
		{
			return true;
		}
		NetworkableId obj = net?.ID ?? default(NetworkableId);
		NetworkableId networkableId = other.net?.ID ?? default(NetworkableId);
		return obj.Value < networkableId.Value;
	}

	public virtual bool IsOutside()
	{
		return IsOutside(WorldSpaceBounds().position);
	}

	public bool IsOutside(Vector3 position)
	{
		bool result = true;
		Vector3 vector = position + Vector3.up * 100f;
		vector.y = Mathf.Max(vector.y, TerrainMeta.HeightMap.GetHeight(vector) + 1f);
		if (UnityEngine.Physics.Linecast(vector, position, out var hitInfo, 161546513, QueryTriggerInteraction.Ignore))
		{
			BaseEntity baseEntity = hitInfo.collider.ToBaseEntity();
			if (baseEntity == null || !baseEntity.HasEntityInParents(this))
			{
				result = false;
			}
		}
		return result;
	}

	public bool IsUnderground(bool cached = true)
	{
		if (!cached || UnityEngine.Time.realtimeSinceStartup > nextHeightCheckTime)
		{
			cachedUnderground = EnvironmentManager.Check(base.transform.position, EnvironmentType.Underground);
			nextHeightCheckTime = UnityEngine.Time.realtimeSinceStartup + 5f;
		}
		return cachedUnderground;
	}

	public virtual float WaterFactor()
	{
		return WaterLevel.Factor(WorldSpaceBounds().ToBounds(), waves: true, volumes: true, this);
	}

	public virtual float AirFactor()
	{
		if (!(WaterFactor() > 0.85f))
		{
			return 1f;
		}
		return 0f;
	}

	public bool WaterTestFromVolumes(Vector3 pos, out WaterLevel.WaterInfo info)
	{
		if (triggers == null)
		{
			info = default(WaterLevel.WaterInfo);
			return false;
		}
		for (int i = 0; i < triggers.Count; i++)
		{
			if (triggers[i] is WaterVolume waterVolume && waterVolume.Test(pos, out info))
			{
				return true;
			}
		}
		info = default(WaterLevel.WaterInfo);
		return false;
	}

	public bool IsInWaterVolume(Vector3 pos, out bool natural)
	{
		natural = false;
		if (triggers == null)
		{
			return false;
		}
		for (int i = 0; i < triggers.Count; i++)
		{
			if (triggers[i] is WaterVolume waterVolume && waterVolume.Test(pos, out var _))
			{
				natural = waterVolume.naturalSource;
				return true;
			}
		}
		return false;
	}

	public bool WaterTestFromVolumes(Bounds bounds, out WaterLevel.WaterInfo info)
	{
		if (triggers == null)
		{
			info = default(WaterLevel.WaterInfo);
			return false;
		}
		for (int i = 0; i < triggers.Count; i++)
		{
			if (triggers[i] is WaterVolume waterVolume && waterVolume.Test(bounds, out info))
			{
				return true;
			}
		}
		info = default(WaterLevel.WaterInfo);
		return false;
	}

	public bool WaterTestFromVolumes(Vector3 start, Vector3 end, float radius, out WaterLevel.WaterInfo info)
	{
		if (triggers == null)
		{
			info = default(WaterLevel.WaterInfo);
			return false;
		}
		for (int i = 0; i < triggers.Count; i++)
		{
			if (triggers[i] is WaterVolume waterVolume && waterVolume.Test(start, end, radius, out info))
			{
				return true;
			}
		}
		info = default(WaterLevel.WaterInfo);
		return false;
	}

	public virtual bool BlocksWaterFor(BasePlayer player)
	{
		return false;
	}

	public virtual float Health()
	{
		return 0f;
	}

	public virtual float MaxHealth()
	{
		return 0f;
	}

	public virtual float MaxVelocity()
	{
		return 0f;
	}

	public virtual float BoundsPadding()
	{
		return 0.1f;
	}

	public virtual float PenetrationResistance(HitInfo info)
	{
		return 100f;
	}

	public virtual GameObjectRef GetImpactEffect(HitInfo info)
	{
		return impactEffect;
	}

	public virtual void OnAttacked(HitInfo info)
	{
	}

	public virtual Item GetItem()
	{
		return null;
	}

	public virtual Item GetItem(ItemId itemId)
	{
		return null;
	}

	public virtual void GiveItem(Item item, GiveItemReason reason = GiveItemReason.Generic)
	{
		item.Remove();
	}

	public virtual bool CanBeLooted(BasePlayer player)
	{
		return !IsTransferring();
	}

	public virtual BaseEntity GetEntity()
	{
		return this;
	}

	public override string ToString()
	{
		if (_name == null)
		{
			if (base.isServer)
			{
				if (net == null)
				{
					return base.ShortPrefabName;
				}
				_name = $"{base.ShortPrefabName}[{net.ID}]";
			}
			else
			{
				_name = base.ShortPrefabName;
			}
		}
		return _name;
	}

	public virtual string Categorize()
	{
		return "entity";
	}

	public void Log(string str)
	{
		if (base.isClient)
		{
			Debug.Log("<color=#ffa>[" + ToString() + "] " + str + "</color>", base.gameObject);
		}
		else
		{
			Debug.Log("<color=#aff>[" + ToString() + "] " + str + "</color>", base.gameObject);
		}
	}

	public void SetModel(Model mdl)
	{
		if (!(model == mdl))
		{
			model = mdl;
		}
	}

	public Model GetModel()
	{
		return model;
	}

	public virtual Transform[] GetBones()
	{
		if ((bool)model)
		{
			return model.GetBones();
		}
		return null;
	}

	public virtual Transform FindBone(string strName)
	{
		if ((bool)model)
		{
			return model.FindBone(strName);
		}
		return base.transform;
	}

	public virtual uint FindBoneID(Transform boneTransform)
	{
		if ((bool)model)
		{
			return model.FindBoneID(boneTransform);
		}
		return StringPool.closest;
	}

	public virtual Transform FindClosestBone(Vector3 worldPos)
	{
		if ((bool)model)
		{
			return model.FindClosestBone(worldPos);
		}
		return base.transform;
	}

	public virtual bool ShouldBlockProjectiles()
	{
		return true;
	}

	public virtual bool ShouldInheritNetworkGroup()
	{
		return true;
	}

	public virtual bool SupportsChildDeployables()
	{
		return false;
	}

	public virtual bool ForceDeployableSetParent()
	{
		return false;
	}

	public virtual bool ShouldUseCastNoClipChecks()
	{
		return GetWorldVelocity().magnitude > 0f;
	}

	public bool IsOnMovingObject()
	{
		if (syncPosition)
		{
			return true;
		}
		BaseEntity baseEntity = GetParentEntity();
		if (!(baseEntity != null))
		{
			return false;
		}
		return baseEntity.IsOnMovingObject();
	}

	public void BroadcastEntityMessage(string msg, float radius = 20f, int layerMask = 1218652417)
	{
		if (base.isClient)
		{
			return;
		}
		List<BaseEntity> obj = Facepunch.Pool.Get<List<BaseEntity>>();
		Vis.Entities(base.transform.position, radius, obj, layerMask);
		foreach (BaseEntity item in obj)
		{
			if (item.isServer)
			{
				item.OnEntityMessage(this, msg);
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	public virtual void OnEntityMessage(BaseEntity from, string msg)
	{
	}

	public T AddComponent<T>() where T : EntityComponentBase
	{
		T val = base.gameObject.AddComponent<T>();
		_components.Add(val);
		return val;
	}

	public virtual void DebugServer(int rep, float time)
	{
		DebugText(base.transform.position + Vector3.up * 1f, $"{net?.ID.Value ?? 0}: {base.name}\n{DebugText()}", Color.white, time);
	}

	public virtual string DebugText()
	{
		return "";
	}

	public void OnDebugStart()
	{
		EntityDebug entityDebug = base.gameObject.GetComponent<EntityDebug>();
		if (entityDebug == null)
		{
			entityDebug = base.gameObject.AddComponent<EntityDebug>();
		}
		entityDebug.enabled = true;
	}

	protected void DebugText(Vector3 pos, string str, Color color, float time)
	{
		if (base.isServer)
		{
			ConsoleNetwork.BroadcastToAllClients("ddraw.text", time, color, pos, str);
		}
	}

	public bool HasFlag(Flags f)
	{
		return (flags & f) == f;
	}

	public bool HasAny(Flags f)
	{
		return (flags & f) > (Flags)0;
	}

	public bool ParentHasFlag(Flags f)
	{
		BaseEntity baseEntity = GetParentEntity();
		if (baseEntity == null)
		{
			return false;
		}
		return baseEntity.HasFlag(f);
	}

	public void SetFlag(Flags f, bool b, bool recursive = false, bool networkupdate = true)
	{
		Flags flags = this.flags;
		if (b)
		{
			if (HasFlag(f))
			{
				return;
			}
			this.flags |= f;
		}
		else
		{
			if (!HasFlag(f))
			{
				return;
			}
			this.flags &= ~f;
		}
		OnFlagsChanged(flags, this.flags);
		if (networkupdate)
		{
			SendNetworkUpdate();
			if (flags != this.flags)
			{
				GlobalNetworkHandler.server?.TrySendNetworkUpdate(this);
			}
		}
		else
		{
			InvalidateNetworkCache();
		}
		if (recursive && children != null)
		{
			for (int i = 0; i < children.Count; i++)
			{
				children[i].SetFlag(f, b, recursive: true);
			}
		}
	}

	public bool IsOn()
	{
		return HasFlag(Flags.On);
	}

	public bool IsOpen()
	{
		return HasFlag(Flags.Open);
	}

	public bool IsOnFire()
	{
		return HasFlag(Flags.OnFire);
	}

	public bool IsLocked()
	{
		return HasFlag(Flags.Locked);
	}

	public override bool IsDebugging()
	{
		return HasFlag(Flags.Debugging);
	}

	public bool IsDisabled()
	{
		if (!HasFlag(Flags.Disabled))
		{
			return ParentHasFlag(Flags.Disabled);
		}
		return true;
	}

	public bool IsBroken()
	{
		return HasFlag(Flags.Broken);
	}

	public bool IsBusy()
	{
		return HasFlag(Flags.Busy);
	}

	public bool IsTransferProtected()
	{
		return HasFlag(Flags.Protected);
	}

	public bool IsTransferring()
	{
		return HasFlag(Flags.Transferring);
	}

	public override string GetLogColor()
	{
		if (base.isServer)
		{
			return "cyan";
		}
		return "yellow";
	}

	public virtual void OnFlagsChanged(Flags old, Flags next)
	{
		if (IsDebugging() && (old & Flags.Debugging) != (next & Flags.Debugging))
		{
			OnDebugStart();
		}
		if (base.isServer)
		{
			if (next.HasFlag(Flags.OnFire) && !old.HasFlag(Flags.OnFire))
			{
				SingletonComponent<NpcFireManager>.Instance.Add(this);
			}
			else if (!next.HasFlag(Flags.OnFire) && old.HasFlag(Flags.OnFire))
			{
				SingletonComponent<NpcFireManager>.Instance.Remove(this);
			}
		}
	}

	protected void SendNetworkUpdate_Flags()
	{
		if (Rust.Application.isLoading || Rust.Application.isLoadingSave || base.IsDestroyed || net == null || !isSpawned)
		{
			return;
		}
		using (TimeWarning.New("SendNetworkUpdate_Flags"))
		{
			LogEntry(RustLog.EntryType.Network, 3, "SendNetworkUpdate_Flags");
			List<Connection> subscribers = GetSubscribers();
			if (subscribers != null && subscribers.Count > 0)
			{
				NetWrite netWrite = Network.Net.sv.StartWrite();
				netWrite.PacketID(Message.Type.EntityFlags);
				netWrite.EntityID(net.ID);
				netWrite.Int32((int)flags);
				SendInfo info = new SendInfo(subscribers);
				netWrite.Send(info);
			}
			base.gameObject.SendOnSendNetworkUpdate(this);
		}
	}

	public virtual bool IsOccupied(Socket_Base socket)
	{
		return FindLink(socket)?.IsOccupied() ?? false;
	}

	public bool IsOccupied(string socketName)
	{
		return FindLink(socketName)?.IsOccupied() ?? false;
	}

	public EntityLink FindLink(Socket_Base socket)
	{
		List<EntityLink> entityLinks = GetEntityLinks();
		for (int i = 0; i < entityLinks.Count; i++)
		{
			if (entityLinks[i].socket == socket)
			{
				return entityLinks[i];
			}
		}
		return null;
	}

	public EntityLink FindLink(string socketName)
	{
		List<EntityLink> entityLinks = GetEntityLinks();
		for (int i = 0; i < entityLinks.Count; i++)
		{
			if (entityLinks[i].socket.socketName == socketName)
			{
				return entityLinks[i];
			}
		}
		return null;
	}

	public EntityLink FindLink(string[] socketNames)
	{
		List<EntityLink> entityLinks = GetEntityLinks();
		for (int i = 0; i < entityLinks.Count; i++)
		{
			for (int j = 0; j < socketNames.Length; j++)
			{
				if (entityLinks[i].socket.socketName == socketNames[j])
				{
					return entityLinks[i];
				}
			}
		}
		return null;
	}

	public T FindLinkedEntity<T>() where T : BaseEntity
	{
		List<EntityLink> entityLinks = GetEntityLinks();
		for (int i = 0; i < entityLinks.Count; i++)
		{
			EntityLink entityLink = entityLinks[i];
			for (int j = 0; j < entityLink.connections.Count; j++)
			{
				EntityLink entityLink2 = entityLink.connections[j];
				if (entityLink2.owner is T)
				{
					return entityLink2.owner as T;
				}
			}
		}
		return null;
	}

	public void EntityLinkMessage<T>(Action<T> action) where T : BaseEntity
	{
		List<EntityLink> entityLinks = GetEntityLinks();
		for (int i = 0; i < entityLinks.Count; i++)
		{
			EntityLink entityLink = entityLinks[i];
			for (int j = 0; j < entityLink.connections.Count; j++)
			{
				EntityLink entityLink2 = entityLink.connections[j];
				if (entityLink2.owner is T)
				{
					action(entityLink2.owner as T);
				}
			}
		}
	}

	public void EntityLinkBroadcast<T, S>(Action<T> action, Func<S, bool> canTraverseSocket) where T : BaseEntity where S : Socket_Base
	{
		globalBroadcastProtocol++;
		globalBroadcastQueue.Clear();
		broadcastProtocol = globalBroadcastProtocol;
		globalBroadcastQueue.Enqueue(this);
		if (this is T)
		{
			action(this as T);
		}
		while (globalBroadcastQueue.Count > 0)
		{
			List<EntityLink> entityLinks = globalBroadcastQueue.Dequeue().GetEntityLinks();
			for (int i = 0; i < entityLinks.Count; i++)
			{
				EntityLink entityLink = entityLinks[i];
				if (!(entityLink.socket is S) || !canTraverseSocket(entityLink.socket as S))
				{
					continue;
				}
				for (int j = 0; j < entityLink.connections.Count; j++)
				{
					BaseEntity owner = entityLink.connections[j].owner;
					if (owner.broadcastProtocol != globalBroadcastProtocol)
					{
						owner.broadcastProtocol = globalBroadcastProtocol;
						globalBroadcastQueue.Enqueue(owner);
						if (owner is T)
						{
							action(owner as T);
						}
					}
				}
			}
		}
	}

	public void EntityLinkBroadcast<T>(Action<T> action) where T : BaseEntity
	{
		globalBroadcastProtocol++;
		globalBroadcastQueue.Clear();
		broadcastProtocol = globalBroadcastProtocol;
		globalBroadcastQueue.Enqueue(this);
		if (this is T)
		{
			action(this as T);
		}
		while (globalBroadcastQueue.Count > 0)
		{
			List<EntityLink> entityLinks = globalBroadcastQueue.Dequeue().GetEntityLinks();
			for (int i = 0; i < entityLinks.Count; i++)
			{
				EntityLink entityLink = entityLinks[i];
				for (int j = 0; j < entityLink.connections.Count; j++)
				{
					BaseEntity owner = entityLink.connections[j].owner;
					if (owner.broadcastProtocol != globalBroadcastProtocol)
					{
						owner.broadcastProtocol = globalBroadcastProtocol;
						globalBroadcastQueue.Enqueue(owner);
						if (owner is T)
						{
							action(owner as T);
						}
					}
				}
			}
		}
	}

	public void EntityLinkBroadcast()
	{
		globalBroadcastProtocol++;
		globalBroadcastQueue.Clear();
		broadcastProtocol = globalBroadcastProtocol;
		globalBroadcastQueue.Enqueue(this);
		while (globalBroadcastQueue.Count > 0)
		{
			List<EntityLink> entityLinks = globalBroadcastQueue.Dequeue().GetEntityLinks();
			for (int i = 0; i < entityLinks.Count; i++)
			{
				EntityLink entityLink = entityLinks[i];
				for (int j = 0; j < entityLink.connections.Count; j++)
				{
					BaseEntity owner = entityLink.connections[j].owner;
					if (owner.broadcastProtocol != globalBroadcastProtocol)
					{
						owner.broadcastProtocol = globalBroadcastProtocol;
						globalBroadcastQueue.Enqueue(owner);
					}
				}
			}
		}
	}

	public bool ReceivedEntityLinkBroadcast()
	{
		return broadcastProtocol == globalBroadcastProtocol;
	}

	public List<EntityLink> GetEntityLinks(bool linkToNeighbours = true)
	{
		if (Rust.Application.isLoadingSave)
		{
			return links;
		}
		if (!linkedToNeighbours && linkToNeighbours)
		{
			LinkToNeighbours();
		}
		return links;
	}

	private void LinkToEntity(BaseEntity other)
	{
		if (this == other || links.Count == 0 || other.links.Count == 0)
		{
			return;
		}
		using (TimeWarning.New("LinkToEntity"))
		{
			for (int i = 0; i < links.Count; i++)
			{
				EntityLink entityLink = links[i];
				for (int j = 0; j < other.links.Count; j++)
				{
					EntityLink entityLink2 = other.links[j];
					if (entityLink.CanConnect(entityLink2))
					{
						if (!entityLink.Contains(entityLink2))
						{
							entityLink.Add(entityLink2);
						}
						if (!entityLink2.Contains(entityLink))
						{
							entityLink2.Add(entityLink);
						}
					}
				}
			}
		}
	}

	private void LinkToNeighbours()
	{
		if (links.Count == 0)
		{
			return;
		}
		linkedToNeighbours = true;
		using (TimeWarning.New("LinkToNeighbours"))
		{
			List<BaseEntity> obj = Facepunch.Pool.Get<List<BaseEntity>>();
			OBB oBB = WorldSpaceBounds();
			Vis.Entities(oBB.position, oBB.extents.magnitude + 1f, obj);
			for (int i = 0; i < obj.Count; i++)
			{
				BaseEntity baseEntity = obj[i];
				if (baseEntity.isServer == base.isServer)
				{
					LinkToEntity(baseEntity);
				}
			}
			Facepunch.Pool.FreeUnmanaged(ref obj);
		}
	}

	private void InitEntityLinks()
	{
		using (TimeWarning.New("InitEntityLinks"))
		{
			if (base.isServer)
			{
				links.AddLinks(this, PrefabAttribute.server.FindAll<Socket_Base>(prefabID));
			}
		}
	}

	private void FreeEntityLinks()
	{
		using (TimeWarning.New("FreeEntityLinks"))
		{
			links.FreeLinks();
			linkedToNeighbours = false;
		}
	}

	public void RefreshEntityLinks()
	{
		using (TimeWarning.New("RefreshEntityLinks"))
		{
			links.ClearLinks();
			LinkToNeighbours();
		}
	}

	[RPC_Server]
	public void SV_RequestFile(RPCMessage msg)
	{
		uint num = msg.read.UInt32();
		FileStorage.Type type = (FileStorage.Type)msg.read.UInt8();
		string funcName = StringPool.Get(msg.read.UInt32());
		uint num2 = ((msg.read.Unread > 0) ? msg.read.UInt32() : 0u);
		bool flag = msg.read.Unread > 0 && msg.read.Bit();
		byte[] array = FileStorage.server.Get(num, type, net.ID, num2);
		if (array == null)
		{
			if (!flag)
			{
				return;
			}
			array = Array.Empty<byte>();
		}
		SendInfo sendInfo = new SendInfo(msg.connection);
		sendInfo.channel = 2;
		sendInfo.method = SendMethod.Reliable;
		SendInfo sendInfo2 = sendInfo;
		ClientRPC(RpcTarget.SendInfo(funcName, sendInfo2), num, (uint)array.Length, array, num2, (byte)type);
	}

	public virtual void EnableTransferProtection()
	{
		if (!IsTransferProtected())
		{
			SetFlag(Flags.Protected, b: true);
			List<Connection> subscribers = GetSubscribers();
			if (subscribers != null)
			{
				List<Connection> obj = Facepunch.Pool.Get<List<Connection>>();
				foreach (Connection item in subscribers)
				{
					if (!ShouldNetworkTo(item.player as BasePlayer))
					{
						obj.Add(item);
					}
				}
				OnNetworkSubscribersLeave(obj);
				Facepunch.Pool.FreeUnmanaged(ref obj);
			}
			float protectionDuration = Nexus.protectionDuration;
			_transferProtectionRemaining = protectionDuration;
			Invoke(DisableTransferProtectionAction, protectionDuration);
		}
		foreach (BaseEntity child in children)
		{
			child.EnableTransferProtection();
		}
	}

	public virtual void DisableTransferProtection()
	{
		BaseEntity baseEntity = GetParentEntity();
		if (baseEntity != null && baseEntity.IsTransferProtected())
		{
			baseEntity.DisableTransferProtection();
		}
		if (IsTransferProtected())
		{
			SetFlag(Flags.Protected, b: false);
			List<Connection> subscribers = GetSubscribers();
			if (subscribers != null)
			{
				OnNetworkSubscribersEnter(subscribers);
			}
			_transferProtectionRemaining = 0f;
			CancelInvoke(DisableTransferProtectionAction);
		}
		foreach (BaseEntity child in children)
		{
			child.DisableTransferProtection();
		}
	}

	public void SetParent(BaseEntity entity, bool worldPositionStays = false, bool sendImmediate = false)
	{
		SetParent(entity, 0u, worldPositionStays, sendImmediate);
	}

	public void SetParent(BaseEntity entity, string strBone, bool worldPositionStays = false, bool sendImmediate = false)
	{
		SetParent(entity, (!string.IsNullOrEmpty(strBone)) ? StringPool.Get(strBone) : 0u, worldPositionStays, sendImmediate);
	}

	public bool HasChild(BaseEntity c)
	{
		if (c == this)
		{
			return true;
		}
		BaseEntity baseEntity = c.GetParentEntity();
		if (baseEntity != null)
		{
			return HasChild(baseEntity);
		}
		return false;
	}

	public void SetParent(BaseEntity entity, uint boneID, bool worldPositionStays = false, bool sendImmediate = false)
	{
		if (entity != null)
		{
			if (entity == this)
			{
				Debug.LogError("Trying to parent to self " + this, base.gameObject);
				return;
			}
			if (HasChild(entity))
			{
				Debug.LogError("Trying to parent to child " + this, base.gameObject);
				return;
			}
		}
		LogEntry(RustLog.EntryType.Hierarchy, 2, "SetParent {0} {1}", entity, boneID);
		BaseEntity baseEntity = GetParentEntity();
		if ((bool)baseEntity)
		{
			baseEntity.RemoveChild(this);
		}
		if (base.limitNetworking && baseEntity != null && baseEntity != entity)
		{
			BasePlayer basePlayer = baseEntity as BasePlayer;
			if (basePlayer.IsValid())
			{
				DestroyOnClient(basePlayer.net.connection);
			}
		}
		if (entity == null)
		{
			OnParentChanging(baseEntity, null);
			parentEntity.Set(null);
			base.transform.SetParent(null, worldPositionStays);
			parentBone = 0u;
			UpdateNetworkGroup();
			if (sendImmediate)
			{
				SendNetworkUpdateImmediate();
				SendChildrenNetworkUpdateImmediate();
			}
			else
			{
				SendNetworkUpdate();
				SendChildrenNetworkUpdate();
			}
			return;
		}
		Debug.Assert(entity.isServer, "SetParent - child should be a SERVER entity");
		Debug.Assert(entity.net != null, "Setting parent to entity that hasn't spawned yet! (net is null)");
		Debug.Assert(entity.net.ID.IsValid, "Setting parent to entity that hasn't spawned yet! (id = 0)");
		entity.AddChild(this);
		OnParentChanging(baseEntity, entity);
		parentEntity.Set(entity);
		if (boneID != 0 && boneID != StringPool.closest)
		{
			base.transform.SetParent(entity.FindBone(StringPool.Get(boneID)), worldPositionStays);
		}
		else
		{
			base.transform.SetParent(entity.transform, worldPositionStays);
		}
		parentBone = boneID;
		UpdateNetworkGroup();
		if (sendImmediate)
		{
			SendNetworkUpdateImmediate();
			SendChildrenNetworkUpdateImmediate();
		}
		else
		{
			SendNetworkUpdate();
			SendChildrenNetworkUpdate();
		}
	}

	public void DestroyOnClient(Connection connection)
	{
		if (children != null)
		{
			foreach (BaseEntity child in children)
			{
				child.DestroyOnClient(connection);
			}
		}
		if (Network.Net.sv.IsConnected())
		{
			NetWrite netWrite = Network.Net.sv.StartWrite();
			netWrite.PacketID(Message.Type.EntityDestroy);
			netWrite.EntityID(net.ID);
			netWrite.UInt8(0);
			netWrite.Send(new SendInfo(connection));
			LogEntry(RustLog.EntryType.Network, 2, "EntityDestroy");
		}
	}

	private void SendChildrenNetworkUpdate()
	{
		if (children == null)
		{
			return;
		}
		foreach (BaseEntity child in children)
		{
			child.UpdateNetworkGroup();
			child.SendNetworkUpdate();
		}
	}

	private void SendChildrenNetworkUpdateImmediate()
	{
		if (children == null)
		{
			return;
		}
		foreach (BaseEntity child in children)
		{
			child.UpdateNetworkGroup();
			child.SendNetworkUpdateImmediate();
		}
	}

	public virtual void SwitchParent(BaseEntity ent)
	{
		Log("SwitchParent Missed " + ent);
	}

	public virtual void OnParentChanging(BaseEntity oldParent, BaseEntity newParent)
	{
		Rigidbody component = GetComponent<Rigidbody>();
		if (!component || component.isKinematic)
		{
			return;
		}
		if (oldParent != null)
		{
			Rigidbody component2 = oldParent.GetComponent<Rigidbody>();
			if (component2 == null || component2.isKinematic)
			{
				component.velocity += oldParent.GetWorldVelocity();
			}
		}
		if (newParent != null)
		{
			Rigidbody component3 = newParent.GetComponent<Rigidbody>();
			if (component3 == null || component3.isKinematic)
			{
				component.velocity -= newParent.GetWorldVelocity();
			}
		}
	}

	public virtual EntityPrivilege GetEntityBuildingPrivilege()
	{
		return null;
	}

	public virtual BuildingPrivlidge GetBuildingPrivilege()
	{
		return GetNearestBuildingPrivledge();
	}

	public BuildingPrivlidge GetNearestBuildingPrivledge()
	{
		return GetBuildingPrivilege(WorldSpaceBounds());
	}

	public BuildingPrivlidge GetBuildingPrivilege(OBB obb)
	{
		BuildingBlock other = null;
		BuildingPrivlidge result = null;
		List<BuildingBlock> obj = Facepunch.Pool.Get<List<BuildingBlock>>();
		Vis.Entities(obb.position, 16f + obb.extents.magnitude, obj, 2097152);
		for (int i = 0; i < obj.Count; i++)
		{
			BuildingBlock buildingBlock = obj[i];
			if (buildingBlock.isServer != base.isServer || !buildingBlock.IsOlderThan(other) || obb.Distance(buildingBlock.WorldSpaceBounds()) > 16f)
			{
				continue;
			}
			BuildingManager.Building building = buildingBlock.GetBuilding();
			if (building != null)
			{
				BuildingPrivlidge dominatingBuildingPrivilege = building.GetDominatingBuildingPrivilege();
				if (!(dominatingBuildingPrivilege == null))
				{
					other = buildingBlock;
					result = dominatingBuildingPrivilege;
				}
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return result;
	}

	public void SV_RPCMessage(uint nameID, Message message)
	{
		Assert.IsTrue(base.isServer, "Should be server!");
		BasePlayer basePlayer = message.Player();
		if (!basePlayer.IsValid())
		{
			if (ConVar.Global.developer > 0)
			{
				Debug.Log("SV_RPCMessage: From invalid player " + basePlayer);
			}
		}
		else if (ConVar.AntiHack.rpcstallmode > 0 && basePlayer.isStalled)
		{
			if (ConVar.Global.developer > 0)
			{
				Debug.Log("SV_RPCMessage: player is stalled " + basePlayer);
			}
		}
		else if (ConVar.AntiHack.rpcstallmode > 1 && basePlayer.wasStalled)
		{
			if (ConVar.Global.developer > 0)
			{
				Debug.Log("SV_RPCMessage: player was stalled " + basePlayer);
			}
		}
		else if (!OnRpcMessage(basePlayer, nameID, message))
		{
			for (int i = 0; i < Components.Count && !Components[i].OnRpcMessage(basePlayer, nameID, message); i++)
			{
			}
		}
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPCPlayer<T1, T2, T3, T4, T5>(Connection sourceConnection, BasePlayer player, string funcName, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
	{
		if (Network.Net.sv.IsConnected() && net != null && player.net.connection != null)
		{
			ClientRPCEx(new SendInfo(player.net.connection), sourceConnection, funcName, arg1, arg2, arg3, arg4, arg5);
		}
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPCPlayer<T1, T2, T3, T4>(Connection sourceConnection, BasePlayer player, string funcName, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
	{
		if (Network.Net.sv.IsConnected() && net != null && player.net.connection != null)
		{
			ClientRPCEx(new SendInfo(player.net.connection), sourceConnection, funcName, arg1, arg2, arg3, arg4);
		}
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPCPlayer<T1, T2, T3>(Connection sourceConnection, BasePlayer player, string funcName, T1 arg1, T2 arg2, T3 arg3)
	{
		if (Network.Net.sv.IsConnected() && net != null && player.net.connection != null)
		{
			ClientRPCEx(new SendInfo(player.net.connection), sourceConnection, funcName, arg1, arg2, arg3);
		}
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPCPlayer<T1, T2>(Connection sourceConnection, BasePlayer player, string funcName, T1 arg1, T2 arg2)
	{
		if (Network.Net.sv.IsConnected() && net != null && player.net.connection != null)
		{
			ClientRPCEx(new SendInfo(player.net.connection), sourceConnection, funcName, arg1, arg2);
		}
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPCPlayer<T1>(Connection sourceConnection, BasePlayer player, string funcName, T1 arg1)
	{
		if (Network.Net.sv.IsConnected() && net != null && player.net.connection != null)
		{
			ClientRPCEx(new SendInfo(player.net.connection), sourceConnection, funcName, arg1);
		}
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPCPlayer(Connection sourceConnection, BasePlayer player, string funcName)
	{
		if (Network.Net.sv.IsConnected() && net != null && player.net.connection != null)
		{
			ClientRPCEx(new SendInfo(player.net.connection), sourceConnection, funcName);
		}
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPC<T1, T2, T3, T4, T5>(Connection sourceConnection, string funcName, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
	{
		if (Network.Net.sv.IsConnected() && net != null && net.group != null)
		{
			ClientRPCEx(new SendInfo(net.group.subscribers), sourceConnection, funcName, arg1, arg2, arg3, arg4, arg5);
		}
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPC<T1, T2, T3, T4>(Connection sourceConnection, string funcName, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
	{
		if (Network.Net.sv.IsConnected() && net != null && net.group != null)
		{
			ClientRPCEx(new SendInfo(net.group.subscribers), sourceConnection, funcName, arg1, arg2, arg3, arg4);
		}
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPC<T1, T2, T3>(Connection sourceConnection, string funcName, T1 arg1, T2 arg2, T3 arg3)
	{
		if (Network.Net.sv.IsConnected() && net != null && net.group != null)
		{
			ClientRPCEx(new SendInfo(net.group.subscribers), sourceConnection, funcName, arg1, arg2, arg3);
		}
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPC<T1, T2>(Connection sourceConnection, string funcName, T1 arg1, T2 arg2)
	{
		if (Network.Net.sv.IsConnected() && net != null && net.group != null)
		{
			ClientRPCEx(new SendInfo(net.group.subscribers), sourceConnection, funcName, arg1, arg2);
		}
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPC<T1>(Connection sourceConnection, string funcName, T1 arg1)
	{
		if (Network.Net.sv.IsConnected() && net != null && net.group != null)
		{
			ClientRPCEx(new SendInfo(net.group.subscribers), sourceConnection, funcName, arg1);
		}
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPC(Connection sourceConnection, string funcName)
	{
		if (Network.Net.sv.IsConnected() && net != null && net.group != null)
		{
			ClientRPCEx(new SendInfo(net.group.subscribers), sourceConnection, funcName);
		}
	}

	public void ClientRPC(RpcTarget target)
	{
		if (Network.Net.sv.IsConnected() && net != null)
		{
			GetRpcTargetNetworkGroup(ref target);
			NetWrite write = ClientRPCStart(target.Function);
			ClientRPCSend(write, target.Connections);
			FreeRPCTarget(target);
		}
	}

	public void ClientRPC<T1>(RpcTarget target, T1 arg1)
	{
		if (Network.Net.sv.IsConnected() && net != null)
		{
			GetRpcTargetNetworkGroup(ref target);
			NetWrite write = ClientRPCStart(target.Function);
			ClientRPCWrite(write, arg1);
			ClientRPCSend(write, target.Connections);
			FreeRPCTarget(target);
		}
	}

	public void ClientRPC<T1, T2>(RpcTarget target, T1 arg1, T2 arg2)
	{
		if (Network.Net.sv.IsConnected() && net != null)
		{
			GetRpcTargetNetworkGroup(ref target);
			NetWrite write = ClientRPCStart(target.Function);
			ClientRPCWrite(write, arg1);
			ClientRPCWrite(write, arg2);
			ClientRPCSend(write, target.Connections);
			FreeRPCTarget(target);
		}
	}

	public void ClientRPC<T1, T2, T3>(RpcTarget target, T1 arg1, T2 arg2, T3 arg3)
	{
		if (Network.Net.sv.IsConnected() && net != null)
		{
			GetRpcTargetNetworkGroup(ref target);
			NetWrite write = ClientRPCStart(target.Function);
			ClientRPCWrite(write, arg1);
			ClientRPCWrite(write, arg2);
			ClientRPCWrite(write, arg3);
			ClientRPCSend(write, target.Connections);
			FreeRPCTarget(target);
		}
	}

	public void ClientRPC<T1, T2, T3, T4>(RpcTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
	{
		if (Network.Net.sv.IsConnected() && net != null)
		{
			GetRpcTargetNetworkGroup(ref target);
			NetWrite write = ClientRPCStart(target.Function);
			ClientRPCWrite(write, arg1);
			ClientRPCWrite(write, arg2);
			ClientRPCWrite(write, arg3);
			ClientRPCWrite(write, arg4);
			ClientRPCSend(write, target.Connections);
			FreeRPCTarget(target);
		}
	}

	public void ClientRPC<T1, T2, T3, T4, T5>(RpcTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
	{
		if (Network.Net.sv.IsConnected() && net != null)
		{
			GetRpcTargetNetworkGroup(ref target);
			NetWrite write = ClientRPCStart(target.Function);
			ClientRPCWrite(write, arg1);
			ClientRPCWrite(write, arg2);
			ClientRPCWrite(write, arg3);
			ClientRPCWrite(write, arg4);
			ClientRPCWrite(write, arg5);
			ClientRPCSend(write, target.Connections);
			FreeRPCTarget(target);
		}
	}

	public void ClientRPC<T1, T2, T3, T4, T5, T6>(RpcTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
	{
		if (Network.Net.sv.IsConnected() && net != null)
		{
			GetRpcTargetNetworkGroup(ref target);
			NetWrite write = ClientRPCStart(target.Function);
			ClientRPCWrite(write, arg1);
			ClientRPCWrite(write, arg2);
			ClientRPCWrite(write, arg3);
			ClientRPCWrite(write, arg4);
			ClientRPCWrite(write, arg5);
			ClientRPCWrite(write, arg6);
			ClientRPCSend(write, target.Connections);
			FreeRPCTarget(target);
		}
	}

	public void ClientRPC<T1, T2, T3, T4, T5, T6, T7>(RpcTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
	{
		if (Network.Net.sv.IsConnected() && net != null)
		{
			GetRpcTargetNetworkGroup(ref target);
			NetWrite write = ClientRPCStart(target.Function);
			ClientRPCWrite(write, arg1);
			ClientRPCWrite(write, arg2);
			ClientRPCWrite(write, arg3);
			ClientRPCWrite(write, arg4);
			ClientRPCWrite(write, arg5);
			ClientRPCWrite(write, arg6);
			ClientRPCWrite(write, arg7);
			ClientRPCSend(write, target.Connections);
			FreeRPCTarget(target);
		}
	}

	public void ClientRPC(RpcTarget target, MemoryStream stream)
	{
		if (Network.Net.sv.IsConnected() && net != null)
		{
			GetRpcTargetNetworkGroup(ref target);
			NetWrite netWrite = ClientRPCStart(target.Function);
			using (TimeWarning.New("Copy Buffer"))
			{
				netWrite.Write(stream.GetBuffer(), 0, (int)stream.Length);
			}
			ClientRPCSend(netWrite, target.Connections);
			FreeRPCTarget(target);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void GetRpcTargetNetworkGroup(ref RpcTarget target)
	{
		if (target.ToNetworkGroup)
		{
			target.Connections = new SendInfo(net.group.subscribers);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void FreeRPCTarget(RpcTarget target)
	{
		if (target.UsingPooledConnections)
		{
			Facepunch.Pool.FreeUnmanaged(ref target.Connections.connections);
		}
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPCEx<T1, T2, T3, T4, T5>(SendInfo sendInfo, Connection sourceConnection, string funcName, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
	{
		ClientRPC(RpcTarget.SendInfo(funcName, sendInfo), arg1, arg2, arg3, arg4, arg5);
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPCEx<T1, T2, T3, T4>(SendInfo sendInfo, Connection sourceConnection, string funcName, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
	{
		ClientRPC(RpcTarget.SendInfo(funcName, sendInfo), arg1, arg2, arg3, arg4);
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPCEx<T1, T2, T3>(SendInfo sendInfo, Connection sourceConnection, string funcName, T1 arg1, T2 arg2, T3 arg3)
	{
		ClientRPC(RpcTarget.SendInfo(funcName, sendInfo), arg1, arg2, arg3);
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPCEx<T1, T2>(SendInfo sendInfo, Connection sourceConnection, string funcName, T1 arg1, T2 arg2)
	{
		ClientRPC(RpcTarget.SendInfo(funcName, sendInfo), arg1, arg2);
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPCEx<T1>(SendInfo sendInfo, Connection sourceConnection, string funcName, T1 arg1)
	{
		ClientRPC(RpcTarget.SendInfo(funcName, sendInfo), arg1);
	}

	[Obsolete("Use ClientRPC( RpcTarget ) overloads")]
	public void ClientRPCEx(SendInfo sendInfo, Connection sourceConnection, string funcName)
	{
		ClientRPC(RpcTarget.SendInfo(funcName, sendInfo));
	}

	protected NetWrite ClientRPCStart(string funcName)
	{
		NetWrite netWrite = Network.Net.sv.StartWrite();
		netWrite.PacketID(Message.Type.RPCMessage);
		netWrite.EntityID(net.ID);
		netWrite.UInt32(StringPool.Get(funcName));
		return netWrite;
	}

	private void ClientRPCWrite<T>(NetWrite write, T arg)
	{
		write.WriteObject(arg);
	}

	protected void ClientRPCSend(NetWrite write, SendInfo sendInfo)
	{
		write.Send(sendInfo);
	}

	public void ClientRPCPlayerList<T1>(Connection sourceConnection, BasePlayer player, string funcName, List<T1> list)
	{
		if (!Network.Net.sv.IsConnected() || net == null || player.net.connection == null)
		{
			return;
		}
		NetWrite write = ClientRPCStart(funcName);
		ClientRPCWrite(write, list.Count);
		foreach (T1 item in list)
		{
			ClientRPCWrite(write, item);
		}
		ClientRPCSend(write, new SendInfo(player.net.connection)
		{
			priority = Priority.Immediate
		});
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		BaseEntity baseEntity = parentEntity.Get(base.isServer);
		info.msg.baseEntity = Facepunch.Pool.Get<ProtoBuf.BaseEntity>();
		if (info.forDisk)
		{
			if (this is BasePlayer)
			{
				if (baseEntity == null || baseEntity.enableSaving)
				{
					info.msg.baseEntity.pos = base.transform.localPosition;
					info.msg.baseEntity.rot = base.transform.localRotation.eulerAngles;
				}
				else
				{
					info.msg.baseEntity.pos = base.transform.position;
					info.msg.baseEntity.rot = base.transform.rotation.eulerAngles;
				}
			}
			else
			{
				info.msg.baseEntity.pos = base.transform.localPosition;
				info.msg.baseEntity.rot = base.transform.localRotation.eulerAngles;
			}
		}
		else
		{
			info.msg.baseEntity.pos = GetNetworkPosition();
			info.msg.baseEntity.rot = GetNetworkRotation().eulerAngles;
			info.msg.baseEntity.time = GetNetworkTime();
		}
		info.msg.baseEntity.flags = (int)flags;
		info.msg.baseEntity.skinid = skinID;
		if (info.forDisk && this is BasePlayer)
		{
			if (baseEntity != null && baseEntity.enableSaving)
			{
				info.msg.parent = Facepunch.Pool.Get<ParentInfo>();
				info.msg.parent.uid = parentEntity.uid;
				info.msg.parent.bone = parentBone;
			}
		}
		else if (baseEntity != null)
		{
			info.msg.parent = Facepunch.Pool.Get<ParentInfo>();
			info.msg.parent.uid = parentEntity.uid;
			info.msg.parent.bone = parentBone;
		}
		if (HasAnySlot())
		{
			info.msg.entitySlots = Facepunch.Pool.Get<EntitySlots>();
			info.msg.entitySlots.slotLock = entitySlots[0].uid;
			info.msg.entitySlots.slotFireMod = entitySlots[1].uid;
			info.msg.entitySlots.slotUpperModification = entitySlots[2].uid;
			info.msg.entitySlots.centerDecoration = entitySlots[5].uid;
			info.msg.entitySlots.lowerCenterDecoration = entitySlots[6].uid;
			info.msg.entitySlots.storageMonitor = entitySlots[7].uid;
		}
		if (info.forDisk && (bool)_spawnable)
		{
			_spawnable.Save(info);
		}
		if (OwnerID != 0L && (info.forDisk || ShouldNetworkOwnerInfo()))
		{
			info.msg.ownerInfo = Facepunch.Pool.Get<OwnerInfo>();
			if (info.forDisk)
			{
				info.msg.ownerInfo.steamid = OwnerID;
			}
			else
			{
				info.msg.ownerInfo.steamid = ((OwnerID == info.forConnection.userid) ? info.forConnection.userid : 0);
			}
		}
		if (Components != null)
		{
			for (int i = 0; i < Components.Count; i++)
			{
				if (!(Components[i] == null))
				{
					Components[i].SaveComponent(info);
				}
			}
		}
		if (info.forTransfer && ShouldTransferAssociatedFiles)
		{
			info.msg.associatedFiles = Facepunch.Pool.Get<AssociatedFiles>();
			info.msg.associatedFiles.files = Facepunch.Pool.Get<List<AssociatedFiles.AssociatedFile>>();
			info.msg.associatedFiles.files.AddRange(FileStorage.server.QueryAllByEntity(net.ID));
		}
	}

	public virtual bool ShouldNetworkOwnerInfo()
	{
		return false;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.baseEntity != null)
		{
			ProtoBuf.BaseEntity baseEntity = info.msg.baseEntity;
			Flags old = flags;
			if (base.isServer)
			{
				baseEntity.flags &= -33554433;
			}
			flags = (Flags)baseEntity.flags;
			OnFlagsChanged(old, flags);
			OnSkinChanged(skinID, info.msg.baseEntity.skinid);
			if (info.fromDisk)
			{
				if (baseEntity.pos.IsNaNOrInfinity())
				{
					string text = ToString();
					Vector3 pos = baseEntity.pos;
					Debug.LogWarning(text + " has broken position - " + pos.ToString());
					baseEntity.pos = Vector3.zero;
				}
				base.transform.localPosition = baseEntity.pos;
				base.transform.localRotation = Quaternion.Euler(baseEntity.rot);
			}
		}
		if (info.msg.entitySlots != null)
		{
			entitySlots[0].uid = info.msg.entitySlots.slotLock;
			entitySlots[1].uid = info.msg.entitySlots.slotFireMod;
			entitySlots[2].uid = info.msg.entitySlots.slotUpperModification;
			entitySlots[5].uid = info.msg.entitySlots.centerDecoration;
			entitySlots[6].uid = info.msg.entitySlots.lowerCenterDecoration;
			entitySlots[7].uid = info.msg.entitySlots.storageMonitor;
		}
		if (info.msg.parent != null)
		{
			if (base.isServer)
			{
				BaseEntity entity = BaseNetworkable.serverEntities.Find(info.msg.parent.uid) as BaseEntity;
				SetParent(entity, info.msg.parent.bone);
			}
			parentEntity.uid = info.msg.parent.uid;
			parentBone = info.msg.parent.bone;
		}
		else
		{
			parentEntity.uid = default(NetworkableId);
			parentBone = 0u;
		}
		if (info.msg.ownerInfo != null)
		{
			OwnerID = info.msg.ownerInfo.steamid;
		}
		if ((bool)_spawnable)
		{
			_spawnable.Load(info);
		}
		if (info.fromTransfer && ShouldTransferAssociatedFiles && info.msg.associatedFiles != null && info.msg.associatedFiles.files != null)
		{
			foreach (AssociatedFiles.AssociatedFile file in info.msg.associatedFiles.files)
			{
				if (FileStorage.server.Store(file.data, (FileStorage.Type)file.type, net.ID, file.numID) != file.crc)
				{
					Debug.LogWarning("Associated file has a different CRC after transfer!");
				}
			}
		}
		if (info.fromDisk && info.msg.baseEntity != null && IsTransferProtected())
		{
			float num = ((info.msg.baseEntity.protection > 0f) ? info.msg.baseEntity.protection : Nexus.protectionDuration);
			_transferProtectionRemaining = num;
			Invoke(DisableTransferProtectionAction, num);
		}
		if (Components == null)
		{
			return;
		}
		for (int i = 0; i < Components.Count; i++)
		{
			if (!(Components[i] == null))
			{
				Components[i].LoadComponent(info);
			}
		}
	}

	public virtual void SetCreatorEntity(BaseEntity newCreatorEntity)
	{
		creatorEntity = newCreatorEntity;
	}

	public virtual Vector3 GetLocalVelocityServer()
	{
		return Vector3.zero;
	}

	public virtual Quaternion GetAngularVelocityServer()
	{
		return Quaternion.identity;
	}

	public void EnableGlobalBroadcast(bool wants)
	{
		if (globalBroadcast != wants)
		{
			globalBroadcast = wants;
			UpdateNetworkGroup();
		}
	}

	public void EnableSaving(bool wants)
	{
		if (enableSaving != wants)
		{
			enableSaving = wants;
			if (enableSaving)
			{
				saveList.Add(this);
			}
			else
			{
				saveList.Remove(this);
			}
		}
	}

	public void RestoreCanSave()
	{
		EnableSaving(couldSaveOriginally);
	}

	public override void ServerInit()
	{
		_spawnable = GetComponent<Spawnable>();
		base.ServerInit();
		if (!base.isServer)
		{
			return;
		}
		couldSaveOriginally = enableSaving;
		if (enableSaving)
		{
			saveList.Add(this);
		}
		if (flags != 0)
		{
			OnFlagsChanged((Flags)0, flags);
		}
		if (syncPosition && PositionTickRate >= 0f)
		{
			if (PositionTickFixedTime)
			{
				InvokeRepeatingFixedTime(NetworkPositionTick);
			}
			else
			{
				InvokeRandomized(NetworkPositionTick, PositionTickRate, PositionTickRate - PositionTickRate * 0.05f, PositionTickRate * 0.05f);
			}
		}
		Query.Server.Add(this);
		if (this is SamSite.ISamSiteTarget item)
		{
			SamSite.ISamSiteTarget.serverList.Add(item);
		}
	}

	public virtual void OnPlaced(BasePlayer player)
	{
	}

	protected virtual bool ShouldUpdateNetworkGroup()
	{
		return syncPosition;
	}

	protected virtual bool ShouldUpdateNetworkPosition()
	{
		return syncPosition;
	}

	protected void NetworkPositionTick()
	{
		if (!base.transform.hasChanged)
		{
			if (ticksSinceStopped >= 6)
			{
				return;
			}
			ticksSinceStopped++;
		}
		else
		{
			ticksSinceStopped = 0;
		}
		TransformChanged();
		base.transform.hasChanged = false;
	}

	private void TransformChanged()
	{
		if (Query.Server != null)
		{
			Query.Server.Move(this);
		}
		SingletonComponent<NpcFoodManager>.Instance.Move(this);
		SingletonComponent<NpcFireManager>.Instance.Move(this);
		if (net == null)
		{
			return;
		}
		InvalidateNetworkCache();
		if (!globalBroadcast && !ValidBounds.Test(this, base.transform.position))
		{
			OnInvalidPosition();
			return;
		}
		if (ShouldUpdateNetworkGroup() && !isCallingUpdateNetworkGroup)
		{
			Invoke(UpdateNetworkGroup, 5f);
			isCallingUpdateNetworkGroup = true;
		}
		if (ShouldUpdateNetworkPosition())
		{
			SendNetworkUpdate_Position();
			OnPositionalNetworkUpdate();
		}
	}

	public virtual void OnPositionalNetworkUpdate()
	{
	}

	public override void Spawn()
	{
		base.Spawn();
		if (base.isServer)
		{
			base.gameObject.BroadcastOnParentSpawning();
		}
	}

	public void OnParentSpawning()
	{
		if (net != null || base.IsDestroyed)
		{
			return;
		}
		if (Rust.Application.isLoadingSave)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		if (GameManager.server.preProcessed.NeedsProcessing(base.gameObject))
		{
			GameManager.server.preProcessed.ProcessObject(null, base.gameObject, PreProcessPrefabOptions.Default_NoResetPosition);
		}
		BaseEntity baseEntity = ((base.transform.parent != null) ? base.transform.parent.GetComponentInParent<BaseEntity>() : null);
		Spawn();
		if (baseEntity != null)
		{
			SetParent(baseEntity, worldPositionStays: true);
		}
	}

	public void SpawnAsMapEntity()
	{
		if (net == null && !base.IsDestroyed && ((base.transform.parent != null) ? base.transform.parent.GetComponentInParent<BaseEntity>() : null) == null)
		{
			if (GameManager.server.preProcessed.NeedsProcessing(base.gameObject))
			{
				GameManager.server.preProcessed.ProcessObject(null, base.gameObject, PreProcessPrefabOptions.Default_NoResetPosition);
			}
			base.transform.parent = null;
			SceneManager.MoveGameObjectToScene(base.gameObject, Rust.Server.EntityScene);
			base.gameObject.SetActive(value: true);
			Spawn();
		}
	}

	public virtual void PostMapEntitySpawn()
	{
	}

	internal override void DoServerDestroy()
	{
		CancelInvoke(NetworkPositionTick);
		if (enableSaving)
		{
			saveList.Remove(this);
		}
		enableSaving = couldSaveOriginally;
		RemoveFromTriggers();
		if (children != null)
		{
			BaseEntity[] array = children.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				array[i].OnParentRemoved();
			}
		}
		SetParent(null, worldPositionStays: true);
		Query.Server.Remove(this);
		SingletonComponent<NpcFireManager>.Instance.Remove(this);
		if (this is SamSite.ISamSiteTarget item)
		{
			SamSite.ISamSiteTarget.serverList.Remove(item);
		}
		base.DoServerDestroy();
	}

	internal virtual void OnParentRemoved()
	{
		Kill();
	}

	public virtual void OnInvalidPosition()
	{
		Debug.Log("Invalid Position: " + this?.ToString() + " " + base.transform.position.ToString() + " (destroying)");
		Kill();
	}

	public BaseCorpse DropCorpse(string strCorpsePrefab, BasePlayer.PlayerFlags playerFlagsOnDeath = (BasePlayer.PlayerFlags)0, ModelState modelState = null)
	{
		return DropCorpse(strCorpsePrefab, base.transform.position, base.transform.rotation, playerFlagsOnDeath, modelState);
	}

	public BaseCorpse DropCorpse(string strCorpsePrefab, Vector3 posOnDeath, Quaternion rotOnDeath, BasePlayer.PlayerFlags playerFlagsOnDeath = (BasePlayer.PlayerFlags)0, ModelState modelState = null)
	{
		Assert.IsTrue(base.isServer, "DropCorpse called on client!");
		if (!ConVar.Server.corpses)
		{
			return null;
		}
		if (string.IsNullOrEmpty(strCorpsePrefab))
		{
			return null;
		}
		BaseCorpse baseCorpse = GameManager.server.CreateEntity(strCorpsePrefab) as BaseCorpse;
		if (baseCorpse == null)
		{
			Debug.LogWarning("Error creating corpse: " + base.gameObject?.ToString() + " - " + strCorpsePrefab);
			return null;
		}
		baseCorpse.ServerInitCorpse(this, posOnDeath, rotOnDeath, playerFlagsOnDeath, modelState);
		return baseCorpse;
	}

	public override void UpdateNetworkGroup()
	{
		Assert.IsTrue(base.isServer, "UpdateNetworkGroup called on clientside entity!");
		isCallingUpdateNetworkGroup = false;
		if (net == null || Network.Net.sv == null || Network.Net.sv.visibility == null)
		{
			return;
		}
		using (TimeWarning.New("UpdateNetworkGroup"))
		{
			if (globalBroadcast)
			{
				if (net.SwitchGroup(BaseNetworkable.GlobalNetworkGroup))
				{
					SendNetworkGroupChange();
				}
			}
			else if (ShouldInheritNetworkGroup() && parentEntity.IsSet())
			{
				BaseEntity baseEntity = GetParentEntity();
				if (!baseEntity.IsValid())
				{
					if (!Rust.Application.isLoadingSave)
					{
						Debug.LogWarning("UpdateNetworkGroup: Missing parent entity " + parentEntity.uid.ToString());
						Invoke(UpdateNetworkGroup, 2f);
						isCallingUpdateNetworkGroup = true;
					}
				}
				else if (baseEntity != null)
				{
					if (net.SwitchGroup(baseEntity.net.group))
					{
						SendNetworkGroupChange();
					}
				}
				else
				{
					Debug.LogWarning(base.gameObject?.ToString() + ": has parent id - but couldn't find parent! " + parentEntity);
				}
			}
			else if (base.limitNetworking)
			{
				if (net.SwitchGroup(BaseNetworkable.LimboNetworkGroup))
				{
					SendNetworkGroupChange();
				}
			}
			else
			{
				base.UpdateNetworkGroup();
			}
		}
	}

	public virtual void Eat(BaseNpc baseNpc, float timeSpent)
	{
		baseNpc.AddCalories(100f);
	}

	public virtual void OnDeployed(BaseEntity parent, BasePlayer deployedBy, Item fromItem)
	{
	}

	public override bool ShouldNetworkTo(BasePlayer player)
	{
		if (player == this)
		{
			return true;
		}
		if (IsTransferProtected())
		{
			return false;
		}
		BaseEntity baseEntity = GetParentEntity();
		if (base.limitNetworking)
		{
			if (baseEntity == null)
			{
				return false;
			}
			if (baseEntity != player)
			{
				return false;
			}
		}
		if (ShouldInheritNetworkGroup() && baseEntity != null)
		{
			return baseEntity.ShouldNetworkTo(player);
		}
		return base.ShouldNetworkTo(player);
	}

	public virtual void AttackerInfo(PlayerLifeStory.DeathInfo info)
	{
		info.attackerName = base.ShortPrefabName;
		info.attackerSteamID = 0uL;
		info.inflictorName = "";
	}

	public virtual void Push(Vector3 velocity)
	{
		SetVelocity(velocity);
	}

	public virtual void ApplyInheritedVelocity(Vector3 velocity)
	{
		Rigidbody component = GetComponent<Rigidbody>();
		if ((bool)component)
		{
			component.velocity = Vector3.Lerp(component.velocity, velocity, 10f * UnityEngine.Time.fixedDeltaTime);
			component.angularVelocity *= Mathf.Clamp01(1f - 10f * UnityEngine.Time.fixedDeltaTime);
			component.AddForce(-UnityEngine.Physics.gravity * Mathf.Clamp01(0.9f), ForceMode.Acceleration);
		}
	}

	public virtual void SetVelocity(Vector3 velocity)
	{
		Rigidbody component = GetComponent<Rigidbody>();
		if ((bool)component)
		{
			component.velocity = velocity;
		}
	}

	public virtual void SetAngularVelocity(Vector3 velocity)
	{
		Rigidbody component = GetComponent<Rigidbody>();
		if ((bool)component)
		{
			component.angularVelocity = velocity;
		}
	}

	public virtual Vector3 GetDropPosition()
	{
		return base.transform.position;
	}

	public virtual Vector3 GetDropVelocity()
	{
		return GetInheritedDropVelocity() + Vector3.up;
	}

	public virtual bool OnStartBeingLooted(BasePlayer baseEntity)
	{
		return true;
	}

	public virtual string Admin_Who()
	{
		return $"Owner ID: {OwnerID}";
	}

	public virtual bool BuoyancyWake()
	{
		return false;
	}

	public virtual bool BuoyancySleep(bool inWater)
	{
		return false;
	}

	public virtual float RadiationProtection()
	{
		return 0f;
	}

	public virtual float RadiationExposureFraction()
	{
		return 1f;
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	private void BroadcastSignalFromClient(RPCMessage msg)
	{
		uint num = StringPool.Get("BroadcastSignalFromClient");
		if (num != 0)
		{
			BasePlayer player = msg.player;
			if (!(player == null) && player.rpcHistory.TryIncrement(num, (ulong)ConVar.Server.maxpacketspersecond_rpc_signal))
			{
				Signal signal = (Signal)msg.read.Int32();
				string arg = msg.read.String();
				SignalBroadcast(signal, arg, msg.connection);
				OnReceivedSignalServer(signal, arg);
			}
		}
	}

	protected virtual void OnReceivedSignalServer(Signal signal, string arg)
	{
		SingletonComponent<NpcFireManager>.Instance.OnReceivedSignalServer(this, signal, arg);
	}

	public void SignalBroadcast(Signal signal, string arg, Connection sourceConnection = null)
	{
		if (net != null && net.group != null)
		{
			ClientRPC(RpcTarget.NetworkGroup("SignalFromServerEx", this, SendMethod.Unreliable, Priority.Immediate), (int)signal, arg, sourceConnection?.userid ?? 0);
		}
	}

	public void SignalBroadcast(Signal signal, Connection sourceConnection = null)
	{
		if (net != null && net.group != null)
		{
			ClientRPC(RpcTarget.NetworkGroup("SignalFromServer", this, SendMethod.Unreliable, Priority.Immediate), (int)signal, sourceConnection?.userid ?? 0);
		}
	}

	public void SignalBroadcast(Signal signal, string arg, Connection sourceConnection, string fallbackEffect)
	{
		if (!ServerOcclusion.OcclusionEnabled)
		{
			SignalBroadcast(signal, arg, sourceConnection);
		}
		else
		{
			if (net == null || net.group == null)
			{
				return;
			}
			List<Connection> obj = Facepunch.Pool.Get<List<Connection>>();
			List<Connection> obj2 = Facepunch.Pool.Get<List<Connection>>();
			foreach (Connection subscriber in net.group.subscribers)
			{
				BasePlayer basePlayer = subscriber.player as BasePlayer;
				if (!(basePlayer == null))
				{
					if (ShouldNetworkTo(basePlayer))
					{
						obj.Add(subscriber);
					}
					else
					{
						obj2.Add(subscriber);
					}
				}
			}
			if (obj.Count > 0)
			{
				ClientRPC(RpcTarget.Players("SignalFromServerEx", obj, SendMethod.Unreliable, Priority.Immediate), (int)signal, arg, sourceConnection?.userid ?? 0);
			}
			if (obj2.Count > 0)
			{
				Effect.server.Run(fallbackEffect, base.transform.position, base.transform.up, sourceConnection, broadcast: false, obj2);
			}
			Facepunch.Pool.FreeUnmanaged(ref obj);
			Facepunch.Pool.FreeUnmanaged(ref obj2);
		}
	}

	protected virtual void OnSkinChanged(ulong oldSkinID, ulong newSkinID)
	{
		if (oldSkinID != newSkinID)
		{
			skinID = newSkinID;
		}
	}

	protected virtual void OnSkinPreProcess(IPrefabProcessor preProcess, GameObject rootObj, string name, bool serverside, bool clientside, bool bundling)
	{
		if (clientside && Skinnable.All != null && Skinnable.FindForEntity(name) != null)
		{
			Rust.Workshop.WorkshopSkin.Prepare(rootObj);
			MaterialReplacement.Prepare(rootObj);
		}
	}

	public virtual void PreProcess(IPrefabProcessor preProcess, GameObject rootObj, string name, bool serverside, bool clientside, bool bundling)
	{
		OnSkinPreProcess(preProcess, rootObj, name, serverside, clientside, bundling);
	}

	public bool HasAnySlot()
	{
		for (int i = 0; i < entitySlots.Length; i++)
		{
			if (entitySlots[i].IsValid(base.isServer))
			{
				return true;
			}
		}
		return false;
	}

	public BaseEntity GetSlot(Slot slot)
	{
		return entitySlots[(int)slot].Get(base.isServer);
	}

	public string GetSlotAnchorName(Slot slot)
	{
		return slot.ToString().ToLower();
	}

	public void SetSlot(Slot slot, BaseEntity ent)
	{
		entitySlots[(int)slot].Set(ent);
		SendNetworkUpdate();
	}

	public EntityRef[] GetSlots()
	{
		return entitySlots;
	}

	public void SetSlots(EntityRef[] newSlots)
	{
		entitySlots = newSlots;
	}

	public virtual bool HasSlot(Slot slot)
	{
		return false;
	}

	public bool HasTrait(TraitFlag f)
	{
		return (Traits & f) == f;
	}

	public bool HasAnyTrait(TraitFlag f)
	{
		return (Traits & f) != 0;
	}

	public virtual bool EnterTrigger(TriggerBase trigger)
	{
		if (triggers == null)
		{
			triggers = Facepunch.Pool.Get<List<TriggerBase>>();
		}
		triggers.Add(trigger);
		return true;
	}

	public virtual void LeaveTrigger(TriggerBase trigger)
	{
		if (triggers != null)
		{
			triggers.Remove(trigger);
			if (triggers.Count == 0)
			{
				Facepunch.Pool.FreeUnmanaged(ref triggers);
			}
		}
	}

	public void RemoveFromTriggers()
	{
		if (triggers == null)
		{
			return;
		}
		using (TimeWarning.New("RemoveFromTriggers"))
		{
			List<TriggerBase> obj = triggers.ShallowClonePooled();
			foreach (TriggerBase item in obj)
			{
				if ((bool)item)
				{
					item.RemoveEntity(this);
				}
			}
			Facepunch.Pool.FreeUnmanaged(ref obj);
			if (triggers != null && triggers.Count == 0)
			{
				Facepunch.Pool.FreeUnmanaged(ref triggers);
			}
		}
	}

	public T FindTrigger<T>() where T : TriggerBase
	{
		if (triggers == null)
		{
			return null;
		}
		foreach (TriggerBase trigger in triggers)
		{
			if (!(trigger as T == null))
			{
				return trigger as T;
			}
		}
		return null;
	}

	public bool FindTrigger<T>(out T result) where T : TriggerBase
	{
		result = FindTrigger<T>();
		return result != null;
	}

	private void ForceUpdateTriggersAction()
	{
		if (!base.IsDestroyed)
		{
			ForceUpdateTriggers(enter: false, exit: true, invoke: false);
		}
	}

	public void ForceUpdateTriggers(bool enter = true, bool exit = true, bool invoke = true)
	{
		List<TriggerBase> obj = Facepunch.Pool.Get<List<TriggerBase>>();
		List<TriggerBase> obj2 = Facepunch.Pool.Get<List<TriggerBase>>();
		if (triggers != null)
		{
			obj.AddRange(triggers);
		}
		Collider componentInChildren = GetComponentInChildren<Collider>();
		if (componentInChildren is CapsuleCollider)
		{
			CapsuleCollider capsuleCollider = componentInChildren as CapsuleCollider;
			Vector3 point = base.transform.position + new Vector3(0f, capsuleCollider.radius, 0f);
			Vector3 point2 = base.transform.position + new Vector3(0f, capsuleCollider.height - capsuleCollider.radius, 0f);
			GamePhysics.OverlapCapsule(point, point2, capsuleCollider.radius, obj2, 262144, QueryTriggerInteraction.Collide);
		}
		else if (componentInChildren is BoxCollider)
		{
			BoxCollider boxCollider = componentInChildren as BoxCollider;
			GamePhysics.OverlapOBB(new OBB(base.transform.position, base.transform.lossyScale, base.transform.rotation, new Bounds(boxCollider.center, boxCollider.size)), obj2, 262144, QueryTriggerInteraction.Collide);
		}
		else if (componentInChildren is SphereCollider)
		{
			SphereCollider sphereCollider = componentInChildren as SphereCollider;
			GamePhysics.OverlapSphere(base.transform.TransformPoint(sphereCollider.center), sphereCollider.radius, obj2, 262144, QueryTriggerInteraction.Collide);
		}
		else
		{
			obj2.AddRange(obj);
		}
		if (exit)
		{
			foreach (TriggerBase item in obj)
			{
				if (!obj2.Contains(item))
				{
					item.OnTriggerExit(componentInChildren);
				}
			}
		}
		if (enter)
		{
			foreach (TriggerBase item2 in obj2)
			{
				if (!obj.Contains(item2))
				{
					item2.OnTriggerEnter(componentInChildren);
				}
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		Facepunch.Pool.FreeUnmanaged(ref obj2);
		if (invoke)
		{
			Invoke(ForceUpdateTriggersAction, UnityEngine.Time.time - UnityEngine.Time.fixedTime + UnityEngine.Time.fixedDeltaTime * 1.5f);
		}
	}

	public virtual bool InSafeZone()
	{
		BaseGameMode activeGameMode = BaseGameMode.GetActiveGameMode(serverside: true);
		if (activeGameMode != null && !activeGameMode.safeZone)
		{
			return false;
		}
		float num = 0f;
		Vector3 position = base.transform.position;
		if (triggers != null)
		{
			for (int i = 0; i < triggers.Count; i++)
			{
				TriggerSafeZone triggerSafeZone = triggers[i] as TriggerSafeZone;
				if (!(triggerSafeZone == null))
				{
					float safeLevel = triggerSafeZone.GetSafeLevel(position);
					if (safeLevel > num)
					{
						num = safeLevel;
					}
				}
			}
		}
		return num > 0f;
	}

	public TriggerParent FindSuitableParent()
	{
		if (triggers == null)
		{
			return null;
		}
		foreach (TriggerBase trigger in triggers)
		{
			if (trigger is TriggerParent triggerParent && triggerParent.ShouldParent(this, bypassOtherTriggerCheck: true))
			{
				return triggerParent;
			}
		}
		return null;
	}
}
