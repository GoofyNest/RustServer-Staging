using ConVar;
using UnityEngine;

public class AttackEntity : HeldEntity
{
	[Header("Attack Entity")]
	public float deployDelay = 1f;

	public float repeatDelay = 0.5f;

	public float animationDelay;

	[Header("NPCUsage")]
	public float effectiveRange = 1f;

	public float npcDamageScale = 1f;

	public float attackLengthMin = -1f;

	public float attackLengthMax = -1f;

	public float attackSpacing;

	public float aiAimSwayOffset;

	public float aiAimCone;

	public bool aiOnlyInRange;

	public float CloseRangeAddition;

	public float MediumRangeAddition;

	public float LongRangeAddition;

	public bool CanUseAtMediumRange = true;

	public bool CanUseAtLongRange = true;

	public SoundDefinition[] reloadSounds;

	public SoundDefinition thirdPersonMeleeSound;

	[Header("Recoil Compensation")]
	public float recoilCompDelayOverride;

	public bool wantsRecoilComp;

	public bool showCrosshairOnTutorial;

	public bool noHeadshots;

	private EncryptedValue<float> nextAttackTime = float.NegativeInfinity;

	protected bool UsingInfiniteAmmoCheat
	{
		get
		{
			BasePlayer ownerPlayer = GetOwnerPlayer();
			if (ownerPlayer == null || (!ownerPlayer.IsAdmin && !ownerPlayer.IsDeveloper))
			{
				return false;
			}
			return ownerPlayer.GetInfoBool("player.infiniteammo", defaultVal: false);
		}
	}

	public float NextAttackTime => nextAttackTime;

	public virtual Vector3 GetInheritedVelocity(BasePlayer player, Vector3 direction)
	{
		return Vector3.zero;
	}

	public virtual float AmmoFraction()
	{
		return 0f;
	}

	public virtual bool CanReload()
	{
		return false;
	}

	public virtual bool ServerIsReloading()
	{
		return false;
	}

	public virtual void ServerReload()
	{
	}

	public virtual bool ServerTryReload(IAmmoContainer ammoSource)
	{
		return true;
	}

	public virtual void TopUpAmmo()
	{
	}

	public virtual Vector3 ModifyAIAim(Vector3 eulerInput, float swayModifier = 1f)
	{
		return eulerInput;
	}

	public virtual void GetAttackStats(HitInfo info)
	{
	}

	protected void StartAttackCooldownRaw(float cooldown)
	{
		nextAttackTime = UnityEngine.Time.time + cooldown;
	}

	protected void StartAttackCooldown(float cooldown)
	{
		nextAttackTime = CalculateCooldownTime(nextAttackTime, cooldown, catchup: true, unscaledTime: false);
	}

	public void ResetAttackCooldown()
	{
		nextAttackTime = float.NegativeInfinity;
	}

	public bool HasAttackCooldown()
	{
		return UnityEngine.Time.time < (float)nextAttackTime;
	}

	protected float GetAttackCooldown()
	{
		return Mathf.Max((float)nextAttackTime - UnityEngine.Time.time, 0f);
	}

	protected float GetAttackIdle()
	{
		return Mathf.Max(UnityEngine.Time.time - (float)nextAttackTime, 0f);
	}

	protected float CalculateCooldownTime(float nextTime, float cooldown, bool catchup, bool unscaledTime)
	{
		float num = (unscaledTime ? UnityEngine.Time.unscaledTime : UnityEngine.Time.time);
		float num2 = 0f;
		if (base.isServer)
		{
			BasePlayer ownerPlayer = GetOwnerPlayer();
			num2 += 0.1f;
			num2 += cooldown * 0.1f;
			num2 += (ownerPlayer ? ownerPlayer.desyncTimeClamped : 0.1f);
			num2 += Mathf.Max(UnityEngine.Time.deltaTime, UnityEngine.Time.smoothDeltaTime);
		}
		nextTime = ((nextTime < 0f) ? Mathf.Max(0f, num + cooldown - num2) : ((!(num - nextTime <= num2)) ? Mathf.Max(nextTime + cooldown, num + cooldown - num2) : Mathf.Min(nextTime + cooldown, num + cooldown)));
		return nextTime;
	}

