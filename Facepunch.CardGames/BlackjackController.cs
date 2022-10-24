using System;
using System.Collections.Generic;
using ProtoBuf;
using UnityEngine;

namespace Facepunch.CardGames;

public class BlackjackController : CardGameController
{
	[Flags]
	public enum BlackjackInputOption
	{
		None = 0,
		SubmitBet = 1,
		Hit = 2,
		Stand = 4,
		Split = 8,
		DoubleDown = 0x10,
		Insurance = 0x20,
		AllIn = 0x40,
		Abandon = 0x80
	}

	public enum BlackjackRoundResult
	{
		NotInRound,
		Bust,
		Loss,
		Standoff,
		Win,
		BlackjackWin
	}

	public enum CardsValueMode
	{
		Low,
		High
	}

	private enum BetType
	{
		Main,
		Split,
		Insurance
	}

	public List<PlayingCard> dealerCards = new List<PlayingCard>();

	public const float BLACKJACK_PAYOUT_RATIO = 1.5f;

	public const float INSURANCE_PAYOUT_RATIO = 2f;

	private const int NUM_DECKS = 6;

	private StackOfCards cardStack = new StackOfCards(6);

	public override int MinPlayers => 1;

	public override int MinBuyIn => 5;

	public override int MaxBuyIn => int.MaxValue;

	public override int MinToPlay => MinBuyIn;

	public override int TimeBetweenRounds => 4;

	public BlackjackInputOption LastAction { get; private set; }

	public ulong LastActionTarget { get; private set; }

	public int LastActionValue { get; private set; }

	public bool AllBetsPlaced
	{
		get
		{
			if (!base.HasRoundInProgress)
			{
				return false;
			}
			foreach (CardPlayerData item in PlayersInRound())
			{
				if (item.betThisRound == 0)
				{
					return false;
				}
			}
			return true;
		}
	}

	public BlackjackController(BaseCardGameEntity owner)
		: base(owner)
	{
	}

	protected override int GetFirstPlayerRelIndex(bool startOfRound)
	{
		return 0;
	}

	public override List<PlayingCard> GetTableCards()
	{
		return dealerCards;
	}

	public void InputsToList(int availableInputs, List<BlackjackInputOption> result)
	{
		BlackjackInputOption[] array = (BlackjackInputOption[])Enum.GetValues(typeof(BlackjackInputOption));
		foreach (BlackjackInputOption blackjackInputOption in array)
		{
			if (blackjackInputOption != 0 && ((uint)availableInputs & (uint)blackjackInputOption) == (uint)blackjackInputOption)
			{
				result.Add(blackjackInputOption);
			}
		}
	}

	public bool WaitingForOtherPlayer(CardPlayerData pData)
	{
		if (!pData.HasUserInCurrentRound)
		{
			return false;
		}
		if (base.State == CardGameState.InGameRound && !pData.HasAvailableInputs)
		{
			foreach (CardPlayerData item in PlayersInRound())
			{
				if (item != pData && item.HasAvailableInputs)
				{
					return true;
				}
			}
		}
		return false;
	}

	public int GetCardsValue(List<PlayingCard> cards, CardsValueMode mode)
	{
		int num = 0;
		foreach (PlayingCard card in cards)
		{
			if (!card.IsUnknownCard)
			{
				num += GetCardValue(card, mode);
			}
		}
		return num;
	}

	public int GetOptimalCardsValue(List<PlayingCard> cards)
	{
		int cardsValue = GetCardsValue(cards, CardsValueMode.Low);
		int cardsValue2 = GetCardsValue(cards, CardsValueMode.High);
		if (cardsValue2 <= 21)
		{
			return cardsValue2;
		}
		return cardsValue;
	}

	public int GetCardValue(PlayingCard card, CardsValueMode mode)
	{
		int rank = (int)card.Rank;
		if (rank <= 8)
		{
			return rank + 2;
		}
		if (rank <= 11)
		{
			return 10;
		}
		if (mode != 0)
		{
			return 11;
		}
		return 1;
	}

	public bool Has21(List<PlayingCard> cards)
	{
		return GetOptimalCardsValue(cards) == 21;
	}

	public bool HasBlackjack(List<PlayingCard> cards)
	{
		if (GetCardsValue(cards, CardsValueMode.High) == 21)
		{
			return cards.Count == 2;
		}
		return false;
	}

