using Facepunch.CardGames;
using UnityEngine;

public class BlackjackMachine : BaseCardGameEntity
{
	[Header("Blackjack Machine")]
	[SerializeField]
	private BlackjackMainScreenUI mainScreenUI;

	[SerializeField]
	private BlackjackSmallScreenUI[] smallScreenUIs;

	[SerializeField]
	private Canvas[] worldSpaceCanvases;

	private BlackjackController controller;

	protected override float MaxStorageInteractionDist => 1f;

	public override void InitShared()
	{
		base.InitShared();
		controller = (BlackjackController)base.GameController;
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
	}

	public override void PlayerStorageChanged()
	{
		base.PlayerStorageChanged();
		SendNetworkUpdate();
	}
}
