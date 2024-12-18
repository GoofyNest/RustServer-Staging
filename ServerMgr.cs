using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CompanionServer;
using ConVar;
using Facepunch;
using Facepunch.Math;
using Facepunch.Models;
using Facepunch.Network;
using Facepunch.Ping;
using Facepunch.Rust;
using Facepunch.Rust.Profiling;
using Ionic.Crc;
using Network;
using Network.Visibility;
using ProtoBuf;
using Rust;
using Rust.Ai.Gen2;
using Steamworks;
using UnityEngine;

public class ServerMgr : SingletonComponent<ServerMgr>, IServerCallback
{
	public const string BYPASS_PROCEDURAL_SPAWN_PREF = "bypassProceduralSpawn";

	private ConnectionAuth auth;

	public UserPersistance persistance;

	public PlayerStateManager playerStateManager;

	private AIThinkManager.QueueType aiTick;

	private Stopwatch methodTimer = new Stopwatch();

	private Stopwatch updateTimer = new Stopwatch();

	private List<ulong> bannedPlayerNotices = new List<ulong>();

	private string _AssemblyHash;

	private IEnumerator restartCoroutine;

	public ConnectionQueue connectionQueue = new ConnectionQueue();

	public TimeAverageValueLookup<Message.Type> packetHistory = new TimeAverageValueLookup<Message.Type>();

	public TimeAverageValueLookup<uint> rpcHistory = new TimeAverageValueLookup<uint>();

	private Stopwatch timer = new Stopwatch();

	public bool runFrameUpdate { get; private set; }

	public int AvailableSlots => ConVar.Server.maxplayers - BasePlayer.activePlayerList.Count - connectionQueue.ReservedCount;

	private string AssemblyHash
	{
		get
		{
			if (_AssemblyHash == null)
			{
				string location = typeof(ServerMgr).Assembly.Location;
				if (!string.IsNullOrEmpty(location))
				{
					byte[] array = File.ReadAllBytes(location);
					CRC32 cRC = new CRC32();
					cRC.SlurpBlock(array, 0, array.Length);
					_AssemblyHash = cRC.Crc32Result.ToString("x");
				}
				else
				{
					_AssemblyHash = "il2cpp";
				}
			}
			return _AssemblyHash;
		}
	}

	public bool Restarting => restartCoroutine != null;

	public bool Initialize(bool loadSave = true, string saveFile = "", bool allowOutOfDateSaves = false, bool skipInitialSpawn = false)
	{
		persistance = new UserPersistance(ConVar.Server.rootFolder);
		playerStateManager = new PlayerStateManager(persistance);
		TutorialIsland.GenerateIslandSpawnPoints(loadingSave: true);
		if ((bool)SingletonComponent<SpawnHandler>.Instance)
		{
			using (TimeWarning.New("SpawnHandler.UpdateDistributions"))
			{
				SingletonComponent<SpawnHandler>.Instance.UpdateDistributions();
			}
		}
		if (loadSave)
		{
			World.LoadedFromSave = true;
			World.LoadedFromSave = (skipInitialSpawn = SaveRestore.Load(saveFile, allowOutOfDateSaves));
		}
		else
		{
			SaveRestore.SaveCreatedTime = DateTime.UtcNow;
			World.LoadedFromSave = false;
		}
		if (!World.LoadedFromSave)
		{
			SaveRestore.SpawnMapEntities(SaveRestore.FindMapEntities());
		}
		SaveRestore.InitializeWipeId();
		if ((bool)SingletonComponent<SpawnHandler>.Instance)
		{
			if (!skipInitialSpawn)
			{
				using (TimeWarning.New("SpawnHandler.InitialSpawn", 200))
				{
					SingletonComponent<SpawnHandler>.Instance.InitialSpawn();
				}
			}
			using (TimeWarning.New("SpawnHandler.StartSpawnTick", 200))
			{
				SingletonComponent<SpawnHandler>.Instance.StartSpawnTick();
			}
		}
		CreateImportantEntities();
		auth = GetComponent<ConnectionAuth>();
		Analytics.Azure.Initialize();
		return World.LoadedFromSave;
	}

	public void OpenConnection(bool useSteamServer = true)
	{
		if (ConVar.Server.queryport <= 0 || ConVar.Server.queryport == ConVar.Server.port)
		{
			ConVar.Server.queryport = Math.Max(ConVar.Server.port, RCon.Port) + 1;
		}
		Network.Net.sv.ip = ConVar.Server.ip;
		Network.Net.sv.port = ConVar.Server.port;
		if (useSteamServer)
		{
			StartSteamServer();
		}
		else
		{
			PlatformService.Instance.Initialize(RustPlatformHooks.Instance);
		}
		if (!Network.Net.sv.Start())
		{
			UnityEngine.Debug.LogWarning("Couldn't Start Server.");
			CloseConnection();
			return;
		}
		Network.Net.sv.callbackHandler = this;
		Network.Net.sv.cryptography = new NetworkCryptographyServer();
		EACServer.DoStartup();
		InvokeRepeating("DoTick", 1f, 1f / (float)ConVar.Server.tickrate);
		InvokeRepeating("DoHeartbeat", 1f, 1f);
		runFrameUpdate = true;
		ConsoleSystem.OnReplicatedVarChanged += OnReplicatedVarChanged;
		if (ConVar.Server.autoUploadMap)
		{
			MapUploader.UploadMap();
		}
	}

	public void CloseConnection()
	{
		if (persistance != null)
		{
			persistance.Dispose();
			persistance = null;
		}
		EACServer.DoShutdown();
		Network.Net.sv.callbackHandler = null;
		using (TimeWarning.New("sv.Stop"))
		{
			Network.Net.sv.Stop("Shutting Down");
		}
		using (TimeWarning.New("RCon.Shutdown"))
		{
			RCon.Shutdown();
		}
		using (TimeWarning.New("PlatformService.Shutdown"))
		{
			PlatformService.Instance?.Shutdown();
		}
		using (TimeWarning.New("CompanionServer.Shutdown"))
		{
			CompanionServer.Server.Shutdown();
		}
		using (TimeWarning.New("NexusServer.Shutdown"))
		{
			NexusServer.Shutdown();
		}
		ConsoleSystem.OnReplicatedVarChanged -= OnReplicatedVarChanged;
	}

	private void OnDisable()
	{
		if (!Rust.Application.isQuitting)
		{
			CloseConnection();
		}
	}

	private void OnApplicationQuit()
	{
		Rust.Application.isQuitting = true;
		CloseConnection();
	}

	private void CreateImportantEntities()
	{
		CreateImportantEntity<EnvSync>("assets/bundled/prefabs/system/net_env.prefab");
		CreateImportantEntity<CommunityEntity>("assets/bundled/prefabs/system/server/community.prefab");
		CreateImportantEntity<ResourceDepositManager>("assets/bundled/prefabs/system/server/resourcedepositmanager.prefab");
		CreateImportantEntity<RelationshipManager>("assets/bundled/prefabs/system/server/relationship_manager.prefab");
		if (ConVar.Clan.enabled)
		{
			CreateImportantEntity<ClanManager>("assets/bundled/prefabs/system/server/clan_manager.prefab");
		}
		CreateImportantEntity<TreeManager>("assets/bundled/prefabs/system/tree_manager.prefab");
		CreateImportantEntity<GlobalNetworkHandler>("assets/bundled/prefabs/system/net_global.prefab");
	}

