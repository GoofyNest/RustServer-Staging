namespace Facepunch.Rust;

public class TickLogging
{
	public static AzureAnalyticsUploader tickUploader;

	[ServerVar]
	[Help("time (in seconds) before the tick uploader is disposed and recreated")]
	public static int tick_uploader_lifetime = 60;
}
