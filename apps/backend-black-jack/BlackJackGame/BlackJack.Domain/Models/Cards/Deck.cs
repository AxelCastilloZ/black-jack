using BlackJack.Domain.Models.Cards;
using BlackJack.Domain.Enums;

namespace BlackJack.Domain.Models.Cards;

public class Deck
{
    private readonly List<Card> _cards;
    private readonly Random _random;

    public Deck()
    {
        _cards = new List<Card>();
        _random = new Random();
        InitializeDeck();
        Shuffle();
    }

    public int Count => _cards.Count;
    public bool IsEmpty => _cards.Count == 0;

    private void InitializeDeck()
    {
        _cards.Clear();

        // CORREGIDO: Usar los enums correctos
        foreach (CardSuit suit in Enum.GetValues<CardSuit>())
        {
            foreach (CardRank rank in Enum.GetValues<CardRank>())
            {
                _cards.Add(new Card(suit, rank));
            }
        }
    }

    public void Shuffle()
    {
        for (int i = _cards.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
    }

    // AGREGADO: Método CreateShuffled estático
    public static Deck CreateShuffled()
    {
        var deck = new Deck();
        deck.Shuffle();
        return deck;
    }

    // AGREGADO: Método ShouldShuffle
    public bool ShouldShuffle()
    {
        // Normalmente se baraja cuando quedan menos del 25% de cartas
        return _cards.Count < 13; // 52 * 0.25 = 13
    }

    // AGREGADO: Método DealCard (alias de DrawCard)
    public Card DealCard()
    {
        return DrawCard();
    }

    public Card DrawCard()
    {
        if (IsEmpty)
            throw new InvalidOperationException("No se pueden sacar cartas de un mazo vacío");

        var card = _cards[0];
        _cards.RemoveAt(0);
        return card;
    }

    public List<Card> DrawCards(int count)
    {
        if (count > _cards.Count)
            throw new InvalidOperationException($"No hay suficientes cartas. Solicitadas: {count}, Disponibles: {_cards.Count}");

        var drawnCards = new List<Card>();
        for (int i = 0; i < count; i++)
        {
            drawnCards.Add(DrawCard());
        }
        return drawnCards;
    }

    public void Reset()
    {
        InitializeDeck();
        Shuffle();
    }

    public static Deck Create()
    {
        return new Deck();
    }
}