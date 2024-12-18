using Facepunch;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;

public class HitInfo : Pool.IPooled
{
	public BaseEntity Initiator;

	public BaseEntity WeaponPrefab;

	public AttackEntity Weapon;

	public bool DoHitEffects = true;

	public bool DoDecals = true;

	public bool IsPredicting;

	public bool UseProtection = true;

	public Connection Predicted;

	public bool DidHit;

	public BaseEntity HitEntity;

	public uint HitBone;

	public uint HitPart;

	public uint HitMaterial;

	public Vector3 HitPositionWorld;

	public Vector3 HitPositionLocal;

	public Vector3 HitNormalWorld;

	public Vector3 HitNormalLocal;

	public Vector3 PointStart;

	public Vector3 PointEnd;

	public int ProjectileID;

	public int ProjectileHits;

	public float ProjectileDistance;

	public float ProjectileIntegrity;

	public float ProjectileTravelTime;

	public float ProjectileTrajectoryMismatch;

	public Vector3 ProjectileVelocity;

	public Projectile ProjectilePrefab;

	public PhysicMaterial material;

	public DamageProperties damageProperties;

	public DamageTypeList damageTypes = new DamageTypeList();

	public bool CanGather;

	public bool DidGather;

	public float gatherScale = 1f;

	public BasePlayer InitiatorPlayer
	{
		get
		{
			if (!Initiator)
			{
				return null;
			}
			return Initiator.ToPlayer();
		}
	}

	public Vector3 attackNormal => (PointEnd - PointStart).normalized;

	public bool hasDamage => damageTypes.Total() > 0f;

	public bool InitiatorParented
	{
		get
		{
			if (Initiator != null && Initiator.GetParentEntity() != null)
			{
				return Initiator.GetParentEntity().IsValid();
			}
			return false;
		}
	}

	public bool HitEntityParented
	{
		get
		{
			if (HitEntity != null && HitEntity.GetParentEntity() != null)
			{
				return HitEntity.GetParentEntity().IsValid();
			}
			return false;
		}
	}

	public bool isHeadshot
	{
		get
		{
			if (HitEntity == null)
			{
				return false;
			}
			BaseCombatEntity baseCombatEntity = HitEntity as BaseCombatEntity;
			if (baseCombatEntity == null)
			{
				return false;
			}
			if (baseCombatEntity.skeletonProperties == null)
			{
				return false;
			}
			SkeletonProperties.BoneProperty boneProperty = baseCombatEntity.skeletonProperties.FindBone(HitBone);
			if (boneProperty == null)
			{
				return false;
			}
			return boneProperty.area == HitArea.Head;
		}
	}

	public Translate.Phrase bonePhrase
	{
		get
		{
			if (HitEntity == null)
			{
				return null;
			}
			BaseCombatEntity baseCombatEntity = HitEntity as BaseCombatEntity;
			if (baseCombatEntity == null)
			{
				return null;
			}
			if (baseCombatEntity.skeletonProperties == null)
			{
				return null;
			}
			return baseCombatEntity.skeletonProperties.FindBone(HitBone)?.name;
		}
	}

	public string boneName
	{
		get
		{
			Translate.Phrase phrase = bonePhrase;
			if (phrase != null)
			{
				return phrase.english;
			}
			return "N/A";
		}
	}

	public HitArea boneArea
	{
		get
		{
			if (HitEntity == null)
			{
				return (HitArea)(-1);
			}
			BaseCombatEntity baseCombatEntity = HitEntity as BaseCombatEntity;
			if (baseCombatEntity == null)
			{
				return (HitArea)(-1);
			}
			return baseCombatEntity.SkeletonLookup(HitBone);
		}
	}

	public void EnterPool()
	{
		Clear();
	}

	public void LeavePool()
	{
	}

	public void Clear()
	{
		Initiator = null;
		WeaponPrefab = null;
		Weapon = null;
		DoHitEffects = true;
		DoDecals = true;
		IsPredicting = false;
		UseProtection = true;
		Predicted = null;
		DidHit = false;
		HitEntity = null;
		HitBone = 0u;
		HitPart = 0u;
		HitMaterial = 0u;
		HitPositionWorld = default(Vector3);
		HitPositionLocal = default(Vector3);
		HitNormalWorld = default(Vector3);
		HitNormalLocal = default(Vector3);
		PointStart = default(Vector3);
		PointEnd = default(Vector3);
		ProjectileID = 0;
		ProjectileHits = 0;
		ProjectileDistance = 0f;
		ProjectileIntegrity = 0f;
		ProjectileTravelTime = 0f;
		ProjectileTrajectoryMismatch = 0f;
		ProjectileVelocity = default(Vector3);
		ProjectilePrefab = null;
		material = null;
		damageProperties = null;
		damageTypes.Clear();
		CanGather = false;
		DidGather = false;
		gatherScale = 1f;
	}

