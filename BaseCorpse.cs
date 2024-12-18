using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using ProtoBuf;
using UnityEngine;

public class BaseCorpse : BaseCombatEntity
{
	public GameObjectRef prefabRagdoll;

	public BaseEntity parentEnt;

	[NonSerialized]
	internal ResourceDispenser resourceDispenser;

	public const float CORPSE_SLEEP_THRESHOLD = 0.05f;

	protected Rigidbody rigidBody;

	public bool blockDamageIfNotGather;

	[NonSerialized]
	public SpawnGroup spawnGroup;

	private const float RAGDOLL_PUSH_DIST = 0.5f;

	private const float RAGDOLL_PUSH_FORCE = 2.5f;

	public virtual bool CorpseIsRagdoll => false;

	public bool IsSleeping
	{
		get
		{
			if (rigidBody != null)
			{
				return rigidBody.IsSleeping();
			}
			return false;
		}
	}

	public override TraitFlag Traits => base.Traits | TraitFlag.Food | TraitFlag.Meat;

	public override void ResetState()
	{
		spawnGroup = null;
		base.ResetState();
	}

	public override void ServerInit()
	{
		base.ServerInit();
		rigidBody = SetupRigidBody();
		ResetRemovalTime();
		resourceDispenser = GetComponent<ResourceDispenser>();
		SingletonComponent<NpcFoodManager>.Instance.Add(this);
	}

	public virtual void ServerInitCorpse(BaseEntity pr, Vector3 posOnDeah, Quaternion rotOnDeath, BasePlayer.PlayerFlags playerFlagsOnDeath, ModelState modelState)
	{
		parentEnt = pr;
		base.transform.SetPositionAndRotation(parentEnt.CenterPoint(), parentEnt.transform.rotation);
		SpawnPointInstance component = GetComponent<SpawnPointInstance>();
		if (component != null)
		{
			spawnGroup = component.parentSpawnPointUser as SpawnGroup;
		}
	}

	public virtual bool CanRemove()
	{
		return true;
	}

	public void RemoveCorpse()
	{
		if (!CanRemove())
		{
			ResetRemovalTime();
		}
		else
		{
			Kill();
		}
	}

	public override void DestroyShared()
	{
		base.DestroyShared();
		if (base.isServer)
		{
			SingletonComponent<NpcFoodManager>.Instance.Remove(this);
		}
	}

	public void ResetRemovalTime(float dur)
	{
		using (TimeWarning.New("ResetRemovalTime"))
		{
			if (IsInvoking(RemoveCorpse))
			{
				CancelInvoke(RemoveCorpse);
			}
			Invoke(RemoveCorpse, dur);
		}
	}

	public virtual float GetRemovalTime()
	{
		BaseGameMode activeGameMode = BaseGameMode.GetActiveGameMode(serverside: true);
		if (activeGameMode != null)
		{
			return activeGameMode.CorpseRemovalTime(this);
		}
		return Server.corpsedespawn;
	}

	public void ResetRemovalTime()
	{
		ResetRemovalTime(GetRemovalTime());
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.corpse = Facepunch.Pool.Get<Corpse>();
		if (parentEnt.IsValid())
		{
			info.msg.corpse.parentID = parentEnt.net.ID;
		}
	}

