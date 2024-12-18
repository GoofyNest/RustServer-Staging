#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Network;
using ProtoBuf;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;

public class DigitalClock : IOEntity, INotifyLOD
{
	public struct Alarm
	{
		public TimeSpan time;

		public bool active;

		public Alarm(TimeSpan time, bool active)
		{
			this.time = time;
			this.active = active;
		}

		public Alarm(DigitalClockAlarm alarm)
		{
			time = alarm.time.ToTimeSpan();
			active = alarm.active;
		}
	}

	[SerializeField]
	private TextMeshPro clockText;

	[SerializeField]
	private SoundDefinition ringingSoundDef;

	[SerializeField]
	private SoundDefinition ringingStartSoundDef;

	[SerializeField]
	private SoundDefinition ringingStopSoundDef;

	public GameObjectRef clockConfigPanel;

	public List<Alarm> alarms = new List<Alarm>();

	[HideInInspector]
	public bool muted;

	private bool isRinging;

	public const float ringDuration = 5f;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("DigitalClock.OnRpcMessage"))
		{
			if (rpc == 2287159130u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_SetAlarms ");
				}
				using (TimeWarning.New("RPC_SetAlarms"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(2287159130u, "RPC_SetAlarms", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.IsVisible.Test(2287159130u, "RPC_SetAlarms", this, player, 3f))
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
							RPC_SetAlarms(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_SetAlarms");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	private void OnMinute()
	{
		if (IsOn() && base.isServer)
		{
			CheckAlarms();
		}
	}

	public override void OnFlagsChanged(Flags old, Flags next)
	{
		base.OnFlagsChanged(old, next);
	}

	public bool CanPlayerAdmin(BasePlayer player)
	{
		if (player != null)
		{
			return player.CanBuild();
		}
		return false;
	}

	public override void ServerInit()
	{
		base.ServerInit();
		TOD_Sky.Instance.Components.Time.OnMinute += OnMinute;
	}

	private void CheckAlarms()
	{
		TimeSpan timeOfDay = TOD_Sky.Instance.Cycle.DateTime.TimeOfDay;
		foreach (Alarm alarm in alarms)
		{
			if (!alarm.active)
			{
				continue;
			}
			int hours = timeOfDay.Hours;
			TimeSpan time = alarm.time;
			if (hours == time.Hours)
			{
				int minutes = timeOfDay.Minutes;
				time = alarm.time;
				if (minutes == time.Minutes)
				{
					Ring();
				}
			}
		}
	}

	private void Ring()
	{
		isRinging = true;
		ClientRPC(RpcTarget.NetworkGroup("RPC_StartRinging"));
		Invoke(StopRinging, 5f);
		MarkDirty();
	}

	private void StopRinging()
	{
		isRinging = false;
		ClientRPC(RpcTarget.NetworkGroup("RPC_StopRinging"));
		MarkDirty();
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	[RPC_Server.CallsPerSecond(5uL)]
	public void RPC_SetAlarms(RPCMessage msg)
	{
		if (!CanPlayerAdmin(msg.player))
		{
			return;
		}
		DigitalClockMessage digitalClockMessage = DigitalClockMessage.Deserialize(msg.read);
		List<DigitalClockAlarm> list = digitalClockMessage.alarms;
		alarms.Clear();
		foreach (DigitalClockAlarm item2 in list)
		{
			Alarm item = new Alarm(item2.time.ToTimeSpan(), item2.active);
			alarms.Add(item);
		}
		muted = digitalClockMessage.muted;
		MarkDirty();
		SendNetworkUpdate();
	}

	public override int GetPassthroughAmount(int outputSlot = 0)
	{
		if (!isRinging)
		{
			return 0;
		}
		return base.GetPassthroughAmount(outputSlot);
	}

	public override void UpdateHasPower(int inputAmount, int inputSlot)
	{
		base.UpdateHasPower(inputAmount, inputSlot);
		if (inputAmount == 0)
		{
			ResetIOState();
		}
		SetFlag(Flags.On, HasFlag(Flags.Reserved8));
	}

	public override void ResetIOState()
	{
		isRinging = false;
		SetFlag(Flags.On, b: false);
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.digitalClock = Facepunch.Pool.Get<ProtoBuf.DigitalClock>();
		List<DigitalClockAlarm> list = Facepunch.Pool.Get<List<DigitalClockAlarm>>();
		foreach (Alarm alarm in alarms)
		{
			DigitalClockAlarm digitalClockAlarm = new DigitalClockAlarm();
			digitalClockAlarm.active = alarm.active;
			digitalClockAlarm.time = alarm.time.ToFloat();
			list.Add(digitalClockAlarm);
		}
		info.msg.digitalClock.alarms = list;
		info.msg.digitalClock.muted = muted;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.digitalClock == null)
		{
			return;
		}
		alarms.Clear();
		foreach (DigitalClockAlarm alarm in info.msg.digitalClock.alarms)
		{
			alarms.Add(new Alarm(alarm));
		}
		muted = info.msg.digitalClock.muted;
	}
}
