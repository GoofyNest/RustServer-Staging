namespace Rust.Ai.Gen2;

public class AIArena2 : FacepunchBehaviour, IClientComponent
{
	private void Start()
	{
		Invoke(ExecuteCommands, 2f);
	}

	private void ExecuteCommands()
	{
		ConsoleSystem.Run(ConsoleSystem.Option.Client, "vddraw.setisrecording t");
		ConsoleSystem.Run(ConsoleSystem.Option.Client, "noclip");
		ConsoleSystem.Run(ConsoleSystem.Option.Client, "ai.showstate");
		ConsoleSystem.Run(ConsoleSystem.Option.Client, "bear.population 0");
		ConsoleSystem.Run(ConsoleSystem.Option.Client, "polarbear.population 0");
		ConsoleSystem.Run(ConsoleSystem.Option.Client, "stag.population 0");
		ConsoleSystem.Run(ConsoleSystem.Option.Client, "boar.population 0");
		ConsoleSystem.Run(ConsoleSystem.Option.Client, "chicken.population 0");
		ConsoleSystem.Run(ConsoleSystem.Option.Client, "halloween.scarecrowpopulation 0");
		ConsoleSystem.Run(ConsoleSystem.Option.Client, "halloween.murdererpopulation 0");
		ConsoleSystem.Run(ConsoleSystem.Option.Client, "ai.killanimals");
		ConsoleSystem.Run(ConsoleSystem.Option.Client, "ai.move t");
	}
}
