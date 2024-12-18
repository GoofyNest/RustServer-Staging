using Facepunch.Rust;
using Network;
using UnityEngine;

public class OreResourceEntity : StagedResourceEntity
{
	public GameObjectRef bonusPrefab;

	public GameObjectRef finishEffect;

	public GameObjectRef bonusFailEffect;

	public bool useHotspotMinigame = true;

	public SoundPlayer bonusSound;

	public float heightOffset;

	private int bonusesKilled;

	private int bonusesSpawned;

	private OreHotSpot _hotSpot;

	private Vector3 lastNodeDir = Vector3.zero;

	private Ray? spawnBonusHitRay;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("OreResourceEntity.OnRpcMessage"))
		{
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void OnAttacked(HitInfo info)
	{
		if (!info.DidGather && info.gatherScale > 0f)
		{
			if (useHotspotMinigame)
			{
				Jackhammer jackhammer = info.Weapon as Jackhammer;
				bool flag = _hotSpot == null;
				if (_hotSpot == null)
				{
					_hotSpot = SpawnBonusSpotOnRay(new Ray(info.PointStart, info.attackNormal));
				}
				float num = ((_hotSpot != null) ? Vector3.Distance(info.HitPositionWorld, _hotSpot.transform.position) : float.MaxValue);
				if (flag || num <= _hotSpot.GetComponent<SphereCollider>().radius * 1.5f || jackhammer != null)
				{
					float num2 = ((jackhammer == null) ? 1f : jackhammer.HotspotBonusScale);
					bonusesKilled++;
					info.gatherScale = 1f + Mathf.Clamp((float)bonusesKilled * 0.5f, 0f, 2f * num2);
					if (_hotSpot != null)
					{
						_hotSpot.FireFinishEffect();
						ClientRPC(null, "PlayBonusLevelSound", bonusesKilled, _hotSpot.transform.position);
					}
				}
				else if (bonusesKilled > 0)
				{
					bonusesKilled = 0;
					Effect.server.Run(bonusFailEffect.resourcePath, base.transform.position, base.transform.up);
				}
				if (bonusesKilled > 0)
				{
					CleanupBonus();
				}
				if (_hotSpot == null)
				{
					if (flag)
					{
						spawnBonusHitRay = new Ray(info.PointStart, info.attackNormal);
						lastNodeDir = (info.PointStart - (base.transform.position + new Vector3(0f, 0.5f, 0f))).normalized;
						float num3 = 0.5f;
						if (lastNodeDir.y > num3)
						{
							float num4 = lastNodeDir.y - num3;
							lastNodeDir.y = num4;
							lastNodeDir.x += num4;
							lastNodeDir.z += num4;
						}
						lastNodeDir = base.transform.InverseTransformDirection(lastNodeDir);
					}
					DelayedBonusSpawn();
				}
			}
			else
			{
				info.gatherScale = 1f;
			}
		}
		base.OnAttacked(info);
	}

	protected override void UpdateNetworkStage()
	{
		int num = stage;
		base.UpdateNetworkStage();
		if (stage != num && (bool)_hotSpot && useHotspotMinigame)
		{
			DelayedBonusSpawn();
		}
	}

	public void CleanupBonus()
	{
		if ((bool)_hotSpot)
		{
			_hotSpot.Kill();
		}
		_hotSpot = null;
	}

	public override void DestroyShared()
	{
		base.DestroyShared();
		CleanupBonus();
	}

	public override void OnKilled(HitInfo info)
	{
		CleanupBonus();
		Analytics.Server.OreKilled(this, info);
		base.OnKilled(info);
	}

	public void FinishBonusAssigned()
	{
		Effect.server.Run(finishEffect.resourcePath, base.transform.position, base.transform.up);
	}

	public void DelayedBonusSpawn()
	{
		CancelInvoke(RespawnBonus);
		Invoke(RespawnBonus, 0.25f);
	}

	public void RespawnBonus()
	{
		CleanupBonus();
		if (spawnBonusHitRay.HasValue)
		{
			_hotSpot = SpawnBonusSpotOnRay(spawnBonusHitRay.Value);
			spawnBonusHitRay = null;
		}
		else
		{
			_hotSpot = SpawnBonusSpot(lastNodeDir);
		}
	}

	private OreHotSpot SpawnBonusSpotOnRay(Ray r)
	{
		if (base.isClient)
		{
			return null;
		}
		if (!useHotspotMinigame)
		{
			return null;
		}
		if (!bonusPrefab.isValid)
		{
			return null;
		}
		if (ResourceMeshCollider.Raycast(r, out var hitInfo, 15f))
		{
			OreHotSpot obj = GameManager.server.CreateEntity(bonusPrefab.resourcePath, hitInfo.point - r.direction * 0.025f, Quaternion.LookRotation(hitInfo.normal, Vector3.up)) as OreHotSpot;
			obj.Spawn();
			obj.OreOwner(this);
			return obj;
		}
		return SpawnBonusSpot(lastNodeDir);
	}

	public OreHotSpot SpawnBonusSpot(Vector3 lastDirection)
	{
		if (base.isClient)
		{
			return null;
		}
		if (!useHotspotMinigame)
		{
			return null;
		}
		if (!bonusPrefab.isValid)
		{
			return null;
		}
		Vector3 zero = Vector3.zero;
		MeshCollider resourceMeshCollider = ResourceMeshCollider;
		Vector3 vector = base.transform.InverseTransformPoint(resourceMeshCollider.bounds.center);
		if (lastDirection == Vector3.zero)
		{
			float num = 0.5f;
			if (heightOffset != 0f)
			{
				num = heightOffset;
			}
			Vector3 vector2 = RandomCircle();
			Vector3 vector3 = (lastNodeDir = base.transform.TransformDirection(vector2.normalized));
			vector2 = base.transform.position + base.transform.up * (vector.y + num) + vector3.normalized * 5f;
			zero = vector2;
		}
		else
		{
			Vector3 vector4 = Vector3.Cross(lastNodeDir, Vector3.up);
			float num2 = Random.Range(0.25f, 0.5f) + (float)stage * 0.25f;
			float num3 = ((Random.Range(0, 2) == 0) ? (-1f) : 1f);
			Vector3 direction = (lastNodeDir = (lastNodeDir + vector4 * (num2 * num3)).normalized);
			zero = base.transform.position + base.transform.TransformDirection(direction) * 2f;
			float num4 = Random.Range(1f, 1.5f);
			zero += base.transform.up * (vector.y + num4);
		}
		bonusesSpawned++;
		Vector3 normalized = (resourceMeshCollider.bounds.center - zero).normalized;
		if (resourceMeshCollider.Raycast(new Ray(zero, normalized), out var hitInfo, 15f))
		{
			OreHotSpot obj = GameManager.server.CreateEntity(bonusPrefab.resourcePath, hitInfo.point - normalized * 0.025f, Quaternion.LookRotation(hitInfo.normal, Vector3.up)) as OreHotSpot;
			obj.Spawn();
			obj.OreOwner(this);
			return obj;
		}
		return null;
	}

	public Vector3 RandomCircle(float distance = 1f, bool allowInside = false)
	{
		Vector2 vector = (allowInside ? Random.insideUnitCircle : Random.insideUnitCircle.normalized);
		return new Vector3(vector.x, 0f, vector.y);
	}

	public Vector3 RandomHemisphereDirection(Vector3 input, float degreesOffset, bool allowInside = true, bool changeHeight = true)
	{
		degreesOffset = Mathf.Clamp(degreesOffset / 180f, -180f, 180f);
		Vector2 vector = (allowInside ? Random.insideUnitCircle : Random.insideUnitCircle.normalized);
		Vector3 vector2 = new Vector3(vector.x * degreesOffset, changeHeight ? (Random.Range(-1f, 1f) * degreesOffset) : 0f, vector.y * degreesOffset);
		return (input + vector2).normalized;
	}

	public Vector3 ClampToHemisphere(Vector3 hemiInput, float degreesOffset, Vector3 inputVec)
	{
		degreesOffset = Mathf.Clamp(degreesOffset / 180f, -180f, 180f);
		Vector3 normalized = (hemiInput + Vector3.one * degreesOffset).normalized;
		Vector3 normalized2 = (hemiInput + Vector3.one * (0f - degreesOffset)).normalized;
		for (int i = 0; i < 3; i++)
		{
			inputVec[i] = Mathf.Clamp(inputVec[i], normalized2[i], normalized[i]);
		}
		return inputVec;
	}

	public static Vector3 RandomCylinderPointAroundVector(Vector3 input, float distance, float minHeight = 0f, float maxHeight = 0f, bool allowInside = false)
	{
		Vector2 vector = (allowInside ? Random.insideUnitCircle : Random.insideUnitCircle.normalized);
		Vector3 result = new Vector3(vector.x, 0f, vector.y).normalized * distance;
		result.y = Random.Range(minHeight, maxHeight);
		return result;
	}

	public Vector3 ClampToCylinder(Vector3 localPos, Vector3 cylinderAxis, float cylinderDistance, float minHeight = 0f, float maxHeight = 0f)
	{
		return Vector3.zero;
	}
}