	public void CopyFrom(HitInfo other)
	{
		Initiator = other.Initiator;
		WeaponPrefab = other.WeaponPrefab;
		Weapon = other.Weapon;
		DoHitEffects = other.DoHitEffects;
		DoDecals = other.DoDecals;
		IsPredicting = other.IsPredicting;
		UseProtection = other.UseProtection;
		Predicted = other.Predicted;
		DidHit = other.DidHit;
		HitEntity = other.HitEntity;
		HitBone = other.HitBone;
		HitPart = other.HitPart;
		HitMaterial = other.HitMaterial;
		HitPositionWorld = other.HitPositionWorld;
		HitPositionLocal = other.HitPositionLocal;
		HitNormalWorld = other.HitNormalWorld;
		HitNormalLocal = other.HitNormalLocal;
		PointStart = other.PointStart;
		PointEnd = other.PointEnd;
		ProjectileID = other.ProjectileID;
		ProjectileHits = other.ProjectileHits;
		ProjectileDistance = other.ProjectileDistance;
		ProjectileIntegrity = other.ProjectileIntegrity;
		ProjectileTravelTime = other.ProjectileTravelTime;
		ProjectileTrajectoryMismatch = other.ProjectileTrajectoryMismatch;
		ProjectileVelocity = other.ProjectileVelocity;
		ProjectilePrefab = other.ProjectilePrefab;
		material = other.material;
		damageProperties = other.damageProperties;
		for (int i = 0; i < damageTypes.types.Length; i++)
		{
			damageTypes.types[i] = other.damageTypes.types[i];
		}
		CanGather = other.CanGather;
		DidGather = other.DidGather;
		gatherScale = other.gatherScale;
	}

	public bool IsProjectile()
	{
		return ProjectileID != 0;
	}

	public void Init(BaseEntity attacker, BaseEntity target, DamageType type, float damageAmount, Vector3 vhitPosition)
	{
		Initiator = attacker;
		HitEntity = target;
		HitPositionWorld = vhitPosition;
		if (attacker != null)
		{
			PointStart = attacker.transform.position;
		}
		damageTypes.Add(type, damageAmount);
	}

	public HitInfo()
	{
	}

	public HitInfo(BaseEntity attacker, BaseEntity target, DamageType type, float damageAmount, Vector3 vhitPosition)
	{
		Init(attacker, target, type, damageAmount, vhitPosition);
	}

	public HitInfo(BaseEntity attacker, BaseEntity target, DamageType type, float damageAmount)
	{
		Init(attacker, target, type, damageAmount, target.transform.position);
	}

	public void LoadFromAttack(Attack attack, bool serverSide)
	{
		HitEntity = null;
		PointStart = attack.pointStart;
		PointEnd = attack.pointEnd;
		if (attack.hitID.IsValid)
		{
			DidHit = true;
			if (serverSide)
			{
				HitEntity = BaseNetworkable.serverEntities.Find(attack.hitID) as BaseEntity;
			}
			if ((bool)HitEntity)
			{
				HitBone = attack.hitBone;
				HitPart = attack.hitPartID;
			}
		}
		DidHit = true;
		HitPositionLocal = attack.hitPositionLocal;
		HitPositionWorld = attack.hitPositionWorld;
		HitNormalLocal = attack.hitNormalLocal.normalized;
		HitNormalWorld = attack.hitNormalWorld.normalized;
		HitMaterial = attack.hitMaterialID;
		if (attack.srcParentID.IsValid)
		{
			BaseEntity baseEntity = null;
			if (serverSide)
			{
				baseEntity = BaseNetworkable.serverEntities.Find(attack.srcParentID) as BaseEntity;
			}
			if (baseEntity.IsValid())
			{
				PointStart = baseEntity.transform.TransformPoint(PointStart);
			}
		}
		if (attack.dstParentID.IsValid)
		{
			BaseEntity baseEntity2 = null;
			if (serverSide)
			{
				baseEntity2 = BaseNetworkable.serverEntities.Find(attack.dstParentID) as BaseEntity;
			}
			if (baseEntity2.IsValid())
			{
				PointEnd = baseEntity2.transform.TransformPoint(PointEnd);
				HitPositionWorld = baseEntity2.transform.TransformPoint(HitPositionWorld);
				HitNormalWorld = baseEntity2.transform.TransformDirection(HitNormalWorld);
			}
		}
	}

	public Vector3 PositionOnRay(Vector3 position)
	{
		Ray ray = new Ray(PointStart, attackNormal);
		if (ProjectilePrefab == null)
		{
			return ray.ClosestPoint(position);
		}
		if (new Sphere(position, ProjectilePrefab.thickness).Trace(ray, out var hit))
		{
			return hit.point;
		}
		return position;
	}

	public Vector3 HitPositionOnRay()
	{
		return PositionOnRay(HitPositionWorld);
	}

	public bool IsNaNOrInfinity()
	{
		if (PointStart.IsNaNOrInfinity())
		{
			return true;
		}
		if (PointEnd.IsNaNOrInfinity())
		{
			return true;
		}
		if (HitPositionWorld.IsNaNOrInfinity())
		{
			return true;
		}
		if (HitPositionLocal.IsNaNOrInfinity())
		{
			return true;
		}
		if (HitNormalWorld.IsNaNOrInfinity())
		{
			return true;
		}
		if (HitNormalLocal.IsNaNOrInfinity())
		{
			return true;
		}
		if (ProjectileVelocity.IsNaNOrInfinity())
		{
			return true;
		}
		if (float.IsNaN(ProjectileDistance))
		{
			return true;
		}
		if (float.IsInfinity(ProjectileDistance))
		{
			return true;
		}
		if (float.IsNaN(ProjectileIntegrity))
		{
			return true;
		}
		if (float.IsInfinity(ProjectileIntegrity))
		{
			return true;
		}
		if (float.IsNaN(ProjectileTravelTime))
		{
			return true;
		}
		if (float.IsInfinity(ProjectileTravelTime))
		{
			return true;
		}
		if (float.IsNaN(ProjectileTrajectoryMismatch))
		{
			return true;
		}
		if (float.IsInfinity(ProjectileTrajectoryMismatch))
		{
			return true;
		}
		return false;
	}
}
