using System;
using ConVar;
using UnityEngine;
using UnityEngine.Serialization;

public class Buoyancy : ListComponent<Buoyancy>, IServerComponent, IPrefabPreProcess
{
	public enum Priority
	{
		High,
		Low
	}

	[Serializable]
	private struct BuoyancyPointData
	{
		[ReadOnly]
		public Vector3 localPosition;

		[ReadOnly]
		public Vector3 rootToPoint;

		[NonSerialized]
		public Vector3 position;
	}

	public BuoyancyPoint[] points;

	public GameObjectRef[] waterImpacts;

	public Rigidbody rigidBody;

	public float buoyancyScale = 1f;

	public bool scaleForceWithMass;

	public bool doEffects = true;

	public float flowMovementScale = 1f;

	public float requiredSubmergedFraction = 0.5f;

	public bool useUnderwaterDrag;

	[Range(0f, 3f)]
	public float underwaterDrag = 2f;

	[Range(0f, 1f)]
	[Tooltip("How much this object will pay attention to the wave system, 0 = flat water, 1 = full waves (default 1)")]
	[FormerlySerializedAs("flatWaterLerp")]
	public float wavesEffect = 1f;

	public Action<bool> SubmergedChanged;

	public BaseEntity forEntity;

	[NonSerialized]
	public float submergedFraction;

	[SerializeField]
	[ReadOnly]
	private BuoyancyPointData[] pointData;

	private bool initedPointArrays;

	private Vector2[] pointPositionArray;

	private Vector2[] pointPositionUVArray;

	private float[] pointShoreDistanceArray;

	private float[] pointTerrainHeightArray;

	private float[] pointWaterHeightArray;

	private float defaultDrag;

	private float defaultAngularDrag;

	private float timeInWater;

	[NonSerialized]
	public float? ArtificialHeight;

	private BaseVehicle forVehicle;

	private bool hasLocalPlayers;

	private bool hadLocalPlayers;

	public float timeOutOfWater { get; private set; }

	public bool InWater => submergedFraction > requiredSubmergedFraction;

	public Priority BuoyancyPriority { get; set; }

	public void PreProcess(IPrefabProcessor preProcess, GameObject rootObj, string name, bool serverside, bool clientside, bool bundling)
	{
		if (!Application.isPlaying || serverside)
		{
			SavePointData(forced: false);
		}
	}

	public void SavePointData(bool forced)
	{
		if (points == null || points.Length == 0)
		{
			Rigidbody rigidbody = GetComponent<Rigidbody>();
			if (rigidbody == null)
			{
				rigidbody = base.gameObject.AddComponent<Rigidbody>();
			}
			GameObject obj = new GameObject("BuoyancyPoint");
			obj.transform.parent = rigidbody.gameObject.transform;
			obj.transform.localPosition = rigidbody.centerOfMass;
			BuoyancyPoint buoyancyPoint = obj.AddComponent<BuoyancyPoint>();
			buoyancyPoint.buoyancyForce = rigidbody.mass * (0f - UnityEngine.Physics.gravity.y);
			buoyancyPoint.buoyancyForce *= 1.32f;
			buoyancyPoint.size = 0.2f;
			points = new BuoyancyPoint[1];
			points[0] = buoyancyPoint;
		}
		if (pointData == null || pointData.Length != points.Length || forced)
		{
			pointData = new BuoyancyPointData[points.Length];
			for (int i = 0; i < points.Length; i++)
			{
				Transform transform = points[i].transform;
				pointData[i].localPosition = transform.localPosition;
				pointData[i].rootToPoint = base.transform.InverseTransformPoint(transform.position);
			}
		}
	}

	public static string DefaultWaterImpact()
	{
		return "assets/bundled/prefabs/fx/impacts/physics/water-enter-exit.prefab";
	}

	private void Awake()
	{
		forVehicle = forEntity as BaseVehicle;
		InvokeRandomized(CheckSleepState, 0.5f, 5f, 1f);
	}

	public void Sleep()
	{
		if ((forEntity == null || !forEntity.BuoyancySleep(InWater)) && rigidBody != null)
		{
			rigidBody.Sleep();
		}
		base.enabled = false;
	}

	public void Wake()
	{
		if ((forEntity == null || !forEntity.BuoyancyWake()) && rigidBody != null)
		{
			rigidBody.WakeUp();
		}
		base.enabled = true;
	}

	public void CheckSleepState()
	{
		if (base.transform == null || rigidBody == null)
		{
			return;
		}
		hasLocalPlayers = HasLocalPlayers();
		bool flag = rigidBody.IsSleeping() || rigidBody.isKinematic;
		bool flag2 = flag || (!hasLocalPlayers && timeInWater > 6f);
		if (forVehicle != null && forVehicle.IsOn())
		{
			flag2 = false;
		}
		if (base.enabled && flag2)
		{
			Invoke(Sleep, 0f);
			return;
		}
		if (!base.enabled && hasLocalPlayers && !hadLocalPlayers)
		{
			DoCycle(forced: true);
		}
		bool flag3 = !flag || ShouldWake(hasLocalPlayers);
		if (!base.enabled && flag3)
		{
			Invoke(Wake, 0f);
		}
		hadLocalPlayers = hasLocalPlayers;
	}

