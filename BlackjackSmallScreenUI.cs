using Rust.UI;
using UnityEngine;
using UnityEngine.UI;

public class BlackjackSmallScreenUI : FacepunchBehaviour
{
	[SerializeField]
	private BlackjackMachine blackjack;

	[SerializeField]
	private Canvas notInGameDisplay;

	[SerializeField]
	private Canvas inGameDisplay;

	[SerializeField]
	private RustText cardCountText;

	[SerializeField]
	private RustText betText;

	[SerializeField]
	private RustText splitBetText;

	[SerializeField]
	private RustText insuranceText;

	[SerializeField]
	private RustText bankText;

	[SerializeField]
	private Canvas infoTextCanvas;

	[SerializeField]
	private RustText inGameText;

	[SerializeField]
	private RustText notInGameText;

	[SerializeField]
	private HorizontalLayoutGroup cardsLayout;

	[SerializeField]
	private BlackjackScreenCardUI[] cards;

	[SerializeField]
	private BlackjackScreenInputUI[] inputs;

	[SerializeField]
	private Translate.Phrase phraseBust;

	[SerializeField]
	private Translate.Phrase phraseBet;

	[SerializeField]
	private Translate.Phrase phrasePlaceYourBet;

	[SerializeField]
	private Translate.Phrase phraseStandoff;

	[SerializeField]
	private Translate.Phrase phraseYouWin;

	[SerializeField]
	private Translate.Phrase phraseYouLose;

	[SerializeField]
	private Translate.Phrase phraseWaitingForOtherPlayers;

	[SerializeField]
	private Translate.Phrase phraseAddFunds;

	[SerializeField]
	private Translate.Phrase phraseWaitingForPlayer;
}
