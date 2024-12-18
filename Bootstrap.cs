using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompanionServer;
using ConVar;
using Facepunch;
using Facepunch.Network;
using Facepunch.Network.Raknet;
using Facepunch.Rust;
using Facepunch.Rust.Profiling;
using Facepunch.Utility;
using Network;
using Rust;
using Rust.Ai;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

public class Bootstrap : SingletonComponent<Bootstrap>
{
	internal static bool bootstrapInitRun;

	public static bool isErrored;

	public string messageString = "Loading...";

	public CanvasGroup BootstrapUiCanvas;

	public GameObject errorPanel;

	public TextMeshProUGUI errorText;

	public TextMeshProUGUI statusText;

	private static string lastWrittenValue;

	public static bool needsSetup => !bootstrapInitRun;

	public static bool isPresent
	{
		get
		{
			if (bootstrapInitRun)
			{
				return true;
			}
			if (UnityEngine.Object.FindObjectsOfType<GameSetup>().Count() > 0)
			{
				return true;
			}
			return false;
		}
	}

	public static void RunDefaults()
	{
		Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
		Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
		UnityEngine.Application.targetFrameRate = 256;
		UnityEngine.Time.fixedDeltaTime = 0.0625f;
		UnityEngine.Time.maximumDeltaTime = 0.125f;
	}

	public static void Init_Tier0()
	{
		RunDefaults();
		GameSetup.RunOnce = true;
		bootstrapInitRun = true;
		ConsoleSystem.Index.Initialize(ConsoleGen.All);
		ConsoleSystem.Index.Reset();
		UnityButtons.Register();
		Output.Install();
		Facepunch.Pool.ResizeBuffer<NetRead>(16384);
		Facepunch.Pool.ResizeBuffer<NetWrite>(16384);
		Facepunch.Pool.ResizeBuffer<Networkable>(65536);
		Facepunch.Pool.ResizeBuffer<EntityLink>(65536);
		Facepunch.Pool.ResizeBuffer<EventRecord>(16384);
		Facepunch.Pool.FillBuffer<Networkable>();
		Facepunch.Pool.FillBuffer<EntityLink>();
		if (Facepunch.CommandLine.HasSwitch("-nonetworkthread"))
		{
			BaseNetwork.Multithreading = false;
		}
		SteamNetworking.SetDebugFunction();
		if (Facepunch.CommandLine.HasSwitch("-swnet"))
		{
			NetworkInitSteamworks(enableSteamDatagramRelay: false);
		}
		else if (Facepunch.CommandLine.HasSwitch("-sdrnet"))
		{
			NetworkInitSteamworks(enableSteamDatagramRelay: true);
		}
		else if (Facepunch.CommandLine.HasSwitch("-raknet"))
		{
			NetworkInitRaknet();
		}
		else
		{
			NetworkInitRaknet();
		}
		if (!UnityEngine.Application.isEditor)
		{
			string text = Facepunch.CommandLine.Full.Replace(Facepunch.CommandLine.GetSwitch("-rcon.password", Facepunch.CommandLine.GetSwitch("+rcon.password", "RCONPASSWORD")), "******");
			WriteToLog("Command Line: " + text);
		}
		int parentProcessId = Facepunch.CommandLine.GetSwitchInt("-parent-pid", 0);
		if (parentProcessId != 0)
		{
			try
			{
				SynchronizationContext syncContext = SynchronizationContext.Current;
				Process processById = Process.GetProcessById(parentProcessId);
				processById.EnableRaisingEvents = true;
				processById.Exited += delegate
				{
					syncContext.Post(delegate
					{
						WriteToLog($"Parent process ID {parentProcessId} exited. Exiting the server now...");
						ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit");
					}, null);
				};
				WriteToLog($"Watching parent process ID {parentProcessId}...");
			}
			catch (ArgumentException)
			{
				WriteToLog($"Parent process ID {parentProcessId} has exited during boot! Exiting now...");
				Rust.Application.Quit();
			}
		}
		UnityHookHandler.EnsureCreated();
	}

	public static void Init_Systems()
	{
		Rust.Global.Init();
		if (GameInfo.IsOfficialServer && ConVar.Server.stats)
		{
			GA.Logging = false;
			GA.Build = BuildInfo.Current.Scm.ChangeId;
			GA.Initialize("218faecaf1ad400a4e15c53392ebeebc", "0c9803ce52c38671278899538b9c54c8d4e19849");
			Analytics.Server.Enabled = true;
		}
		Integration integration = new Integration();
		integration.OnManifestUpdated += CpuAffinity.Apply;
		Facepunch.Application.Initialize(integration);
		Facepunch.Performance.GetMemoryUsage = () => SystemInfoEx.systemMemoryUsed;
	}

