using System.Collections.Generic;
using UnityEngine;

public class RFManager
{
	private static readonly Dictionary<int, HashSet<IRFObject>> _listeners = new Dictionary<int, HashSet<IRFObject>>();

	private static readonly Dictionary<int, HashSet<IRFObject>> _broadcasters = new Dictionary<int, HashSet<IRFObject>>();

	private static readonly Dictionary<int, bool> _isFrequencyBroadcasting = new Dictionary<int, bool>();

	public static int minFreq = 1;

	public static int maxFreq = 999999;

	private static int reserveRangeMin = 4760;

	private static int reserveRangeMax = 4790;

	public static string reserveString = "Channels " + reserveRangeMin + " to " + reserveRangeMax + " are restricted.";

	public static int ClampFrequency(int freq)
	{
		return Mathf.Clamp(freq, minFreq, maxFreq);
	}

	public static HashSet<IRFObject> GetListenerSet(int frequency)
	{
		frequency = ClampFrequency(frequency);
		if (!_listeners.TryGetValue(frequency, out var value))
		{
			value = new HashSet<IRFObject>();
			_listeners[frequency] = value;
		}
		return value;
	}

	public static HashSet<IRFObject> GetBroadcasterSet(int frequency)
	{
		frequency = ClampFrequency(frequency);
		if (!_broadcasters.TryGetValue(frequency, out var value))
		{
			value = new HashSet<IRFObject>();
			_broadcasters[frequency] = value;
		}
		return value;
	}

	public static void AddListener(int frequency, IRFObject obj)
	{
		frequency = ClampFrequency(frequency);
		if (!GetListenerSet(frequency).Add(obj))
		{
			Debug.Log("adding same listener twice");
			return;
		}
		bool value;
		bool on = _isFrequencyBroadcasting.TryGetValue(frequency, out value) && value;
		obj.RFSignalUpdate(on);
	}

	public static void RemoveListener(int frequency, IRFObject obj)
	{
		frequency = ClampFrequency(frequency);
		if (GetListenerSet(frequency).Remove(obj))
		{
			obj.RFSignalUpdate(on: false);
		}
	}

	public static void AddBroadcaster(int frequency, IRFObject obj)
	{
		frequency = ClampFrequency(frequency);
		HashSet<IRFObject> broadcasterSet = GetBroadcasterSet(frequency);
		if (broadcasterSet.RemoveWhere((IRFObject b) => b == null || !b.IsValidEntityReference()) > 0)
		{
			Debug.LogWarning($"Found null entries in the RF broadcaster set for frequency {frequency}... cleaning up.");
		}
		if (broadcasterSet.Add(obj) && (!_isFrequencyBroadcasting.TryGetValue(frequency, out var value) || !value))
		{
			_isFrequencyBroadcasting[frequency] = true;
			UpdateListenersForFrequency(frequency, isBroadcasting: true);
		}
	}

	public static void RemoveBroadcaster(int frequency, IRFObject obj)
	{
		frequency = ClampFrequency(frequency);
		HashSet<IRFObject> broadcasterSet = GetBroadcasterSet(frequency);
		if (broadcasterSet.RemoveWhere((IRFObject b) => b == null || !b.IsValidEntityReference()) > 0)
		{
			Debug.LogWarning($"Found null entries in the RF broadcaster set for frequency {frequency}... cleaning up.");
		}
		if (broadcasterSet.Remove(obj) && broadcasterSet.Count == 0)
		{
			_isFrequencyBroadcasting[frequency] = false;
			UpdateListenersForFrequency(frequency, isBroadcasting: false);
		}
	}

	private static void UpdateListenersForFrequency(int frequency, bool isBroadcasting)
	{
		HashSet<IRFObject> listenerSet = GetListenerSet(frequency);
		listenerSet.RemoveWhere((IRFObject l) => l == null || !l.IsValidEntityReference());
		foreach (IRFObject item in listenerSet)
		{
			item.RFSignalUpdate(isBroadcasting);
		}
	}

	public static bool IsReserved(int frequency)
	{
		if (frequency >= reserveRangeMin && frequency <= reserveRangeMax)
		{
			return true;
		}
		return false;
	}

	public static void ReserveErrorPrint(BasePlayer player)
	{
		player.ChatMessage(reserveString);
	}

	public static void ChangeFrequency(int oldFrequency, int newFrequency, IRFObject obj, bool isListener, bool isOn = true)
	{
		newFrequency = ClampFrequency(newFrequency);
		if (isListener)
		{
			RemoveListener(oldFrequency, obj);
			if (isOn)
			{
				AddListener(newFrequency, obj);
			}
		}
		else
		{
			RemoveBroadcaster(oldFrequency, obj);
			if (isOn)
			{
				AddBroadcaster(newFrequency, obj);
			}
		}
	}
}
