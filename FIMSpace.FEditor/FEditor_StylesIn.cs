using UnityEngine;

namespace FIMSpace.FEditor;

public class FEditor_StylesIn
{
	public static GUIStyle GrayBackground => Style(new Color32(128, 128, 127, 76));

	public static GUIStyle LGrayBackground => Style(new Color32(128, 128, 127, 36));

	public static GUIStyle LBlueBackground => Style(new Color32(0, 128, byte.MaxValue, 12));

	public static GUIStyle LNavy => Style(new Color32(167, 228, 243, 44));

	public static GUIStyle Emerald => Style(new Color32(0, 200, 100, 44));

	public static GUIStyle GreenBackground => Style(new Color32(0, 225, 86, 45));

	public static GUIStyle BlueBackground => Style(new Color32(0, 128, byte.MaxValue, 76));

	public static GUIStyle RedBackground => Style(new Color32(225, 72, 72, 45));

	public static GUIStyle YellowBackground => Style(new Color32(225, 244, 11, 45));

	public static GUIStyle Style(Color bgColor)
	{
		GUIStyle gUIStyle = new GUIStyle(GUI.skin.box);
		Color[] pixels = new Color[1] { bgColor };
		Texture2D texture2D = new Texture2D(1, 1);
		texture2D.SetPixels(pixels);
		texture2D.Apply();
		gUIStyle.normal.background = texture2D;
		return gUIStyle;
	}

	public static void DrawUILine(Color color, int thickness = 2, int padding = 10)
	{
	}
}
