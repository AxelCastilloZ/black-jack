using BlackJack.Domain.Common;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Models.Users;

public class Spectator : BaseEntity
{
    // Constructor sin parámetros para EF Core
    protected Spectator() : base()
    {
        PlayerId = PlayerId.New();
        Name = string.Empty;
        JoinedAt = DateTime.UtcNow;
    }

    // Constructor principal
    public Spectator(PlayerId playerId, string name) : base()
    {
        PlayerId = playerId ?? throw new ArgumentNullException(nameof(playerId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        JoinedAt = DateTime.UtcNow;
    }

    // Constructor con ID específico (para casos especiales)
    public Spectator(PlayerId playerId, string name, Guid id) : base(id)
    {
        PlayerId = playerId ?? throw new ArgumentNullException(nameof(playerId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        JoinedAt = DateTime.UtcNow;
    }

    // Propiedades principales
    public PlayerId PlayerId { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public DateTime JoinedAt { get; private set; }

    // Propiedades calculadas
    public TimeSpan TimeWatching => DateTime.UtcNow - JoinedAt;
    public bool IsLongTimeSpectator => TimeWatching.TotalMinutes > 30;

    // Métodos de negocio
    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        Name = name;
        UpdateTimestamp();
    }

    public void RefreshJoinTime()
    {
        JoinedAt = DateTime.UtcNow;
        UpdateTimestamp();
    }

    // Factory methods
    public static Spectator Create(PlayerId playerId, string name)
    {
        if (playerId == null)
            throw new ArgumentNullException(nameof(playerId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        return new Spectator(playerId, name);
    }

    public static Spectator Create(string name)
    {
        return Create(PlayerId.New(), name);
    }

    // Métodos de información
    public string GetDisplayInfo()
    {
        var timeWatching = TimeWatching;
        var timeText = timeWatching.TotalMinutes < 1
            ? "just joined"
            : $"watching for {timeWatching.TotalMinutes:F0}m";

        return $"{Name} ({timeText})";
    }

    public override string ToString()
    {
        return GetDisplayInfo();
    }

    // Métodos de comparación
    public override bool Equals(object? obj)
    {
        if (obj is Spectator other)
        {
            return PlayerId == other.PlayerId;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return PlayerId.GetHashCode();
    }
}