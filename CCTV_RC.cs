#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

[ConsoleSystem.Factory("cctv")]
public class CCTV_RC : PoweredRemoteControlEntity, IRemoteControllableClientCallbacks, IRemoteControllable
{
	public Transform pivotOrigin;

	public Transform yaw;

	public Transform pitch;

	public Vector2 pitchClamp = new Vector2(-50f, 50f);

	public Vector2 yawClamp = new Vector2(-50f, 50f);

	public float turnSpeed = 25f;

	public float serverLerpSpeed = 15f;

	public float clientLerpSpeed = 10f;

	public float zoomLerpSpeed = 10f;

	public float[] fovScales;

	private float pitchAmount;

	private float yawAmount;

	private int fovScaleIndex;

	private float fovScaleLerped = 1f;

	public bool hasPTZ = true;

	public AnimationCurve dofCurve = AnimationCurve.Constant(0f, 1f, 0f);

	public float dofApertureMax = 10f;

	public const Flags Flag_HasViewer = Flags.Reserved5;

	public bool disableWhenShot = true;

	[ServerVar(Name = "camera_disable_seconds")]
	public static float CameraDisableSeconds = 300f;

	public SoundDefinition movementLoopSoundDef;

	public AnimationCurve movementLoopGainCurve;

	public float movementLoopSmoothing = 1f;

	public float movementLoopReference = 50f;

	private Sound movementLoop;

	private SoundModulation.Modulator movementLoopGainModulator;

	public SoundDefinition zoomInSoundDef;

	public SoundDefinition zoomOutSoundDef;

	private RealTimeSinceEx timeSinceLastServerTick;

	public override bool RequiresMouse => hasPTZ;

	protected override bool EntityCanPing => true;

