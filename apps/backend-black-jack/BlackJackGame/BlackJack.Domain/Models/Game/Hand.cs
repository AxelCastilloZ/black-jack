using BlackJack.Domain.Common;
using BlackJack.Domain.Models.Cards;
using BlackJack.Domain.Enums;

namespace BlackJack.Domain.Models.Game;

public class Hand : BaseEntity
{
    private readonly List<Card> _cards = new();

    // Propiedad privada para EF Core - maneja la serialización automáticamente
    private string CardsJson
    {
        get => System.Text.Json.JsonSerializer.Serialize(_cards);
        set
        {
            if (!string.IsNullOrEmpty(value))
            {
                try
                {
                    var cards = System.Text.Json.JsonSerializer.Deserialize<List<Card>>(value);
                    if (cards != null)
                    {
                        _cards.Clear();
                        _cards.AddRange(cards);
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // Si hay error en deserialización, mantener lista vacía
                    _cards.Clear();
                }
            }
        }
    }

    // EF Core constructor
    protected Hand() : base()
    {
        Status = HandStatus.Active;
    }

    // Constructor principal
    public Hand(Guid? id = null) : base(id ?? Guid.NewGuid())
    {
        Status = HandStatus.Active;
    }

    // Propiedades
    public HandStatus Status { get; private set; }
    public IReadOnlyList<Card> Cards => _cards.AsReadOnly();

    // Propiedades calculadas
    public int Value => CalculateValue();
    public bool IsSoft => HasAce() && Value <= 11;
    public bool IsBlackjack => Value == 21 && Cards.Count == 2;
    public bool IsBust => Value > 21;
    public bool IsComplete => Status != HandStatus.Active;

    // Factory method para mano vacía
    public static Hand Empty => new Hand();

    // Métodos principales
    public void AddCard(Card card)
    {
        if (card == null)
            throw new ArgumentNullException(nameof(card));

        if (IsComplete)
            throw new InvalidOperationException("Cannot add cards to a completed hand");

        _cards.Add(card);
        UpdateStatus();
        UpdateTimestamp();
    }

    public void Stand()
    {
        if (Status == HandStatus.Active)
        {
            Status = HandStatus.Stand;
            UpdateTimestamp();
        }
    }

    public void Surrender()
    {
        if (Status == HandStatus.Active && Cards.Count == 2)
        {
            Status = HandStatus.Surrender;
            UpdateTimestamp();
        }
    }

    public void Clear()
    {
        _cards.Clear();
        Status = HandStatus.Active;
        UpdateTimestamp();
    }

    // Métodos privados
    private int CalculateValue()
    {
        int value = 0;
        int aces = 0;

        foreach (var card in _cards)
        {
            if (card.Rank == CardRank.Ace)
            {
                aces++;
                value += 11;
            }
            else if (card.Rank >= CardRank.Jack)
            {
                value += 10;
            }
            else
            {
                value += (int)card.Rank;
            }
        }

        // Ajustar ases
        while (value > 21 && aces > 0)
        {
            value -= 10;
            aces--;
        }

        return value;
    }

    private bool HasAce()
    {
        return _cards.Any(c => c.Rank == CardRank.Ace);
    }

    private void UpdateStatus()
    {
        if (Value > 21)
        {
            Status = HandStatus.Bust;
        }
        else if (Value == 21 && Cards.Count == 2)
        {
            Status = HandStatus.Blackjack;
        }
        // No cambiar automáticamente a Stand, el jugador debe decidir
    }

    // Factory methods
    public static Hand Create(Guid? id = null)
    {
        return new Hand(id);
    }

    public static Hand CreateWithCards(IEnumerable<Card> cards, Guid? id = null)
    {
        var hand = new Hand(id);
        foreach (var card in cards)
        {
            hand.AddCard(card);
        }
        return hand;
    }
}