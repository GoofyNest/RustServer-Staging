using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

public class RadialBlurRenderer : PostProcessEffectRenderer<RadialBlur>
{
	private Shader shader;

	private int rt1ID = Shader.PropertyToID("_BlurRT1");

	private int rt2ID = Shader.PropertyToID("_BlurRT2");

	private int paramsID = Shader.PropertyToID("_Params");

	public override void Init()
	{
		base.Init();
		shader = Shader.Find("Hidden/PostProcessing/RadialBlur");
	}

	public override void Render(PostProcessRenderContext context)
	{
		CommandBuffer command = context.command;
		command.BeginSample("RadialBlur");
		if (Mathf.Approximately(base.settings.start, 1f) && Mathf.Approximately(base.settings.amount, 0f))
		{
			command.BlitFullscreenTriangle(context.source, context.destination);
		}
		else
		{
			PropertySheet propertySheet = context.propertySheets.Get(shader);
			propertySheet.properties.SetVector(paramsID, new Vector4(base.settings.center.value.x, base.settings.center.value.y, base.settings.start, base.settings.amount));
			int width = context.width >> (int)base.settings.downsample;
			int height = context.height >> (int)base.settings.downsample;
			int num = (int)base.settings.iterations / 2;
			int num2 = (int)base.settings.iterations % 2;
			command.GetTemporaryRT(rt1ID, width, height, 0, FilterMode.Bilinear, context.sourceFormat, RenderTextureReadWrite.Default);
			command.GetTemporaryRT(rt2ID, width, height, 0, FilterMode.Bilinear, context.sourceFormat, RenderTextureReadWrite.Default);
			command.BlitFullscreenTriangle(context.source, rt1ID, propertySheet, 0);
			if ((int)base.settings.iterations > 1)
			{
				for (int i = 0; i < num; i++)
				{
					command.BlitFullscreenTriangle(rt1ID, rt2ID, propertySheet, 1);
					if (i == num - 1 && num2 == 0)
					{
						command.BlitFullscreenTriangle(rt2ID, context.destination, propertySheet, 1);
					}
					else
					{
						command.BlitFullscreenTriangle(rt2ID, rt1ID, propertySheet, 1);
					}
				}
			}
			if (num2 > 0)
			{
				command.BlitFullscreenTriangle(rt1ID, context.destination, propertySheet, 1);
			}
			command.ReleaseTemporaryRT(rt1ID);
			command.ReleaseTemporaryRT(rt2ID);
		}
		command.EndSample("RadialBlur");
	}
}
