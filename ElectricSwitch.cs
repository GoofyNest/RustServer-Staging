#define UNITY_ASSERTIONS
using System;
using ConVar;
using Network;
using UnityEngine;
using UnityEngine.Assertions;

public class ElectricSwitch : IOEntity
{
	public bool isToggleSwitch;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("ElectricSwitch.OnRpcMessage"))
		{
			if (rpc == 3043863856u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_Switch ");
				}
				using (TimeWarning.New("RPC_Switch"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(3043863856u, "RPC_Switch", this, player, 3f))
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
							RPC_Switch(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_Switch");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override bool WantsPower(int inputIndex)
	{
		if (inputIndex == 0)
		{
			return IsOn();
		}
		return false;
	}

	public override int ConsumptionAmount()
	{
		return 0;
	}

	public override void ResetIOState()
	{
		SetFlag(Flags.On, b: false);
	}

	public override int GetPassthroughAmount(int outputSlot = 0)
	{
		if (!IsOn())
		{
			return 0;
		}
		return GetCurrentEnergy();
	}

	public override int CalculateCurrentEnergy(int inputAmount, int inputSlot)
	{
		if (inputSlot != 0)
		{
			return currentEnergy;
		}
		return base.CalculateCurrentEnergy(inputAmount, inputSlot);
	}

	public override void UpdateHasPower(int inputAmount, int inputSlot)
	{
		if (inputSlot == 1 && inputAmount > 0)
		{
			SetSwitch(state: true);
		}
		if (inputSlot == 2 && inputAmount > 0)
		{
			SetSwitch(state: false);
		}
		if (inputSlot == 0)
		{
			base.UpdateHasPower(inputAmount, inputSlot);
		}
	}

	public override void ServerInit()
	{
		base.ServerInit();
		SetFlag(Flags.Busy, b: false);
	}

	public virtual void SetSwitch(bool state)
	{
		if (state != IsOn())
		{
			SetFlag(Flags.On, state);
			SetFlag(Flags.Busy, b: true);
			Invoke(UnBusy, 0.5f);
			SendNetworkUpdateImmediate();
			MarkDirty();
		}
	}

	public void Flip()
	{
		SetSwitch(!IsOn());
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void RPC_Switch(RPCMessage msg)
	{
		bool @switch = msg.read.Bool();
		SetSwitch(@switch);
	}

	private void UnBusy()
	{
		SetFlag(Flags.Busy, b: false);
	}
}
