using System;
using System.Linq;
using UnityEngine;

namespace Rust.Ai.Gen2;

public abstract class FSMTransitionBase
{
	[SerializeField]
	public bool Inverted;

	[NonSerialized]
	public BaseEntity Owner;

	private SenseComponent _senses;

	private LimitedTurnNavAgent _agent;

	protected SenseComponent Senses => _senses ?? (_senses = Owner.GetComponent<SenseComponent>());

	protected LimitedTurnNavAgent Agent => _agent ?? (_agent = Owner.GetComponent<LimitedTurnNavAgent>());

	public virtual void Init(BaseEntity owner)
	{
		Owner = owner;
	}

	public virtual void OnStateEnter()
	{
	}

	public virtual void OnStateExit()
	{
	}

	public virtual void OnTransitionTaken(FSMStateBase from, FSMStateBase to)
	{
	}

	public bool Evaluate()
	{
		if (!Inverted)
		{
			return EvaluateInternal();
		}
		return !EvaluateInternal();
	}

	protected virtual bool EvaluateInternal()
	{
		return false;
	}

	public virtual string GetName()
	{
		return (Inverted ? "!" : "") + GetGenericTypeName(GetType());
	}

	protected static string GetGenericTypeName(Type type)
	{
		if (type.IsGenericType)
		{
			string name = type.Name;
			return (name[..name.IndexOf('`')] + "<" + string.Join(", ", type.GetGenericArguments().Select(GetGenericTypeName)) + ">").Replace("Trans_", "");
		}
		return type.Name.Replace("Trans_", "");
	}

	public virtual FSMTransitionBase Clone()
	{
		return (FSMTransitionBase)MemberwiseClone();
	}
}
