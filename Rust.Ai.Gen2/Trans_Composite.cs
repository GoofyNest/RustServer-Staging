using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
public abstract class Trans_Composite : FSMTransitionBase, IEnumerable<FSMTransitionBase>, IEnumerable
{
	[SerializeField]
	protected List<FSMTransitionBase> transitions = new List<FSMTransitionBase>();

	public IEnumerator<FSMTransitionBase> GetEnumerator()
	{
		return transitions.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public void Add(FSMTransitionBase transition)
	{
		transitions.Add(transition);
	}

	public Trans_Composite()
	{
	}

	public override void Init(BaseEntity owner)
	{
		base.Init(owner);
		foreach (FSMTransitionBase transition in transitions)
		{
			transition.Init(owner);
		}
	}

	public Trans_Composite(List<FSMTransitionBase> transitions)
	{
		this.transitions = transitions;
	}

	public override void OnStateEnter()
	{
		foreach (FSMTransitionBase transition in transitions)
		{
			transition.OnStateEnter();
		}
	}

	public override void OnStateExit()
	{
		foreach (FSMTransitionBase transition in transitions)
		{
			transition.OnStateExit();
		}
	}

	public override void OnTransitionTaken(FSMStateBase from, FSMStateBase to)
	{
		foreach (FSMTransitionBase transition in transitions)
		{
			transition.OnTransitionTaken(from, to);
		}
	}

	protected virtual string GetNameSeparator()
	{
		return " ";
	}

	public override string GetName()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("(");
		for (int i = 0; i < transitions.Count; i++)
		{
			stringBuilder.Append(transitions[i].GetName());
			if (i < transitions.Count - 1)
			{
				stringBuilder.Append(" ");
				stringBuilder.Append(GetNameSeparator());
				stringBuilder.Append(" ");
			}
		}
		stringBuilder.Append(" )");
		return stringBuilder.ToString();
	}

	public override FSMTransitionBase Clone()
	{
		Trans_Composite obj = base.Clone() as Trans_Composite;
		obj.transitions = new List<FSMTransitionBase>();
		return obj;
	}
}
