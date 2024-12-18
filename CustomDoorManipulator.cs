#define UNITY_ASSERTIONS
using System;
using ConVar;
using Network;
using UnityEngine;
using UnityEngine.Assertions;

public class CustomDoorManipulator : DoorManipulator
{
	public static Translate.Phrase pairAttemptPhrase = new Translate.Phrase("doorcontroller.unlock", "Door must be unlocked for pairing!");

	private int inputOpenAmount;

	private int inputCloseAmount;

	private DoorEffect delayedAction;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("CustomDoorManipulator.OnRpcMessage"))
		{
			if (rpc == 114855818 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_DoPair ");
				}
				using (TimeWarning.New("RPC_DoPair"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(114855818u, "RPC_DoPair", this, player, 3f))
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
							RPC_DoPair(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_DoPair");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public bool IsPaired()
	{
		return targetDoor != null;
	}

	public override void SetupInitialDoorConnection()
	{
		if (entityRef.IsValid(serverside: true) && !IsPaired())
		{
			Door component = entityRef.Get(serverside: true).GetComponent<Door>();
			SetTargetDoor(component);
		}
	}

	public override void SetTargetDoor(Door newTargetDoor)
	{
		targetDoor = newTargetDoor;
		SetFlag(Flags.On, targetDoor != null);
		entityRef.Set(newTargetDoor);
	}

	public override void DoAction(DoorEffect action)
	{
		if (!IsPaired())
		{
			DoActionDoorMissing();
			return;
		}
		if (targetDoor.IsBusy())
		{
			delayedAction = action;
			Invoke(DoDelayedAction, 1f);
			return;
		}
		switch (action)
		{
		case DoorEffect.Open:
			targetDoor.SetOpen(open: true);
			break;
		case DoorEffect.Close:
			targetDoor.SetOpen(open: false);
			break;
		}
	}

	private void DoDelayedAction()
	{
		DoAction(delayedAction);
	}

	public override void DoActionDoorMissing()
	{
		SetTargetDoor(null);
	}

	public override Door FindDoor(bool allowLocked = true)
	{
		if (parentEntity.Get(serverside: true) is Door result)
		{
			return result;
		}
		return null;
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	public void RPC_DoPair(RPCMessage msg)
	{
		Door door = targetDoor;
		Door door2 = FindDoor();
		if (door2 != null && door2 != door)
		{
			PairDoorAttempt(door2, msg.player);
		}
	}

	private void PairDoorAttempt(Door door, BasePlayer byPlayer)
	{
		if (door.GetPlayerLockPermission(byPlayer))
		{
			SetTargetDoor(door);
		}
		else
		{
			byPlayer.ShowToast(GameTip.Styles.Blue_Normal, pairAttemptPhrase, false);
		}
	}

	public override void OnDeployed(BaseEntity parent, BasePlayer deployedBy, Item fromItem)
	{
		base.OnDeployed(parent, deployedBy, fromItem);
		Door door = parent as Door;
		if (door != null)
		{
			Invoke(delegate
			{
				PairDoorAttempt(door, deployedBy);
			}, 0.25f);
		}
	}

	public override void UpdateHasPower(int inputAmount, int inputSlot)
	{
		if (inputSlot == 0)
		{
			base.UpdateHasPower(inputAmount, inputSlot);
		}
	}

	public override void IOStateChanged(int inputAmount, int inputSlot)
	{
	}

	public override void UpdateFromInput(int inputAmount, int inputSlot)
	{
		if (inputSlot == 0)
		{
			bool flag = currentEnergy != 0;
			base.UpdateFromInput(inputAmount, inputSlot);
			if (inputAmount == 0 && flag && inputOpenAmount == 0)
			{
				DoAction(DoorEffect.Close);
			}
			else if (inputAmount > 0 && !flag)
			{
				inputCloseAmount = GetPowerAtInput(2);
				DoAction((inputCloseAmount == 0) ? DoorEffect.Open : DoorEffect.Close);
			}
		}
		if (inputSlot == 1 && inputOpenAmount != inputAmount)
		{
			if (inputAmount > 0 && IsPowered())
			{
				DoAction(DoorEffect.Open);
			}
			inputOpenAmount = inputAmount;
		}
		else if (inputSlot == 2 && inputCloseAmount != inputAmount)
		{
			if (inputAmount > 0 && IsPowered())
			{
				DoAction(DoorEffect.Close);
			}
			inputCloseAmount = inputAmount;
		}
	}

	private int GetPowerAtInput(int slotIndex)
	{
		IOSlot iOSlot = inputs[slotIndex];
		if (!iOSlot.IsConnected())
		{
			return 0;
		}
		int connectedToSlot = iOSlot.connectedToSlot;
		return iOSlot.connectedTo.Get().GetPassthroughAmount(connectedToSlot);
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		targetDoor = entityRef.Get(base.isServer) as Door;
	}
}
