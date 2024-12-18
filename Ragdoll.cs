using System.Collections.Generic;
using ConVar;
using Facepunch;
using Facepunch.Extend;
using Facepunch.Utility;
using Network;
using ProtoBuf;
using UnityEngine;

public class Ragdoll : EntityComponent<BaseEntity>, IPrefabPreProcess
{
	[Header("Ragdoll")]
	[Tooltip("If true, ragdoll physics are simulated on the server instead of the client")]
	public bool simOnServer;

	public float lerpToServerSimTime = 0.5f;

	public Transform eyeTransform;

	public Rigidbody primaryBody;

	[ReadOnly]
	public SpringJoint corpseJoint;

	[SerializeField]
	private PhysicMaterial physicMaterial;

	[SerializeField]
	private Skeleton skeleton;

	[SerializeField]
	private Model model;

	[ReadOnly]
	public List<Rigidbody> rigidbodies = new List<Rigidbody>();

	[ReadOnly]
	[SerializeField]
	private List<Transform> rbTransforms = new List<Transform>();

	[ReadOnly]
	[SerializeField]
	private List<Joint> joints = new List<Joint>();

	[ReadOnly]
	[SerializeField]
	private List<CharacterJoint> characterJoints = new List<CharacterJoint>();

	[ReadOnly]
	[SerializeField]
	private List<ConfigurableJoint> configurableJoints = new List<ConfigurableJoint>();

	[ReadOnly]
	[SerializeField]
	private List<Collider> colliders = new List<Collider>();

	[ReadOnly]
	[SerializeField]
	private int[] boneIndex;

	[ReadOnly]
	[SerializeField]
	private Vector3[] genericBonePos;

	[ReadOnly]
	[SerializeField]
	private Quaternion[] genericBoneRot;

	[SerializeField]
	private GameObject GibEffect;

	protected bool isSetUp;

	private const float MAX_JOINT_DIST = 2f;

	private bool wasSyncingJoints = true;

	protected bool IsClient => false;

	protected bool isServer => !IsClient;

	public bool IsSleeping => !rigidbodies[0].IsSleeping();

