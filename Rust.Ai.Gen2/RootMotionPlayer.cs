using System;
using ConVar;
using Network;
using UnityEngine;

namespace Rust.Ai.Gen2;

[SoftRequireComponent(typeof(LimitedTurnNavAgent))]
public class RootMotionPlayer : EntityComponent<BaseEntity>
{
	private struct PlayServerState
	{
		public AnimationClip animCLip;

		public RootMotionData rmData;

		public Action onComplete;

		public float elapsedTime;

		public Vector3 lastFrameOffset;

		public Quaternion initialRotation;

		public Action ServerTickAction;

		public PlayServerState(RootMotionData data, Quaternion initialRotation, Action onComplete)
		{
			rmData = data;
			animCLip = null;
			this.onComplete = onComplete;
			this.initialRotation = initialRotation;
			elapsedTime = 0f;
			lastFrameOffset = Vector3.zero;
			ServerTickAction = null;
		}

		public PlayServerState(AnimationClip data, Quaternion initialRotation, Action onComplete)
		{
			animCLip = data;
			rmData = null;
			this.onComplete = onComplete;
			this.initialRotation = initialRotation;
			elapsedTime = 0f;
			lastFrameOffset = Vector3.zero;
			ServerTickAction = null;
		}

		public int GetAnimHash()
		{
			if (!(rmData != null))
			{
				return Animator.StringToHash(animCLip.name);
			}
			return Animator.StringToHash(rmData.inPlaceAnimation.name);
		}

		public float GetAnimLength()
		{
			if (!(rmData != null))
			{
				return animCLip.length;
			}
			return rmData.inPlaceAnimation.length;
		}
	}

	[Header("Client")]
	[SerializeField]
	private Animator animator;

	private LimitedTurnNavAgent _agent;

	private PlayServerState currentPlayState;

	private Action _playServerTickAction;

	private LockState.LockHandle lockHandle;

	private LimitedTurnNavAgent Agent => _agent ?? (_agent = base.baseEntity.GetComponent<LimitedTurnNavAgent>());

	private Action PlayServerTickAction => PlayServerTick;

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("RootMotionPlayer.OnRpcMessage"))
		{
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public void PlayServer(RootMotionData Data, Action onComplete = null)
	{
		currentPlayState = new PlayServerState(Data, base.baseEntity.transform.rotation, onComplete);
		_PlayServer();
	}

	public void PlayServer(AnimationClip Data, Action onComplete = null)
	{
		currentPlayState = new PlayServerState(Data, base.baseEntity.transform.rotation, onComplete);
		_PlayServer();
	}

	private void _PlayServer()
	{
		StopServer(sendStopClientRPC: false);
		base.baseEntity.ClientRPC(RpcTarget.NetworkGroup("CL_PlayAnimation"), currentPlayState.GetAnimHash());
		lockHandle = Agent.Pause();
		base.baseEntity.InvokeRepeating(PlayServerTickAction, 0f, 0f);
	}

	private void PlayServerTick()
	{
		using (TimeWarning.New("RootMotionPlayer:PlayServerTick"))
		{
			if (currentPlayState.rmData != null)
			{
				float x = currentPlayState.rmData.xMotionCurve.Evaluate(currentPlayState.elapsedTime);
				float z = currentPlayState.rmData.zMotionCurve.Evaluate(currentPlayState.elapsedTime);
				float y = currentPlayState.rmData.yRotationCurve.Evaluate(currentPlayState.elapsedTime);
				Vector3 vector = currentPlayState.initialRotation * new Vector3(x, 0f, z);
				Vector3 offset = vector - currentPlayState.lastFrameOffset;
				currentPlayState.lastFrameOffset = vector;
				Agent.Move(offset);
				base.baseEntity.transform.rotation = Quaternion.Euler(0f, y, 0f) * currentPlayState.initialRotation;
			}
			currentPlayState.elapsedTime += UnityEngine.Time.deltaTime;
			if (currentPlayState.elapsedTime >= currentPlayState.GetAnimLength() - ConVar.Animation.defaultFadeDuration)
			{
				StopServer(sendStopClientRPC: false);
				currentPlayState.onComplete?.Invoke();
			}
		}
	}

	public void StopServer(bool sendStopClientRPC = true)
	{
		if (base.baseEntity.IsInvoking(PlayServerTickAction))
		{
			base.baseEntity.CancelInvoke(PlayServerTickAction);
			Agent.Unpause(ref lockHandle);
			if (sendStopClientRPC)
			{
				base.baseEntity.ClientRPC(RpcTarget.NetworkGroup("CL_StopAnimation"));
			}
		}
	}
}
