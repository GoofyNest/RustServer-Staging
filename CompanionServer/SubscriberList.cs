using System;
using System.Collections.Generic;
using System.Diagnostics;
using Facepunch;
using Network;

namespace CompanionServer;

public class SubscriberList<TKey, TMessage> where TKey : IEquatable<TKey>
{
	private readonly object _syncRoot;

	private readonly Dictionary<TKey, Dictionary<Connection, double>> _subscriptions;

	private readonly IBroadcastSender<TMessage> _sender;

	private readonly double? _timeoutSeconds;

	private readonly Stopwatch _lastCleanup;

	public SubscriberList(IBroadcastSender<TMessage> sender, double? timeoutSeconds = null)
	{
		_syncRoot = new object();
		_subscriptions = new Dictionary<TKey, Dictionary<Connection, double>>();
		_sender = sender;
		_timeoutSeconds = timeoutSeconds;
		_lastCleanup = Stopwatch.StartNew();
	}

	public void Add(TKey key, Connection value)
	{
		lock (_syncRoot)
		{
			if (!_subscriptions.TryGetValue(key, out var value2))
			{
				value2 = new Dictionary<Connection, double>();
				_subscriptions.Add(key, value2);
			}
			value2[value] = TimeEx.realtimeSinceStartup;
		}
		CleanupExpired();
	}

	public void Remove(TKey key, Connection value)
	{
		lock (_syncRoot)
		{
			if (_subscriptions.TryGetValue(key, out var value2))
			{
				value2.Remove(value);
				if (value2.Count == 0)
				{
					_subscriptions.Remove(key);
				}
			}
		}
		CleanupExpired();
	}

	public void Clear(TKey key)
	{
		lock (_syncRoot)
		{
			if (_subscriptions.TryGetValue(key, out var value))
			{
				value.Clear();
			}
		}
	}

	public void Send(TKey key, TMessage message)
	{
		double realtimeSinceStartup = TimeEx.realtimeSinceStartup;
		List<Connection> obj;
		lock (_syncRoot)
		{
			if (!_subscriptions.TryGetValue(key, out var value))
			{
				return;
			}
			obj = Pool.Get<List<Connection>>();
			foreach (KeyValuePair<Connection, double> item in value)
			{
				if (!_timeoutSeconds.HasValue || realtimeSinceStartup - item.Value < _timeoutSeconds.Value)
				{
					obj.Add(item.Key);
				}
			}
		}
		_sender.BroadcastTo(obj, message);
		Pool.FreeUnmanaged(ref obj);
	}

	public bool HasAnySubscribers(TKey key)
	{
		double realtimeSinceStartup = TimeEx.realtimeSinceStartup;
		lock (_syncRoot)
		{
			if (!_subscriptions.TryGetValue(key, out var value))
			{
				return false;
			}
			foreach (KeyValuePair<Connection, double> item in value)
			{
				if (!_timeoutSeconds.HasValue || realtimeSinceStartup - item.Value < _timeoutSeconds.Value)
				{
					return true;
				}
			}
		}
		return false;
	}

	public bool HasSubscriber(TKey key, Connection target)
	{
		double realtimeSinceStartup = TimeEx.realtimeSinceStartup;
		lock (_syncRoot)
		{
			if (!_subscriptions.TryGetValue(key, out var value) || !value.TryGetValue(target, out var value2))
			{
				return false;
			}
			if (!_timeoutSeconds.HasValue || realtimeSinceStartup - value2 < _timeoutSeconds.Value)
			{
				return true;
			}
		}
		return false;
	}

	private void CleanupExpired()
	{
		if (!_timeoutSeconds.HasValue || _lastCleanup.Elapsed.TotalMinutes < 2.0)
		{
			return;
		}
		_lastCleanup.Restart();
		double realtimeSinceStartup = TimeEx.realtimeSinceStartup;
		List<(TKey, Connection)> obj = Pool.Get<List<(TKey, Connection)>>();
		lock (_syncRoot)
		{
			foreach (KeyValuePair<TKey, Dictionary<Connection, double>> subscription in _subscriptions)
			{
				foreach (KeyValuePair<Connection, double> item in subscription.Value)
				{
					if (realtimeSinceStartup - item.Value >= _timeoutSeconds.Value)
					{
						obj.Add((subscription.Key, item.Key));
					}
				}
			}
			foreach (var (key, value) in obj)
			{
				Remove(key, value);
			}
		}
		Pool.FreeUnmanaged(ref obj);
	}
}
