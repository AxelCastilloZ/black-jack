// BlackJack.Data/Repositories/Game/IGameRoomRepository.cs - INTERFAZ COMPLETA
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Game;

public interface IGameRoomRepository : IRepository<GameRoom>
{
    // Métodos existentes para GameRoom
    Task<GameRoom?> GetByRoomCodeAsync(string roomCode);
    Task<GameRoom?> GetRoomWithPlayersAsync(string roomCode);
    Task<GameRoom?> GetRoomWithPlayersAsync(Guid roomId);
    Task<GameRoom?> GetRoomWithPlayersReadOnlyAsync(string roomCode);
    Task<List<GameRoom>> GetActiveRoomsAsync();
    Task<List<GameRoom>> GetActiveRoomsReadOnlyAsync();
    Task<List<GameRoom>> GetRoomsByStatusAsync(RoomStatus status);
    Task<bool> RoomCodeExistsAsync(string roomCode);
    Task<GameRoom?> GetPlayerCurrentRoomAsync(PlayerId playerId);
    Task<bool> IsPlayerInRoomAsync(PlayerId playerId, string roomCode);
    Task<GameRoom?> GetRoomByTableIdAsync(Guid tableId);
    Task<GameRoom?> RefreshRoomAsync(GameRoom room);
    Task FlushChangesAsync();

    // NUEVOS: Métodos para manejo de RoomPlayer y SeatPosition

    /// <summary>
    /// Obtiene un RoomPlayer específico por sala y jugador
    /// </summary>
    Task<RoomPlayer?> GetRoomPlayerAsync(string roomCode, PlayerId playerId);

    /// <summary>
    /// Actualiza un RoomPlayer específico
    /// </summary>
    Task UpdateRoomPlayerAsync(RoomPlayer roomPlayer);

    /// <summary>
    /// Verifica si una posición de asiento está ocupada en una sala
    /// </summary>
    Task<bool> IsSeatOccupiedAsync(string roomCode, int seatPosition);

    /// <summary>
    /// Obtiene el RoomPlayer que ocupa una posición específica
    /// </summary>
    Task<RoomPlayer?> GetPlayerInSeatAsync(string roomCode, int seatPosition);

    /// <summary>
    /// Obtiene todas las posiciones de asientos ocupadas en una sala
    /// Retorna diccionario PlayerId -> SeatPosition
    /// </summary>
    Task<Dictionary<Guid, int>> GetSeatPositionsAsync(string roomCode);

    /// <summary>
    /// Libera un asiento (setea SeatPosition a null)
    /// </summary>
    Task<bool> FreeSeatAsync(string roomCode, PlayerId playerId);

    /// <summary>
    /// Verifica si un jugador está sentado en algún asiento de la sala
    /// </summary>
    Task<bool> IsPlayerSeatedAsync(string roomCode, PlayerId playerId);
}