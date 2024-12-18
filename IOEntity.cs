#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ConVar;
using Facepunch;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

public class IOEntity : DecayEntity
{
	public enum IOType
	{
		Electric,
		Fluidic,
		Kinetic,
		Generic,
		Industrial
	}

	public enum QueueType
	{
		ElectricLowPriority,
		ElectricHighPriority,
		Fluidic,
		Kinetic,
		Generic,
		Industrial
	}

	[Serializable]
	public class IORef
	{
		public EntityRef entityRef;

		public IOEntity ioEnt;

		public void Init()
		{
			if (ioEnt != null && !entityRef.IsValid(serverside: true))
			{
				entityRef.Set(ioEnt);
			}
			if (entityRef.IsValid(serverside: true))
			{
				ioEnt = entityRef.Get(serverside: true).GetComponent<IOEntity>();
			}
		}

		public void InitClient()
		{
			if (entityRef.IsValid(serverside: false) && ioEnt == null)
			{
				ioEnt = entityRef.Get(serverside: false).GetComponent<IOEntity>();
			}
		}

		public IOEntity Get(bool isServer = true)
		{
			if (ioEnt == null && entityRef.IsValid(isServer))
			{
				ioEnt = entityRef.Get(isServer) as IOEntity;
			}
			return ioEnt;
		}

		public void Clear()
		{
			ioEnt = null;
			entityRef.Set(null);
		}

		public void Set(IOEntity newIOEnt)
		{
			entityRef.Set(newIOEnt);
		}
	}

	[Serializable]
	public class IOSlot
	{
		public string niceName;

		public IOType type;

		public IORef connectedTo;

		public int connectedToSlot;

		public IOHandlePriority importance;

		public float ArrowOffset;

		public Vector3[] linePoints;

		public LineAnchor[] lineAnchors;

		public float[] slackLevels;

		public Vector3 worldSpaceLineEndRotation;

		[HideInInspector]
		public Vector3 originPosition;

		[HideInInspector]
		public Vector3 originRotation;

		public ClientIOLine line;

		public Vector3 handlePosition;

		public Vector3 handleDirection;

		public bool rootConnectionsOnly;

		public bool mainPowerSlot;

		public WireTool.WireColour wireColour;

		public float lineThickness;

		public void Clear()
		{
			if (connectedTo == null)
			{
				connectedTo = new IORef();
			}
			else
			{
				connectedTo.Clear();
			}
			connectedToSlot = 0;
			linePoints = null;
			lineAnchors = null;
		}

		public bool IsConnected()
		{
			return connectedTo.Get() != null;
		}
	}

	private struct FrameTiming
	{
		public string PrefabName;

		public double Time;
	}

	public struct LineAnchor
	{
		public EntityRef<Door> entityRef;

		public string boneName;

		public int index;

		public Vector3 position;

		public LineAnchor(WireLineAnchorInfo info)
		{
			entityRef = new EntityRef<Door>(info.parentID);
			boneName = info.boneName;
			index = (int)info.index;
			position = info.position;
		}

		public WireLineAnchorInfo ToInfo()
		{
			return new WireLineAnchorInfo
			{
				parentID = entityRef.Get(serverside: true).net.ID,
				boneName = boneName,
				index = index,
				position = position
			};
		}
	}

	public struct ContainerInputOutput
	{
		public IIndustrialStorage Storage;

		public int SlotIndex;

		public int MaxStackSize;

		public int ParentStorage;

		public int IndustrialSiblingCount;
	}

	[Header("IOEntity")]
	public Transform debugOrigin;

	public ItemDefinition sourceItem;

	[NonSerialized]
	public int lastResetIndex;

	[ServerVar]
	[Help("How many milliseconds to budget for processing high priority electric io entities per server frame (monuments)")]
	public static float frameBudgetElectricHighPriorityMs = 1f;

	[ServerVar]
	[Help("How many milliseconds to budget for processing low priority io entities per server frame (player placed)")]
	public static float frameBudgetElectricLowPriorityMs = 0.5f;

	[ServerVar]
	[Help("How many milliseconds to budget for processing fluid io entities per server frame")]
	public static float frameBudgetFluidMs = 0.25f;

	[ServerVar]
	[Help("How many milliseconds to budget for processing kinetic io entities per server frame (monuments)")]
	public static float frameBudgetKineticMs = 1f;

	[ServerVar]
	[Help("How many milliseconds to budget for processing generic io entities per server frame (unused for now)")]
	public static float frameBudgetGenericMs = 1f;

	[ServerVar]
	[Help("How many milliseconds to budget for processing industrial entities per server frame")]
	public static float frameBudgetIndustrialMs = 0.25f;

	[ServerVar]
	public static float responsetime = 0.1f;

	[ServerVar]
	public static int backtracking = 8;

	[ServerVar(Help = "Print out what is taking so long in the IO frame budget")]
	public static bool debugBudget = false;

	[ServerVar(Help = "Ignore frames with a lower ms than this while debugBudget is active")]
	public static float debugBudgetThreshold = 2f;

	private static bool _infinitePower = false;

	public const Flags Flag_ShortCircuit = Flags.Reserved7;

	public const Flags Flag_HasPower = Flags.Reserved8;

	public IOSlot[] inputs;

	public IOSlot[] outputs;

	public IOType ioType;

	private static Dictionary<QueueType, Queue<IOEntity>> _processQueues = new Dictionary<QueueType, Queue<IOEntity>>
	{
		{
			QueueType.ElectricHighPriority,
			new Queue<IOEntity>()
		},
		{
			QueueType.ElectricLowPriority,
			new Queue<IOEntity>()
		},
		{
			QueueType.Fluidic,
			new Queue<IOEntity>()
		},
		{
			QueueType.Kinetic,
			new Queue<IOEntity>()
		},
		{
			QueueType.Generic,
			new Queue<IOEntity>()
		},
		{
			QueueType.Industrial,
			new Queue<IOEntity>()
		}
	};

	private static Dictionary<QueueType, string> _processQueueProfilerString = new Dictionary<QueueType, string>
	{
		{
			QueueType.ElectricHighPriority,
			"HighPriorityElectric"
		},
		{
			QueueType.ElectricLowPriority,
			"LowPriorityElectric"
		},
		{
			QueueType.Fluidic,
			"Fluid"
		},
		{
			QueueType.Kinetic,
			"Kinetic"
		},
		{
			QueueType.Generic,
			"Generic"
		},
		{
			QueueType.Industrial,
			"Industrial"
		}
	};

