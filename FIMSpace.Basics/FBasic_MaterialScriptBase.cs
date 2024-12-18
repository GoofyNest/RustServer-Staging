using UnityEngine;

namespace FIMSpace.Basics;

public abstract class FBasic_MaterialScriptBase : MonoBehaviour
{
	protected Material RendererMaterial;

	protected Renderer ObjectRenderer;

	protected Material GetRendererMaterial()
	{
		if (!Application.isPlaying && ObjectRenderer != null && ObjectRenderer.sharedMaterial != RendererMaterial)
		{
			RendererMaterial = null;
		}
		if (RendererMaterial == null || ObjectRenderer == null)
		{
			Renderer renderer = base.gameObject.GetComponent<Renderer>();
			if (renderer == null)
			{
				renderer = base.gameObject.GetComponentInChildren<Renderer>();
			}
			if (renderer == null)
			{
				Debug.Log("<color=red>No renderer in " + base.gameObject.name + "!</color>");
				return null;
			}
			ObjectRenderer = renderer;
			if (Application.isPlaying)
			{
				RendererMaterial = renderer.material;
			}
			else
			{
				RendererMaterial = new Material(renderer.sharedMaterial);
			}
		}
		return RendererMaterial;
	}
}
