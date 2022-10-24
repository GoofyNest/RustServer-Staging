using System;
using System.Collections.Generic;
using ProtoBuf;
using UnityEngine;

namespace Facepunch.CardGames;

public class CardPlayerData : IDisposable
{
	public enum CardPlayerState
	{
		None,
		WantsToPlay,
		InGame,
		InCurrentRound
	}

	public List<PlayingCard> Cards;

	public List<PlayingCard> PocketCards;

	public readonly int mountIndex;

	private readonly bool isServer;

	public int availableInputs;

	public int betThisRound;

	public int betThisTurn;

	public int sideBetAThisRound;

	public int sideBetBThisRound;

	public int finalScore;

	public float lastActionTime;

	public int remainingToPayOut;

	private Func<int, StorageContainer> getStorage;

	private readonly int scrapItemID;

	public ulong UserID { get; private set; }

	public CardPlayerState State { get; private set; }

	public bool HasUser => State >= CardPlayerState.WantsToPlay;

	public bool HasUserInGame => State >= CardPlayerState.InGame;

	public bool HasUserInCurrentRound => State == CardPlayerState.InCurrentRound;

	public bool HasAvailableInputs => availableInputs > 0;

	private bool IsClient => !isServer;

	public bool LeftRoundEarly { get; private set; }

	public bool SendCardDetails { get; private set; }

	public bool hasCompletedTurn { get; private set; }

	public CardPlayerData(int mountIndex, bool isServer)
	{
		this.isServer = isServer;
		this.mountIndex = mountIndex;
		Cards = Pool.GetList<PlayingCard>();
		PocketCards = Pool.GetList<PlayingCard>();
	}

	public CardPlayerData(int scrapItemID, Func<int, StorageContainer> getStorage, int mountIndex, bool isServer)
		: this(mountIndex, isServer)
	{
		this.scrapItemID = scrapItemID;
		this.getStorage = getStorage;
	}

	public void Dispose()
	{
		Pool.FreeList(ref Cards);
		Pool.FreeList(ref PocketCards);
	}

	public int GetScrapAmount()
	{
		if (isServer)
		{
			StorageContainer storage = GetStorage();
			if (storage != null)
			{
				return storage.inventory.GetAmount(scrapItemID, onlyUsableAmounts: true);
			}
			Debug.LogError(GetType().Name + ": Couldn't get player storage.");
		}
		return 0;
	}

	public void SetHasCompletedTurn(bool hasActed)
	{
		hasCompletedTurn = hasActed;
		if (!hasActed)
		{
			betThisTurn = 0;
		}
	}

	public bool HasBeenIdleFor(int seconds)
	{
		if (HasUserInGame)
		{
			return Time.unscaledTime > lastActionTime + (float)seconds;
		}
		return false;
	}

	public StorageContainer GetStorage()
	{
		return getStorage(mountIndex);
	}

	public void AddUser(ulong userID)
	{
		ClearAllData();
		UserID = userID;
		State = CardPlayerState.WantsToPlay;
		lastActionTime = Time.unscaledTime;
	}

	public void ClearAllData()
	{
		UserID = 0uL;
		availableInputs = 0;
		State = CardPlayerState.None;
		ClearPerRoundData();
	}

	public void JoinRound()
	{
		if (HasUser)
		{
			State = CardPlayerState.InCurrentRound;
			ClearPerRoundData();
		}
	}

	private void ClearPerRoundData()
	{
		Cards.Clear();
		PocketCards.Clear();
		betThisRound = 0;
		betThisTurn = 0;
		sideBetAThisRound = 0;
		sideBetBThisRound = 0;
		finalScore = 0;
		LeftRoundEarly = false;
		hasCompletedTurn = false;
		SendCardDetails = false;
	}

	public void LeaveCurrentRound(bool clearBets, bool leftRoundEarly)
	{
		if (HasUserInCurrentRound)
		{
			availableInputs = 0;
			finalScore = 0;
			hasCompletedTurn = false;
			if (clearBets)
			{
				betThisRound = 0;
				betThisTurn = 0;
				sideBetAThisRound = 0;
				sideBetBThisRound = 0;
			}
			State = CardPlayerState.InGame;
			LeftRoundEarly = leftRoundEarly;
		}
	}

	public void LeaveGame()
	{
		if (HasUserInGame)
		{
			Cards.Clear();
			PocketCards.Clear();
			availableInputs = 0;
			finalScore = 0;
			SendCardDetails = false;
			LeftRoundEarly = false;
			State = CardPlayerState.WantsToPlay;
		}
	}

	public void EnableSendingCards()
	{
		SendCardDetails = true;
	}

	public string HandToString()
	{
		return HandToString(Cards);
	}

	public void MovePocketCardsToMain()
	{
		Cards.Clear();
		Cards.AddRange(PocketCards);
		PocketCards.Clear();
	}

	public static string HandToString(List<PlayingCard> cards)
	{
		string text = string.Empty;
		foreach (PlayingCard card in cards)
		{
			text = text + "23456789TJQKA"[(int)card.Rank] + "♠♥♦♣"[(int)card.Suit] + " ";
		}
		return text;
	}

	public void Save(List<CardGame.CardPlayer> playersMsg)
	{
		CardGame.CardPlayer cardPlayer = Pool.Get<CardGame.CardPlayer>();
		cardPlayer.userid = UserID;
		cardPlayer.cards = Pool.GetList<int>();
		foreach (PlayingCard card in Cards)
		{
			cardPlayer.cards.Add(SendCardDetails ? card.GetIndex() : (-1));
		}
		cardPlayer.pocketCards = Pool.GetList<int>();
		foreach (PlayingCard pocketCard in PocketCards)
		{
			cardPlayer.pocketCards.Add(SendCardDetails ? pocketCard.GetIndex() : (-1));
		}
		cardPlayer.scrap = GetScrapAmount();
		cardPlayer.state = (int)State;
		cardPlayer.availableInputs = availableInputs;
		cardPlayer.betThisRound = betThisRound;
		cardPlayer.betThisTurn = betThisTurn;
		cardPlayer.sideBetAThisRound = sideBetAThisRound;
		cardPlayer.sideBetBThisRound = sideBetBThisRound;
		cardPlayer.leftRoundEarly = LeftRoundEarly;
		cardPlayer.sendCardDetails = SendCardDetails;
		playersMsg.Add(cardPlayer);
	}
}
