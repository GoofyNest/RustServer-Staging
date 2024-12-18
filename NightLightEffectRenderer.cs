using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

public class NightLightEffectRenderer : PostProcessEffectRenderer<NightLightEffect>
{
	private int distanceProperty = Shader.PropertyToID("_distance");

	private int fadeFractionProperty = Shader.PropertyToID("_fadefraction");

	private int brightnessProperty = Shader.PropertyToID("_brightness");

	private Shader nightlightShader;

	public override void Init()
	{
		base.Init();
		nightlightShader = Shader.Find("Hidden/PostProcessing/NightLightShader");
	}

	public override void Render(PostProcessRenderContext context)
	{
		CommandBuffer command = context.command;
		command.BeginSample("NightLight");
		PropertySheet propertySheet = context.propertySheets.Get(nightlightShader);
		propertySheet.properties.Clear();
		propertySheet.properties.SetFloat(distanceProperty, base.settings.distance.value);
		propertySheet.properties.SetFloat(fadeFractionProperty, base.settings.fadeFraction.value);
		propertySheet.properties.SetFloat(brightnessProperty, base.settings.brightness.value);
		command.BlitFullscreenTriangle(context.source, context.destination, propertySheet, 0);
		command.EndSample("NightLight");
	}
}
