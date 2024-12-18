#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Network;
using ProtoBuf;
using UnityEngine;
using UnityEngine.Assertions;

public class SamSite : ContainerIOEntity
{
	public interface ISamSiteTarget
	{
		static List<ISamSiteTarget> serverList;

		SamTargetType SAMTargetType { get; }

		bool isClient { get; }

		bool IsValidSAMTarget(bool staticRespawn);

		Vector3 CenterPoint();

		Vector3 GetWorldVelocity();

		bool IsVisible(Vector3 position, float maxDistance = float.PositiveInfinity);

		static ISamSiteTarget()
		{
			serverList = new List<ISamSiteTarget>();
		}
	}

	public class SamTargetType
	{
		public readonly float scanRadius;

		public readonly float speedMultiplier;

		public readonly float timeBetweenBursts;

		public SamTargetType(float scanRadius, float speedMultiplier, float timeBetweenBursts)
		{
			this.scanRadius = scanRadius;
			this.speedMultiplier = speedMultiplier;
			this.timeBetweenBursts = timeBetweenBursts;
		}
	}

	public Animator pitchAnimator;

	public GameObject yaw;

	public GameObject pitch;

	public GameObject gear;

	public Transform eyePoint;

	public float gearEpislonDegrees = 20f;

	public float turnSpeed = 1f;

	public float clientLerpSpeed = 1f;

	public Vector3 currentAimDir = Vector3.forward;

	public Vector3 targetAimDir = Vector3.forward;

	public float vehicleScanRadius = 350f;

	public float missileScanRadius = 500f;

	public GameObjectRef projectileTest;

	public GameObjectRef muzzleFlashTest;

	public bool staticRespawn;

	public ItemDefinition ammoType;

	public Transform[] tubes;

	[ServerVar(Help = "how long until static sam sites auto repair")]
	public static float staticrepairseconds = 1200f;

	public SoundDefinition yawMovementLoopDef;

	public float yawGainLerp = 8f;

	public float yawGainMovementSpeedMult = 0.1f;

	public SoundDefinition pitchMovementLoopDef;

	public float pitchGainLerp = 10f;

	public float pitchGainMovementSpeedMult = 0.5f;

	public int lowAmmoThreshold = 5;

	public Flags Flag_TargetMode = Flags.Reserved9;

	public Flags Flag_ManuallySetMode = Flags.Reserved10;

	public static SamTargetType targetTypeUnknown;

	public static SamTargetType targetTypeVehicle;

	public static SamTargetType targetTypeMissile;

	private ISamSiteTarget currentTarget;

	private SamTargetType mostRecentTargetType;

	private Item ammoItem;

	private float lockOnTime;

	private float lastTargetVisibleTime;

	private int lastAmmoCount;

	private int currentTubeIndex;

	private int firedCount;

	private float nextBurstTime;

