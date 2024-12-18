using System.Diagnostics;
using UnityEngine;

namespace FIMSpace;

public static class FDebug
{
	private static readonly Stopwatch _debugWatch = new Stopwatch();

	public static long _LastMeasureMilliseconds = 0L;

	public static long _LastMeasureTicks = 0L;

	public static void Log(string log)
	{
		UnityEngine.Debug.Log("LOG: " + log);
	}

	public static void Log(string log, string category)
	{
		UnityEngine.Debug.Log(MarkerColor("#1A6600") + "[" + category + "]" + EndColorMarker() + " " + log);
	}

	public static void LogRed(string log)
	{
		UnityEngine.Debug.Log(MarkerColor("red") + log + EndColorMarker());
	}

	public static void LogOrange(string log)
	{
		UnityEngine.Debug.Log(MarkerColor("#D1681D") + log + EndColorMarker());
	}

	public static void LogYellow(string log)
	{
		UnityEngine.Debug.Log(MarkerColor("#E0D300") + log + EndColorMarker());
	}

	public static void StartMeasure()
	{
		_debugWatch.Reset();
		_debugWatch.Start();
	}

	public static void PauseMeasure()
	{
		_debugWatch.Stop();
	}

	public static void ResumeMeasure()
	{
		_debugWatch.Start();
	}

	public static void EndMeasureAndLog(string v)
	{
		_debugWatch.Stop();
		_LastMeasureMilliseconds = _debugWatch.ElapsedMilliseconds;
		_LastMeasureTicks = _debugWatch.ElapsedTicks;
		UnityEngine.Debug.Log("Measure " + v + ": " + _debugWatch.ElapsedTicks + " ticks   " + _debugWatch.ElapsedMilliseconds + "ms");
	}

	public static long EndMeasureAndGetTicks()
	{
		_debugWatch.Stop();
		_LastMeasureMilliseconds = _debugWatch.ElapsedMilliseconds;
		_LastMeasureTicks = _debugWatch.ElapsedTicks;
		return _debugWatch.ElapsedTicks;
	}

	public static string MarkerColor(string color)
	{
		return "<color='" + color + "'>";
	}

	public static string EndColorMarker()
	{
		return "</color>";
	}

	public static void DrawBounds2D(this Bounds b, Color c, float y = 0f, float scale = 1f, float duration = 1.1f)
	{
		Vector3 vector = new Vector3(b.max.x, y, b.max.z) * scale;
		Vector3 vector2 = new Vector3(b.max.x, y, b.min.z) * scale;
		Vector3 vector3 = new Vector3(b.min.x, y, b.min.z) * scale;
		Vector3 vector4 = new Vector3(b.min.x, y, b.max.z) * scale;
		UnityEngine.Debug.DrawLine(vector, vector2, c, duration);
		UnityEngine.Debug.DrawLine(vector2, vector3, c, duration);
		UnityEngine.Debug.DrawLine(vector2, vector3, c, duration);
		UnityEngine.Debug.DrawLine(vector3, vector4, c, duration);
		UnityEngine.Debug.DrawLine(vector4, vector, c, duration);
	}

	public static void DrawBounds3D(this Bounds b, Color c, float scale = 1f, float time = 1.01f)
	{
		Vector3 vector = new Vector3(b.max.x, b.min.y, b.max.z) * scale;
		Vector3 vector2 = new Vector3(b.max.x, b.min.y, b.min.z) * scale;
		Vector3 vector3 = new Vector3(b.min.x, b.min.y, b.min.z) * scale;
		Vector3 vector4 = new Vector3(b.min.x, b.min.y, b.max.z) * scale;
		UnityEngine.Debug.DrawLine(vector, vector2, c, time);
		UnityEngine.Debug.DrawLine(vector2, vector3, c, time);
		UnityEngine.Debug.DrawLine(vector2, vector3, c, time);
		UnityEngine.Debug.DrawLine(vector3, vector4, c, time);
		UnityEngine.Debug.DrawLine(vector4, vector, c, time);
		Vector3 vector5 = new Vector3(b.max.x, b.max.y, b.max.z) * scale;
		Vector3 vector6 = new Vector3(b.max.x, b.max.y, b.min.z) * scale;
		Vector3 vector7 = new Vector3(b.min.x, b.max.y, b.min.z) * scale;
		Vector3 vector8 = new Vector3(b.min.x, b.max.y, b.max.z) * scale;
		UnityEngine.Debug.DrawLine(vector5, vector6, c, time);
		UnityEngine.Debug.DrawLine(vector6, vector7, c, time);
		UnityEngine.Debug.DrawLine(vector6, vector7, c, time);
		UnityEngine.Debug.DrawLine(vector7, vector8, c, time);
		UnityEngine.Debug.DrawLine(vector8, vector5, c, time);
		UnityEngine.Debug.DrawLine(vector, vector, c, time);
		UnityEngine.Debug.DrawLine(vector6, vector2, c, time);
		UnityEngine.Debug.DrawLine(vector3, vector7, c, time);
		UnityEngine.Debug.DrawLine(vector4, vector8, c, time);
	}
}
