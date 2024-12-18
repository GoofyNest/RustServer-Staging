using System;
using System.Collections;
using System.IO;
using ProtoBuf;
using UnityEngine;

public class HumanNPC : NPCPlayer, IAISenses, IAIAttack, IThinker
{
	[Header("LOS")]
	public int AdditionalLosBlockingLayer;

	[Header("Loot")]
	public LootContainer.LootSpawnSlot[] LootSpawnSlots;

	[Header("Damage")]
	public float aimConeScale = 2f;

	public float lastDismountTime;

	[NonSerialized]
	protected bool lightsOn;

	private float nextZoneSearchTime;

	private AIInformationZone cachedInfoZone;

	private float targetAimedDuration;

	private float lastAimSetTime;

	private Vector3 aimOverridePosition = Vector3.zero;

	public ScientistBrain Brain { get; private set; }

	public override float StartHealth()
	{
		return startHealth;
	}

	public override float StartMaxHealth()
	{
		return startHealth;
	}

	public override float MaxHealth()
	{
		return startHealth;
	}

	public override bool IsLoadBalanced()
	{
		return true;
	}

	public override void ServerInit()
	{
		base.ServerInit();
		Brain = GetComponent<ScientistBrain>();
		if (!base.isClient)
		{
			AIThinkManager.Add(this);
		}
	}

	internal override void DoServerDestroy()
	{
		AIThinkManager.Remove(this);
		base.DoServerDestroy();
	}

	public void LightCheck()
	{
		if ((TOD_Sky.Instance.IsNight && !lightsOn) || (TOD_Sky.Instance.IsDay && lightsOn))
		{
			LightToggle();
			lightsOn = !lightsOn;
		}
	}

	public override float GetAimConeScale()
	{
		return aimConeScale;
	}

	public override void EquipWeapon(bool skipDeployDelay = false)
	{
		base.EquipWeapon(skipDeployDelay);
	}

	public override void DismountObject()
	{
		base.DismountObject();
		lastDismountTime = Time.time;
	}

	public bool RecentlyDismounted()
	{
		return Time.time < lastDismountTime + 10f;
	}

	public virtual float GetIdealDistanceFromTarget()
	{
		return Mathf.Max(5f, EngagementRange() * 0.75f);
	}

	public AIInformationZone GetInformationZone(Vector3 pos)
	{
		if (VirtualInfoZone != null)
		{
			return VirtualInfoZone;
		}
		if (cachedInfoZone == null || Time.time > nextZoneSearchTime)
		{
			cachedInfoZone = AIInformationZone.GetForPoint(pos);
			nextZoneSearchTime = Time.time + 5f;
		}
		return cachedInfoZone;
	}

	public float EngagementRange()
	{
		AttackEntity attackEntity = GetAttackEntity();
		if ((bool)attackEntity)
		{
			return attackEntity.effectiveRange * (attackEntity.aiOnlyInRange ? 1f : 2f) * Brain.AttackRangeMultiplier;
		}
		return Brain.SenseRange;
	}

	public void SetDucked(bool flag)
	{
		modelState.ducked = flag;
		SendNetworkUpdate();
	}

	public virtual void TryThink()
	{
		ServerThink_Internal();
	}

	public override void ServerThink(float delta)
	{
		base.ServerThink(delta);
		if (Brain.ShouldServerThink())
		{
			Brain.DoThink();
		}
	}

	public void TickAttack(float delta, BaseCombatEntity target, bool targetIsLOS)
	{
		if (target == null)
		{
			return;
		}
		float num = Vector3.Dot(base.eyes.BodyForward(), (target.CenterPoint() - base.eyes.position).normalized);
		if (targetIsLOS)
		{
			if (num > 0.2f)
			{
				targetAimedDuration += delta;
			}
		}
		else
		{
			if (num < 0.5f)
			{
				targetAimedDuration = 0f;
			}
			CancelBurst();
		}
		if (targetAimedDuration >= 0.2f && targetIsLOS)
		{
			bool flag = false;
			float dist = 0f;
			if ((object)this != null)
			{
				flag = ((IAIAttack)this).IsTargetInRange((BaseEntity)target, out dist);
			}
			else
			{
				AttackEntity attackEntity = GetAttackEntity();
				if ((bool)attackEntity)
				{
					dist = ((target != null) ? Vector3.Distance(base.transform.position, target.transform.position) : (-1f));
					flag = dist < attackEntity.effectiveRange * (attackEntity.aiOnlyInRange ? 1f : 2f);
				}
			}
			if (flag)
			{
				ShotTest(dist);
			}
		}
		else
		{
			CancelBurst();
		}
	}