	public static void Init_Config()
	{
		ConsoleNetwork.Init();
		ConsoleSystem.UpdateValuesFromCommandLine();
		ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.readcfg");
		ServerUsers.Load();
		if (string.IsNullOrEmpty(ConVar.Server.server_id))
		{
			ConVar.Server.server_id = Guid.NewGuid().ToString("N");
			ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.writecfg");
		}
		if (Facepunch.CommandLine.HasSwitch("-server-occlusion"))
		{
			ServerOcclusion.OcclusionEnabled = true;
		}
		if (Facepunch.CommandLine.HasSwitch("-server-occlusion-rocks"))
		{
			ServerOcclusion.OcclusionEnabled = true;
			ServerOcclusion.OcclusionIncludeRocks = true;
		}
		HttpManager.UpdateMaxConnections();
		if (!RuntimeProfiler.runtime_profiling_persist)
		{
			RuntimeProfiler.Disable();
		}
	}

	public static void NetworkInitRaknet()
	{
		Network.Net.sv = new Facepunch.Network.Raknet.Server();
	}

	public static void NetworkInitSteamworks(bool enableSteamDatagramRelay)
	{
		Network.Net.sv = new SteamNetworking.Server(enableSteamDatagramRelay);
	}

	private IEnumerator Start()
	{
		WriteToLog("Bootstrap Startup");
		EarlyInitialize();
		BenchmarkTimer.Enabled = Facepunch.Utility.CommandLine.Full.Contains("+autobench");
		BenchmarkTimer timer = BenchmarkTimer.New("bootstrap");
		if (!UnityEngine.Application.isEditor)
		{
			BuildInfo current = BuildInfo.Current;
			if ((current.Scm.Branch == null || !(current.Scm.Branch == "experimental/release")) && !(current.Scm.Branch == "release"))
			{
				ExceptionReporter.InitializeFromUrl("https://0654eb77d1e04d6babad83201b6b6b95:d2098f1d15834cae90501548bd5dbd0d@sentry.io/1836389");
			}
			else
			{
				ExceptionReporter.InitializeFromUrl("https://83df169465e84da091c1a3cd2fbffeee:3671b903f9a840ecb68411cf946ab9b6@sentry.io/51080");
			}
			bool num = Facepunch.Utility.CommandLine.Full.Contains("-official") || Facepunch.Utility.CommandLine.Full.Contains("-server.official") || Facepunch.Utility.CommandLine.Full.Contains("+official") || Facepunch.Utility.CommandLine.Full.Contains("+server.official");
			bool flag = Facepunch.Utility.CommandLine.Full.Contains("-stats") || Facepunch.Utility.CommandLine.Full.Contains("-server.stats") || Facepunch.Utility.CommandLine.Full.Contains("+stats") || Facepunch.Utility.CommandLine.Full.Contains("+server.stats");
			ExceptionReporter.Disabled = !(num && flag);
		}
		if (AssetBundleBackend.Enabled)
		{
			AssetBundleBackend newBackend = new AssetBundleBackend();
			using (BenchmarkTimer.New("bootstrap;bundles"))
			{
				yield return StartCoroutine(LoadingUpdate("Opening Bundles"));
				newBackend.Load("Bundles/Bundles");
				FileSystem.Backend = newBackend;
			}
			if (FileSystem.Backend.isError)
			{
				ThrowError(FileSystem.Backend.loadingError);
				yield break;
			}
			using (BenchmarkTimer.New("bootstrap;bundlesindex"))
			{
				newBackend.BuildFileIndex();
			}
		}
		if (FileSystem.Backend.isError)
		{
			ThrowError(FileSystem.Backend.loadingError);
			yield break;
		}
		if (!UnityEngine.Application.isEditor)
		{
			WriteToLog(SystemInfoGeneralText.currentInfo);
		}
		UnityEngine.Texture.SetGlobalAnisotropicFilteringLimits(1, 16);
		if (isErrored)
		{
			yield break;
		}
		using (BenchmarkTimer.New("bootstrap;gamemanifest"))
		{
			yield return StartCoroutine(LoadingUpdate("Loading Game Manifest"));
			GameManifest.Load();
			yield return StartCoroutine(LoadingUpdate("DONE!"));
		}
		using (BenchmarkTimer.New("bootstrap;selfcheck"))
		{
			yield return StartCoroutine(LoadingUpdate("Running Self Check"));
			SelfCheck.Run();
		}
		if (isErrored)
		{
			yield break;
		}
		yield return StartCoroutine(LoadingUpdate("Bootstrap Tier0"));
		using (BenchmarkTimer.New("bootstrap;tier0"))
		{
			Init_Tier0();
		}
		using (BenchmarkTimer.New("bootstrap;commandlinevalues"))
		{
			ConsoleSystem.UpdateValuesFromCommandLine();
		}
		yield return StartCoroutine(LoadingUpdate("Bootstrap Systems"));
		using (BenchmarkTimer.New("bootstrap;init_systems"))
		{
			Init_Systems();
		}
		yield return StartCoroutine(LoadingUpdate("Bootstrap Config"));
		using (BenchmarkTimer.New("bootstrap;init_config"))
		{
			Init_Config();
		}
		if (!isErrored)
		{
			yield return StartCoroutine(LoadingUpdate("Loading Items"));
			using (BenchmarkTimer.New("bootstrap;itemmanager"))
			{
				ItemManager.Initialize();
			}
			if (!isErrored)
			{
				yield return StartCoroutine(DedicatedServerStartup());
				timer?.Dispose();
				GameManager.Destroy(base.gameObject);
			}
		}
	}

