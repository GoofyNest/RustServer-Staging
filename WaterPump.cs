using UnityEngine;

public class WaterPump : LiquidContainer
{
	public Transform WaterResourceLocation;

	public float PumpInterval = 20f;

	public int AmountPerPump = 30;

	public int PowerConsumption = 5;

	public override bool IsGravitySource => true;

	public override int ConsumptionAmount()
	{
		return PowerConsumption;
	}

	private void CreateWater()
	{
		if (!IsFull())
		{
			ItemDefinition itemDefinition = WaterResource.SV_GetAtPoint(WaterResourceLocation.position);
			if (itemDefinition != null)
			{
				base.inventory.AddItem(itemDefinition, AmountPerPump, 0uL);
				UpdateOnFlag();
			}
		}
	}

	public override void OnFlagsChanged(Flags old, Flags next)
	{
		base.OnFlagsChanged(old, next);
		bool flag = next.HasFlag(Flags.Reserved8);
		if (!base.isServer || old.HasFlag(Flags.Reserved8) == flag)
		{
			return;
		}
		if (flag)
		{
			if (!IsInvoking(CreateWater))
			{
				InvokeRandomized(CreateWater, PumpInterval, PumpInterval, PumpInterval * 0.1f);
			}
		}
		else if (IsInvoking(CreateWater))
		{
			CancelInvoke(CreateWater);
		}
	}

	public override int GetPassthroughAmount(int outputSlot = 0)
	{
		return Mathf.Clamp(GetLiquidCount(), 0, maxOutputFlow);
	}

	private bool IsFull()
	{
		if (base.inventory.itemList.Count == 0)
		{
			return false;
		}
		if (base.inventory.itemList[0].amount < base.inventory.maxStackSize)
		{
			return false;
		}
		return true;
	}
}