	private static List<FrameTiming> timings = new List<FrameTiming>();

	protected int cachedOutputsUsed;

	protected int lastPassthroughEnergy;

	private int lastEnergy;

	protected int currentEnergy;

	private int changedCount;

	private float lastChangeTime;

	protected float lastUpdateTime;

	protected int lastUpdateBlockedFrame;

	protected bool ensureOutputsUpdated;

	public const int MaxContainerSourceCount = 32;

	private List<Collider> spawnedColliders = new List<Collider>();

	public virtual bool IsGravitySource => false;

	[ReplicatedVar(Help = "All player placed electrical entities will receive full power without needing to be plugged into anything")]
	public static bool infiniteIoPower
	{
		get
		{
			return _infinitePower;
		}
		set
		{
			if (_infinitePower == value)
			{
				return;
			}
			_infinitePower = value;
			foreach (BaseNetworkable serverEntity in BaseNetworkable.serverEntities)
			{
				if (!(serverEntity is IOEntity iOEntity) || iOEntity.GetQueueType() != 0)
				{
					continue;
				}
				if (infiniteIoPower)
				{
					iOEntity.ApplyInfinitePower();
					continue;
				}
				iOEntity.MarkDirtyForceUpdateOutputs();
				bool flag = false;
				IOSlot[] array = iOEntity.inputs;
				for (int i = 0; i < array.Length; i++)
				{
					if (array[i].connectedTo.Get() != null)
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					for (int j = 0; j < iOEntity.inputs.Length; j++)
					{
						iOEntity.UpdateFromInput(0, j);
					}
				}
			}
		}
	}

	protected virtual bool PreventDuplicatesInQueue => false;

	private bool HasBlockedUpdatedOutputsThisFrame => UnityEngine.Time.frameCount == lastUpdateBlockedFrame;

	public virtual bool BlockFluidDraining => false;

	protected virtual float LiquidPassthroughGravityThreshold => 1f;

