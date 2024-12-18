#define UNITY_ASSERTIONS
using System;
using ConVar;
using Network;
using UnityEngine;
using UnityEngine.Assertions;

public class Handcuffs : BaseMelee
{
	public static int PrisonerHoodItemID = -892718768;

	[ServerVar]
	public static float restrainedPushDamage = 5f;

	[ServerVar]
	public static float maxConditionRepairLossOnPush = 0.4f;

	[Header("Handcuffs")]
	public AnimatorOverrideController CaptiveHoldAnimationOverride;

	public GameObjectRef lockEffect;

	public GameObjectRef escapeEffect;

	[Header("Handcuff Behaviour")]
	public bool BlockInventory = true;

	public bool BlockSuicide = true;

	public bool BlockUse = true;

	public bool BlockCrafting = true;

	public float UnlockMiniGameDuration = 60f;

	public float UseDistance = 1.8f;

	public float ConditionLossPerSecond = 1f;

	private float unlockStartTime;

	private float startCondition;

	public bool Locked
	{
		get
		{
			if (GetItem() != null)
			{
				return GetItem().IsOn();
			}
			return false;
		}
	}

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("Handcuffs.OnRpcMessage"))
		{
			if (rpc == 695796023 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_ReqCancelUnlockMiniGame ");
				}
				using (TimeWarning.New("RPC_ReqCancelUnlockMiniGame"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(695796023u, "RPC_ReqCancelUnlockMiniGame", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(695796023u, "RPC_ReqCancelUnlockMiniGame", this, player))
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
							RPCMessage rpc2 = rPCMessage;
							RPC_ReqCancelUnlockMiniGame(rpc2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_ReqCancelUnlockMiniGame");
					}
				}
				return true;
			}
			if (rpc == 3883360127u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_ReqCompleteUnlockMiniGame ");
				}
				using (TimeWarning.New("RPC_ReqCompleteUnlockMiniGame"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3883360127u, "RPC_ReqCompleteUnlockMiniGame", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(3883360127u, "RPC_ReqCompleteUnlockMiniGame", this, player))
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
							RPCMessage rpc3 = rPCMessage;
							RPC_ReqCompleteUnlockMiniGame(rpc3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in RPC_ReqCompleteUnlockMiniGame");
					}
				}
				return true;
			}
			if (rpc == 1571851761 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_ReqLock ");
				}
				using (TimeWarning.New("RPC_ReqLock"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(1571851761u, "RPC_ReqLock", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(1571851761u, "RPC_ReqLock", this, player))
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
							RPCMessage rpc4 = rPCMessage;
							RPC_ReqLock(rpc4);
						}
					}
					catch (Exception exception3)
					{
						Debug.LogException(exception3);
						player.Kick("RPC Error in RPC_ReqLock");
					}
				}
				return true;
			}
			if (rpc == 3248381320u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_ReqStartUnlockMiniGame ");
				}
				using (TimeWarning.New("RPC_ReqStartUnlockMiniGame"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3248381320u, "RPC_ReqStartUnlockMiniGame", this, player, 5uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(3248381320u, "RPC_ReqStartUnlockMiniGame", this, player))
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
							RPCMessage rpc5 = rPCMessage;
							RPC_ReqStartUnlockMiniGame(rpc5);
						}
					}
					catch (Exception exception4)
					{
						Debug.LogException(exception4);
						player.Kick("RPC Error in RPC_ReqStartUnlockMiniGame");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		Item item = GetItem();
		if (base.isServer && item != null)
		{
			SetLocked(Locked);
		}
		SetWMLocked(Locked);
	}

	private void SetWMLocked(bool flag)
	{
	}

	private void StartUnlockMiniGame()
	{
		InterruptUnlockMiniGame();
		unlockStartTime = UnityEngine.Time.realtimeSinceStartup;
	}

	public void HeldWhenOwnerDied(BasePlayer player)
	{
		if (Locked)
		{
			SetLocked(flag: false, player);
		}
	}

	public void SetLocked(bool flag, BasePlayer player = null, Item handcuffsItem = null)
	{
		if (base.isClient)
		{
			return;
		}
		if (handcuffsItem == null)
		{
			handcuffsItem = GetOwnerItem();
		}
		handcuffsItem?.SetFlag(Item.Flag.IsOn, flag);
		if (player == null)
		{
			player = GetOwnerPlayer();
		}
		if (!(player == null))
		{
			player.SetPlayerFlag(BasePlayer.PlayerFlags.IsRestrained, flag);
			if (handcuffsItem != null)
			{
				player.restraintItemId = (flag ? new ItemId?(handcuffsItem.uid) : null);
			}
			else
			{
				player.restraintItemId = null;
			}
			if (BlockInventory)
			{
				player.inventory.SetLockedByRestraint(flag);
			}
			ClientRPC(RpcTarget.Player("CL_SetLocked", player), Locked);
		}
	}

	[ServerVar]
	public static void togglecuffslocked(ConsoleSystem.Arg args)
	{
		BasePlayer basePlayer = args.Player();
		HeldEntity heldEntity = basePlayer.GetHeldEntity();
		if (!(heldEntity == null))
		{
			Handcuffs handcuffs = heldEntity as Handcuffs;
			if (!(handcuffs == null))
			{
				handcuffs.SetLocked(!handcuffs.Locked, basePlayer);
			}
		}
	}

	private void ModifyConditionForElapsedTime(float elapsed)
	{
		if (unlockStartTime <= 0f || elapsed <= 0f)
		{
			return;
		}
		Item ownerItem = GetOwnerItem();
		if (ownerItem == null)
		{
			return;
		}
		float num = elapsed * ConditionLossPerSecond;
		if (num + 1f >= ownerItem.condition)
		{
			num = ownerItem.condition;
		}
		if (!(num > 1f) && !(num >= ownerItem.condition))
		{
			return;
		}
		ownerItem.condition -= num;
		if (ownerItem.condition <= 0f)
		{
			BasePlayer ownerPlayer = GetOwnerPlayer();
			if (ownerPlayer != null)
			{
				ownerPlayer.ApplyWoundedStartTime();
			}
			SetLocked(flag: false);
			ownerItem.UseItem();
		}
	}

	public void RepairOnPush()
	{
		if (base.isServer)
		{
			GetOwnerItem()?.DoRepair(maxConditionRepairLossOnPush);
		}
	}

	public void InterruptUnlockMiniGame(bool wasPushedOrDamaged = false)
	{
		if (base.isServer && unlockStartTime > 0f && !wasPushedOrDamaged)
		{
			ModifyConditionForElapsedTime(UnityEngine.Time.realtimeSinceStartup - unlockStartTime);
		}
		unlockStartTime = 0f;
		if (base.isServer)
		{
			BasePlayer ownerPlayer = GetOwnerPlayer();
			if (!(ownerPlayer == null))
			{
				ClientRPC(RpcTarget.Player("CL_CancelUnlockMiniGame", ownerPlayer), wasPushedOrDamaged ? 2f : 0f);
			}
		}
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(5uL)]
	private void RPC_ReqStartUnlockMiniGame(RPCMessage rpc)
	{
		BasePlayer player = rpc.player;
		if (!(player == null))
		{
			SV_StartUnlockMiniGame(player);
		}
	}

	private void SV_StartUnlockMiniGame(BasePlayer player)
	{
		if (!player.IsDead() && !player.IsWounded())
		{
			StartUnlockMiniGame();
			ClientRPC(RpcTarget.Player("CL_StartUnlockMiniGame", player));
		}
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(5uL)]
	private void RPC_ReqCancelUnlockMiniGame(RPCMessage rpc)
	{
		BasePlayer player = rpc.player;
		if (!(player == null))
		{
			SV_CancelUnlockMiniGame(player);
		}
	}

	private void SV_CancelUnlockMiniGame(BasePlayer player)
	{
		InterruptUnlockMiniGame();
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(5uL)]
	private void RPC_ReqCompleteUnlockMiniGame(RPCMessage rpc)
	{
		BasePlayer player = rpc.player;
		if (!(player == null))
		{
			SV_ReqCompleteUnlockMiniGame(player);
		}
	}

	private void SV_ReqCompleteUnlockMiniGame(BasePlayer player)
	{
		InterruptUnlockMiniGame();
		Effect.server.Run(escapeEffect.resourcePath, player, 0u, Vector3.zero, Vector3.zero);
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(5uL)]
	private void RPC_ReqLock(RPCMessage rpc)
	{
		BasePlayer player = rpc.player;
		if (!(player == null))
		{
			NetworkableId uid = rpc.read.EntityID();
			BasePlayer basePlayer = BaseNetworkable.serverEntities.Find(uid) as BasePlayer;
			if (!(basePlayer == null))
			{
				SV_HandcuffVictim(basePlayer, player);
			}
		}
	}

	private void SV_HandcuffVictim(BasePlayer victim, BasePlayer handcuffer)
	{
		if (victim == null || handcuffer == null || victim.IsRestrained || (!victim.CurrentGestureIsSurrendering && !victim.IsWounded()) || Vector3.Distance(victim.transform.position, handcuffer.transform.position) > UseDistance)
		{
			return;
		}
		Item ownerItem = GetOwnerItem();
		if (ownerItem == null)
		{
			return;
		}
		victim.SetPlayerFlag(BasePlayer.PlayerFlags.IsRestrained, b: true);
		victim.SendNetworkUpdateImmediate();
		ownerItem.SetFlag(Item.Flag.IsOn, b: true);
		bool flag = true;
		if (!ownerItem.MoveToContainer(victim.inventory.containerBelt))
		{
			Item slot = victim.inventory.containerBelt.GetSlot(0);
			if (slot != null)
			{
				if (!slot.MoveToContainer(victim.inventory.containerMain))
				{
					slot.DropAndTossUpwards(victim.transform.position);
				}
				if (!ownerItem.MoveToContainer(victim.inventory.containerBelt))
				{
					flag = false;
				}
			}
		}
		if (!flag)
		{
			ownerItem.SetFlag(Item.Flag.IsOn, b: false);
			victim.SetPlayerFlag(BasePlayer.PlayerFlags.IsRestrained, b: false);
		}
		ownerItem.MarkDirty();
		if (flag)
		{
			victim.Server_CancelGesture();
			if (victim.IsBot)
			{
				Inventory.EquipItemInSlot(victim, 0);
			}
			victim.ClientRPC(RpcTarget.Player("SetActiveBeltSlot", victim), ownerItem.position, ownerItem.uid);
			SetLocked(flag: true, victim, ownerItem);
			Effect.server.Run(lockEffect.resourcePath, victim, 0u, Vector3.zero, Vector3.zero);
		}
	}

	public void UnlockAndReturnToPlayer(BasePlayer returnToPlayer)
	{
		SetLocked(flag: false);
		if (!(returnToPlayer == null))
		{
			Item ownerItem = GetOwnerItem();
			if (ownerItem != null)
			{
				returnToPlayer.GiveItem(ownerItem);
			}
		}
	}

	public override bool CanHit(HitTest info)
	{
		if (info.HitEntity is BasePlayer basePlayer)
		{
			if (!basePlayer.CurrentGestureIsSurrendering && !basePlayer.IsSleeping())
			{
				return basePlayer.IsWounded();
			}
			return true;
		}
		return false;
	}

	public override void DoAttackShared(HitInfo info)
	{
		if (!base.isServer)
		{
			return;
		}
		BasePlayer basePlayer = info.HitEntity as BasePlayer;
		if (basePlayer != null)
		{
			BasePlayer ownerPlayer = GetOwnerPlayer();
			if (ownerPlayer != null && basePlayer != null)
			{
				SV_HandcuffVictim(basePlayer, ownerPlayer);
			}
		}
	}
}
