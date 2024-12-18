using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class TextureColorPicker : MonoBehaviour, IPointerDownHandler, IEventSystemHandler, IDragHandler
{
	[Serializable]
	public class onColorSelectedEvent : UnityEvent<Color>
	{
	}

	public Texture2D texture;

	public onColorSelectedEvent onColorSelected = new onColorSelectedEvent();

	public virtual void OnPointerDown(PointerEventData eventData)
	{
		OnDrag(eventData);
	}

	public virtual void OnDrag(PointerEventData eventData)
	{
		RectTransform rectTransform = base.transform as RectTransform;
		Vector2 localPoint = default(Vector2);
		if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out localPoint))
		{
			localPoint.x += rectTransform.rect.width * rectTransform.pivot.x;
			localPoint.y += rectTransform.rect.height * rectTransform.pivot.y;
			localPoint.x /= rectTransform.rect.width;
			localPoint.y /= rectTransform.rect.height;
			Color pixel = texture.GetPixel((int)(localPoint.x * (float)texture.width), (int)(localPoint.y * (float)texture.height));
			onColorSelected.Invoke(pixel);
		}
	}
}
