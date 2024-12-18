using System.Collections;
using System.IO;
using ConVar;
using Network;
using Rust;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSetup : MonoBehaviour
{
	public static bool RunOnce;

	public bool startServer = true;

	public string clientConnectCommand = "client.connect 127.0.0.1:28015";

	public bool loadMenu = true;

	public bool loadLevel;

	public string loadLevelScene = "";

	public bool loadSave;

	public string loadSaveFile = "";

	public string initializationFile = "";

	public string initializationCommands = "";

	public bool normalRendering;

	protected void Awake()
	{
		if (RunOnce)
		{
			GameManager.Destroy(base.gameObject);
			return;
		}
		Render.use_normal_rendering = normalRendering;
		GameManifest.Load();
		GameManifest.LoadAssets();
		RunOnce = true;
		if (Bootstrap.needsSetup)
		{
			Bootstrap.Init_Tier0();
			if (!string.IsNullOrEmpty(initializationFile))
			{
				if (!File.Exists(initializationFile))
				{
					Debug.Log("Unable to load " + initializationFile + ", does not exist");
				}
				else
				{
					Debug.Log("Loading initialization file: " + initializationFile);
					ConsoleSystem.RunFile(ConsoleSystem.Option.Server, File.ReadAllText(initializationFile));
				}
			}
			if (!string.IsNullOrEmpty(initializationCommands))
			{
				string[] array = initializationCommands.Split(';');
				foreach (string text in array)
				{
					Debug.Log("Running initialization command: " + text);
					string strCommand = text.Trim();
					ConsoleSystem.Run(ConsoleSystem.Option.Server, strCommand);
				}
			}
			Bootstrap.Init_Systems();
			Bootstrap.Init_Config();
		}
		StartCoroutine(DoGameSetup());
	}

	private IEnumerator DoGameSetup()
	{
		Rust.Application.isLoading = true;
		TerrainMeta.InitNoTerrain();
		ItemManager.Initialize();
		LevelManager.CurrentLevelName = SceneManager.GetActiveScene().name;
		if (startServer)
		{
			yield return StartCoroutine(Bootstrap.StartNexusServer());
		}
		if (loadLevel && !string.IsNullOrEmpty(loadLevelScene))
		{
			Network.Net.sv.Reset();
			ConVar.Server.level = loadLevelScene;
			LoadingScreen.Update("LOADING SCENE");
			UnityEngine.Application.LoadLevelAdditive(loadLevelScene);
			LoadingScreen.Update(loadLevelScene.ToUpper() + " LOADED");
		}
		if (startServer)
		{
			yield return StartCoroutine(StartServer());
		}
		yield return null;
		Rust.Application.isLoading = false;
	}

	private IEnumerator StartServer()
	{
		ConVar.GC.collect();
		ConVar.GC.unload();
		yield return CoroutineEx.waitForEndOfFrame;
		yield return CoroutineEx.waitForEndOfFrame;
		if (loadSaveFile.StartsWith('"') && loadSaveFile.EndsWith('"'))
		{
			loadSaveFile = loadSaveFile.Substring(1, loadSaveFile.Length - 2);
		}
		yield return StartCoroutine(Bootstrap.StartServer(loadSave, loadSaveFile, allowOutOfDateSaves: true));
	}
}
