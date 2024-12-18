using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using CompanionServer.Handlers;
using ConVar;
using Facepunch;
using Fleck;
using ProtoBuf;
using SilentOrbit.ProtocolBuffers;
using UnityEngine;

namespace CompanionServer;

public class Listener : IDisposable, IBroadcastSender<AppBroadcast>
{
	private struct Message
	{
		public readonly Connection Connection;

		public readonly MemoryBuffer Buffer;

		public Message(Connection connection, MemoryBuffer buffer)
		{
			Connection = connection;
			Buffer = buffer;
		}
	}

	private static readonly ByteArrayStream Stream = new ByteArrayStream();

	private readonly TokenBucketList<IPAddress> _ipTokenBuckets;

	private readonly BanList<IPAddress> _ipBans;

	private readonly TokenBucketList<ulong> _playerTokenBuckets;

	private readonly TokenBucketList<ulong> _pairingTokenBuckets;

	private readonly Queue<Message> _messageQueue;

	private readonly WebSocketServer _server;

	private readonly Stopwatch _stopwatch;

	private RealTimeSince _lastCleanup;

	private long _nextConnectionId;

	public readonly IPAddress Address;

	public readonly int Port;

	public readonly ConnectionLimiter Limiter;

	public readonly SubscriberList<PlayerTarget, AppBroadcast> PlayerSubscribers;

	public readonly SubscriberList<EntityTarget, AppBroadcast> EntitySubscribers;

	public readonly SubscriberList<ClanTarget, AppBroadcast> ClanSubscribers;

	public readonly SubscriberList<CameraTarget, AppBroadcast> CameraSubscribers;

	public Listener(IPAddress ipAddress, int port)
	{
		Listener listener = this;
		Address = ipAddress;
		Port = port;
		Limiter = new ConnectionLimiter();
		_ipTokenBuckets = new TokenBucketList<IPAddress>(50.0, 15.0);
		_ipBans = new BanList<IPAddress>();
		_playerTokenBuckets = new TokenBucketList<ulong>(25.0, 3.0);
		_pairingTokenBuckets = new TokenBucketList<ulong>(5.0, 0.1);
		_messageQueue = new Queue<Message>();
		SynchronizationContext syncContext = SynchronizationContext.Current;
		_server = new WebSocketServer($"ws://{Address}:{Port}/");
		_server.Start(delegate(IWebSocketConnection socket)
		{
			IPAddress address = socket.ConnectionInfo.ClientIpAddress;
			if (!listener.Limiter.TryAdd(address) || listener._ipBans.IsBanned(address))
			{
				socket.Close();
			}
			else
			{
				long connectionId = Interlocked.Increment(ref listener._nextConnectionId);
				Connection conn = new Connection(connectionId, listener, socket);
				socket.OnClose = delegate
				{
					listener.Limiter.Remove(address);
					syncContext.Post(delegate(object c)
					{
						((Connection)c).OnClose();
					}, conn);
				};
				socket.OnBinary = conn.OnMessage;
				socket.OnError = UnityEngine.Debug.LogError;
			}
		});
		_stopwatch = new Stopwatch();
		PlayerSubscribers = new SubscriberList<PlayerTarget, AppBroadcast>(this);
		EntitySubscribers = new SubscriberList<EntityTarget, AppBroadcast>(this);
		ClanSubscribers = new SubscriberList<ClanTarget, AppBroadcast>(this);
		CameraSubscribers = new SubscriberList<CameraTarget, AppBroadcast>(this, 30.0);
	}

	public void Dispose()
	{
		_server?.Dispose();
	}

	internal void Enqueue(Connection connection, MemoryBuffer data)
	{
		lock (_messageQueue)
		{
			if (!App.update || _messageQueue.Count >= App.queuelimit)
			{
				data.Dispose();
				return;
			}
			Message item = new Message(connection, data);
			_messageQueue.Enqueue(item);
		}
	}

	public bool Update()
	{
		if (!App.update)
		{
			return false;
		}
		bool result = false;
		using (TimeWarning.New("CompanionServer.MessageQueue"))
		{
			lock (_messageQueue)
			{
				_stopwatch.Restart();
				while (_messageQueue.Count > 0 && _stopwatch.Elapsed.TotalMilliseconds < 5.0)
				{
					Message message = _messageQueue.Dequeue();
					Dispatch(message);
					result = true;
				}
			}
		}
		if ((float)_lastCleanup >= 3f)
		{
			_lastCleanup = 0f;
			_ipTokenBuckets.Cleanup();
			_ipBans.Cleanup();
			_playerTokenBuckets.Cleanup();
			_pairingTokenBuckets.Cleanup();
		}
		return result;
	}