	public bool HasBusted(List<PlayingCard> cards)
	{
		return GetCardsValue(cards, CardsValueMode.Low) > 21;
	}

	private bool CanSplit(CardPlayerData pData)
	{
		if (pData.Cards.Count != 2)
		{
			return false;
		}
		if (HasSplit(pData))
		{
			return false;
		}
		int betThisRound = pData.betThisRound;
		if (pData.GetScrapAmount() < betThisRound)
		{
			return false;
		}
		return GetCardValue(pData.Cards[0], CardsValueMode.Low) == GetCardValue(pData.Cards[1], CardsValueMode.Low);
	}

	private bool HasAnyAces(List<PlayingCard> cards)
	{
		foreach (PlayingCard card in cards)
		{
			if (card.Rank == Rank.Ace)
			{
				return true;
			}
		}
		return false;
	}

	private bool CanDoubleDown(CardPlayerData pData)
	{
		if (HasSplit(pData))
		{
			return false;
		}
		int cardsValue = GetCardsValue(pData.Cards, CardsValueMode.Low);
		if (cardsValue < 9 || cardsValue > 11)
		{
			return false;
		}
		if (HasAnyAces(pData.Cards))
		{
			return false;
		}
		int betThisRound = pData.betThisRound;
		return pData.GetScrapAmount() >= betThisRound;
	}

	private bool CanTakeInsurance(CardPlayerData pData)
	{
		if (dealerCards.Count != 2)
		{
			return false;
		}
		if (dealerCards[1].Rank != Rank.Ace)
		{
			return false;
		}
		if (pData.sideBetBThisRound > 0)
		{
			return false;
		}
		int num = Mathf.FloorToInt((float)pData.betThisRound / 2f);
		return pData.GetScrapAmount() >= num;
	}

	private bool HasSplit(CardPlayerData pData)
	{
		return pData.PocketCards.Count > 0;
	}

	public override void Save(CardGame syncData)
	{
		base.Save(syncData);
		syncData.blackjack = Pool.Get<CardGame.Blackjack>();
		syncData.blackjack.dealerCards = Pool.GetList<int>();
		syncData.lastActionId = (int)LastAction;
		syncData.lastActionTarget = LastActionTarget;
		syncData.lastActionValue = LastActionValue;
		for (int i = 0; i < dealerCards.Count; i++)
		{
			PlayingCard playingCard = dealerCards[i];
			if (base.HasRoundInProgress && i == 0)
			{
				syncData.blackjack.dealerCards.Add(-1);
			}
			else
			{
				syncData.blackjack.dealerCards.Add(playingCard.GetIndex());
			}
		}
		ClearLastAction();
	}

	private void EditorMakeRandomMove(CardPlayerData data)
	{
		List<BlackjackInputOption> obj = Pool.GetList<BlackjackInputOption>();
		InputsToList(data.availableInputs, obj);
		if (obj.Count == 0)
		{
			Debug.Log("No moves currently available.");
			Pool.FreeList(ref obj);
			return;
		}
		BlackjackInputOption blackjackInputOption = obj[UnityEngine.Random.Range(0, obj.Count)];
		if (AllBetsPlaced)
		{
			if (GetOptimalCardsValue(data.Cards) < 17 && obj.Contains(BlackjackInputOption.Hit))
			{
				blackjackInputOption = BlackjackInputOption.Hit;
			}
			else if (obj.Contains(BlackjackInputOption.Stand))
			{
				blackjackInputOption = BlackjackInputOption.Stand;
			}
		}
		else if (obj.Contains(BlackjackInputOption.SubmitBet))
		{
			blackjackInputOption = BlackjackInputOption.SubmitBet;
		}
		if (obj.Count > 0)
		{
			int num = 0;
			if (blackjackInputOption == BlackjackInputOption.SubmitBet)
			{
				num = MinBuyIn;
			}
			Debug.Log(string.Concat(data.UserID, " Taking random action: ", blackjackInputOption, " with value ", num));
			ReceivedInputFromPlayer(data, (int)blackjackInputOption, countAsAction: true, num);
		}
		else
		{
			Debug.LogWarning(GetType().Name + ": No input options are available for the current player.");
		}
		Pool.FreeList(ref obj);
	}

