#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Facepunch;
using Facepunch.Rust;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

public class BaseCombatEntity : BaseEntity
{
	public enum LifeState
	{
		Alive,
		Dead
	}

	[Serializable]
	public enum Faction
	{
		Default,
		Player,
		Bandit,
		Scientist,
		Horror
	}

	[Serializable]
	public struct Pickup
	{
		public bool enabled;

		[ItemSelector(ItemCategory.All)]
		public ItemDefinition itemTarget;

		public int itemCount;

		[Tooltip("Should we set the condition of the item based on the health of the picked up entity")]
		public bool setConditionFromHealth;

		[Tooltip("How much to reduce the item condition when picking up")]
		public float subtractCondition;

		[Tooltip("Must have building access to pick up")]
		public bool requireBuildingPrivilege;

		[Tooltip("Must have hammer equipped to pick up")]
		public bool requireHammer;

		[Tooltip("Inventory Must be empty (if applicable) to be picked up")]
		public bool requireEmptyInv;

		[Tooltip("If set, pickup will take this long in seconds")]
		public float overridePickupTime;
	}

	[Serializable]
	public struct Repair
	{
		public bool enabled;

		[ItemSelector(ItemCategory.All)]
		public ItemDefinition itemTarget;

		[ItemSelector(ItemCategory.All)]
		public ItemDefinition ignoreForRepair;

		public GameObjectRef repairEffect;

		public GameObjectRef repairFullEffect;

		public GameObjectRef repairFailedEffect;
	}

	public enum ActionVolume
	{
		Quiet,
		Normal,
		Loud
	}

	[Header("BaseCombatEntity")]
	public SkeletonProperties skeletonProperties;

	public ProtectionProperties baseProtection;

	public float startHealth;

	public Pickup pickup;

	public Repair repair;

	public bool ShowHealthInfo = true;

	[ReadOnly]
	public LifeState lifestate;

	public bool sendsHitNotification;

	public bool sendsMeleeHitNotification = true;

	public bool markAttackerHostile = true;

	protected float _health;

	protected float _maxHealth = 100f;

	public Faction faction;

	[NonSerialized]
	public float lastAttackedTime = float.NegativeInfinity;

	[NonSerialized]
	public float lastDealtDamageTime = float.NegativeInfinity;

	private int lastNotifyFrame;

	private const float MAX_HEALTH_REPAIR = 50f;

	public static readonly Translate.Phrase RecentlyDamagedError = new Translate.Phrase("error_recentlydamaged", "Recently damaged, repairable in {0} seconds");

	public static readonly Translate.Phrase NotDamagedError = new Translate.Phrase("error_notdamaged", "Not damaged");

	[NonSerialized]
	public DamageType lastDamage;

	[NonSerialized]
	public BaseEntity lastAttacker;

	[NonSerialized]
	public BaseEntity lastDealtDamageTo;

	[NonSerialized]
	public bool ResetLifeStateOnSpawn = true;

	protected DirectionProperties[] propDirection;

	protected float unHostileTime;

	private float lastNoiseTime;

	public Vector3 LastAttackedDir { get; set; }

	public float SecondsSinceAttacked => UnityEngine.Time.time - lastAttackedTime;

	public float SecondsSinceDealtDamage => UnityEngine.Time.time - lastDealtDamageTime;

	public float healthFraction => Health() / MaxHealth();

	public float health
	{
		get
		{
			return _health;
		}
		set
		{
			float num = _health;
			_health = Mathf.Clamp(value, 0f, MaxHealth());
			if (base.isServer && _health != num)
			{
				OnHealthChanged(num, _health);
			}
		}
	}

	public float TimeSinceLastNoise => UnityEngine.Time.time - lastNoiseTime;

	public ActionVolume LastNoiseVolume { get; private set; }

