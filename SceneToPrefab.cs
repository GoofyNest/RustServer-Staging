using System;
using System.Collections.Generic;
using UnityEngine;

public class SceneToPrefab : MonoBehaviour, IEditorComponent
{
	private struct MonumentName
	{
		public bool IsMonument;

		public string RemappedPath;
	}

	public enum PrefabMode
	{
		LegacyPrefab,
		RedirectPrefab
	}

	public bool flattenHierarchy;

	public GameObject outputPrefab;

	[Tooltip("If true the HLOD generation will be skipped and the previous results will be used, good to use if non-visual changes were made (eg.triggers)")]
	public bool skipAllHlod;

	[ClientVar]
	[ServerVar]
	public static bool monument_scenes_enabled;

	private static Dictionary<string, string> autospawnToSceneSpawner;

	public const string Label_Scene = "PrefabScene";

	public const string Label_ClientScene = "PrefabScene_Client";

	public const string Label_ServerScene = "PrefabScene_Server";

	public const string Label_GenericScene = "PrefabScene_Generic";

	public static bool TryRemapAutospawnToSceneSpawner(string autospawnPath, out string spawnerPath)
	{
		if (autospawnToSceneSpawner == null)
		{
			autospawnToSceneSpawner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			string[] array = FileSystem.Backend.FindAll("Assets/bundled/Prefabs/remapped");
			foreach (string text in array)
			{
				string key = ConvertRemappedToAutospawnPath(text);
				autospawnToSceneSpawner[key] = text;
			}
		}
		return autospawnToSceneSpawner.TryGetValue(autospawnPath, out spawnerPath);
	}

	private static string ConvertAutospawnToRemappedPath(string prefabPath)
	{
		return prefabPath.Replace("/autospawn/", "/remapped/").Replace(".prefab", "_scene.prefab");
	}

	private static string ConvertRemappedToAutospawnPath(string remappedPath)
	{
		return remappedPath.Replace("/remapped/", "/autospawn/").Replace("_scene.prefab", ".prefab");
	}
}