	public void LowPriorityCheck(bool forceHighPriority)
	{
		Priority buoyancyPriority = BuoyancyPriority;
		Priority priority = buoyancyPriority;
		if (forceHighPriority)
		{
			priority = Priority.High;
		}
		else
		{
			Vector3 position = base.transform.position;
			priority = ((!BaseNetworkable.HasCloseConnections(position, Server.lowPriorityBuoyancyRange)) ? Priority.Low : Priority.High);
			if (priority == Priority.Low && priority != buoyancyPriority)
			{
				Vector3 vector = base.transform.TransformPoint(Vector3.forward * 2f).WithY(position.y);
				rigidBody.rotation = Quaternion.LookRotation((vector - rigidBody.position).normalized, Vector3.up);
			}
		}
		if (priority != buoyancyPriority)
		{
			rigidBody.velocity = Vector3.zero;
			rigidBody.angularVelocity = Vector3.zero;
			BuoyancyPriority = priority;
		}
	}

	public bool ShouldWake()
	{
		return ShouldWake(HasLocalPlayers());
	}

	public bool ShouldWake(bool hasLocalPlayers)
	{
		if (hasLocalPlayers)
		{
			return submergedFraction > 0f;
		}
		return false;
	}

	private bool HasLocalPlayers()
	{
		return BaseNetworkable.HasCloseConnections(base.transform.position, 100f);
	}

	protected void DoCycle(bool forced = false)
	{
		if (!base.enabled && !forced)
		{
			return;
		}
		bool num = submergedFraction > 0f;
		BuoyancyFixedUpdate();
		bool flag = submergedFraction > 0f;
		if (num == flag)
		{
			return;
		}
		if (useUnderwaterDrag && rigidBody != null)
		{
			if (flag)
			{
				defaultDrag = rigidBody.drag;
				defaultAngularDrag = rigidBody.angularDrag;
				rigidBody.drag = underwaterDrag;
				rigidBody.angularDrag = underwaterDrag;
			}
			else
			{
				rigidBody.drag = defaultDrag;
				rigidBody.angularDrag = defaultAngularDrag;
			}
		}
		if (SubmergedChanged != null)
		{
			SubmergedChanged(flag);
		}
	}

	public static void Cycle()
	{
		bool autoSyncTransforms = UnityEngine.Physics.autoSyncTransforms;
		try
		{
			UnityEngine.Physics.autoSyncTransforms = false;
			Buoyancy[] buffer = ListComponent<Buoyancy>.InstanceList.Values.Buffer;
			int count = ListComponent<Buoyancy>.InstanceList.Count;
			for (int i = 0; i < count; i++)
			{
				buffer[i].DoCycle();
			}
		}
		finally
		{
			if (autoSyncTransforms)
			{
				UnityEngine.Physics.SyncTransforms();
			}
			UnityEngine.Physics.autoSyncTransforms = autoSyncTransforms;
		}
	}

	private Vector3 GetFlowDirection(Vector3 worldPos)
	{
		return WaterLevel.GetWaterFlowDirection(worldPos);
	}

