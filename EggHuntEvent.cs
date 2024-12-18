using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using ConVar;
using Facepunch;
using ProtoBuf;
using UnityEngine;

public class EggHuntEvent : BaseHuntEvent
{
	public class EggHunter
	{
		public ulong userid;

		public string displayName;

		public int numEggs;
	}

	public float warmupTime = 10f;

	public float warnTime = 20f;

	public float timeAlive;

	public static EggHuntEvent serverEvent = null;

	public static EggHuntEvent clientEvent = null;

	[NonSerialized]
	public static float durationSeconds = 180f;

	private Dictionary<ulong, EggHunter> _eggHunters = new Dictionary<ulong, EggHunter>();

	public ItemAmount[] placementAwards;

	private Dictionary<ulong, List<CollectableEasterEgg>> _spawnedEggs = new Dictionary<ulong, List<CollectableEasterEgg>>();

	private float eggSpawningFrameBudget = 1.5f;

	private readonly int maxEggPerPlayer = 25;

	private int initialSpawnIndex;

	private readonly Stopwatch stopwatch = new Stopwatch();

	public static Translate.Phrase topBunnyPhrase = new Translate.Phrase("egghunt.result.topbunny", "{0} is the top bunny with {1} eggs collected.");

	public static Translate.Phrase noPlayersPhrase = new Translate.Phrase("egghunt.result.noplayers", "Wow, no one played so no one won.");

	public static Translate.Phrase placePhrase = new Translate.Phrase("egghunt.result.place", "You placed {0} of {1} with {2} eggs collected.");

	public static Translate.Phrase rewardPhrase = new Translate.Phrase("egghunt.result.reward", "You received {0}x {1} as an award!.");

	public bool IsEventActive()
	{
		if (timeAlive > warmupTime)
		{
			return timeAlive - warmupTime < durationSeconds;
		}
		return false;
	}

	public void Update()
	{
		timeAlive += UnityEngine.Time.deltaTime;
		if (base.isServer && !base.IsDestroyed)
		{
			if (timeAlive - warmupTime > durationSeconds - warnTime)
			{
				SetFlag(Flags.Reserved1, b: true);
			}
			if (timeAlive - warmupTime > durationSeconds && !IsInvoking(Cooldown))
			{
				SetFlag(Flags.Reserved2, b: true);
				CleanupEggs();
				PrintWinnersAndAward();
				Invoke(Cooldown, 10f);
			}
		}
	}

	public override void DestroyShared()
	{
		base.DestroyShared();
		if (base.isServer)
		{
			serverEvent = null;
		}
		else
		{
			clientEvent = null;
		}
	}

	public override void ServerInit()
	{
		base.ServerInit();
		if ((bool)serverEvent && base.isServer)
		{
			serverEvent.Kill();
			serverEvent = null;
		}
		serverEvent = this;
		SpawnEggs();
		Invoke(StartEvent, warmupTime);
	}

	private void StartEvent()
	{
		if (initialSpawnIndex <= BasePlayer.activePlayerList.Count)
		{
			eggSpawningFrameBudget = float.PositiveInfinity;
		}
		EnableEggs();
	}

	private void EnableEggs()
	{
		foreach (KeyValuePair<ulong, List<CollectableEasterEgg>> spawnedEgg in _spawnedEggs)
		{
			foreach (CollectableEasterEgg item in spawnedEgg.Value)
			{
				item.gameObject.SetActive(value: true);
				item.SetFlag(Flags.Disabled, b: false);
			}
		}
	}

	[ContextMenu("SpawnDebug")]
	public void SpawnEggs()
	{
		initialSpawnIndex = 0;
		StartCoroutine(SpawnInitialEggs());
	}

