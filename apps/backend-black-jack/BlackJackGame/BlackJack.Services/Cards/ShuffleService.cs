using BlackJack.Domain.Models.Cards;

namespace BlackJack.Services.Cards;

public class ShuffleService : IShuffleService
{
    public Deck ShuffleDeck(Deck deck)
    {
        // For now, just return a new shuffled deck
        return Deck.CreateShuffled();
    }

    public List<Card> ShuffleCards(List<Card> cards)
    {
        return cards.OrderBy(x => Random.Shared.Next()).ToList();
    }
}