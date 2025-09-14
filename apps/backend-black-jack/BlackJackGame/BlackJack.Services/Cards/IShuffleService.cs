using BlackJack.Domain.Models.Cards;

namespace BlackJack.Services.Cards;

public interface IShuffleService
{
    Deck ShuffleDeck(Deck deck);
    List<Card> ShuffleCards(List<Card> cards);
}