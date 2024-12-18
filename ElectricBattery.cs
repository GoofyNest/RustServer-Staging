using System.Collections.Generic;
using Facepunch;
using ProtoBuf;
using UnityEngine;

public class ElectricBattery : IOEntity, IInstanceDataReceiver
{
	public int maxOutput;

	public float maxCapactiySeconds;

	public float rustWattSeconds;

	[Tooltip("How much energy we can request from power sources for charging is this value multiplied by our maxOutput")]
	public float maximumInboundEnergyRatio = 4f;

	public bool rechargable;

	public float chargeRatio = 0.25f;

	private int activeDrain;

	private float lastChargeIn;

	private const float tickRateSeconds = 1f;

	public const Flags Flag_HalfFull = Flags.Reserved5;

	public const Flags Flag_VeryFull = Flags.Reserved6;

	public const Flags Flag_Full = Flags.Reserved9;

	private bool wasLoaded;

	private HashSet<(IOEntity entity, int inputIndex)> connectedList = new HashSet<(IOEntity, int)>();

	private HashSet<(IOEntity entity, int inputIndex)> auxConnectedList = new HashSet<(IOEntity, int)>();

	private Queue<int> inputHistory = new Queue<int>();

	private const int inputHistorySize = 5;

	public override bool IsRootEntity()
	{
		return true;
	}

	public override int ConsumptionAmount()
	{
		return 0;
	}

	public override int MaximalPowerOutput()
	{
		return maxOutput;
	}

	public int GetActiveDrain()
	{
		if (!IsOn())
		{
			return 0;
		}
		return activeDrain;
	}

	public void ReceiveInstanceData(ProtoBuf.Item.InstanceData data)
	{
		rustWattSeconds = data.dataInt;
	}

	public override void ServerInit()
	{
		base.ServerInit();
		InvokeRandomized(CheckDischarge, Random.Range(0f, 1f), 1f, 0.1f);
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		wasLoaded = true;
	}

	public override void OnPickedUp(Item createdItem, BasePlayer player)
	{
		base.OnPickedUp(createdItem, player);
		if (createdItem.instanceData == null)
		{
			createdItem.instanceData = new ProtoBuf.Item.InstanceData();
		}
		createdItem.instanceData.ShouldPool = false;
		createdItem.instanceData.dataInt = Mathf.FloorToInt(rustWattSeconds);
	}

	public override int GetCurrentEnergy()
	{
		return currentEnergy;
	}

	public int GetDrain()
	{
		connectedList.Clear();
		auxConnectedList.Clear();
		IOEntity iOEntity = outputs[0].connectedTo.Get();
		if (iOEntity != null)
		{
			int connectedToSlot = outputs[0].connectedToSlot;
			if (iOEntity.WantsPower(connectedToSlot))
			{
				AddConnectedRecursive(iOEntity, connectedToSlot, ref connectedList);
			}
			else
			{
				connectedList.Add((iOEntity, connectedToSlot));
			}
		}
		int num = 0;
		if (HasFlag(Flags.Reserved9))
		{
			IOEntity iOEntity2 = outputs[1].connectedTo.Get();
			if (iOEntity2 != null)
			{
				int connectedToSlot2 = outputs[1].connectedToSlot;
				if (iOEntity2.WantsPower(connectedToSlot2))
				{
					AddConnectedRecursive(iOEntity2, connectedToSlot2, ref auxConnectedList);
				}
				else
				{
					auxConnectedList.Add((iOEntity2, connectedToSlot2));
				}
			}
			foreach (var auxConnected in auxConnectedList)
			{
				if (auxConnected.entity.ShouldDrainBattery(this))
				{
					num += auxConnected.entity.DesiredPower(auxConnected.inputIndex);
					if (num >= 1)
					{
						num = 1;
						break;
					}
				}
			}
		}
		int num2 = num;
		foreach (var connected in connectedList)
		{
			if (connected.entity.ShouldDrainBattery(this))
			{
				num2 += connected.entity.DesiredPower(connected.inputIndex);
				if (num2 >= maxOutput)
				{
					num2 = maxOutput;
					break;
				}
			}
		}
		return num2;
	}

