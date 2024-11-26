using System;
using UnityEngine;

namespace Rust.Ai.Gen2;

[Serializable]
internal class Trans_HasBlackboardBool : FSMTransitionBase
{
	[SerializeField]
	public string Key;

	private BlackboardComponent _blackboard;

	private BlackboardComponent Blackboard => _blackboard ?? (_blackboard = Owner.GetComponent<BlackboardComponent>());

	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_HasBlackboardBool"))
		{
			return Blackboard.Has(Key);
		}
	}

	public override string GetName()
	{
		return base.GetName() + " " + Key;
	}
}