	public void TakeChildren(BaseEntity takeChildrenFrom)
	{
		if (takeChildrenFrom.children == null)
		{
			return;
		}
		using (TimeWarning.New("Corpse.TakeChildren"))
		{
			BaseEntity[] array = takeChildrenFrom.children.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				array[i].SwitchParent(this);
			}
		}
	}

	public override void ApplyInheritedVelocity(Vector3 velocity)
	{
	}

	private Rigidbody SetupRigidBody()
	{
		if (!prefabRagdoll.isValid)
		{
			return GetComponent<Rigidbody>();
		}
		if (base.isServer)
		{
			GameObject gameObject = base.gameManager.FindPrefab(prefabRagdoll.resourcePath);
			if (gameObject == null)
			{
				return null;
			}
			Ragdoll component = gameObject.GetComponent<Ragdoll>();
			if (component == null)
			{
				return null;
			}
			if (component.primaryBody == null)
			{
				Debug.LogError("[BaseCorpse] ragdoll.primaryBody isn't set!" + component.gameObject.name);
				return null;
			}
			if (base.gameObject.GetComponent<Collider>() == null)
			{
				BoxCollider component2 = component.primaryBody.GetComponent<BoxCollider>();
				if (component2 == null)
				{
					Debug.LogError("Ragdoll has unsupported primary collider (make it supported) ", component);
					return null;
				}
				BoxCollider boxCollider = base.gameObject.AddComponent<BoxCollider>();
				boxCollider.size = component2.size * 2f;
				boxCollider.center = component2.center;
				boxCollider.sharedMaterial = component2.sharedMaterial;
			}
		}
		Rigidbody rigidbody = GetComponent<Rigidbody>();
		if (rigidbody == null)
		{
			rigidbody = base.gameObject.AddComponent<Rigidbody>();
			rigidbody.mass = 10f;
			rigidbody.drag = 0.5f;
			rigidbody.angularDrag = 0.5f;
		}
		rigidbody.useGravity = true;
		rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
		rigidbody.sleepThreshold = Mathf.Max(0.05f, UnityEngine.Physics.sleepThreshold);
		if (base.isServer)
		{
			Buoyancy component3 = GetComponent<Buoyancy>();
			if (component3 != null)
			{
				component3.rigidBody = rigidbody;
			}
			Vector3 velocity = Vector3Ex.Range(-1f, 1f);
			velocity.y += 1f;
			rigidbody.velocity = velocity;
			rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
			rigidbody.angularVelocity = Vector3Ex.Range(-10f, 10f);
		}
		return rigidbody;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.corpse != null)
		{
			Load(info.msg.corpse);
		}
	}

	private void Load(Corpse corpse)
	{
		if (base.isServer)
		{
			parentEnt = BaseNetworkable.serverEntities.Find(corpse.parentID) as BaseEntity;
		}
		_ = base.isClient;
	}

	public override void OnAttacked(HitInfo info)
	{
		if (!base.isServer)
		{
			return;
		}
		ResetRemovalTime();
		if (!blockDamageIfNotGather || !(info.Weapon is BaseMelee baseMelee) || baseMelee.GetGatherInfoFromIndex(ResourceDispenser.GatherType.Flesh).gatherDamage != 0f)
		{
			if ((bool)resourceDispenser)
			{
				resourceDispenser.DoGather(info, this);
			}
			if (!info.DidGather)
			{
				base.OnAttacked(info);
			}
			if (CorpseIsRagdoll)
			{
				PushRagdoll(info);
			}
		}
	}

	protected virtual void PushRagdoll(HitInfo info)
	{
		List<Rigidbody> obj = Facepunch.Pool.Get<List<Rigidbody>>();
		Vis.Components(info.HitPositionWorld, 0.5f, obj, 512);
		PushRigidbodies(obj, info.HitPositionWorld, info.attackNormal);
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	protected void PushRigidbodies(List<Rigidbody> rbs, Vector3 hitPos, Vector3 hitNormal)
	{
		foreach (Rigidbody rb in rbs)
		{
			float value = Vector3.Distance(hitPos, rb.position);
			float num = 1f - Mathf.InverseLerp(0f, 0.5f, value);
			if (!(num <= 0f))
			{
				if (num < 0.5f)
				{
					num = 0.5f;
				}
				rb.AddForceAtPosition(hitNormal * 2.5f * num, hitPos, ForceMode.Impulse);
			}
		}
	}

	public override string Categorize()
	{
		return "corpse";
	}

	public override void Eat(BaseNpc baseNpc, float timeSpent)
	{
		ResetRemovalTime();
		Hurt(timeSpent * 5f);
		baseNpc.AddCalories(timeSpent * 2f);
	}

	public override bool ShouldInheritNetworkGroup()
	{
		return false;
	}
}