	public override void Hurt(HitInfo info)
	{
		if (base.isMounted)
		{
			info.damageTypes.ScaleAll(0.1f);
		}
		base.Hurt(info);
		BaseEntity initiator = info.Initiator;
		if (initiator != null && !initiator.EqualNetID(this))
		{
			Brain.Senses.Memory.SetKnown(initiator, this, null);
		}
	}

	public float GetAimSwayScalar()
	{
		return 1f - Mathf.InverseLerp(1f, 3f, Time.time - lastGunShotTime);
	}

	public override Vector3 GetAimDirection()
	{
		if (Brain != null && Brain.Navigator != null && Brain.Navigator.IsOverridingFacingDirection)
		{
			return Brain.Navigator.FacingDirectionOverride;
		}
		return base.GetAimDirection();
	}

	public override void SetAimDirection(Vector3 newAim)
	{
		if (newAim == Vector3.zero)
		{
			return;
		}
		float num = Time.time - lastAimSetTime;
		lastAimSetTime = Time.time;
		AttackEntity attackEntity = GetAttackEntity();
		if ((bool)attackEntity)
		{
			newAim = attackEntity.ModifyAIAim(newAim, GetAimSwayScalar());
		}
		if (base.isMounted)
		{
			BaseMountable baseMountable = GetMounted();
			Vector3 eulerAngles = baseMountable.transform.eulerAngles;
			Quaternion quaternion = Quaternion.Euler(Quaternion.LookRotation(newAim, baseMountable.transform.up).eulerAngles);
			Vector3 eulerAngles2 = Quaternion.LookRotation(base.transform.InverseTransformDirection(quaternion * Vector3.forward), base.transform.up).eulerAngles;
			eulerAngles2 = BaseMountable.ConvertVector(eulerAngles2);
			Quaternion quaternion2 = Quaternion.Euler(Mathf.Clamp(eulerAngles2.x, baseMountable.pitchClamp.x, baseMountable.pitchClamp.y), Mathf.Clamp(eulerAngles2.y, baseMountable.yawClamp.x, baseMountable.yawClamp.y), eulerAngles.z);
			newAim = BaseMountable.ConvertVector(Quaternion.LookRotation(base.transform.TransformDirection(quaternion2 * Vector3.forward), base.transform.up).eulerAngles);
		}
		else
		{
			BaseEntity baseEntity = GetParentEntity();
			if ((bool)baseEntity)
			{
				Vector3 vector = baseEntity.transform.InverseTransformDirection(newAim);
				Vector3 forward = new Vector3(newAim.x, vector.y, newAim.z);
				base.eyes.rotation = Quaternion.Lerp(base.eyes.rotation, Quaternion.LookRotation(forward, baseEntity.transform.up), num * 25f);
				viewAngles = base.eyes.bodyRotation.eulerAngles;
				ServerRotation = base.eyes.bodyRotation;
				return;
			}
		}
		base.eyes.rotation = (base.isMounted ? Quaternion.Slerp(base.eyes.rotation, Quaternion.Euler(newAim), num * 70f) : Quaternion.Lerp(base.eyes.rotation, Quaternion.LookRotation(newAim, base.transform.up), num * 25f));
		viewAngles = base.eyes.rotation.eulerAngles;
		ServerRotation = base.eyes.rotation;
	}

	public void SetStationaryAimPoint(Vector3 aimAt)
	{
		aimOverridePosition = aimAt;
	}

	public void ClearStationaryAimPoint()
	{
		aimOverridePosition = Vector3.zero;
	}

	public override bool ShouldDropActiveItem()
	{
		return false;
	}

	public override void AttackerInfo(PlayerLifeStory.DeathInfo info)
	{
		base.AttackerInfo(info);
		info.inflictorName = base.inventory.containerBelt.GetSlot(0).info.shortname;
		if (DeathIconOverride.isValid)
		{
			info.attackerName = Path.GetFileNameWithoutExtension(DeathIconOverride.resourcePath);
		}
		else
		{
			info.attackerName = base.ShortPrefabName;
		}
	}

	public bool IsThreat(BaseEntity entity)
	{
		return IsTarget(entity);
	}

	public bool IsTarget(BaseEntity entity)
	{
		if (entity is BasePlayer && !entity.IsNpc)
		{
			return true;
		}
		if (entity is BasePet)
		{
			return true;
		}
		if (entity is ScarecrowNPC)
		{
			return true;
		}
		return false;
	}

	public bool IsFriendly(BaseEntity entity)
	{
		if (entity == null)
		{
			return false;
		}
		return entity.prefabID == prefabID;
	}

