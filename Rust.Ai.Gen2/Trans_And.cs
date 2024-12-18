namespace Rust.Ai.Gen2;

public class Trans_And : Trans_Composite
{
	protected override bool EvaluateInternal()
	{
		using (TimeWarning.New("Trans_And"))
		{
			foreach (FSMTransitionBase transition in transitions)
			{
				if (!transition.Evaluate())
				{
					return false;
				}
			}
			return true;
		}
	}

	protected override string GetNameSeparator()
	{
		return "&&";
	}
}
