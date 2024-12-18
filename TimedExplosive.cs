using System;
using System.Collections.Generic;
using Facepunch;
using Facepunch.Rust;
using Rust;
using UnityEngine;

public class TimedExplosive : BaseEntity, ServerProjectile.IProjectileImpact
{
	public enum ExplosionEffectOffsetMode
	{
		Local,
		World
	}

	[Header("General")]
	public float timerAmountMin = 10f;

	public float timerAmountMax = 20f;

	public float minExplosionRadius;

	public float explosionRadius = 10f;

	public bool explodeOnContact;

	public bool canStick;

	public bool onlyDamageParent;

	[Header("AI")]
	public bool IgnoreAI;

	public bool BlindAI;

	public float aiBlindDuration = 2.5f;

	public float aiBlindRange = 4f;

	[Header("Offsets")]
	public ExplosionEffectOffsetMode explosionOffsetMode;

	public Vector3 explosionEffectOffset = Vector3.zero;

	[Header("Normals")]
	public bool explosionMatchesNormal;

	public bool explosionUsesForward;

	public bool explosionMatchesOrientation;

	[Header("Effects")]
	public GameObjectRef explosionEffect;

	[Tooltip("Optional: Will fall back to watersurfaceExplosionEffect or explosionEffect if not assigned.")]
	public GameObjectRef underwaterExplosionEffect;

	public GameObjectRef stickEffect;

	public GameObjectRef bounceEffect;

	public GameObjectRef watersurfaceExplosionEffect;

	[Header("Water")]
	[Min(0f)]
	public float underwaterExplosionDepth = 1f;

	[Tooltip("Optional: Will fall back to underwaterExplosionEffect or explosionEffect if not assigned.")]
	[MinMax(0f, 100f)]
	public MinMax watersurfaceExplosionDepth = new MinMax(0.5f, 10f);

	public bool waterCausesExplosion;

	[Header("Other")]
	public int vibrationLevel = 3;

	public List<DamageTypeEntry> damageTypes = new List<DamageTypeEntry>();

	[NonSerialized]
	private float lastBounceTime;

	private bool hadRB;

	private float rbMass;

	private float rbDrag;

	private float rbAngularDrag;

	private CollisionDetectionMode rbCollisionMode;

	private const int parentOnlySplashDamage = 166144;

	private const int fullSplashDamage = 1210222849;

	private Vector3? hitNormal;

	private static BaseEntity[] queryResults = new BaseEntity[64];

	private Vector3 lastPosition = Vector3.zero;

	protected override bool PositionTickFixedTime => true;

	protected virtual bool AlwaysRunWaterCheck => false;

	public void SetDamageScale(float scale)
	{
		foreach (DamageTypeEntry damageType in damageTypes)
		{
			damageType.amount *= scale;
		}
	}

	public override float GetNetworkTime()
	{
		return Time.fixedTime;
	}

	public override void ServerInit()
	{
		lastBounceTime = Time.time;
		base.ServerInit();
		SetFuse(GetRandomTimerTime());
		if (base.transform.HasComponent<Collider>())
		{
			ReceiveCollisionMessages(b: true);
		}
		if (waterCausesExplosion || AlwaysRunWaterCheck)
		{
			InvokeRepeating(WaterCheck, 0f, 0.5f);
		}
	}

	public virtual void WaterCheck()
	{
		if (waterCausesExplosion && WaterFactor() >= 0.5f)
		{
			Explode();
		}
	}

	public virtual void SetFuse(float fuseLength)
	{
		if (base.isServer)
		{
			Invoke(Explode, fuseLength);
			SetFlag(Flags.Reserved2, b: true);
		}
	}

	public virtual float GetRandomTimerTime()
	{
		return UnityEngine.Random.Range(timerAmountMin, timerAmountMax);
	}

	public virtual void ProjectileImpact(RaycastHit info, Vector3 rayOrigin)
	{
		hitNormal = info.normal;
		Explode();
	}

	public void ForceExplode()
	{
		if (this is DudTimedExplosive dudTimedExplosive)
		{
			dudTimedExplosive.dudChance = 0f;
		}
		if (this is RFTimedExplosive rFTimedExplosive)
		{
			rFTimedExplosive.DisarmRF();
		}
		Explode();
	}

	public virtual void Explode()
	{
		Explode(PivotPoint());
	}

