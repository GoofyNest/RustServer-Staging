#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Facepunch.Extend;
using Facepunch.Rust;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

public class CodeLock : BaseLock
{
	public GameObjectRef keyEnterDialog;

	public GameObjectRef effectUnlocked;

	public GameObjectRef effectLocked;

	public GameObjectRef effectDenied;

	public GameObjectRef effectCodeChanged;

	public GameObjectRef effectShock;

	private bool hasCode;

	public const Flags Flag_CodeEntryBlocked = Flags.Reserved11;

	public static readonly Translate.Phrase blockwarning = new Translate.Phrase("codelock.blockwarning", "Further failed attempts will block code entry for some time");

	[ServerVar]
	public static float maxFailedAttempts = 8f;

	[ServerVar]
	public static float lockoutCooldown = 900f;

	private bool hasGuestCode;

	private string code = string.Empty;

	private string guestCode = string.Empty;

	[NonSerialized]
	public List<ulong> whitelistPlayers = new List<ulong>();

	[NonSerialized]
	public List<ulong> guestPlayers = new List<ulong>();

	private int wrongCodes;

	private float lastWrongTime = float.NegativeInfinity;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("CodeLock.OnRpcMessage"))
		{
			if (rpc == 4013784361u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_ChangeCode ");
				}
				using (TimeWarning.New("RPC_ChangeCode"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(4013784361u, "RPC_ChangeCode", this, player, 3f, checkParent: true))
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
							RPC_ChangeCode(rpc2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_ChangeCode");
					}
				}
				return true;
			}
			if (rpc == 2626067433u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - TryLock ");
				}
				using (TimeWarning.New("TryLock"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(2626067433u, "TryLock", this, player, 3f, checkParent: true))
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
							TryLock(rpc3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in TryLock");
					}
				}
				return true;
			}
			if (rpc == 1718262 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - TryUnlock ");
				}
				using (TimeWarning.New("TryUnlock"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(1718262u, "TryUnlock", this, player, 3f, checkParent: true))
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
							TryUnlock(rpc4);
						}
					}
					catch (Exception exception3)
					{
						Debug.LogException(exception3);
						player.Kick("RPC Error in TryUnlock");
					}
				}
				return true;
			}
			if (rpc == 418605506 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - UnlockWithCode ");
				}
				using (TimeWarning.New("UnlockWithCode"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(418605506u, "UnlockWithCode", this, player, 3f, checkParent: true))
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
							UnlockWithCode(rpc5);
						}
					}
					catch (Exception exception4)
					{
						Debug.LogException(exception4);
						player.Kick("RPC Error in UnlockWithCode");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public bool IsCodeEntryBlocked()
	{
		return HasFlag(Flags.Reserved11);
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.codeLock != null)
		{
			hasCode = info.msg.codeLock.hasCode;
			hasGuestCode = info.msg.codeLock.hasGuestCode;
			if (info.msg.codeLock.pv != null)
			{
				code = info.msg.codeLock.pv.code;
				whitelistPlayers = info.msg.codeLock.pv.users.ShallowClonePooled();
				guestCode = info.msg.codeLock.pv.guestCode;
				guestPlayers = info.msg.codeLock.pv.guestUsers.ShallowClonePooled();
			}
		}
	}

	internal void DoEffect(string effect)
	{
		Effect.server.Run(effect, this, 0u, Vector3.zero, Vector3.forward);
	}

	public override bool OnTryToOpen(BasePlayer player)
	{
		if (!IsLocked())
		{
			return true;
		}
		if (whitelistPlayers.Contains(player.userID) || guestPlayers.Contains(player.userID))
		{
			DoEffect(effectUnlocked.resourcePath);
			return true;
		}
		DoEffect(effectDenied.resourcePath);
		return false;
	}

	public override bool OnTryToClose(BasePlayer player)
	{
		if (!IsLocked())
		{
			return true;
		}
		if (whitelistPlayers.Contains(player.userID) || guestPlayers.Contains(player.userID))
		{
			DoEffect(effectUnlocked.resourcePath);
			return true;
		}
		DoEffect(effectDenied.resourcePath);
		return false;
	}

	public override bool CanUseNetworkCache(Connection connection)
	{
		return false;
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.codeLock = Facepunch.Pool.Get<ProtoBuf.CodeLock>();
		info.msg.codeLock.hasGuestCode = guestCode.Length > 0;
		info.msg.codeLock.hasCode = code.Length > 0;
		if (!info.forDisk && info.forConnection != null)
		{
			info.msg.codeLock.hasAuth = whitelistPlayers.Contains(info.forConnection.userid);
			info.msg.codeLock.hasGuestAuth = guestPlayers.Contains(info.forConnection.userid);
		}
		if (info.forDisk)
		{
			info.msg.codeLock.pv = Facepunch.Pool.Get<ProtoBuf.CodeLock.Private>();
			info.msg.codeLock.pv.code = code;
			info.msg.codeLock.pv.users = whitelistPlayers.ShallowClonePooled();
			info.msg.codeLock.pv.guestCode = guestCode;
			info.msg.codeLock.pv.guestUsers = guestPlayers.ShallowClonePooled();
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f, CheckParent = true)]
	private void RPC_ChangeCode(RPCMessage rpc)
	{
		if (!rpc.player.CanInteract())
		{
			return;
		}
		string text = rpc.read.String();
		bool flag = rpc.read.Bit();
		if (!IsLocked() && text.Length == 4 && text.IsNumeric() && !(!hasCode && flag))
		{
			if (!hasCode && !flag)
			{
				SetFlag(Flags.Locked, b: true);
			}
			Analytics.Azure.OnCodelockChanged(rpc.player, this, flag ? guestCode : code, text, flag);
			if (!flag)
			{
				code = text;
				hasCode = code.Length > 0;
				whitelistPlayers.Clear();
				whitelistPlayers.Add(rpc.player.userID);
			}
			else
			{
				guestCode = text;
				hasGuestCode = guestCode.Length > 0;
				guestPlayers.Clear();
				guestPlayers.Add(rpc.player.userID);
			}
			DoEffect(effectCodeChanged.resourcePath);
			SendNetworkUpdate();
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f, CheckParent = true)]
	private void TryUnlock(RPCMessage rpc)
	{
		if (rpc.player.CanInteract() && IsLocked() && !IsCodeEntryBlocked() && whitelistPlayers.Contains(rpc.player.userID))
		{
			DoEffect(effectUnlocked.resourcePath);
			SetFlag(Flags.Locked, b: false);
			SendNetworkUpdate();
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f, CheckParent = true)]
	private void TryLock(RPCMessage rpc)
	{
		if (rpc.player.CanInteract() && !IsLocked() && code.Length == 4 && whitelistPlayers.Contains(rpc.player.userID))
		{
			DoEffect(effectLocked.resourcePath);
			SetFlag(Flags.Locked, b: true);
			SendNetworkUpdate();
		}
	}

	public void ClearCodeEntryBlocked()
	{
		SetFlag(Flags.Reserved11, b: false);
		wrongCodes = 0;
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f, CheckParent = true)]
	private void UnlockWithCode(RPCMessage rpc)
	{
		if (!rpc.player.CanInteract() || !IsLocked() || IsCodeEntryBlocked())
		{
			return;
		}
		string text = rpc.read.String();
		bool flag = text == guestCode;
		bool flag2 = text == code;
		if (!(text == code) && (!hasGuestCode || !(text == guestCode)))
		{
			if (UnityEngine.Time.realtimeSinceStartup > lastWrongTime + 60f)
			{
				wrongCodes = 0;
			}
			DoEffect(effectDenied.resourcePath);
			DoEffect(effectShock.resourcePath);
			rpc.player.Hurt((float)(wrongCodes + 1) * 5f, DamageType.ElectricShock, this, useProtection: false);
			wrongCodes++;
			if (wrongCodes > 5)
			{
				rpc.player.ShowToast(GameTip.Styles.Red_Normal, blockwarning, false);
			}
			if ((float)wrongCodes >= maxFailedAttempts)
			{
				SetFlag(Flags.Reserved11, b: true);
				Invoke(ClearCodeEntryBlocked, lockoutCooldown);
			}
			lastWrongTime = UnityEngine.Time.realtimeSinceStartup;
			return;
		}
		SendNetworkUpdate();
		if (flag2)
		{
			if (!whitelistPlayers.Contains(rpc.player.userID))
			{
				DoEffect(effectCodeChanged.resourcePath);
				whitelistPlayers.Add(rpc.player.userID);
				wrongCodes = 0;
			}
			Analytics.Azure.OnCodeLockEntered(rpc.player, this, isGuest: false);
		}
		else if (flag && !guestPlayers.Contains(rpc.player.userID))
		{
			DoEffect(effectCodeChanged.resourcePath);
			guestPlayers.Add(rpc.player.userID);
			Analytics.Azure.OnCodeLockEntered(rpc.player, this, isGuest: true);
		}
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		SetFlag(Flags.Reserved11, b: false);
	}
}
