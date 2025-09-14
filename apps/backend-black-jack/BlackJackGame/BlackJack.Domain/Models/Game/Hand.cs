using BlackJack.Domain.Common;
using BlackJack.Domain.Models.Cards;
using BlackJack.Domain.Enums;

namespace BlackJack.Domain.Models.Game;

public class Hand : BaseEntity
{
    public List<Card> Cards { get; private set; } = new();
    public HandStatus Status { get; private set; }
    public int Value { get; private set; }
    public bool IsSoft { get; private set; }

    private Hand() { } // EF Constructor

    public static Hand Empty()
    {
        return new Hand
        {
            Status = HandStatus.Playing
        };
    }

    public void AddCard(Card card)
    {
        Cards.Add(card);
        CalculateValue();
        UpdateTimestamp();
    }

    private void CalculateValue()
    {
        Value = 0;
        int aces = 0;

        foreach (var card in Cards)
        {
            if (card.IsAce)
            {
                aces++;
                Value += 11;
            }
            else
            {
                Value += card.GetValue();
            }
        }

        // Ajustar aces
        while (Value > 21 && aces > 0)
        {
            Value -= 10;
            aces--;
        }

        IsSoft = aces > 0;

        if (Value > 21)
        {
            Status = HandStatus.Bust;
        }
        else if (Value == 21 && Cards.Count == 2)
        {
            Status = HandStatus.Blackjack;
        }
    }
}