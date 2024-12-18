#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Network;
using UnityEngine;
using UnityEngine.Assertions;

public class BaseMetalDetector : HeldEntity
{
	public enum DetectState
	{
		LongRange,
		SweetSpot
	}

	public DetectState State;

	public float LongRangeDetectionRange = 20f;

	public float SweetSpotDetectionRange = 0.2f;

	public SoundDefinition BeepSoundEffect;

	[ServerVar]
	public static float NearestDistanceTick = 0.25f;

	[ServerVar]
	public static float DetectLongRangeTick = 1f;

	[ServerVar]
	public static float DetectMinMovementDistance = 1f;

	private List<IMetalDetectable> inRangeSources = new List<IMetalDetectable>();

	private IMetalDetectable nearestSource;

	private float nearestSourceDistanceSqr;

	private Vector3 lastDetectPlayerPos;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("BaseMetalDetector.OnRpcMessage"))
		{
			if (rpc == 2192859691u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_RequestFlag ");
				}
				using (TimeWarning.New("RPC_RequestFlag"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(2192859691u, "RPC_RequestFlag", this, player, 2uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(2192859691u, "RPC_RequestFlag", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage rpc2 = rPCMessage;
							RPC_RequestFlag(rpc2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in RPC_RequestFlag");
					}
				}
				return true;
			}
			if (rpc == 50929187 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - SV_SetSweetspotScanning ");
				}
				using (TimeWarning.New("SV_SetSweetspotScanning"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(50929187u, "SV_SetSweetspotScanning", this, player, 6uL))
						{
							return true;
						}
						if (!RPC_Server.FromOwner.Test(50929187u, "SV_SetSweetspotScanning", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg2 = rPCMessage;
							SV_SetSweetspotScanning(msg2);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in SV_SetSweetspotScanning");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void OnHeldChanged()
	{
		base.OnHeldChanged();
		if (IsDeployed())
		{
			StartDetecting();
		}
		else
		{
			StopDetecting();
		}
	}

	private void StartDetecting()
	{
		lastDetectPlayerPos = Vector3.zero;
		if (!IsInvoking(DetectLongRange))
		{
			InvokeRepeating(DetectLongRange, 0f, DetectLongRangeTick);
			InvokeRepeating(SendNearestDistance, 0f, NearestDistanceTick);
		}
	}

	private void StopDetecting()
	{
		CancelInvoke(DetectLongRange);
		CancelInvoke(SendNearestDistance);
		ClearSources();
	}

	private void SendNearestDistance()
	{
		BasePlayer ownerPlayer = GetOwnerPlayer();
		if (!(ownerPlayer == null))
		{
			float distanceToCenterOrNearestSubSource = GetDistanceToCenterOrNearestSubSource(nearestSource);
			ClientRPC(RpcTarget.Player("CL_UpdateNearest", ownerPlayer), distanceToCenterOrNearestSubSource, (nearestSource != null) ? nearestSource.GetRadius() : 1f);
		}
	}

	private float GetDistanceToCenterOrNearestSubSource(IMetalDetectable source)
	{
		if (source == null)
		{
			return float.PositiveInfinity;
		}
		Vector3 detectionPoint = GetDetectionPoint();
		return Vector3.Distance(source.GetNearestPosition(detectionPoint), detectionPoint);
	}

	private void ProcessDetectedSources()
	{
		if (GetOwnerPlayer() == null)
		{
			nearestSource = null;
		}
		nearestSourceDistanceSqr = float.PositiveInfinity;
		IMetalDetectable metalDetectable = null;
		Vector3 detectionPoint = GetDetectionPoint();
		foreach (IMetalDetectable inRangeSource in inRangeSources)
		{
			if (inRangeSource == null)
			{
				continue;
			}
			foreach (Vector3 scanLocation in inRangeSource.GetScanLocations())
			{
				float num = Vector3.SqrMagnitude(scanLocation - detectionPoint);
				if (num < nearestSourceDistanceSqr)
				{
					nearestSourceDistanceSqr = num;
					metalDetectable = inRangeSource;
				}
			}
		}
		nearestSource = metalDetectable;
	}

	private void DetectLongRange()
	{
		BasePlayer ownerPlayer = GetOwnerPlayer();
		if (ownerPlayer == null || ownerPlayer.GetHeldEntity() != this)
		{
			StopDetecting();
			return;
		}
		if (Vector3.SqrMagnitude(ownerPlayer.transform.position - lastDetectPlayerPos) < DetectMinMovementDistance)
		{
			ProcessDetectedSources();
			return;
		}
		DetectSources(ownerPlayer);
		ProcessDetectedSources();
	}

	private void DetectSources(BasePlayer player)
	{
		lastDetectPlayerPos = player.transform.position;
		List<IMetalDetectable> obj = Facepunch.Pool.Get<List<IMetalDetectable>>();
		if (!player.InSafeZone())
		{
			Vis.Entities(base.transform.position, LongRangeDetectionRange + 5f, obj, 512, QueryTriggerInteraction.Ignore);
		}
		inRangeSources.Clear();
		inRangeSources.AddRange(obj);
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	private void ClearSources()
	{
		nearestSource = null;
		inRangeSources.Clear();
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(2uL)]
	private void RPC_RequestFlag(RPCMessage rpc)
	{
		BasePlayer player = rpc.player;
		if (!(player == null) && !player.InSafeZone() && nearestSource != null)
		{
			Vector3 pos = rpc.read.Vector3();
			if (nearestSource.VerifyScanPosition(player.transform.position, pos, out var spotPos))
			{
				nearestSource.Detected(spotPos);
			}
		}
	}

	[RPC_Server]
	[RPC_Server.FromOwner]
	[RPC_Server.CallsPerSecond(6uL)]
	public void SV_SetSweetspotScanning(RPCMessage msg)
	{
		if (!(msg.player == null) && !(msg.player != GetOwnerPlayer()))
		{
			bool b = msg.read.Bit();
			SetFlag(Flags.On, b);
		}
	}

	public Vector3 GetDetectionPoint()
	{
		BasePlayer ownerPlayer = GetOwnerPlayer();
		if (ownerPlayer == null)
		{
			return base.transform.position;
		}
		Vector3 vector = ownerPlayer.transform.position + ownerPlayer.eyes.MovementForward() * 0.3f;
		if (UnityEngine.Physics.Raycast(vector + Vector3.up * 0.5f, Vector3.down, out var hitInfo, 1.5f, 8388608))
		{
			return hitInfo.point;
		}
		return vector;
	}

	public float GetSweetSpotDistancePercent(float distance, float sourceSpawnRadius)
	{
		if (State != DetectState.SweetSpot)
		{
			return 0f;
		}
		if (distance > sourceSpawnRadius)
		{
			return 0f;
		}
		return Mathf.Clamp01(1f - distance / sourceSpawnRadius);
	}
}
