// ConnectionManager.cs - En BlackJack.Realtime/Services/
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

    // Información de reconexión
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
            var connectionInfo = new ConnectionInfo(
                ConnectionId: connectionId,
                PlayerId: playerId.Value,
                UserName: userName,
                Groups: new List<string>(),
                ConnectedAt: DateTime.UtcNow
            );

            _connections[connectionId] = connectionInfo;
            _connectionToPlayer[connectionId] = playerId.Value;

            // Agregar a la lista de conexiones del jugador
            _playerConnections.AddOrUpdate(
                playerId.Value,
                new List<string> { connectionId },
                (key, existingList) =>
                {
                    existingList.Add(connectionId);
                    return existingList;
                }
            );

            _logger.LogInformation("[ConnectionManager] Added connection {ConnectionId} for player {PlayerId} ({UserName})",
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
            if (!_connections.TryRemove(connectionId, out var connectionInfo))
            {
                _logger.LogWarning("[ConnectionManager] Attempted to remove non-existent connection {ConnectionId}",
                    connectionId);
                return Task.CompletedTask;
            }

            _connectionToPlayer.TryRemove(connectionId, out _);

            // Remover de la lista de conexiones del jugador
            if (connectionInfo.PlayerId.HasValue)
            {
                var playerId = connectionInfo.PlayerId.Value;
                if (_playerConnections.TryGetValue(playerId, out var connections))
                {
                    connections.Remove(connectionId);

                    // Si no quedan conexiones para este jugador, remover la entrada
                    if (connections.Count == 0)
                    {
                        _playerConnections.TryRemove(playerId, out _);

                        // Guardar información de reconexión
                        var lastRoomCode = connectionInfo.Groups
                            .FirstOrDefault(g => g.StartsWith("Room_"))?
                            .Replace("Room_", "");

                        if (!string.IsNullOrEmpty(lastRoomCode))
                        {
                            SaveReconnectionInfoAsync(PlayerId.From(playerId), lastRoomCode);
                        }
                    }
                }
            }

            _logger.LogInformation("[ConnectionManager] Removed connection {ConnectionId} for player {PlayerId}",
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

    public Task UpdateConnectionAsync(string connectionId, string? roomCode = null)
    {
        try
        {
            if (_connections.TryGetValue(connectionId, out var connectionInfo))
            {
                var updatedGroups = new List<string>(connectionInfo.Groups);

                if (!string.IsNullOrEmpty(roomCode))
                {
                    var roomGroup = HubMethodNames.Groups.GetRoomGroup(roomCode);
                    if (!updatedGroups.Contains(roomGroup))
                    {
                        updatedGroups.Add(roomGroup);
                    }
                }

                var updatedInfo = connectionInfo with { Groups = updatedGroups };
                _connections[connectionId] = updatedInfo;

                _logger.LogDebug("[ConnectionManager] Updated connection {ConnectionId} with room {RoomCode}",
                    connectionId, roomCode);
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error updating connection {ConnectionId}: {Error}",
                connectionId, ex.Message);
            throw;
        }
    }

    #endregion

    #region Consultas de conexiones

    public Task<List<string>> GetConnectionsForPlayerAsync(PlayerId playerId)
    {
        try
        {
            if (_playerConnections.TryGetValue(playerId.Value, out var connections))
            {
                return Task.FromResult(new List<string>(connections));
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
            var roomGroup = HubMethodNames.Groups.GetRoomGroup(roomCode);
            var connectionsInRoom = _connections.Values
                .Where(c => c.Groups.Contains(roomGroup))
                .Select(c => c.ConnectionId)
                .ToList();

            return Task.FromResult(connectionsInRoom);
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

    #region Gestión de grupos/salas

    public Task AddToGroupAsync(string connectionId, string groupName)
    {
        try
        {
            if (_connections.TryGetValue(connectionId, out var connectionInfo))
            {
                var updatedGroups = new List<string>(connectionInfo.Groups);
                if (!updatedGroups.Contains(groupName))
                {
                    updatedGroups.Add(groupName);
                    var updatedInfo = connectionInfo with { Groups = updatedGroups };
                    _connections[connectionId] = updatedInfo;

                    _logger.LogDebug("[ConnectionManager] Added connection {ConnectionId} to group {GroupName}",
                        connectionId, groupName);
                }
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error adding connection {ConnectionId} to group {GroupName}: {Error}",
                connectionId, groupName, ex.Message);
            throw;
        }
    }

    public Task RemoveFromGroupAsync(string connectionId, string groupName)
    {
        try
        {
            if (_connections.TryGetValue(connectionId, out var connectionInfo))
            {
                var updatedGroups = new List<string>(connectionInfo.Groups);
                if (updatedGroups.Remove(groupName))
                {
                    var updatedInfo = connectionInfo with { Groups = updatedGroups };
                    _connections[connectionId] = updatedInfo;

                    _logger.LogDebug("[ConnectionManager] Removed connection {ConnectionId} from group {GroupName}",
                        connectionId, groupName);
                }
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error removing connection {ConnectionId} from group {GroupName}: {Error}",
                connectionId, groupName, ex.Message);
            throw;
        }
    }

    public Task<List<string>> GetGroupsForConnectionAsync(string connectionId)
    {
        try
        {
            if (_connections.TryGetValue(connectionId, out var connectionInfo))
            {
                return Task.FromResult(new List<string>(connectionInfo.Groups));
            }

            return Task.FromResult(new List<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error getting groups for connection {ConnectionId}: {Error}",
                connectionId, ex.Message);
            return Task.FromResult(new List<string>());
        }
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
            var roomGroup = HubMethodNames.Groups.GetRoomGroup(roomCode);
            var playersInRoom = _connections.Values
                .Where(c => c.Groups.Contains(roomGroup) && c.PlayerId.HasValue)
                .Select(c => PlayerId.From(c.PlayerId!.Value))
                .Distinct()
                .ToList();

            return Task.FromResult(playersInRoom);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error getting players in room {RoomCode}: {Error}",
                roomCode, ex.Message);
            return Task.FromResult(new List<PlayerId>());
        }
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

            _logger.LogInformation("[ConnectionManager] Saved reconnection info for player {PlayerId}, room: {RoomCode}",
                playerId, roomCode);

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
            _reconnectionInfo.TryRemove(playerId.Value, out _);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error clearing reconnection info for player {PlayerId}: {Error}",
                playerId, ex.Message);
            throw;
        }
    }

    #endregion

    #region Limpieza y mantenimiento

    public Task CleanupStaleConnectionsAsync()
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-30); // Considerar conexiones de más de 30 min como obsoletas
            var staleConnections = _connections.Values
                .Where(c => c.ConnectedAt < cutoffTime)
                .Select(c => c.ConnectionId)
                .ToList();

            foreach (var connectionId in staleConnections)
            {
                RemoveConnectionAsync(connectionId);
            }

            if (staleConnections.Any())
            {
                _logger.LogInformation("[ConnectionManager] Cleaned up {Count} stale connections",
                    staleConnections.Count);
            }

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

    #endregion
}