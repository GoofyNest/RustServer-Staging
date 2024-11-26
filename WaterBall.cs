using System.Collections.Generic;
using Facepunch;
using UnityEngine;

public class WaterBall : BaseEntity
{
	public ItemDefinition liquidType;

	public int waterAmount;

	public GameObjectRef waterExplosion;

	public Collider waterCollider;

	public Rigidbody myRigidBody;

	public override void ServerInit()
	{
		base.ServerInit();
		Invoke(Extinguish, 10f);
	}

	public void Extinguish()
	{
		CancelInvoke(Extinguish);
		if (!base.IsDestroyed)
		{
			Kill();
		}
	}

	public void FixedUpdate()
	{
		if (base.isServer && myRigidBody != null)
		{
			myRigidBody.AddForce(Physics.gravity, ForceMode.Acceleration);
		}
	}

	public static bool DoSplash(Vector3 position, float radius, ItemDefinition liquidDef, int amount)
	{
		List<BaseEntity> obj = Pool.Get<List<BaseEntity>>();
		Vis.Entities(position, radius, obj, 1220225811);
		int num = 0;
		int num2 = amount;
		while (amount > 0 && num < 3)
		{
			List<ISplashable> obj2 = Pool.Get<List<ISplashable>>();
			foreach (BaseEntity item in obj)
			{
				if (!item.isClient && item is ISplashable splashable && !obj2.Contains(splashable) && splashable.WantsSplash(liquidDef, amount))
				{
					bool flag = true;
					if (item is PlanterBox && !GamePhysics.LineOfSight(item.transform.position + new Vector3(0f, 1f, 0f), position, 2097152))
					{
						flag = false;
					}
					if (flag)
					{
						obj2.Add(splashable);
					}
				}
			}
			if (obj2.Count == 0)
			{
				break;
			}
			int b = Mathf.CeilToInt(amount / obj2.Count);
			foreach (ISplashable item2 in obj2)
			{
				int num3 = item2.DoSplash(liquidDef, Mathf.Min(amount, b));
				amount -= num3;
				if (amount <= 0)
				{
					break;
				}
			}
			Pool.FreeUnmanaged(ref obj2);
			num++;
		}
		Pool.FreeUnmanaged(ref obj);
		return amount < num2;
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (!base.isClient && !myRigidBody.isKinematic)
		{
			float num = 2.5f;
			Vector3 position = base.transform.position;
			float num2 = num * 0.75f;
			if (GamePhysics.Trace(new Ray(position, Vector3.up), 0.05f, out var hitInfo, num2, 1084293377))
			{
				num2 = hitInfo.distance;
			}
			DoSplash(position + new Vector3(0f, num2, 0f), num, liquidType, waterAmount);
			Effect.server.Run(waterExplosion.resourcePath, position, Vector3.up);
			myRigidBody.isKinematic = true;
			waterCollider.enabled = false;
			Invoke(Extinguish, 2f);
		}
	}
}