	protected override int GetAvailableInputsForPlayer(CardPlayerData playerData)
	{
		BlackjackInputOption blackjackInputOption = BlackjackInputOption.None;
		if (playerData == null || isWaitingBetweenTurns || playerData.hasCompletedTurn || !playerData.HasUserInGame)
		{
			return (int)blackjackInputOption;
		}
		if (!base.HasRoundInProgress)
		{
			return (int)blackjackInputOption;
		}
		if (AllBetsPlaced)
		{
			blackjackInputOption |= BlackjackInputOption.Stand;
			blackjackInputOption |= BlackjackInputOption.Hit;
			CanSplit(playerData);
			CanDoubleDown(playerData);
			if (CanTakeInsurance(playerData))
			{
				blackjackInputOption |= BlackjackInputOption.Insurance;
			}
		}
		else
		{
			blackjackInputOption |= BlackjackInputOption.SubmitBet;
			blackjackInputOption |= BlackjackInputOption.AllIn;
		}
		return (int)blackjackInputOption;
	}

	protected override void SubEndGameplay()
	{
		dealerCards.Clear();
	}

	protected override void SubEndRound()
	{
		int dealerCardsVal = GetOptimalCardsValue(dealerCards);
		if (dealerCardsVal > 21)
		{
			dealerCardsVal = 0;
		}
		base.resultInfo.winningScore = dealerCardsVal;
		if (NumPlayersInCurrentRound() == 0)
		{
			base.Owner.ClientRPC(null, "OnResultsDeclared", base.resultInfo);
			return;
		}
		bool dealerHasBlackjack = HasBlackjack(dealerCards);
		if (dealerHasBlackjack)
		{
			foreach (CardPlayerData item in PlayersInRound())
			{
				if (item.sideBetBThisRound > 0)
				{
					int amount = Mathf.FloorToInt((float)item.sideBetBThisRound * 2f);
					item.GetStorage().inventory.AddItem(base.Owner.scrapItemDef, amount, 0uL);
				}
			}
		}
		foreach (CardPlayerData item2 in PlayersInRound())
		{
			CardPlayerData pData = item2;
			CheckResult(pData.Cards, pData.betThisRound);
			CheckResult(pData.PocketCards, pData.sideBetAThisRound);
			void CheckResult(List<PlayingCard> cards, int betAmount)
			{
				int optimalCardsValue = GetOptimalCardsValue(cards);
				if (optimalCardsValue > 21)
				{
					AddRoundResult(pData, 0, 1);
				}
				else
				{
					if (optimalCardsValue > base.resultInfo.winningScore)
					{
						base.resultInfo.winningScore = optimalCardsValue;
					}
					BlackjackRoundResult blackjackRoundResult = BlackjackRoundResult.Loss;
					bool flag = HasBlackjack(cards);
					if (dealerHasBlackjack)
					{
						if (flag)
						{
							blackjackRoundResult = BlackjackRoundResult.Standoff;
						}
					}
					else if (optimalCardsValue > dealerCardsVal)
					{
						blackjackRoundResult = (flag ? BlackjackRoundResult.BlackjackWin : BlackjackRoundResult.Win);
					}
					else if (optimalCardsValue == dealerCardsVal)
					{
						blackjackRoundResult = ((!flag) ? BlackjackRoundResult.Standoff : BlackjackRoundResult.BlackjackWin);
					}
					if (blackjackRoundResult == BlackjackRoundResult.BlackjackWin)
					{
						int winnings = Mathf.FloorToInt((float)betAmount * 2.5f);
						PayOut(pData, winnings);
						AddRoundResult(pData, winnings, (int)blackjackRoundResult);
					}
					switch (blackjackRoundResult)
					{
					case BlackjackRoundResult.Win:
					{
						int winnings2 = Mathf.FloorToInt((float)betAmount * 2f);
						PayOut(pData, winnings2);
						AddRoundResult(pData, winnings2, (int)blackjackRoundResult);
						break;
					}
					case BlackjackRoundResult.Standoff:
						PayOut(pData, betAmount);
						AddRoundResult(pData, betAmount, (int)blackjackRoundResult);
						break;
					default:
						AddRoundResult(pData, 0, (int)blackjackRoundResult);
						break;
					}
				}
			}
		}
		base.Owner.ClientRPC(null, "OnResultsDeclared", base.resultInfo);
	}

	private int PayOut(CardPlayerData pData, int winnings)
	{
		StorageContainer storage = pData.GetStorage();
		if (storage == null)
		{
			return 0;
		}
		storage.inventory.AddItem(base.Owner.scrapItemDef, winnings, 0uL, ItemContainer.LimitStack.None);
		return winnings;
	}

