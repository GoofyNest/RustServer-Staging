#define UNITY_ASSERTIONS
using System;
using ConVar;
using Network;
using UnityEngine;
using UnityEngine.Assertions;

public class SeismicSensor : IOEntity
{
	public static int MinRange = 1;

	public static int MaxRange = 30;

	public int range = 30;

	public GameObjectRef sensorPanelPrefab;

	private int vibrationLevel;

	private const int holdTime = 3;

	private static readonly BaseEntity[] resultBuffer = new BaseEntity[128];

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("SeismicSensor.OnRpcMessage"))
		{
			if (rpc == 128851379 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_SetRange ");
				}
				using (TimeWarning.New("RPC_SetRange"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(128851379u, "RPC_SetRange", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.IsVisible.Test(128851379u, "RPC_SetRange", this, player, 3f))
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
							RPC_SetRange(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_SetRange");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public void SetVibrationLevel(int value)
	{
		float num = value;
		if (num <= 0f)
		{
			SetOff();
			return;
		}
		if (num > (float)vibrationLevel)
		{
			vibrationLevel = Mathf.RoundToInt(num);
			SetFlag(Flags.On, b: true);
			MarkDirty();
		}
		if (IsInvoking(SetOff))
		{
			CancelInvoke(SetOff);
		}
		Invoke(SetOff, 3f);
	}

	private void SetOff()
	{
		if (vibrationLevel != 0)
		{
			vibrationLevel = 0;
			SetFlag(Flags.On, b: false);
			MarkDirty();
		}
	}

	public void SetRange(int value)
	{
		value = Mathf.Clamp(value, MinRange, MaxRange);
		range = value;
		SendNetworkUpdate();
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	[RPC_Server.CallsPerSecond(5uL)]
	public void RPC_SetRange(RPCMessage msg)
	{
		BasePlayer player = msg.player;
		if (!(player == null) && player.CanBuild())
		{
			int num = msg.read.Int32();
			SetRange(num);
		}
	}

	public override void UpdateHasPower(int inputAmount, int inputSlot)
	{
		base.UpdateHasPower(inputAmount, inputSlot);
		if (inputAmount == 0)
		{
			ResetIOState();
		}
	}

	public override int GetPassthroughAmount(int outputSlot = 0)
	{
		if (!IsPowered())
		{
			return 0;
		}
		return vibrationLevel;
	}

	public override void ResetIOState()
	{
		vibrationLevel = 0;
		SetFlag(Flags.On, b: false);
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.ioEntity.genericInt1 = range;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.ioEntity != null)
		{
			range = info.msg.ioEntity.genericInt1;
		}
	}

	public static void Notify(Vector3 position, int value)
	{
		if (value == 0)
		{
			return;
		}
		int inSphereFast = Query.Server.GetInSphereFast(position, MaxRange, resultBuffer, FilterOutSensors);
		for (int i = 0; i < inSphereFast; i++)
		{
			SeismicSensor seismicSensor = resultBuffer[i] as SeismicSensor;
			Vector3 position2 = seismicSensor.transform.position;
			float sqrMagnitude = (position - position2).sqrMagnitude;
			float num = (float)seismicSensor.range + 0.5f;
			if (sqrMagnitude < num * num)
			{
				seismicSensor.SetVibrationLevel(value);
			}
		}
	}

	private static bool FilterOutSensors(BaseEntity entity)
	{
		SeismicSensor seismicSensor = entity as SeismicSensor;
		if (seismicSensor != null && seismicSensor.IsValidEntityReference())
		{
			return seismicSensor.HasFlag(Flags.Reserved8);
		}
		return false;
	}
}