	private IEnumerator DedicatedServerStartup()
	{
		Rust.Application.isLoading = true;
		UnityEngine.Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.High;
		WriteToLog("Skinnable Warmup");
		yield return CoroutineEx.waitForEndOfFrame;
		yield return CoroutineEx.waitForEndOfFrame;
		GameManifest.LoadAssets();
		WriteToLog("Initializing Nexus");
		yield return StartCoroutine(StartNexusServer());
		WriteToLog("Loading Scene");
		yield return CoroutineEx.waitForEndOfFrame;
		yield return CoroutineEx.waitForEndOfFrame;
		UnityEngine.Physics.defaultSolverIterations = 3;
		int @int = PlayerPrefs.GetInt("UnityGraphicsQuality");
		QualitySettings.SetQualityLevel(0);
		PlayerPrefs.SetInt("UnityGraphicsQuality", @int);
		UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		UnityEngine.Object.DontDestroyOnLoad(GameManager.server.CreatePrefab("assets/bundled/prefabs/system/server_console.prefab"));
		StartupShared();
		World.InitSize(ConVar.Server.worldsize);
		World.InitSeed(ConVar.Server.seed);
		World.InitSalt(ConVar.Server.salt);
		World.Url = ConVar.Server.levelurl;
		World.Transfer = ConVar.Server.leveltransfer;
		LevelManager.LoadLevel(ConVar.Server.level);
		yield return CoroutineEx.waitForEndOfFrame;
		yield return CoroutineEx.waitForEndOfFrame;
		string[] assetList = FileSystem_Warmup.GetAssetList();
		yield return StartCoroutine(FileSystem_Warmup.Run(assetList, WriteToLog, "Asset Warmup ({0}/{1})"));
		yield return StartCoroutine(StartServer(!Facepunch.CommandLine.HasSwitch("-skipload"), "", allowOutOfDateSaves: false));
		if (!UnityEngine.Object.FindObjectOfType<Performance>())
		{
			UnityEngine.Object.DontDestroyOnLoad(GameManager.server.CreatePrefab("assets/bundled/prefabs/system/performance.prefab"));
		}
		Rust.GC.Collect();
		DemoConVars.Level = LevelManager.CurrentLevelName;
		DemoConVars.Seed = World.Seed.ToString();
		DemoConVars.WorldSize = World.Size.ToString();
		DemoConVars.LevelUrl = World.Url;
		DemoConVars.Checksum = World.Checksum;
		DemoConVars.Hostname = ConVar.Server.hostname;
		DemoConVars.NetworkVersion = 2571;
		DemoConVars.Changeset = BuildInfo.Current?.Scm?.ChangeId ?? "0";
		Rust.Application.isLoading = false;
	}

	private static void EnsureRootFolderCreated()
	{
		try
		{
			Directory.CreateDirectory(ConVar.Server.rootFolder);
		}
		catch (Exception arg)
		{
			UnityEngine.Debug.LogWarning($"Failed to automatically create the save directory: {ConVar.Server.rootFolder}\n\n{arg}");
		}
	}

	public static IEnumerator StartNexusServer()
	{
		EnsureRootFolderCreated();
		yield return NexusServer.Initialize();
		if (NexusServer.FailedToStart)
		{
			UnityEngine.Debug.LogError("Nexus server failed to start, terminating");
			Rust.Application.Quit();
		}
	}

