using UnityEngine;

public class TriggerSnowmobileAchievement : TriggerBase
{
	internal override GameObject InterestedInObject(GameObject obj)
	{
		BaseEntity baseEntity = obj.ToBaseEntity();
		if (baseEntity != null && baseEntity.isServer && baseEntity.ToPlayer() != null)
		{
			return baseEntity.gameObject;
		}
		return null;
	}
}
