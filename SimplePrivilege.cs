using System.Collections.Generic;
using System.Linq;
using Facepunch;
using ProtoBuf;

public class SimplePrivilege : BaseEntity
{
	protected List<PlayerNameID> authorizedPlayers = new List<PlayerNameID>();

	protected const Flags Flag_MaxAuths = Flags.Reserved5;

	public override void ResetState()
	{
		base.ResetState();
		authorizedPlayers.Clear();
	}

	public bool IsAuthed(BasePlayer player)
	{
		return authorizedPlayers.Any((PlayerNameID x) => x.userid == (ulong)player.userID);
	}

	public bool IsAuthed(ulong userID)
	{
		return authorizedPlayers.Any((PlayerNameID x) => x.userid == userID);
	}

	public bool AnyAuthed()
	{
		return authorizedPlayers.Count > 0;
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.buildingPrivilege = Pool.Get<BuildingPrivilege>();
		info.msg.buildingPrivilege.users = authorizedPlayers;
	}

	public override void PostSave(SaveInfo info)
	{
		info.msg.buildingPrivilege.users = null;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		authorizedPlayers.Clear();
		if (info.msg.buildingPrivilege != null && info.msg.buildingPrivilege.users != null)
		{
			authorizedPlayers = info.msg.buildingPrivilege.users;
			info.msg.buildingPrivilege.users = null;
		}
	}

	protected bool AtMaxAuthCapacity()
	{
		return HasFlag(Flags.Reserved5);
	}

	protected void UpdateMaxAuthCapacity()
	{
		BaseGameMode activeGameMode = BaseGameMode.GetActiveGameMode(serverside: true);
		if ((bool)activeGameMode && activeGameMode.limitTeamAuths)
		{
			SetFlag(Flags.Reserved5, authorizedPlayers.Count >= activeGameMode.GetMaxRelationshipTeamSize());
		}
	}
}