	private Vector3 GetExplosionNormal()
	{
		Vector3 result;
		if (explosionUsesForward)
		{
			result = base.transform.forward;
		}
		else if (explosionMatchesOrientation)
		{
			Quaternion rotation = base.transform.rotation;
			Vector3 forward = Vector3.forward;
			result = rotation * forward;
		}
		else
		{
			result = Vector3.up;
		}
		if (explosionMatchesNormal && hitNormal.HasValue)
		{
			result = hitNormal.Value;
		}
		return result;
	}

	public virtual void Explode(Vector3 explosionFxPos)
	{
		Analytics.Azure.OnExplosion(this);
		Collider component = GetComponent<Collider>();
		if ((bool)component)
		{
			component.enabled = false;
		}
		WaterLevel.WaterInfo waterInfo = WaterLevel.GetWaterInfo(explosionFxPos - new Vector3(0f, 0.25f, 0f), waves: true, volumes: true);
		if (underwaterExplosionEffect.isValid && waterInfo.isValid && waterInfo.currentDepth >= underwaterExplosionDepth)
		{
			Effect.server.Run(underwaterExplosionEffect.resourcePath, explosionFxPos, GetExplosionNormal(), null, broadcast: true);
		}
		else if (explosionEffect.isValid)
		{
			Vector3 posWorld = explosionFxPos;
			if (explosionOffsetMode == ExplosionEffectOffsetMode.Local)
			{
				Vector3 vector = base.transform.TransformPoint(explosionEffectOffset) - base.transform.position;
				posWorld += vector;
			}
			if (explosionOffsetMode == ExplosionEffectOffsetMode.World)
			{
				posWorld += explosionEffectOffset;
			}
			Effect.server.Run(explosionEffect.resourcePath, posWorld, GetExplosionNormal(), null, broadcast: true);
		}
		if (watersurfaceExplosionEffect.isValid && waterInfo.isValid && waterInfo.overallDepth >= watersurfaceExplosionDepth.x && waterInfo.currentDepth <= watersurfaceExplosionDepth.y)
		{
			Effect.server.Run(watersurfaceExplosionEffect.resourcePath, explosionFxPos.WithY(waterInfo.surfaceLevel), GetExplosionNormal(), null, broadcast: true);
		}
		if (damageTypes.Count > 0)
		{
			Vector3 vector2 = ExplosionCenter();
			if (onlyDamageParent)
			{
				DamageUtil.RadiusDamage(creatorEntity, LookupPrefab(), vector2, minExplosionRadius, explosionRadius, damageTypes, 166144, useLineOfSight: true, IgnoreAI);
				BaseEntity baseEntity = GetParentEntity();
				BaseCombatEntity baseCombatEntity = baseEntity as BaseCombatEntity;
				while (baseCombatEntity == null && baseEntity != null && baseEntity.HasParent())
				{
					baseEntity = baseEntity.GetParentEntity();
					baseCombatEntity = baseEntity as BaseCombatEntity;
				}
				if (baseEntity == null || !baseEntity.gameObject.IsOnLayer(Layer.Construction))
				{
					List<BuildingBlock> obj = Pool.Get<List<BuildingBlock>>();
					Vis.Entities(vector2, explosionRadius, obj, 2097152, QueryTriggerInteraction.Ignore);
					BuildingBlock buildingBlock = null;
					float num = float.PositiveInfinity;
					foreach (BuildingBlock item in obj)
					{
						if (!item.isClient && !item.IsDestroyed && !(item.healthFraction <= 0f))
						{
							float num2 = Vector3.Distance(item.ClosestPoint(vector2), vector2);
							if (num2 < num && item.IsVisible(vector2, explosionRadius))
							{
								buildingBlock = item;
								num = num2;
							}
						}
					}
					if ((bool)buildingBlock)
					{
						HitInfo hitInfo = new HitInfo();
						hitInfo.Initiator = creatorEntity;
						hitInfo.WeaponPrefab = LookupPrefab();
						hitInfo.damageTypes.Add(damageTypes);
						hitInfo.PointStart = vector2;
						hitInfo.PointEnd = buildingBlock.transform.position;
						float amount = 1f - Mathf.Clamp01((num - minExplosionRadius) / (explosionRadius - minExplosionRadius));
						hitInfo.damageTypes.ScaleAll(amount);
						buildingBlock.Hurt(hitInfo);
					}
					Pool.FreeUnmanaged(ref obj);
				}
				if ((bool)baseCombatEntity)
				{
					HitInfo hitInfo2 = new HitInfo();
					hitInfo2.Initiator = creatorEntity;
					hitInfo2.WeaponPrefab = LookupPrefab();
					hitInfo2.damageTypes.Add(damageTypes);
					baseCombatEntity.Hurt(hitInfo2);
				}
				else if (baseEntity != null)
				{
					HitInfo hitInfo3 = new HitInfo();
					hitInfo3.Initiator = creatorEntity;
					hitInfo3.WeaponPrefab = LookupPrefab();
					hitInfo3.damageTypes.Add(damageTypes);
					hitInfo3.PointStart = vector2;
					hitInfo3.PointEnd = baseEntity.transform.position;
					baseEntity.OnAttacked(hitInfo3);
				}
			}
			else
			{
				DamageUtil.RadiusDamage(creatorEntity, LookupPrefab(), vector2, minExplosionRadius, explosionRadius, damageTypes, 1210222849, useLineOfSight: true, IgnoreAI);
			}
			SeismicSensor.Notify(vector2, vibrationLevel);
			BlindAnyAI();
		}
		if (!base.IsDestroyed && !HasFlag(Flags.Broken))
		{
			Kill(DestroyMode.Gib);
		}
	}