	protected override void HandlePlayerLeavingDuringTheirTurn(CardPlayerData playerData, CardPlayerData activePlayer)
	{
		ReceivedInputFromPlayer(activePlayer, 128, countAsAction: true, 0, playerInitiated: false);
	}

	protected override void SubReceivedInputFromPlayer(CardPlayerData playerData, int input, int value, bool countAsAction)
	{
		if (!Enum.IsDefined(typeof(BlackjackInputOption), input))
		{
			return;
		}
		BlackjackInputOption selectedMove = (BlackjackInputOption)input;
		if (!base.HasRoundInProgress)
		{
			LastActionTarget = playerData.UserID;
			LastAction = selectedMove;
			LastActionValue = 0;
			return;
		}
		int selectedMoveValue = 0;
		if (AllBetsPlaced)
		{
			DoInRoundPlayerInput(playerData, ref selectedMove, ref selectedMoveValue);
		}
		else
		{
			DoBettingPhasePlayerInput(playerData, value, countAsAction, ref selectedMove, ref selectedMoveValue);
		}
		LastActionTarget = playerData.UserID;
		LastAction = selectedMove;
		LastActionValue = selectedMoveValue;
		if (ShouldEndCycle())
		{
			EndCycle();
			return;
		}
		StartTurnTimer(MaxTurnTime);
		base.Owner.SendNetworkUpdate();
	}

	private void DoInRoundPlayerInput(CardPlayerData pData, ref BlackjackInputOption selectedMove, ref int selectedMoveValue)
	{
		if (((uint)pData.availableInputs & (uint)selectedMove) != (uint)selectedMove)
		{
			return;
		}
		switch (selectedMove)
		{
		case BlackjackInputOption.Hit:
		{
			cardStack.TryTakeCard(out var card3);
			pData.Cards.Add(card3);
			break;
		}
		case BlackjackInputOption.Stand:
			pData.SetHasCompletedTurn(hasActed: true);
			break;
		case BlackjackInputOption.Split:
		{
			PlayingCard item = pData.Cards[1];
			pData.PocketCards.Add(item);
			pData.Cards.Remove(item);
			cardStack.TryTakeCard(out var card2);
			pData.Cards.Add(card2);
			cardStack.TryTakeCard(out card2);
			pData.PocketCards.Add(card2);
			selectedMoveValue = TryMakeBet(pData, pData.betThisRound, BetType.Split);
			break;
		}
		case BlackjackInputOption.DoubleDown:
		{
			selectedMoveValue = TryMakeBet(pData, pData.betThisRound, BetType.Main);
			cardStack.TryTakeCard(out var card);
			pData.Cards.Add(card);
			break;
		}
		case BlackjackInputOption.Insurance:
		{
			int betAmount = Mathf.FloorToInt((float)pData.betThisRound / 2f);
			selectedMoveValue = TryMakeBet(pData, betAmount, BetType.Insurance);
			break;
		}
		case BlackjackInputOption.Abandon:
			pData.LeaveCurrentRound(clearBets: false, leftRoundEarly: true);
			break;
		}
		if (NumPlayersInCurrentRound() == 0)
		{
			EndRound();
		}
		else if (HasBusted(pData.Cards))
		{
			if (HasSplitCards(pData))
			{
				pData.MovePocketCardsToMain();
			}
			else
			{
				pData.SetHasCompletedTurn(hasActed: true);
			}
		}
	}

	private void DoBettingPhasePlayerInput(CardPlayerData pData, int value, bool countAsAction, ref BlackjackInputOption selectedMove, ref int selectedMoveValue)
	{
		if (((uint)pData.availableInputs & (uint)selectedMove) != (uint)selectedMove)
		{
			return;
		}
		if (selectedMove == BlackjackInputOption.SubmitBet)
		{
			selectedMoveValue = TryMakeBet(pData, value, BetType.Main);
			if (countAsAction)
			{
				pData.SetHasCompletedTurn(hasActed: true);
			}
		}
		else if (selectedMove == BlackjackInputOption.AllIn)
		{
			selectedMoveValue = TryMakeBet(pData, 999999, BetType.Main);
			if (countAsAction)
			{
				pData.SetHasCompletedTurn(hasActed: true);
			}
		}
	}

