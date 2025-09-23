using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Models.Game;

public class Spectator
{
    // EF Core constructor
    protected Spectator()
    {
        Id = Guid.NewGuid();
        Name = string.Empty;
        PlayerId = PlayerId.New();
        GameRoomId = Guid.Empty;
        JoinedAt = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    // Constructor principal
    private Spectator(PlayerId playerId, string name, Guid gameRoomId)
    {
        Id = Guid.NewGuid();
        PlayerId = playerId ?? throw new ArgumentNullException(nameof(playerId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        GameRoomId = gameRoomId;
        JoinedAt = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    // Propiedades principales
    public Guid Id { get; private set; }
    public PlayerId PlayerId { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;

    // LIMPIADO: Solo relación con GameRoom, TableId removido completamente
    public Guid GameRoomId { get; private set; }

    public DateTime JoinedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // LIMPIADO: Navigation property solo para GameRoom
    public GameRoom GameRoom { get; set; } = null!;

    // Factory method
    public static Spectator Create(PlayerId playerId, string name)
    {
        if (playerId == null)
            throw new ArgumentNullException(nameof(playerId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        // GameRoomId se establecerá cuando se agregue a una room específica
        return new Spectator(playerId, name, Guid.Empty);
    }

    // NUEVO: Factory method con GameRoomId específico
    public static Spectator Create(PlayerId playerId, string name, Guid gameRoomId)
    {
        if (playerId == null)
            throw new ArgumentNullException(nameof(playerId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        if (gameRoomId == Guid.Empty)
            throw new ArgumentException("GameRoomId cannot be empty", nameof(gameRoomId));

        return new Spectator(playerId, name, gameRoomId);
    }

    // Métodos de actualización
    public void SetGameRoom(Guid gameRoomId)
    {
        if (gameRoomId == Guid.Empty)
            throw new ArgumentException("GameRoomId cannot be empty", nameof(gameRoomId));

        GameRoomId = gameRoomId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        Name = name;
        UpdatedAt = DateTime.UtcNow;
    }

    // Método de utilidad para verificar si pertenece a una room específica
    public bool BelongsToRoom(Guid gameRoomId)
    {
        return GameRoomId == gameRoomId;
    }

    // Método para obtener información básica
    public string GetDisplayInfo()
    {
        return $"Spectator: {Name} (joined at {JoinedAt:HH:mm})";
    }

    // Método para comparación
    public bool IsSamePlayer(PlayerId playerId)
    {
        return PlayerId.Value == playerId.Value;
    }
}