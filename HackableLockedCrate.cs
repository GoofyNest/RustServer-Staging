#define UNITY_ASSERTIONS
using System;
using ConVar;
using Facepunch.Rust;
using Network;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

public class HackableLockedCrate : LootContainer
{
	public const Flags Flag_Hacking = Flags.Reserved1;

	public const Flags Flag_FullyHacked = Flags.Reserved2;

	public Text timerText;

	[ServerVar(Help = "How many seconds for the crate to unlock")]
	public static float requiredHackSeconds = 900f;

	[ServerVar(Help = "How many seconds until the crate is destroyed without any hack attempts")]
	public static float decaySeconds = 7200f;

	public SoundPlayer hackProgressBeep;

	private float hackSeconds;

	public GameObjectRef shockEffect;

	public GameObjectRef mapMarkerEntityPrefab;

	public GameObjectRef landEffect;

	public bool shouldDecay = true;

	private BasePlayer originalHackerPlayer;

	private ulong originalHackerPlayerId;

	private bool hasBeenOpened;

	private BaseEntity mapMarkerInstance;

	private bool hasLanded;

	private bool wasDropped;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("HackableLockedCrate.OnRpcMessage"))
		{
			if (rpc == 888500940 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_Hack ");
				}
				using (TimeWarning.New("RPC_Hack"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(888500940u, "RPC_Hack", this, player, 3f))
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
							RPC_Hack(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_Hack");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public bool IsBeingHacked()
	{
		return HasFlag(Flags.Reserved1);
	}

	public bool IsFullyHacked()
	{
		return HasFlag(Flags.Reserved2);
	}

	public override void DestroyShared()
	{
		if (base.isServer && (bool)mapMarkerInstance)
		{
			mapMarkerInstance.Kill();
		}
		base.DestroyShared();
	}

	public void CreateMapMarker(float durationMinutes)
	{
		if ((bool)mapMarkerInstance)
		{
			mapMarkerInstance.Kill();
		}
		BaseEntity baseEntity = GameManager.server.CreateEntity(mapMarkerEntityPrefab.resourcePath, base.transform.position, Quaternion.identity);
		baseEntity.Spawn();
		baseEntity.SetParent(this);
		baseEntity.transform.localPosition = Vector3.zero;
		baseEntity.SendNetworkUpdate();
		mapMarkerInstance = baseEntity;
	}

	public void RefreshDecay()
	{
		CancelInvoke(DelayedDestroy);
		if (shouldDecay)
		{
			Invoke(DelayedDestroy, decaySeconds);
		}
	}

	public void DelayedDestroy()
	{
		Kill();
	}

	public override void OnAttacked(HitInfo info)
	{
		if (base.isServer)
		{
			if (StringPool.Get(info.HitBone) == "laptopcollision")
			{
				Effect.server.Run(shockEffect.resourcePath, info.HitPositionWorld, Vector3.up);
				hackSeconds -= 8f * (info.damageTypes.Total() / 50f);
				if (hackSeconds < 0f)
				{
					hackSeconds = 0f;
				}
			}
			RefreshDecay();
		}
		base.OnAttacked(info);
	}

	public void SetWasDropped()
	{
		wasDropped = true;
	}

	public override void ServerInit()
	{
		base.ServerInit();
		if (base.isClient)
		{
			return;
		}
		if (!Rust.Application.isLoadingSave)
		{
			SetFlag(Flags.Reserved1, b: false);
			SetFlag(Flags.Reserved2, b: false);
			if (wasDropped)
			{
				InvokeRepeating(LandCheck, 0f, 0.015f);
			}
			Analytics.Azure.OnEntitySpawned(this);
		}
		RefreshDecay();
		isLootable = IsFullyHacked();
		CreateMapMarker(120f);
		base.inventory.onItemAddedRemoved = OnItemAddedOrRemoved;
	}

	public override void OnItemAddedOrRemoved(Item item, bool added)
	{
		if (!added && mapMarkerInstance != null)
		{
			mapMarkerInstance.Kill();
		}
		base.OnItemAddedOrRemoved(item, added);
	}

	public void LandCheck()
	{
		if (!hasLanded && UnityEngine.Physics.Raycast(new Ray(base.transform.position + Vector3.up * 0.5f, Vector3.down), out var hitInfo, 1f, 1084293377))
		{
			Effect.server.Run(landEffect.resourcePath, hitInfo.point, Vector3.up);
			hasLanded = true;
			CancelInvoke(LandCheck);
		}
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		SetFlag(Flags.Reserved1, b: false);
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void RPC_Hack(RPCMessage msg)
	{
		if (!IsBeingHacked())
		{
			Analytics.Azure.OnLockedCrateStarted(msg.player, this);
			originalHackerPlayerId = msg.player.userID;
			originalHackerPlayer = msg.player;
			StartHacking();
		}
	}

	public void StartHacking()
	{
		BroadcastEntityMessage("HackingStarted", 20f, 256);
		SetFlag(Flags.Reserved1, b: true);
		InvokeRepeating(HackProgress, 1f, 1f);
		ClientRPC(RpcTarget.NetworkGroup("UpdateHackProgress"), 0, (int)requiredHackSeconds);
		RefreshDecay();
	}

	public void HackProgress()
	{
		hackSeconds += 1f;
		if (hackSeconds > requiredHackSeconds)
		{
			Analytics.Azure.OnLockedCrateFinished(originalHackerPlayerId, this);
			if (originalHackerPlayer != null && originalHackerPlayer.serverClan != null)
			{
				originalHackerPlayer.AddClanScore(ClanScoreEventType.HackedCrate);
			}
			RefreshDecay();
			SetFlag(Flags.Reserved2, b: true);
			isLootable = true;
			CancelInvoke(HackProgress);
		}
		ClientRPC(RpcTarget.NetworkGroup("UpdateHackProgress"), (int)hackSeconds, (int)requiredHackSeconds);
	}

	public override bool OnStartBeingLooted(BasePlayer player)
	{
		bool num = base.OnStartBeingLooted(player);
		if (num && !hasBeenOpened)
		{
			hasBeenOpened = true;
			player.AddClanScore(ClanScoreEventType.OpenedHackedCrate);
		}
		return num;
	}
}
