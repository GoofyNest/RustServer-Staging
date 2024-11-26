using UnityEngine;

namespace FIMSpace.Basics;

public class FBasic_MaterialTiler : FBasic_MaterialScriptBase
{
	[Header("When you scale object change")]
	[Header("something in script to apply")]
	[Space(10f)]
	[Tooltip("Texture identificator in shader")]
	public string TextureProperty = "_MainTex";

	[Tooltip("How much tiles should be multiplied according to gameObject's scale")]
	public Vector2 ScaleValues = new Vector2(1f, 1f);

	[Tooltip("When scale on Y should be same as X")]
	public bool EqualDimensions;

	private void OnValidate()
	{
		GetRendererMaterial();
		if (EqualDimensions)
		{
			ScaleValues.y = ScaleValues.x;
		}
		TileMaterialToScale();
	}

	private void TileMaterialToScale()
	{
		if (!(RendererMaterial == null) && !(ObjectRenderer == null))
		{
			Vector2 scaleValues = ScaleValues;
			scaleValues.x *= base.transform.localScale.x;
			scaleValues.y *= base.transform.localScale.z;
			RendererMaterial.SetTextureScale("_MainTex", scaleValues);
			ObjectRenderer.material = RendererMaterial;
		}
	}
}
