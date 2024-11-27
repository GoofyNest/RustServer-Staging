public interface ISimpleUpgradable
{
	bool UpgradingEnabled();

	bool CanUpgrade(BasePlayer player);

	void DoUpgrade(BasePlayer player);

	ItemDefinition GetUpgradeItem();
}