	private int TryMakeBet(CardPlayerData pData, int betAmount, BetType betType)
	{
		StorageContainer storage = pData.GetStorage();
		List<Item> obj = Pool.GetList<Item>();
		int num = storage.inventory.Take(obj, base.ScrapItemID, betAmount);
		switch (betType)
		{
		case BetType.Main:
			pData.betThisTurn += num;
			pData.betThisRound += num;
			break;
		case BetType.Split:
			pData.sideBetAThisRound += num;
			break;
		case BetType.Insurance:
			pData.sideBetBThisRound += num;
			break;
		}
		foreach (Item item in obj)
		{
			item.Remove();
		}
		Pool.FreeList(ref obj);
		return num;
	}

	protected override void SubStartRound()
	{
		dealerCards.Clear();
		cardStack = new StackOfCards(6);
		ClearLastAction();
		ServerPlaySound(CardGameSounds.SoundType.Shuffle);
		foreach (CardPlayerData item in PlayersInRound())
		{
			item.EnableSendingCards();
			item.availableInputs = GetAvailableInputsForPlayer(item);
		}
		StartTurnTimer(MaxTurnTime);
	}

	protected override void TimeoutTurn()
	{
		if (AllBetsPlaced)
		{
			foreach (CardPlayerData item in PlayersInRound())
			{
				if (!item.hasCompletedTurn)
				{
					ReceivedInputFromPlayer(item, 128, countAsAction: true, 0, playerInitiated: false);
				}
			}
			return;
		}
		foreach (CardPlayerData item2 in PlayersInRound())
		{
			if (item2.betThisRound == 0)
			{
				item2.LeaveCurrentRound(clearBets: true, leftRoundEarly: true);
			}
		}
		if (NumPlayersInCurrentRound() < MinPlayers)
		{
			EndRound();
		}
	}

	protected override bool ShouldEndCycle()
	{
		foreach (CardPlayerData item in PlayersInRound())
		{
			if (!item.hasCompletedTurn)
			{
				return false;
			}
		}
		return true;
	}

	protected override void EndCycle()
	{
		CardPlayerData[] playerData = base.PlayerData;
		for (int i = 0; i < playerData.Length; i++)
		{
			playerData[i].SetHasCompletedTurn(hasActed: false);
		}
		if (dealerCards.Count == 0)
		{
			DealInitialCards();
			ServerPlaySound(CardGameSounds.SoundType.Draw);
			QueueNextCycleInvoke();
			return;
		}
		bool flag = true;
		bool flag2 = true;
		foreach (CardPlayerData item in PlayersInRound())
		{
			if (!HasBusted(item.Cards))
			{
				flag = false;
			}
			if (!HasBlackjack(item.Cards))
			{
				flag2 = false;
			}
			if (item.PocketCards.Count > 0)
			{
				if (!HasBusted(item.PocketCards))
				{
					flag = false;
				}
				if (!HasBlackjack(item.PocketCards))
				{
					flag2 = false;
				}
			}
			if (!flag && !flag2)
			{
				break;
			}
		}
		if (NumPlayersInCurrentRound() > 0 && (!flag || !flag2))
		{
			for (int cardsValue = GetCardsValue(dealerCards, CardsValueMode.High); cardsValue < 17; cardsValue = GetCardsValue(dealerCards, CardsValueMode.High))
			{
				cardStack.TryTakeCard(out var card);
				dealerCards.Add(card);
			}
		}
		ServerPlaySound(CardGameSounds.SoundType.Draw);
		EndRound();
	}

	private bool HasSplitCards(CardPlayerData playerData)
	{
		return playerData.PocketCards.Count > 0;
	}

	private void DealInitialCards()
	{
		if (!base.HasRoundInProgress)
		{
			return;
		}
		PlayingCard card;
		foreach (CardPlayerData item in PlayersInRound())
		{
			cardStack.TryTakeCard(out card);
			item.Cards.Add(card);
		}
		cardStack.TryTakeCard(out card);
		dealerCards.Add(card);
		foreach (CardPlayerData item2 in PlayersInRound())
		{
			cardStack.TryTakeCard(out card);
			item2.Cards.Add(card);
			if (HasBlackjack(item2.Cards))
			{
				item2.SetHasCompletedTurn(hasActed: true);
			}
		}
		cardStack.TryTakeCard(out card);
		dealerCards.Add(card);
	}

	private void ClearLastAction()
	{
		LastAction = BlackjackInputOption.None;
		LastActionTarget = 0uL;
		LastActionValue = 0;
	}
}
