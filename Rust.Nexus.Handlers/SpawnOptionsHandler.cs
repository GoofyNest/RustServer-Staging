using System.Collections.Generic;
using Facepunch;
using ProtoBuf;
using ProtoBuf.Nexus;

namespace Rust.Nexus.Handlers;

public class SpawnOptionsHandler : BaseNexusRequestHandler<SpawnOptionsRequest>
{
	protected override void Handle()
	{
		Response response = BaseNexusRequestHandler<SpawnOptionsRequest>.NewResponse();
		response.spawnOptions = Pool.Get<SpawnOptionsResponse>();
		response.spawnOptions.spawnOptions = Pool.Get<List<RespawnInformation.SpawnOptions>>();
		BasePlayer.GetRespawnOptionsForPlayer(response.spawnOptions.spawnOptions, base.Request.userId);
		SendSuccess(response);
	}
}
