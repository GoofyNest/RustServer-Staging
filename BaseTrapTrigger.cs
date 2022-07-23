using UnityEngine;

public class BaseTrapTrigger : TriggerBase
{
	public BaseTrap _trap;

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
		if (baseEntity.isClient)
		{
			return null;
		}
		return baseEntity.gameObject;
	}

	internal override void OnObjectAdded(GameObject obj, Collider col)
	{
		base.OnObjectAdded(obj, col);
		_trap.ObjectEntered(obj);
	}

	internal override void OnEmpty()
	{
		base.OnEmpty();
		_trap.OnEmpty();
	}
}
