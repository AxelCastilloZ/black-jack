// BlackJack.Data/Repositories/Game/IGameRoomRepository.cs - COMPLETA CON TODOS LOS MÉTODOS
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using Microsoft.EntityFrameworkCore.Storage;

namespace BlackJack.Data.Repositories.Game;

public interface IGameRoomRepository
{
    // Métodos básicos CRUD
    Task<GameRoom?> GetByIdAsync(Guid id);
    Task<GameRoom?> GetByRoomCodeAsync(string roomCode);
    Task<List<GameRoom>> GetAllAsync();
    Task AddAsync(GameRoom gameRoom);
    Task UpdateAsync(GameRoom gameRoom);
    Task DeleteAsync(GameRoom entity);

    // ✅ MÉTODO CRÍTICO: Para obtener sala por TableId (requerido por GameService)
    Task<GameRoom?> GetByTableIdAsync(Guid tableId);

    // ✅ MÉTODOS FALTANTES: Usados por GameRoomService
    Task<GameRoom?> GetRoomByTableIdAsync(Guid tableId); // Alias de GetByTableIdAsync
    Task<GameRoom?> GetRoomWithPlayersAsync(string roomCode);
    Task<GameRoom?> GetRoomWithPlayersAsync(Guid roomId);
    Task<GameRoom?> GetRoomWithPlayersReadOnlyAsync(string roomCode);
    Task<List<GameRoom>> GetActiveRoomsAsync();
    Task<List<GameRoom>> GetActiveRoomsReadOnlyAsync();
    Task<GameRoom?> RefreshRoomAsync(GameRoom room);
    Task FlushChangesAsync();

    // Métodos de búsqueda y filtrado
    Task<List<GameRoom>> GetAvailableRoomsAsync();
    Task<List<GameRoom>> GetRoomsByHostAsync(PlayerId hostPlayerId);
    Task<List<GameRoom>> GetRoomsInProgressAsync();
    Task<List<GameRoom>> GetRoomsByStatusAsync(RoomStatus status);

    // Métodos de validación
    Task<bool> RoomCodeExistsAsync(string roomCode);
    Task<bool> IsPlayerInAnyRoomAsync(PlayerId playerId);
    Task<GameRoom?> GetPlayerCurrentRoomAsync(PlayerId playerId);

    // ✅ MÉTODOS FALTANTES: RoomPlayer operations
    Task<RoomPlayer?> GetRoomPlayerAsync(string roomCode, PlayerId playerId);
    Task UpdateRoomPlayerAsync(RoomPlayer roomPlayer);
    Task<bool> IsPlayerInRoomAsync(PlayerId playerId, string roomCode);
    Task<bool> IsSeatOccupiedAsync(string roomCode, int seatPosition);
    Task<RoomPlayer?> GetPlayerInSeatAsync(string roomCode, int seatPosition);
    Task<Dictionary<Guid, int>> GetSeatPositionsAsync(string roomCode);
    Task<bool> FreeSeatAsync(string roomCode, PlayerId playerId);
    Task<bool> IsPlayerSeatedAsync(string roomCode, PlayerId playerId);

    // ✅ MÉTODOS FALTANTES: Data cleanup operations
    Task<bool> RemoveRoomPlayerAsync(string roomCode, PlayerId playerId);
    Task<int> ForceCleanupPlayerFromAllRoomsAsync(PlayerId playerId);
    Task<List<string>> GetPlayerOrphanRoomsAsync(PlayerId playerId);
    Task<int> CleanupEmptyRoomsAsync();

    // Métodos de paginación y búsqueda
    Task<(List<GameRoom> rooms, int totalCount)> GetRoomsPagedAsync(
        int page,
        int pageSize,
        string? searchTerm = null,
        RoomStatus? status = null);

    // Métodos de estadísticas
    Task<int> GetTotalRoomsCountAsync();
    Task<int> GetActiveRoomsCountAsync();
    Task<Dictionary<RoomStatus, int>> GetRoomStatisticsAsync();

    // Métodos de transacciones
    Task<IDbContextTransaction> BeginTransactionAsync();
    Task SaveChangesAsync();

    // Métodos específicos para el juego
    Task<List<GameRoom>> GetRoomsWithPlayersAsync();
    Task<bool> HasActiveGameAsync(Guid tableId);
}