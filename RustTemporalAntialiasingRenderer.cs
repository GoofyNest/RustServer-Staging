using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

public sealed class RustTemporalAntialiasingRenderer : PostProcessEffectRenderer<RustTemporalAntialiasing>
{
	private const string BUFFER_NAME = "RustTemporalAntiAliasing";

	private static readonly int historyTextureId = Shader.PropertyToID("_HistoryTex");

	private static readonly int jitterTexelOffsetId = Shader.PropertyToID("_JitterTexelOffset");

	public readonly Jitter JitterSettings = new Jitter();

	private RenderTexture[] historyTextures = new RenderTexture[2];

	private readonly RenderTargetIdentifier[] multipleRenderTargets = new RenderTargetIdentifier[2];

	private int pingPongValue;

	private Shader postProcessShader;

	public static RustTemporalAntialiasingRenderer Instance { get; private set; }

	public override void Init()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		postProcessShader = Shader.Find("Hidden/PostProcessing/RustTemporalAntialiasing");
		if (postProcessShader == null)
		{
			Debug.LogError("Failed to initialize RustTemporalAntialiasing as the shader couldn't be found!");
		}
	}

	private bool IsValid()
	{
		return postProcessShader != null;
	}

	private RenderTexture ConvertTextureToMatchCamera(RenderTexture texture, PostProcessRenderContext context)
	{
		if (texture == null || texture.width != context.width || texture.height != context.height)
		{
			texture?.Release();
			texture = new RenderTexture(context.width, context.height, 0, context.sourceFormat);
		}
		return texture;
	}

	private void RecreateRenderTexturesIfNeeded(PostProcessRenderContext context)
	{
		for (int i = 0; i < historyTextures.Length; i++)
		{
			historyTextures[i] = ConvertTextureToMatchCamera(historyTextures[i], context);
		}
	}

	public override void Render(PostProcessRenderContext context)
	{
		if (!IsValid())
		{
			context.command.BlitFullscreenTriangle(context.source, context.destination);
			return;
		}
		context.camera.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
		JitterSettings.ConfigureCameraJitter(context);
		RecreateRenderTexturesIfNeeded(context);
		PropertySheet propertySheet = context.propertySheets.Get(postProcessShader);
		CommandBuffer command = context.command;
		RenderTexture renderTexture = historyTextures[pingPongValue++ % 2];
		RenderTexture renderTexture2 = historyTextures[pingPongValue++ % 2];
		pingPongValue++;
		multipleRenderTargets[0] = context.destination;
		multipleRenderTargets[1] = renderTexture;
		command.BeginSample("RustTemporalAntiAliasing");
		command.SetGlobalVector(jitterTexelOffsetId, JitterSettings.TexelOffset);
		command.SetGlobalTexture(historyTextureId, renderTexture2);
		command.BlitFullscreenTriangle(context.source, multipleRenderTargets, BuiltinRenderTextureType.None, propertySheet, 0);
		command.EndSample("RustTemporalAntiAliasing");
	}

	public override void Release()
	{
		for (int i = 0; i < historyTextures.Length; i++)
		{
			historyTextures[i]?.Release();
			historyTextures[i] = null;
		}
	}
}