	public static IEnumerator StartServer(bool doLoad, string saveFileOverride, bool allowOutOfDateSaves)
	{
		float timeScale = UnityEngine.Time.timeScale;
		if (ConVar.Time.pausewhileloading)
		{
			UnityEngine.Time.timeScale = 0f;
		}
		RCon.Initialize();
		BaseEntity.Query.Server = new BaseEntity.Query.EntityTree(8096f);
		EnsureRootFolderCreated();
		if ((bool)SingletonComponent<WorldSetup>.Instance)
		{
			yield return SingletonComponent<WorldSetup>.Instance.StartCoroutine(SingletonComponent<WorldSetup>.Instance.InitCoroutine());
		}
		if ((bool)SingletonComponent<DynamicNavMesh>.Instance && SingletonComponent<DynamicNavMesh>.Instance.enabled && !AiManager.nav_disable)
		{
			yield return SingletonComponent<DynamicNavMesh>.Instance.StartCoroutine(SingletonComponent<DynamicNavMesh>.Instance.UpdateNavMeshAndWait());
		}
		if ((bool)SingletonComponent<AiManager>.Instance && SingletonComponent<AiManager>.Instance.enabled)
		{
			SingletonComponent<AiManager>.Instance.Initialize();
			if (!AiManager.nav_disable && AI.npc_enable && TerrainMeta.Path != null)
			{
				foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
				{
					if (monument.HasNavmesh)
					{
						yield return monument.StartCoroutine(monument.GetMonumentNavMesh().UpdateNavMeshAndWait());
					}
				}
				if ((bool)TerrainMeta.Path && (bool)TerrainMeta.Path.DungeonGridRoot)
				{
					DungeonNavmesh dungeonNavmesh = TerrainMeta.Path.DungeonGridRoot.AddComponent<DungeonNavmesh>();
					dungeonNavmesh.NavMeshCollectGeometry = NavMeshCollectGeometry.PhysicsColliders;
					dungeonNavmesh.LayerMask = 65537;
					yield return dungeonNavmesh.StartCoroutine(dungeonNavmesh.UpdateNavMeshAndWait());
				}
				else
				{
					UnityEngine.Debug.LogError("Failed to find DungeonGridRoot, NOT generating Dungeon navmesh");
				}
				if ((bool)TerrainMeta.Path && (bool)TerrainMeta.Path.DungeonBaseRoot)
				{
					DungeonNavmesh dungeonNavmesh2 = TerrainMeta.Path.DungeonBaseRoot.AddComponent<DungeonNavmesh>();
					dungeonNavmesh2.NavmeshResolutionModifier = 0.3f;
					dungeonNavmesh2.NavMeshCollectGeometry = NavMeshCollectGeometry.PhysicsColliders;
					dungeonNavmesh2.LayerMask = 65537;
					yield return dungeonNavmesh2.StartCoroutine(dungeonNavmesh2.UpdateNavMeshAndWait());
				}
				else
				{
					UnityEngine.Debug.LogError("Failed to find DungeonBaseRoot , NOT generating Dungeon navmesh");
				}
				GenerateDungeonBase.SetupAI();
			}
		}
		UnityEngine.Object.DontDestroyOnLoad(GameManager.server.CreatePrefab("assets/bundled/prefabs/system/shared.prefab"));
		GameObject gameObject = GameManager.server.CreatePrefab("assets/bundled/prefabs/system/server.prefab");
		UnityEngine.Object.DontDestroyOnLoad(gameObject);
		ServerMgr serverMgr = gameObject.GetComponent<ServerMgr>();
		bool saveWasLoaded = serverMgr.Initialize(doLoad, saveFileOverride, allowOutOfDateSaves);
		yield return CoroutineEx.waitForSecondsRealtime(0.1f);
		SaveRestore.InitializeEntityLinks();
		yield return CoroutineEx.waitForSecondsRealtime(0.1f);
		SaveRestore.InitializeEntitySupports();
		yield return CoroutineEx.waitForSecondsRealtime(0.1f);
		SaveRestore.InitializeEntityConditionals();
		yield return CoroutineEx.waitForSecondsRealtime(0.1f);
		SaveRestore.GetSaveCache();
		yield return CoroutineEx.waitForSecondsRealtime(0.1f);
		BaseGameMode.CreateGameMode();
		yield return CoroutineEx.waitForSecondsRealtime(0.1f);
		MissionManifest.Get();
		yield return CoroutineEx.waitForSecondsRealtime(0.1f);
		if (Clan.enabled)
		{
			ClanManager clanManager = ClanManager.ServerInstance;
			if (clanManager == null)
			{
				UnityEngine.Debug.LogError("ClanManager was not spawned!");
				Rust.Application.Quit();
				yield break;
			}
			Task initializeTask = clanManager.Initialize();
			yield return new WaitUntil(() => initializeTask.IsCompleted);
			initializeTask.Wait();
			clanManager.LoadClanInfoForSleepers();
		}
		yield return CoroutineEx.waitForSecondsRealtime(0.1f);
		if (ServerOcclusion.OcclusionEnabled)
		{
			ServerOcclusion.SetupGrid();
		}
		yield return CoroutineEx.waitForSecondsRealtime(0.1f);
		if (NexusServer.Started)
		{
			NexusServer.UploadMapImage();
			if (saveWasLoaded)
			{
				NexusServer.RestoreUnsavedState();
			}
			NexusServer.ZoneClient.StartListening();
		}
		serverMgr.OpenConnection();
		CompanionServer.Server.Initialize();
		using (BenchmarkTimer.New("Boombox.LoadStations"))
		{
			BoomBox.LoadStations();
		}
		RustEmojiLibrary.FindAllServerEmoji();
		if (ConVar.Time.pausewhileloading)
		{
			UnityEngine.Time.timeScale = timeScale;
		}
		WriteToLog("Server startup complete");
	}

