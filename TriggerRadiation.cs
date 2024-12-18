using UnityEngine;
using UnityEngine.Serialization;

public class TriggerRadiation : TriggerBase
{
	public Radiation.Tier radiationTier = Radiation.Tier.LOW;

	public bool BypassArmor;

	public float RadiationAmountOverride;

	public float falloff = 0.1f;

	[FormerlySerializedAs("UseColliderRadius")]
	public bool DontScaleRadiationSize;

	public bool UseLOSCheck;

	public bool ApplyLocalHeightCheck;

	public float MinLocalHeight;

	private SphereCollider sphereCollider;

	private BoxCollider boxCollider;

	private float GetRadiationSize()
	{
		if (!sphereCollider)
		{
			sphereCollider = GetComponent<SphereCollider>();
		}
		if (sphereCollider != null)
		{
			if (!DontScaleRadiationSize)
			{
				return sphereCollider.radius * base.transform.localScale.Max();
			}
			return sphereCollider.radius;
		}
		if (!boxCollider)
		{
			boxCollider = GetComponent<BoxCollider>();
		}
		if (boxCollider != null)
		{
			Vector3 size = boxCollider.size;
			if (!DontScaleRadiationSize)
			{
				return Mathf.Max(size.x, size.y, size.z) * 0.5f * base.transform.localScale.Max();
			}
			return Mathf.Max(size.x, size.y, size.z) * 0.5f;
		}
		return 0f;
	}

	private float GetTriggerRadiation()
	{
		if (RadiationAmountOverride > 0f)
		{
			return RadiationAmountOverride;
		}
		return Radiation.GetRadiation(radiationTier);
	}

	public float GetRadiation(Vector3 position, float radProtection)
	{
		if (ApplyLocalHeightCheck && base.transform.InverseTransformPoint(position).y < MinLocalHeight)
		{
			return 0f;
		}
		if (UseLOSCheck && !GamePhysics.LineOfSight(base.gameObject.transform.position, position, 2097152))
		{
			return 0f;
		}
		float radiationSize = GetRadiationSize();
		float triggerRadiation = GetTriggerRadiation();
		float num = Mathf.InverseLerp(value: Vector3.Distance(base.gameObject.transform.position, position), a: radiationSize, b: radiationSize * (1f - falloff));
		float num2 = triggerRadiation;
		if (!BypassArmor)
		{
			num2 = Radiation.GetRadiationAfterProtection(triggerRadiation, radProtection);
		}
		return num2 * num;
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
		if (baseEntity.isClient)
		{
			return null;
		}
		if (!(baseEntity is BaseCombatEntity))
		{
			return null;
		}
		return baseEntity.gameObject;
	}

	public void OnDrawGizmosSelected()
	{
		float radiationSize = GetRadiationSize();
		Gizmos.color = Color.green;
		if ((bool)sphereCollider)
		{
			Gizmos.DrawWireSphere(base.transform.position, radiationSize);
		}
		else if ((bool)boxCollider)
		{
			Vector3 size = new Vector3(radiationSize, radiationSize, radiationSize);
			size *= 2f;
			Gizmos.DrawWireCube(base.transform.position, size);
		}
		Gizmos.color = Color.red;
		if ((bool)sphereCollider)
		{
			Gizmos.DrawWireSphere(base.transform.position, radiationSize * (1f - falloff));
		}
		else if ((bool)boxCollider)
		{
			Vector3 vector = new Vector3(radiationSize, radiationSize, radiationSize);
			vector *= 2f;
			Gizmos.DrawWireCube(base.transform.position, vector * (1f - falloff));
		}
	}
}