	public void CreateImportantEntity<T>(string prefabName) where T : BaseEntity
	{
		if (!BaseNetworkable.serverEntities.OfType<T>().FirstOrDefault())
		{
			UnityEngine.Debug.LogWarning("Missing " + typeof(T).Name + " - creating");
			BaseEntity baseEntity = GameManager.server.CreateEntity(prefabName);
			if (baseEntity == null)
			{
				UnityEngine.Debug.LogWarning("Couldn't create");
			}
			else
			{
				baseEntity.Spawn();
			}
		}
	}

	private void StartSteamServer()
	{
		PlatformService.Instance.Initialize(RustPlatformHooks.Instance);
		InvokeRepeating("UpdateServerInformation", 2f, 30f);
		InvokeRepeating("UpdateItemDefinitions", 10f, 3600f);
		DebugEx.Log("SteamServer Initialized");
	}

	private void UpdateItemDefinitions()
	{
		UnityEngine.Debug.Log("Checking for new Steam Item Definitions..");
		PlatformService.Instance.RefreshItemDefinitions();
	}

	internal void OnValidateAuthTicketResponse(ulong SteamId, ulong OwnerId, AuthResponse Status)
	{
		if (Auth_Steam.ValidateConnecting(SteamId, OwnerId, Status))
		{
			return;
		}
		Network.Connection connection = Network.Net.sv.connections.FirstOrDefault((Network.Connection x) => x.userid == SteamId);
		if (connection == null)
		{
			UnityEngine.Debug.LogWarning($"Steam gave us a {Status} ticket response for unconnected id {SteamId}");
			return;
		}
		switch (Status)
		{
		case AuthResponse.OK:
			UnityEngine.Debug.LogWarning($"Steam gave us a 'ok' ticket response for already connected id {SteamId}");
			return;
		case AuthResponse.TimedOut:
			return;
		case AuthResponse.VACBanned:
		case AuthResponse.PublisherBanned:
			if (!bannedPlayerNotices.Contains(SteamId))
			{
				ConsoleNetwork.BroadcastToAllClients("chat.add", 2, 0, "<color=#fff>SERVER</color> Kicking " + connection.username.EscapeRichText() + " (banned by anticheat)");
				bannedPlayerNotices.Add(SteamId);
			}
			break;
		}
		UnityEngine.Debug.Log($"Kicking {connection.ipaddress}/{connection.userid}/{connection.username} (Steam Status \"{Status.ToString()}\")");
		connection.authStatusSteam = Status.ToString();
		Network.Net.sv.Kick(connection, "Steam: " + Status);
	}