	private int input1Amount;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("SamSite.OnRpcMessage"))
		{
			if (rpc == 3160662357u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - ToggleDefenderMode ");
				}
				using (TimeWarning.New("ToggleDefenderMode"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3160662357u, "ToggleDefenderMode", this, player, 1uL))
						{
							return true;
						}
						if (!RPC_Server.IsVisible.Test(3160662357u, "ToggleDefenderMode", this, player, 3f))
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
							ToggleDefenderMode(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in ToggleDefenderMode");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override bool IsPowered()
	{
		if (!staticRespawn)
		{
			return HasFlag(Flags.Reserved8);
		}
		return true;
	}

	public override int ConsumptionAmount()
	{
		return 25;
	}

	public bool IsInDefenderMode()
	{
		return HasFlag(Flag_TargetMode);
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
	}

	private void SetTarget(ISamSiteTarget target)
	{
		bool num = currentTarget != target;
		currentTarget = target;
		if (!target.IsUnityNull())
		{
			mostRecentTargetType = target.SAMTargetType;
		}
		if (num)
		{
			MarkIODirty();
		}
	}

	private void MarkIODirty()
	{
		if (!staticRespawn)
		{
			lastPassthroughEnergy = -1;
			MarkDirtyForceUpdateOutputs();
		}
	}

	private void ClearTarget()
	{
		SetTarget(null);
	}

	public override void ServerInit()
	{
		base.ServerInit();
		targetTypeUnknown = new SamTargetType(vehicleScanRadius, 1f, 5f);
		targetTypeVehicle = new SamTargetType(vehicleScanRadius, 1f, 5f);
		targetTypeMissile = new SamTargetType(missileScanRadius, 2.25f, 3.5f);
		mostRecentTargetType = targetTypeUnknown;
		ClearTarget();
		InvokeRandomized(TargetScan, 1f, 3f, 0.2f);
		currentAimDir = base.transform.forward;
		if (base.inventory != null && !staticRespawn)
		{
			base.inventory.onItemAddedRemoved = OnItemAddedRemoved;
		}
	}

	private void OnItemAddedRemoved(Item arg1, bool arg2)
	{
		EnsureReloaded();
		if (IsPowered())
		{
			MarkIODirty();
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.samSite = Facepunch.Pool.Get<SAMSite>();
		info.msg.samSite.aimDir = GetAimDir();
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		if (staticRespawn && HasFlag(Flags.Reserved1))
		{
			Invoke(SelfHeal, staticrepairseconds);
		}
	}

	public void SelfHeal()
	{
		lifestate = LifeState.Alive;
		base.health = startHealth;
		SetFlag(Flags.Reserved1, b: false);
	}

	public override void Die(HitInfo info = null)
	{
		if (staticRespawn)
		{
			ClearTarget();
			Quaternion quaternion = Quaternion.Euler(0f, Quaternion.LookRotation(currentAimDir, Vector3.up).eulerAngles.y, 0f);
			currentAimDir = quaternion * Vector3.forward;
			Invoke(SelfHeal, staticrepairseconds);
			lifestate = LifeState.Dead;
			base.health = 0f;
			SetFlag(Flags.Reserved1, b: true);
		}
		else
		{
			base.Die(info);
		}
	}

	public void FixedUpdate()
	{
		Vector3 vector = currentAimDir;
		if (!currentTarget.IsUnityNull() && IsPowered())
		{
			float num = projectileTest.Get().GetComponent<ServerProjectile>().speed * currentTarget.SAMTargetType.speedMultiplier;
			Vector3 vector2 = currentTarget.CenterPoint();
			float num2 = Vector3.Distance(vector2, eyePoint.transform.position);
			float num3 = num2 / num;
			Vector3 a = vector2 + currentTarget.GetWorldVelocity() * num3;
			num3 = Vector3.Distance(a, eyePoint.transform.position) / num;
			a = vector2 + currentTarget.GetWorldVelocity() * num3;
			if (currentTarget.GetWorldVelocity().magnitude > 0.1f)
			{
				float num4 = Mathf.Sin(UnityEngine.Time.time * 3f) * (1f + num3 * 0.5f);
				a += currentTarget.GetWorldVelocity().normalized * num4;
			}
			currentAimDir = (a - eyePoint.transform.position).normalized;
			if (num2 > currentTarget.SAMTargetType.scanRadius)
			{
				ClearTarget();
			}
		}
		Vector3 eulerAngles = Quaternion.LookRotation(currentAimDir, base.transform.up).eulerAngles;
		eulerAngles = BaseMountable.ConvertVector(eulerAngles);
		float t = Mathf.InverseLerp(0f, 90f, 0f - eulerAngles.x);
		float z = Mathf.Lerp(15f, -75f, t);
		Quaternion localRotation = Quaternion.Euler(0f, eulerAngles.y, 0f);
		yaw.transform.localRotation = localRotation;
		Quaternion localRotation2 = Quaternion.Euler(pitch.transform.localRotation.eulerAngles.x, pitch.transform.localRotation.eulerAngles.y, z);
		pitch.transform.localRotation = localRotation2;
		if (currentAimDir != vector)
		{
			SendNetworkUpdate();
		}
	}

	public Vector3 GetAimDir()
	{
		return currentAimDir;
	}

	public bool HasValidTarget()
	{
		return !currentTarget.IsUnityNull();
	}

	public override bool CanPickup(BasePlayer player)
	{
		if (!base.CanPickup(player))
		{
			return false;
		}
		if (base.isServer && pickup.requireEmptyInv && base.inventory != null && base.inventory.itemList.Count > 0)
		{
			return false;
		}
		return !HasAmmo();
	}

	private void AddTargetSet(List<ISamSiteTarget> allTargets, float scanRadius)
	{
		foreach (ISamSiteTarget server in ISamSiteTarget.serverList)
		{
			if (!(server is MLRSRocket) && Vector3.Distance(server.CenterPoint(), eyePoint.transform.position) < scanRadius)
			{
				allTargets.Add(server);
			}
		}
	}

	private void AddMLRSRockets(List<ISamSiteTarget> allTargets, float scanRadius)
	{
		if (MLRSRocket.serverList.Count == 0)
		{
			return;
		}
		foreach (MLRSRocket server in MLRSRocket.serverList)
		{
			if (Vector3.Distance(server.transform.position, base.transform.position) < scanRadius)
			{
				allTargets.Add(server);
			}
		}
	}

	public void TargetScan()
	{
		if (!IsPowered())
		{
			lastTargetVisibleTime = 0f;
			return;
		}
		if (UnityEngine.Time.time > lastTargetVisibleTime + 3f)
		{
			ClearTarget();
		}
		if (!staticRespawn)
		{
			int num = ((ammoItem != null && ammoItem.parent == base.inventory) ? ammoItem.amount : 0);
			bool flag = lastAmmoCount < lowAmmoThreshold;
			bool flag2 = num < lowAmmoThreshold;
			if (num != lastAmmoCount && flag != flag2)
			{
				MarkIODirty();
			}
			lastAmmoCount = num;
		}
		if (HasValidTarget() || IsDead())
		{
			return;
		}
		List<ISamSiteTarget> obj = Facepunch.Pool.Get<List<ISamSiteTarget>>();
		if (!IsInDefenderMode())
		{
			AddTargetSet(obj, targetTypeVehicle.scanRadius);
		}
		AddMLRSRockets(obj, targetTypeMissile.scanRadius);
		ISamSiteTarget samSiteTarget = null;
		foreach (ISamSiteTarget item in obj)
		{
			if (!item.isClient && !(item.CenterPoint().y < eyePoint.transform.position.y) && item.IsVisible(eyePoint.transform.position, item.SAMTargetType.scanRadius * 2f) && item.IsValidSAMTarget(staticRespawn))
			{
				samSiteTarget = item;
				break;
			}
		}
		if (!samSiteTarget.IsUnityNull() && currentTarget != samSiteTarget)
		{
			lockOnTime = UnityEngine.Time.time + 0.5f;
		}
		SetTarget(samSiteTarget);
		if (!currentTarget.IsUnityNull())
		{
			lastTargetVisibleTime = UnityEngine.Time.time;
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		if (currentTarget.IsUnityNull())
		{
			CancelInvoke(WeaponTick);
		}
		else
		{
			InvokeRandomized(WeaponTick, 0f, 0.5f, 0.2f);
		}
	}

	public virtual bool HasAmmo()
	{
		if (!staticRespawn)
		{
			if (ammoItem != null && ammoItem.amount > 0)
			{
				return ammoItem.parent == base.inventory;
			}
			return false;
		}
		return true;
	}

	public void Reload()
	{
		if (staticRespawn)
		{
			return;
		}
		for (int i = 0; i < base.inventory.itemList.Count; i++)
		{
			Item item = base.inventory.itemList[i];
			if (item != null && item.info.itemid == ammoType.itemid && item.amount > 0)
			{
				ammoItem = item;
				return;
			}
		}
		ammoItem = null;
	}

	public void EnsureReloaded()
	{
		if (!HasAmmo())
		{
			Reload();
		}
	}

	public bool IsReloading()
	{
		return IsInvoking(Reload);
	}

	public void WeaponTick()
	{
		if (IsDead() || UnityEngine.Time.time < lockOnTime || UnityEngine.Time.time < nextBurstTime)
		{
			return;
		}
		if (!IsPowered())
		{
			firedCount = 0;
			return;
		}
		if (firedCount >= 6)
		{
			float timeBetweenBursts = mostRecentTargetType.timeBetweenBursts;
			nextBurstTime = UnityEngine.Time.time + timeBetweenBursts;
			firedCount = 0;
			return;
		}
		EnsureReloaded();
		if (HasAmmo())
		{
			bool num = ammoItem != null && ammoItem.amount == lowAmmoThreshold;
			if (!staticRespawn && ammoItem != null)
			{
				ammoItem.UseItem();
			}
			firedCount++;
			float speedMultiplier = 1f;
			if (!currentTarget.IsUnityNull())
			{
				speedMultiplier = currentTarget.SAMTargetType.speedMultiplier;
			}
			FireProjectile(tubes[currentTubeIndex].position, currentAimDir, speedMultiplier);
			Effect.server.Run(muzzleFlashTest.resourcePath, this, StringPool.Get("Tube " + (currentTubeIndex + 1)), Vector3.zero, Vector3.up);
			currentTubeIndex++;
			if (currentTubeIndex >= tubes.Length)
			{
				currentTubeIndex = 0;
			}
			if (num)
			{
				MarkIODirty();
			}
		}
	}

	public void FireProjectile(Vector3 origin, Vector3 direction, float speedMultiplier)
	{
		BaseEntity baseEntity = GameManager.server.CreateEntity(projectileTest.resourcePath, origin, Quaternion.LookRotation(direction, Vector3.up));
		if (!(baseEntity == null))
		{
			baseEntity.creatorEntity = this;
			ServerProjectile component = baseEntity.GetComponent<ServerProjectile>();
			if ((bool)component)
			{
				component.InitializeVelocity(GetInheritedProjectileVelocity(direction) + direction * component.speed * speedMultiplier);
			}
			baseEntity.Spawn();
		}
	}

	public override int GetPassthroughAmount(int outputSlot = 0)
	{
		int result = Mathf.Min(1, GetCurrentEnergy());
		switch (outputSlot)
		{
		case 0:
			if (currentTarget.IsUnityNull())
			{
				return 0;
			}
			return result;
		case 1:
			if (ammoItem == null || ammoItem.amount >= lowAmmoThreshold || ammoItem.parent != base.inventory)
			{
				return 0;
			}
			return result;
		case 2:
			if (HasAmmo())
			{
				return 0;
			}
			return result;
		default:
			return GetCurrentEnergy();
		}
	}

	[RPC_Server]
	[RPC_Server.IsVisible(3f)]
	[RPC_Server.CallsPerSecond(1uL)]
	private void ToggleDefenderMode(RPCMessage msg)
	{
		if (staticRespawn)
		{
			return;
		}
		BasePlayer player = msg.player;
		if (!(player == null) && player.CanBuild())
		{
			bool flag = msg.read.Bit();
			if (flag != IsInDefenderMode())
			{
				SetFlag(Flag_ManuallySetMode, flag);
				SetFlag(Flag_TargetMode, flag);
			}
		}
	}

	public override void UpdateHasPower(int inputAmount, int inputSlot)
	{
		if (inputSlot == 0)
		{
			base.UpdateHasPower(inputAmount, inputSlot);
		}
	}

	public override void UpdateFromInput(int inputAmount, int inputSlot)
	{
		switch (inputSlot)
		{
		case 0:
			base.UpdateFromInput(inputAmount, inputSlot);
			break;
		case 1:
			if (input1Amount != inputAmount)
			{
				bool flag = HasFlag(Flag_ManuallySetMode);
				SetFlag(Flag_TargetMode, (inputAmount == 0) ? flag : (!flag));
			}
			input1Amount = inputAmount;
			break;
		}
	}
}
