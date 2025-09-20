// IConnectionManager.cs - INTERFAZ ACTUALIZADA con métodos de reconexión mejorados
using BlackJack.Domain.Models.Users;
using BlackJack.Realtime.Models;

namespace BlackJack.Realtime.Services;

public interface IConnectionManager
{
    // Gestión de conexiones
    Task AddConnectionAsync(string connectionId, PlayerId playerId, string userName);
    Task RemoveConnectionAsync(string connectionId);
    Task UpdateConnectionAsync(string connectionId, string? roomCode = null);

    // Consultas de conexiones
    Task<List<string>> GetConnectionsForPlayerAsync(PlayerId playerId);
    Task<List<string>> GetConnectionsInRoomAsync(string roomCode);
    Task<ConnectionInfo?> GetConnectionInfoAsync(string connectionId);
    Task<PlayerId?> GetPlayerIdByConnectionAsync(string connectionId);

    // Gestión de grupos/salas
    Task AddToGroupAsync(string connectionId, string groupName);
    Task RemoveFromGroupAsync(string connectionId, string groupName);
    Task<List<string>> GetGroupsForConnectionAsync(string connectionId);

    // Estado de jugadores
    Task<bool> IsPlayerOnlineAsync(PlayerId playerId);
    Task<int> GetOnlinePlayerCountAsync();
    Task<List<PlayerId>> GetOnlinePlayersAsync();
    Task<List<PlayerId>> GetPlayersInRoomAsync(string roomCode);

    // Reconexión - MEJORADO
    Task<ReconnectionInfo?> GetReconnectionInfoAsync(PlayerId playerId);
    Task SaveReconnectionInfoAsync(PlayerId playerId, string? roomCode);
    Task ClearReconnectionInfoAsync(PlayerId playerId);

    // NUEVO: Estado detallado de jugadores
    Task<PlayerRoomState?> GetPlayerRoomStateAsync(PlayerId playerId);

    // Limpieza y mantenimiento
    Task CleanupStaleConnectionsAsync();
    Task<int> GetTotalConnectionCountAsync();

    // NUEVO: Estadísticas del manager
    Task<ConnectionManagerStats> GetStatsAsync();
}