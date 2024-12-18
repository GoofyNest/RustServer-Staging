using UnityEngine;

public class TriggerHostileWarningZone : TriggerBase
{
	public GameObject TargetGameObject;

	public Collider triggerCollider { get; private set; }

	protected void Awake()
	{
		triggerCollider = GetComponent<Collider>();
	}

	protected void OnEnable()
	{
		ResizeTrigger();
	}

	private void ResizeTrigger()
	{
		if (TargetGameObject == null)
		{
			return;
		}
		BaseEntity baseEntity = TargetGameObject.ToBaseEntity();
		if (!(baseEntity == null) && baseEntity is IHostileWarningEntity hostileWarningEntity)
		{
			SphereCollider sphereCollider = triggerCollider as SphereCollider;
			if (sphereCollider != null)
			{
				sphereCollider.radius = hostileWarningEntity.WarningRange();
			}
		}
	}

	internal override GameObject InterestedInObject(GameObject obj)
	{
		obj = base.InterestedInObject(obj);
		if (obj == null)
		{
			return null;
		}
		BaseEntity baseEntity = obj.ToBaseEntity();
		if (baseEntity == null)
		{
			return null;
		}
		if (baseEntity.isServer)
		{
			return null;
		}
		return baseEntity.gameObject;
	}

	public bool WarningEnabled(BaseEntity forEntity)
	{
		if (TargetGameObject == null)
		{
			return true;
		}
		BaseEntity baseEntity = TargetGameObject.ToBaseEntity();
		if (baseEntity == null)
		{
			return true;
		}
		if (baseEntity is IHostileWarningEntity hostileWarningEntity)
		{
			return hostileWarningEntity.WarningEnabled(forEntity);
		}
		return true;
	}
}
