using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using UnityEngine;

public class BoomBox : EntityComponent<BaseEntity>, INotifyLOD
{
	public AudioSource SoundSource;

	public float ConditionLossRate = 0.25f;

	public ItemDefinition[] ValidCassettes;

	public SoundDefinition PlaySfx;

	public SoundDefinition StopSfx;

	public const BaseEntity.Flags HasCassette = BaseEntity.Flags.Reserved1;

	[ServerVar(Saved = true)]
	public static int BacktrackLength = 30;

	public Action<float> HurtCallback;

	public static Dictionary<string, string> ValidStations;

	public static Dictionary<string, string> ServerValidStations;

	[ReplicatedVar(Saved = true, Help = "A list of radio stations that are valid on this server. Format: NAME,URL,NAME,URL,etc", ShowInAdminUI = true)]
	public static string ServerUrlList = string.Empty;

	private static string lastParsedServerList;

	public ShoutcastStreamer ShoutcastStreamer;

	public GameObjectRef RadioIpDialog;

	public ulong AssignedRadioBy;

	public BaseEntity BaseEntity => base.baseEntity;

	private bool isClient
	{
		get
		{
			if (base.baseEntity != null)
			{
				return base.baseEntity.isClient;
			}
			return false;
		}
	}

	public string CurrentRadioIp { get; private set; } = "rustradio.facepunch.com";


	[ServerVar]
	public static void ClearRadioByUser(ConsoleSystem.Arg arg)
	{
		ulong uInt = arg.GetUInt64(0, 0uL);
		int num = 0;
		foreach (BaseNetworkable serverEntity in BaseNetworkable.serverEntities)
		{
			if (serverEntity is DeployableBoomBox deployableBoomBox)
			{
				if (deployableBoomBox.ClearRadioByUserId(uInt))
				{
					num++;
				}
			}
			else if (serverEntity is HeldBoomBox heldBoomBox && heldBoomBox.ClearRadioByUserId(uInt))
			{
				num++;
			}
		}
		arg.ReplyWith($"Stopped and cleared saved URL of {num} boom boxes");
	}

	public void ServerTogglePlay(BaseEntity.RPCMessage msg, bool bypassPower = false)
	{
		if (IsPowered() || bypassPower)
		{
			bool play = msg.read.ReadByte() == 1;
			ServerTogglePlay(play);
		}
	}

	private void DeductCondition()
	{
		HurtCallback?.Invoke(ConditionLossRate * ConVar.Decay.scale);
	}

	public void ServerTogglePlay(bool play)
	{
		if (!(base.baseEntity == null) && HasFlag(BaseEntity.Flags.On) != play)
		{
			SetFlag(BaseEntity.Flags.On, play);
			if (base.baseEntity is IOEntity iOEntity)
			{
				iOEntity.MarkDirtyForceUpdateOutputs();
			}
			if (play && !IsInvoking(DeductCondition) && ConditionLossRate > 0f)
			{
				InvokeRepeating(DeductCondition, 1f, 1f);
			}
			else if (IsInvoking(DeductCondition))
			{
				CancelInvoke(DeductCondition);
			}
		}
	}

	public void OnCassetteInserted(Cassette c)
	{
		if (!(base.baseEntity == null))
		{
			base.baseEntity.ClientRPC(RpcTarget.NetworkGroup("Client_OnCassetteInserted"), c.net.ID);
			ServerTogglePlay(play: false);
			SetFlag(BaseEntity.Flags.Reserved1, state: true);
			base.baseEntity.SendNetworkUpdate();
		}
	}

	public void OnCassetteRemoved(Cassette c)
	{
		if (!(base.baseEntity == null))
		{
			base.baseEntity.ClientRPC(RpcTarget.NetworkGroup("Client_OnCassetteRemoved"));
			ServerTogglePlay(play: false);
			SetFlag(BaseEntity.Flags.Reserved1, state: false);
		}
	}

	private bool IsPowered()
	{
		if (base.baseEntity == null)
		{
			return false;
		}
		if (!base.baseEntity.HasFlag(BaseEntity.Flags.Reserved8))
		{
			return base.baseEntity is HeldBoomBox;
		}
		return true;
	}

	private bool IsOn()
	{
		if (base.baseEntity == null)
		{
			return false;
		}
		return base.baseEntity.IsOn();
	}

