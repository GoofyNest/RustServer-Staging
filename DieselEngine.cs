#define UNITY_ASSERTIONS
using System;
using ConVar;
using Facepunch;
using Facepunch.Rust;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

public class DieselEngine : StorageContainer
{
	public GameObjectRef rumbleEffect;

	public Transform rumbleOrigin;

	public const Flags Flag_HasFuel = Flags.Reserved3;

	public float runningTimePerFuelUnit = 120f;

	private float cachedFuelTime;

	private const float rumbleMaxDistSq = 100f;

	private const string EXCAVATOR_ACTIVATED_STAT = "excavator_activated";

	private BasePlayer startedByPlayer;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("DieselEngine.OnRpcMessage"))
		{
			if (rpc == 578721460 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - EngineSwitch ");
				}
				using (TimeWarning.New("EngineSwitch"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(578721460u, "EngineSwitch", this, player, 6f))
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
							EngineSwitch(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in EngineSwitch");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public void FixedUpdate()
	{
		if (!base.isClient && IsOn())
		{
			if (cachedFuelTime <= UnityEngine.Time.fixedDeltaTime && ConsumeFuelItem())
			{
				cachedFuelTime += runningTimePerFuelUnit;
			}
			cachedFuelTime -= UnityEngine.Time.fixedDeltaTime;
			if (cachedFuelTime <= 0f)
			{
				cachedFuelTime = 0f;
				EngineOff();
			}
		}
	}

	[RPC_Server]
	[RPC_Server.IsVisible(6f)]
	public void EngineSwitch(RPCMessage msg)
	{
		if (msg.read.Bit())
		{
			if (GetFuelAmount() > 0)
			{
				EngineOn();
				startedByPlayer = msg.player;
				if (GameInfo.HasAchievements && msg.player != null)
				{
					msg.player.stats.Add("excavator_activated", 1, Stats.All);
					msg.player.stats.Save(forceSteamSave: true);
				}
			}
		}
		else
		{
			EngineOff();
		}
	}

	public void TimedShutdown()
	{
		EngineOff();
	}

	public bool ConsumeFuelItem(int amount = 1)
	{
		Item slot = base.inventory.GetSlot(0);
		if (slot == null || slot.amount < amount)
		{
			return false;
		}
		slot.UseItem(amount);
		Analytics.Azure.OnExcavatorConsumeFuel(slot, amount, this);
		if (startedByPlayer != null && startedByPlayer.serverClan != null)
		{
			startedByPlayer.AddClanScore(ClanScoreEventType.RanExcavator, amount);
		}
		UpdateHasFuelFlag();
		return true;
	}

	public int GetFuelAmount()
	{
		Item slot = base.inventory.GetSlot(0);
		if (slot == null || slot.amount < 1)
		{
			return 0;
		}
		return slot.amount;
	}

	public void UpdateHasFuelFlag()
	{
		SetFlag(Flags.Reserved3, GetFuelAmount() > 0);
	}

	public override void PlayerStoppedLooting(BasePlayer player)
	{
		base.PlayerStoppedLooting(player);
		UpdateHasFuelFlag();
	}

	public void EngineOff()
	{
		SetFlag(Flags.On, b: false);
		BroadcastEntityMessage("DieselEngineOff");
	}

	public void EngineOn()
	{
		SetFlag(Flags.On, b: true);
		BroadcastEntityMessage("DieselEngineOn");
	}

	public void RescheduleEngineShutdown()
	{
		float time = 120f;
		Invoke(TimedShutdown, time);
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		if (IsOn())
		{
			BroadcastEntityMessage("DieselEngineOn");
		}
		else
		{
			BroadcastEntityMessage("DieselEngineOff");
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.dieselEngine = Facepunch.Pool.Get<ProtoBuf.DieselEngine>();
		info.msg.dieselEngine.fuelTime = cachedFuelTime;
		if (info.forDisk)
		{
			info.msg.dieselEngine.startedByPlayer = ((startedByPlayer != null) ? startedByPlayer.userID : ((BasePlayer.EncryptedValue<ulong>)0uL));
		}
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.ioEntity != null)
		{
			cachedFuelTime = info.msg.ioEntity.genericFloat1;
		}
		else if (info.msg.dieselEngine != null)
		{
			cachedFuelTime = info.msg.dieselEngine.fuelTime;
			if (base.isServer)
			{
				startedByPlayer = BasePlayer.FindAwakeOrSleepingByID(info.msg.dieselEngine.startedByPlayer);
			}
		}
	}

	public bool HasFuel()
	{
		return HasFlag(Flags.Reserved3);
	}
}
