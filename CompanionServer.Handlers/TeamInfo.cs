using Facepunch;
using ProtoBuf;

namespace CompanionServer.Handlers;

public class TeamInfo : BasePlayerHandler<AppEmpty>
{
	public override void Execute()
	{
		RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(base.UserId);
		AppTeamInfo teamInfo = ((playerTeam == null) ? base.Player.GetAppTeamInfo(base.UserId) : playerTeam.GetAppTeamInfo(base.UserId));
		AppResponse appResponse = Pool.Get<AppResponse>();
		appResponse.teamInfo = teamInfo;
		Send(appResponse);
	}
}
