#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Facepunch.Rust;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

public class GrowableEntity : BaseCombatEntity, IInstanceDataReceiver
{
	public class GrowableEntityUpdateQueue : ObjectWorkQueue<GrowableEntity>
	{
		protected override void RunJob(GrowableEntity entity)
		{
			if (ShouldAdd(entity))
			{
				entity.CalculateQualities_Water();
			}
		}

		protected override bool ShouldAdd(GrowableEntity entity)
		{
			if (base.ShouldAdd(entity))
			{
				return entity.IsValid();
			}
			return false;
		}
	}

	public PlantProperties Properties;

	public ItemDefinition SourceItemDef;

	private float stageAge;

	public GrowableGenes Genes = new GrowableGenes();

	private const float startingHealth = 10f;

	private const float artificalLightQuality = 1f;

	private const float planterGroundModifierBase = 0.6f;

	private const float fertilizerGroundModifierBonus = 0.4f;

	private const float growthGeneSpeedMultiplier = 0.25f;

	private const float waterGeneRequirementMultiplier = 0.1f;

	private const float hardinessGeneModifierBonus = 0.2f;

	private const float hardinessGeneTemperatureModifierBonus = 0.05f;

	private const float baseYieldIncreaseMultiplier = 1f;

	private const float yieldGeneBonusMultiplier = 0.25f;

	private const float maxNonPlanterGroundQuality = 0.6f;

	private const float deathRatePerQuality = 0.1f;

	private TimeCachedValue<float> sunExposure;

	private TimeCachedValue<float> artificialLightExposure;

	private TimeCachedValue<float> artificialTemperatureExposure;

	[ServerVar]
	[Help("How many miliseconds to budget for processing growable quality updates per frame")]
	public static float framebudgetms = 0.25f;

	public const Flags GodSpawn = Flags.Reserved1;

	public static GrowableEntityUpdateQueue growableEntityUpdateQueue = new GrowableEntityUpdateQueue();

	private bool underWater;

	private int seasons;

	private int harvests;

	private float terrainTypeValue;

	private float yieldPool;

	private PlanterBox planter;

	public PlantProperties.State State { get; private set; }

	public float Age { get; private set; }

	public float LightQuality { get; private set; }

	public float GroundQuality { get; private set; } = 1f;


	public float WaterQuality { get; private set; }

	public float WaterConsumption { get; private set; }

	public bool Fertilized { get; private set; }

	public float TemperatureQuality { get; private set; }

	public float OverallQuality { get; private set; }

	public float Yield { get; private set; }

	public float StageProgressFraction => stageAge / currentStage.lifeLengthSeconds;

	private PlantProperties.Stage currentStage => Properties.stages[(int)State];

	public static float ThinkDeltaTime => ConVar.Server.planttick;

	private float growDeltaTime => ConVar.Server.planttick * ConVar.Server.planttickscale;

	public int CurrentPickAmount => Mathf.RoundToInt(CurrentPickAmountFloat);

	public float CurrentPickAmountFloat => (currentStage.resources + Yield) * (float)Properties.pickupMultiplier;

	private bool god => HasFlag(Flags.Reserved1);

