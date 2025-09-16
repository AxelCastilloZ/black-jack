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

        // Crear los 6 asientos con posiciones 0-5 (coincide con el frontend)
        for (int i = 0; i < 6; i++)
        {
            table.Seats.Add(Seat.Create(i));
        }

        return table;
    }

    /// <summary>Fija los límites de apuesta de la mesa (valida min &lt;= max y &gt; 0).</summary>
    public void SetBetLimits(Money min, Money max)
    {
        if (min.Amount <= 0 || max.Amount <= 0)
            throw new ArgumentException("Bet limits must be greater than zero");
        if (min.Amount > max.Amount)
            throw new ArgumentException("MinBet cannot be greater than MaxBet");

        MinBet = min;
        MaxBet = max;
        UpdateTimestamp();
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

    /// <summary>Termina la ronda: cambia estado, actualiza timestamp y baraja si el mazo está bajo.</summary>
    public void EndRound()
    {
        Status = GameStatus.RoundEnded;
        UpdateTimestamp();

        // (opcional) barajar si quedan pocas cartas
        if (Deck.ShouldShuffle())
        {
            Deck = Deck.CreateShuffled();
        }
    }

    /// <summary>Vuelve la mesa al estado de espera de jugadores.</summary>
    public void SetWaitingForPlayers()
    {
        Status = GameStatus.WaitingForPlayers;
        UpdateTimestamp();
    }
}