	public bool IsKinematic => rigidbodies[0].isKinematic;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("Ragdoll.OnRpcMessage"))
		{
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	private void SetUpPhysics(bool isServer)
	{
		if (isSetUp)
		{
			return;
		}
		isSetUp = true;
		if (isServer != simOnServer)
		{
			return;
		}
		foreach (Joint joint in joints)
		{
			joint.enablePreprocessing = false;
		}
		foreach (CharacterJoint characterJoint in characterJoints)
		{
			characterJoint.enableProjection = true;
		}
		foreach (ConfigurableJoint configurableJoint in configurableJoints)
		{
			configurableJoint.projectionMode = JointProjectionMode.PositionAndRotation;
		}
		SetInterpolationMode(base.transform.parent, isServer);
		foreach (Rigidbody rigidbody in rigidbodies)
		{
			SetCollisionMode(rigidbody, isServer);
			rigidbody.angularDrag = 1f;
			rigidbody.drag = 1f;
			rigidbody.detectCollisions = true;
			if (isServer)
			{
				rigidbody.solverIterations = 40;
			}
			else
			{
				rigidbody.solverIterations = 20;
			}
			rigidbody.solverVelocityIterations = 10;
			rigidbody.maxDepenetrationVelocity = 2f;
			rigidbody.sleepThreshold = Mathf.Max(0.05f, UnityEngine.Physics.sleepThreshold);
			if (rigidbody.mass < 1f)
			{
				rigidbody.mass = 1f;
			}
			rigidbody.velocity = Random.onUnitSphere * 5f;
			rigidbody.angularVelocity = Random.onUnitSphere * 5f;
		}
	}

	public void ParentChanging(BaseCorpse corpse, Transform newParent)
	{
		SetInterpolationMode(newParent, corpse.isServer);
	}

	private void SetInterpolationMode(Transform parent, bool isServer)
	{
		if (isServer != simOnServer)
		{
			return;
		}
		RigidbodyInterpolation interpolation = ((!simOnServer && (parent == null || !AnyParentMoves(parent))) ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None);
		foreach (Rigidbody rigidbody in rigidbodies)
		{
			rigidbody.interpolation = interpolation;
		}
	}

	private bool AnyParentMoves(Transform parent)
	{
		while (parent != null)
		{
			BaseEntity component = parent.GetComponent<BaseEntity>();
			if (component != null && component.syncPosition)
			{
				return true;
			}
			parent = parent.parent;
		}
		return false;
	}

	private static void SetCollisionMode(Rigidbody rigidBody, bool isServer)
	{
		int serverragdollmode = ConVar.Physics.serverragdollmode;
		if (serverragdollmode <= 0)
		{
			rigidBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
		}
		if (serverragdollmode == 1)
		{
			rigidBody.collisionDetectionMode = CollisionDetectionMode.Continuous;
		}
		if (serverragdollmode == 2)
		{
			rigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
		}
		if (serverragdollmode >= 3)
		{
			rigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
		}
	}

	public void MoveRigidbodiesToRoot()
	{
		foreach (Transform rbTransform in rbTransforms)
		{
			rbTransform.SetParent(base.transform, worldPositionStays: true);
		}
	}

	public override void LoadComponent(BaseNetworkable.LoadInfo info)
	{
		if (simOnServer && info.msg.ragdoll != null && isServer)
		{
			for (int i = 0; i < rbTransforms.Count; i++)
			{
				rbTransforms[i].localPosition = Compression.UnpackVector3FromInt(info.msg.ragdoll.positions[i], -2f, 2f);
				rbTransforms[i].localEulerAngles = Compression.UnpackVector3FromInt(info.msg.ragdoll.rotations[i], -360f, 360f);
			}
		}
	}

	public void GetCurrentBoneState(GameObject[] bones, ref Vector3[] bonePos, ref Quaternion[] boneRot)
	{
		int num = bones.Length;
		bonePos = new Vector3[num];
		boneRot = new Quaternion[num];
		for (int i = 0; i < num; i++)
		{
			if (bones[i] != null)
			{
				Transform transform = bones[i].transform;
				bonePos[i] = transform.localPosition;
				boneRot[i] = transform.localRotation;
			}
		}
	}

	public void PreProcess(IPrefabProcessor preProcess, GameObject rootObj, string name, bool serverside, bool clientside, bool bundling)
	{
		joints.Clear();
		rbTransforms.Clear();
		characterJoints.Clear();
		configurableJoints.Clear();
		rigidbodies.Clear();
		colliders.Clear();
		GetComponentsInChildren(includeInactive: true, rigidbodies);
		for (int i = 0; i < rigidbodies.Count; i++)
		{
			if (!(rigidbodies[i].transform == base.transform))
			{
				rbTransforms.Add(rigidbodies[i].transform);
			}
		}
		GetComponentsInChildren(includeInactive: true, joints);
		GetComponentsInChildren(includeInactive: true, characterJoints);
		GetComponentsInChildren(includeInactive: true, configurableJoints);
		GetComponentsInChildren(includeInactive: true, colliders);
		rbTransforms.Sort((Transform t1, Transform t2) => t1.GetDepth().CompareTo(t2.GetDepth()));
		if (skeleton.Bones != null && skeleton.Bones.Length != 0)
		{
			GetCurrentBoneState(skeleton.Bones, ref genericBonePos, ref genericBoneRot);
			int num = skeleton.Bones.Length;
			boneIndex = new int[num];
			for (int j = 0; j < num; j++)
			{
				boneIndex[j] = -1;
				GameObject gameObject = skeleton.Bones[j];
				for (int k = 0; k < rbTransforms.Count; k++)
				{
					if (rbTransforms[k].gameObject == gameObject)
					{
						boneIndex[j] = k;
						break;
					}
				}
			}
		}
		if (!clientside || !simOnServer)
		{
			return;
		}
		foreach (Joint joint in joints)
		{
			Object.DestroyImmediate(joint, allowDestroyingAssets: true);
		}
		foreach (Rigidbody rigidbody in rigidbodies)
		{
			Object.DestroyImmediate(rigidbody, allowDestroyingAssets: true);
		}
	}

	private void RemoveRootBoneOffset()
	{
		if (simOnServer)
		{
			Transform rootBone = model.rootBone;
			if (rootBone != null && !rootBone.HasComponent<Rigidbody>())
			{
				base.transform.position = rootBone.position;
				base.transform.rotation = rootBone.rotation;
				rootBone.localPosition = Vector3.zero;
				rootBone.localRotation = Quaternion.identity;
			}
		}
	}

	public virtual void ServerInit()
	{
		if (simOnServer)
		{
			RemoveRootBoneOffset();
			InvokeRepeating(SyncJointsToClients, 0f, 0.1f);
		}
		else
		{
			MoveRigidbodiesToRoot();
		}
		SetUpPhysics(isServer: true);
	}

	public override void SaveComponent(BaseNetworkable.SaveInfo info)
	{
		if (simOnServer)
		{
			info.msg.ragdoll = Facepunch.Pool.Get<ProtoBuf.Ragdoll>();
			SetRagdollMessageVals(info.msg.ragdoll);
		}
	}

	public bool IsFullySleeping()
	{
		foreach (Rigidbody rigidbody in rigidbodies)
		{
			if (!rigidbody.IsSleeping())
			{
				return false;
			}
		}
		return true;
	}

	private void SyncJointsToClients()
	{
		if (!ShouldSyncJoints())
		{
			return;
		}
		using ProtoBuf.Ragdoll ragdoll = Facepunch.Pool.Get<ProtoBuf.Ragdoll>();
		SetRagdollMessageVals(ragdoll);
		base.baseEntity.ClientRPC(RpcTarget.NetworkGroup("RPCSyncJoints"), ragdoll);
	}

	private bool ShouldSyncJoints()
	{
		bool result = false;
		if (wasSyncingJoints)
		{
			if (!IsFullySleeping())
			{
				result = true;
			}
		}
		else
		{
			result = !primaryBody.IsSleeping();
		}
		wasSyncingJoints = result;
		return result;
	}

	private void SetRagdollMessageVals(ProtoBuf.Ragdoll ragdollMsg)
	{
		List<int> list = Facepunch.Pool.Get<List<int>>();
		List<int> list2 = Facepunch.Pool.Get<List<int>>();
		foreach (Transform rbTransform in rbTransforms)
		{
			int item = Compression.PackVector3ToInt(rbTransform.localPosition, -2f, 2f);
			int item2 = Compression.PackVector3ToInt(rbTransform.localEulerAngles, -360f, 360f);
			list.Add(item);
			list2.Add(item2);
		}
		ragdollMsg.time = base.baseEntity.GetNetworkTime();
		ragdollMsg.positions = list;
		ragdollMsg.rotations = list2;
	}

	public void BecomeActive()
	{
		if (!IsKinematic)
		{
			return;
		}
		foreach (Rigidbody rigidbody in rigidbodies)
		{
			rigidbody.isKinematic = false;
			SetCollisionMode(rigidbody, isServer);
			rigidbody.WakeUp();
			if (base.baseEntity != null && base.baseEntity.HasParent())
			{
				Rigidbody component = base.baseEntity.GetParentEntity().GetComponent<Rigidbody>();
				if (component != null)
				{
					rigidbody.velocity = component.velocity;
					rigidbody.angularVelocity = component.angularVelocity;
				}
			}
			foreach (Collider collider in colliders)
			{
				collider.gameObject.layer = 9;
			}
		}
	}

	public void BecomeInactive()
	{
		if (IsKinematic)
		{
			return;
		}
		foreach (Rigidbody rigidbody in rigidbodies)
		{
			rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
			rigidbody.isKinematic = true;
		}
		foreach (Collider collider in colliders)
		{
			collider.gameObject.layer = 19;
		}
	}
}
