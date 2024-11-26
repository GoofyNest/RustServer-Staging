using System.Net;
using ConVar;

namespace Facepunch.Rust;

public static class HttpManager
{
	public static void UpdateMaxConnections()
	{
		ServicePointManager.DefaultConnectionLimit = Server.http_connection_limit;
	}
}