	public void AddConnectedRecursive(IOEntity root, int inputIndex, ref HashSet<(IOEntity, int)> listToUse)
	{
		listToUse.Add((root, inputIndex));
		if (!root.WantsPassthroughPower())
		{
			return;
		}
		for (int i = 0; i < root.outputs.Length; i++)
		{
			if (!root.AllowDrainFrom(i))
			{
				continue;
			}
			IOSlot iOSlot = root.outputs[i];
			if (iOSlot.type == IOType.Electric)
			{
				IOEntity iOEntity = iOSlot.connectedTo.Get();
				if (iOEntity != null && !listToUse.Contains((iOEntity, iOSlot.connectedToSlot)) && iOEntity.WantsPower(iOSlot.connectedToSlot))
				{
					AddConnectedRecursive(iOEntity, iOSlot.connectedToSlot, ref listToUse);
				}
			}
		}
	}

	public override int DesiredPower(int inputIndex = 0)
	{
		if (rustWattSeconds >= maxCapactiySeconds)
		{
			return 0;
		}
		if (!IsFlickering())
		{
			return Mathf.Min(currentEnergy, Mathf.FloorToInt((float)maxOutput * maximumInboundEnergyRatio));
		}
		return GetHighestInputFromHistory();
	}

	public override void IOStateChanged(int inputAmount, int inputSlot)
	{
		base.IOStateChanged(inputAmount, inputSlot);
		if (IsFlickering())
		{
			if (inputHistory.Count >= 5)
			{
				inputHistory.Dequeue();
			}
			inputHistory.Enqueue(inputAmount);
		}
		if (inputSlot == 0 && rechargable)
		{
			if (!IsPowered() && !IsFlickering())
			{
				CancelInvoke(AddCharge);
				lastChargeIn = 0f;
			}
			else if (!IsInvoking(AddCharge))
			{
				InvokeRandomized(AddCharge, 1f, 1f, 0.1f);
			}
		}
	}

	public override int GetPassthroughAmount(int outputSlot = 0)
	{
		switch (outputSlot)
		{
		case 0:
			if (IsOn())
			{
				return Mathf.FloorToInt((float)maxOutput * ((rustWattSeconds >= 1f) ? 1f : 0f));
			}
			return 0;
		case 1:
			if (!HasFlag(Flags.Reserved9))
			{
				return 0;
			}
			return 1;
		default:
			return 0;
		}
	}

	public override bool WantsPower(int inputIndex)
	{
		return rustWattSeconds < maxCapactiySeconds;
	}

	public override void SendAdditionalData(BasePlayer player, int slot, bool input)
	{
		int passthroughAmountForAnySlot = GetPassthroughAmountForAnySlot(slot, input);
		ClientRPC(RpcTarget.Player("Client_ReceiveAdditionalData", player), currentEnergy, passthroughAmountForAnySlot, rustWattSeconds, (float)activeDrain);
	}

	public override void OnCircuitChanged(bool forceUpdate)
	{
		base.OnCircuitChanged(forceUpdate);
		int drain = GetDrain();
		activeDrain = drain;
	}

	public void CheckDischarge()
	{
		if (rustWattSeconds < 5f)
		{
			SetDischarging(wantsOn: false);
			return;
		}
		IOEntity iOEntity = outputs[0].connectedTo.Get();
		IOEntity iOEntity2 = outputs[1].connectedTo.Get();
		int drain = GetDrain();
		activeDrain = drain;
		SetDischarging(iOEntity != null || iOEntity2 != null);
	}

	public void SetDischarging(bool wantsOn)
	{
		SetPassthroughOn(wantsOn);
	}

	private int GetHighestInputFromHistory()
	{
		int num = 0;
		foreach (int item in inputHistory)
		{
			if (item > num)
			{
				num = item;
			}
		}
		return num;
	}

	public void TickUsage()
	{
		float oldCharge = rustWattSeconds;
		bool num = rustWattSeconds > 0f;
		if (rustWattSeconds >= 1f)
		{
			float num2 = 1f * (float)activeDrain;
			rustWattSeconds -= num2;
		}
		if (rustWattSeconds <= 0f)
		{
			rustWattSeconds = 0f;
		}
		bool flag = rustWattSeconds > 0f;
		ChargeChanged(oldCharge);
		if (num != flag)
		{
			MarkDirty();
			SendNetworkUpdate();
		}
	}