	public override bool CanAcceptInput => hasPTZ;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("CCTV_RC.OnRpcMessage"))
		{
			if (rpc == 3353964129u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_SetDir ");
				}
				using (TimeWarning.New("Server_SetDir"))
				{
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg2 = rPCMessage;
							Server_SetDir(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in Server_SetDir");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override int ConsumptionAmount()
	{
		return 3;
	}

	public override void ServerInit()
	{
		base.ServerInit();
		if (!base.isClient)
		{
			if (IsStatic())
			{
				pitchAmount = pitch.localEulerAngles.x;
				yawAmount = yaw.localEulerAngles.y;
				UpdateRCAccess(isOnline: true);
			}
			timeSinceLastServerTick = 0.0;
			InvokeRandomized(ServerTick, UnityEngine.Random.Range(0f, 1f), 0.015f, 0.01f);
		}
	}

	public override void PostServerLoad()
	{
		base.PostServerLoad();
		UpdateRotation(10000f);
	}

	public override void UserInput(InputState inputState, CameraViewerId viewerID)
	{
		if (UpdateManualAim(inputState))
		{
			SendNetworkUpdate();
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		if (info.msg.rcEntity == null)
		{
			info.msg.rcEntity = Facepunch.Pool.Get<RCEntity>();
		}
		info.msg.rcEntity.aim.x = pitchAmount;
		info.msg.rcEntity.aim.y = yawAmount;
		info.msg.rcEntity.aim.z = 0f;
		info.msg.rcEntity.zoom = fovScaleIndex;
	}

	public override void Hurt(HitInfo info)
	{
		DamageType majorityDamageType = info.damageTypes.GetMajorityDamageType();
		if ((uint)(majorityDamageType - 9) <= 2u || (uint)(majorityDamageType - 15) <= 1u)
		{
			TryDisableCamera();
		}
		base.Hurt(info);
	}

	private void TryDisableCamera()
	{
		CancelInvoke(EndCameraDisable);
		if (disableWhenShot)
		{
			SetFlag(Flags.OnFire, b: true);
			Invoke(EndCameraDisable, CameraDisableSeconds);
		}
	}

	private void EndCameraDisable()
	{
		SetFlag(Flags.OnFire, b: false);
	}

	[RPC_Server]
	public void Server_SetDir(RPCMessage msg)
	{
		if (!IsStatic())
		{
			BasePlayer player = msg.player;
			if (player.CanBuild() && player.IsBuildingAuthed())
			{
				Vector3 direction = Vector3Ex.Direction(player.eyes.position, yaw.transform.position);
				direction = base.transform.InverseTransformDirection(direction);
				Vector3 vector = BaseMountable.ConvertVector(Quaternion.LookRotation(direction).eulerAngles);
				pitchAmount = Mathf.Clamp(vector.x, pitchClamp.x, pitchClamp.y);
				yawAmount = Mathf.Clamp(vector.y, yawClamp.x, yawClamp.y);
				SendNetworkUpdate();
			}
		}
	}

	public override bool InitializeControl(CameraViewerId viewerID)
	{
		bool result = base.InitializeControl(viewerID);
		UpdateViewers();
		return result;
	}

	public override void StopControl(CameraViewerId viewerID)
	{
		base.StopControl(viewerID);
		UpdateViewers();
	}

	public void UpdateViewers()
	{
		SetFlag(Flags.Reserved5, base.ViewerCount > 0);
	}

	public void ServerTick()
	{
		if (!base.isClient && !base.IsDestroyed)
		{
			float delta = (float)(double)timeSinceLastServerTick;
			timeSinceLastServerTick = 0.0;
			UpdateRotation(delta);
			if (HasFlag(Flags.Reserved5) && !isStatic)
			{
				bool b = IsObstructedByGeometry();
				SetFlag(Flags.Locked, b);
			}
		}
	}

	private bool IsObstructedByGeometry()
	{
		Vector3 vector = viewEyes.position - viewEyes.forward * 0.1f;
		List<Collider> obj = Facepunch.Pool.Get<List<Collider>>();
		GamePhysics.OverlapSphere(vector, 0.17f, obj, 2162688);
		bool flag = false;
		foreach (Collider item in obj)
		{
			BaseEntity baseEntity = item.ToBaseEntity();
			if (!(baseEntity != null) || !(baseEntity == this))
			{
				flag = true;
			}
		}
		if (!flag)
		{
			flag = GamePhysics.Trace(new Ray(vector, viewEyes.forward), 0f, out var _, 0.2f, 2162688);
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return flag;
	}

	private bool UpdateManualAim(InputState inputState)
	{
		if (!hasPTZ)
		{
			return false;
		}
		float num = 0f - inputState.current.mouseDelta.y;
		float x = inputState.current.mouseDelta.x;
		bool flag = inputState.WasJustPressed(BUTTON.FIRE_PRIMARY);
		pitchAmount = Mathf.Clamp(pitchAmount + num * turnSpeed, pitchClamp.x, pitchClamp.y);
		yawAmount = Mathf.Clamp(yawAmount + x * turnSpeed, yawClamp.x, yawClamp.y) % 360f;
		if (flag)
		{
			fovScaleIndex = (fovScaleIndex + 1) % fovScales.Length;
		}
		return num != 0f || x != 0f || flag;
	}

	public void UpdateRotation(float delta)
	{
		Quaternion to = Quaternion.Euler(pitchAmount, 0f, 0f);
		Quaternion to2 = Quaternion.Euler(0f, yawAmount, 0f);
		float speed = ((base.isServer && !base.IsBeingControlled) ? serverLerpSpeed : clientLerpSpeed);
		pitch.transform.localRotation = Mathx.Lerp(pitch.transform.localRotation, to, speed, delta);
		yaw.transform.localRotation = Mathx.Lerp(yaw.transform.localRotation, to2, speed, delta);
		if (fovScales != null && fovScales.Length != 0)
		{
			if (fovScales.Length > 1)
			{
				fovScaleLerped = Mathx.Lerp(fovScaleLerped, fovScales[fovScaleIndex], zoomLerpSpeed, delta);
			}
			else
			{
				fovScaleLerped = fovScales[0];
			}
		}
		else
		{
			fovScaleLerped = 1f;
		}
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		if (info.msg.rcEntity != null)
		{
			int num = Mathf.Clamp((int)info.msg.rcEntity.zoom, 0, fovScales.Length - 1);
			if (base.isServer)
			{
				pitchAmount = info.msg.rcEntity.aim.x;
				yawAmount = info.msg.rcEntity.aim.y;
				fovScaleIndex = num;
			}
		}
	}

	public override float GetFovScale()
	{
		return fovScaleLerped;
	}
}
