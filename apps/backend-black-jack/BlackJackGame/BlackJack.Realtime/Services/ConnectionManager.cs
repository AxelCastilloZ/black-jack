// BlackJack.Realtime/Services/ConnectionManager.cs - Solo tracking de conexiones
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

    // Información básica de reconexión
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
            _logger.LogInformation("[ConnectionManager] Adding connection {ConnectionId} for player {PlayerId}",
                connectionId, playerId);

            var connectionInfo = new ConnectionInfo(
                ConnectionId: connectionId,
                PlayerId: playerId.Value,
                UserName: userName,
                Groups: new List<string>(),
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

            _logger.LogDebug("[ConnectionManager] Added connection {ConnectionId} for player {PlayerId}",
                connectionId, playerId);

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
            _logger.LogInformation("[ConnectionManager] Removing connection {ConnectionId}", connectionId);

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

                if (_playerConnections.TryGetValue(playerId, out var connections))
                {
                    lock (connections)
                    {
                        connections.Remove(connectionId);

                        // Si no quedan conexiones para este jugador, guardar info de reconexión
                        if (connections.Count == 0)
                        {
                            _playerConnections.TryRemove(playerId, out _);
                            SaveBasicReconnectionInfo(playerId);

                            _logger.LogInformation("[ConnectionManager] Player {PlayerId} has no more connections", playerId);
                        }
                    }
                }
            }

            _logger.LogDebug("[ConnectionManager] Removed connection {ConnectionId}", connectionId);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error removing connection {ConnectionId}: {Error}",
                connectionId, ex.Message);
            throw;
        }
    }

    public Task UpdateConnectionAsync(string connectionId, string? roomCode = null)
    {
        // No necesario en versión simplificada
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
        // No implementado en versión simplificada - usar SignalR Groups directamente
        _logger.LogDebug("[ConnectionManager] GetConnectionsInRoomAsync not supported in simplified version");
        return Task.FromResult(new List<string>());
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

    #region Gestión de grupos - Simplificado (placeholder)

    public Task AddToGroupAsync(string connectionId, string groupName)
    {
        // Placeholder - los grupos se manejan directamente en SignalR
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(string connectionId, string groupName)
    {
        // Placeholder - los grupos se manejan directamente en SignalR
        return Task.CompletedTask;
    }

    public Task<List<string>> GetGroupsForConnectionAsync(string connectionId)
    {
        // No soportado en versión simplificada
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
        // No soportado en versión simplificada
        return Task.FromResult(new List<PlayerId>());
    }

    #endregion

    #region Reconexión

    public Task<ReconnectionInfo?> GetReconnectionInfoAsync(PlayerId playerId)
    {
        try
        {
            _reconnectionInfo.TryGetValue(playerId.Value, out var info);
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
            var info = new ReconnectionInfo(
                PlayerId: playerId.Value,
                LastRoomCode: roomCode,
                LastSeen: DateTime.UtcNow,
                WasInGame: !string.IsNullOrEmpty(roomCode)
            );

            _reconnectionInfo[playerId.Value] = info;

            _logger.LogDebug("[ConnectionManager] Saved reconnection info for player {PlayerId}", playerId);
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
            var removed = _reconnectionInfo.TryRemove(playerId.Value, out _);

            _logger.LogDebug("[ConnectionManager] Cleared reconnection info for player {PlayerId} (removed: {Removed})",
                playerId, removed);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error clearing reconnection info for player {PlayerId}: {Error}",
                playerId, ex.Message);
            throw;
        }
    }

    public Task<PlayerRoomState?> GetPlayerRoomStateAsync(PlayerId playerId)
    {
        // No soportado en versión simplificada
        return Task.FromResult<PlayerRoomState?>(null);
    }

    #endregion

    #region Limpieza y mantenimiento

    public Task CleanupStaleConnectionsAsync()
    {
        try
        {
            _logger.LogDebug("[ConnectionManager] Starting cleanup");

            var cutoffTime = DateTime.UtcNow.AddMinutes(-30);
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

            // Ejecutar limpieza
            var cleanupTasks = staleConnections.Select(RemoveConnectionAsync).ToList();
            foreach (var playerId in staleReconnections)
            {
                _reconnectionInfo.TryRemove(playerId, out _);
            }

            if (staleConnections.Any() || staleReconnections.Any())
            {
                _logger.LogInformation("[ConnectionManager] Cleaned up {ConnectionCount} stale connections and {ReconnectionCount} old reconnection infos",
                    staleConnections.Count, staleReconnections.Count);
            }

            return Task.WhenAll(cleanupTasks);
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

    #region Private Methods

    private void SaveBasicReconnectionInfo(Guid playerId)
    {
        try
        {
            var reconnectionInfo = new ReconnectionInfo(
                PlayerId: playerId,
                LastRoomCode: null, // Se determinará por otros medios
                LastSeen: DateTime.UtcNow,
                WasInGame: false // Se determinará por otros medios
            );

            _reconnectionInfo[playerId] = reconnectionInfo;

            _logger.LogDebug("[ConnectionManager] Saved basic reconnection info for player {PlayerId}", playerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error saving basic reconnection info: {Error}", ex.Message);
        }
    }

    #endregion
}