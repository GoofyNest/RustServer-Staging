using System;
using ConVar;
using Facepunch.Rust;
using UnityEngine;

public class DroppedItem : WorldItem, IContainerSounds
{
	public enum DropReasonEnum
	{
		Unknown,
		Player,
		Death,
		Loot
	}

	public class DroppedItemUnderwaterQueue : PersistentObjectWorkQueue<DroppedItem>
	{
		protected override void RunJob(DroppedItem entity)
		{
			if (entity != null)
			{
				entity.CheckUnderwaterStatus(canSplash: true);
			}
		}
	}

	[Header("DroppedItem")]
	public GameObjectRef itemModel;

	public GameObjectRef splashEffect;

	[ServerVar(Help = "How many milliseconds to spend on updating underwater drag levels")]
	public static float underwater_drag_budget_ms = 0.1f;

	private const Flags FLAG_STUCK = Flags.Reserved1;

	private const Flags FLAG_UNDERWATER = Flags.Reserved2;

	private int originalLayer = -1;

	[NonSerialized]
	public DropReasonEnum DropReason;

	[NonSerialized]
	public ulong DroppedBy;

	[NonSerialized]
	public DateTime DroppedTime;

	[NonSerialized]
	public bool NeverCombine;

	private Rigidbody rB;

	private CollisionDetectionMode originalCollisionMode;

	private Vector3 prevLocalPos;

	private const float SLEEP_CHECK_FREQUENCY = 11f;

	private const float AIR_DRAG = 0.1f;

	private const float UNDERWATER_DRAG = 7f;

	private bool hasLastPos;

	private Vector3 lastGoodColliderCentre;

	private Vector3 lastGoodPos;

	private Quaternion lastGoodRot;

	private Action cachedSleepCheck;

	private float maxBoundsExtent;

	private readonly Vector3 smallVerticalOffset = new Vector3(0f, 0.05f, 0f);

	public static DroppedItemUnderwaterQueue underwaterStatusQueue = new DroppedItemUnderwaterQueue();

	private TimeSince lastUnderwaterFlowImpulse;

	public Collider childCollider { get; private set; }

	private bool StuckInSomething => HasFlag(Flags.Reserved1);

	public SoundDefinition OpenSound
	{
		get
		{
			if (item == null)
			{
				return null;
			}
			ItemModContainer component = item.info.GetComponent<ItemModContainer>();
			if (component == null)
			{
				return null;
			}
			return component.openSound;
		}
	}

	public SoundDefinition CloseSound
	{
		get
		{
			if (item == null)
			{
				return null;
			}
			ItemModContainer component = item.info.GetComponent<ItemModContainer>();
			if (component == null)
			{
				return null;
			}
			return component.closeSound;
		}
	}

	public Rigidbody Rigidbody => rB;

	public bool IsSleeping
	{
		get
		{
			if (rB != null)
			{
				return rB.IsSleeping();
			}
			return false;
		}
	}

	public override float GetNetworkTime()
	{
		return UnityEngine.Time.fixedTime;
	}

	public override void ServerInit()
	{
		base.ServerInit();
		if (GetDespawnDuration() < float.PositiveInfinity)
		{
			Invoke(IdleDestroy, GetDespawnDuration());
		}
		ReceiveCollisionMessages(b: true);
		prevLocalPos = base.transform.localPosition;
		underwaterStatusQueue.Add(this);
	}

	internal override void DoServerDestroy()
	{
		base.DoServerDestroy();
		underwaterStatusQueue.Remove(this);
	}

	public virtual float GetDespawnDuration()
	{
		return item?.GetDespawnDuration() ?? Server.itemdespawn;
	}

	public void IdleDestroy()
	{
		Analytics.Azure.OnItemDespawn(this, item, (int)DropReason, DroppedBy);
		DestroyItem();
		Kill();
	}

