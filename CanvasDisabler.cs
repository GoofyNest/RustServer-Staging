using UnityEngine;

public class CanvasDisabler : MonoBehaviour
{
	public Canvas canvas;

	public CanvasGroup canvasGroup;

	private float lastAlpha = -1f;

	private void Awake()
	{
		if (canvas == null)
		{
			canvas = GetComponent<Canvas>();
		}
		if (canvasGroup == null)
		{
			canvasGroup = GetComponent<CanvasGroup>();
		}
	}

	private void Update()
	{
		float alpha = canvasGroup.alpha;
		if (alpha != lastAlpha)
		{
			lastAlpha = alpha;
			bool flag = alpha > 0f;
			canvas.enabled = flag;
		}
	}
}
