using ConVar;
using Facepunch;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;

public class PlayerMetabolism : BaseMetabolism<BasePlayer>
{
	public const float HotThreshold = 40f;

	public const float ColdThreshold = 5f;

	public const float OxygenHurtThreshold = 0.5f;

	public const float OxygenDepleteTime = 10f;

	public const float OxygenRefillTime = 1f;

	public MetabolismAttribute temperature = new MetabolismAttribute();

	public MetabolismAttribute poison = new MetabolismAttribute();

	public MetabolismAttribute radiation_level = new MetabolismAttribute();

	public MetabolismAttribute radiation_poison = new MetabolismAttribute();

	public MetabolismAttribute wetness = new MetabolismAttribute();

	public MetabolismAttribute dirtyness = new MetabolismAttribute();

	public MetabolismAttribute oxygen = new MetabolismAttribute();

	public MetabolismAttribute bleeding = new MetabolismAttribute();

	public MetabolismAttribute comfort = new MetabolismAttribute();

	public MetabolismAttribute pending_health = new MetabolismAttribute();

	public bool isDirty;

	private float lastConsumeTime;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("PlayerMetabolism.OnRpcMessage"))
		{
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void Reset()
	{
		base.Reset();
		poison.Reset();
		radiation_level.Reset();
		radiation_poison.Reset();
		temperature.Reset();
		oxygen.Reset();
		bleeding.Reset();
		wetness.Reset();
		dirtyness.Reset();
		comfort.Reset();
		pending_health.Reset();
		lastConsumeTime = float.NegativeInfinity;
		isDirty = true;
	}

	public override void ServerUpdate(BaseCombatEntity ownerEntity, float delta)
	{
		base.ServerUpdate(ownerEntity, delta);
		SendChangesToClient();
	}

	internal bool HasChanged()
	{
		bool flag = isDirty;
		flag = calories.HasChanged() || flag;
		flag = hydration.HasChanged() || flag;
		flag = heartrate.HasChanged() || flag;
		flag = poison.HasChanged() || flag;
		flag = radiation_level.HasChanged() || flag;
		flag = radiation_poison.HasChanged() || flag;
		flag = temperature.HasChanged() || flag;
		flag = wetness.HasChanged() || flag;
		flag = dirtyness.HasChanged() || flag;
		flag = comfort.HasChanged() || flag;
		return pending_health.HasChanged() || flag;
	}

	protected override void DoMetabolismDamage(BaseCombatEntity ownerEntity, float delta)
	{
		base.DoMetabolismDamage(ownerEntity, delta);
		if (temperature.value < -20f)
		{
			owner.Hurt(Mathf.InverseLerp(1f, -50f, temperature.value) * delta * 1f, DamageType.Cold);
		}
		else if (temperature.value < -10f)
		{
			owner.Hurt(Mathf.InverseLerp(1f, -50f, temperature.value) * delta * 0.3f, DamageType.Cold);
		}
		else if (temperature.value < 1f)
		{
			owner.Hurt(Mathf.InverseLerp(1f, -50f, temperature.value) * delta * 0.1f, DamageType.Cold);
		}
		if (temperature.value > 60f)
		{
			owner.Hurt(Mathf.InverseLerp(60f, 200f, temperature.value) * delta * 5f, DamageType.Heat);
		}
		if (oxygen.value < 0.5f)
		{
			owner.Hurt(Mathf.InverseLerp(0.5f, 0f, oxygen.value) * delta * 20f, DamageType.Drowned, null, useProtection: false);
		}
		if (!owner.IsGod() && bleeding.value > 0f)
		{
			float num = delta * (1f / 3f);
			owner.Hurt(num, DamageType.Bleeding);
			bleeding.Subtract(num);
		}
		if (!owner.IsGod() && poison.value > 0f)
		{
			owner.Hurt(poison.value * delta * 0.1f, DamageType.Poison);
		}
		if (ConVar.Server.radiation && radiation_poison.value > 0f)
		{
			float num2 = (1f + Mathf.Clamp01(radiation_poison.value / 25f) * 5f) * (delta / 5f);
			owner.Hurt(num2, DamageType.Radiation);
			radiation_poison.Subtract(num2);
		}
	}

	public bool SignificantBleeding()
	{
		return bleeding.value > 0f;
	}

	public void ForceUpdateWorkbenchFlags()
	{
		owner.InvalidateWorkbenchCache();
		UpdateWorkbenchFlags();
	}

	private void UpdateWorkbenchFlags()
	{
		float currentCraftLevel = owner.currentCraftLevel;
		owner.SetPlayerFlag(BasePlayer.PlayerFlags.Workbench1, currentCraftLevel == 1f);
		owner.SetPlayerFlag(BasePlayer.PlayerFlags.Workbench2, currentCraftLevel == 2f);
		owner.SetPlayerFlag(BasePlayer.PlayerFlags.Workbench3, currentCraftLevel == 3f);
	}

	protected override void RunMetabolism(BaseCombatEntity ownerEntity, float delta)
	{
		BaseGameMode activeGameMode = BaseGameMode.GetActiveGameMode(serverside: true);
		float num = owner.currentTemperature;
		float fTarget = owner.currentComfort;
		UpdateWorkbenchFlags();
		owner.SetPlayerFlag(BasePlayer.PlayerFlags.SafeZone, owner.InSafeZone());
		owner.SetPlayerFlag(BasePlayer.PlayerFlags.NoRespawnZone, owner.InNoRespawnZone());
		owner.SetPlayerFlag(BasePlayer.PlayerFlags.ModifyClan, Clan.editsRequireClanTable && owner.CanModifyClan());
		bool num2 = activeGameMode == null || activeGameMode.allowTemperature;
		if (owner.IsInTutorial)
		{
			num = 25f;
		}
		if (num2)
		{
			float num3 = num;
			num3 -= DeltaWet() * 34f;
			float num4 = Mathf.Clamp(owner.baseProtection.amounts[18] * 1.5f, -1f, 1f);
			float num5 = Mathf.InverseLerp(20f, -50f, num);
			float num6 = Mathf.InverseLerp(20f, 30f, num);
			num3 += num5 * 70f * num4;
			num3 += num6 * 10f * Mathf.Abs(num4);
			num3 += heartrate.value * 5f;
			temperature.MoveTowards(num3, delta * 5f);
		}
		else
		{
			temperature.value = 25f;
		}
		if (temperature.value >= 40f)
		{
			fTarget = 0f;
		}
		comfort.MoveTowards(fTarget, delta / 5f);
		float num7 = 0.6f + 0.4f * comfort.value;
		if (calories.value > 100f && owner.healthFraction < num7 && radiation_poison.Fraction() < 0.25f && owner.SecondsSinceAttacked > 10f && !SignificantBleeding() && temperature.value >= 10f && hydration.value > 40f)
		{
			float num8 = Mathf.InverseLerp(calories.min, calories.max, calories.value);
			float num9 = 5f;
			float num10 = num9 * owner.MaxHealth() * 0.8f / 600f;
			num10 += num10 * num8 * 0.5f;
			float num11 = num10 / num9;
			num11 += num11 * comfort.value * 6f;
			ownerEntity.Heal(num11 * delta);
			calories.Subtract(num10 * delta);
			hydration.Subtract(num10 * delta * 0.2f);
		}
		float num12 = owner.estimatedSpeed2D / owner.GetMaxSpeed() * 0.75f;
		float fTarget2 = Mathf.Clamp(0.05f + num12, 0f, 1f);
		heartrate.MoveTowards(fTarget2, delta * 0.1f);
		if (!owner.IsGod())
		{
			float num13 = heartrate.Fraction() * 0.375f;
			calories.MoveTowards(0f, delta * num13);
			float num14 = 1f / 120f;
			num14 += Mathf.InverseLerp(40f, 60f, temperature.value) * (1f / 12f);
			num14 += heartrate.value * (1f / 15f);
			hydration.MoveTowards(0f, delta * num14);
		}
		bool b = hydration.Fraction() <= 0f || radiation_poison.value >= 100f;
		owner.SetPlayerFlag(BasePlayer.PlayerFlags.NoSprint, b);
		if (temperature.value > 40f)
		{
			hydration.Add(Mathf.InverseLerp(40f, 200f, temperature.value) * delta * -1f);
		}
		if (temperature.value < 10f)
		{
			float num15 = Mathf.InverseLerp(20f, -100f, temperature.value);
			heartrate.MoveTowards(Mathf.Lerp(0.2f, 1f, num15), delta * 2f * num15);
		}
		float num16 = owner.AirFactor();
		float num17 = ((num16 > oxygen.value) ? 1f : 0.1f);
		oxygen.MoveTowards(num16, delta * num17);
		float f = 0f;
		float f2 = 0f;
		if (owner.IsOutside(owner.eyes.position))
		{
			f = Climate.GetRain(owner.eyes.position) * Weather.wetness_rain;
			f2 = Climate.GetSnow(owner.eyes.position) * Weather.wetness_snow;
		}
		bool flag = owner.baseProtection.amounts[4] > 0f;
		float currentEnvironmentalWetness = owner.currentEnvironmentalWetness;
		currentEnvironmentalWetness = Mathf.Clamp(currentEnvironmentalWetness, 0f, 0.8f);
		float num18 = owner.WaterFactor();
		if (!flag && num18 > 0f)
		{
			wetness.value = Mathf.Max(wetness.value, Mathf.Clamp(num18, wetness.min, wetness.max));
		}
		float num19 = Mathx.Max(wetness.value, f, f2, currentEnvironmentalWetness);
		num19 = Mathf.Min(num19, flag ? 0f : num19);
		wetness.MoveTowards(num19, delta * 0.05f);
		if (num18 < wetness.value && currentEnvironmentalWetness <= 0f)
		{
			wetness.MoveTowards(0f, delta * 0.2f * Mathf.InverseLerp(0f, 100f, num));
		}
		poison.MoveTowards(0f, delta * (5f / 9f));
		if (wetness.Fraction() > 0.4f && owner.estimatedSpeed > 0.25f && radiation_level.Fraction() == 0f)
		{
			radiation_poison.Subtract(radiation_poison.value * 0.2f * wetness.Fraction() * delta * 0.2f);
		}
		if (ConVar.Server.radiation)
		{
			if (!owner.IsGod())
			{
				radiation_level.value = owner.radiationLevel;
				if (radiation_level.value > 0f)
				{
					radiation_poison.Add(radiation_level.value * delta);
				}
			}
			else if (radiation_level.value > 0f)
			{
				radiation_level.value = 0f;
				radiation_poison.value = 0f;
			}
		}
		if (pending_health.value > 0f)
		{
			float num20 = Mathf.Min(1f * delta, pending_health.value);
			ownerEntity.Heal(num20);
			if (ownerEntity.healthFraction == 1f)
			{
				pending_health.value = 0f;
			}
			else
			{
				pending_health.Subtract(num20);
			}
		}
	}

	private float DeltaHot()
	{
		return Mathf.InverseLerp(20f, 100f, temperature.value);
	}

	private float DeltaCold()
	{
		return Mathf.InverseLerp(20f, -50f, temperature.value);
	}

	private float DeltaWet()
	{
		return wetness.value;
	}

	public void UseHeart(float frate)
	{
		if (heartrate.value > frate)
		{
			heartrate.Add(frate);
		}
		else
		{
			heartrate.value = frate;
		}
	}

	public void SendChangesToClient()
	{
		if (!HasChanged())
		{
			return;
		}
		isDirty = false;
		using ProtoBuf.PlayerMetabolism arg = Save();
		base.baseEntity.ClientRPC(RpcTarget.PlayerAndSpectators("UpdateMetabolism", base.baseEntity), arg);
	}

	public override void ApplyChange(MetabolismAttribute.Type type, float amount, float time)
	{
		FindAttribute(type)?.Add(amount);
	}

	public bool CanConsume()
	{
		if ((bool)owner && owner.IsHeadUnderwater())
		{
			return false;
		}
		return UnityEngine.Time.time - lastConsumeTime > 1f;
	}

	public void MarkConsumption()
	{
		lastConsumeTime = UnityEngine.Time.time;
	}

	public ProtoBuf.PlayerMetabolism Save()
	{
		ProtoBuf.PlayerMetabolism playerMetabolism = Facepunch.Pool.Get<ProtoBuf.PlayerMetabolism>();
		playerMetabolism.calories = calories.value;
		playerMetabolism.hydration = hydration.value;
		playerMetabolism.heartrate = heartrate.value;
		playerMetabolism.temperature = temperature.value;
		playerMetabolism.radiation_level = radiation_level.value;
		playerMetabolism.radiation_poisoning = radiation_poison.value;
		playerMetabolism.wetness = wetness.value;
		playerMetabolism.dirtyness = dirtyness.value;
		playerMetabolism.oxygen = oxygen.value;
		playerMetabolism.bleeding = bleeding.value;
		playerMetabolism.comfort = comfort.value;
		playerMetabolism.pending_health = pending_health.value;
		if ((bool)owner)
		{
			playerMetabolism.health = owner.Health();
		}
		return playerMetabolism;
	}

	public void Load(ProtoBuf.PlayerMetabolism s)
	{
		calories.SetValue(s.calories);
		hydration.SetValue(s.hydration);
		comfort.SetValue(s.comfort);
		heartrate.value = s.heartrate;
		temperature.value = s.temperature;
		radiation_level.value = s.radiation_level;
		radiation_poison.value = s.radiation_poisoning;
		wetness.value = s.wetness;
		dirtyness.value = s.dirtyness;
		oxygen.value = s.oxygen;
		bleeding.value = s.bleeding;
		pending_health.value = s.pending_health;
		if ((bool)owner)
		{
			owner.health = s.health;
		}
	}

	public void SetAttribute(MetabolismAttribute.Type type, float amount)
	{
		MetabolismAttribute metabolismAttribute = FindAttribute(type);
		if (metabolismAttribute != null)
		{
			float num = metabolismAttribute.value - amount;
			metabolismAttribute.Add(0f - num);
		}
	}

	public override MetabolismAttribute FindAttribute(MetabolismAttribute.Type type)
	{
		return type switch
		{
			MetabolismAttribute.Type.Poison => poison, 
			MetabolismAttribute.Type.Bleeding => bleeding, 
			MetabolismAttribute.Type.Radiation => radiation_poison, 
			MetabolismAttribute.Type.HealthOverTime => pending_health, 
			_ => base.FindAttribute(type), 
		};
	}
}