	public override void OnCollision(Collision collision, BaseEntity hitEntity)
	{
		if (item != null && item.MaxStackable() > 1)
		{
			DroppedItem droppedItem = hitEntity as DroppedItem;
			if (!(droppedItem == null) && droppedItem.item != null && !(droppedItem.item.info != item.info) && droppedItem.item.skin == item.skin)
			{
				droppedItem.OnDroppedOn(this);
			}
		}
	}

	public void OnDroppedOn(DroppedItem di)
	{
		if (item == null || di.item == null || di.item.info != item.info || (di.item.IsBlueprint() && di.item.blueprintTarget != item.blueprintTarget) || NeverCombine || di.NeverCombine || (di.item.hasCondition && di.item.condition != di.item.maxCondition) || (item.hasCondition && item.condition != item.maxCondition))
		{
			return;
		}
		if (di.item.info != null)
		{
			if (di.item.info.amountType == ItemDefinition.AmountType.Genetics)
			{
				int num = ((di.item.instanceData != null) ? di.item.instanceData.dataInt : (-1));
				int num2 = ((item.instanceData != null) ? item.instanceData.dataInt : (-1));
				if (num != num2)
				{
					return;
				}
			}
			if ((di.item.info.GetComponent<ItemModSign>() != null && ItemModAssociatedEntity<SignContent>.GetAssociatedEntity(di.item) != null) || (item.info != null && item.info.GetComponent<ItemModSign>() != null && ItemModAssociatedEntity<SignContent>.GetAssociatedEntity(item) != null))
			{
				return;
			}
		}
		int num3 = di.item.amount + item.amount;
		if (num3 <= item.MaxStackable() && num3 != 0)
		{
			if (di.DropReason == DropReasonEnum.Player)
			{
				DropReason = DropReasonEnum.Player;
			}
			di.DestroyItem();
			di.Kill();
			int worldModelIndex = item.info.GetWorldModelIndex(item.amount);
			item.amount = num3;
			item.MarkDirty();
			if (GetDespawnDuration() < float.PositiveInfinity)
			{
				Invoke(IdleDestroy, GetDespawnDuration());
			}
			Effect.server.Run("assets/bundled/prefabs/fx/notice/stack.world.fx.prefab", this, 0u, Vector3.zero, Vector3.zero);
			int worldModelIndex2 = item.info.GetWorldModelIndex(item.amount);
			if (worldModelIndex != worldModelIndex2)
			{
				item.Drop(base.transform.position, Vector3.zero, base.transform.rotation);
			}
		}
	}

	public override void OnParentChanging(BaseEntity oldParent, BaseEntity newParent)
	{
		base.OnParentChanging(oldParent, newParent);
		if (newParent != null && newParent != oldParent)
		{
			OnParented();
		}
		else if (newParent == null && oldParent != null)
		{
			OnUnparented();
		}
	}

