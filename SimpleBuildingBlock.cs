#define UNITY_ASSERTIONS
using System;
using ConVar;
using Network;
using UnityEngine;
using UnityEngine.Assertions;

public class SimpleBuildingBlock : StabilityEntity, ISimpleUpgradable
{
	public ItemDefinition UpgradeItem;

	public Menu.Option UpgradeMenu;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("SimpleBuildingBlock.OnRpcMessage"))
		{
			if (rpc == 2824056853u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - DoSimpleUpgrade ");
				}
				using (TimeWarning.New("DoSimpleUpgrade"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(2824056853u, "DoSimpleUpgrade", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.IsVisible.Test(2824056853u, "DoSimpleUpgrade", this, player, 3f))
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
							DoSimpleUpgrade(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in DoSimpleUpgrade");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public ItemDefinition GetUpgradeItem()
	{
		return UpgradeItem;
	}

	public bool CanUpgrade(BasePlayer player)
	{
		return SimpleUpgrade.CanUpgrade(this, UpgradeItem, player);
	}

	public void DoUpgrade(BasePlayer player)
	{
		SimpleUpgrade.DoUpgrade(this, player);
	}

	public Menu.Option GetUpgradeMenuOption()
	{
		return UpgradeMenu;
	}

	public bool UpgradingEnabled()
	{
		return UpgradeItem != null;
	}

	public bool CostIsItem()
	{
		return true;
	}

	[RPC_Server]
	[RPC_Server.CallsPerSecond(5uL)]
	[RPC_Server.IsVisible(3f)]
	public void DoSimpleUpgrade(RPCMessage msg)
	{
		if (base.SecondsSinceAttacked < 30f)
		{
			msg.player.ShowToast(GameTip.Styles.Error, ConstructionErrors.CantUpgradeRecentlyDamaged, false, (30f - base.SecondsSinceAttacked).ToString("N0"));
		}
		else if (CanUpgrade(msg.player))
		{
			DoUpgrade(msg.player);
		}
	}
}