	public float CurrentTemperature
	{
		get
		{
			if (GetPlanter() != null)
			{
				return GetPlanter().GetPlantTemperature();
			}
			return Climate.GetTemperature(base.transform.position) + (artificialTemperatureExposure?.Get(force: false) ?? 0f);
		}
	}

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("GrowableEntity.OnRpcMessage"))
		{
			if (rpc == 759768385 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_EatFruit ");
				}
				using (TimeWarning.New("RPC_EatFruit"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(759768385u, "RPC_EatFruit", this, player, 3f))
						{
							return true;
						}
						if (!RPC_Server.MaxDistance.Test(759768385u, "RPC_EatFruit", this, player, 3f))
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
							RPCMessage msg2 = rPCMessage;
							RPC_EatFruit(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_EatFruit");
					}
				}
				return true;
			}
			if (rpc == 598660365 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_PickFruit ");
				}
				using (TimeWarning.New("RPC_PickFruit"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(598660365u, "RPC_PickFruit", this, player, 3f))
						{
							return true;
						}
						if (!RPC_Server.MaxDistance.Test(598660365u, "RPC_PickFruit", this, player, 3f))
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
							RPCMessage msg3 = rPCMessage;
							RPC_PickFruit(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in RPC_PickFruit");
					}
				}
				return true;
			}
			if (rpc == 3465633431u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_PickFruitAll ");
				}
				using (TimeWarning.New("RPC_PickFruitAll"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(3465633431u, "RPC_PickFruitAll", this, player, 3f))
						{
							return true;
						}
						if (!RPC_Server.MaxDistance.Test(3465633431u, "RPC_PickFruitAll", this, player, 3f))
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
							RPCMessage msg4 = rPCMessage;
							RPC_PickFruitAll(msg4);
						}
					}
					catch (Exception exception3)
					{
						Debug.LogException(exception3);
						player.Kick("RPC Error in RPC_PickFruitAll");
					}
				}
				return true;
			}
			if (rpc == 1959480148 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_RemoveDying ");
				}
				using (TimeWarning.New("RPC_RemoveDying"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(1959480148u, "RPC_RemoveDying", this, player, 3f))
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
							RPCMessage msg5 = rPCMessage;
							RPC_RemoveDying(msg5);
						}
					}
					catch (Exception exception4)
					{
						Debug.LogException(exception4);
						player.Kick("RPC Error in RPC_RemoveDying");
					}
				}
				return true;
			}
			if (rpc == 1771718099 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_RemoveDyingAll ");
				}
				using (TimeWarning.New("RPC_RemoveDyingAll"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(1771718099u, "RPC_RemoveDyingAll", this, player, 3f))
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
							RPCMessage msg6 = rPCMessage;
							RPC_RemoveDyingAll(msg6);
						}
					}
					catch (Exception exception5)
					{
						Debug.LogException(exception5);
						player.Kick("RPC Error in RPC_RemoveDyingAll");
					}
				}
				return true;
			}
			if (rpc == 232075937 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_RequestQualityUpdate ");
				}
				using (TimeWarning.New("RPC_RequestQualityUpdate"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(232075937u, "RPC_RequestQualityUpdate", this, player, 3f))
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
							RPCMessage msg7 = rPCMessage;
							RPC_RequestQualityUpdate(msg7);
						}
					}
					catch (Exception exception6)
					{
						Debug.LogException(exception6);
						player.Kick("RPC Error in RPC_RequestQualityUpdate");
					}
				}
				return true;
			}
			if (rpc == 2222960834u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_TakeClone ");
				}
				using (TimeWarning.New("RPC_TakeClone"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(2222960834u, "RPC_TakeClone", this, player, 3f))
						{
							return true;
						}
						if (!RPC_Server.MaxDistance.Test(2222960834u, "RPC_TakeClone", this, player, 3f))
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
							RPCMessage msg8 = rPCMessage;
							RPC_TakeClone(msg8);
						}
					}
					catch (Exception exception7)
					{
						Debug.LogException(exception7);
						player.Kick("RPC Error in RPC_TakeClone");
					}
				}
				return true;
			}
			if (rpc == 95639240 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_TakeCloneAll ");
				}
				using (TimeWarning.New("RPC_TakeCloneAll"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsVisible.Test(95639240u, "RPC_TakeCloneAll", this, player, 3f))
						{
							return true;
						}
						if (!RPC_Server.MaxDistance.Test(95639240u, "RPC_TakeCloneAll", this, player, 3f))
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
							RPCMessage msg9 = rPCMessage;
							RPC_TakeCloneAll(msg9);
						}
					}
					catch (Exception exception8)
					{
						Debug.LogException(exception8);
						player.Kick("RPC Error in RPC_TakeCloneAll");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public void ReceiveInstanceData(ProtoBuf.Item.InstanceData data)
	{
		GrowableGeneEncoding.DecodeIntToGenes(data.dataInt, Genes);
		GrowableGeneEncoding.DecodeIntToPreviousGenes(data.dataInt, Genes);
	}

	public override void ResetState()
	{
		base.ResetState();
		State = PlantProperties.State.Seed;
	}

	public bool CanPick()
	{
		return currentStage.resources > 0f;
	}

	public bool CanTakeSeeds()
	{
		if (currentStage.resources > 0f)
		{
			return Properties.SeedItem != null;
		}
		return false;
	}

	public bool CanClone()
	{
		if (currentStage.resources > 0f)
		{
			return Properties.CloneItem != null;
		}
		return false;
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.growableEntity = Facepunch.Pool.Get<ProtoBuf.GrowableEntity>();
		info.msg.growableEntity.state = (int)State;
		info.msg.growableEntity.totalAge = Age;
		info.msg.growableEntity.stageAge = stageAge;
		info.msg.growableEntity.yieldFraction = Yield;
		info.msg.growableEntity.yieldPool = yieldPool;
		info.msg.growableEntity.fertilized = Fertilized;
		if (Genes != null)
		{
			Genes.Save(info);
		}
		if (!info.forDisk)
		{
			info.msg.growableEntity.lightModifier = LightQuality;
			info.msg.growableEntity.groundModifier = GroundQuality;
			info.msg.growableEntity.waterModifier = WaterQuality;
			info.msg.growableEntity.happiness = OverallQuality;
			info.msg.growableEntity.temperatureModifier = TemperatureQuality;
			info.msg.growableEntity.waterConsumption = WaterConsumption;
		}
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.growableEntity != null)
		{
			Age = info.msg.growableEntity.totalAge;
			stageAge = info.msg.growableEntity.stageAge;
			Yield = info.msg.growableEntity.yieldFraction;
			Fertilized = info.msg.growableEntity.fertilized;
			yieldPool = info.msg.growableEntity.yieldPool;
			Genes.Load(info);
			ChangeState((PlantProperties.State)info.msg.growableEntity.state, resetAge: false, loading: true);
		}
		else
		{
			Genes.GenerateRandom(this);
		}
	}

	private void ChangeState(PlantProperties.State state, bool resetAge, bool loading = false)
	{
		if (base.isServer && State == state)
		{
			return;
		}
		State = state;
		if (!base.isServer)
		{
			return;
		}
		if (!loading)
		{
			if (currentStage.resources > 0f)
			{
				yieldPool = currentStage.yield;
			}
			if (state == PlantProperties.State.Crossbreed)
			{
				if (Properties.CrossBreedEffect.isValid)
				{
					Effect.server.Run(Properties.CrossBreedEffect.resourcePath, base.transform.position, Vector3.up);
				}
				GrowableGenetics.CrossBreed(this);
			}
			SendNetworkUpdate();
		}
		if (resetAge)
		{
			stageAge = 0f;
		}
	}

	public override void OnDeployed(BaseEntity parent, BasePlayer deployedBy, Item fromItem)
	{
		base.OnDeployed(parent, deployedBy, fromItem);
		if (parent != null && parent is PlanterBox planterBox)
		{
			planterBox.OnPlantInserted(this, deployedBy);
		}
	}

	public void QueueForQualityUpdate()
	{
		growableEntityUpdateQueue.Add(this);
	}

	public void SetGodQuality(bool qual)
	{
		SetFlag(Flags.Reserved1, qual);
	}

	public void CalculateQualities(bool firstTime, bool forceArtificialLightUpdates = false, bool forceArtificialTemperatureUpdates = false)
	{
		if (IsDead())
		{
			return;
		}
		if (god)
		{
			SetMaxGrowingConditions();
			return;
		}
		if (sunExposure == null)
		{
			sunExposure = new TimeCachedValue<float>
			{
				refreshCooldown = 30f,
				refreshRandomRange = 5f,
				updateValue = SunRaycast
			};
		}
		if (artificialLightExposure == null)
		{
			artificialLightExposure = new TimeCachedValue<float>
			{
				refreshCooldown = 60f,
				refreshRandomRange = 5f,
				updateValue = CalculateArtificialLightExposure
			};
		}
		if (artificialTemperatureExposure == null)
		{
			artificialTemperatureExposure = new TimeCachedValue<float>
			{
				refreshCooldown = 60f,
				refreshRandomRange = 5f,
				updateValue = CalculateArtificialTemperature
			};
		}
		if (forceArtificialTemperatureUpdates)
		{
			artificialTemperatureExposure.ForceNextRun();
		}
		CalculateLightQuality(forceArtificialLightUpdates || firstTime);
		CalculateWaterQuality();
		CalculateWaterConsumption();
		CalculateGroundQuality(firstTime);
		CalculateTemperatureQuality();
		CalculateOverallQuality();
	}

	private void CalculateQualities_Water()
	{
		CalculateWaterQuality();
		CalculateWaterConsumption();
		CalculateOverallQuality();
	}

	public void CalculateLightQuality(bool forceArtificalUpdate)
	{
		float num = Mathf.Clamp01(Properties.timeOfDayHappiness.Evaluate(TOD_Sky.Instance.Cycle.Hour));
		if (!ConVar.Server.plantlightdetection)
		{
			LightQuality = num;
			return;
		}
		LightQuality = CalculateSunExposure(forceArtificalUpdate) * num;
		if (LightQuality <= 0f)
		{
			LightQuality = GetArtificialLightExposure(forceArtificalUpdate);
		}
		LightQuality = RemapValue(LightQuality, 0f, Properties.OptimalLightQuality, 0f, 1f);
	}

	private float CalculateSunExposure(bool force)
	{
		if (TOD_Sky.Instance.IsNight)
		{
			return 0f;
		}
		if (GetPlanter() != null)
		{
			return GetPlanter().GetSunExposure();
		}
		return sunExposure?.Get(force) ?? 0f;
	}

	private float SunRaycast()
	{
		return SunRaycast(base.transform.position + new Vector3(0f, 1f, 0f));
	}

	private float GetArtificialLightExposure(bool force)
	{
		if (GetPlanter() != null)
		{
			return GetPlanter().GetArtificialLightExposure();
		}
		return artificialLightExposure?.Get(force) ?? 0f;
	}

	private float CalculateArtificialLightExposure()
	{
		return CalculateArtificialLightExposure(base.transform);
	}

	public static float CalculateArtificialLightExposure(Transform forTransform)
	{
		float result = 0f;
		List<CeilingLight> obj = Facepunch.Pool.Get<List<CeilingLight>>();
		Vis.Entities(forTransform.position + new Vector3(0f, ConVar.Server.ceilingLightHeightOffset, 0f), ConVar.Server.ceilingLightGrowableRange, obj, 256);
		foreach (CeilingLight item in obj)
		{
			if (item.IsOn())
			{
				result = 1f;
				break;
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return result;
	}

	public static float SunRaycast(Vector3 checkPosition)
	{
		Vector3 normalized = (TOD_Sky.Instance.Components.Sun.transform.position - checkPosition).normalized;
		if (!UnityEngine.Physics.Raycast(checkPosition, normalized, out var _, 100f, 10551297))
		{
			return 1f;
		}
		return 0f;
	}

	public void CalculateWaterQuality()
	{
		if (GetPlanter() != null)
		{
			float soilSaturationFraction = planter.soilSaturationFraction;
			if (soilSaturationFraction > ConVar.Server.optimalPlanterQualitySaturation)
			{
				WaterQuality = RemapValue(soilSaturationFraction, ConVar.Server.optimalPlanterQualitySaturation, 1f, 1f, 0.6f);
			}
			else
			{
				WaterQuality = RemapValue(soilSaturationFraction, 0f, ConVar.Server.optimalPlanterQualitySaturation, 0f, 1f);
			}
		}
		else
		{
			switch ((TerrainBiome.Enum)TerrainMeta.BiomeMap.GetBiomeMaxType(base.transform.position))
			{
			case TerrainBiome.Enum.Arctic:
				WaterQuality = 0.1f;
				break;
			case TerrainBiome.Enum.Arid:
			case TerrainBiome.Enum.Temperate:
			case TerrainBiome.Enum.Tundra:
				WaterQuality = 0.3f;
				break;
			default:
				WaterQuality = 0f;
				break;
			}
		}
		WaterQuality = Mathf.Clamp01(WaterQuality);
		WaterQuality = RemapValue(WaterQuality, 0f, Properties.OptimalWaterQuality, 0f, 1f);
	}

	public void CalculateGroundQuality(bool firstCheck)
	{
		if (underWater && !firstCheck)
		{
			GroundQuality = 0f;
			return;
		}
		if (firstCheck)
		{
			Vector3 position = base.transform.position;
			if (WaterLevel.Test(position, waves: true, volumes: true, this))
			{
				underWater = true;
				GroundQuality = 0f;
				return;
			}
			underWater = false;
			terrainTypeValue = GetGroundTypeValue(position);
		}
		if (GetPlanter() != null)
		{
			GroundQuality = 0.6f;
			GroundQuality += (Fertilized ? 0.4f : 0f);
		}
		else
		{
			GroundQuality = terrainTypeValue;
			float num = (float)Genes.GetGeneTypeCount(GrowableGenetics.GeneType.Hardiness) * 0.2f;
			float b = GroundQuality + num;
			GroundQuality = Mathf.Min(0.6f, b);
		}
		GroundQuality = RemapValue(GroundQuality, 0f, Properties.OptimalGroundQuality, 0f, 1f);
	}

	private float GetGroundTypeValue(Vector3 pos)
	{
		return (TerrainSplat.Enum)TerrainMeta.SplatMap.GetSplatMaxType(pos) switch
		{
			TerrainSplat.Enum.Grass => 0.3f, 
			TerrainSplat.Enum.Snow => 0f, 
			TerrainSplat.Enum.Rock => 0f, 
			TerrainSplat.Enum.Stones => 0f, 
			TerrainSplat.Enum.Dirt => 0.3f, 
			TerrainSplat.Enum.Forest => 0.2f, 
			TerrainSplat.Enum.Sand => 0f, 
			TerrainSplat.Enum.Gravel => 0f, 
			_ => 0.5f, 
		};
	}

	private void CalculateTemperatureQuality()
	{
		TemperatureQuality = Mathf.Clamp01(Properties.temperatureHappiness.Evaluate(CurrentTemperature));
		float num = (float)Genes.GetGeneTypeCount(GrowableGenetics.GeneType.Hardiness) * 0.05f;
		TemperatureQuality = Mathf.Clamp01(TemperatureQuality + num);
		TemperatureQuality = RemapValue(TemperatureQuality, 0f, Properties.OptimalTemperatureQuality, 0f, 1f);
	}

	public void SetMaxGrowingConditions()
	{
		LightQuality = 1f;
		WaterQuality = 1f;
		GroundQuality = 1f;
		TemperatureQuality = 1f;
		OverallQuality = 1f;
	}

	public float CalculateOverallQuality()
	{
		float a = 1f;
		if (ConVar.Server.useMinimumPlantCondition)
		{
			a = Mathf.Min(a, LightQuality);
			a = Mathf.Min(a, WaterQuality);
			a = Mathf.Min(a, GroundQuality);
			a = Mathf.Min(a, TemperatureQuality);
		}
		else
		{
			a = LightQuality * WaterQuality * GroundQuality * TemperatureQuality;
		}
		OverallQuality = a;
		return OverallQuality;
	}

	public void CalculateWaterConsumption()
	{
		float num = Properties.temperatureWaterRequirementMultiplier.Evaluate(CurrentTemperature);
		float num2 = 1f + (float)Genes.GetGeneTypeCount(GrowableGenetics.GeneType.WaterRequirement) * 0.1f;
		WaterConsumption = Properties.WaterIntake * num * num2;
	}

	private float CalculateArtificialTemperature()
	{
		return CalculateArtificialTemperature(base.transform);
	}

	public static float CalculateArtificialTemperature(Transform forTransform)
	{
		Vector3 position = forTransform.position;
		List<GrowableHeatSource> obj = Facepunch.Pool.Get<List<GrowableHeatSource>>();
		Vis.Components(position, ConVar.Server.artificialTemperatureGrowableRange, obj, 256);
		float num = 0f;
		foreach (GrowableHeatSource item in obj)
		{
			num = Mathf.Max(item.ApplyHeat(position), num);
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return num;
	}

	public int CalculateMarketValue()
	{
		int baseMarketValue = Properties.BaseMarketValue;
		int num = Genes.GetPositiveGeneCount() * 10;
		int num2 = Genes.GetNegativeGeneCount() * -10;
		baseMarketValue += num;
		baseMarketValue += num2;
		return Mathf.Max(0, baseMarketValue);
	}

	private static float RemapValue(float inValue, float minA, float maxA, float minB, float maxB)
	{
		if (inValue >= maxA)
		{
			return maxB;
		}
		float t = Mathf.InverseLerp(minA, maxA, inValue);
		return Mathf.Lerp(minB, maxB, t);
	}

	public bool IsFood()
	{
		if (Properties.pickupItem.category == ItemCategory.Food)
		{
			return Properties.pickupItem.GetComponent<ItemModConsume>() != null;
		}
		return false;
	}

	public override void ServerInit()
	{
		base.ServerInit();
		InvokeRandomized(RunUpdate, ThinkDeltaTime, ThinkDeltaTime, ThinkDeltaTime * 0.1f);
		base.health = 10f;
		ResetSeason();
		Genes.GenerateRandom(this);
		if (!Rust.Application.isLoadingSave)
		{
			CalculateQualities(firstTime: true);
		}
	}

	public PlanterBox GetPlanter()
	{
		if (planter == null)
		{
			BaseEntity baseEntity = GetParentEntity();
			if (baseEntity != null)
			{
				planter = baseEntity as PlanterBox;
			}
		}
		return planter;
	}

	public override void OnParentChanging(BaseEntity oldParent, BaseEntity newParent)
	{
		base.OnParentChanging(oldParent, newParent);
		planter = newParent as PlanterBox;
		if (!Rust.Application.isLoadingSave && planter != null)
		{
			planter.FertilizeGrowables();
		}
		CalculateQualities(firstTime: true);
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		CalculateQualities(firstTime: true);
	}

	public void ResetSeason()
	{
		Yield = 0f;
		yieldPool = 0f;
	}

	private void RunUpdate()
	{
		if (!IsDead())
		{
			CalculateQualities(firstTime: false);
			float overallQuality = CalculateOverallQuality();
			float actualStageAgeIncrease = UpdateAge(overallQuality);
			UpdateHealthAndYield(overallQuality, actualStageAgeIncrease);
			if (base.health <= 0f)
			{
				TellPlanter();
				Die();
			}
			else
			{
				UpdateState();
				ConsumeWater();
				SendNetworkUpdate();
			}
		}
	}

	private float UpdateAge(float overallQuality)
	{
		Age += growDeltaTime;
		float num = (currentStage.IgnoreConditions ? 1f : (Mathf.Max(overallQuality, 0f) * GetGrowthBonus(overallQuality)));
		float num2 = growDeltaTime * num;
		stageAge += num2;
		return num2;
	}

	private void UpdateHealthAndYield(float overallQuality, float actualStageAgeIncrease)
	{
		if (GetPlanter() == null && UnityEngine.Random.Range(0f, 1f) <= ConVar.Server.nonPlanterDeathChancePerTick)
		{
			base.health = 0f;
			return;
		}
		if (overallQuality <= 0f)
		{
			ApplyDeathRate();
		}
		base.health += overallQuality * currentStage.health * growDeltaTime;
		if (yieldPool > 0f)
		{
			float num = currentStage.yield / (currentStage.lifeLengthSeconds / growDeltaTime);
			float num2 = Mathf.Min(yieldPool, num * (actualStageAgeIncrease / growDeltaTime));
			yieldPool -= num;
			float num3 = 1f + (float)Genes.GetGeneTypeCount(GrowableGenetics.GeneType.Yield) * 0.25f;
			Yield += num2 * 1f * num3;
		}
	}

	private void ApplyDeathRate()
	{
		float num = 0f;
		if (WaterQuality <= 0f)
		{
			num += 0.1f;
		}
		if (LightQuality <= 0f)
		{
			num += 0.1f;
		}
		if (GroundQuality <= 0f)
		{
			num += 0.1f;
		}
		if (TemperatureQuality <= 0f)
		{
			num += 0.1f;
		}
		base.health -= num;
	}

	private float GetGrowthBonus(float overallQuality)
	{
		float result = 1f + (float)Genes.GetGeneTypeCount(GrowableGenetics.GeneType.GrowthSpeed) * 0.25f;
		if (overallQuality <= 0f)
		{
			result = 1f;
		}
		return result;
	}

	private PlantProperties.State UpdateState()
	{
		if (stageAge <= currentStage.lifeLengthSeconds)
		{
			return State;
		}
		if (State == PlantProperties.State.Dying)
		{
			TellPlanter();
			Die();
			return PlantProperties.State.Dying;
		}
		if (currentStage.nextState <= State)
		{
			seasons++;
		}
		if (seasons >= Properties.MaxSeasons)
		{
			ChangeState(PlantProperties.State.Dying, resetAge: true);
		}
		else
		{
			ChangeState(currentStage.nextState, resetAge: true);
		}
		return State;
	}

	private void ConsumeWater()
	{
		if (State != PlantProperties.State.Dying && !(GetPlanter() == null))
		{
			int num = Mathf.CeilToInt(Mathf.Min(planter.soilSaturation, WaterConsumption));
			if ((float)num > 0f)
			{
				planter.ConsumeWater(num, this);
			}
		}
	}

	public void Fertilize()
	{
		if (!Fertilized)
		{
			Fertilized = true;
			CalculateQualities(firstTime: false);
			SendNetworkUpdate();
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	[RPC_Server.IsVisible(3f)]
	public void RPC_TakeClone(RPCMessage msg)
	{
		TakeClones(msg.player);
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	[RPC_Server.IsVisible(3f)]
	public void RPC_TakeCloneAll(RPCMessage msg)
	{
		if (GetParentEntity() != null)
		{
			List<GrowableEntity> obj = Facepunch.Pool.Get<List<GrowableEntity>>();
			foreach (BaseEntity child in GetParentEntity().children)
			{
				if (child != this && child is GrowableEntity item)
				{
					obj.Add(item);
				}
			}
			foreach (GrowableEntity item2 in obj)
			{
				item2.TakeClones(msg.player);
			}
			Facepunch.Pool.FreeUnmanaged(ref obj);
		}
		TakeClones(msg.player);
	}

	private void TakeClones(BasePlayer player)
	{
		if (player == null || !CanClone())
		{
			return;
		}
		int num = Properties.BaseCloneCount + Genes.GetGeneTypeCount(GrowableGenetics.GeneType.Yield) / 2;
		if (num > 0)
		{
			Item item = ItemManager.Create(Properties.CloneItem, num, 0uL);
			GrowableGeneEncoding.EncodeGenesToItem(this, item);
			Analytics.Azure.OnGatherItem(item.info.shortname, item.amount, this, player);
			player.GiveItem(item, GiveItemReason.ResourceHarvested);
			if (Properties.pickEffect.isValid)
			{
				Effect.server.Run(Properties.pickEffect.resourcePath, base.transform.position, Vector3.up);
			}
			TellPlanter();
			Die();
		}
	}

	public void PickFruit(BasePlayer player, bool eat = false)
	{
		if (!CanPick())
		{
			return;
		}
		harvests++;
		GiveFruit(player, CurrentPickAmount, eat);
		RandomItemDispenser randomItemDispenser = PrefabAttribute.server.Find<RandomItemDispenser>(prefabID);
		if (randomItemDispenser != null)
		{
			randomItemDispenser.DistributeItems(player, base.transform.position);
		}
		ResetSeason();
		if (Properties.pickEffect.isValid)
		{
			Effect.server.Run(Properties.pickEffect.resourcePath, base.transform.position, Vector3.up);
		}
		if (harvests >= Properties.maxHarvests)
		{
			if (Properties.disappearAfterHarvest)
			{
				TellPlanter();
				Die();
			}
			else
			{
				ChangeState(PlantProperties.State.Dying, resetAge: true);
			}
		}
		else
		{
			ChangeState(PlantProperties.State.Mature, resetAge: true);
		}
	}

	private void GiveFruit(BasePlayer player, int amount, bool eat)
	{
		if (amount <= 0)
		{
			return;
		}
		bool flag = Properties.pickupItem.condition.enabled;
		if (flag)
		{
			for (int i = 0; i < amount; i++)
			{
				GiveFruit(player, 1, flag, eat);
			}
		}
		else
		{
			GiveFruit(player, amount, flag, eat);
		}
	}

	private void GiveFruit(BasePlayer player, int amount, bool applyCondition, bool eat)
	{
		Item item = ItemManager.Create(Properties.pickupItem, amount, 0uL);
		if (applyCondition)
		{
			item.conditionNormalized = Properties.fruitVisualScaleCurve.Evaluate(StageProgressFraction);
		}
		if (eat && player != null && IsFood())
		{
			ItemModConsume component = item.info.GetComponent<ItemModConsume>();
			if (component != null)
			{
				component.DoAction(item, player);
				return;
			}
		}
		if (player != null)
		{
			Analytics.Azure.OnGatherItem(item.info.shortname, item.amount, this, player);
			player.GiveItem(item, GiveItemReason.ResourceHarvested);
		}
		else
		{
			item.Drop(base.transform.position + Vector3.up * 0.5f, Vector3.up * 1f);
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	[RPC_Server.IsVisible(3f)]
	public void RPC_PickFruit(RPCMessage msg)
	{
		PickFruit(msg.player);
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	[RPC_Server.IsVisible(3f)]
	public void RPC_EatFruit(RPCMessage msg)
	{
		PickFruit(msg.player, eat: true);
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	[RPC_Server.IsVisible(3f)]
	public void RPC_PickFruitAll(RPCMessage msg)
	{
		if (GetParentEntity() != null)
		{
			List<GrowableEntity> obj = Facepunch.Pool.Get<List<GrowableEntity>>();
			foreach (BaseEntity child in GetParentEntity().children)
			{
				if (child != this && child is GrowableEntity item)
				{
					obj.Add(item);
				}
			}
			foreach (GrowableEntity item2 in obj)
			{
				item2.PickFruit(msg.player);
			}
			Facepunch.Pool.FreeUnmanaged(ref obj);
		}
		PickFruit(msg.player);
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	public void RPC_RemoveDying(RPCMessage msg)
	{
		RemoveDying(msg.player);
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	public void RPC_RemoveDyingAll(RPCMessage msg)
	{
		if (GetParentEntity() != null)
		{
			List<GrowableEntity> obj = Facepunch.Pool.Get<List<GrowableEntity>>();
			foreach (BaseEntity child in GetParentEntity().children)
			{
				if (child != this && child is GrowableEntity item)
				{
					obj.Add(item);
				}
			}
			foreach (GrowableEntity item2 in obj)
			{
				item2.RemoveDying(msg.player);
			}
			Facepunch.Pool.FreeUnmanaged(ref obj);
		}
		RemoveDying(msg.player);
	}

	public void RemoveAllForce()
	{
		if (GetParentEntity() != null)
		{
			List<GrowableEntity> obj = Facepunch.Pool.Get<List<GrowableEntity>>();
			foreach (BaseEntity child in GetParentEntity().children)
			{
				if (child != this && child is GrowableEntity item)
				{
					obj.Add(item);
				}
			}
			foreach (GrowableEntity item2 in obj)
			{
				item2.RemoveDying(null);
			}
			Facepunch.Pool.FreeUnmanaged(ref obj);
		}
		ForceRemove();
	}

	public void ForceRemove()
	{
		if (!(Properties.removeDyingItem == null))
		{
			if (Properties.removeDyingEffect.isValid)
			{
				Effect.server.Run(Properties.removeDyingEffect.resourcePath, base.transform.position, Vector3.up);
			}
			TellPlanter();
			Die();
		}
	}

	public void RemoveDying(BasePlayer receiver)
	{
		if (State == PlantProperties.State.Dying && !(Properties.removeDyingItem == null))
		{
			if (Properties.removeDyingEffect.isValid)
			{
				Effect.server.Run(Properties.removeDyingEffect.resourcePath, base.transform.position, Vector3.up);
			}
			Item item = ItemManager.Create(Properties.removeDyingItem, 1, 0uL);
			if (receiver != null)
			{
				receiver.GiveItem(item, GiveItemReason.PickedUp);
			}
			else
			{
				item.Drop(base.transform.position + Vector3.up * 0.5f, Vector3.up * 1f);
			}
			TellPlanter();
			Die();
		}
	}

	private void TellPlanter()
	{
		BaseEntity baseEntity = GetParentEntity();
		if (baseEntity != null && baseEntity is PlanterBox planterBox)
		{
			planterBox.OnPlantRemoved(this, null);
		}
	}

	[ServerVar(ServerAdmin = true)]
	public static void GrowAll(ConsoleSystem.Arg arg)
	{
		BasePlayer basePlayer = arg.Player();
		if (!basePlayer.IsAdmin)
		{
			return;
		}
		List<GrowableEntity> obj = Facepunch.Pool.Get<List<GrowableEntity>>();
		Vis.Entities(basePlayer.ServerPosition, 6f, obj);
		foreach (GrowableEntity item in obj)
		{
			if (item.isServer)
			{
				item.ChangeState(item.currentStage.nextState, resetAge: false);
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	private void RPC_RequestQualityUpdate(RPCMessage msg)
	{
		if (msg.player != null)
		{
			ProtoBuf.GrowableEntity growableEntity = Facepunch.Pool.Get<ProtoBuf.GrowableEntity>();
			growableEntity.lightModifier = LightQuality;
			growableEntity.groundModifier = GroundQuality;
			growableEntity.waterModifier = WaterQuality;
			growableEntity.happiness = OverallQuality;
			growableEntity.temperatureModifier = TemperatureQuality;
			growableEntity.waterConsumption = WaterConsumption;
			ClientRPC(RpcTarget.Player("RPC_ReceiveQualityUpdate", msg.player), growableEntity);
		}
	}
}