	internal override void OnParentRemoved()
	{
		if (rB == null)
		{
			base.OnParentRemoved();
			return;
		}
		Vector3 position = base.transform.position;
		Quaternion rotation = base.transform.rotation;
		SetParent(null);
		if (UnityEngine.Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out var hitInfo, 2f, 161546240) && position.y < hitInfo.point.y)
		{
			position += Vector3.up * 1.5f;
		}
		base.transform.position = position;
		base.transform.rotation = rotation;
		Unstick();
		if (GetDespawnDuration() < float.PositiveInfinity)
		{
			Invoke(IdleDestroy, GetDespawnDuration());
		}
	}

	public void StickIn()
	{
		SetFlag(Flags.Reserved1, b: true);
	}

	public void Unstick()
	{
		SetFlag(Flags.Reserved1, b: false);
	}

	private void SleepCheck()
	{
		if (!HasParent() || StuckInSomething)
		{
			return;
		}
		if (rB.isKinematic)
		{
			if (maxBoundsExtent == 0f)
			{
				maxBoundsExtent = ((childCollider != null) ? childCollider.bounds.extents.Max() : bounds.extents.Max());
			}
			if (!GamePhysics.Trace(new Ray(CenterPoint(), Vector3.down), 0f, out var _, maxBoundsExtent + 0.1f, -928830719, QueryTriggerInteraction.Ignore, this))
			{
				BecomeActive();
			}
		}
		else if (Vector3.SqrMagnitude(base.transform.localPosition - prevLocalPos) < 0.075f)
		{
			BecomeInactive();
		}
		prevLocalPos = base.transform.localPosition;
	}

	private void OnPhysicsNeighbourChanged()
	{
		if (!StuckInSomething)
		{
			BecomeActive();
		}
	}

	public override void OnPositionalNetworkUpdate()
	{
		base.OnPositionalNetworkUpdate();
		CheckValidPosition();
	}

	protected override bool ShouldUpdateNetworkPosition()
	{
		if (syncPosition)
		{
			return !rB.isKinematic;
		}
		return false;
	}

	private void CheckValidPosition()
	{
		if (!(rB != null) || !(childCollider != null))
		{
			return;
		}
		Vector3 vector = childCollider.bounds.center + smallVerticalOffset;
		Vector3 vector2 = vector - lastGoodColliderCentre;
		Ray ray = new Ray(lastGoodColliderCentre, vector2.normalized);
		if (hasLastPos && GamePhysics.Trace(ray, 0f, out var _, vector2.magnitude, 1218511105, QueryTriggerInteraction.Ignore, this))
		{
			base.transform.position = lastGoodPos + smallVerticalOffset;
			base.transform.rotation = lastGoodRot;
			if (!rB.isKinematic)
			{
				rB.velocity = Vector3.zero;
				rB.angularVelocity = Vector3.zero;
			}
			UnityEngine.Physics.SyncTransforms();
		}
		else
		{
			lastGoodColliderCentre = vector;
			lastGoodPos = base.transform.position;
			lastGoodRot = base.transform.rotation;
			hasLastPos = true;
		}
	}

	private void OnUnparented()
	{
		if (cachedSleepCheck != null)
		{
			CancelInvoke(cachedSleepCheck);
		}
	}

	private void OnParented()
	{
		if (childCollider == null)
		{
			return;
		}
		if ((bool)childCollider)
		{
			childCollider.enabled = false;
			Invoke(EnableCollider, 0.1f);
		}
		if (base.isServer && !StuckInSomething)
		{
			if (cachedSleepCheck == null)
			{
				cachedSleepCheck = SleepCheck;
			}
			InvokeRandomized(cachedSleepCheck, 5.5f, 11f, UnityEngine.Random.Range(-1.1f, 1.1f));
		}
	}

	public override void PostInitShared()
	{
		base.PostInitShared();
		GameObject gameObject = null;
		if (item != null && item.GetWorldModel().isValid)
		{
			gameObject = base.gameManager.CreatePrefab(item.GetWorldModel().resourcePath, base.transform);
			gameObject.transform.localScale = item.GetWorldModel().Get().transform.localScale;
		}
		else
		{
			gameObject = base.gameManager.CreatePrefab(itemModel.resourcePath, base.transform);
		}
		gameObject.transform.localPosition = Vector3.zero;
		gameObject.transform.localRotation = Quaternion.identity;
		gameObject.SetLayerRecursive(base.gameObject.layer);
		childCollider = gameObject.GetComponentInChildren<Collider>();
		if ((bool)childCollider)
		{
			childCollider.enabled = false;
			if (HasParent())
			{
				OnParented();
			}
			else
			{
				childCollider.enabled = true;
			}
			originalLayer = childCollider.gameObject.layer;
		}
		if (base.isServer)
		{
			float angularDrag = 0.1f;
			rB = base.gameObject.AddComponent<Rigidbody>();
			UpdateItemMass();
			rB.drag = 0.1f;
			rB.angularDrag = angularDrag;
			rB.interpolation = RigidbodyInterpolation.None;
			rB.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
			originalCollisionMode = rB.collisionDetectionMode;
			rB.sleepThreshold = Mathf.Max(0.05f, UnityEngine.Physics.sleepThreshold);
			CheckValidPosition();
			CheckUnderwaterStatus(canSplash: false);
			UpdateUnderwaterDrag();
		}
		if (item != null)
		{
			PhysicsEffects component = base.gameObject.GetComponent<PhysicsEffects>();
			if (component != null)
			{
				component.entity = this;
				if (item.info.physImpactSoundDef != null)
				{
					component.physImpactSoundDef = item.info.physImpactSoundDef;
				}
			}
			Buoyancy component2 = gameObject.GetComponent<Buoyancy>();
			if (component2 != null && base.isServer)
			{
				component2.rigidBody = rB;
			}
		}
		gameObject.SetActive(value: true);
	}

	public override void OnFlagsChanged(Flags old, Flags next)
	{
		base.OnFlagsChanged(old, next);
		if (!old.HasFlag(Flags.Reserved1) && next.HasFlag(Flags.Reserved1))
		{
			BecomeInactive();
		}
		else if (old.HasFlag(Flags.Reserved1) && !next.HasFlag(Flags.Reserved1))
		{
			BecomeActive();
		}
		if (base.isServer && old.HasFlag(Flags.Reserved2) != next.HasFlag(Flags.Reserved2))
		{
			UpdateUnderwaterDrag();
		}
	}

	private void BecomeActive()
	{
		if (base.isServer)
		{
			rB.isKinematic = false;
			rB.collisionDetectionMode = originalCollisionMode;
			rB.WakeUp();
			if (HasParent())
			{
				Rigidbody component = GetParentEntity().GetComponent<Rigidbody>();
				if (component != null)
				{
					rB.velocity = component.velocity;
					rB.angularVelocity = component.angularVelocity;
				}
			}
			prevLocalPos = base.transform.localPosition;
		}
		if (childCollider != null)
		{
			childCollider.gameObject.layer = originalLayer;
		}
	}

	private void BecomeInactive()
	{
		if (base.isServer)
		{
			rB.collisionDetectionMode = CollisionDetectionMode.Discrete;
			rB.isKinematic = true;
		}
		if (childCollider != null)
		{
			childCollider.gameObject.layer = 19;
		}
	}

	private void EnableCollider()
	{
		if ((bool)childCollider)
		{
			childCollider.enabled = true;
		}
	}

	public void UpdateItemMass()
	{
		if (rB == null)
		{
			rB = GetComponent<Rigidbody>();
		}
		if (rB == null || item == null || item.contents?.itemList == null)
		{
			return;
		}
		float num = item.info.GetWorldModelMass();
		ItemModContainer component = item.info.GetComponent<ItemModContainer>();
		if (component != null)
		{
			_ = component.worldWeightScale;
		}
		foreach (Item item in item.contents.itemList)
		{
			num += item.info.GetWorldModelMass() * component.worldWeightScale;
		}
		if (component != null && component.maxWeight > 0f)
		{
			num = Mathf.Min(component.maxWeight, num);
		}
		rB.mass = num;
	}

	public override bool ShouldInheritNetworkGroup()
	{
		return false;
	}

	private void CheckUnderwaterStatus(bool canSplash)
	{
		bool flag = WaterLevel.Test(base.transform.position, waves: false, volumes: true, this);
		if (canSplash && flag && !HasFlag(Flags.Reserved2) && splashEffect.isValid)
		{
			Effect.server.Run(splashEffect.resourcePath, base.transform.position, Vector3.zero);
		}
		SetFlag(Flags.Reserved2, flag);
		if (flag && rB != null && !rB.IsSleeping() && (float)lastUnderwaterFlowImpulse > 1f)
		{
			lastUnderwaterFlowImpulse = 0f - UnityEngine.Random.Range(0f, 1f);
			rB.AddForceAtPosition(UnityEngine.Random.onUnitSphere, base.transform.position + UnityEngine.Random.onUnitSphere * 3f, ForceMode.Impulse);
		}
	}

	private void UpdateUnderwaterDrag()
	{
		if (rB != null)
		{
			rB.drag = (HasFlag(Flags.Reserved2) ? 7f : 0.1f);
		}
	}
}
