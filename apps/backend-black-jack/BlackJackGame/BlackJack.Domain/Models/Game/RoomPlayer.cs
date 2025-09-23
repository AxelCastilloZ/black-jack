
using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Betting;

namespace BlackJack.Domain.Models.Game;

public class RoomPlayer
{
    public Guid Id { get; set; }
    public Guid GameRoomId { get; set; }

    // Value object para el dominio
    public PlayerId PlayerId { get; set; } = null!;

    // NUEVO: Foreign Key específica para Entity Framework (relaciona con Player.Id)
    public Guid PlayerEntityId { get; set; }

    public string Name { get; set; } = string.Empty;
    public int Position { get; set; } // Posición en lista de jugadores (para orden)

    // Posición de asiento en la mesa (0-5, null = no sentado)
    public int? SeatPosition { get; set; }
    public bool IsReady { get; set; }
    public bool HasPlayedTurn { get; set; }
    public bool IsViewer { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LastActionAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public GameRoom GameRoom { get; set; } = null!;

    // CORREGIDO: Navigation Property para Player (usando PlayerEntityId como FK)
    public Player Player { get; set; } = null!;

    // NUEVAS: Propiedades calculadas para auto-betting
    public Money CurrentBalance => Player?.Balance ?? new Money(0m);
    public bool CanAffordBet => Player?.Balance?.Amount >= (GameRoom?.MinBetPerRound?.Amount ?? 0);
    public Money? CurrentBetAmount => Player?.CurrentBet?.Amount;
    public bool HasActiveBet => Player?.HasActiveBet ?? false;

    // NUEVA: Calcular total apostado en esta sesión (básico por ahora)
    // Nota: Esto podría requerir un campo adicional TotalBetThisSession en Player o GameRoom
    public decimal TotalBetThisSession
    {
        get
        {
            // Por ahora retornamos la apuesta actual, pero esto podría expandirse
            // para rastrear el total apostado durante toda la sesión
            return CurrentBetAmount?.Amount ?? 0m;
        }
    }

    public RoomPlayer()
    {
        var now = DateTime.UtcNow;
        CreatedAt = now;
        UpdatedAt = now;
        JoinedAt = now;
    }

    public RoomPlayer(PlayerId playerId, string name, int position) : this()
    {
        PlayerId = playerId;
        PlayerEntityId = playerId.Value; // NUEVO: Sincronizar con value object
        Name = name;
        Position = position;
        SeatPosition = null; // Por defecto, no sentado
        IsReady = false;
        HasPlayedTurn = false;
        IsViewer = false; // Por defecto, no es viewer
    }

    public RoomPlayer(PlayerId playerId, string name, int position, bool isViewer) : this()
    {
        PlayerId = playerId;
        PlayerEntityId = playerId.Value; // NUEVO: Sincronizar con value object
        Name = name;
        Position = position;
        SeatPosition = null; // Por defecto, no sentado
        IsReady = false;
        HasPlayedTurn = false;
        IsViewer = isViewer;
    }

    // NUEVO: Constructor que acepta directamente Player entity
    public RoomPlayer(Player player, string name, int position, bool isViewer = false) : this()
    {
        PlayerId = player.PlayerId;
        PlayerEntityId = player.Id; // NUEVO: Usar Player.Id para FK
        Name = name;
        Position = position;
        SeatPosition = null;
        IsReady = false;
        HasPlayedTurn = false;
        IsViewer = isViewer;
    }

    // Método para verificar si el jugador está sentado
    public bool IsSeated => SeatPosition.HasValue;

    // Método para obtener posición de asiento de forma segura
    public int GetSeatPosition() => SeatPosition ?? -1;

    // NUEVAS: Validaciones para auto-betting
    public bool CanAffordAutoBet(Money minBetPerRound)
    {
        return Player?.CanAffordBet(minBetPerRound) ?? false;
    }

    public int EstimatedAffordableRounds(Money minBetPerRound)
    {
        if (minBetPerRound?.Amount <= 0 || CurrentBalance.Amount <= 0)
            return 0;

        return (int)(CurrentBalance.Amount / minBetPerRound.Amount);
    }

    // Método para unirse a un asiento
    public void JoinSeat(int seatPosition)
    {
        if (seatPosition < 0 || seatPosition > 5)
            throw new ArgumentException("La posición del asiento debe estar entre 0 y 5", nameof(seatPosition));
        SeatPosition = seatPosition;
        UpdatedAt = DateTime.UtcNow;
    }

    // Método para salir del asiento
    public void LeaveSeat()
    {
        SeatPosition = null;
        UpdatedAt = DateTime.UtcNow;
    }

    // Método para actualizar posición en lista de jugadores (requerido por GameRoom)
    public void UpdatePosition(int newPosition)
    {
        Position = newPosition;
        UpdatedAt = DateTime.UtcNow;
    }

    // NUEVO: Método para sincronizar PlayerEntityId con PlayerId (útil para migrations)
    public void SyncPlayerEntityId()
    {
        PlayerEntityId = PlayerId.Value;
        UpdatedAt = DateTime.UtcNow;
    }

    // Método para resetear jugador para nuevo juego (requerido por GameRoom)
    public void ResetForNewGame()
    {
        IsReady = false;
        HasPlayedTurn = false;
        LastActionAt = null;
        // Mantener SeatPosition - el jugador sigue sentado pero resetea su estado de juego
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsReady()
    {
        IsReady = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsNotReady()
    {
        IsReady = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkTurnPlayed()
    {
        HasPlayedTurn = true;
        LastActionAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ResetTurn()
    {
        HasPlayedTurn = false;
        LastActionAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}