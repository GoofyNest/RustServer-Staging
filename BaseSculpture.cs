#define UNITY_ASSERTIONS
using System;
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

public class BaseSculpture : BaseCombatEntity, IServerFileReceiver, IDisposable
{
	[Header("BaseSculpture")]
	[SerializeField]
	private MeshFilter targetMesh;

	[SerializeField]
	private MeshCollider clientMeshCollider;

	[SerializeField]
	private DamageType carvingDamageType;

	[SerializeField]
	private Vector3Int gridResolution = new Vector3Int(32, 32, 32);

	[SerializeField]
	private Vector3 gridOffset;

	[SerializeField]
	private float gridScale;

	[Header("HitGuide")]
	[SerializeField]
	private GameObject hitGuide;

	[SerializeField]
	private GameObject carvingGuide;

	[SerializeField]
	private GameObject smoothingGuide;

	[SerializeField]
	private float guideLerpSpeed = 5f;

	private static int _carveRadius;

	public const int MinCarveRadius = 2;

	public const int MaxCarveRadius = 6;

	private Point3DGrid _grid;

	private uint _crc = uint.MaxValue;

	private static readonly byte[] _decompressArr = new byte[8192];

	[ClientVar(Default = "false")]
	public static bool LogMeshStats = false;

	private static readonly ListHashSet<BaseSculpture> ServerUpdateProcessQueue = new ListHashSet<BaseSculpture>();

	private bool _gridDirty;

	[ClientVar]
	public bool ToolIsSmoothing { get; set; }

	[ClientVar(Default = "3")]
	public static int CarveRadius
	{
		get
		{
			return _carveRadius;
		}
		set
		{
			_carveRadius = math.clamp(value, 2, 6);
		}
	}

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("BaseSculpture.OnRpcMessage"))
		{
			if (rpc == 4082449050u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SV_CarveSphere ");
				}
				using (TimeWarning.New("SV_CarveSphere"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(4082449050u, "SV_CarveSphere", this, player, 3f))
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
							SV_CarveSphere(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in SV_CarveSphere");
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
							RPCMessage msg3 = rPCMessage;
							SV_LockSculpture(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in SV_LockSculpture");
					}
				}
				return true;
			}
			if (rpc == 3720219864u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SV_SmoothSphere ");
				}
				using (TimeWarning.New("SV_SmoothSphere"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(3720219864u, "SV_SmoothSphere", this, player, 3f))
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
							SV_SmoothSphere(msg4);
						}
					}
					catch (Exception exception3)
					{
						Debug.LogException(exception3);
						player.Kick("RPC Error in SV_SmoothSphere");
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
							RPCMessage msg5 = rPCMessage;
							SV_UnlockSculpture(msg5);
						}
					}
					catch (Exception exception4)
					{
						Debug.LogException(exception4);
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
		if (!info.damageTypes.Contains(carvingDamageType) || !base.isServer)
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
		Vector3 vector = clientMeshCollider.transform.InverseTransformPoint(worldSpace);
		Vector3 vector2 = new Vector3(gridResolution.x, gridResolution.y, gridResolution.z) * 0.5f;
		return new int3(vector * (1f / gridScale) + (vector2 + gridOffset));
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.baseSculpture = Facepunch.Pool.Get<ProtoBuf.BaseSculpture>();
		info.msg.baseSculpture.crc = _crc;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.baseSculpture == null)
		{
			return;
		}
		uint crc = _crc;
		_crc = info.msg.baseSculpture.crc;
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
	public void SV_CarveSphere(RPCMessage msg)
	{
		if (!IsLocked())
		{
			Vector3 worldSpacePosition = msg.read.Vector3();
			int r = math.clamp(msg.read.Int32(), 2, 6);
			CarveSphere(r, worldSpacePosition);
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	public void SV_SmoothSphere(RPCMessage msg)
	{
		if (!IsLocked())
		{
			Vector3 worldSpacePosition = msg.read.Vector3();
			int r = math.clamp(msg.read.Int32(), 2, 6);
			BlurSphere(r, worldSpacePosition);
		}
	}

	private void CarveSphere(int r, Vector3 worldSpacePosition)
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

	private void BlurSphere(int r, Vector3 worldSpacePosition)
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

	private void CarveRect(int3 halfExtents, Vector3 worldSpacePositionCentre)
	{
		int3 inBlockSpace = GetInBlockSpace(worldSpacePositionCentre);
		for (int i = -halfExtents.x; i <= halfExtents.x; i++)
		{
			for (int j = -halfExtents.y; j <= halfExtents.y; j++)
			{
				for (int k = -halfExtents.z; k <= halfExtents.z; k++)
				{
					int3 @int = new int3(i, j, k);
					int3 p = inBlockSpace + @int;
					if (_grid.InBounds(p))
					{
						_grid[p] = false;
					}
				}
			}
		}
		MarkServerGridUpdate();
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

	internal override void DoServerDestroy()
	{
		base.DoServerDestroy();
		FileStorage.server.RemoveAllByEntity(net.ID);
	}
}
