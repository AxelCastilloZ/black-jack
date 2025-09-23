
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Game;

public interface IRoomPlayerRepository : IRepository<RoomPlayer>
{
    // Métodos existentes
    Task<RoomPlayer?> GetByPlayerIdAsync(PlayerId playerId);
    Task<List<RoomPlayer>> GetPlayersByRoomAsync(Guid roomId);
    Task<RoomPlayer?> GetPlayerInRoomAsync(Guid roomId, PlayerId playerId);
    Task<bool> IsPlayerInAnyRoomAsync(PlayerId playerId);

   
    Task<List<RoomPlayer>> GetSeatedPlayersByRoomCodeAsync(string roomCode);

  
    Task<List<RoomPlayer>> GetSeatedPlayersByRoomAsync(Guid roomId);

    /// <summary>
    /// Verifica si un jugador está sentado en alguna posición de la sala
    /// </summary>
    Task<bool> IsPlayerSeatedInRoomAsync(string roomCode, PlayerId playerId);

    /// <summary>
    /// Obtiene la posición de asiento de un jugador en una sala específica
    /// </summary>
    Task<int?> GetPlayerSeatPositionAsync(string roomCode, PlayerId playerId);

    /// <summary>
    /// Cuenta el número de jugadores sentados en una sala
    /// </summary>
    Task<int> GetSeatedPlayersCountAsync(string roomCode);

    /// <summary>
    /// Obtiene los PlayerId de todos los jugadores sentados en una sala
    /// </summary>
    Task<List<PlayerId>> GetSeatedPlayerIdsAsync(string roomCode);

    /// <summary>
    /// Verifica si hay al menos un jugador sentado en la sala
    /// </summary>
    Task<bool> HasSeatedPlayersAsync(string roomCode);
}