	public void BuoyancyFixedUpdate()
	{
		if (rigidBody == null)
		{
			return;
		}
		if (buoyancyScale == 0f)
		{
			Invoke(Sleep, 0f);
			return;
		}
		if (BuoyancyPriority == Priority.Low)
		{
			WaterLevel.WaterInfo waterInfo = WaterLevel.GetWaterInfo(base.transform.position, waves: true, volumes: true, forEntity);
			Vector3 position = rigidBody.position;
			if (position.y < waterInfo.surfaceLevel)
			{
				rigidBody.position = new Vector3(position.x, waterInfo.surfaceLevel, position.z);
			}
			return;
		}
		if (!initedPointArrays)
		{
			InitPointArrays();
		}
		float time = UnityEngine.Time.time;
		float x = TerrainMeta.Position.x;
		float z = TerrainMeta.Position.z;
		float x2 = TerrainMeta.OneOverSize.x;
		float z2 = TerrainMeta.OneOverSize.z;
		Matrix4x4 localToWorldMatrix = base.transform.localToWorldMatrix;
		for (int i = 0; i < pointData.Length; i++)
		{
			Vector3 position2 = localToWorldMatrix.MultiplyPoint3x4(pointData[i].rootToPoint);
			pointData[i].position = position2;
			float x3 = (position2.x - x) * x2;
			float y = (position2.z - z) * z2;
			pointPositionArray[i] = new Vector2(position2.x, position2.z);
			pointPositionUVArray[i] = new Vector2(x3, y);
		}
		WaterSystem.GetHeightArray(pointPositionArray, pointPositionUVArray, pointShoreDistanceArray, pointTerrainHeightArray, pointWaterHeightArray);
		bool flag = wavesEffect < 1f;
		int num = 0;
		for (int j = 0; j < points.Length; j++)
		{
			BuoyancyPoint buoyancyPoint = points[j];
			Vector3 pos = pointData[j].position;
			Vector3 localPosition = pointData[j].localPosition;
			Vector2 posUV = pointPositionUVArray[j];
			float terrainHeight = pointTerrainHeightArray[j];
			float num2 = pointWaterHeightArray[j];
			if (ArtificialHeight.HasValue)
			{
				num2 = ArtificialHeight.Value;
			}
			else if (flag)
			{
				num2 = Mathf.Lerp(0f, num2, wavesEffect);
			}
			bool doDeepwaterChecks = !ArtificialHeight.HasValue;
			WaterLevel.WaterInfo waterInfo2 = WaterLevel.GetBuoyancyWaterInfo(pos, posUV, terrainHeight, num2, doDeepwaterChecks, forEntity);
			if (flag && waterInfo2.isValid)
			{
				waterInfo2.currentDepth = Mathf.Lerp(waterInfo2.currentDepth, waterInfo2.surfaceLevel - pos.y, wavesEffect);
			}
			bool flag2 = false;
			if (pos.y < waterInfo2.surfaceLevel && waterInfo2.isValid)
			{
				flag2 = true;
				num++;
				float currentDepth = waterInfo2.currentDepth;
				float num3 = Mathf.InverseLerp(0f, buoyancyPoint.size, currentDepth);
				float num4 = 1f + Mathf.PerlinNoise(buoyancyPoint.randomOffset + time * buoyancyPoint.waveFrequency, 0f) * buoyancyPoint.waveScale;
				float num5 = buoyancyPoint.buoyancyForce * buoyancyScale;
				if (scaleForceWithMass)
				{
					num5 *= rigidBody.mass;
				}
				Vector3 accumForce = new Vector3(0f, num4 * num3 * num5, 0f);
				AccumulateFlowForce(ref accumForce, in pos, in waterInfo2, Mathf.Abs(pointShoreDistanceArray[j]), num5);
				rigidBody.AddForceAtPosition(accumForce, pos, ForceMode.Force);
			}
			if (buoyancyPoint.doSplashEffects && ((!buoyancyPoint.wasSubmergedLastFrame && flag2) || (!flag2 && buoyancyPoint.wasSubmergedLastFrame)) && doEffects && rigidBody.GetRelativePointVelocity(localPosition).magnitude > 1f)
			{
				string strName = ((waterImpacts != null && waterImpacts.Length != 0 && waterImpacts[0].isValid) ? waterImpacts[0].resourcePath : DefaultWaterImpact());
				Vector3 vector = new Vector3(UnityEngine.Random.Range(-0.25f, 0.25f), 0f, UnityEngine.Random.Range(-0.25f, 0.25f));
				Effect.server.Run(strName, pos + vector, Vector3.up);
				buoyancyPoint.nexSplashTime = UnityEngine.Time.time + 0.25f;
			}
			buoyancyPoint.wasSubmergedLastFrame = flag2;
		}
		if (points.Length != 0)
		{
			submergedFraction = (float)num / (float)points.Length;
		}
		if (InWater)
		{
			timeInWater += UnityEngine.Time.fixedDeltaTime;
			timeOutOfWater = 0f;
		}
		else
		{
			timeOutOfWater += UnityEngine.Time.fixedDeltaTime;
			timeInWater = 0f;
		}
	}

	public void AccumulateFlowForce(ref Vector3 accumForce, in Vector3 pos, in WaterLevel.WaterInfo waterInfo, float shoreDistance, float scaledBuoyancyForce)
	{
		if ((waterInfo.topology & 0x10000) == 0)
		{
			float num = Mathf.Clamp01(Mathf.InverseLerp(60f, 0f, shoreDistance));
			if (!(num <= Mathf.Epsilon))
			{
				num = Mathf.Pow(num, 0.5f);
				Vector3 flowDirection = GetFlowDirection(pos);
				scaledBuoyancyForce *= 0.025f * num;
				accumForce.x += flowDirection.x * scaledBuoyancyForce * flowMovementScale;
				accumForce.z += flowDirection.z * scaledBuoyancyForce * flowMovementScale;
			}
		}
	}

	private void InitPointArrays()
	{
		pointPositionArray = new Vector2[points.Length];
		pointPositionUVArray = new Vector2[points.Length];
		pointShoreDistanceArray = new float[points.Length];
		pointTerrainHeightArray = new float[points.Length];
		pointWaterHeightArray = new float[points.Length];
		initedPointArrays = true;
	}
}
