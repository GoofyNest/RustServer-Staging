using System.Collections.Generic;
using Facepunch;
using UnityEngine;

public class TriggerParentElevator : TriggerParentEnclosed
{
	public bool AllowHorsesToBypassClippingChecks = true;

	public bool AllowBikesToBypassClippingChecks = true;

	public bool IgnoreParentEntityColliders;

	protected override bool IsClipping(BaseEntity ent)
	{
		if (AllowHorsesToBypassClippingChecks && ent is BaseRidableAnimal)
		{
			return false;
		}
		if ((AllowBikesToBypassClippingChecks && ent is Bike) || ent is Snowmobile)
		{
			return false;
		}
		if (IgnoreParentEntityColliders)
		{
			List<Collider> obj = Pool.Get<List<Collider>>();
			GamePhysics.OverlapOBB(ent.WorldSpaceBounds(), obj, 1218511105);
			BaseEntity baseEntity = base.gameObject.ToBaseEntity();
			foreach (Collider item in obj)
			{
				BaseEntity baseEntity2 = item.ToBaseEntity();
				if (baseEntity2 != null)
				{
					if (!(baseEntity2 == baseEntity) && !(baseEntity2 is Elevator))
					{
					}
					continue;
				}
				return true;
			}
			Pool.FreeUnmanaged(ref obj);
			return false;
		}
		return base.IsClipping(ent);
	}
}
