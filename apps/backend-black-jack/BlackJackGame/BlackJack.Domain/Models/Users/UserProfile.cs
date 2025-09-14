using BlackJack.Domain.Common;
using BlackJack.Domain.Models.Betting;

namespace BlackJack.Domain.Models.Users;

public class UserProfile : BaseEntity
{
    public PlayerId PlayerId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public Money Balance { get; private set; }
    public int TotalGamesPlayed { get; private set; }
    public int GamesWon { get; private set; }
    public int GamesLost { get; private set; }
    public Money TotalWinnings { get; private set; }
    public DateTime LastLoginAt { get; private set; }
    public bool IsActive { get; private set; }

    private UserProfile() { } // EF Constructor

    public static UserProfile Create(PlayerId playerId, string displayName, string email)
    {
        return new UserProfile
        {
            PlayerId = playerId,
            DisplayName = displayName,
            Email = email,
            Balance = new Money(1000m), // Starting balance
            TotalWinnings = Money.Zero,
            LastLoginAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public void UpdateBalance(Money newBalance)
    {
        Balance = newBalance;
        UpdateTimestamp();
    }

    public void RecordGameResult(bool won, Money winnings)
    {
        TotalGamesPlayed++;
        if (won)
        {
            GamesWon++;
            TotalWinnings = TotalWinnings.Add(winnings);
        }
        else
        {
            GamesLost++;
        }
        UpdateTimestamp();
    }

    public decimal WinPercentage => TotalGamesPlayed > 0 ? (decimal)GamesWon / TotalGamesPlayed * 100 : 0;
}