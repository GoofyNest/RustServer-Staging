using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Facepunch.CardGames;

public class StackOfCards
{
	private readonly List<PlayingCard> cards;

	public StackOfCards(int numDecks)
	{
		cards = new List<PlayingCard>(52 * numDecks);
		for (int i = 0; i < numDecks; i++)
		{
			for (int j = 0; j < 4; j++)
			{
				for (int k = 0; k < 13; k++)
				{
					cards.Add(PlayingCard.GetCard(j, k));
				}
			}
		}
		ShuffleDeck();
	}

	public bool TryTakeCard(out PlayingCard card)
	{
		if (cards.Count == 0)
		{
			card = null;
			return false;
		}
		card = cards[cards.Count - 1];
		cards.RemoveAt(cards.Count - 1);
		return true;
	}

	public void AddCard(PlayingCard card)
	{
		cards.Insert(0, card);
	}

	public void ShuffleDeck()
	{
		int num = cards.Count;
		while (num > 1)
		{
			num--;
			int num2 = Random.Range(0, num);
			List<PlayingCard> list = cards;
			int index = num2;
			List<PlayingCard> list2 = cards;
			int index2 = num;
			PlayingCard playingCard = cards[num];
			PlayingCard playingCard2 = cards[num2];
			PlayingCard playingCard4 = (list[index] = playingCard);
			playingCard4 = (list2[index2] = playingCard2);
		}
	}

	public void Print()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("Cards in the deck: ");
		foreach (PlayingCard card in cards)
		{
			stringBuilder.AppendLine(card.Rank.ToString() + " of " + card.Suit);
		}
		Debug.Log(stringBuilder.ToString());
	}
}
