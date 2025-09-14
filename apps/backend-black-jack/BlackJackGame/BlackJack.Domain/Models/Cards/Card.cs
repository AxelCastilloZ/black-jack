using BlackJack.Domain.Enums;

namespace BlackJack.Domain.Models.Cards;

public record Card(Suit Suit, Rank Rank)
{
    public int GetValue()
    {
        return Rank switch
        {
            Rank.Ace => 11,  // Se manejará como 1 o 11 en la lógica del juego
            Rank.Jack or Rank.Queen or Rank.King => 10,
            _ => (int)Rank
        };
    }

    public bool IsAce => Rank == Rank.Ace;

    public override string ToString()
    {
        return $"{Rank} of {Suit}";
    }
}