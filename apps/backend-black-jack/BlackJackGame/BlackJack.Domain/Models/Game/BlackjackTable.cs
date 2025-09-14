using BlackJack.Domain.Common;
using BlackJack.Domain.Enums;
using BlackJack.Domain.Models.Betting;
using BlackJack.Domain.Models.Cards;

namespace BlackJack.Domain.Models.Game;

public class BlackjackTable : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public Money MinBet { get; private set; } = new Money(10m);
    public Money MaxBet { get; private set; } = new Money(500m);
    public GameStatus Status { get; private set; }
    public List<Seat> Seats { get; private set; } = new();
    public List<Spectator> Spectators { get; private set; } = new();
    public Deck Deck { get; private set; } = Deck.CreateShuffled();
    public Hand DealerHand { get; private set; } = Hand.Empty();
    public int RoundNumber { get; private set; }

    private BlackjackTable() { } // EF Constructor

    public static BlackjackTable Create(string name)
    {
        var table = new BlackjackTable
        {
            Name = name,
            Status = GameStatus.WaitingForPlayers,
            Deck = Deck.CreateShuffled(),
            DealerHand = Hand.Empty()
        };

        // Crear los 6 asientos
        for (int i = 1; i <= 6; i++)
        {
            table.Seats.Add(Seat.Create(i));
        }

        return table;
    }

    public void StartNewRound()
    {
        RoundNumber++;
        Status = GameStatus.InProgress;
        DealerHand = Hand.Empty();

        // Reset player hands
        foreach (var seat in Seats.Where(s => s.IsOccupied))
        {
            seat.Player!.Hands.Clear();
            seat.Player.AddHand(Hand.Empty());
        }

        UpdateTimestamp();
    }
}