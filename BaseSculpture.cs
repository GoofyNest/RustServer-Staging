#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Facepunch.MarchingCubes;
using LZ4;
using Network;
using ProtoBuf;
using Rust;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

public class BaseSculpture : IOEntity, IServerFileReceiver, IUGCBrowserEntity, ISplashable, IDisposable
{
	[Serializable]
	public struct ColorSetting
	{
		public GameObject toggleObj;

		public Translate.Phrase name;

		public Translate.Phrase desc;

		public Color color;

		[ColorUsage(false, true)]
		public Color materialColor;
	}

	[Header("BaseSculpture")]
	[SerializeField]
	private MeshFilter targetMesh;

	[SerializeField]
	private MeshCollider clientMeshCollider;

	[SerializeField]
	private Renderer clientBlockRenderer;

	[SerializeField]
	private DamageType carvingDamageType;

	[SerializeField]
	private Vector3Int gridResolution = new Vector3Int(32, 32, 32);

	[SerializeField]
	private Vector3 gridOffset;

	[SerializeField]
	private float gridScale;

	[SerializeField]
	private GameObjectRef blockImpactEffect;

	[SerializeField]
	private Collider blockerCollider;

	[Header("HitGuide")]
	[SerializeField]
	private GameObject hitGuide;

	[SerializeField]
	private GameObject carvingGuide;

	[SerializeField]
	private GameObject smoothingGuide;

	[SerializeField]
	private float guideLerpSpeed = 5f;

	[SerializeField]
	private Vector3 carveColorMultiplier;

	[SerializeField]
	private Vector3 smoothColorMultiplier;

	[Header("IO")]
	[SerializeField]
	private ColorSetting[] colorSettings;

	[SerializeField]
	private Renderer[] lightRenderers;

	[SerializeField]
	private Material noLightMaterial;

	[SerializeField]
	private Material lightMaterial;

	public const int CarveDepth = 3;

	private Point3DGrid _grid;

	private uint _crc = uint.MaxValue;

	private int _currentColorIndex;

	private bool _hasMovementBlocker;

	private Transform _movementBlockerTransform;

	private int _cachedMaxY;

	private int _carveRadius;

	private int _minCarveRadius;

	private int _maxCarveRadius;

	private static readonly byte[] _decompressArr = new byte[8192];

	[ClientVar(Default = "false")]
	public static bool LogMeshStats = false;

	private static readonly ListHashSet<BaseSculpture> ServerUpdateProcessQueue = new ListHashSet<BaseSculpture>();

	private bool _gridDirty;

	private Action _resetSplashedThisFrame;

	private bool _splashedThisFrame;

	private int CarveRadius
	{
		get
		{
			return _carveRadius;
		}
		set
		{
			_carveRadius = math.clamp(value, _minCarveRadius, _maxCarveRadius);
		}
	}

	public uint[] GetContentCRCs => new uint[1] { _crc };

	public UGCType ContentType => UGCType.Sculpt;

	public List<ulong> EditingHistory => new List<ulong> { base.OwnerID };

	public BaseNetworkable UgcEntity => this;

