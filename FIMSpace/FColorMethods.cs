using System.Globalization;
using UnityEngine;

namespace FIMSpace;

public static class FColorMethods
{
	public static Color ChangeColorAlpha(this Color color, float alpha)
	{
		return new Color(color.r, color.g, color.b, alpha);
	}

	public static Color ToGammaSpace(Color hdrColor)
	{
		float num = hdrColor.r;
		if (hdrColor.g > num)
		{
			num = hdrColor.g;
		}
		if (hdrColor.b > num)
		{
			num = hdrColor.b;
		}
		if (hdrColor.a > num)
		{
			num = hdrColor.a;
		}
		if (num <= 0f)
		{
			return Color.clear;
		}
		return hdrColor / num;
	}

	public static Color ChangeColorsValue(this Color color, float brightenOrDarken = 0f)
	{
		return new Color(color.r + brightenOrDarken, color.g + brightenOrDarken, color.b + brightenOrDarken, color.a);
	}

	public static Color32 HexToColor(this string hex)
	{
		if (string.IsNullOrEmpty(hex))
		{
			Debug.Log("<color=red>Trying convert from hex to color empty string!</color>");
			return Color.white;
		}
		uint result = 255u;
		hex = hex.Replace("#", "");
		hex = hex.Replace("0x", "");
		if (!uint.TryParse(hex, NumberStyles.HexNumber, null, out result))
		{
			Debug.Log("Error during converting hex string.");
			return Color.white;
		}
		return new Color32((byte)((result & -16777216) >> 24), (byte)((result & 0xFF0000) >> 16), (byte)((result & 0xFF00) >> 8), (byte)(result & 0xFFu));
	}

	public static string ColorToHex(this Color32 color, bool addHash = true)
	{
		string text = "";
		if (addHash)
		{
			text = "#";
		}
		return text + string.Format("{0}{1}{2}{3}", (color.r.ToString("X").Length == 1) ? string.Format("0{0}", color.r.ToString("X")) : color.r.ToString("X"), (color.g.ToString("X").Length == 1) ? string.Format("0{0}", color.g.ToString("X")) : color.g.ToString("X"), (color.b.ToString("X").Length == 1) ? string.Format("0{0}", color.b.ToString("X")) : color.b.ToString("X"), (color.a.ToString("X").Length == 1) ? string.Format("0{0}", color.a.ToString("X")) : color.a.ToString("X"));
	}

	public static string ColorToHex(this Color color, bool addHash = true)
	{
		return new Color32((byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f), (byte)(color.a * 255f)).ColorToHex(addHash);
	}

	public static void LerpMaterialColor(this Material mat, string property, Color targetColor, float deltaMultiplier = 8f)
	{
		if (!(mat == null))
		{
			if (!mat.HasProperty(property))
			{
				Debug.LogError("Material " + mat.name + " don't have property '" + property + "'  in shader " + mat.shader.name);
			}
			else
			{
				Color color = mat.GetColor(property);
				mat.SetColor(property, Color.Lerp(color, targetColor, Time.deltaTime * deltaMultiplier));
			}
		}
	}
}
