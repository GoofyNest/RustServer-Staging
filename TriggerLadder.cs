using UnityEngine;

public class TriggerLadder : TriggerBase
{
	public enum LadderType
	{
		Rungs,
		Rope
	}

	public LadderType Type;

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
		if (baseEntity as BasePlayer == null)
		{
			return null;
		}
		return baseEntity.gameObject;
	}
}
