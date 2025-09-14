using BlackJack.Domain.Common;
using BlackJack.Domain.Models.Betting;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Models.Game;

public class Player : BaseEntity
{
    public PlayerId PlayerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Money Balance { get; private set; }
    public List<Hand> Hands { get; private set; } = new();
    public Bet? CurrentBet { get; private set; }
    public bool IsActive { get; private set; }

    private Player() { } // EF Constructor

    public static Player Create(PlayerId playerId, string name, Money initialBalance)
    {
        return new Player
        {
            PlayerId = playerId,
            Name = name,
            Balance = initialBalance,
            IsActive = true
        };
    }

    public void PlaceBet(Bet bet)
    {
        if (Balance.IsLessThan(bet.Amount))
            throw new InvalidOperationException("Insufficient funds");

        CurrentBet = bet;
        Balance = Balance.Subtract(bet.Amount);
        UpdateTimestamp();
    }

    public void AddHand(Hand hand)
    {
        Hands.Add(hand);
        UpdateTimestamp();
    }

    public void WinBet(Money winnings)
    {
        Balance = Balance.Add(winnings);
        UpdateTimestamp();
    }
}