	private void Update()
	{
		if (!runFrameUpdate)
		{
			return;
		}
		updateTimer.Restart();
		Facepunch.Models.Manifest manifest = Facepunch.Application.Manifest;
		if (manifest != null && manifest.Features.ServerAnalytics)
		{
			try
			{
				PerformanceLogging.server.OnFrame();
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
		using (TimeWarning.New("ServerMgr.Update", 500))
		{
			try
			{
				using (TimeWarning.New("EACServer.DoUpdate", 100))
				{
					EACServer.DoUpdate();
				}
			}
			catch (Exception exception2)
			{
				UnityEngine.Debug.LogWarning("Server Exception: EACServer.DoUpdate");
				UnityEngine.Debug.LogException(exception2, this);
			}
			try
			{
				using (TimeWarning.New("PlatformService.Update", 100))
				{
					PlatformService.Instance.Update();
				}
			}
			catch (Exception exception3)
			{
				UnityEngine.Debug.LogWarning("Server Exception: Platform Service Update");
				UnityEngine.Debug.LogException(exception3, this);
			}
			try
			{
				using (TimeWarning.New("BaseMountable.PlayerSyncCycle"))
				{
					BaseMountable.PlayerSyncCycle();
				}
			}
			catch (Exception exception4)
			{
				UnityEngine.Debug.LogWarning("Server Exception: BaseMountable Player Sync Cycle");
				UnityEngine.Debug.LogException(exception4, this);
			}
			try
			{
				using (TimeWarning.New("Net.sv.Cycle", 100))
				{
					methodTimer.Restart();
					Network.Net.sv.Cycle();
					RuntimeProfiler.Net_Cycle = methodTimer.Elapsed;
				}
			}
			catch (Exception exception5)
			{
				UnityEngine.Debug.LogWarning("Server Exception: Network Cycle");
				UnityEngine.Debug.LogException(exception5, this);
			}
			try
			{
				using (TimeWarning.New("ServerBuildingManager.Cycle"))
				{
					BuildingManager.server.Cycle();
				}
			}
			catch (Exception exception6)
			{
				UnityEngine.Debug.LogWarning("Server Exception: Building Manager");
				UnityEngine.Debug.LogException(exception6, this);
			}
			try
			{
				using (TimeWarning.New("BasePlayer.ServerCycle"))
				{
					bool batchsynctransforms = ConVar.Physics.batchsynctransforms;
					bool autosynctransforms = ConVar.Physics.autosynctransforms;
					if (batchsynctransforms && autosynctransforms)
					{
						UnityEngine.Physics.autoSyncTransforms = false;
					}
					if (!UnityEngine.Physics.autoSyncTransforms)
					{
						methodTimer.Restart();
						UnityEngine.Physics.SyncTransforms();
						RuntimeProfiler.Physics_SyncTransforms = methodTimer.Elapsed;
					}
					try
					{
						using (TimeWarning.New("CameraRendererManager.Tick", 100))
						{
							CameraRendererManager instance = SingletonComponent<CameraRendererManager>.Instance;
							if (instance != null)
							{
								methodTimer.Restart();
								instance.Tick();
								RuntimeProfiler.Companion_Tick = methodTimer.Elapsed;
							}
						}
					}
					catch (Exception exception7)
					{
						UnityEngine.Debug.LogWarning("Server Exception: CameraRendererManager.Tick");
						UnityEngine.Debug.LogException(exception7, this);
					}
					methodTimer.Restart();
					BasePlayer.ServerCycle(UnityEngine.Time.deltaTime);
					RuntimeProfiler.BasePlayer_ServerCycle = methodTimer.Elapsed;
					try
					{
						using (TimeWarning.New("FlameTurret.BudgetedUpdate"))
						{
							FlameTurret.updateFlameTurretQueueServer.RunQueue(0.25);
						}
					}
					catch (Exception exception8)
					{
						UnityEngine.Debug.LogWarning("Server Exception: FlameTurret.BudgetedUpdate");
						UnityEngine.Debug.LogException(exception8, this);
					}
					try
					{
						using (TimeWarning.New("AutoTurret.BudgetedUpdate"))
						{
							AutoTurret.updateAutoTurretScanQueue.RunList(AutoTurret.auto_turret_budget_ms);
						}
					}
					catch (Exception exception9)
					{
						UnityEngine.Debug.LogWarning("Server Exception: AutoTurret.BudgetedUpdate");
						UnityEngine.Debug.LogException(exception9, this);
					}
					try
					{
						using (TimeWarning.New("GunTrap.BudgetedUpdate"))
						{
							GunTrap.updateGunTrapWorkQueue.RunList(GunTrap.gun_trap_budget_ms);
						}
					}
					catch (Exception exception10)
					{
						UnityEngine.Debug.LogWarning("Server Exception: GunTrap.BudgetedUpdate");
						UnityEngine.Debug.LogException(exception10, this);
					}
					try
					{
						using (TimeWarning.New("BaseFishingRod.BudgetedUpdate"))
						{
							BaseFishingRod.updateFishingRodQueue.RunQueue(1.0);
						}
					}
					catch (Exception exception11)
					{
						UnityEngine.Debug.LogWarning("Server Exception: BaseFishingRod.BudgetedUpdate");
						UnityEngine.Debug.LogException(exception11, this);
					}
					try
					{
						using (TimeWarning.New("DroppedItem.BudgetedUpdate"))
						{
							DroppedItem.underwaterStatusQueue.RunList(DroppedItem.underwater_drag_budget_ms);
						}
					}
					catch (Exception exception12)
					{
						UnityEngine.Debug.LogWarning("Server Exception: DroppedItem.BudgetedUpdate");
						UnityEngine.Debug.LogException(exception12, this);
					}
					if (batchsynctransforms && autosynctransforms)
					{
						UnityEngine.Physics.autoSyncTransforms = true;
					}
				}
			}
			catch (Exception exception13)
			{
				UnityEngine.Debug.LogWarning("Server Exception: Player Update");
				UnityEngine.Debug.LogException(exception13, this);
			}
			try
			{
				using (TimeWarning.New("connectionQueue.Cycle"))
				{
					connectionQueue.Cycle(AvailableSlots);
				}
			}
			catch (Exception exception14)
			{
				UnityEngine.Debug.LogWarning("Server Exception: Connection Queue");
				UnityEngine.Debug.LogException(exception14, this);
			}
			try
			{
				using (TimeWarning.New("IOEntity.ProcessQueue"))
				{
					IOEntity.ProcessQueue();
				}
			}
			catch (Exception exception15)
			{
				UnityEngine.Debug.LogWarning("Server Exception: IOEntity.ProcessQueue");
				UnityEngine.Debug.LogException(exception15, this);
			}
			try
			{
				using (TimeWarning.New("NpcManagers.Tick"))
				{
					if (SingletonComponent<NpcFireManager>.Instance != null)
					{
						SingletonComponent<NpcFireManager>.Instance.Tick();
					}
					if (SingletonComponent<NpcNoiseManager>.Instance != null)
					{
						SingletonComponent<NpcNoiseManager>.Instance.Tick();
					}
				}
			}
			catch (Exception exception16)
			{
				UnityEngine.Debug.LogWarning("Server Exception: NpcManagers.Tick");
				UnityEngine.Debug.LogException(exception16, this);
			}
			try
			{
				using (TimeWarning.New("FSMComponent.BudgetedUpdate"))
				{
					FSMComponent.workQueue.RunList(FSMComponent.frameBudgetMs);
				}
			}
			catch (Exception exception17)
			{
				UnityEngine.Debug.LogWarning("Server Exception: FSMComponent.BudgetedUpdate");
				UnityEngine.Debug.LogException(exception17, this);
			}
			try
			{
				using (TimeWarning.New("LimitedTurnNavAgent.TickSteering"))
				{
					LimitedTurnNavAgent.TickSteering();
				}
			}
			catch (Exception exception18)
			{
				UnityEngine.Debug.LogWarning("Server Exception: LimitedTurnNavAgent.TickSteering");
				UnityEngine.Debug.LogException(exception18, this);
			}
			if (!AI.spliceupdates)
			{
				aiTick = AIThinkManager.QueueType.Human;
			}
			else
			{
				aiTick = ((aiTick == AIThinkManager.QueueType.Human) ? AIThinkManager.QueueType.Animal : AIThinkManager.QueueType.Human);
			}
			if (aiTick == AIThinkManager.QueueType.Human)
			{
				try
				{
					using (TimeWarning.New("AIThinkManager.ProcessQueue"))
					{
						AIThinkManager.ProcessQueue(AIThinkManager.QueueType.Human);
					}
				}
				catch (Exception exception19)
				{
					UnityEngine.Debug.LogWarning("Server Exception: AIThinkManager.ProcessQueue");
					UnityEngine.Debug.LogException(exception19, this);
				}
				if (!AI.spliceupdates)
				{
					aiTick = AIThinkManager.QueueType.Animal;
				}
			}
			if (aiTick == AIThinkManager.QueueType.Animal)
			{
				try
				{
					using (TimeWarning.New("AIThinkManager.ProcessAnimalQueue"))
					{
						AIThinkManager.ProcessQueue(AIThinkManager.QueueType.Animal);
					}
				}
				catch (Exception exception20)
				{
					UnityEngine.Debug.LogWarning("Server Exception: AIThinkManager.ProcessAnimalQueue");
					UnityEngine.Debug.LogException(exception20, this);
				}
			}
			try
			{
				using (TimeWarning.New("AIThinkManager.ProcessPetQueue"))
				{
					AIThinkManager.ProcessQueue(AIThinkManager.QueueType.Pets);
				}
			}
			catch (Exception exception21)
			{
				UnityEngine.Debug.LogWarning("Server Exception: AIThinkManager.ProcessPetQueue");
				UnityEngine.Debug.LogException(exception21, this);
			}
			try
			{
				using (TimeWarning.New("AIThinkManager.ProcessPetMovementQueue"))
				{
					BasePet.ProcessMovementQueue();
				}
			}
			catch (Exception exception22)
			{
				UnityEngine.Debug.LogWarning("Server Exception: AIThinkManager.ProcessPetMovementQueue");
				UnityEngine.Debug.LogException(exception22, this);
			}
			try
			{
				using (TimeWarning.New("BaseSculpture.ProcessGridUpdates"))
				{
					BaseSculpture.ProcessGridUpdates();
				}
			}
			catch (Exception exception23)
			{
				UnityEngine.Debug.LogWarning("Server Exception: BaseSculpture.ProcessGridUpdates");
				UnityEngine.Debug.LogException(exception23, this);
			}
			try
			{
				using (TimeWarning.New("BaseRidableAnimal.ProcessQueue"))
				{
					BaseRidableAnimal.ProcessQueue();
				}
			}
			catch (Exception exception24)
			{
				UnityEngine.Debug.LogWarning("Server Exception: BaseRidableAnimal.ProcessQueue");
				UnityEngine.Debug.LogException(exception24, this);
			}
			try
			{
				using (TimeWarning.New("GrowableEntity.BudgetedUpdate"))
				{
					GrowableEntity.growableEntityUpdateQueue.RunQueue(GrowableEntity.framebudgetms);
				}
			}
			catch (Exception exception25)
			{
				UnityEngine.Debug.LogWarning("Server Exception: GrowableEntity.BudgetedUpdate");
				UnityEngine.Debug.LogException(exception25, this);
			}
			try
			{
				using (TimeWarning.New("BasePlayer.BudgetedLifeStoryUpdate"))
				{
					BasePlayer.lifeStoryQueue.RunQueue(BasePlayer.lifeStoryFramebudgetms);
				}
			}
			catch (Exception exception26)
			{
				UnityEngine.Debug.LogWarning("Server Exception: BasePlayer.BudgetedLifeStoryUpdate");
				UnityEngine.Debug.LogException(exception26, this);
			}
			try
			{
				using (TimeWarning.New("JunkPileWater.UpdateNearbyPlayers"))
				{
					JunkPileWater.junkpileWaterWorkQueue.RunQueue(JunkPileWater.framebudgetms);
				}
			}
			catch (Exception exception27)
			{
				UnityEngine.Debug.LogWarning("Server Exception: JunkPileWater.UpdateNearbyPlayers");
				UnityEngine.Debug.LogException(exception27, this);
			}
			try
			{
				using (TimeWarning.New("IndustrialEntity.RunQueue"))
				{
					IndustrialEntity.Queue.RunQueue(ConVar.Server.industrialFrameBudgetMs);
				}
			}
			catch (Exception exception28)
			{
				UnityEngine.Debug.LogWarning("Server Exception: IndustrialEntity.RunQueue");
				UnityEngine.Debug.LogException(exception28, this);
			}
			try
			{
				using (TimeWarning.New("AntiHack.Cycle"))
				{
					AntiHack.Cycle();
				}
			}
			catch (Exception exception29)
			{
				UnityEngine.Debug.LogWarning("Server Exception: AntiHack.Cycle");
				UnityEngine.Debug.LogException(exception29, this);
			}
			try
			{
				using (TimeWarning.New("TreeManager.SendPendingTrees"))
				{
					TreeManager.server.SendPendingTrees();
				}
			}
			catch (Exception exception30)
			{
				UnityEngine.Debug.LogWarning("Server Exception: TreeManager.SendPendingTrees");
				UnityEngine.Debug.LogException(exception30, this);
			}
		}
		RuntimeProfiler.ServerMgr_Update = updateTimer.Elapsed;
	}

	private void LateUpdate()
	{
		if (!runFrameUpdate)
		{
			return;
		}
		using (TimeWarning.New("ServerMgr.LateUpdate", 500))
		{
			if (!Facepunch.Network.SteamNetworking.steamnagleflush)
			{
				return;
			}
			try
			{
				using (TimeWarning.New("Connection.Flush"))
				{
					for (int i = 0; i < Network.Net.sv.connections.Count; i++)
					{
						Network.Net.sv.Flush(Network.Net.sv.connections[i]);
					}
				}
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogWarning("Server Exception: Connection.Flush");
				UnityEngine.Debug.LogException(exception, this);
			}
		}
	}

	private void FixedUpdate()
	{
		using (TimeWarning.New("ServerMgr.FixedUpdate"))
		{
			try
			{
				using (TimeWarning.New("BaseMountable.FixedUpdateCycle"))
				{
					BaseMountable.FixedUpdateCycle();
				}
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogWarning("Server Exception: Mountable Cycle");
				UnityEngine.Debug.LogException(exception, this);
			}
			try
			{
				using (TimeWarning.New("Buoyancy.Cycle"))
				{
					Buoyancy.Cycle();
				}
			}
			catch (Exception exception2)
			{
				UnityEngine.Debug.LogWarning("Server Exception: Buoyancy Cycle");
				UnityEngine.Debug.LogException(exception2, this);
			}
		}
	}

	private void DoTick()
	{
		RCon.Update();
		CompanionServer.Server.Update();
		NexusServer.Update();
		for (int i = 0; i < Network.Net.sv.connections.Count; i++)
		{
			Network.Connection connection = Network.Net.sv.connections[i];
			if (!connection.isAuthenticated && !(connection.GetSecondsConnected() < (float)ConVar.Server.authtimeout))
			{
				Network.Net.sv.Kick(connection, "Authentication Timed Out");
			}
		}
	}

	private void DoHeartbeat()
	{
		ItemManager.Heartbeat();
	}

	private static BaseGameMode Gamemode()
	{
		BaseGameMode activeGameMode = BaseGameMode.GetActiveGameMode(serverside: true);
		if (!(activeGameMode != null))
		{
			return null;
		}
		return activeGameMode;
	}

	public static string GamemodeName()
	{
		return Gamemode()?.shortname ?? "rust";
	}

	public static string GamemodeTitle()
	{
		return Gamemode()?.gamemodeTitle ?? "Survival";
	}

	private void UpdateServerInformation()
	{
		if (!SteamServer.IsValid)
		{
			return;
		}
		using (TimeWarning.New("UpdateServerInformation"))
		{
			SteamServer.ServerName = ConVar.Server.hostname;
			SteamServer.MaxPlayers = ConVar.Server.maxplayers;
			SteamServer.Passworded = false;
			SteamServer.MapName = World.GetServerBrowserMapName();
			string value = "stok";
			if (Restarting)
			{
				value = "strst";
			}
			string text = $"born{Epoch.FromDateTime(SaveRestore.SaveCreatedTime)}";
			string text2 = $"gm{GamemodeName()}";
			string text3 = (ConVar.Server.pve ? ",pve" : string.Empty);
			string text4 = ConVar.Server.tags?.Trim(',') ?? "";
			string text5 = ((!string.IsNullOrWhiteSpace(text4)) ? ("," + text4) : "");
			string text6 = BuildInfo.Current?.Scm?.ChangeId ?? "0";
			string text7 = PingEstimater.GetCachedClosestRegion().Code;
			if (!string.IsNullOrEmpty(ConVar.Server.ping_region_code_override))
			{
				text7 = ConVar.Server.ping_region_code_override;
			}
			SteamServer.GameTags = ServerTagCompressor.CompressTags($"mp{ConVar.Server.maxplayers},cp{BasePlayer.activePlayerList.Count},pt{Network.Net.sv.ProtocolId},qp{SingletonComponent<ServerMgr>.Instance.connectionQueue.Queued},$r{text7},v{2571}{text3}{text5},{text},{text2},cs{text6}");
			if (ConVar.Server.description != null && ConVar.Server.description.Length > 100)
			{
				string[] array = ConVar.Server.description.SplitToChunks(100).ToArray();
				for (int i = 0; i < 16; i++)
				{
					if (i < array.Length)
					{
						SteamServer.SetKey($"description_{i:00}", array[i]);
					}
					else
					{
						SteamServer.SetKey($"description_{i:00}", string.Empty);
					}
				}
			}
			else
			{
				SteamServer.SetKey("description_0", ConVar.Server.description);
				for (int j = 1; j < 16; j++)
				{
					SteamServer.SetKey($"description_{j:00}", string.Empty);
				}
			}
			SteamServer.SetKey("hash", AssemblyHash);
			SteamServer.SetKey("status", value);
			string value2 = World.Seed.ToString();
			BaseGameMode activeGameMode = BaseGameMode.GetActiveGameMode(serverside: true);
			if (activeGameMode != null && !activeGameMode.ingameMap)
			{
				value2 = "0";
			}
			SteamServer.SetKey("world.seed", value2);
			SteamServer.SetKey("world.size", World.Size.ToString());
			SteamServer.SetKey("pve", ConVar.Server.pve.ToString());
			SteamServer.SetKey("headerimage", ConVar.Server.headerimage);
			SteamServer.SetKey("logoimage", ConVar.Server.logoimage);
			SteamServer.SetKey("url", ConVar.Server.url);
			if (!string.IsNullOrWhiteSpace(ConVar.Server.favoritesEndpoint))
			{
				SteamServer.SetKey("favendpoint", ConVar.Server.favoritesEndpoint);
			}
			SteamServer.SetKey("gmn", GamemodeName());
			SteamServer.SetKey("gmt", GamemodeTitle());
			SteamServer.SetKey("uptime", ((int)UnityEngine.Time.realtimeSinceStartup).ToString());
			SteamServer.SetKey("gc_mb", Performance.report.memoryAllocations.ToString());
			SteamServer.SetKey("gc_cl", Performance.report.memoryCollections.ToString());
			SteamServer.SetKey("ram_sys", (Performance.report.memoryUsageSystem / 1000000).ToString());
			SteamServer.SetKey("fps", Performance.report.frameRate.ToString());
			SteamServer.SetKey("fps_avg", Performance.report.frameRateAverage.ToString("0.00"));
			SteamServer.SetKey("ent_cnt", BaseNetworkable.serverEntities.Count.ToString());
			SteamServer.SetKey("build", BuildInfo.Current.Scm.ChangeId);
		}
	}

	public void OnDisconnected(string strReason, Network.Connection connection)
	{
		Analytics.Azure.OnPlayerDisconnected(connection, strReason);
		GlobalNetworkHandler.server.OnClientDisconnected(connection);
		connectionQueue.TryAddReservedSlot(connection);
		connectionQueue.RemoveConnection(connection);
		ConnectionAuth.OnDisconnect(connection);
		if (connection.authStatusSteam == "ok")
		{
			PlatformService.Instance.EndPlayerSession(connection.userid);
		}
		EACServer.OnLeaveGame(connection);
		BasePlayer basePlayer = connection.player as BasePlayer;
		if (basePlayer != null)
		{
			basePlayer.OnDisconnected();
		}
		if (connection.authStatusNexus == "ok")
		{
			NexusServer.Logout(connection.userid);
		}
	}

	public static void OnEnterVisibility(Network.Connection connection, Group group)
	{
		if (Network.Net.sv.IsConnected())
		{
			NetWrite netWrite = Network.Net.sv.StartWrite();
			netWrite.PacketID(Message.Type.GroupEnter);
			netWrite.GroupID(group.ID);
			netWrite.Send(new SendInfo(connection));
		}
	}

	public static void OnLeaveVisibility(Network.Connection connection, Group group)
	{
		if (Network.Net.sv.IsConnected())
		{
			NetWrite netWrite = Network.Net.sv.StartWrite();
			netWrite.PacketID(Message.Type.GroupLeave);
			netWrite.GroupID(group.ID);
			netWrite.Send(new SendInfo(connection));
			NetWrite netWrite2 = Network.Net.sv.StartWrite();
			netWrite2.PacketID(Message.Type.GroupDestroy);
			netWrite2.GroupID(group.ID);
			netWrite2.Send(new SendInfo(connection));
		}
	}

	public static BasePlayer.SpawnPoint FindSpawnPoint(BasePlayer forPlayer = null)
	{
		bool flag = false;
		if (forPlayer != null && forPlayer.IsInTutorial)
		{
			TutorialIsland currentTutorialIsland = forPlayer.GetCurrentTutorialIsland();
			if (currentTutorialIsland != null)
			{
				BasePlayer.SpawnPoint spawnPoint = new BasePlayer.SpawnPoint();
				if (forPlayer.CurrentTutorialAllowance > BasePlayer.TutorialItemAllowance.Level1_HatchetPickaxe)
				{
					spawnPoint.pos = currentTutorialIsland.MidMissionSpawnPoint.position;
					spawnPoint.rot = currentTutorialIsland.MidMissionSpawnPoint.rotation;
				}
				else
				{
					spawnPoint.pos = currentTutorialIsland.InitialSpawnPoint.position;
					spawnPoint.rot = currentTutorialIsland.InitialSpawnPoint.rotation;
				}
				return spawnPoint;
			}
		}
		BaseGameMode baseGameMode = Gamemode();
		if ((bool)baseGameMode && baseGameMode.useCustomSpawns)
		{
			BasePlayer.SpawnPoint playerSpawn = baseGameMode.GetPlayerSpawn(forPlayer);
			if (playerSpawn != null)
			{
				return playerSpawn;
			}
		}
		if (SingletonComponent<SpawnHandler>.Instance != null && !flag)
		{
			BasePlayer.SpawnPoint spawnPoint2 = SpawnHandler.GetSpawnPoint();
			if (spawnPoint2 != null)
			{
				return spawnPoint2;
			}
		}
		BasePlayer.SpawnPoint spawnPoint3 = new BasePlayer.SpawnPoint();
		if (forPlayer != null && forPlayer.IsInTutorial)
		{
			TutorialIsland currentTutorialIsland2 = forPlayer.GetCurrentTutorialIsland();
			if (currentTutorialIsland2 != null)
			{
				spawnPoint3.pos = currentTutorialIsland2.InitialSpawnPoint.position;
				spawnPoint3.rot = currentTutorialIsland2.InitialSpawnPoint.rotation;
				return spawnPoint3;
			}
		}
		GameObject[] array = GameObject.FindGameObjectsWithTag("spawnpoint");
		if (array.Length != 0)
		{
			GameObject gameObject = array[UnityEngine.Random.Range(0, array.Length)];
			spawnPoint3.pos = gameObject.transform.position;
			spawnPoint3.rot = gameObject.transform.rotation;
		}
		else
		{
			UnityEngine.Debug.Log("Couldn't find an appropriate spawnpoint for the player - so spawning at camera");
			if (MainCamera.mainCamera != null)
			{
				spawnPoint3.pos = MainCamera.position;
				spawnPoint3.rot = MainCamera.rotation;
			}
		}
		if (UnityEngine.Physics.Raycast(new Ray(spawnPoint3.pos, Vector3.down), out var hitInfo, 32f, 1537286401))
		{
			spawnPoint3.pos = hitInfo.point;
		}
		return spawnPoint3;
	}

	public void JoinGame(Network.Connection connection)
	{
		using (Approval approval = Facepunch.Pool.Get<Approval>())
		{
			uint num = (uint)ConVar.Server.encryption;
			if (num > 1 && connection.os == "editor" && DeveloperList.Contains(connection.ownerid))
			{
				num = 1u;
			}
			if (num > 1 && !ConVar.Server.secure)
			{
				num = 1u;
			}
			approval.level = UnityEngine.Application.loadedLevelName;
			approval.levelConfig = World.Config.JsonString;
			approval.levelTransfer = World.Transfer;
			approval.levelUrl = World.Url;
			approval.levelSeed = World.Seed;
			approval.levelSize = World.Size;
			approval.checksum = World.Checksum;
			approval.hostname = ConVar.Server.hostname;
			approval.official = ConVar.Server.official;
			approval.encryption = num;
			approval.version = BuildInfo.Current.Scm.Branch + "#" + BuildInfo.Current.Scm.ChangeId;
			approval.nexus = World.Nexus;
			approval.nexusEndpoint = Nexus.endpoint;
			approval.nexusId = NexusServer.NexusId.GetValueOrDefault();
			NetWrite netWrite = Network.Net.sv.StartWrite();
			netWrite.PacketID(Message.Type.Approved);
			approval.WriteToStream(netWrite);
			netWrite.Send(new SendInfo(connection));
			connection.encryptionLevel = num;
		}
		connection.connected = true;
	}

	internal void Shutdown()
	{
		BasePlayer[] array = BasePlayer.activePlayerList.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			array[i].Kick("Server Shutting Down");
		}
		ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.save");
		ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.writecfg");
	}

	private IEnumerator ServerRestartWarning(string info, int iSeconds)
	{
		if (iSeconds < 0)
		{
			yield break;
		}
		if (!string.IsNullOrEmpty(info))
		{
			ConsoleNetwork.BroadcastToAllClients("chat.add", 2, 0, "<color=#fff>SERVER</color> Restarting: " + info);
		}
		for (int i = iSeconds; i > 0; i--)
		{
			if (i == iSeconds || i % 60 == 0 || (i < 300 && i % 30 == 0) || (i < 60 && i % 10 == 0) || i < 10)
			{
				ConsoleNetwork.BroadcastToAllClients("chat.add", 2, 0, $"<color=#fff>SERVER</color> Restarting in {i} seconds ({info})!");
				UnityEngine.Debug.Log($"Restarting in {i} seconds");
			}
			yield return CoroutineEx.waitForSeconds(1f);
		}
		ConsoleNetwork.BroadcastToAllClients("chat.add", 2, 0, "<color=#fff>SERVER</color> Restarting (" + info + ")");
		yield return CoroutineEx.waitForSeconds(2f);
		BasePlayer[] array = BasePlayer.activePlayerList.ToArray();
		for (int j = 0; j < array.Length; j++)
		{
			array[j].Kick("Server Restarting");
		}
		yield return CoroutineEx.waitForSeconds(1f);
		ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit");
	}

	public static void RestartServer(string strNotice, int iSeconds)
	{
		if (!(SingletonComponent<ServerMgr>.Instance == null))
		{
			if (SingletonComponent<ServerMgr>.Instance.restartCoroutine != null)
			{
				ConsoleNetwork.BroadcastToAllClients("chat.add", 2, 0, "<color=#fff>SERVER</color> Restart interrupted!");
				SingletonComponent<ServerMgr>.Instance.StopCoroutine(SingletonComponent<ServerMgr>.Instance.restartCoroutine);
				SingletonComponent<ServerMgr>.Instance.restartCoroutine = null;
			}
			SingletonComponent<ServerMgr>.Instance.restartCoroutine = SingletonComponent<ServerMgr>.Instance.ServerRestartWarning(strNotice, iSeconds);
			SingletonComponent<ServerMgr>.Instance.StartCoroutine(SingletonComponent<ServerMgr>.Instance.restartCoroutine);
			SingletonComponent<ServerMgr>.Instance.UpdateServerInformation();
		}
	}

	public static void SendReplicatedVars(string filter)
	{
		NetWrite netWrite = Network.Net.sv.StartWrite();
		List<Network.Connection> obj = Facepunch.Pool.Get<List<Network.Connection>>();
		foreach (Network.Connection connection in Network.Net.sv.connections)
		{
			if (connection.connected)
			{
				obj.Add(connection);
			}
		}
		List<ConsoleSystem.Command> obj2 = Facepunch.Pool.Get<List<ConsoleSystem.Command>>();
		foreach (ConsoleSystem.Command item in ConsoleSystem.Index.Server.Replicated)
		{
			if (item.FullName.StartsWith(filter))
			{
				obj2.Add(item);
			}
		}
		netWrite.PacketID(Message.Type.ConsoleReplicatedVars);
		netWrite.Int32(obj2.Count);
		foreach (ConsoleSystem.Command item2 in obj2)
		{
			netWrite.String(item2.FullName);
			netWrite.String(item2.String);
		}
		netWrite.Send(new SendInfo(obj));
		Facepunch.Pool.FreeUnmanaged(ref obj2);
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	public static void SendReplicatedVars(Network.Connection connection)
	{
		NetWrite netWrite = Network.Net.sv.StartWrite();
		List<ConsoleSystem.Command> replicated = ConsoleSystem.Index.Server.Replicated;
		netWrite.PacketID(Message.Type.ConsoleReplicatedVars);
		netWrite.Int32(replicated.Count);
		foreach (ConsoleSystem.Command item in replicated)
		{
			netWrite.String(item.FullName);
			netWrite.String(item.String);
		}
		netWrite.Send(new SendInfo(connection));
	}

	private static void OnReplicatedVarChanged(string fullName, string value)
	{
		NetWrite netWrite = Network.Net.sv.StartWrite();
		List<Network.Connection> obj = Facepunch.Pool.Get<List<Network.Connection>>();
		foreach (Network.Connection connection in Network.Net.sv.connections)
		{
			if (connection.connected)
			{
				obj.Add(connection);
			}
		}
		netWrite.PacketID(Message.Type.ConsoleReplicatedVars);
		netWrite.Int32(1);
		netWrite.String(fullName);
		netWrite.String(value);
		netWrite.Send(new SendInfo(obj));
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	private void Log(Exception e)
	{
		if (ConVar.Global.developer > 0)
		{
			UnityEngine.Debug.LogException(e);
		}
	}

	public void OnNetworkMessage(Message packet)
	{
		if (ConVar.Server.packetlog_enabled)
		{
			packetHistory.Increment(packet.type);
		}
		if (PacketProfiler.enabled)
		{
			PacketProfiler.LogInbound(packet.type, (int)packet.read.Length);
		}
		switch (packet.type)
		{
		case Message.Type.GiveUserInformation:
			if (packet.connection.GetPacketsPerSecond(packet.type) >= 1)
			{
				Network.Net.sv.Kick(packet.connection, "Packet Flooding: User Information", packet.connection.connected);
				break;
			}
			using (TimeWarning.New("GiveUserInformation", 20))
			{
				try
				{
					OnGiveUserInformation(packet);
				}
				catch (Exception e7)
				{
					Log(e7);
					Network.Net.sv.Kick(packet.connection, "Invalid Packet: User Information");
				}
			}
			packet.connection.AddPacketsPerSecond(packet.type);
			break;
		case Message.Type.Ready:
			if (!packet.connection.connected)
			{
				break;
			}
			if (packet.connection.GetPacketsPerSecond(packet.type) >= 1)
			{
				Network.Net.sv.Kick(packet.connection, "Packet Flooding: Client Ready", packet.connection.connected);
				break;
			}
			using (TimeWarning.New("ClientReady", 20))
			{
				try
				{
					ClientReady(packet);
				}
				catch (Exception e9)
				{
					Log(e9);
					Network.Net.sv.Kick(packet.connection, "Invalid Packet: Client Ready");
				}
			}
			packet.connection.AddPacketsPerSecond(packet.type);
			break;
		case Message.Type.RPCMessage:
			if (!packet.connection.connected)
			{
				break;
			}
			if (packet.connection.GetPacketsPerSecond(packet.type) >= (ulong)ConVar.Server.maxpacketspersecond_rpc)
			{
				Network.Net.sv.Kick(packet.connection, "Packet Flooding: RPC Message");
				break;
			}
			using (TimeWarning.New("OnRPCMessage", 20))
			{
				try
				{
					OnRPCMessage(packet);
				}
				catch (Exception e8)
				{
					Log(e8);
					Network.Net.sv.Kick(packet.connection, "Invalid Packet: RPC Message");
				}
			}
			packet.connection.AddPacketsPerSecond(packet.type);
			break;
		case Message.Type.ConsoleCommand:
			if (!packet.connection.connected)
			{
				break;
			}
			if (packet.connection.GetPacketsPerSecond(packet.type) >= (ulong)ConVar.Server.maxpacketspersecond_command)
			{
				Network.Net.sv.Kick(packet.connection, "Packet Flooding: Client Command", packet.connection.connected);
				break;
			}
			using (TimeWarning.New("OnClientCommand", 20))
			{
				try
				{
					ConsoleNetwork.OnClientCommand(packet);
				}
				catch (Exception e5)
				{
					Log(e5);
					Network.Net.sv.Kick(packet.connection, "Invalid Packet: Client Command");
				}
			}
			packet.connection.AddPacketsPerSecond(packet.type);
			break;
		case Message.Type.DisconnectReason:
			if (!packet.connection.connected)
			{
				break;
			}
			if (packet.connection.GetPacketsPerSecond(packet.type) >= 1)
			{
				Network.Net.sv.Kick(packet.connection, "Packet Flooding: Disconnect Reason", packet.connection.connected);
				break;
			}
			using (TimeWarning.New("ReadDisconnectReason", 20))
			{
				try
				{
					ReadDisconnectReason(packet);
					Network.Net.sv.Disconnect(packet.connection);
				}
				catch (Exception e2)
				{
					Log(e2);
					Network.Net.sv.Kick(packet.connection, "Invalid Packet: Disconnect Reason");
				}
			}
			packet.connection.AddPacketsPerSecond(packet.type);
			break;
		case Message.Type.Tick:
			if (!packet.connection.connected)
			{
				break;
			}
			if (packet.connection.GetPacketsPerSecond(packet.type) >= (ulong)ConVar.Server.maxpacketspersecond_tick)
			{
				Network.Net.sv.Kick(packet.connection, "Packet Flooding: Player Tick", packet.connection.connected);
				break;
			}
			using (TimeWarning.New("OnPlayerTick", 20))
			{
				try
				{
					OnPlayerTick(packet);
				}
				catch (Exception e4)
				{
					Log(e4);
					Network.Net.sv.Kick(packet.connection, "Invalid Packet: Player Tick");
				}
			}
			packet.connection.AddPacketsPerSecond(packet.type);
			break;
		case Message.Type.EAC:
			using (TimeWarning.New("OnEACMessage", 20))
			{
				try
				{
					EACServer.OnMessageReceived(packet);
					break;
				}
				catch (Exception e3)
				{
					Log(e3);
					Network.Net.sv.Kick(packet.connection, "Invalid Packet: EAC");
					break;
				}
			}
		case Message.Type.World:
			if (!World.Transfer || !packet.connection.connected)
			{
				break;
			}
			if (packet.connection.GetPacketsPerSecond(packet.type) >= (ulong)ConVar.Server.maxpacketspersecond_world)
			{
				Network.Net.sv.Kick(packet.connection, "Packet Flooding: World", packet.connection.connected);
				break;
			}
			using (TimeWarning.New("OnWorldMessage", 20))
			{
				try
				{
					WorldNetworking.OnMessageReceived(packet);
					break;
				}
				catch (Exception e6)
				{
					Log(e6);
					Network.Net.sv.Kick(packet.connection, "Invalid Packet: World");
					break;
				}
			}
		case Message.Type.VoiceData:
			if (!packet.connection.connected)
			{
				break;
			}
			if (packet.connection.GetPacketsPerSecond(packet.type) >= (ulong)ConVar.Server.maxpacketspersecond_voice)
			{
				Network.Net.sv.Kick(packet.connection, "Packet Flooding: Disconnect Reason", packet.connection.connected);
				break;
			}
			using (TimeWarning.New("OnPlayerVoice", 20))
			{
				try
				{
					OnPlayerVoice(packet);
				}
				catch (Exception e)
				{
					Log(e);
					Network.Net.sv.Kick(packet.connection, "Invalid Packet: Player Voice");
				}
			}
			packet.connection.AddPacketsPerSecond(packet.type);
			break;
		default:
			ProcessUnhandledPacket(packet);
			break;
		}
	}

	public void ProcessUnhandledPacket(Message packet)
	{
		if (ConVar.Global.developer > 0)
		{
			UnityEngine.Debug.LogWarning("[SERVER][UNHANDLED] " + packet.type);
		}
		Network.Net.sv.Kick(packet.connection, "Sent Unhandled Message");
	}

	public void ReadDisconnectReason(Message packet)
	{
		string text = packet.read.String(4096);
		string text2 = packet.connection.ToString();
		if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(text2))
		{
			DebugEx.Log(text2 + " disconnecting: " + text);
		}
	}

	private BasePlayer SpawnPlayerSleeping(Network.Connection connection)
	{
		BasePlayer basePlayer = BasePlayer.FindSleeping(connection.userid);
		if (basePlayer == null)
		{
			return null;
		}
		if (!basePlayer.IsSleeping())
		{
			UnityEngine.Debug.LogWarning("Player spawning into sleeper that isn't sleeping!");
			basePlayer.Kill();
			return null;
		}
		basePlayer.PlayerInit(connection);
		basePlayer.inventory.SendSnapshot();
		DebugEx.Log(basePlayer.net.connection.ToString() + " joined [" + basePlayer.net.connection.os + "/" + basePlayer.net.connection.ownerid + "]");
		return basePlayer;
	}

	public BasePlayer SpawnNewPlayer(Network.Connection connection)
	{
		BasePlayer.SpawnPoint spawnPoint = FindSpawnPoint();
		BasePlayer basePlayer = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", spawnPoint.pos, spawnPoint.rot).ToPlayer();
		basePlayer.health = 0f;
		basePlayer.lifestate = BaseCombatEntity.LifeState.Dead;
		basePlayer.ResetLifeStateOnSpawn = false;
		basePlayer.limitNetworking = true;
		if (connection == null)
		{
			basePlayer.EnableTransferProtection();
		}
		basePlayer.Spawn();
		basePlayer.limitNetworking = false;
		if (connection != null)
		{
			basePlayer.PlayerInit(connection);
			if ((bool)BaseGameMode.GetActiveGameMode(serverside: true))
			{
				BaseGameMode.GetActiveGameMode(serverside: true).OnNewPlayer(basePlayer);
			}
			else if (UnityEngine.Application.isEditor || (SleepingBag.FindForPlayer(basePlayer.userID, ignoreTimers: true).Length == 0 && !basePlayer.hasPreviousLife))
			{
				basePlayer.Respawn();
			}
			DebugEx.Log($"{basePlayer.displayName} with steamid {basePlayer.userID.Get()} joined from ip {basePlayer.net.connection.ipaddress}");
			DebugEx.Log($"\tNetworkId {basePlayer.userID.Get()} is {basePlayer.net.ID} ({basePlayer.displayName})");
			if (basePlayer.net.connection.ownerid != 0L && basePlayer.net.connection.ownerid != basePlayer.net.connection.userid)
			{
				DebugEx.Log($"\t{basePlayer} is sharing the account {basePlayer.net.connection.ownerid}");
			}
		}
		return basePlayer;
	}

	private void ClientReady(Message packet)
	{
		if (packet.connection.state != Network.Connection.State.Welcoming)
		{
			Network.Net.sv.Kick(packet.connection, "Invalid connection state");
			return;
		}
		using (ClientReady clientReady = ProtoBuf.ClientReady.Deserialize(packet.read))
		{
			foreach (ClientReady.ClientInfo item in clientReady.clientInfo)
			{
				packet.connection.info.Set(item.name, item.value);
			}
			packet.connection.globalNetworking = clientReady.globalNetworking;
			connectionQueue.JoinedGame(packet.connection);
			Analytics.Azure.OnPlayerConnected(packet.connection);
			using (TimeWarning.New("ClientReady"))
			{
				BasePlayer basePlayer;
				using (TimeWarning.New("SpawnPlayerSleeping"))
				{
					basePlayer = SpawnPlayerSleeping(packet.connection);
				}
				if (basePlayer == null)
				{
					using (TimeWarning.New("SpawnNewPlayer"))
					{
						basePlayer = SpawnNewPlayer(packet.connection);
					}
				}
				basePlayer.SendRespawnOptions();
				basePlayer.LoadClanInfo();
				if (basePlayer != null)
				{
					Util.SendSignedInNotification(basePlayer);
				}
			}
		}
		SendReplicatedVars(packet.connection);
	}

	private void OnRPCMessage(Message packet)
	{
		timer.Restart();
		NetworkableId uid = packet.read.EntityID();
		uint num = packet.read.UInt32();
		if (ConVar.Server.rpclog_enabled)
		{
			rpcHistory.Increment(num);
		}
		BaseEntity baseEntity = BaseNetworkable.serverEntities.Find(uid) as BaseEntity;
		if (!(baseEntity == null))
		{
			baseEntity.SV_RPCMessage(num, packet);
			if (timer.Elapsed > RuntimeProfiler.RpcWarningThreshold)
			{
				LagSpikeProfiler.RPC(timer.Elapsed, packet, baseEntity, num);
			}
		}
	}

	private void OnPlayerTick(Message packet)
	{
		BasePlayer basePlayer = packet.Player();
		if (!(basePlayer == null))
		{
			basePlayer.OnReceivedTick(packet.read);
		}
	}

	private void OnPlayerVoice(Message packet)
	{
		BasePlayer basePlayer = packet.Player();
		if (!(basePlayer == null))
		{
			basePlayer.OnReceivedVoice(packet.read.BytesWithSize());
		}
	}

	private void OnGiveUserInformation(Message packet)
	{
		if (packet.connection.state != 0)
		{
			Network.Net.sv.Kick(packet.connection, "Invalid connection state");
			return;
		}
		packet.connection.state = Network.Connection.State.Connecting;
		if (packet.read.UInt8() != 228)
		{
			Network.Net.sv.Kick(packet.connection, "Invalid Connection Protocol");
			return;
		}
		packet.connection.userid = packet.read.UInt64();
		packet.connection.protocol = packet.read.UInt32();
		packet.connection.os = packet.read.String(128);
		packet.connection.username = packet.read.String();
		if (string.IsNullOrEmpty(packet.connection.os))
		{
			throw new Exception("Invalid OS");
		}
		if (string.IsNullOrEmpty(packet.connection.username))
		{
			Network.Net.sv.Kick(packet.connection, "Invalid Username");
			return;
		}
		packet.connection.username = packet.connection.username.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ')
			.Trim();
		if (string.IsNullOrEmpty(packet.connection.username))
		{
			Network.Net.sv.Kick(packet.connection, "Invalid Username");
			return;
		}
		string text = string.Empty;
		string branch = ConVar.Server.branch;
		if (packet.read.Unread >= 4)
		{
			text = packet.read.String(128);
		}
		if (branch != string.Empty && branch != text)
		{
			DebugEx.Log("Kicking " + packet.connection?.ToString() + " - their branch is '" + text + "' not '" + branch + "'");
			Network.Net.sv.Kick(packet.connection, "Wrong Steam Beta: Requires '" + branch + "' branch!");
		}
		else if (packet.connection.protocol > 2571)
		{
			DebugEx.Log("Kicking " + packet.connection?.ToString() + " - their protocol is " + packet.connection.protocol + " not " + 2571);
			Network.Net.sv.Kick(packet.connection, "Wrong Connection Protocol: Server update required!");
		}
		else if (packet.connection.protocol < 2571)
		{
			DebugEx.Log("Kicking " + packet.connection?.ToString() + " - their protocol is " + packet.connection.protocol + " not " + 2571);
			Network.Net.sv.Kick(packet.connection, "Wrong Connection Protocol: Client update required!");
		}
		else
		{
			packet.connection.token = packet.read.BytesWithSize(512u);
			if (packet.connection.token == null || packet.connection.token.Length < 1)
			{
				Network.Net.sv.Kick(packet.connection, "Invalid Token");
				return;
			}
			packet.connection.anticheatId = packet.read.StringRaw(128);
			packet.connection.anticheatToken = packet.read.StringRaw(2048);
			packet.connection.clientChangeset = packet.read.Int32();
			packet.connection.clientBuildTime = packet.read.Int64();
			auth.OnNewConnection(packet.connection);
		}
	}
}