	private void StartupShared()
	{
		ItemManager.Initialize();
	}

	public void ThrowError(string error)
	{
		UnityEngine.Debug.Log("ThrowError: " + error);
		errorPanel.SetActive(value: true);
		errorText.text = error;
		isErrored = true;
	}

	public void ExitGame()
	{
		UnityEngine.Debug.Log("Exiting due to Exit Game button on bootstrap error panel");
		Rust.Application.Quit();
	}

	public static IEnumerator LoadingUpdate(string str)
	{
		if ((bool)SingletonComponent<Bootstrap>.Instance)
		{
			SingletonComponent<Bootstrap>.Instance.messageString = str;
			yield return CoroutineEx.waitForEndOfFrame;
			yield return CoroutineEx.waitForEndOfFrame;
		}
	}

	public static void WriteToLog(string str)
	{
		if (!(lastWrittenValue == str))
		{
			DebugEx.Log(str);
			lastWrittenValue = str;
		}
	}

	private static void EarlyInitialize()
	{
	}

	[Conditional("DEVELOPMENT_BUILD")]
	private static void EncryptionSmokeTest()
	{
		WriteToLog("Running encryption smoke test...");
		TestEncryption<byte>(170);
		TestEncryption<uint>(3735928559u);
		TestEncryption<ulong>(1311768467294899695uL);
		HiddenValue<string> hiddenValue = new HiddenValue<string>();
		if (hiddenValue.Get() != null)
		{
			UnityEngine.Debug.LogError("HiddenValue: default value is not null");
		}
		hiddenValue.Set("hello");
		if (hiddenValue.Get() != "hello")
		{
			UnityEngine.Debug.LogError("HiddenValue: returned incorrect value after set");
		}
		hiddenValue.Dispose();
		HiddenValue<string> hiddenValue2 = new HiddenValue<string>("hello");
		if (hiddenValue2.Get() != "hello")
		{
			UnityEngine.Debug.LogError("HiddenValue: returned incorrect value after constructor");
		}
		hiddenValue2.Dispose();
		WriteToLog("Finished encryption smoke test");
		static void TestEncryption<T>(T value) where T : unmanaged
		{
			EncryptedValue<T> encryptedValue = default(EncryptedValue<T>);
			if (object.Equals(encryptedValue.Get(), default(T)))
			{
				UnityEngine.Debug.LogError($"EncryptedValue<{typeof(T)}>: default value is 0 - missing encryption?");
			}
			else
			{
				encryptedValue.Set(value);
				if (!object.Equals(encryptedValue.Get(), value))
				{
					UnityEngine.Debug.LogError($"EncryptedValue<{typeof(T)}>: decrypted value is {encryptedValue.Get()} - expected {value}");
				}
				encryptedValue = value;
				if (!object.Equals((T)encryptedValue, value))
				{
					UnityEngine.Debug.LogError($"EncryptedValue<{typeof(T)}>: implicit decrypted value is {(T)encryptedValue} - expected {value}");
				}
			}
		}
	}
}
