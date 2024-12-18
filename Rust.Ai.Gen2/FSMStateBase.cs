using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rust.Ai.Gen2;

public abstract class FSMStateBase
{
	[NonSerialized]
	public BaseEntity Owner;

	[SerializeField]
	private string _Name;

	public List<(FSMTransitionBase transition, FSMStateBase dstState)> transitions = new List<(FSMTransitionBase, FSMStateBase)>();

	public List<(FSMTransitionBase transition, FSMStateBase dstState, EFSMStateStatus status)> endTransitions = new List<(FSMTransitionBase, FSMStateBase, EFSMStateStatus)>();

	private SenseComponent _senses;

	private LimitedTurnNavAgent _agent;

	private RootMotionPlayer _animPlayer;

	private BlackboardComponent _blackboard;

	public string Name
	{
		get
		{
			if (string.IsNullOrEmpty(_Name))
			{
				_Name = GetType().Name.Replace("State_", "");
			}
			return _Name;
		}
		set
		{
			_Name = value;
		}
	}

	protected SenseComponent Senses => _senses ?? (_senses = Owner.GetComponent<SenseComponent>());

	protected LimitedTurnNavAgent Agent => _agent ?? (_agent = Owner.GetComponent<LimitedTurnNavAgent>());

	protected RootMotionPlayer AnimPlayer => _animPlayer ?? (_animPlayer = Owner.GetComponent<RootMotionPlayer>());

	protected BlackboardComponent Blackboard => _blackboard ?? (_blackboard = Owner.GetComponent<BlackboardComponent>());

	public virtual EFSMStateStatus OnStateEnter()
	{
		return EFSMStateStatus.None;
	}

	public virtual EFSMStateStatus OnStateUpdate(float deltaTime)
	{
		return EFSMStateStatus.None;
	}

	public virtual void OnStateExit()
	{
	}

	protected T GetRootFSM<T>() where T : FSMComponent
	{
		return Owner.GetComponent<T>();
	}

	public virtual FSMStateBase Clone()
	{
		FSMStateBase obj = (FSMStateBase)MemberwiseClone();
		obj.transitions = new List<(FSMTransitionBase, FSMStateBase)>();
		obj.endTransitions = new List<(FSMTransitionBase, FSMStateBase, EFSMStateStatus)>();
		return obj;
	}
}