	private IEnumerator SpawnInitialEggs()
	{
		while (initialSpawnIndex != BasePlayer.activePlayerList.Count)
		{
			stopwatch.Reset();
			stopwatch.Start();
			for (int i = initialSpawnIndex; i < BasePlayer.activePlayerList.Count; i++)
			{
				BasePlayer basePlayer = BasePlayer.activePlayerList[i];
				int num = UnityEngine.Random.Range(4, 6) + Mathf.RoundToInt(basePlayer.eggVision);
				Vector3 position = basePlayer.transform.position;
				List<CollectableEasterEgg> list = TryGetPlayerEggs(basePlayer.userID);
				for (int j = 0; j < num; j++)
				{
					Vector3 randomSpawnPoint = GetRandomSpawnPoint(position, Vector3.zero, 15f, 25f);
					CollectableEasterEgg collectableEasterEgg = SpawnEggAtPoint(randomSpawnPoint, active: false);
					collectableEasterEgg.ownerUserID = basePlayer.userID;
					collectableEasterEgg.SetFlag(Flags.Disabled, b: true);
					collectableEasterEgg.Spawn();
					list.Add(collectableEasterEgg);
				}
				initialSpawnIndex++;
				if (stopwatch.Elapsed.TotalMilliseconds >= (double)eggSpawningFrameBudget)
				{
					break;
				}
			}
			yield return CoroutineEx.waitForEndOfFrame;
		}
	}

	private CollectableEasterEgg SpawnEggAtPoint(Vector3 pos, bool active)
	{
		GameManager server = GameManager.server;
		string strPrefab = HuntableResourcePathCached[UnityEngine.Random.Range(0, HuntableResourcePathCached.Count)];
		bool startActive = active;
		return server.CreateEntity(strPrefab, pos, default(Quaternion), startActive) as CollectableEasterEgg;
	}

	private Vector3 GetRandomSpawnPoint(Vector3 pos, Vector3 aimDir, float minDist = 1f, float maxDist = 2f)
	{
		aimDir = ((aimDir == Vector3.zero) ? UnityEngine.Random.onUnitSphere : AimConeUtil.GetModifiedAimConeDirection(90f, aimDir));
		Vector3 vector = pos + Vector3Ex.Direction2D(pos + aimDir * 10f, pos) * UnityEngine.Random.Range(minDist, maxDist);
		vector.y = TerrainMeta.HeightMap.GetHeight(vector);
		if (((uint)TerrainMeta.TopologyMap.GetTopology(vector) & 0x400002u) != 0 && UnityEngine.Physics.Raycast(vector + Vector3.up * 50f, Vector3.down, out var hitInfo, 55f, 65536))
		{
			vector.y = hitInfo.point.y;
		}
		return vector;
	}

	public void OnEggCollected(BasePlayer player, CollectableEasterEgg collectedEgg)
	{
		IncrementScore(player);
		if (_spawnedEggs.TryGetValue(collectedEgg.ownerUserID, out var value))
		{
			value.Remove(collectedEgg);
		}
		int num = ((!((float)Mathf.RoundToInt(player.eggVision) * 0.5f < 1f)) ? 1 : UnityEngine.Random.Range(0, 2));
		int num2 = UnityEngine.Random.Range(1 + num, 2 + num);
		List<CollectableEasterEgg> list = TryGetPlayerEggs(player.userID);
		for (int i = 0; i < num2; i++)
		{
			Vector3 randomSpawnPoint = GetRandomSpawnPoint(player.transform.position, player.eyes.BodyForward(), 15f, 25f);
			if (list.Count + 1 > maxEggPerPlayer)
			{
				list[0].Kill();
				list.Remove(list[0]);
			}
			CollectableEasterEgg collectableEasterEgg = SpawnEggAtPoint(randomSpawnPoint, active: true);
			collectableEasterEgg.ownerUserID = player.userID;
			collectableEasterEgg.Spawn();
			list.Add(collectableEasterEgg);
		}
	}

	private void IncrementScore(BasePlayer player)
	{
		if (!_eggHunters.TryGetValue(player.userID, out var value))
		{
			value = new EggHunter();
			value.displayName = player.displayName;
			value.userid = player.userID;
			_eggHunters.Add(player.userID, value);
		}
		value.numEggs++;
		QueueUpdate();
	}

	private void QueueUpdate()
	{
		if (!IsInvoking(DoNetworkUpdate))
		{
			Invoke(DoNetworkUpdate, 2f);
		}
	}

	private void DoNetworkUpdate()
	{
		SendNetworkUpdate();
	}

	private List<CollectableEasterEgg> TryGetPlayerEggs(ulong userID)
	{
		if (!_spawnedEggs.TryGetValue(userID, out var value))
		{
			value = new List<CollectableEasterEgg>();
			_spawnedEggs[userID] = value;
		}
		return value;
	}