	private Vector3 ExplosionCenter()
	{
		if (IsStuck() && parentEntity.Get(base.isServer) is BaseVehicle)
		{
			OBB oBB = WorldSpaceBounds();
			return CenterPoint() - oBB.forward * (oBB.extents.z + 0.1f);
		}
		return CenterPoint();
	}

	private void BlindAnyAI()
	{
		if (!BlindAI)
		{
			return;
		}
		int brainsInSphereFast = Query.Server.GetBrainsInSphereFast(base.transform.position, 10f, queryResults);
		for (int i = 0; i < brainsInSphereFast; i++)
		{
			BaseEntity baseEntity = queryResults[i];
			if (Vector3.Distance(base.transform.position, baseEntity.transform.position) > aiBlindRange)
			{
				continue;
			}
			BaseAIBrain component = baseEntity.GetComponent<BaseAIBrain>();
			if (!(component == null))
			{
				BaseEntity brainBaseEntity = component.GetBrainBaseEntity();
				if (!(brainBaseEntity == null) && brainBaseEntity.IsVisible(CenterPoint()))
				{
					float blinded = aiBlindDuration * component.BlindDurationMultiplier * UnityEngine.Random.Range(0.6f, 1.4f);
					component.SetBlinded(blinded);
					queryResults[i] = null;
				}
			}
		}
	}

	public void FixedUpdate()
	{
		CheckClippingThroughWalls();
	}

	private void CheckClippingThroughWalls()
	{
		if (!canStick)
		{
			return;
		}
		if (lastPosition == default(Vector3) || !parentEntity.IsValid(serverside: true))
		{
			lastPosition = CenterPoint();
			return;
		}
		Vector3 vector = lastPosition;
		Vector3 vector2 = CenterPoint();
		Vector3 vector3 = vector2 - vector;
		lastPosition = vector2;
		if (vector == vector2 || !IsStuck(bypassColliderCheck: true))
		{
			return;
		}
		Ray ray = new Ray(vector, vector2 - vector);
		List<RaycastHit> list = Pool.Get<List<RaycastHit>>();
		GamePhysics.TraceAll(ray, 0f, list, Vector3.Distance(vector2, vector), 2097152);
		foreach (RaycastHit item in list)
		{
			if (item.GetEntity() as BuildingBlock != null)
			{
				base.transform.position -= vector3;
				ForceExplode();
				break;
			}
		}
	}

	public override void OnCollision(Collision collision, BaseEntity hitEntity)
	{
		if (canStick && !IsStuck())
		{
			bool flag = true;
			if ((bool)hitEntity)
			{
				flag = CanStickTo(hitEntity);
				if (!flag)
				{
					Collider component = GetComponent<Collider>();
					if (collision.collider != null && component != null)
					{
						Physics.IgnoreCollision(collision.collider, component);
					}
				}
			}
			if (flag)
			{
				DoCollisionStick(collision, hitEntity);
			}
		}
		if (explodeOnContact && !IsBusy())
		{
			SetMotionEnabled(wantsMotion: false);
			SetFlag(Flags.Busy, b: true, recursive: false, networkupdate: false);
			Invoke(Explode, 0.015f);
		}
		else
		{
			DoBounceEffect();
		}
	}