	protected virtual bool DisregardGravityRestrictionsOnLiquid => false;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("IOEntity.OnRpcMessage"))
		{
			if (rpc == 4161541566u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_RequestData ");
				}
				using (TimeWarning.New("Server_RequestData"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(4161541566u, "Server_RequestData", this, player, 10uL))
						{
							return true;
						}
						if (!RPC_Server.IsVisible.Test(4161541566u, "Server_RequestData", this, player, 6f))
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
							Server_RequestData(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in Server_RequestData");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void ResetState()
	{
		base.ResetState();
		if (base.isServer)
		{
			lastResetIndex = 0;
			cachedOutputsUsed = 0;
			lastPassthroughEnergy = 0;
			lastEnergy = 0;
			currentEnergy = 0;
			lastUpdateTime = 0f;
			ensureOutputsUpdated = false;
		}
		ClearIndustrialPreventBuilding();
	}

	public Translate.Phrase GetDisplayName()
	{
		if (!(sourceItem != null))
		{
			return base.ShortPrefabName;
		}
		return sourceItem.displayName;
	}

	public virtual bool IsRootEntity()
	{
		return false;
	}

	public IOEntity FindGravitySource(ref Vector3 worldHandlePosition, int depth, bool ignoreSelf)
	{
		if (depth <= 0)
		{
			return null;
		}
		if (!ignoreSelf && IsGravitySource)
		{
			worldHandlePosition = base.transform.TransformPoint(outputs[0].handlePosition);
			return this;
		}
		IOSlot[] array = inputs;
		for (int i = 0; i < array.Length; i++)
		{
			IOEntity iOEntity = array[i].connectedTo.Get(base.isServer);
			if (iOEntity != null)
			{
				if (iOEntity.IsGravitySource)
				{
					worldHandlePosition = iOEntity.transform.TransformPoint(iOEntity.outputs[0].handlePosition);
					return iOEntity;
				}
				iOEntity = iOEntity.FindGravitySource(ref worldHandlePosition, depth - 1, ignoreSelf: false);
				if (iOEntity != null)
				{
					worldHandlePosition = iOEntity.transform.TransformPoint(iOEntity.outputs[0].handlePosition);
					return iOEntity;
				}
			}
		}
		return null;
	}

	public virtual void SetFuelType(ItemDefinition def, IOEntity source)
	{
	}

	public virtual bool WantsPower(int inputIndex)
	{
		return true;
	}

	public virtual bool AllowWireConnections()
	{
		if (GetComponentInParent<BaseVehicle>() != null)
		{
			return false;
		}
		return true;
	}

	public virtual bool WantsPassthroughPower()
	{
		return true;
	}

	public virtual int ConsumptionAmount()
	{
		return 1;
	}

	public virtual bool ShouldDrainBattery(IOEntity battery)
	{
		return ioType == battery.ioType;
	}

	public virtual bool ShouldBlockCircuit(IOEntity battery)
	{
		return false;
	}

	public virtual int MaximalPowerOutput()
	{
		return 0;
	}

	public virtual bool AllowDrainFrom(int outputSlot)
	{
		return true;
	}

	public QueueType GetQueueType()
	{
		switch (ioType)
		{
		case IOType.Electric:
			if (sourceItem == null)
			{
				return QueueType.ElectricHighPriority;
			}
			return QueueType.ElectricLowPriority;
		case IOType.Fluidic:
			return QueueType.Fluidic;
		case IOType.Kinetic:
			return QueueType.Kinetic;
		case IOType.Generic:
			return QueueType.Generic;
		case IOType.Industrial:
			return QueueType.Industrial;
		default:
			return QueueType.ElectricLowPriority;
		}
	}

	public static float GetFrameBudgetForQueue(QueueType type)
	{
		return type switch
		{
			QueueType.ElectricLowPriority => frameBudgetElectricLowPriorityMs, 
			QueueType.ElectricHighPriority => frameBudgetElectricHighPriorityMs, 
			QueueType.Fluidic => frameBudgetFluidMs, 
			QueueType.Kinetic => frameBudgetKineticMs, 
			QueueType.Generic => frameBudgetGenericMs, 
			QueueType.Industrial => frameBudgetIndustrialMs, 
			_ => frameBudgetElectricLowPriorityMs, 
		};
	}

	public virtual bool IsPowered()
	{
		return HasFlag(Flags.Reserved8);
	}

	public bool IsConnectedToAnySlot(IOEntity entity, int slot, int depth, bool defaultReturn = false)
	{
		if (depth > 0 && slot < inputs.Length)
		{
			IOEntity iOEntity = inputs[slot].connectedTo.Get();
			if (iOEntity != null)
			{
				if (iOEntity == entity)
				{
					return true;
				}
				if (ConsiderConnectedTo(entity))
				{
					return true;
				}
				if (iOEntity.IsConnectedTo(entity, depth - 1, defaultReturn))
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool IsConnectedTo(IOEntity entity, int slot, int depth, bool defaultReturn = false)
	{
		if (depth > 0 && slot < inputs.Length)
		{
			IOSlot iOSlot = inputs[slot];
			if (iOSlot.mainPowerSlot)
			{
				IOEntity iOEntity = iOSlot.connectedTo.Get();
				if (iOEntity != null)
				{
					if (iOEntity == entity)
					{
						return true;
					}
					if (ConsiderConnectedTo(entity))
					{
						return true;
					}
					if (iOEntity.IsConnectedTo(entity, depth - 1, defaultReturn))
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	public bool IsConnectedTo(IOEntity entity, int depth, bool defaultReturn = false)
	{
		if (depth > 0)
		{
			for (int i = 0; i < inputs.Length; i++)
			{
				IOSlot iOSlot = inputs[i];
				if (!iOSlot.mainPowerSlot)
				{
					continue;
				}
				IOEntity iOEntity = iOSlot.connectedTo.Get();
				if (iOEntity != null)
				{
					if (iOEntity == entity)
					{
						return true;
					}
					if (ConsiderConnectedTo(entity))
					{
						return true;
					}
					if (iOEntity.IsConnectedTo(entity, depth - 1, defaultReturn))
					{
						return true;
					}
				}
			}
			return false;
		}
		return defaultReturn;
	}

	protected virtual bool ConsiderConnectedTo(IOEntity entity)
	{
		return false;
	}

	[RPC_Server]
	[RPC_Server.IsVisible(6f)]
	[RPC_Server.CallsPerSecond(10uL)]
	private void Server_RequestData(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		int slot = msg.read.Int32();
		bool input = msg.read.Int32() == 1;
		SendAdditionalData(player, slot, input);
	}

	public virtual void SendAdditionalData(BasePlayer player, int slot, bool input)
	{
		int passthroughAmountForAnySlot = GetPassthroughAmountForAnySlot(slot, input);
		ClientRPC(RpcTarget.Player("Client_ReceiveAdditionalData", player), currentEnergy, passthroughAmountForAnySlot, 0f, 0f);
	}

	protected int GetPassthroughAmountForAnySlot(int slot, bool isInputSlot)
	{
		int result = 0;
		if (isInputSlot)
		{
			if (slot >= 0 && slot < inputs.Length)
			{
				IOSlot iOSlot = inputs[slot];
				IOEntity iOEntity = iOSlot.connectedTo.Get();
				if (iOEntity != null && iOSlot.connectedToSlot >= 0 && iOSlot.connectedToSlot < iOEntity.outputs.Length)
				{
					result = iOEntity.GetPassthroughAmount(inputs[slot].connectedToSlot);
				}
			}
		}
		else if (slot >= 0 && slot < outputs.Length)
		{
			result = GetPassthroughAmount(slot);
		}
		return result;
	}

	public static void ProcessQueue()
	{
		if (debugBudget)
		{
			timings.Clear();
		}
		double realtimeSinceStartupAsDouble = UnityEngine.Time.realtimeSinceStartupAsDouble;
		foreach (KeyValuePair<QueueType, Queue<IOEntity>> processQueue in _processQueues)
		{
			double num = UnityEngine.Time.realtimeSinceStartup;
			double num2 = GetFrameBudgetForQueue(processQueue.Key) / 1000f;
			while (processQueue.Value.Count > 0 && UnityEngine.Time.realtimeSinceStartupAsDouble < num + num2 && !processQueue.Value.Peek().HasBlockedUpdatedOutputsThisFrame)
			{
				double realtimeSinceStartupAsDouble2 = UnityEngine.Time.realtimeSinceStartupAsDouble;
				IOEntity iOEntity = processQueue.Value.Dequeue();
				if (iOEntity.IsValid())
				{
					iOEntity.UpdateOutputs();
				}
				if (debugBudget)
				{
					timings.Add(new FrameTiming
					{
						PrefabName = iOEntity.ShortPrefabName,
						Time = (UnityEngine.Time.realtimeSinceStartupAsDouble - realtimeSinceStartupAsDouble2) * 1000.0
					});
				}
			}
		}
		if (debugBudget)
		{
			double num3 = UnityEngine.Time.realtimeSinceStartupAsDouble - realtimeSinceStartupAsDouble;
			double num4 = (double)debugBudgetThreshold / 1000.0;
			if (num3 > num4)
			{
				TextTable textTable = new TextTable();
				textTable.AddColumns("Prefab Name", "Time (in ms)");
				foreach (FrameTiming timing in timings)
				{
					string[] obj = new string[2] { timing.PrefabName, null };
					double time = timing.Time;
					obj[1] = time.ToString();
					textTable.AddRow(obj);
				}
				textTable.AddRow("Total time", (num3 * 1000.0).ToString());
				Debug.Log(textTable.ToString());
			}
		}
		AutoTurret.ProcessInterferenceQueue();
	}

	[ServerVar(ServerAdmin = true)]
	public static void DebugQueue(ConsoleSystem.Arg arg)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyValuePair<QueueType, Queue<IOEntity>> processQueue in _processQueues)
		{
			QueueType key = processQueue.Key;
			Queue<IOEntity> value = processQueue.Value;
			stringBuilder.AppendLine($"{key} queue size: {value.Count}");
			if (value.Count == 0)
			{
				continue;
			}
			var list = (from e in value
				group e by e.PrefabName into g
				select new
				{
					PrefabName = g.Key,
					Count = g.Count(),
					Entities = g.ToList()
				} into g
				orderby g.Count descending
				select g).Take(10).ToList();
			stringBuilder.AppendLine("-----Top queue occupants-----");
			foreach (var item in list)
			{
				IOEntity iOEntity = item.Entities.First();
				stringBuilder.AppendLine($"[{item.Count} times] {iOEntity.PrefabName} @ {iOEntity.transform.position}");
			}
			if (list.Count > 0)
			{
				IOEntity iOEntity2 = list.First().Entities.First();
				stringBuilder.AppendLine("-----Showing top entity-----");
				stringBuilder.AppendLine($"Entity type: {iOEntity2.ioType}");
				stringBuilder.AppendLine("Entity prefab: " + iOEntity2.PrefabName);
				stringBuilder.AppendLine($"Entity net id: {iOEntity2.net.ID}");
				stringBuilder.AppendLine($"Entity position: teleportpos {iOEntity2.transform.position}");
			}
		}
		arg.ReplyWith(stringBuilder.ToString());
	}

	public virtual void ResetIOState()
	{
	}

	public virtual void Init()
	{
		for (int i = 0; i < outputs.Length; i++)
		{
			IOSlot iOSlot = outputs[i];
			iOSlot.connectedTo.Init();
			if (iOSlot.connectedTo.Get() != null)
			{
				int connectedToSlot = iOSlot.connectedToSlot;
				if (connectedToSlot < 0 || connectedToSlot >= iOSlot.connectedTo.Get().inputs.Length)
				{
					Debug.LogError("Slot IOR Error: " + base.name + " setting up inputs for " + iOSlot.connectedTo.Get().name + " slot : " + iOSlot.connectedToSlot);
				}
				else
				{
					iOSlot.connectedTo.Get().inputs[iOSlot.connectedToSlot].connectedTo.Set(this);
					iOSlot.connectedTo.Get().inputs[iOSlot.connectedToSlot].connectedToSlot = i;
					iOSlot.connectedTo.Get().inputs[iOSlot.connectedToSlot].connectedTo.Init();
				}
			}
		}
		UpdateUsedOutputs();
		if (IsRootEntity())
		{
			Invoke(MarkDirtyForceUpdateOutputs, UnityEngine.Random.Range(1f, 1f));
		}
		ApplyInfinitePower();
	}

	private void ApplyInfinitePower()
	{
		if (infiniteIoPower && GetQueueType() == QueueType.ElectricLowPriority)
		{
			for (int i = 0; i < inputs.Length; i++)
			{
				UpdateFromInput(999, 0);
			}
		}
	}

	internal override void DoServerDestroy()
	{
		if (base.isServer)
		{
			Shutdown();
		}
		base.DoServerDestroy();
	}

	public void ClearConnections()
	{
		List<IOEntity> obj = Facepunch.Pool.Get<List<IOEntity>>();
		List<IOEntity> obj2 = Facepunch.Pool.Get<List<IOEntity>>();
		IOSlot[] array = inputs;
		foreach (IOSlot iOSlot in array)
		{
			IOEntity iOEntity = null;
			if (iOSlot.connectedTo.Get() != null)
			{
				iOEntity = iOSlot.connectedTo.Get();
				if (iOSlot.type == IOType.Industrial)
				{
					obj2.Add(iOEntity);
				}
				IOSlot[] array2 = iOSlot.connectedTo.Get().outputs;
				foreach (IOSlot iOSlot2 in array2)
				{
					if (iOSlot2.connectedTo.Get() != null && iOSlot2.connectedTo.Get().EqualNetID(this))
					{
						iOSlot2.Clear();
					}
				}
			}
			iOSlot.Clear();
			if ((bool)iOEntity)
			{
				iOEntity.SendNetworkUpdate();
			}
		}
		array = outputs;
		foreach (IOSlot iOSlot3 in array)
		{
			if (iOSlot3.connectedTo.Get() != null)
			{
				obj.Add(iOSlot3.connectedTo.Get());
				if (iOSlot3.type == IOType.Industrial)
				{
					obj2.Add(obj[obj.Count - 1]);
				}
				IOSlot[] array2 = iOSlot3.connectedTo.Get().inputs;
				foreach (IOSlot iOSlot4 in array2)
				{
					if (iOSlot4.connectedTo.Get() != null && iOSlot4.connectedTo.Get().EqualNetID(this))
					{
						iOSlot4.Clear();
					}
				}
			}
			if ((bool)iOSlot3.connectedTo.Get())
			{
				iOSlot3.connectedTo.Get().UpdateFromInput(0, iOSlot3.connectedToSlot);
			}
			iOSlot3.Clear();
		}
		SendNetworkUpdate();
		foreach (IOEntity item in obj)
		{
			if (item != null)
			{
				item.MarkDirty();
				item.SendNetworkUpdate();
			}
		}
		for (int k = 0; k < inputs.Length; k++)
		{
			UpdateFromInput(0, k);
		}
		foreach (IOEntity item2 in obj2)
		{
			if (item2 != null)
			{
				item2.NotifyIndustrialNetworkChanged();
			}
			item2.RefreshIndustrialPreventBuilding();
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		Facepunch.Pool.FreeUnmanaged(ref obj2);
		RefreshIndustrialPreventBuilding();
	}

	public void Shutdown()
	{
		SendChangedToRoot(forceUpdate: true);
		ClearConnections();
	}

	public void MarkDirtyForceUpdateOutputs()
	{
		ensureOutputsUpdated = true;
		MarkDirty();
	}

	public void UpdateUsedOutputs()
	{
		cachedOutputsUsed = 0;
		IOSlot[] array = outputs;
		for (int i = 0; i < array.Length; i++)
		{
			IOEntity iOEntity = array[i].connectedTo.Get();
			if (iOEntity != null && !iOEntity.IsDestroyed)
			{
				cachedOutputsUsed++;
			}
		}
	}

	public virtual void MarkDirty()
	{
		if (!base.isClient)
		{
			UpdateUsedOutputs();
			TouchIOState();
		}
	}

	public virtual int DesiredPower(int inputIndex = 0)
	{
		if (!inputs[inputIndex].mainPowerSlot)
		{
			return 0;
		}
		int num = ConsumptionAmount();
		if (IsFlickering())
		{
			return num;
		}
		if (currentEnergy < num)
		{
			return 0;
		}
		return num;
	}

	public virtual int CalculateCurrentEnergy(int inputAmount, int inputSlot)
	{
		return inputAmount;
	}

	public virtual int GetCurrentEnergy()
	{
		return Mathf.Clamp(currentEnergy - ConsumptionAmount(), 0, currentEnergy);
	}

	public virtual int GetPassthroughAmount(int outputSlot = 0)
	{
		if (outputSlot < 0 || outputSlot >= outputs.Length)
		{
			return 0;
		}
		int num = ((cachedOutputsUsed == 0) ? 1 : cachedOutputsUsed);
		return GetCurrentEnergy() / num;
	}

	public virtual void UpdateHasPower(int inputAmount, int inputSlot)
	{
		SetFlag(Flags.Reserved8, inputAmount >= ConsumptionAmount() && inputAmount > 0, recursive: false, networkupdate: false);
	}

	public void TouchInternal()
	{
		int num = GetPassthroughAmount();
		if (infiniteIoPower && GetQueueType() == QueueType.ElectricLowPriority)
		{
			num = 999;
		}
		bool num2 = lastPassthroughEnergy != num;
		lastPassthroughEnergy = num;
		if (num2)
		{
			IOStateChanged(currentEnergy, 0);
			ensureOutputsUpdated = true;
		}
		if (!PreventDuplicatesInQueue || !_processQueues[GetQueueType()].Contains(this))
		{
			_processQueues[GetQueueType()].Enqueue(this);
		}
	}

	public virtual void UpdateFromInput(int inputAmount, int inputSlot)
	{
		if (inputs[inputSlot].type != ioType || inputs[inputSlot].type == IOType.Industrial)
		{
			IOStateChanged(inputAmount, inputSlot);
			return;
		}
		UpdateHasPower(inputAmount, inputSlot);
		lastEnergy = currentEnergy;
		currentEnergy = CalculateCurrentEnergy(inputAmount, inputSlot);
		int num = GetPassthroughAmount();
		if (infiniteIoPower && GetQueueType() == QueueType.ElectricLowPriority)
		{
			num = 999;
		}
		bool flag = lastPassthroughEnergy != num;
		lastPassthroughEnergy = num;
		if (currentEnergy != lastEnergy || flag)
		{
			IOStateChanged(inputAmount, inputSlot);
			ensureOutputsUpdated = true;
		}
		_processQueues[GetQueueType()].Enqueue(this);
	}

	public virtual void TouchIOState()
	{
		if (!base.isClient)
		{
			TouchInternal();
		}
	}

	public virtual void SendIONetworkUpdate()
	{
		SendNetworkUpdate_Flags();
	}

	public bool IsFlickering()
	{
		if (changedCount > 5)
		{
			return UnityEngine.Time.realtimeSinceStartup - lastChangeTime < 1f;
		}
		return false;
	}

	public virtual void IOStateChanged(int inputAmount, int inputSlot)
	{
		if (UnityEngine.Time.realtimeSinceStartup - lastChangeTime > 1f)
		{
			changedCount = 1;
		}
		else
		{
			changedCount++;
		}
		lastChangeTime = UnityEngine.Time.realtimeSinceStartup;
	}

	public virtual void OnCircuitChanged(bool forceUpdate)
	{
		if (forceUpdate)
		{
			MarkDirtyForceUpdateOutputs();
		}
	}

	public virtual void SendChangedToRoot(bool forceUpdate)
	{
		List<IOEntity> existing = Facepunch.Pool.Get<List<IOEntity>>();
		SendChangedToRootRecursive(forceUpdate, ref existing);
		Facepunch.Pool.FreeUnmanaged(ref existing);
	}

	public virtual void SendChangedToRootRecursive(bool forceUpdate, ref List<IOEntity> existing)
	{
		bool flag = IsRootEntity();
		if (existing.Contains(this))
		{
			return;
		}
		existing.Add(this);
		bool flag2 = false;
		for (int i = 0; i < inputs.Length; i++)
		{
			IOSlot iOSlot = inputs[i];
			if (!iOSlot.mainPowerSlot)
			{
				continue;
			}
			IOEntity iOEntity = iOSlot.connectedTo.Get();
			if (!(iOEntity == null) && !existing.Contains(iOEntity))
			{
				flag2 = true;
				if (forceUpdate)
				{
					iOEntity.ensureOutputsUpdated = true;
				}
				iOEntity.SendChangedToRootRecursive(forceUpdate, ref existing);
			}
		}
		if (flag)
		{
			forceUpdate = forceUpdate && !flag2;
			OnCircuitChanged(forceUpdate);
		}
	}

	public void NotifyIndustrialNetworkChanged()
	{
		List<IOEntity> obj = Facepunch.Pool.Get<List<IOEntity>>();
		OnIndustrialNetworkChanged();
		NotifyIndustrialNetworkChanged(obj, input: true, 128);
		obj.Clear();
		NotifyIndustrialNetworkChanged(obj, input: false, 128);
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	private void NotifyIndustrialNetworkChanged(List<IOEntity> existing, bool input, int maxDepth)
	{
		if (maxDepth <= 0 || existing.Contains(this))
		{
			return;
		}
		if (existing.Count != 0)
		{
			OnIndustrialNetworkChanged();
		}
		existing.Add(this);
		IOSlot[] array = (input ? inputs : outputs);
		foreach (IOSlot iOSlot in array)
		{
			if (iOSlot.type == IOType.Industrial && iOSlot.connectedTo.Get() != null)
			{
				iOSlot.connectedTo.Get().NotifyIndustrialNetworkChanged(existing, input, maxDepth - 1);
			}
		}
	}

	protected virtual void OnIndustrialNetworkChanged()
	{
	}

	protected bool ShouldUpdateOutputs()
	{
		if (UnityEngine.Time.realtimeSinceStartup - lastUpdateTime < responsetime)
		{
			lastUpdateBlockedFrame = UnityEngine.Time.frameCount;
			_processQueues[GetQueueType()].Enqueue(this);
			return false;
		}
		lastUpdateTime = UnityEngine.Time.realtimeSinceStartup;
		SendIONetworkUpdate();
		if (outputs.Length == 0)
		{
			ensureOutputsUpdated = false;
			return false;
		}
		return true;
	}

	public virtual void UpdateOutputs()
	{
		if (!ShouldUpdateOutputs() || !ensureOutputsUpdated)
		{
			return;
		}
		ensureOutputsUpdated = false;
		using (TimeWarning.New("ProcessIOOutputs"))
		{
			for (int i = 0; i < outputs.Length; i++)
			{
				IOSlot iOSlot = outputs[i];
				bool flag = true;
				IOEntity iOEntity = iOSlot.connectedTo.Get();
				if (!(iOEntity != null))
				{
					continue;
				}
				if (ioType == IOType.Fluidic && !DisregardGravityRestrictionsOnLiquid && !iOEntity.DisregardGravityRestrictionsOnLiquid)
				{
					using (TimeWarning.New("FluidOutputProcessing"))
					{
						if (!iOEntity.AllowLiquidPassthrough(this, base.transform.TransformPoint(iOSlot.handlePosition)))
						{
							flag = false;
						}
					}
				}
				int passthroughAmount = GetPassthroughAmount(i);
				iOEntity.UpdateFromInput(flag ? passthroughAmount : 0, iOSlot.connectedToSlot);
			}
		}
	}

	public override void Spawn()
	{
		base.Spawn();
		if (!Rust.Application.isLoadingSave)
		{
			Init();
		}
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		Init();
	}

	public override void PostMapEntitySpawn()
	{
		base.PostMapEntitySpawn();
		Init();
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.ioEntity = Facepunch.Pool.Get<ProtoBuf.IOEntity>();
		info.msg.ioEntity.inputs = Facepunch.Pool.Get<List<ProtoBuf.IOEntity.IOConnection>>();
		info.msg.ioEntity.outputs = Facepunch.Pool.Get<List<ProtoBuf.IOEntity.IOConnection>>();
		IOSlot[] array = inputs;
		foreach (IOSlot iOSlot in array)
		{
			ProtoBuf.IOEntity.IOConnection iOConnection = Facepunch.Pool.Get<ProtoBuf.IOEntity.IOConnection>();
			iOConnection.connectedID = iOSlot.connectedTo.entityRef.uid;
			iOConnection.connectedToSlot = iOSlot.connectedToSlot;
			iOConnection.niceName = iOSlot.niceName;
			iOConnection.type = (int)iOSlot.type;
			iOConnection.inUse = iOConnection.connectedID.IsValid;
			iOConnection.colour = (int)iOSlot.wireColour;
			iOConnection.lineThickness = iOSlot.lineThickness;
			iOConnection.originPosition = iOSlot.originPosition;
			iOConnection.originRotation = iOSlot.originRotation;
			info.msg.ioEntity.inputs.Add(iOConnection);
		}
		array = outputs;
		foreach (IOSlot iOSlot2 in array)
		{
			ProtoBuf.IOEntity.IOConnection iOConnection2 = Facepunch.Pool.Get<ProtoBuf.IOEntity.IOConnection>();
			iOConnection2.connectedID = iOSlot2.connectedTo.entityRef.uid;
			iOConnection2.connectedToSlot = iOSlot2.connectedToSlot;
			iOConnection2.niceName = iOSlot2.niceName;
			iOConnection2.type = (int)iOSlot2.type;
			iOConnection2.inUse = iOConnection2.connectedID.IsValid;
			iOConnection2.colour = (int)iOSlot2.wireColour;
			iOConnection2.worldSpaceRotation = iOSlot2.worldSpaceLineEndRotation;
			iOConnection2.lineThickness = iOSlot2.lineThickness;
			iOConnection2.originPosition = iOSlot2.originPosition;
			iOConnection2.originRotation = iOSlot2.originRotation;
			if (iOSlot2.linePoints != null)
			{
				iOConnection2.linePointList = Facepunch.Pool.Get<List<ProtoBuf.IOEntity.IOConnection.LineVec>>();
				for (int j = 0; j < iOSlot2.linePoints.Length; j++)
				{
					Vector3 vector = iOSlot2.linePoints[j];
					ProtoBuf.IOEntity.IOConnection.LineVec lineVec = Facepunch.Pool.Get<ProtoBuf.IOEntity.IOConnection.LineVec>();
					lineVec.vec = vector;
					if (iOSlot2.slackLevels.Length > j)
					{
						lineVec.vec.w = iOSlot2.slackLevels[j];
					}
					iOConnection2.linePointList.Add(lineVec);
				}
			}
			if (iOSlot2.slackLevels != null)
			{
				iOConnection2.slackLevels = iOSlot2.slackLevels.ToList();
			}
			if (iOSlot2.lineAnchors != null)
			{
				iOConnection2.lineAnchorList = Facepunch.Pool.Get<List<WireLineAnchorInfo>>();
				for (int k = 0; k < iOSlot2.lineAnchors.Length; k++)
				{
					if (iOSlot2.lineAnchors[k].entityRef.IsValid(serverside: true))
					{
						WireLineAnchorInfo item = iOSlot2.lineAnchors[k].ToInfo();
						iOConnection2.lineAnchorList.Add(item);
					}
				}
			}
			info.msg.ioEntity.outputs.Add(iOConnection2);
		}
	}

	public virtual float IOInput(IOEntity from, IOType inputType, float inputAmount, int slot = 0)
	{
		IOSlot[] array = outputs;
		foreach (IOSlot iOSlot in array)
		{
			if (iOSlot.connectedTo.Get() != null)
			{
				inputAmount = iOSlot.connectedTo.Get().IOInput(this, iOSlot.type, inputAmount, iOSlot.connectedToSlot);
			}
		}
		return inputAmount;
	}

	public bool Disconnect(int index, bool isInput)
	{
		if (index >= (isInput ? inputs.Length : outputs.Length))
		{
			return false;
		}
		IOSlot iOSlot = (isInput ? inputs[index] : outputs[index]);
		if (iOSlot.connectedTo.Get() == null)
		{
			return false;
		}
		IOEntity iOEntity = iOSlot.connectedTo.Get();
		IOSlot obj = (isInput ? iOEntity.outputs[iOSlot.connectedToSlot] : iOEntity.inputs[iOSlot.connectedToSlot]);
		if (isInput)
		{
			UpdateFromInput(0, index);
		}
		else if ((bool)iOEntity)
		{
			iOEntity.UpdateFromInput(0, iOSlot.connectedToSlot);
		}
		iOSlot.Clear();
		obj.Clear();
		MarkDirtyForceUpdateOutputs();
		SendNetworkUpdateImmediate();
		RefreshIndustrialPreventBuilding();
		if (iOEntity != null)
		{
			iOEntity.RefreshIndustrialPreventBuilding();
		}
		if (isInput && iOEntity != null)
		{
			iOEntity.SendChangedToRoot(forceUpdate: true);
		}
		else if (!isInput)
		{
			IOSlot[] array = inputs;
			foreach (IOSlot iOSlot2 in array)
			{
				if (iOSlot2.mainPowerSlot && (bool)iOSlot2.connectedTo.Get())
				{
					iOSlot2.connectedTo.Get().SendChangedToRoot(forceUpdate: true);
				}
			}
		}
		iOEntity.SendNetworkUpdateImmediate();
		if (ioType == IOType.Industrial)
		{
			NotifyIndustrialNetworkChanged();
		}
		if (iOEntity != null && iOEntity.ioType == IOType.Industrial)
		{
			iOEntity.NotifyIndustrialNetworkChanged();
		}
		return true;
	}

	public void ConnectTo(IOEntity entity, int outputIndex, int inputIndex)
	{
		ConnectTo(entity, outputIndex, inputIndex, new List<Vector3>(), new List<float>(), new LineAnchor[0]);
	}

	public void ConnectTo(IOEntity entity, int outputIndex, int inputIndex, List<Vector3> points, List<float> slackLevels, LineAnchor[] lineAnchors, WireTool.WireColour colour = WireTool.WireColour.Gray)
	{
		IOSlot obj = entity.inputs[inputIndex];
		obj.connectedTo.Set(this);
		obj.connectedToSlot = outputIndex;
		obj.wireColour = colour;
		obj.connectedTo.Init();
		IOSlot obj2 = outputs[outputIndex];
		obj2.connectedTo.Set(entity);
		obj2.connectedToSlot = inputIndex;
		obj2.linePoints = points.ToArray();
		obj2.slackLevels = slackLevels.ToArray();
		obj2.lineAnchors = lineAnchors;
		obj2.wireColour = colour;
		obj2.connectedTo.Init();
		obj2.worldSpaceLineEndRotation = entity.transform.TransformDirection(entity.inputs[inputIndex].handleDirection);
		obj2.originPosition = base.transform.position;
		obj2.originRotation = base.transform.rotation.eulerAngles;
		MarkDirtyForceUpdateOutputs();
		SendNetworkUpdate();
		entity.SendNetworkUpdate();
		SendChangedToRoot(forceUpdate: true);
		RefreshIndustrialPreventBuilding();
	}

	public void FindContainerSource(List<ContainerInputOutput> found, int depth, bool input, List<IOEntity> ignoreList, int parentId = -1, int stackSize = 0)
	{
		if (depth <= 0 || found.Count >= 32)
		{
			return;
		}
		int num = 0;
		int num2 = 1;
		IOSlot[] array;
		if (!input)
		{
			num2 = 0;
			array = outputs;
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i].type == IOType.Industrial)
				{
					num2++;
				}
			}
		}
		List<int> obj = Facepunch.Pool.Get<List<int>>();
		array = (input ? inputs : outputs);
		foreach (IOSlot iOSlot in array)
		{
			num++;
			if (iOSlot.type != IOType.Industrial)
			{
				continue;
			}
			IOEntity iOEntity = iOSlot.connectedTo.Get(base.isServer);
			if (!(iOEntity != null) || ignoreList.Contains(iOEntity))
			{
				continue;
			}
			int num3 = -1;
			if (iOEntity is IIndustrialStorage storage2)
			{
				num = iOSlot.connectedToSlot;
				if (GetExistingCount(storage2) < 2)
				{
					found.Add(new ContainerInputOutput
					{
						SlotIndex = num,
						Storage = storage2,
						ParentStorage = parentId,
						MaxStackSize = stackSize / num2
					});
					num3 = found.Count - 1;
					obj.Add(num3);
				}
			}
			else
			{
				ignoreList.Add(iOEntity);
			}
			if ((!(iOEntity is IIndustrialStorage) || iOEntity is IndustrialStorageAdaptor) && !(iOEntity is IndustrialConveyor) && iOEntity != null)
			{
				iOEntity.FindContainerSource(found, depth - 1, input, ignoreList, (num3 == -1) ? parentId : num3, stackSize / num2);
			}
		}
		int count = obj.Count;
		foreach (int item in obj)
		{
			ContainerInputOutput value = found[item];
			value.IndustrialSiblingCount = count;
			found[item] = value;
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		int GetExistingCount(IIndustrialStorage storage)
		{
			int num4 = 0;
			foreach (ContainerInputOutput item2 in found)
			{
				if (item2.Storage == storage)
				{
					num4++;
				}
			}
			return num4;
		}
	}

	public virtual bool AllowLiquidPassthrough(IOEntity fromSource, Vector3 sourceWorldPosition, bool forPlacement = false)
	{
		if (fromSource.DisregardGravityRestrictionsOnLiquid || DisregardGravityRestrictionsOnLiquid)
		{
			return true;
		}
		if (inputs.Length == 0)
		{
			return false;
		}
		Vector3 vector = base.transform.TransformPoint(inputs[0].handlePosition);
		float num = sourceWorldPosition.y - vector.y;
		if (num > 0f)
		{
			return true;
		}
		if (Mathf.Abs(num) < LiquidPassthroughGravityThreshold)
		{
			return true;
		}
		return false;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.ioEntity == null)
		{
			return;
		}
		if (!info.fromDisk && info.msg.ioEntity.inputs != null)
		{
			int count = info.msg.ioEntity.inputs.Count;
			if (inputs.Length != count)
			{
				inputs = new IOSlot[count];
			}
			for (int i = 0; i < count; i++)
			{
				if (inputs[i] == null)
				{
					inputs[i] = new IOSlot();
				}
				ProtoBuf.IOEntity.IOConnection iOConnection = info.msg.ioEntity.inputs[i];
				inputs[i].connectedTo = new IORef();
				inputs[i].connectedTo.entityRef.uid = iOConnection.connectedID;
				if (base.isClient)
				{
					inputs[i].connectedTo.InitClient();
				}
				inputs[i].connectedToSlot = iOConnection.connectedToSlot;
				inputs[i].niceName = iOConnection.niceName;
				inputs[i].type = (IOType)iOConnection.type;
				inputs[i].wireColour = (WireTool.WireColour)iOConnection.colour;
				inputs[i].lineThickness = iOConnection.lineThickness;
				inputs[i].originPosition = iOConnection.originPosition;
				inputs[i].originRotation = iOConnection.originRotation;
			}
		}
		if (info.msg.ioEntity.outputs != null)
		{
			int count2 = info.msg.ioEntity.outputs.Count;
			IOSlot[] array = null;
			if (outputs.Length != count2 && count2 > 0)
			{
				array = outputs;
				outputs = new IOSlot[count2];
				for (int j = 0; j < array.Length; j++)
				{
					if (j < count2)
					{
						outputs[j] = array[j];
					}
				}
			}
			for (int k = 0; k < count2; k++)
			{
				if (outputs[k] == null)
				{
					outputs[k] = new IOSlot();
				}
				ProtoBuf.IOEntity.IOConnection iOConnection2 = info.msg.ioEntity.outputs[k];
				if (iOConnection2.linePointList == null || iOConnection2.linePointList.Count == 0 || !iOConnection2.connectedID.IsValid)
				{
					outputs[k].Clear();
				}
				outputs[k].connectedTo = new IORef();
				outputs[k].connectedTo.entityRef.uid = iOConnection2.connectedID;
				if (base.isClient)
				{
					outputs[k].connectedTo.InitClient();
				}
				outputs[k].connectedToSlot = iOConnection2.connectedToSlot;
				outputs[k].niceName = iOConnection2.niceName;
				outputs[k].type = (IOType)iOConnection2.type;
				outputs[k].wireColour = (WireTool.WireColour)iOConnection2.colour;
				outputs[k].worldSpaceLineEndRotation = iOConnection2.worldSpaceRotation;
				outputs[k].lineThickness = iOConnection2.lineThickness;
				outputs[k].originPosition = (info.fromCopy ? base.transform.position : iOConnection2.originPosition);
				outputs[k].originRotation = (info.fromCopy ? base.transform.rotation.eulerAngles : iOConnection2.originRotation);
				if (!info.fromDisk && !base.isClient)
				{
					continue;
				}
				List<ProtoBuf.IOEntity.IOConnection.LineVec> list = iOConnection2.linePointList ?? new List<ProtoBuf.IOEntity.IOConnection.LineVec>();
				if (outputs[k].linePoints == null || outputs[k].linePoints.Length != list.Count)
				{
					outputs[k].linePoints = new Vector3[list.Count];
				}
				if (outputs[k].slackLevels == null || outputs[k].slackLevels.Length != list.Count)
				{
					outputs[k].slackLevels = new float[list.Count];
				}
				for (int l = 0; l < list.Count; l++)
				{
					outputs[k].linePoints[l] = list[l].vec;
					outputs[k].slackLevels[l] = list[l].vec.w;
				}
				List<WireLineAnchorInfo> list2 = iOConnection2.lineAnchorList ?? new List<WireLineAnchorInfo>();
				if (outputs[k].lineAnchors == null || outputs[k].lineAnchors.Length != list2.Count)
				{
					outputs[k].lineAnchors = new LineAnchor[list2.Count];
				}
				for (int m = 0; m < list2.Count; m++)
				{
					WireLineAnchorInfo wireLineAnchorInfo = list2[m];
					if (wireLineAnchorInfo.parentID.IsValid)
					{
						LineAnchor lineAnchor = new LineAnchor(wireLineAnchorInfo);
						outputs[k].lineAnchors[m] = lineAnchor;
					}
				}
			}
		}
		RefreshIndustrialPreventBuilding();
	}

	public int GetConnectedInputCount()
	{
		int num = 0;
		IOSlot[] array = inputs;
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].connectedTo.Get(base.isServer) != null)
			{
				num++;
			}
		}
		return num;
	}

	public int GetConnectedOutputCount()
	{
		int num = 0;
		IOSlot[] array = outputs;
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].connectedTo.Get(base.isServer) != null)
			{
				num++;
			}
		}
		return num;
	}

	public bool HasConnections()
	{
		if (GetConnectedInputCount() <= 0)
		{
			return GetConnectedOutputCount() > 0;
		}
		return true;
	}

	public override void DestroyShared()
	{
		base.DestroyShared();
		ClearIndustrialPreventBuilding();
	}

	public void RefreshIndustrialPreventBuilding()
	{
		ClearIndustrialPreventBuilding();
		Matrix4x4 localToWorldMatrix = base.transform.localToWorldMatrix;
		for (int i = 0; i < outputs.Length; i++)
		{
			IOSlot iOSlot = outputs[i];
			if (iOSlot.type != IOType.Industrial || iOSlot.linePoints == null || iOSlot.linePoints.Length <= 1)
			{
				continue;
			}
			Vector3 vector = localToWorldMatrix.MultiplyPoint3x4(iOSlot.linePoints[0]);
			for (int j = 1; j < iOSlot.linePoints.Length; j++)
			{
				Vector3 vector2 = localToWorldMatrix.MultiplyPoint3x4(iOSlot.linePoints[j]);
				Vector3 pos = Vector3.Lerp(vector2, vector, 0.5f);
				float num = Vector3.Distance(vector2, vector);
				Quaternion rot = (((vector2 - vector).normalized != Vector3.zero) ? Quaternion.LookRotation((vector2 - vector).normalized) : Quaternion.identity);
				GameObject obj = base.gameManager.CreatePrefab("assets/prefabs/misc/ioentitypreventbuilding.prefab", pos, rot);
				obj.transform.SetParent(base.transform);
				if (obj.TryGetComponent<CapsuleCollider>(out var component))
				{
					component.height = num + component.radius;
					spawnedColliders.Add(component);
				}
				if (obj.TryGetComponent<ColliderInfo_Pipe>(out var component2))
				{
					component2.OutputSlotIndex = i;
					component2.ParentEntity = this;
				}
				vector = vector2;
			}
		}
	}

	private void ClearIndustrialPreventBuilding()
	{
		foreach (Collider spawnedCollider in spawnedColliders)
		{
			base.gameManager.Retire(spawnedCollider.gameObject);
		}
		spawnedColliders.Clear();
	}
}