	protected bool VerifyClientRPC(BasePlayer player)
	{
		if (player == null)
		{
			Debug.LogWarning("Received RPC from null player");
			return false;
		}
		BasePlayer ownerPlayer = GetOwnerPlayer();
		if (ownerPlayer == null)
		{
			AntiHack.Log(player, AntiHackType.AttackHack, "Owner not found (" + base.ShortPrefabName + ")");
			player.stats.combat.LogInvalid(player, this, "owner_missing");
			return false;
		}
		if (ownerPlayer != player)
		{
			AntiHack.Log(player, AntiHackType.AttackHack, "Player mismatch (" + base.ShortPrefabName + ")");
			player.stats.combat.LogInvalid(player, this, "player_mismatch");
			return false;
		}
		if (player.IsDead())
		{
			AntiHack.Log(player, AntiHackType.AttackHack, "Player dead (" + base.ShortPrefabName + ")");
			player.stats.combat.LogInvalid(player, this, "player_dead");
			return false;
		}
		if (player.IsWounded())
		{
			AntiHack.Log(player, AntiHackType.AttackHack, "Player down (" + base.ShortPrefabName + ")");
			player.stats.combat.LogInvalid(player, this, "player_down");
			return false;
		}
		if (player.IsSleeping())
		{
			AntiHack.Log(player, AntiHackType.AttackHack, "Player sleeping (" + base.ShortPrefabName + ")");
			player.stats.combat.LogInvalid(player, this, "player_sleeping");
			return false;
		}
		if (player.desyncTimeRaw > ConVar.AntiHack.maxdesync)
		{
			AntiHack.Log(player, AntiHackType.AttackHack, "Player stalled (" + base.ShortPrefabName + " with " + player.desyncTimeRaw + "s)");
			player.stats.combat.LogInvalid(player, this, "player_stalled");
			return false;
		}
		Item ownerItem = GetOwnerItem();
		if (ownerItem == null)
		{
			AntiHack.Log(player, AntiHackType.AttackHack, "Item not found (" + base.ShortPrefabName + ")");
			player.stats.combat.LogInvalid(player, this, "item_missing");
			return false;
		}
		if (ownerItem.isBroken)
		{
			AntiHack.Log(player, AntiHackType.AttackHack, "Item broken (" + base.ShortPrefabName + ")");
			player.stats.combat.LogInvalid(player, this, "item_broken");
			return false;
		}
		return true;
	}

	protected virtual bool VerifyClientAttack(BasePlayer player)
	{
		if (!VerifyClientRPC(player))
		{
			return false;
		}
		if (HasAttackCooldown())
		{
			AntiHack.Log(player, AntiHackType.CooldownHack, "T-" + GetAttackCooldown() + "s (" + base.ShortPrefabName + ")");
			player.stats.combat.LogInvalid(player, this, "attack_cooldown");
			return false;
		}
		return true;
	}