	public Vector3 LastNoisePosition { get; private set; }

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("BaseCombatEntity.OnRpcMessage"))
		{
			if (rpc == 1191093595 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_PickupStart ");
				}
				using (TimeWarning.New("RPC_PickupStart"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(1191093595u, "RPC_PickupStart", this, player, 3f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage rpc2 = rPCMessage;
							RPC_PickupStart(rpc2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_PickupStart");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public virtual bool IsDead()
	{
		return lifestate == LifeState.Dead;
	}

	public virtual bool IsAlive()
	{
		return lifestate == LifeState.Alive;
	}

	public Faction GetFaction()
	{
		return faction;
	}

	public virtual bool IsFriendly(BaseCombatEntity other)
	{
		return false;
	}

	public override void ResetState()
	{
		base.ResetState();
		health = MaxHealth();
		if (base.isServer)
		{
			lastAttackedTime = float.NegativeInfinity;
			lastDealtDamageTime = float.NegativeInfinity;
		}
	}

	public override void DestroyShared()
	{
		base.DestroyShared();
		if (base.isServer)
		{
			UpdateSurroundings();
		}
	}

	public virtual float GetThreatLevel()
	{
		return 0f;
	}

	public override float PenetrationResistance(HitInfo info)
	{
		if (!baseProtection)
		{
			return 100f;
		}
		return baseProtection.density;
	}

	public virtual void ScaleDamage(HitInfo info)
	{
		if (info.UseProtection && baseProtection != null)
		{
			baseProtection.Scale(info.damageTypes);
		}
	}

	public HitArea SkeletonLookup(uint boneID)
	{
		if (skeletonProperties == null)
		{
			return (HitArea)(-1);
		}
		return skeletonProperties.FindBone(boneID)?.area ?? ((HitArea)(-1));
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.baseCombat = Facepunch.Pool.Get<BaseCombat>();
		info.msg.baseCombat.state = (int)lifestate;
		info.msg.baseCombat.health = Health();
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		if (Health() > MaxHealth())
		{
			health = MaxHealth();
		}
		if (float.IsNaN(Health()))
		{
			health = MaxHealth();
		}
	}

	public void SetJustAttacked()
	{
		lastAttackedTime = UnityEngine.Time.time;
	}

	public override void Load(LoadInfo info)
	{
		if (base.isServer)
		{
			lifestate = LifeState.Alive;
		}
		if (info.msg.baseCombat != null)
		{
			lifestate = (LifeState)info.msg.baseCombat.state;
			_health = info.msg.baseCombat.health;
		}
		base.Load(info);
	}

	public override float Health()
	{
		return _health;
	}

	public override float MaxHealth()
	{
		return _maxHealth;
	}

	public virtual float StartHealth()
	{
		return startHealth;
	}

	public virtual float StartMaxHealth()
	{
		return StartHealth();
	}

	public void SetMaxHealth(float newMax)
	{
		_maxHealth = newMax;
		_health = Mathf.Min(_health, newMax);
	}

	public void DoHitNotify(HitInfo info)
	{
		using (TimeWarning.New("DoHitNotify"))
		{
			if (sendsHitNotification && !(info.Initiator == null) && info.Initiator is BasePlayer && !(this == info.Initiator) && (!info.isHeadshot || !(info.HitEntity is BasePlayer)) && UnityEngine.Time.frameCount != lastNotifyFrame)
			{
				lastNotifyFrame = UnityEngine.Time.frameCount;
				bool flag = info.Weapon is BaseMelee;
				if (base.isServer && (!flag || sendsMeleeHitNotification))
				{
					bool arg = info.Initiator.net.connection == info.Predicted;
					ClientRPC(RpcTarget.PlayerAndSpectators("HitNotify", info.Initiator as BasePlayer), arg);
				}
			}
		}
	}

	public override void OnAttacked(HitInfo info)
	{
		using (TimeWarning.New("BaseCombatEntity.OnAttacked"))
		{
			if (!IsDead())
			{
				DoHitNotify(info);
			}
			if (base.isServer)
			{
				Hurt(info);
			}
		}
		base.OnAttacked(info);
	}

	protected virtual int GetPickupCount()
	{
		return pickup.itemCount;
	}

	public virtual bool CanPickup(BasePlayer player)
	{
		if (pickup.enabled && (!pickup.requireBuildingPrivilege || player.CanBuild()) && (!pickup.requireHammer || player.IsHoldingEntity<Hammer>()))
		{
			if (player != null)
			{
				return !player.IsInTutorial;
			}
			return false;
		}
		return false;
	}

	public virtual void OnPickedUp(Item createdItem, BasePlayer player)
	{
	}

	public virtual void OnPickedUpPreItemMove(Item createdItem, BasePlayer player)
	{
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	private void RPC_PickupStart(RPCMessage rpc)
	{
		if (rpc.player.CanInteract() && CanPickup(rpc.player))
		{
			Item item = ItemManager.Create(pickup.itemTarget, GetPickupCount(), skinID);
			if (pickup.setConditionFromHealth && item.hasCondition)
			{
				item.conditionNormalized = Mathf.Clamp01(healthFraction - pickup.subtractCondition);
			}
			OnPickedUpPreItemMove(item, rpc.player);
			rpc.player.GiveItem(item, GiveItemReason.PickedUp);
			OnPickedUp(item, rpc.player);
			Analytics.Azure.OnEntityPickedUp(rpc.player, this);
			Kill();
		}
	}

	public virtual List<ItemAmount> BuildCost()
	{
		if (repair.itemTarget == null)
		{
			return null;
		}
		ItemBlueprint itemBlueprint = ItemManager.FindBlueprint(repair.itemTarget);
		if (itemBlueprint == null)
		{
			return null;
		}
		return itemBlueprint.ingredients;
	}

	public virtual float RepairCostFraction()
	{
		return 0.5f;
	}

	public List<ItemAmount> RepairCost(float healthMissingFraction)
	{
		List<ItemAmount> list = BuildCost();
		if (list == null)
		{
			return null;
		}
		List<ItemAmount> list2 = new List<ItemAmount>();
		foreach (ItemAmount item in list)
		{
			if (!(repair.ignoreForRepair != null) || item.itemDef.itemid != repair.ignoreForRepair.itemid)
			{
				list2.Add(new ItemAmount(item.itemDef, Mathf.Max(Mathf.RoundToInt(item.amount * RepairCostFraction() * healthMissingFraction), 1)));
			}
		}
		RepairBench.StripComponentRepairCost(list2, RepairCostFraction() * healthMissingFraction);
		return list2;
	}

	public virtual void OnRepair()
	{
		Effect.server.Run(repair.repairEffect.isValid ? repair.repairEffect.resourcePath : "assets/bundled/prefabs/fx/build/repair.prefab", this, 0u, Vector3.zero, Vector3.zero);
	}

	public virtual void OnRepairFinished()
	{
		Effect.server.Run(repair.repairFullEffect.isValid ? repair.repairFullEffect.resourcePath : "assets/bundled/prefabs/fx/build/repair_full.prefab", this, 0u, Vector3.zero, Vector3.zero);
	}

	public virtual void OnRepairFailed(BasePlayer player, Translate.Phrase reason, params string[] args)
	{
		Effect.server.Run(repair.repairFailedEffect.isValid ? repair.repairFailedEffect.resourcePath : "assets/bundled/prefabs/fx/build/repair_failed.prefab", this, 0u, Vector3.zero, Vector3.zero);
		if (player != null && !string.IsNullOrEmpty(reason.token))
		{
			player.ShowToast(GameTip.Styles.Error, reason, overlay: false, args);
		}
	}

	public virtual void OnRepairFailedResources(BasePlayer player, List<ItemAmount> requirements)
	{
		Effect.server.Run(repair.repairFailedEffect.isValid ? repair.repairFailedEffect.resourcePath : "assets/bundled/prefabs/fx/build/repair_failed.prefab", this, 0u, Vector3.zero, Vector3.zero);
		if (player != null)
		{
			using (ItemAmountList arg = ItemAmount.SerialiseList(requirements))
			{
				player.ClientRPC(RpcTarget.Player("Client_OnRepairFailedResources", player), arg);
			}
		}
	}

	public virtual void DoRepair(BasePlayer player)
	{
		if (!repair.enabled)
		{
			return;
		}
		float num = 30f;
		if (player.IsInCreativeMode && Creative.freeRepair)
		{
			num = 0f;
		}
		if (SecondsSinceAttacked <= num)
		{
			OnRepairFailed(player, RecentlyDamagedError, (num - SecondsSinceAttacked).ToString("N0"));
			return;
		}
		float num2 = MaxHealth() - Health();
		float num3 = num2 / MaxHealth();
		if (num2 <= 0f || num3 <= 0f)
		{
			OnRepairFailed(player, NotDamagedError);
			return;
		}
		List<ItemAmount> list = RepairCost(num3);
		if (list == null)
		{
			return;
		}
		float num4 = list.Sum((ItemAmount x) => x.amount);
		float healthBefore = health;
		if (player.IsInCreativeMode && Creative.freeRepair)
		{
			num4 = 0f;
		}
		if (num4 > 0f)
		{
			float num5 = list.Min((ItemAmount x) => Mathf.Clamp01((float)player.inventory.GetAmount(x.itemid) / x.amount));
			if (float.IsNaN(num5))
			{
				num5 = 0f;
			}
			num5 = Mathf.Min(num5, 50f / num2);
			if (num5 <= 0f)
			{
				OnRepairFailedResources(player, list);
				return;
			}
			int num6 = 0;
			foreach (ItemAmount item in list)
			{
				int amount = Mathf.CeilToInt(num5 * item.amount);
				int num7 = player.inventory.Take(null, item.itemid, amount);
				Analytics.Azure.LogResource(Analytics.Azure.ResourceMode.Consumed, "repair_entity", item.itemDef.shortname, num7, this, null, safezone: false, null, player.userID);
				if (num7 > 0)
				{
					num6 += num7;
					player.Command("note.inv", item.itemid, num7 * -1);
				}
			}
			float num8 = (float)num6 / num4;
			health += num2 * num8;
			SendNetworkUpdate();
		}
		else
		{
			health += num2;
			SendNetworkUpdate();
		}
		Analytics.Azure.OnEntityRepaired(player, this, healthBefore, health);
		if (Health() >= MaxHealth())
		{
			OnRepairFinished();
		}
		else
		{
			OnRepair();
		}
	}

	public virtual void InitializeHealth(float newhealth, float newmax)
	{
		_maxHealth = newmax;
		_health = newhealth;
		lifestate = LifeState.Alive;
	}

	public override void ServerInit()
	{
		propDirection = PrefabAttribute.server.FindAll<DirectionProperties>(prefabID);
		if (ResetLifeStateOnSpawn)
		{
			InitializeHealth(StartHealth(), StartMaxHealth());
			lifestate = LifeState.Alive;
		}
		base.ServerInit();
	}

	public virtual void OnHealthChanged(float oldvalue, float newvalue)
	{
	}

	public void Hurt(float amount)
	{
		Hurt(Mathf.Abs(amount), DamageType.Generic);
	}

	public void Hurt(float amount, DamageType type, BaseEntity attacker = null, bool useProtection = true)
	{
		using (TimeWarning.New("Hurt"))
		{
			HitInfo obj = Facepunch.Pool.Get<HitInfo>();
			obj.Init(attacker, this, type, amount, base.transform.position);
			obj.UseProtection = useProtection;
			Hurt(obj);
			Facepunch.Pool.Free(ref obj);
		}
	}

	public virtual void Hurt(HitInfo info)
	{
		Assert.IsTrue(base.isServer, "This should be called serverside only");
		if (IsDead() || IsTransferProtected())
		{
			return;
		}
		using (TimeWarning.New("Hurt( HitInfo )", 50))
		{
			float num = health;
			ScaleDamage(info);
			if (info.PointStart != Vector3.zero)
			{
				for (int i = 0; i < propDirection.Length; i++)
				{
					if (!(propDirection[i].extraProtection == null) && !propDirection[i].IsWeakspot(base.transform, info))
					{
						propDirection[i].extraProtection.Scale(info.damageTypes);
					}
				}
			}
			info.damageTypes.Scale(DamageType.Arrow, ConVar.Server.arrowdamage);
			info.damageTypes.Scale(DamageType.Bullet, ConVar.Server.bulletdamage);
			info.damageTypes.Scale(DamageType.Slash, ConVar.Server.meleedamage);
			info.damageTypes.Scale(DamageType.Blunt, ConVar.Server.meleedamage);
			info.damageTypes.Scale(DamageType.Stab, ConVar.Server.meleedamage);
			info.damageTypes.Scale(DamageType.Bleeding, ConVar.Server.bleedingdamage);
			if (!(this is BasePlayer))
			{
				info.damageTypes.Scale(DamageType.Fun_Water, 0f);
			}
			DebugHurt(info);
			float num2 = info.damageTypes.Total();
			health = num - num2;
			SendNetworkUpdate();
			LogEntry(RustLog.EntryType.Combat, 2, "hurt {0}/{1} - {2} health left", info.damageTypes.GetMajorityDamageType(), num2, health.ToString("0"));
			lastDamage = info.damageTypes.GetMajorityDamageType();
			lastAttacker = info.Initiator;
			if (lastAttacker != null)
			{
				BaseCombatEntity baseCombatEntity = lastAttacker as BaseCombatEntity;
				if (baseCombatEntity != null)
				{
					baseCombatEntity.lastDealtDamageTime = UnityEngine.Time.time;
					baseCombatEntity.lastDealtDamageTo = this;
				}
				if (this.IsValid() && lastAttacker is BasePlayer basePlayer)
				{
					basePlayer.ProcessMissionEvent(BaseMission.MissionEventType.HURT_ENTITY, net.ID, num2);
				}
			}
			BaseCombatEntity baseCombatEntity2 = lastAttacker as BaseCombatEntity;
			if (markAttackerHostile && baseCombatEntity2 != null && baseCombatEntity2 != this)
			{
				baseCombatEntity2.MarkHostileFor();
			}
			if (lastDamage.IsConsideredAnAttack())
			{
				SetJustAttacked();
				if (lastAttacker != null)
				{
					LastAttackedDir = (lastAttacker.transform.position - base.transform.position).normalized;
				}
			}
			bool flag = Health() <= 0f;
			Analytics.Azure.OnEntityTakeDamage(info, flag);
			if (flag)
			{
				Die(info);
			}
			BasePlayer initiatorPlayer = info.InitiatorPlayer;
			if ((bool)initiatorPlayer)
			{
				if (IsDead())
				{
					initiatorPlayer.stats.combat.LogAttack(info, "killed", num);
				}
				else
				{
					initiatorPlayer.stats.combat.LogAttack(info, "", num);
				}
			}
		}
	}

	public virtual bool IsHostile()
	{
		return unHostileTime > UnityEngine.Time.realtimeSinceStartup;
	}

	public virtual void MarkHostileFor(float duration = 60f)
	{
		float b = UnityEngine.Time.realtimeSinceStartup + duration;
		unHostileTime = Mathf.Max(unHostileTime, b);
	}

	private void DebugHurt(HitInfo info)
	{
		if (!ConVar.Vis.damage)
		{
			return;
		}
		if (info.PointStart != info.PointEnd)
		{
			ConsoleNetwork.BroadcastToAllClients("ddraw.arrow", 60, Color.cyan, info.PointStart, info.PointEnd, 0.1f);
			ConsoleNetwork.BroadcastToAllClients("ddraw.sphere", 60, Color.cyan, info.HitPositionWorld, 0.01f);
		}
		string text = "";
		for (int i = 0; i < info.damageTypes.types.Length; i++)
		{
			float num = info.damageTypes.types[i];
			if (num != 0f)
			{
				string[] obj = new string[5] { text, " ", null, null, null };
				DamageType damageType = (DamageType)i;
				obj[2] = damageType.ToString().PadRight(10);
				obj[3] = num.ToString("0.00");
				obj[4] = "\n";
				text = string.Concat(obj);
			}
		}
		string text2 = "<color=lightblue>Damage:</color>".PadRight(10) + info.damageTypes.Total().ToString("0.00") + "\n<color=lightblue>Health:</color>".PadRight(10) + health.ToString("0.00") + " / " + ((health - info.damageTypes.Total() <= 0f) ? "<color=red>" : "<color=green>") + (health - info.damageTypes.Total()).ToString("0.00") + "</color>" + "\n<color=lightblue>HitEnt:</color>".PadRight(10) + this?.ToString() + "\n<color=lightblue>HitBone:</color>".PadRight(10) + info.boneName + "\n<color=lightblue>Attacker:</color>".PadRight(10) + info.Initiator?.ToString() + "\n<color=lightblue>WeaponPrefab:</color>".PadRight(10) + info.WeaponPrefab?.ToString() + "\n<color=lightblue>Damages:</color>\n" + text;
		ConsoleNetwork.BroadcastToAllClients("ddraw.text", 60, Color.white, info.HitPositionWorld, text2);
	}

	public void SetHealth(float hp)
	{
		if (health != hp)
		{
			health = hp;
			SendNetworkUpdate();
		}
	}

	public virtual void Heal(float amount)
	{
		LogEntry(RustLog.EntryType.Combat, 2, "healed {0}", amount);
		health = _health + amount;
		SendNetworkUpdate();
	}

	public virtual void OnKilled(HitInfo info)
	{
		Kill(DestroyMode.Gib);
	}

	public virtual void Die(HitInfo info = null)
	{
		if (IsDead())
		{
			return;
		}
		LogEntry(RustLog.EntryType.Combat, 2, "died");
		health = 0f;
		lifestate = LifeState.Dead;
		if (info != null && (bool)info.InitiatorPlayer)
		{
			BasePlayer initiatorPlayer = info.InitiatorPlayer;
			if (initiatorPlayer != null && initiatorPlayer.GetActiveMission() != -1 && !initiatorPlayer.IsNpc)
			{
				initiatorPlayer.ProcessMissionEvent(BaseMission.MissionEventType.KILL_ENTITY, prefabID, 1f);
			}
		}
		using (TimeWarning.New("OnKilled"))
		{
			OnKilled(info);
		}
	}

	public void DieInstantly()
	{
		if (!IsDead())
		{
			LogEntry(RustLog.EntryType.Combat, 2, "died");
			health = 0f;
			lifestate = LifeState.Dead;
			OnKilled(null);
		}
	}

	public void UpdateSurroundings()
	{
		BaseEntity baseEntity = GetParentEntity();
		if (baseEntity != null && baseEntity.GetWorldVelocity().sqrMagnitude > 5f)
		{
			StabilityEntity.UpdateSurroundingsQueue.NotifyNeighbours(WorldSpaceBounds().ToBounds());
		}
		else
		{
			StabilityEntity.updateSurroundingsQueue.Add(WorldSpaceBounds().ToBounds());
		}
	}

	public void MakeNoise(Vector3 position, ActionVolume loudness)
	{
		LastNoisePosition = position;
		LastNoiseVolume = loudness;
		lastNoiseTime = UnityEngine.Time.time;
	}

	public bool CanLastNoiseBeHeard(Vector3 listenPosition, float listenRange)
	{
		if (listenRange <= 0f)
		{
			return false;
		}
		return Vector3.Distance(listenPosition, LastNoisePosition) <= listenRange;
	}
}
