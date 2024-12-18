using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ConVar;
using Cysharp.Text;
using UnityEngine;

namespace Facepunch.Rust;

public class AzureAnalyticsUploader : Pool.IPooled
{
	public static bool UsePooling;

	public static ClientSecretCredential Credential;

	private ConcurrentQueue<EventRecord> queue = new ConcurrentQueue<EventRecord>();

	private BlobClient _blobClient;

	private Stream Stream;

	private GZipStream ZipStream;

	private Utf8ValueStringBuilder Writer;

	private bool disposed;

	private BlobOpenWriteOptions blobWriteOptions = new BlobOpenWriteOptions
	{
		HttpHeaders = new BlobHttpHeaders
		{
			ContentEncoding = "gzip"
		}
	};

	public TimeSpan LoopDelay { get; set; }

	public DateTime Expiry { get; private set; }

	public bool StrictMode { get; set; }

	public AnalyticsDocumentMode DocumentMode { get; private set; }

	public bool UseJsonDataObject { get; set; }

	public AzureAnalyticsUploader()
	{
		Writer = ZString.CreateUtf8StringBuilder();
	}

	public void EnterPool()
	{
		disposed = true;
	}

	private void Initialize()
	{
		LoopDelay = TimeSpan.FromMilliseconds(250.0);
		Expiry = DateTime.MinValue;
		StrictMode = false;
		UseJsonDataObject = false;
		DocumentMode = AnalyticsDocumentMode.JSON;
		EmptyUploadQueue();
		_blobClient = null;
		Stream = null;
		ZipStream = null;
		disposed = false;
	}

	public void LeavePool()
	{
		Initialize();
	}

	public bool TryFlush()
	{
		if (Expiry >= DateTime.UtcNow)
		{
			return false;
		}
		disposed = true;
		return true;
	}

	public static AzureAnalyticsUploader Create(string table, TimeSpan timeout, AnalyticsDocumentMode mode = AnalyticsDocumentMode.JSON)
	{
		AzureAnalyticsUploader azureAnalyticsUploader;
		if (UsePooling)
		{
			azureAnalyticsUploader = Pool.Get<AzureAnalyticsUploader>();
		}
		else
		{
			azureAnalyticsUploader = new AzureAnalyticsUploader();
			azureAnalyticsUploader.Initialize();
		}
		azureAnalyticsUploader.Expiry = DateTime.UtcNow + timeout;
		azureAnalyticsUploader.DocumentMode = mode;
		if (string.IsNullOrEmpty(Analytics.GetContainerUrl()))
		{
			Debug.Log("No analytics_bulk_container_url or analytics_bulk_connection_string set, disabling bulk uploader.");
			azureAnalyticsUploader.disposed = true;
			return azureAnalyticsUploader;
		}
		string text = ((mode == AnalyticsDocumentMode.JSON) ? ".json" : ".csv");
		string blobName = Path.Combine(table, Server.server_id, Guid.NewGuid().ToString("N") + text + ".gz");
		BlobContainerClient blobContainerClient;
		if (!string.IsNullOrEmpty(Analytics.BulkUploadConnectionString))
		{
			blobContainerClient = new BlobContainerClient(new Uri(Analytics.BulkUploadConnectionString));
		}
		else
		{
			if (string.IsNullOrEmpty(Analytics.AzureTenantId) || string.IsNullOrEmpty(Analytics.AzureClientId) || string.IsNullOrEmpty(Analytics.AzureClientSecret))
			{
				Debug.Log("analytics_bulk_container_url set but missing Azure AD credentials, disabling bulk uploader.");
				azureAnalyticsUploader.disposed = true;
				return azureAnalyticsUploader;
			}
			if (Credential == null)
			{
				Credential = new ClientSecretCredential(Analytics.AzureTenantId, Analytics.AzureClientId, Analytics.AzureClientSecret);
			}
			blobContainerClient = new BlobContainerClient(new Uri(Analytics.BulkContainerUrl), (TokenCredential)Credential, (BlobClientOptions)null);
		}
		azureAnalyticsUploader._blobClient = blobContainerClient.GetBlobClient(blobName);
		Task.Run((Func<Task>)azureAnalyticsUploader.UploadThread);
		return azureAnalyticsUploader;
	}

	public void Append(EventRecord record)
	{
		if (disposed)
		{
			if (StrictMode)
			{
				throw new Exception("Trying to append to a disposed uploader: make sure to dispose the uploader properly!");
			}
			record.MarkSubmitted();
			Pool.Free(ref record);
		}
		else
		{
			queue.Enqueue(record);
		}
	}

	private async Task CreateBlobAsync()
	{
		CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(10.0));
		blobWriteOptions.HttpHeaders.ContentType = ((DocumentMode == AnalyticsDocumentMode.JSON) ? "application/json" : "text/csv");
		try
		{
			Stream = await _blobClient.OpenWriteAsync(overwrite: true, blobWriteOptions, cancellationTokenSource.Token);
		}
		catch (RequestFailedException ex)
		{
			switch (ex.Status)
			{
			case 403:
				Debug.Log("Access denied to container " + _blobClient.BlobContainerName + ", disabling bulk uploader.");
				break;
			case 404:
				Debug.Log("Container " + _blobClient.BlobContainerName + " doesn't exist, disabling bulk uploader.");
				break;
			default:
				Debug.Log($"Unknown error when opening Azure container, status code: {ex.Status}, disabling bulk uploader.");
				Debug.LogException(ex);
				break;
			}
			EmptyUploadQueue();
			return;
		}
		ZipStream = new GZipStream(Stream, System.IO.Compression.CompressionLevel.Fastest);
		Writer.Clear();
	}

	private async Task UploadThread()
	{
		try
		{
			_ = 2;
			try
			{
				while (!disposed || !queue.IsEmpty)
				{
					EventRecord record;
					while (queue.TryDequeue(out record))
					{
						if (Stream == null)
						{
							await CreateBlobAsync();
							if (Stream == null)
							{
								record.MarkSubmitted();
								Pool.Free(ref record);
								continue;
							}
						}
						Writer.Clear();
						if (DocumentMode == AnalyticsDocumentMode.JSON)
						{
							record.SerializeAsJson(ref Writer, UseJsonDataObject);
						}
						else if (DocumentMode == AnalyticsDocumentMode.CSV)
						{
							record.SerializeAsCSV(ref Writer);
						}
						Writer.AppendLine();
						await Writer.WriteToAsync(ZipStream);
						record.MarkSubmitted();
						Pool.Free(ref record);
					}
					await Task.Delay(LoopDelay);
				}
			}
			catch (Exception exception)
			{
				disposed = true;
				Debug.LogException(exception);
				EmptyUploadQueue();
			}
		}
		finally
		{
			await DisposeStreamsAsync();
			if (UsePooling)
			{
				AzureAnalyticsUploader obj = this;
				Pool.Free(ref obj);
			}
		}
	}

	private void EmptyUploadQueue()
	{
		EventRecord result;
		while (queue.TryDequeue(out result))
		{
			result.MarkSubmitted();
			Pool.Free(ref result);
		}
	}

	private async Task DisposeStreamsAsync()
	{
		if (ZipStream != null)
		{
			await ZipStream.DisposeAsync();
			ZipStream = null;
		}
		if (Stream != null)
		{
			await Stream.DisposeAsync();
			Stream = null;
		}
	}
}
