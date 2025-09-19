using BlackJack.Domain.Enums;

namespace BlackJack.Domain.Models.Cards;

public class Card
{
    protected Card()
    {
        Suit = CardSuit.Hearts;
        Rank = CardRank.Two;
    }

    public Card(CardSuit suit, CardRank rank)
    {
        Suit = suit;
        Rank = rank;
    }

    public CardSuit Suit { get; private set; }
    public CardRank Rank { get; private set; }

    public int GetValue()
    {
        return Rank switch
        {
            CardRank.Ace => 11, // Se maneja como 11 por defecto, la lógica de 1/11 está en Hand
            CardRank.Jack or CardRank.Queen or CardRank.King => 10,
            _ => (int)Rank
        };
    }

    public string GetDisplayName()
    {
        var rankName = Rank switch
        {
            CardRank.Ace => "A",
            CardRank.Jack => "J",
            CardRank.Queen => "Q",
            CardRank.King => "K",
            _ => ((int)Rank).ToString()
        };

        var suitSymbol = Suit switch
        {
            CardSuit.Hearts => "♥",
            CardSuit.Diamonds => "♦",
            CardSuit.Clubs => "♣",
            CardSuit.Spades => "♠",
            _ => Suit.ToString()
        };

        return $"{rankName}{suitSymbol}";
    }

    public override string ToString()
    {
        return GetDisplayName();
    }

    public override bool Equals(object? obj)
    {
        if (obj is Card other)
        {
            return Suit == other.Suit && Rank == other.Rank;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Suit, Rank);
    }

    public static Card Create(CardSuit suit, CardRank rank)
    {
        return new Card(suit, rank);
    }
}