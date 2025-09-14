using BlackJack.Domain.Common;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Models.Game;

public class Spectator : BaseEntity
{
    public PlayerId PlayerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime JoinedAt { get; private set; }
    public bool IsActive { get; private set; }

    private Spectator() { } // EF Constructor

    public static Spectator Create(PlayerId playerId, string name)
    {
        return new Spectator
        {
            PlayerId = playerId,
            Name = name,
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public void Leave()
    {
        IsActive = false;
        UpdateTimestamp();
    }
}