	public virtual bool CanStickTo(BaseEntity entity)
	{
		if (entity.TryGetComponent<DecorDeployable>(out var _))
		{
			return false;
		}
		if (entity is Drone)
		{
			return false;
		}
		if (entity is TravellingVendor)
		{
			return false;
		}
		return true;
	}

	private void DoBounceEffect()
	{
		if (!bounceEffect.isValid || Time.time - lastBounceTime < 0.2f)
		{
			return;
		}
		Rigidbody component = GetComponent<Rigidbody>();
		if (!component || !(component.velocity.magnitude < 1f))
		{
			if (bounceEffect.isValid)
			{
				Effect.server.Run(bounceEffect.resourcePath, base.transform.position, Vector3.up, null, broadcast: true);
			}
			lastBounceTime = Time.time;
		}
	}

	private void DoCollisionStick(Collision collision, BaseEntity ent)
	{
		ContactPoint contact = collision.GetContact(0);
		DoStick(contact.point, contact.normal, ent, collision.collider);
	}

	public virtual void SetMotionEnabled(bool wantsMotion)
	{
		Rigidbody component = GetComponent<Rigidbody>();
		if (wantsMotion)
		{
			if (component == null && hadRB)
			{
				component = base.gameObject.AddComponent<Rigidbody>();
				component.mass = rbMass;
				component.drag = rbDrag;
				component.angularDrag = rbAngularDrag;
				component.collisionDetectionMode = rbCollisionMode;
				component.useGravity = true;
				component.isKinematic = false;
			}
		}
		else if (component != null)
		{
			hadRB = true;
			rbMass = component.mass;
			rbDrag = component.drag;
			rbAngularDrag = component.angularDrag;
			rbCollisionMode = component.collisionDetectionMode;
			UnityEngine.Object.Destroy(component);
		}
	}

	public bool IsStuck(bool bypassColliderCheck = false)
	{
		Rigidbody component = GetComponent<Rigidbody>();
		if ((bool)component && !component.isKinematic)
		{
			return false;
		}
		if (!bypassColliderCheck)
		{
			Collider component2 = GetComponent<Collider>();
			if ((bool)component2 && component2.enabled)
			{
				return false;
			}
		}
		return parentEntity.IsValid(serverside: true);
	}

	public void DoStick(Vector3 position, Vector3 normal, BaseEntity ent, Collider collider)
	{
		if (ent == null)
		{
			return;
		}
		if (ent is TimedExplosive)
		{
			if (!ent.HasParent())
			{
				return;
			}
			position = ent.transform.position;
			ent = ent.parentEntity.Get(serverside: true);
		}
		SetMotionEnabled(wantsMotion: false);
		if (!HasChild(ent))
		{
			base.transform.position = position;
			base.transform.rotation = Quaternion.LookRotation(normal, base.transform.up);
			if (collider != null)
			{
				SetParent(ent, ent.FindBoneID(collider.transform), worldPositionStays: true);
			}
			else
			{
				SetParent(ent, StringPool.closest, worldPositionStays: true);
			}
			if (ent is BaseCombatEntity baseCombatEntity)
			{
				baseCombatEntity.SetJustAttacked();
			}
			if (stickEffect.isValid)
			{
				Effect.server.Run(stickEffect.resourcePath, base.transform.position, Vector3.up, null, broadcast: true);
			}
			ReceiveCollisionMessages(b: false);
		}
	}

	private void UnStick()
	{
		if ((bool)GetParentEntity())
		{
			SetParent(null, worldPositionStays: true, sendImmediate: true);
			SetMotionEnabled(wantsMotion: true);
			if (base.transform.HasComponent<Collider>())
			{
				ReceiveCollisionMessages(b: true);
			}
		}
	}

	internal override void OnParentRemoved()
	{
		UnStick();
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		if (parentEntity.IsValid(serverside: true))
		{
			DoStick(base.transform.position, base.transform.forward, parentEntity.Get(serverside: true), null);
		}
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.explosive != null)
		{
			parentEntity.uid = info.msg.explosive.parentid;
		}
	}

	public virtual void SetCollisionEnabled(bool wantsCollision)
	{
		Collider component = GetComponent<Collider>();
		if ((bool)component && component.enabled != wantsCollision)
		{
			component.enabled = wantsCollision;
		}
	}
}