	protected bool ValidateEyePos(BasePlayer player, Vector3 eyePos, bool checkLineOfSight = true)
	{
		bool flag = true;
		if (eyePos.IsNaNOrInfinity())
		{
			string shortPrefabName = base.ShortPrefabName;
			AntiHack.Log(player, AntiHackType.EyeHack, "Contains NaN (" + shortPrefabName + ")");
			player.stats.combat.LogInvalid(player, this, "eye_nan");
			flag = false;
		}
		if (ConVar.AntiHack.eye_protection > 0)
		{
			if (ConVar.AntiHack.eye_protection >= 1)
			{
				float num = player.GetParentVelocity().magnitude + player.GetMountVelocity().magnitude;
				float num2 = ((((player.HasParent() || player.isMounted) ? ConVar.AntiHack.eye_distance_parented_mounted_forgiveness : 0f) + player.estimatedSpeed > 0f) ? ConVar.AntiHack.eye_forgiveness : 0f);
				float num3 = num + num2;
				float num4 = player.tickHistory.Distance(player, eyePos);
				if (num4 > num3)
				{
					string shortPrefabName2 = base.ShortPrefabName;
					AntiHack.Log(player, AntiHackType.EyeHack, "Distance (" + shortPrefabName2 + " on attack with " + num4 + "m > " + num3 + "m)");
					player.stats.combat.LogInvalid(player, this, "eye_distance");
					flag = false;
				}
			}
			if (ConVar.AntiHack.eye_protection >= 3)
			{
				float num5 = Mathf.Abs(player.GetMountVelocity().y + player.GetParentVelocity().y) + player.GetJumpHeight();
				float num6 = Mathf.Abs(player.eyes.position.y - eyePos.y);
				if (num6 > num5)
				{
					string shortPrefabName3 = base.ShortPrefabName;
					AntiHack.Log(player, AntiHackType.EyeHack, "Altitude (" + shortPrefabName3 + " on attack with " + num6 + "m > " + num5 + "m)");
					player.stats.combat.LogInvalid(player, this, "eye_altitude");
					flag = false;
				}
			}
			if (checkLineOfSight)
			{
				int num7 = 2162688;
				if (ConVar.AntiHack.eye_terraincheck)
				{
					num7 |= 0x800000;
				}
				if (ConVar.AntiHack.eye_vehiclecheck)
				{
					num7 |= 0x8000000;
				}
				if (ConVar.AntiHack.eye_protection >= 2)
				{
					Vector3 center = player.eyes.center;
					Vector3 position = player.eyes.position;
					Vector3 vector = eyePos;
					if (!GamePhysics.LineOfSightRadius(center, position, num7, ConVar.AntiHack.eye_losradius) || !GamePhysics.LineOfSightRadius(position, vector, num7, ConVar.AntiHack.eye_losradius))
					{
						string shortPrefabName4 = base.ShortPrefabName;
						string[] obj = new string[8] { "Line of sight (", shortPrefabName4, " on attack) ", null, null, null, null, null };
						Vector3 vector2 = center;
						obj[3] = vector2.ToString();
						obj[4] = " ";
						vector2 = position;
						obj[5] = vector2.ToString();
						obj[6] = " ";
						vector2 = vector;
						obj[7] = vector2.ToString();
						AntiHack.Log(player, AntiHackType.EyeHack, string.Concat(obj));
						player.stats.combat.LogInvalid(player, this, "eye_los");
						flag = false;
					}
				}
				if (ConVar.AntiHack.eye_protection >= 4 && !player.HasParent())
				{
					Vector3 position2 = player.eyes.position;
					Vector3 vector3 = eyePos;
					float num8 = Vector3.Distance(position2, vector3);
					Collider col;
					if (num8 > ConVar.AntiHack.eye_noclip_cutoff)
					{
						if (AntiHack.TestNoClipping(player, position2, vector3, player.NoClipRadius(ConVar.AntiHack.eye_noclip_margin), ConVar.AntiHack.eye_noclip_backtracking, out col))
						{
							string shortPrefabName5 = base.ShortPrefabName;
							string[] obj2 = new string[6] { "NoClip (", shortPrefabName5, " on attack) ", null, null, null };
							Vector3 vector2 = position2;
							obj2[3] = vector2.ToString();
							obj2[4] = " ";
							vector2 = vector3;
							obj2[5] = vector2.ToString();
							AntiHack.Log(player, AntiHackType.EyeHack, string.Concat(obj2));
							player.stats.combat.LogInvalid(player, this, "eye_noclip");
							flag = false;
						}
					}
					else if (num8 > 0.01f && AntiHack.TestNoClipping(player, position2, vector3, 0.1f, ConVar.AntiHack.eye_noclip_backtracking, out col))
					{
						string shortPrefabName6 = base.ShortPrefabName;
						string[] obj3 = new string[6] { "NoClip (", shortPrefabName6, " on attack) ", null, null, null };
						Vector3 vector2 = position2;
						obj3[3] = vector2.ToString();
						obj3[4] = " ";
						vector2 = vector3;
						obj3[5] = vector2.ToString();
						AntiHack.Log(player, AntiHackType.EyeHack, string.Concat(obj3));
						player.stats.combat.LogInvalid(player, this, "eye_noclip");
						flag = false;
					}
				}
			}
			if (!flag)
			{
				AntiHack.AddViolation(player, AntiHackType.EyeHack, ConVar.AntiHack.eye_penalty);
			}
			else if (ConVar.AntiHack.eye_protection >= 5 && !player.HasParent() && !player.isMounted)
			{
				player.eyeHistory.PushBack(eyePos);
			}
		}
		return flag;
	}

	public override void OnHeldChanged()
	{
		base.OnHeldChanged();
		StartAttackCooldown(deployDelay * 0.9f);
	}
}
