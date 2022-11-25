using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Facepunch;
using Newtonsoft.Json;
using Rust;
using Steamworks;
using UnityEngine;

public class ClientSteamAuthReporter
{
	private class AuthChangeEvent
	{
		[JsonProperty("state")]
		public string State;

		[JsonProperty("token")]
		public byte[] SessionKey;

		[JsonProperty("ip")]
		public string Ip;

		[JsonProperty("port")]
		public int Port;
	}

	private List<AuthChangeEvent> pendingEvents = new List<AuthChangeEvent>();

	private object _lock = new object();

	private HttpClient _http = new HttpClient();

	private Task _uploadTask;

	private bool isConnected;

	private byte[] _sessionToken;

	private string _ip;

	private int _port;

	private DateTime lastHeartbeat = DateTime.MinValue;

	public TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(120.0);

	private const string BaseUrl = "https://api.facepunch.com/api/public/";

	private const string UploadChangesRoute = "https://api.facepunch.com/api/public/auth-tracking/client";

	public static ClientSteamAuthReporter Instance { get; } = new ClientSteamAuthReporter();


	public ClientSteamAuthReporter()
	{
		double num = 120.0 * (0.9 + new System.Random().NextDouble() * 0.2);
		HeartbeatInterval = TimeSpan.FromMinutes(num);
		lastHeartbeat = DateTime.UtcNow.AddMinutes(num * new System.Random().NextDouble());
	}

	private void EnsureUploadTask()
	{
		if (_uploadTask == null)
		{
			_uploadTask = Task.Run((Func<Task>)UploadThread);
		}
	}

	public void OnConnectToServer(byte[] sessionToken, string ip, int port)
	{
		if (isConnected)
		{
			Debug.LogError("Already connected to server!");
			return;
		}
		isConnected = true;
		EnsureUploadTask();
		AuthTicket authSessionTicket = SteamUser.GetAuthSessionTicket();
		_sessionToken = authSessionTicket.Data;
		_ip = ip;
		_port = port;
		lock (_lock)
		{
			pendingEvents.Add(new AuthChangeEvent
			{
				State = "connect",
				SessionKey = _sessionToken,
				Ip = _ip,
				Port = _port
			});
		}
	}

	public void OnDisconnectFromServer()
	{
		if (isConnected)
		{
			isConnected = false;
			EnsureUploadTask();
			lock (_lock)
			{
				pendingEvents.Add(new AuthChangeEvent
				{
					State = "disconnect",
					SessionKey = _sessionToken,
					Ip = _ip,
					Port = _port
				});
			}
			_ip = null;
			_port = 0;
			_sessionToken = null;
		}
	}

	private async Task UploadThread()
	{
		List<AuthChangeEvent> copy = new List<AuthChangeEvent>();
		while (!Rust.Application.isQuitting)
		{
			await Task.Delay(10000);
			try
			{
				copy.Clear();
				lock (_lock)
				{
					copy.AddRange(pendingEvents);
					pendingEvents.Clear();
				}
				if (copy.Count > 0)
				{
					bool hasConnection = false;
					foreach (AuthChangeEvent item2 in copy)
					{
						if (item2.State == "connecting")
						{
							hasConnection = true;
						}
					}
					if (await PostJsonAsync("https://api.facepunch.com/api/public/auth-tracking/client", copy))
					{
						if (hasConnection)
						{
							lastHeartbeat = DateTime.UtcNow;
						}
					}
					else
					{
						lock (_lock)
						{
							pendingEvents.InsertRange(0, copy);
						}
					}
				}
				else
				{
					if (!(lastHeartbeat.Add(HeartbeatInterval) < DateTime.UtcNow))
					{
						continue;
					}
					lastHeartbeat = DateTime.UtcNow;
					if (isConnected)
					{
						AuthChangeEvent item = new AuthChangeEvent
						{
							Ip = _ip,
							Port = _port,
							SessionKey = _sessionToken,
							State = "heartbeat"
						};
						lock (pendingEvents)
						{
							pendingEvents.Add(item);
						}
					}
					continue;
				}
			}
			catch (Exception ex)
			{
				ExceptionReporter.SendReport(ex.ToString(), ex.StackTrace.ToString());
			}
		}
	}

	private async Task<bool> PostJsonAsync(string url, object body)
	{
		string content2 = JsonConvert.SerializeObject(body);
		using StringContent content = new StringContent(content2, Encoding.UTF8, "application/json");
		return (await _http.PostAsync(url, content)).IsSuccessStatusCode;
	}
}