	public virtual void ChargeChanged(float oldCharge)
	{
		bool flag = rustWattSeconds > maxCapactiySeconds * 0.25f;
		bool flag2 = rustWattSeconds > maxCapactiySeconds * 0.75f;
		if (HasFlag(Flags.Reserved5) != flag || HasFlag(Flags.Reserved6) != flag2)
		{
			SetFlag(Flags.Reserved5, flag);
			SetFlag(Flags.Reserved6, flag2);
			SendNetworkUpdate_Flags();
		}
		RefreshFullChargeFlag();
	}

	private void RefreshFullChargeFlag()
	{
		bool flag = (float)Mathf.RoundToInt(rustWattSeconds / 60f) >= maxCapactiySeconds / 60f;
		bool flag2 = HasFlag(Flags.Reserved9);
		if (flag && !flag2)
		{
			SetFlag(Flags.Reserved9, b: true);
			MarkDirtyForceUpdateOutputs();
		}
		else if (!flag && flag2 && ((float)activeDrain > lastChargeIn || lastChargeIn == 0f))
		{
			SetFlag(Flags.Reserved9, b: false);
			MarkDirtyForceUpdateOutputs();
		}
	}

	public void SetCharge(float charge)
	{
		float oldCharge = rustWattSeconds;
		rustWattSeconds = charge;
		ChargeChanged(oldCharge);
	}

	public void AddCharge()
	{
		float oldCharge = rustWattSeconds;
		float num = (lastChargeIn = (float)Mathf.Min(IsFlickering() ? GetHighestInputFromHistory() : currentEnergy, DesiredPower()) * 1f * chargeRatio);
		if (num > 0f)
		{
			rustWattSeconds += num;
			rustWattSeconds = Mathf.Clamp(rustWattSeconds, 0f, maxCapactiySeconds);
			ChargeChanged(oldCharge);
		}
	}

	public void SetPassthroughOn(bool wantsOn)
	{
		if (wantsOn == IsOn() && !wasLoaded)
		{
			return;
		}
		wasLoaded = false;
		SetFlag(Flags.On, wantsOn);
		if (IsOn())
		{
			if (!IsInvoking(TickUsage))
			{
				InvokeRandomized(TickUsage, 1f, 1f, 0.1f);
			}
		}
		else
		{
			CancelInvoke(TickUsage);
		}
		MarkDirty();
	}

	public void UnBusy()
	{
		SetFlag(Flags.Busy, b: false);
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		if (info.msg.ioEntity == null)
		{
			info.msg.ioEntity = Pool.Get<ProtoBuf.IOEntity>();
		}
		info.msg.ioEntity.genericFloat1 = rustWattSeconds;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.ioEntity != null)
		{
			rustWattSeconds = info.msg.ioEntity.genericFloat1;
		}
	}

	[ServerVar]
	public static void batteryid(ConsoleSystem.Arg arg)
	{
		ElectricBattery electricBattery = BaseNetworkable.serverEntities.Find(arg.GetEntityID(1)) as ElectricBattery;
		if (electricBattery == null)
		{
			arg.ReplyWith("Not a battery");
			return;
		}
		string @string = arg.GetString(0);
		if (!(@string == "charge"))
		{
			if (@string == "deplete")
			{
				float oldCharge = electricBattery.rustWattSeconds;
				electricBattery.rustWattSeconds = 0f;
				electricBattery.ChargeChanged(oldCharge);
				arg.ReplyWith("Depleted " + electricBattery.GetDisplayName().english);
			}
			else
			{
				arg.ReplyWith("Unknown command");
			}
		}
		else
		{
			float oldCharge2 = electricBattery.rustWattSeconds;
			float num = arg.GetInt(2, (int)electricBattery.maxCapactiySeconds / 60);
			electricBattery.rustWattSeconds = Mathf.Clamp(electricBattery.rustWattSeconds + num * 60f, 0f, electricBattery.maxCapactiySeconds);
			electricBattery.ChargeChanged(oldCharge2);
			arg.ReplyWith("Charged " + electricBattery.GetDisplayName().english);
		}
	}
}
