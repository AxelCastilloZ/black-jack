// ConnectionManager.cs - SIMPLIFICADO PARA NO INTERFERIR CON GRUPOS SIGNALR
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using BlackJack.Domain.Models.Users;
using BlackJack.Realtime.Models;

namespace BlackJack.Realtime.Services;

public class ConnectionManager : IConnectionManager
{
    private readonly ILogger<ConnectionManager> _logger;

    // Diccionario thread-safe para almacenar conexiones activas
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();

    // Mapeo de PlayerId a lista de ConnectionIds (un jugador puede tener múltiples conexiones)
    private readonly ConcurrentDictionary<Guid, List<string>> _playerConnections = new();

    // Mapeo de ConnectionId a PlayerId para búsquedas rápidas
    private readonly ConcurrentDictionary<string, Guid> _connectionToPlayer = new();

    // SIMPLIFICADO: Solo información de reconexión básica
    private readonly ConcurrentDictionary<Guid, ReconnectionInfo> _reconnectionInfo = new();

    public ConnectionManager(ILogger<ConnectionManager> logger)
    {
        _logger = logger;
    }

    #region Gestión de conexiones

    public Task AddConnectionAsync(string connectionId, PlayerId playerId, string userName)
    {
        try
        {
            _logger.LogInformation("[ConnectionManager] === ADDING CONNECTION ===");
            _logger.LogInformation("[ConnectionManager] ConnectionId: {ConnectionId}, PlayerId: {PlayerId}, UserName: {UserName}",
                connectionId, playerId, userName);

            // SIMPLIFICADO: Solo trackear conexión, NO grupos
            var connectionInfo = new ConnectionInfo(
                ConnectionId: connectionId,
                PlayerId: playerId.Value,
                UserName: userName,
                Groups: new List<string>(), // Inicializar vacío - NO manejar grupos aquí
                ConnectedAt: DateTime.UtcNow
            );

            _connections[connectionId] = connectionInfo;
            _connectionToPlayer[connectionId] = playerId.Value;

            // Agregar a la lista de conexiones del jugador con manejo thread-safe
            _playerConnections.AddOrUpdate(
                playerId.Value,
                new List<string> { connectionId },
                (key, existingList) =>
                {
                    lock (existingList)
                    {
                        if (!existingList.Contains(connectionId))
                        {
                            existingList.Add(connectionId);
                        }
                        return existingList;
                    }
                }
            );

            _logger.LogInformation("[ConnectionManager] ✅ Added connection {ConnectionId} for player {PlayerId} ({UserName})",
                connectionId, playerId, userName);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error adding connection {ConnectionId}: {Error}",
                connectionId, ex.Message);
            throw;
        }
    }

    public Task RemoveConnectionAsync(string connectionId)
    {
        try
        {
            _logger.LogInformation("[ConnectionManager] === REMOVING CONNECTION ===");
            _logger.LogInformation("[ConnectionManager] ConnectionId: {ConnectionId}", connectionId);

            if (!_connections.TryRemove(connectionId, out var connectionInfo))
            {
                _logger.LogWarning("[ConnectionManager] Attempted to remove non-existent connection {ConnectionId}",
                    connectionId);
                return Task.CompletedTask;
            }

            _connectionToPlayer.TryRemove(connectionId, out _);

            // Remover de la lista de conexiones del jugador con manejo thread-safe
            if (connectionInfo.PlayerId.HasValue)
            {
                var playerId = connectionInfo.PlayerId.Value;
                _logger.LogInformation("[ConnectionManager] Removing connection for player {PlayerId}", playerId);

                if (_playerConnections.TryGetValue(playerId, out var connections))
                {
                    lock (connections)
                    {
                        connections.Remove(connectionId);

                        // Si no quedan conexiones para este jugador
                        if (connections.Count == 0)
                        {
                            _playerConnections.TryRemove(playerId, out _);
                            _logger.LogInformation("[ConnectionManager] Player {PlayerId} has no more connections", playerId);

                            // SIMPLIFICADO: Solo guardar información básica de reconexión
                            SaveBasicReconnectionInfo(playerId);
                        }
                        else
                        {
                            _logger.LogInformation("[ConnectionManager] Player {PlayerId} still has {Count} other connections",
                                playerId, connections.Count);
                        }
                    }
                }
            }

            _logger.LogInformation("[ConnectionManager] ✅ Removed connection {ConnectionId} for player {PlayerId}",
                connectionId, connectionInfo.PlayerId);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error removing connection {ConnectionId}: {Error}",
                connectionId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// SIMPLIFICADO: Solo guarda timestamp de desconexión, no estado de grupos
    /// </summary>
    private void SaveBasicReconnectionInfo(Guid playerId)
    {
        try
        {
            _logger.LogInformation("[ConnectionManager] Saving basic reconnection info for player {PlayerId}", playerId);

            var reconnectionInfo = new ReconnectionInfo(
                PlayerId: playerId,
                LastRoomCode: null, // Se determinará por otros medios
                LastSeen: DateTime.UtcNow,
                WasInGame: false // Se determinará por otros medios
            );

            _reconnectionInfo[playerId] = reconnectionInfo;

            _logger.LogInformation("[ConnectionManager] ✅ Saved basic reconnection info for player {PlayerId}", playerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error saving reconnection info: {Error}", ex.Message);
        }
    }

    public Task UpdateConnectionAsync(string connectionId, string? roomCode = null)
    {
        // SIMPLIFICADO: NO trackear cambios de sala aquí
        // Esto debe manejarse a nivel de hub/servicio
        _logger.LogDebug("[ConnectionManager] UpdateConnection called but not tracking room changes internally");
        return Task.CompletedTask;
    }

    #endregion

    #region Consultas de conexiones

    public Task<List<string>> GetConnectionsForPlayerAsync(PlayerId playerId)
    {
        try
        {
            if (_playerConnections.TryGetValue(playerId.Value, out var connections))
            {
                lock (connections)
                {
                    return Task.FromResult(new List<string>(connections));
                }
            }

            return Task.FromResult(new List<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error getting connections for player {PlayerId}: {Error}",
                playerId, ex.Message);
            return Task.FromResult(new List<string>());
        }
    }

    public Task<List<string>> GetConnectionsInRoomAsync(string roomCode)
    {
        try
        {
            // SIMPLIFICADO: No podemos determinar esto sin estado de grupos
            // Este método debería moverse a SignalR nativo o un servicio especializado
            _logger.LogWarning("[ConnectionManager] GetConnectionsInRoomAsync not supported in simplified version");
            return Task.FromResult(new List<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error getting connections for room {RoomCode}: {Error}",
                roomCode, ex.Message);
            return Task.FromResult(new List<string>());
        }
    }

    public Task<ConnectionInfo?> GetConnectionInfoAsync(string connectionId)
    {
        try
        {
            _connections.TryGetValue(connectionId, out var connectionInfo);
            return Task.FromResult(connectionInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error getting connection info for {ConnectionId}: {Error}",
                connectionId, ex.Message);
            return Task.FromResult<ConnectionInfo?>(null);
        }
    }

    public Task<PlayerId?> GetPlayerIdByConnectionAsync(string connectionId)
    {
        try
        {
            if (_connectionToPlayer.TryGetValue(connectionId, out var playerId))
            {
                return Task.FromResult<PlayerId?>(PlayerId.From(playerId));
            }

            return Task.FromResult<PlayerId?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error getting player ID for connection {ConnectionId}: {Error}",
                connectionId, ex.Message);
            return Task.FromResult<PlayerId?>(null);
        }
    }

    #endregion

    #region Gestión de grupos - SIMPLIFICADO (NO HACER NADA)

    public Task AddToGroupAsync(string connectionId, string groupName)
    {
        // CRÍTICO: NO hacer nada aquí - los grupos se manejan en SignalR nativo
        _logger.LogDebug("[ConnectionManager] AddToGroupAsync called but ignored - groups handled by SignalR natively");
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(string connectionId, string groupName)
    {
        // CRÍTICO: NO hacer nada aquí - los grupos se manejan en SignalR nativo
        _logger.LogDebug("[ConnectionManager] RemoveFromGroupAsync called but ignored - groups handled by SignalR natively");
        return Task.CompletedTask;
    }

    public Task<List<string>> GetGroupsForConnectionAsync(string connectionId)
    {
        // SIMPLIFICADO: No trackear grupos aquí
        _logger.LogDebug("[ConnectionManager] GetGroupsForConnectionAsync not supported - use SignalR native methods");
        return Task.FromResult(new List<string>());
    }

    #endregion

    #region Estado de jugadores

    public Task<bool> IsPlayerOnlineAsync(PlayerId playerId)
    {
        try
        {
            var isOnline = _playerConnections.ContainsKey(playerId.Value);
            return Task.FromResult(isOnline);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error checking if player {PlayerId} is online: {Error}",
                playerId, ex.Message);
            return Task.FromResult(false);
        }
    }

    public Task<int> GetOnlinePlayerCountAsync()
    {
        try
        {
            return Task.FromResult(_playerConnections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error getting online player count: {Error}", ex.Message);
            return Task.FromResult(0);
        }
    }

    public Task<List<PlayerId>> GetOnlinePlayersAsync()
    {
        try
        {
            var onlinePlayers = _playerConnections.Keys
                .Select(PlayerId.From)
                .ToList();

            return Task.FromResult(onlinePlayers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error getting online players: {Error}", ex.Message);
            return Task.FromResult(new List<PlayerId>());
        }
    }

    public Task<List<PlayerId>> GetPlayersInRoomAsync(string roomCode)
    {
        try
        {
            // SIMPLIFICADO: No podemos determinar esto sin estado de grupos
            _logger.LogWarning("[ConnectionManager] GetPlayersInRoomAsync not supported in simplified version");
            return Task.FromResult(new List<PlayerId>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error getting players in room {RoomCode}: {Error}",
                roomCode, ex.Message);
            return Task.FromResult(new List<PlayerId>());
        }
    }

    #endregion

    #region Reconexión - SIMPLIFICADO

    public Task<ReconnectionInfo?> GetReconnectionInfoAsync(PlayerId playerId)
    {
        try
        {
            _logger.LogInformation("[ConnectionManager] Getting reconnection info for player {PlayerId}", playerId);

            _reconnectionInfo.TryGetValue(playerId.Value, out var info);

            if (info != null)
            {
                _logger.LogInformation("[ConnectionManager] Found reconnection info - LastSeen: {LastSeen}", info.LastSeen);
            }
            else
            {
                _logger.LogInformation("[ConnectionManager] No reconnection info found for player {PlayerId}", playerId);
            }

            return Task.FromResult(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error getting reconnection info for player {PlayerId}: {Error}",
                playerId, ex.Message);
            return Task.FromResult<ReconnectionInfo?>(null);
        }
    }

    public Task SaveReconnectionInfoAsync(PlayerId playerId, string? roomCode)
    {
        try
        {
            _logger.LogInformation("[ConnectionManager] Saving reconnection info for player {PlayerId}, room: {RoomCode}",
                playerId, roomCode);

            var info = new ReconnectionInfo(
                PlayerId: playerId.Value,
                LastRoomCode: roomCode,
                LastSeen: DateTime.UtcNow,
                WasInGame: !string.IsNullOrEmpty(roomCode)
            );

            _reconnectionInfo[playerId.Value] = info;

            _logger.LogInformation("[ConnectionManager] ✅ Saved reconnection info for player {PlayerId}", playerId);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error saving reconnection info for player {PlayerId}: {Error}",
                playerId, ex.Message);
            throw;
        }
    }

    public Task ClearReconnectionInfoAsync(PlayerId playerId)
    {
        try
        {
            _logger.LogInformation("[ConnectionManager] Clearing reconnection info for player {PlayerId}", playerId);

            var removed = _reconnectionInfo.TryRemove(playerId.Value, out _);

            _logger.LogInformation("[ConnectionManager] ✅ Cleared reconnection info (removed: {Removed}) for player {PlayerId}",
                removed, playerId);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error clearing reconnection info for player {PlayerId}: {Error}",
                playerId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// ELIMINADO: PlayerRoomState ya no se maneja aquí
    /// </summary>
    public Task<PlayerRoomState?> GetPlayerRoomStateAsync(PlayerId playerId)
    {
        _logger.LogDebug("[ConnectionManager] GetPlayerRoomStateAsync not supported in simplified version");
        return Task.FromResult<PlayerRoomState?>(null);
    }

    #endregion

    #region Limpieza y mantenimiento - SIMPLIFICADO

    public Task CleanupStaleConnectionsAsync()
    {
        try
        {
            _logger.LogDebug("[ConnectionManager] === STARTING CLEANUP ===");

            var cutoffTime = DateTime.UtcNow.AddMinutes(-30); // Considerar conexiones de más de 30 min como obsoletas
            var staleConnections = _connections.Values
                .Where(c => c.ConnectedAt < cutoffTime)
                .Select(c => c.ConnectionId)
                .ToList();

            // Limpiar reconexiones muy antiguas (más de 2 horas)
            var reconnectionCutoffTime = DateTime.UtcNow.AddHours(-2);
            var staleReconnections = _reconnectionInfo.Values
                .Where(r => r.LastSeen < reconnectionCutoffTime)
                .Select(r => r.PlayerId)
                .ToList();

            foreach (var connectionId in staleConnections)
            {
                _ = RemoveConnectionAsync(connectionId);
            }

            foreach (var playerId in staleReconnections)
            {
                _reconnectionInfo.TryRemove(playerId, out _);
            }

            if (staleConnections.Any() || staleReconnections.Any())
            {
                _logger.LogInformation("[ConnectionManager] ✅ Cleaned up {ConnectionCount} stale connections and {ReconnectionCount} old reconnection infos",
                    staleConnections.Count, staleReconnections.Count);
            }

            _logger.LogDebug("[ConnectionManager] === CLEANUP COMPLETED ===");
            _logger.LogDebug("[ConnectionManager] Active connections: {ConnectionCount}, Online players: {PlayerCount}, Pending reconnections: {ReconnectionCount}",
                _connections.Count, _playerConnections.Count, _reconnectionInfo.Count);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error during cleanup: {Error}", ex.Message);
            throw;
        }
    }

    public Task<int> GetTotalConnectionCountAsync()
    {
        try
        {
            return Task.FromResult(_connections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error getting total connection count: {Error}", ex.Message);
            return Task.FromResult(0);
        }
    }

    /// <summary>
    /// SIMPLIFICADO: Estadísticas básicas
    /// </summary>
    public Task<ConnectionManagerStats> GetStatsAsync()
    {
        try
        {
            var stats = new ConnectionManagerStats
            {
                TotalConnections = _connections.Count,
                OnlinePlayers = _playerConnections.Count,
                PendingReconnections = _reconnectionInfo.Count,
                PlayersWithRoomState = 0, // No se trackea en versión simplificada
                Timestamp = DateTime.UtcNow
            };

            return Task.FromResult(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error getting stats: {Error}", ex.Message);
            return Task.FromResult(new ConnectionManagerStats
            {
                TotalConnections = 0,
                OnlinePlayers = 0,
                PendingReconnections = 0,
                PlayersWithRoomState = 0,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    #endregion
}