	private void Dispatch(Message message)
	{
		MemoryBuffer buffer = message.Buffer;
		AppRequest appRequest;
		try
		{
			Stream.SetData(message.Buffer.Data, 0, message.Buffer.Length);
			appRequest = AppRequest.Deserialize(Stream);
		}
		catch
		{
			DebugEx.LogWarning($"Malformed companion packet from {message.Connection.Address}");
			message.Connection.Close();
			throw;
		}
		finally
		{
			buffer.Dispose();
		}
		if (!Handle<AppEmpty, Info>(appRequest.getInfo, message.Connection, appRequest) && !Handle<AppEmpty, CompanionServer.Handlers.Time>(appRequest.getTime, message.Connection, appRequest) && !Handle<AppEmpty, Map>(appRequest.getMap, message.Connection, appRequest) && !Handle<AppEmpty, TeamInfo>(appRequest.getTeamInfo, message.Connection, appRequest) && !Handle<AppEmpty, TeamChat>(appRequest.getTeamChat, message.Connection, appRequest) && !Handle<AppSendMessage, SendTeamChat>(appRequest.sendTeamMessage, message.Connection, appRequest) && !Handle<AppEmpty, EntityInfo>(appRequest.getEntityInfo, message.Connection, appRequest) && !Handle<AppSetEntityValue, SetEntityValue>(appRequest.setEntityValue, message.Connection, appRequest) && !Handle<AppEmpty, CheckSubscription>(appRequest.checkSubscription, message.Connection, appRequest) && !Handle<AppFlag, SetSubscription>(appRequest.setSubscription, message.Connection, appRequest) && !Handle<AppEmpty, MapMarkers>(appRequest.getMapMarkers, message.Connection, appRequest) && !Handle<AppPromoteToLeader, PromoteToLeader>(appRequest.promoteToLeader, message.Connection, appRequest) && !Handle<AppEmpty, CompanionServer.Handlers.ClanInfo>(appRequest.getClanInfo, message.Connection, appRequest) && !Handle<AppEmpty, ClanChat>(appRequest.getClanChat, message.Connection, appRequest) && !Handle<AppSendMessage, SetClanMotd>(appRequest.setClanMotd, message.Connection, appRequest) && !Handle<AppSendMessage, SendClanChat>(appRequest.sendClanMessage, message.Connection, appRequest) && !Handle<AppGetNexusAuth, NexusAuth>(appRequest.getNexusAuth, message.Connection, appRequest) && !Handle<AppCameraSubscribe, CameraSubscribe>(appRequest.cameraSubscribe, message.Connection, appRequest) && !Handle<AppEmpty, CameraUnsubscribe>(appRequest.cameraUnsubscribe, message.Connection, appRequest) && !Handle<AppCameraInput, CameraInput>(appRequest.cameraInput, message.Connection, appRequest))
		{
			AppResponse appResponse = Facepunch.Pool.Get<AppResponse>();
			appResponse.seq = appRequest.seq;
			appResponse.error = Facepunch.Pool.Get<AppError>();
			appResponse.error.error = "unhandled";
			message.Connection.Send(appResponse);
			appRequest.Dispose();
		}
	}

	private bool Handle<TProto, THandler>(TProto protocol, Connection connection, AppRequest request) where TProto : class, IProto where THandler : BaseHandler<TProto>, new()
	{
		if (protocol == null)
		{
			return false;
		}
		THandler obj = Facepunch.Pool.Get<THandler>();
		obj.Initialize(_playerTokenBuckets, connection, request, protocol);
		try
		{
			ValidationResult validationResult = obj.Validate();
			switch (validationResult)
			{
			case ValidationResult.Rejected:
				connection.Close();
				break;
			default:
				obj.SendError(validationResult.ToErrorCode());
				break;
			case ValidationResult.Success:
				obj.Execute();
				break;
			}
		}
		catch (Exception arg)
		{
			UnityEngine.Debug.LogError($"AppRequest threw an exception: {arg}");
			obj.SendError("server_error");
		}
		Facepunch.Pool.Free(ref obj);
		return true;
	}

	public void BroadcastTo(List<Connection> targets, AppBroadcast broadcast)
	{
		MemoryBuffer broadcastBuffer = GetBroadcastBuffer(broadcast);
		foreach (Connection target in targets)
		{
			target.SendRaw(broadcastBuffer.DontDispose());
		}
		broadcastBuffer.Dispose();
	}

	private static MemoryBuffer GetBroadcastBuffer(AppBroadcast broadcast)
	{
		MemoryBuffer memoryBuffer = new MemoryBuffer(65536);
		Stream.SetData(memoryBuffer.Data, 0, memoryBuffer.Length);
		AppMessage appMessage = Facepunch.Pool.Get<AppMessage>();
		appMessage.broadcast = broadcast;
		appMessage.ToProto(Stream);
		if (appMessage.ShouldPool)
		{
			appMessage.Dispose();
		}
		return memoryBuffer.Slice((int)Stream.Position);
	}

	public bool CanSendPairingNotification(ulong playerId)
	{
		return _pairingTokenBuckets.Get(playerId).TryTake(1.0);
	}
}