	public string ContentString => string.Empty;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("BaseSculpture.OnRpcMessage"))
		{
			if (rpc == 3180266995u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SV_Add ");
				}
				using (TimeWarning.New("SV_Add"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(3180266995u, "SV_Add", this, player, 3f))
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
							SV_Add(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in SV_Add");
					}
				}
				return true;
			}
			if (rpc == 737203553 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SV_Carve ");
				}
				using (TimeWarning.New("SV_Carve"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(737203553u, "SV_Carve", this, player, 3f))
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
							SV_Carve(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in SV_Carve");
					}
				}
				return true;
			}
			if (rpc == 3650562316u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SV_LoadFromData ");
				}
				using (TimeWarning.New("SV_LoadFromData"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3650562316u, "SV_LoadFromData", this, player, 1uL))
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
							RPCMessage msg4 = rPCMessage;
							SV_LoadFromData(msg4);
						}
					}
					catch (Exception exception3)
					{
						Debug.LogException(exception3);
						player.Kick("RPC Error in SV_LoadFromData");
					}
				}
				return true;
			}
			if (rpc == 4267718869u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SV_LockSculpture ");
				}
				using (TimeWarning.New("SV_LockSculpture"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(4267718869u, "SV_LockSculpture", this, player, 3f))
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
							RPCMessage msg5 = rPCMessage;
							SV_LockSculpture(msg5);
						}
					}
					catch (Exception exception4)
					{
						Debug.LogException(exception4);
						player.Kick("RPC Error in SV_LockSculpture");
					}
				}
				return true;
			}
			if (rpc == 2374043062u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SV_SetColorIndex ");
				}
				using (TimeWarning.New("SV_SetColorIndex"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(2374043062u, "SV_SetColorIndex", this, player, 3f))
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
							RPCMessage msg6 = rPCMessage;
							SV_SetColorIndex(msg6);
						}
					}
					catch (Exception exception5)
					{
						Debug.LogException(exception5);
						player.Kick("RPC Error in SV_SetColorIndex");
					}
				}
				return true;
			}
			if (rpc == 2622097655u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SV_Smooth ");
				}
				using (TimeWarning.New("SV_Smooth"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(2622097655u, "SV_Smooth", this, player, 3f))
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
							SV_Smooth(msg7);
						}
					}
					catch (Exception exception6)
					{
						Debug.LogException(exception6);
						player.Kick("RPC Error in SV_Smooth");
					}
				}
				return true;
			}
			if (rpc == 1358295833 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SV_UnlockSculpture ");
				}
				using (TimeWarning.New("SV_UnlockSculpture"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(1358295833u, "SV_UnlockSculpture", this, player, 3f))
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
							SV_UnlockSculpture(msg8);
						}
					}
					catch (Exception exception7)
					{
						Debug.LogException(exception7);
						player.Kick("RPC Error in SV_UnlockSculpture");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void InitShared()
	{
		base.InitShared();
		_grid = new Point3DGrid(gridResolution.x, gridResolution.y, gridResolution.z);
		_hasMovementBlocker = blockerCollider != null;
		if (_hasMovementBlocker)
		{
			_movementBlockerTransform = blockerCollider.transform;
			_cachedMaxY = gridResolution.y - 1;
		}
	}

	public override void ServerInit()
	{
		base.ServerInit();
		if (_crc == uint.MaxValue)
		{
			FillGrid(_grid);
		}
		else
		{
			byte[] array = FileStorage.server.Get(_crc, FileStorage.Type.sculpt, net.ID);
			if (array == null)
			{
				Debug.LogError("Missing sculpt data on-disk - fill with default");
				FillGrid(_grid);
			}
			else
			{
				PopulateGridFromEncodedData(array);
			}
		}
		MarkServerGridUpdate();
		_resetSplashedThisFrame = ResetSplashedThisFrame;
	}

	public bool CanUpdateSculpture(BasePlayer player)
	{
		if (player.IsAdmin || player.IsDeveloper)
		{
			return true;
		}
		if (!player.CanBuild())
		{
			return false;
		}
		if (IsLocked())
		{
			return (ulong)player.userID == base.OwnerID;
		}
		return true;
	}

	public override void OnAttacked(HitInfo info)
	{
		if (info.damageTypes.Contains(carvingDamageType) && base.isServer)
		{
			info.DidHit = false;
			info.DoHitEffects = false;
		}
		else
		{
			base.OnAttacked(info);
		}
	}

	private void PopulateGridFromEncodedData(byte[] encoded)
	{
		int count = LZ4Codec.Decode(encoded, 0, encoded.Length, _decompressArr, 0, _decompressArr.Length);
		_grid.CopyFromByteArray(_decompressArr, count);
	}

	private static void FillGrid(Point3DGrid grid)
	{
		for (int i = 1; i < grid.Width - 1; i++)
		{
			for (int j = 1; j < grid.Height - 1; j++)
			{
				for (int k = 1; k < grid.Depth - 1; k++)
				{
					grid[i, j, k] = true;
				}
			}
		}
	}

	private int3 GetInBlockSpace(Vector3 worldSpace)
	{
		return new int3(GetInBlockSpaceFloat(worldSpace));
	}

	private float3 GetInBlockSpaceFloat(Vector3 worldSpace)
	{
		Vector3 vector = clientMeshCollider.transform.InverseTransformPoint(worldSpace);
		Vector3 vector2 = new Vector3(gridResolution.x, gridResolution.y, gridResolution.z) * 0.5f;
		return vector * (1f / gridScale) + (vector2 + gridOffset);
	}

	private Vector3 GetInWorldSpace(int3 blockSpace)
	{
		Vector3 vector = new Vector3(gridResolution.x, gridResolution.y, gridResolution.z) * 0.5f;
		Vector3 position = ((Vector3)(float3)blockSpace - (vector + gridOffset)) * gridScale;
		return clientMeshCollider.transform.TransformPoint(position);
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.baseSculpture = Facepunch.Pool.Get<ProtoBuf.BaseSculpture>();
		info.msg.baseSculpture.crc = _crc;
		info.msg.baseSculpture.colourSelection = _currentColorIndex;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.baseSculpture == null)
		{
			return;
		}
		uint crc = _crc;
		_ = _currentColorIndex;
		_crc = info.msg.baseSculpture.crc;
		_currentColorIndex = info.msg.baseSculpture.colourSelection;
		if (base.isServer && info.fromDisk && crc != _crc)
		{
			byte[] array = FileStorage.server.Get(_crc, FileStorage.Type.sculpt, net.ID);
			if (array == null)
			{
				Debug.LogError("Missing sculpt data on-disk - fill with default");
				FillGrid(_grid);
			}
			else
			{
				PopulateGridFromEncodedData(array);
				MarkServerGridUpdate();
			}
		}
	}

	private void UpdateMovementBlocker()
	{
		using (TimeWarning.New("UpdateMovementBlocker"))
		{
			if (_hasMovementBlocker)
			{
				int num = FindMaxY();
				if (num <= 0)
				{
					blockerCollider.enabled = false;
					return;
				}
				float y = (float)num / (float)gridResolution.y;
				_movementBlockerTransform.localScale = _movementBlockerTransform.localScale.WithY(y);
			}
		}
		int FindMaxY()
		{
			for (int num2 = _cachedMaxY; num2 >= 0; num2--)
			{
				for (int i = 0; i < gridResolution.x; i++)
				{
					for (int j = 0; j < gridResolution.z; j++)
					{
						if (_grid[i, num2, j])
						{
							_cachedMaxY = num2;
							return num2;
						}
					}
				}
			}
			return -1;
		}
	}

	public override void DestroyShared()
	{
		base.DestroyShared();
		Dispose();
	}

	public void Dispose()
	{
		_grid.Dispose();
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	public void SV_SetColorIndex(RPCMessage msg)
	{
		int num = msg.read.Int32();
		if (num >= 0 && num < colorSettings.Length)
		{
			_currentColorIndex = num;
			SendNetworkUpdate();
		}
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(1uL)]
	public void SV_LoadFromData(RPCMessage msg)
	{
		if (msg.player.IsAdmin && msg.player.IsDeveloper)
		{
			ArraySegment<byte> arraySegment = msg.read.PooledBytes();
			int count = LZ4Codec.Decode(arraySegment.Array, arraySegment.Offset, arraySegment.Count, _decompressArr, 0, _decompressArr.Length);
			_grid.CopyFromByteArray(_decompressArr, count);
			MarkServerGridUpdate();
		}
	}

	private bool TryGetHeldCarvingAttributeServer(BasePlayer player, out SculptingToolData attribute)
	{
		attribute = null;
		if (player == null)
		{
			return false;
		}
		HeldEntity heldEntity = player.GetHeldEntity();
		if (heldEntity == null)
		{
			return false;
		}
		attribute = PrefabAttribute.server.Find<SculptingToolData>(heldEntity.prefabID);
		if (attribute == null)
		{
			return false;
		}
		return true;
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	public void SV_Add(RPCMessage msg)
	{
		if (!IsLocked())
		{
			Vector3 worldSpacePosition = msg.read.Vector3();
			if (TryGetHeldCarvingAttributeServer(msg.player, out var attribute) && attribute.AllowCarve)
			{
				int r = Mathf.Clamp(msg.read.Int32(), attribute.MinCarvingSize, attribute.MaxCarvingSize);
				AddSphere(worldSpacePosition, r);
			}
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	public void SV_Carve(RPCMessage msg)
	{
		if (IsLocked())
		{
			return;
		}
		Vector3 worldSpacePosition = msg.read.Vector3();
		if (TryGetHeldCarvingAttributeServer(msg.player, out var attribute) && attribute.AllowCarve)
		{
			int r = Mathf.Clamp(msg.read.Int32(), attribute.MinCarvingSize, attribute.MaxCarvingSize);
			switch (attribute.CarvingShape)
			{
			case SculptingToolData.CarvingShapeType.Cylinder:
				CarveCylinder(worldSpacePosition, msg.player.eyes.HeadForward(), r, 3);
				break;
			case SculptingToolData.CarvingShapeType.Sphere:
				CarveSphere(worldSpacePosition, r);
				break;
			case SculptingToolData.CarvingShapeType.Rectangle:
				break;
			}
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	public void SV_Smooth(RPCMessage msg)
	{
		if (IsLocked())
		{
			return;
		}
		Vector3 worldSpacePosition = msg.read.Vector3();
		if (TryGetHeldCarvingAttributeServer(msg.player, out var attribute) && attribute.AllowSmooth)
		{
			int r = Mathf.Clamp(msg.read.Int32(), attribute.MinCarvingSize, attribute.MaxCarvingSize);
			switch (attribute.CarvingShape)
			{
			case SculptingToolData.CarvingShapeType.Cylinder:
				SmoothCylinder(worldSpacePosition, msg.player.eyes.HeadForward(), r, 3);
				break;
			case SculptingToolData.CarvingShapeType.Sphere:
				SmoothSphere(worldSpacePosition, r);
				break;
			case SculptingToolData.CarvingShapeType.Rectangle:
				break;
			}
		}
	}

	private void CarveCylinder(Vector3 worldSpacePosition, Vector3 worldSpaceView, int r, int depth)
	{
		Vector3 vector = clientMeshCollider.transform.InverseTransformDirection(worldSpaceView);
		float3 inBlockSpaceFloat = GetInBlockSpaceFloat(worldSpacePosition);
		CarveAndBlurCylinderJob carveAndBlurCylinderJob = default(CarveAndBlurCylinderJob);
		carveAndBlurCylinderJob.Grid = _grid;
		carveAndBlurCylinderJob.P0 = inBlockSpaceFloat;
		carveAndBlurCylinderJob.P1 = inBlockSpaceFloat + (float3)vector * (float)depth;
		carveAndBlurCylinderJob.R = r;
		CarveAndBlurCylinderJob jobData = carveAndBlurCylinderJob;
		IJobExtensions.RunByRef(ref jobData);
		MarkServerGridUpdate();
	}

	private void SmoothCylinder(Vector3 worldSpacePosition, Vector3 worldSpaceView, int r, int depth)
	{
		Vector3 vector = clientMeshCollider.transform.InverseTransformDirection(worldSpaceView);
		float3 inBlockSpaceFloat = GetInBlockSpaceFloat(worldSpacePosition);
		BoxBlurCylinderJob boxBlurCylinderJob = default(BoxBlurCylinderJob);
		boxBlurCylinderJob.Grid = _grid;
		boxBlurCylinderJob.P0 = inBlockSpaceFloat;
		boxBlurCylinderJob.P1 = inBlockSpaceFloat + (float3)vector * (float)depth;
		boxBlurCylinderJob.R = r;
		BoxBlurCylinderJob jobData = boxBlurCylinderJob;
		IJobExtensions.RunByRef(ref jobData);
		MarkServerGridUpdate();
	}

	private void AddSphere(Vector3 worldSpacePosition, int r)
	{
		int3 inBlockSpace = GetInBlockSpace(worldSpacePosition);
		AddAndBlurSphereJob addAndBlurSphereJob = default(AddAndBlurSphereJob);
		addAndBlurSphereJob.Grid = _grid;
		addAndBlurSphereJob.Origin = inBlockSpace;
		addAndBlurSphereJob.R = r;
		AddAndBlurSphereJob jobData = addAndBlurSphereJob;
		IJobExtensions.RunByRef(ref jobData);
		MarkServerGridUpdate();
	}

	private void CarveSphere(Vector3 worldSpacePosition, int r)
	{
		int3 inBlockSpace = GetInBlockSpace(worldSpacePosition);
		CarveAndBlurSphereJob carveAndBlurSphereJob = default(CarveAndBlurSphereJob);
		carveAndBlurSphereJob.Grid = _grid;
		carveAndBlurSphereJob.Origin = inBlockSpace;
		carveAndBlurSphereJob.R = r;
		CarveAndBlurSphereJob jobData = carveAndBlurSphereJob;
		IJobExtensions.RunByRef(ref jobData);
		MarkServerGridUpdate();
	}

	private void SmoothSphere(Vector3 worldSpacePosition, int r)
	{
		int3 inBlockSpace = GetInBlockSpace(worldSpacePosition);
		BoxBlurSphereJob boxBlurSphereJob = default(BoxBlurSphereJob);
		boxBlurSphereJob.Grid = _grid;
		boxBlurSphereJob.Origin = inBlockSpace;
		boxBlurSphereJob.R = r;
		BoxBlurSphereJob jobData = boxBlurSphereJob;
		IJobExtensions.RunByRef(ref jobData);
		MarkServerGridUpdate();
	}

	public bool WantsSplash(ItemDefinition splashType, int amount)
	{
		if (!IsLocked())
		{
			return splashType != WaterTypes.RadioactiveWaterItemDef;
		}
		return false;
	}

	private void ResetSplashedThisFrame()
	{
		_splashedThisFrame = false;
	}

	public int DoSplash(ItemDefinition splashType, int amount)
	{
		if (_splashedThisFrame)
		{
			return 0;
		}
		if (amount < 200)
		{
			return amount;
		}
		_splashedThisFrame = true;
		Invoke(_resetSplashedThisFrame, 0f);
		Debug.Log("Splash");
		if (splashType == WaterTypes.WaterItemDef)
		{
			NativeBitArray other = new NativeBitArray(_grid.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			BoxBlur3DJob boxBlur3DJob = default(BoxBlur3DJob);
			boxBlur3DJob.InputGrid = _grid;
			boxBlur3DJob.OutputGrid = other;
			boxBlur3DJob.Width = _grid.Width;
			boxBlur3DJob.WidthHeight = _grid.Width * _grid.Height;
			BoxBlur3DJob jobData = boxBlur3DJob;
			IJobForExtensions.RunByRef(ref jobData, _grid.Length);
			_grid.CopyFromNativeBitArray(ref other);
			other.Dispose();
			MarkServerGridUpdate();
		}
		return 200;
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	public void SV_LockSculpture(RPCMessage msg)
	{
		if (msg.player.CanInteract() && CanUpdateSculpture(msg.player))
		{
			SetFlag(Flags.Locked, b: true);
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	public void SV_UnlockSculpture(RPCMessage msg)
	{
		if (msg.player.CanInteract() && CanUpdateSculpture(msg.player))
		{
			SetFlag(Flags.Locked, b: false);
		}
	}

	private void MarkServerGridUpdate()
	{
		if (!_gridDirty)
		{
			_gridDirty = true;
			ServerUpdateProcessQueue.Add(this);
		}
	}

	private JobHandle ScheduleRemoveIslandsFromGrid()
	{
		CleanFloatingIslandsJob jobData = default(CleanFloatingIslandsJob);
		jobData.Sampler = _grid;
		return jobData.Schedule();
	}

	public static void ProcessGridUpdates()
	{
		if (ServerUpdateProcessQueue.Count == 0)
		{
			return;
		}
		using (TimeWarning.New("RemoveIslandsFromGrid"))
		{
			NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(ServerUpdateProcessQueue.Count, Allocator.Temp);
			for (int i = 0; i < ServerUpdateProcessQueue.Count; i++)
			{
				BaseSculpture baseSculpture = ServerUpdateProcessQueue[i];
				if (!(baseSculpture == null))
				{
					jobs[i] = baseSculpture.ScheduleRemoveIslandsFromGrid();
				}
			}
			JobHandle.CompleteAll(jobs);
		}
		using (TimeWarning.New("FileUpdates"))
		{
			for (int j = 0; j < ServerUpdateProcessQueue.Count; j++)
			{
				BaseSculpture baseSculpture2 = ServerUpdateProcessQueue[j];
				if (!(baseSculpture2 == null))
				{
					baseSculpture2.ServerGridUpdate();
				}
			}
		}
		ServerUpdateProcessQueue.Clear();
	}

	private void ServerGridUpdate()
	{
		byte[] arr = FileStorage.server.Get(_crc, FileStorage.Type.sculpt, net.ID);
		bool num = arr != null;
		if (!num)
		{
			arr = Array.Empty<byte>();
		}
		_grid.CopyToByteArray(ref arr);
		if (num)
		{
			FileStorage.server.Remove(_crc, FileStorage.Type.sculpt, net.ID);
		}
		arr = LZ4Codec.Encode(arr, 0, arr.Length);
		_crc = FileStorage.server.Store(arr, FileStorage.Type.sculpt, net.ID);
		InvalidateNetworkCache();
		ClientRPC(RpcTarget.NetworkGroup("CL_UpdateCrc"), _crc);
		UpdateMovementBlocker();
		_gridDirty = false;
	}

	public override void OnPickedUpPreItemMove(Item createdItem, BasePlayer player)
	{
		base.OnPickedUpPreItemMove(createdItem, player);
		if (_crc != uint.MaxValue && createdItem.info.TryGetComponent<ItemModSculpture>(out var component))
		{
			component.OnSculpturePickUp(net.ID, _crc, createdItem);
		}
	}

	public override void OnDeployed(BaseEntity parent, BasePlayer deployedBy, Item fromItem)
	{
		base.OnDeployed(parent, deployedBy, fromItem);
		if (!fromItem.info.HasComponent<ItemModSculpture>())
		{
			return;
		}
		AssociatedSculptureStorage associatedEntity = ItemModAssociatedEntity<AssociatedSculptureStorage>.GetAssociatedEntity(fromItem);
		if (associatedEntity != null)
		{
			_crc = associatedEntity.Crc;
			FileStorage.server.ReassignEntityId(associatedEntity.net.ID, net.ID);
			byte[] array = FileStorage.server.Get(_crc, FileStorage.Type.sculpt, net.ID);
			if (array == null)
			{
				Debug.LogError("Missing sculpt data on-disk - fill with default");
				FillGrid(_grid);
			}
			else
			{
				PopulateGridFromEncodedData(array);
				InvalidateNetworkCache();
				ClientRPC(RpcTarget.NetworkGroup("CL_UpdateCrc"), _crc);
			}
		}
	}

	public void ClearContent()
	{
		FillGrid(_grid);
		MarkServerGridUpdate();
	}

	internal override void DoServerDestroy()
	{
		base.DoServerDestroy();
		FileStorage.server.RemoveAllByEntity(net.ID);
	}

	public override void ResetIOState()
	{
		base.ResetIOState();
		SetFlag(Flags.On, b: false);
	}

	public override void UpdateFromInput(int inputAmount, int inputSlot)
	{
		base.UpdateFromInput(inputAmount, inputSlot);
		SetFlag(Flags.On, IsPowered());
	}
}