	protected List<EggHunter> GetTopHunters()
	{
		List<EggHunter> list = Facepunch.Pool.Get<List<EggHunter>>();
		foreach (KeyValuePair<ulong, EggHunter> eggHunter in _eggHunters)
		{
			list.Add(eggHunter.Value);
		}
		list.Sort((EggHunter a, EggHunter b) => b.numEggs.CompareTo(a.numEggs));
		return list;
	}

	protected virtual Translate.Phrase GetTopBunnyPhrase()
	{
		return topBunnyPhrase;
	}

	protected virtual Translate.Phrase GetNoPlayersPhrase()
	{
		return noPlayersPhrase;
	}

	protected virtual Translate.Phrase GetPlacePhrase()
	{
		return placePhrase;
	}

	protected virtual Translate.Phrase GetRewardPhrase()
	{
		return rewardPhrase;
	}

	protected void PrintWinnersAndAward()
	{
		List<EggHunter> topHunters = GetTopHunters();
		if (topHunters.Count > 0)
		{
			EggHunter eggHunter = topHunters[0];
			Chat.Broadcast(string.Format(GetTopBunnyPhrase().translated, eggHunter.displayName, eggHunter.numEggs), "", "#eee", 0uL);
			for (int i = 0; i < topHunters.Count; i++)
			{
				EggHunter eggHunter2 = topHunters[i];
				BasePlayer basePlayer = BasePlayer.FindByID(eggHunter2.userid);
				if ((bool)basePlayer)
				{
					string translated = GetPlacePhrase().translated;
					translated = string.Format(translated, i + 1, topHunters.Count, topHunters[i].numEggs);
					basePlayer.ChatMessage(translated);
					ReportEggsCollected(topHunters[i].numEggs);
				}
				else
				{
					UnityEngine.Debug.LogWarning("EggHuntEvent PrintWinnersAndAward could not find player with id :" + eggHunter2.userid);
				}
			}
			ReportPlayerParticipated(topHunters.Count);
			for (int j = 0; j < placementAwards.Length && j < topHunters.Count; j++)
			{
				BasePlayer basePlayer2 = BasePlayer.FindByID(topHunters[j].userid);
				if ((bool)basePlayer2)
				{
					basePlayer2.inventory.GiveItem(ItemManager.Create(placementAwards[j].itemDef, (int)placementAwards[j].amount, 0uL), basePlayer2.inventory.containerMain);
					string translated2 = GetRewardPhrase().translated;
					translated2 = string.Format(translated2, (int)placementAwards[j].amount, placementAwards[j].itemDef.displayName.english);
					basePlayer2.ChatMessage(translated2);
				}
			}
		}
		else
		{
			Chat.Broadcast(GetNoPlayersPhrase().translated, "", "#eee", 0uL);
		}
	}

	protected virtual void ReportEggsCollected(int numEggs)
	{
	}

	protected virtual void ReportPlayerParticipated(int topCount)
	{
	}

	private void CleanupEggs()
	{
		foreach (KeyValuePair<ulong, List<CollectableEasterEgg>> spawnedEgg in _spawnedEggs)
		{
			if (spawnedEgg.Value == null)
			{
				continue;
			}
			foreach (CollectableEasterEgg item in spawnedEgg.Value)
			{
				if (item != null)
				{
					item.Kill();
				}
			}
		}
	}

	private void Cooldown()
	{
		CancelInvoke(Cooldown);
		Kill();
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.eggHunt = Facepunch.Pool.Get<EggHunt>();
		List<EggHunter> topHunters = GetTopHunters();
		info.msg.eggHunt.hunters = Facepunch.Pool.Get<List<EggHunt.EggHunter>>();
		for (int i = 0; i < Mathf.Min(10, topHunters.Count); i++)
		{
			EggHunt.EggHunter eggHunter = Facepunch.Pool.Get<EggHunt.EggHunter>();
			eggHunter.displayName = topHunters[i].displayName;
			eggHunter.numEggs = topHunters[i].numEggs;
			eggHunter.playerID = topHunters[i].userid;
			info.msg.eggHunt.hunters.Add(eggHunter);
		}
	}
}