	public bool CanAttack(BaseEntity entity)
	{
		return true;
	}

	public bool IsTargetInRange(BaseEntity entity, out float dist)
	{
		dist = Vector3.Distance(entity.transform.position, base.transform.position);
		return dist <= EngagementRange();
	}

	public bool CanSeeTarget(BaseEntity entity)
	{
		return CanSeeTarget(entity, Vector3.zero);
	}

	public bool CanSeeTarget(BaseEntity entity, Vector3 fromOffset)
	{
		BasePlayer basePlayer = entity as BasePlayer;
		if (basePlayer == null)
		{
			return true;
		}
		if (AdditionalLosBlockingLayer == 0)
		{
			return IsPlayerVisibleToUs(basePlayer, fromOffset, 1218519041);
		}
		return IsPlayerVisibleToUs(basePlayer, fromOffset, 0x48A12001 | (1 << AdditionalLosBlockingLayer));
	}

	public bool NeedsToReload()
	{
		return false;
	}

	public bool Reload()
	{
		return true;
	}

	public float CooldownDuration()
	{
		return 5f;
	}

	public bool IsOnCooldown()
	{
		return false;
	}

	public bool StartAttacking(BaseEntity entity)
	{
		return true;
	}

	public void StopAttacking()
	{
	}

	public float GetAmmoFraction()
	{
		return AmmoFractionRemaining();
	}

	public BaseEntity GetBestTarget()
	{
		BaseEntity result = null;
		float num = -1f;
		foreach (BaseEntity player in Brain.Senses.Players)
		{
			if (!(player == null) && !(player.Health() <= 0f))
			{
				float value = Vector3.Distance(player.transform.position, base.transform.position);
				float num2 = 1f - Mathf.InverseLerp(1f, Brain.SenseRange, value);
				float value2 = Vector3.Dot((player.transform.position - base.eyes.position).normalized, base.eyes.BodyForward());
				num2 += Mathf.InverseLerp(Brain.VisionCone, 1f, value2) / 2f;
				num2 += (Brain.Senses.Memory.IsLOS(player) ? 2f : 0f);
				if (num2 > num)
				{
					result = player;
					num = num2;
				}
			}
		}
		return result;
	}

	public void AttackTick(float delta, BaseEntity target, bool targetIsLOS)
	{
		BaseCombatEntity target2 = target as BaseCombatEntity;
		TickAttack(delta, target2, targetIsLOS);
	}

	public void UseHealingItem(Item item)
	{
		StartCoroutine(Heal(item));
	}

	private IEnumerator Heal(Item item)
	{
		UpdateActiveItem(item.uid);
		Item activeItem = GetActiveItem();
		MedicalTool heldItem = activeItem.GetHeldEntity() as MedicalTool;
		if (!(heldItem == null))
		{
			yield return new WaitForSeconds(1f);
			heldItem.ServerUse();
			Heal(MaxHealth());
			yield return new WaitForSeconds(2f);
			EquipWeapon();
		}
	}

	public Item FindHealingItem()
	{
		if (Brain == null)
		{
			return null;
		}
		if (!Brain.CanUseHealingItems)
		{
			return null;
		}
		if (base.inventory == null || base.inventory.containerBelt == null)
		{
			return null;
		}
		for (int i = 0; i < base.inventory.containerBelt.capacity; i++)
		{
			Item slot = base.inventory.containerBelt.GetSlot(i);
			if (slot != null && slot.amount > 1 && slot.GetHeldEntity() as MedicalTool != null)
			{
				return slot;
			}
		}
		return null;
	}

	protected override void ApplyLoot(NPCPlayerCorpse corpse)
	{
		base.ApplyLoot(corpse);
		if (LootSpawnSlots.Length == 0)
		{
			return;
		}
		LootContainer.LootSpawnSlot[] lootSpawnSlots = LootSpawnSlots;
		for (int i = 0; i < lootSpawnSlots.Length; i++)
		{
			LootContainer.LootSpawnSlot lootSpawnSlot = lootSpawnSlots[i];
			for (int j = 0; j < lootSpawnSlot.numberToSpawn; j++)
			{
				if ((string.IsNullOrEmpty(lootSpawnSlot.onlyWithLoadoutNamed) || lootSpawnSlot.onlyWithLoadoutNamed == GetLoadoutName()) && UnityEngine.Random.Range(0f, 1f) <= lootSpawnSlot.probability)
				{
					lootSpawnSlot.definition.SpawnIntoContainer(corpse.containers[0]);
				}
			}
		}
	}

	public override bool IsOnGround()
	{
		return true;
	}
}
