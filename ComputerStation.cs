#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

public class ComputerStation : BaseMountable
{
	public const Flags Flag_HasFullControl = Flags.Reserved2;

	[Header("Computer")]
	public GameObjectRef menuPrefab;

	public ComputerMenu computerMenu;

	public EntityRef currentlyControllingEnt;

	public List<string> controlBookmarks = new List<string>();

	public Transform leftHandIKPosition;

	public Transform rightHandIKPosition;

	public SoundDefinition turnOnSoundDef;

	public SoundDefinition turnOffSoundDef;

	public SoundDefinition onLoopSoundDef;

	public bool isStatic;

	public float autoGatherRadius;

	private ulong currentPlayerID;

	private float nextAddTime;

	private static readonly char[] BookmarkSplit = new char[1] { ';' };

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("ComputerStation.OnRpcMessage"))
		{
			if (rpc == 481778085 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - AddBookmark ");
				}
				using (TimeWarning.New("AddBookmark"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg2 = rPCMessage;
							AddBookmark(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in AddBookmark");
					}
				}
				return true;
			}
			if (rpc == 552248427 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - BeginControllingBookmark ");
				}
				using (TimeWarning.New("BeginControllingBookmark"))
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
							BeginControllingBookmark(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in BeginControllingBookmark");
					}
				}
				return true;
			}
			if (rpc == 2498687923u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - DeleteBookmark ");
				}
				using (TimeWarning.New("DeleteBookmark"))
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
							DeleteBookmark(msg4);
						}
					}
					catch (Exception exception3)
					{
						Debug.LogException(exception3);
						player.Kick("RPC Error in DeleteBookmark");
					}
				}
				return true;
			}
			if (rpc == 2139261430 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_DisconnectControl ");
				}
				using (TimeWarning.New("Server_DisconnectControl"))
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
							Server_DisconnectControl(msg5);
						}
					}
					catch (Exception exception4)
					{
						Debug.LogException(exception4);
						player.Kick("RPC Error in Server_DisconnectControl");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public bool AllowPings()
	{
		BaseEntity baseEntity = currentlyControllingEnt.Get(base.isServer);
		if (baseEntity != null && baseEntity is IRemoteControllable { CanPing: not false })
		{
			return true;
		}
		return false;
	}

	public static bool IsValidIdentifier(string str)
	{
		if (string.IsNullOrEmpty(str))
		{
			return false;
		}
		if (str.Length > 32)
		{
			return false;
		}
		return str.IsAlphaNumeric();
	}

	public override void DestroyShared()
	{
		if (base.isServer && (bool)GetMounted())
		{
			StopControl(GetMounted());
		}
		base.DestroyShared();
	}

	public override void ServerInit()
	{
		base.ServerInit();
		Invoke(GatherStaticCameras, 5f);
	}

	public void GatherStaticCameras()
	{
		if (Rust.Application.isLoadingSave)
		{
			Invoke(GatherStaticCameras, 1f);
		}
		else
		{
			if (!isStatic || !(autoGatherRadius > 0f))
			{
				return;
			}
			List<BaseEntity> obj = Facepunch.Pool.Get<List<BaseEntity>>();
			Vis.Entities(base.transform.position, autoGatherRadius, obj, 256, QueryTriggerInteraction.Ignore);
			foreach (BaseEntity item in obj)
			{
				IRemoteControllable component = item.GetComponent<IRemoteControllable>();
				if (component != null)
				{
					CCTV_RC component2 = item.GetComponent<CCTV_RC>();
					if (!(component2 == null) && component2.IsStatic() && !controlBookmarks.Contains(component.GetIdentifier()))
					{
						ForceAddBookmark(component.GetIdentifier());
					}
				}
			}
			Facepunch.Pool.FreeUnmanaged(ref obj);
		}
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		GatherStaticCameras();
	}

	public void StopControl(BasePlayer ply)
	{
		BaseEntity baseEntity = currentlyControllingEnt.Get(serverside: true);
		if ((bool)baseEntity)
		{
			baseEntity.GetComponent<IRemoteControllable>().StopControl(new CameraViewerId(currentPlayerID, 0L));
		}
		if ((bool)ply)
		{
			ply.net.SwitchSecondaryGroup(null);
			ply.SetRcEntityPosition(null);
		}
		currentlyControllingEnt.uid = default(NetworkableId);
		currentPlayerID = 0uL;
		SetFlag(Flags.Reserved2, b: false, recursive: false, networkupdate: false);
		SendNetworkUpdate();
		SendControlBookmarks(ply);
		CancelInvoke(ControlCheck);
		CancelInvoke(CheckCCTVAchievement);
	}

	public bool IsPlayerAdmin(BasePlayer player)
	{
		return player == GetMounted();
	}

	[RPC_Server]
	public void DeleteBookmark(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (!IsPlayerAdmin(player) || isStatic)
		{
			return;
		}
		string text = msg.read.String();
		if (IsValidIdentifier(text) && controlBookmarks.Contains(text))
		{
			controlBookmarks.Remove(text);
			SendControlBookmarks(player);
			BaseEntity baseEntity = currentlyControllingEnt.Get(serverside: true);
			if (baseEntity != null && baseEntity.TryGetComponent<IRemoteControllable>(out var component) && component.GetIdentifier() == text)
			{
				StopControl(player);
			}
		}
	}

	[RPC_Server]
	public void Server_DisconnectControl(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (IsPlayerAdmin(player))
		{
			StopControl(player);
		}
	}

	[RPC_Server]
	public void BeginControllingBookmark(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (!IsPlayerAdmin(player))
		{
			return;
		}
		string text = msg.read.String();
		if (!IsValidIdentifier(text) || !controlBookmarks.Contains(text))
		{
			return;
		}
		IRemoteControllable remoteControllable = RemoteControlEntity.FindByID(text);
		if (remoteControllable == null)
		{
			return;
		}
		BaseEntity ent = remoteControllable.GetEnt();
		if (ent == null)
		{
			Debug.LogWarning("RC identifier " + text + " was found but has a null or destroyed entity, this should never happen");
		}
		else if (remoteControllable.CanControl(player.userID) && !(Vector3.Distance(base.transform.position, ent.transform.position) >= remoteControllable.MaxRange))
		{
			BaseEntity baseEntity = currentlyControllingEnt.Get(serverside: true);
			if ((bool)baseEntity)
			{
				baseEntity.GetComponent<IRemoteControllable>()?.StopControl(new CameraViewerId(currentPlayerID, 0L));
			}
			player.net.SwitchSecondaryGroup(ent.net.group);
			player.SetRcEntityPosition(ent.transform.position);
			currentlyControllingEnt.uid = ent.net.ID;
			currentPlayerID = player.userID;
			bool b = remoteControllable.InitializeControl(new CameraViewerId(currentPlayerID, 0L));
			SetFlag(Flags.Reserved2, b, recursive: false, networkupdate: false);
			SendNetworkUpdateImmediate();
			SendControlBookmarks(player);
			if (GameInfo.HasAchievements && remoteControllable.GetEnt() is CCTV_RC)
			{
				InvokeRepeating(CheckCCTVAchievement, 1f, 3f);
			}
			InvokeRepeating(ControlCheck, 0f, 0f);
		}
	}

	private void CheckCCTVAchievement()
	{
		BasePlayer mounted = GetMounted();
		if (!(mounted != null))
		{
			return;
		}
		BaseEntity baseEntity = currentlyControllingEnt.Get(serverside: true);
		if (!(baseEntity != null) || !(baseEntity is CCTV_RC cCTV_RC))
		{
			return;
		}
		foreach (Connection subscriber in mounted.net.secondaryGroup.subscribers)
		{
			if (!subscriber.active)
			{
				continue;
			}
			BasePlayer basePlayer = subscriber.player as BasePlayer;
			if (!(basePlayer == null))
			{
				Vector3 vector = basePlayer.CenterPoint();
				float num = Vector3.Dot((vector - cCTV_RC.pitch.position).normalized, cCTV_RC.pitch.forward);
				Vector3 vector2 = cCTV_RC.pitch.InverseTransformPoint(vector);
				if (num > 0.6f && vector2.magnitude < 10f)
				{
					mounted.GiveAchievement("BIG_BROTHER");
					CancelInvoke(CheckCCTVAchievement);
					break;
				}
			}
		}
	}

	public bool CanAddBookmark(BasePlayer player)
	{
		if (!IsPlayerAdmin(player))
		{
			return false;
		}
		if (isStatic)
		{
			return false;
		}
		if (UnityEngine.Time.realtimeSinceStartup < nextAddTime)
		{
			return false;
		}
		if (controlBookmarks.Count > 3)
		{
			player.ChatMessage("Too many bookmarks, delete some");
			return false;
		}
		return true;
	}

	public void ForceAddBookmark(string identifier)
	{
		if (controlBookmarks.Count >= 128 || !IsValidIdentifier(identifier) || controlBookmarks.Contains(identifier))
		{
			return;
		}
		IRemoteControllable remoteControllable = RemoteControlEntity.FindByID(identifier);
		if (remoteControllable != null)
		{
			if (remoteControllable.GetEnt() == null)
			{
				Debug.LogWarning("RC identifier " + identifier + " was found but has a null or destroyed entity, this should never happen");
			}
			else
			{
				controlBookmarks.Add(identifier);
			}
		}
	}

	[RPC_Server]
	public void AddBookmark(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (IsPlayerAdmin(player) && !isStatic)
		{
			if (UnityEngine.Time.realtimeSinceStartup < nextAddTime)
			{
				player.ChatMessage("Slow down...");
				return;
			}
			if (controlBookmarks.Count >= 128)
			{
				player.ChatMessage("Too many bookmarks, delete some");
				return;
			}
			nextAddTime = UnityEngine.Time.realtimeSinceStartup + 1f;
			string identifier = msg.read.String();
			ForceAddBookmark(identifier);
			SendControlBookmarks(player);
		}
	}

	public void ControlCheck()
	{
		bool flag = false;
		BaseEntity baseEntity = currentlyControllingEnt.Get(base.isServer);
		BasePlayer mounted = GetMounted();
		if ((bool)baseEntity && (bool)mounted)
		{
			IRemoteControllable component = baseEntity.GetComponent<IRemoteControllable>();
			if (component != null && component.CanControl(mounted.userID) && Vector3.Distance(base.transform.position, baseEntity.transform.position) < component.MaxRange)
			{
				flag = true;
				mounted.net.SwitchSecondaryGroup(baseEntity.net.group);
				mounted.SetRcEntityPosition(baseEntity.transform.position);
			}
		}
		if (!flag)
		{
			StopControl(mounted);
		}
	}

	public string GenerateControlBookmarkString()
	{
		return string.Join(";", controlBookmarks);
	}

	public void SendControlBookmarks(BasePlayer player)
	{
		if (!(player == null))
		{
			string arg = GenerateControlBookmarkString();
			ClientRPC(RpcTarget.Player("ReceiveBookmarks", player), arg);
		}
	}

	public override void OnPlayerMounted()
	{
		base.OnPlayerMounted();
		BasePlayer mounted = GetMounted();
		if ((bool)mounted)
		{
			SendControlBookmarks(mounted);
		}
		SetFlag(Flags.On, b: true);
	}

	public override void OnPlayerDismounted(BasePlayer player)
	{
		base.OnPlayerDismounted(player);
		StopControl(player);
		SetFlag(Flags.On, b: false);
	}

	public override void PlayerServerInput(InputState inputState, BasePlayer player)
	{
		base.PlayerServerInput(inputState, player);
		if (HasFlag(Flags.Reserved2) && currentlyControllingEnt.IsValid(serverside: true))
		{
			currentlyControllingEnt.Get(serverside: true).GetComponent<IRemoteControllable>().UserInput(inputState, new CameraViewerId(player.userID, 0L));
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		if (!info.forDisk)
		{
			info.msg.ioEntity = Facepunch.Pool.Get<ProtoBuf.IOEntity>();
			info.msg.ioEntity.genericEntRef1 = currentlyControllingEnt.uid;
		}
		else
		{
			info.msg.computerStation = Facepunch.Pool.Get<ProtoBuf.ComputerStation>();
			info.msg.computerStation.bookmarks = GenerateControlBookmarkString();
		}
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (!info.fromDisk)
		{
			if (info.msg.ioEntity != null)
			{
				currentlyControllingEnt.uid = info.msg.ioEntity.genericEntRef1;
			}
		}
		else
		{
			if (info.msg.computerStation == null)
			{
				return;
			}
			string[] array = info.msg.computerStation.bookmarks.Split(BookmarkSplit, StringSplitOptions.RemoveEmptyEntries);
			foreach (string text in array)
			{
				if (IsValidIdentifier(text))
				{
					controlBookmarks.Add(text);
				}
			}
		}
	}
}