	private bool HasFlag(BaseEntity.Flags f)
	{
		if (base.baseEntity == null)
		{
			return false;
		}
		return base.baseEntity.HasFlag(f);
	}

	private void SetFlag(BaseEntity.Flags f, bool state)
	{
		if (base.baseEntity != null)
		{
			base.baseEntity.SetFlag(f, state);
		}
	}

	public static void LoadStations()
	{
		if (ValidStations == null)
		{
			ValidStations = GetStationData() ?? new Dictionary<string, string>();
			ParseServerUrlList();
		}
	}

	private static Dictionary<string, string> GetStationData()
	{
		if ((Facepunch.Application.Manifest?.Metadata)?["RadioStations"] is JArray { Count: >0 } jArray)
		{
			string[] array = new string[2];
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			{
				foreach (string item in jArray.Values<string>())
				{
					array = item.Split(',');
					if (!dictionary.ContainsKey(array[0]) && !array[1].Contains("facepunch"))
					{
						dictionary.Add(array[0], array[1]);
					}
				}
				return dictionary;
			}
		}
		return null;
	}

	private static bool IsStationValid(string url)
	{
		ParseServerUrlList();
		ShoutcastStreamer.CheckBuiltInRadios();
		if (ValidStations == null || !ValidStations.ContainsValue(url))
		{
			if (ServerValidStations == null || !ServerValidStations.ContainsValue(url))
			{
				if (ShoutcastStreamer.ParsedLocalRadioList != null)
				{
					return ShoutcastStreamer.ParsedLocalRadioList.ContainsValue(url);
				}
				return false;
			}
			return true;
		}
		return true;
	}

	public static void ParseServerUrlList()
	{
		if (ServerValidStations == null)
		{
			ServerValidStations = new Dictionary<string, string>();
		}
		if (lastParsedServerList == ServerUrlList)
		{
			return;
		}
		ServerValidStations.Clear();
		if (!string.IsNullOrEmpty(ServerUrlList))
		{
			string[] array = ServerUrlList.Split(',');
			if (array.Length % 2 != 0)
			{
				Debug.Log("Invalid number of stations in BoomBox.ServerUrlList, ensure you always have a name and a url");
				return;
			}
			for (int i = 0; i < array.Length; i += 2)
			{
				if (ServerValidStations.ContainsKey(array[i]))
				{
					Debug.Log("Duplicate station name detected in BoomBox.ServerUrlList, all station names must be unique: " + array[i]);
				}
				else
				{
					ServerValidStations.Add(array[i], array[i + 1]);
				}
			}
		}
		lastParsedServerList = ServerUrlList;
	}

	public void Server_UpdateRadioIP(BaseEntity.RPCMessage msg)
	{
		string text = msg.read.String();
		if (IsStationValid(text))
		{
			if (msg.player != null)
			{
				ulong assignedRadioBy = msg.player.userID.Get();
				AssignedRadioBy = assignedRadioBy;
			}
			CurrentRadioIp = text;
			base.baseEntity.ClientRPC(RpcTarget.NetworkGroup("OnRadioIPChanged"), CurrentRadioIp);
			if (IsOn())
			{
				ServerTogglePlay(play: false);
			}
		}
	}

	public void Save(BaseNetworkable.SaveInfo info)
	{
		if (info.msg.boomBox == null)
		{
			info.msg.boomBox = Facepunch.Pool.Get<ProtoBuf.BoomBox>();
		}
		info.msg.boomBox.radioIp = CurrentRadioIp;
		info.msg.boomBox.assignedRadioBy = AssignedRadioBy;
	}

	public bool ClearRadioByUserId(ulong id)
	{
		if (AssignedRadioBy == id)
		{
			CurrentRadioIp = string.Empty;
			AssignedRadioBy = 0uL;
			if (HasFlag(BaseEntity.Flags.On))
			{
				ServerTogglePlay(play: false);
			}
			return true;
		}
		return false;
	}

	public void Load(BaseNetworkable.LoadInfo info)
	{
		if (info.msg.boomBox != null)
		{
			CurrentRadioIp = info.msg.boomBox.radioIp;
			AssignedRadioBy = info.msg.boomBox.assignedRadioBy;
		}
	}
}
