using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class DragReceiver : MonoBehaviour
{
	[Serializable]
	public class TriggerEvent : UnityEvent<BaseEventData>
	{
	}

	public TriggerEvent onEndDrag;
}
