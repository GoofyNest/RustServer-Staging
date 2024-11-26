namespace Facepunch.Rust;

public static class AzureAnalyticsUploaderEx
{
	public static bool NeedsCreation(this AzureAnalyticsUploader uploader)
	{
		return uploader?.TryFlush() ?? true;
	}
}
