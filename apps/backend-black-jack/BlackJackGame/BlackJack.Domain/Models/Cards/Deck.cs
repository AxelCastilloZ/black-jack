using BlackJack.Domain.Enums;

namespace BlackJack.Domain.Models.Cards;

public class Deck
{
    private Queue<Card> _cards = new();

    public int CardsRemaining => _cards.Count;
    public bool IsEmpty => !_cards.Any();

    private Deck(IEnumerable<Card> cards)
    {
        foreach (var card in cards)
        {
            _cards.Enqueue(card);
        }
    }

    public static Deck CreateShuffled()
    {
        var allCards = new List<Card>();

        foreach (Suit suit in Enum.GetValues<Suit>())
        {
            foreach (Rank rank in Enum.GetValues<Rank>())
            {
                allCards.Add(new Card(suit, rank));
            }
        }

        var shuffled = allCards.OrderBy(x => Random.Shared.Next()).ToList();
        return new Deck(shuffled);
    }

    public Card DealCard()
    {
        if (IsEmpty)
            throw new InvalidOperationException("Cannot deal from empty deck");

        return _cards.Dequeue();
    }

    public bool ShouldShuffle()
    {
        return CardsRemaining < 15; // Reshuffle when less than 15 cards
    }
}