namespace Rust.Ai.Gen2;

public class Trans_Triggerable : FSMTransitionBase
{
	protected bool Triggered { get; private set; }

	public void Trigger()
	{
		Triggered = true;
	}

	public override void OnStateEnter()
	{
		Triggered = false;
	}

	public override void OnStateExit()
	{
		Triggered = false;
	}

	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_Triggerable"))
		{
			return Triggered;
		}
	}
}
public class Trans_Triggerable<T> : Trans_Triggerable
{
	private T Parameter;

	public void Trigger(T parameter)
	{
		Parameter = parameter;
		Trigger();
	}

	public override void OnTransitionTaken(FSMStateBase from, FSMStateBase to)
	{
		if (base.Triggered && to is IParametrized<T> parametrized)
		{
			parametrized.SetParameter(Parameter);
		}
	}
}
