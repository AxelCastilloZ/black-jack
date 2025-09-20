// BlackJack.Domain/Models/Game/RoomPlayer.cs - COMPLETO CON SeatPosition
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Models.Game;

public class RoomPlayer
{
    public Guid Id { get; set; }
    public Guid GameRoomId { get; set; }
    public PlayerId PlayerId { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int Position { get; set; } // Posición en lista de jugadores (para orden)

    // NUEVO: Posición de asiento en la mesa (0-5, null = no sentado)
    public int? SeatPosition { get; set; }

    public bool IsReady { get; set; }
    public bool HasPlayedTurn { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LastActionAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public GameRoom GameRoom { get; set; } = null!;

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
        Name = name;
        Position = position;
        SeatPosition = null; // Por defecto, no sentado
        IsReady = false;
        HasPlayedTurn = false;
    }

    // NUEVO: Método para verificar si el jugador está sentado
    public bool IsSeated => SeatPosition.HasValue;

    // NUEVO: Método para obtener posición de asiento de forma segura
    public int GetSeatPosition() => SeatPosition ?? -1;

    // NUEVO: Método para unirse a un asiento
    public void JoinSeat(int seatPosition)
    {
        if (seatPosition < 0 || seatPosition > 5)
            throw new ArgumentException("La posición del asiento debe estar entre 0 y 5", nameof(seatPosition));

        SeatPosition = seatPosition;
        UpdatedAt = DateTime.UtcNow;
    }

    // NUEVO: Método para salir del asiento
    public void LeaveSeat()
    {
        SeatPosition = null;
        UpdatedAt = DateTime.UtcNow;
    }

    // NUEVO: Método para actualizar posición en lista de jugadores (requerido por GameRoom)
    public void UpdatePosition(int newPosition)
    {
        Position = newPosition;
        UpdatedAt = DateTime.UtcNow;
    }

    // NUEVO: Método para resetear jugador para nuevo juego (requerido por GameRoom)
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