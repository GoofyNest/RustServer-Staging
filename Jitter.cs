using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public class Jitter
{
	private readonly Vector2[] haltonSequence = new Vector2[16]
	{
		new Vector2(0.5f, 0.333333f),
		new Vector2(0.25f, 0.666667f),
		new Vector2(0.75f, 0.111111f),
		new Vector2(0.125f, 0.444444f),
		new Vector2(0.625f, 0.777778f),
		new Vector2(0.375f, 0.222222f),
		new Vector2(0.875f, 0.555556f),
		new Vector2(0.0625f, 0.888889f),
		new Vector2(0.5625f, 0.037037f),
		new Vector2(0.3125f, 0.37037f),
		new Vector2(0.8125f, 0.703704f),
		new Vector2(0.1875f, 0.148148f),
		new Vector2(0.6875f, 0.481481f),
		new Vector2(0.4375f, 0.814815f),
		new Vector2(0.9375f, 0.259259f),
		new Vector2(1f / 32f, 0.592593f)
	};

	public int SampleIndex { get; private set; }

	public int SampleCount { get; private set; } = 8;


	public Vector2 Offset { get; private set; } = Vector2.zero;


	public Vector2 TexelOffset { get; private set; } = Vector2.zero;


	public Jitter()
	{
		SampleCount = haltonSequence.Length;
	}

	private Matrix4x4 GetJitteredProjectionMatrix(Camera camera)
	{
		Offset = haltonSequence[++SampleIndex % 8] - new Vector2(0.5f, 0.5f);
		TexelOffset = new Vector2(Offset.x / (float)camera.pixelWidth, Offset.y / (float)camera.pixelHeight);
		return RuntimeUtilities.GetJitteredPerspectiveProjectionMatrix(camera, Offset);
	}

	public void ConfigureCameraJitter(PostProcessRenderContext context)
	{
		Camera camera = context.camera;
		camera.ResetProjectionMatrix();
		camera.nonJitteredProjectionMatrix = camera.projectionMatrix;
		camera.projectionMatrix = GetJitteredProjectionMatrix(camera);
		camera.useJitteredProjectionMatrixForTransparentRendering = true;
	}
}
