using UnityEngine.EventSystems;
using UnityEngine.Events;

public class RectChangeEvent : UIBehaviour
{
	public UnityEvent action;

	protected override void OnRectTransformDimensionsChange()
	{
		action.Invoke();
	}
}
