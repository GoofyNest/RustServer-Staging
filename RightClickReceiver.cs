using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class RightClickReceiver : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
{
	public UnityEvent ClickReceiver;

	public void OnPointerClick(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Right)
		{
			ClickReceiver?.Invoke();
		}
	}
}
