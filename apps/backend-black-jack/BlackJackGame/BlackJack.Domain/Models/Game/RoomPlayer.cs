using BlackJack.Domain.Common;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Models.Game;

public class RoomPlayer : BaseEntity
{
    // EF Core constructor
    protected RoomPlayer() : base()
    {
        PlayerId = PlayerId.New();
        Name = string.Empty;
        Position = 0;
        IsReady = false;
        HasPlayedTurn = false;
        JoinedAt = DateTime.UtcNow;
    }

    // Constructor principal
    public RoomPlayer(PlayerId playerId, string name, int position, Guid? id = null)
        : base(id ?? Guid.NewGuid())
    {
        PlayerId = playerId ?? throw new ArgumentNullException(nameof(playerId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Position = position;
        IsReady = false;
        HasPlayedTurn = false;
        JoinedAt = DateTime.UtcNow;
    }

    // Propiedades principales
    public PlayerId PlayerId { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public int Position { get; private set; }
    public bool IsReady { get; private set; }
    public bool HasPlayedTurn { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? LastActionAt { get; private set; }

    // Propiedades calculadas
    public bool IsActive => IsReady;
    public TimeSpan TimeInRoom => DateTime.UtcNow - JoinedAt;

    // Métodos de estado
    public void SetReady(bool isReady = true)
    {
        IsReady = isReady;
        UpdateTimestamp();
    }

    public void MarkTurnPlayed()
    {
        HasPlayedTurn = true;
        LastActionAt = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public void UpdatePosition(int newPosition)
    {
        if (newPosition < 0)
            throw new ArgumentException("Position cannot be negative", nameof(newPosition));

        Position = newPosition;
        UpdateTimestamp();
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        Name = name;
        UpdateTimestamp();
    }

    // Reset para nueva partida
    public void ResetForNewGame()
    {
        IsReady = false;
        HasPlayedTurn = false;
        LastActionAt = null;
        UpdateTimestamp();
    }

    // Factory method
    public static RoomPlayer Create(PlayerId playerId, string name, int position)
    {
        return new RoomPlayer(playerId, name, position);
    }

    // Métodos de información
    public string GetStatusInfo()
    {
        var status = IsReady ? "Ready" : "Not Ready";
        var turnStatus = HasPlayedTurn ? "Turn Played" : "Waiting";
        return $"{Name} (Pos: {Position}) - {status}, {turnStatus}";
    }

    public override string ToString()
    {
        return GetStatusInfo();
    }

    // Métodos de comparación
    public override bool Equals(object? obj)
    {
        if (obj is RoomPlayer other)
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