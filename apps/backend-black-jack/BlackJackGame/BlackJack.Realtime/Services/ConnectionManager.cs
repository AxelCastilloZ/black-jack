// ConnectionManager.cs - MEJORADO PARA RECONEXIÓN ROBUSTA Y MANTENIMIENTO DE ESTADO (USANDO MODELOS EXISTENTES)
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

    // MEJORADO: Información de reconexión más robusta
    private readonly ConcurrentDictionary<Guid, ReconnectionInfo> _reconnectionInfo = new();

    // NUEVO: Cache de estado de salas por jugador para reconexión rápida
    private readonly ConcurrentDictionary<Guid, PlayerRoomState> _playerRoomStates = new();

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

            _logger.LogInformation("[ConnectionManager] ✅ Added connection {ConnectionId} for player {PlayerId} ({UserName})",
                connectionId, playerId, userName);

            // NUEVO: Actualizar estado de reconexión si existe
            if (_reconnectionInfo.ContainsKey(playerId.Value))
            {
                _logger.LogInformation("[ConnectionManager] Player {PlayerId} has reconnection info - will be handled by hub",
                    playerId);
            }

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

                            // MEJORADO: Guardar estado completo para reconexión
                            SavePlayerStateForReconnection(connectionInfo, playerId);
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
    /// NUEVO: Guarda estado completo del jugador para reconexión
    /// </summary>
    private void SavePlayerStateForReconnection(ConnectionInfo connectionInfo, Guid playerId)
    {
        try
        {
            _logger.LogInformation("[ConnectionManager] === SAVING PLAYER STATE FOR RECONNECTION ===");
            _logger.LogInformation("[ConnectionManager] PlayerId: {PlayerId}", playerId);

            // Determinar sala actual basándose en los grupos
            var roomGroups = connectionInfo.Groups
                .Where(g => g.StartsWith("Room_"))
                .ToList();

            var tableGroups = connectionInfo.Groups
                .Where(g => g.StartsWith("Table_"))
                .ToList();

            string? lastRoomCode = null;
            string? lastTableId = null;

            if (roomGroups.Any())
            {
                // Extraer RoomCode del grupo "Room_{RoomCode}"
                lastRoomCode = roomGroups.First().Replace("Room_", "");
                _logger.LogInformation("[ConnectionManager] Found room group: {RoomCode}", lastRoomCode);
            }

            if (tableGroups.Any())
            {
                // Extraer TableId del grupo "Table_{TableId}"
                lastTableId = tableGroups.First().Replace("Table_", "");
                _logger.LogInformation("[ConnectionManager] Found table group: {TableId}", lastTableId);
            }

            // Guardar información de reconexión si está en una sala
            if (!string.IsNullOrEmpty(lastRoomCode))
            {
                var reconnectionInfo = new ReconnectionInfo(
                    PlayerId: playerId,
                    LastRoomCode: lastRoomCode,
                    LastSeen: DateTime.UtcNow,
                    WasInGame: true
                );

                _reconnectionInfo[playerId] = reconnectionInfo;

                // NUEVO: Guardar estado de sala más detallado
                var playerRoomState = new PlayerRoomState(
                    PlayerId: playerId,
                    RoomCode: lastRoomCode,
                    TableId: lastTableId,
                    Groups: connectionInfo.Groups.ToList(),
                    LastActivity: DateTime.UtcNow,
                    ConnectionLostAt: DateTime.UtcNow
                );

                _playerRoomStates[playerId] = playerRoomState;

                _logger.LogInformation("[ConnectionManager] ✅ Saved reconnection info for player {PlayerId} - Room: {RoomCode}, Table: {TableId}",
                    playerId, lastRoomCode, lastTableId ?? "None");
            }
            else
            {
                _logger.LogInformation("[ConnectionManager] Player {PlayerId} was not in any room - no reconnection info saved", playerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error saving player state for reconnection: {Error}", ex.Message);
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

                    // NUEVO: Actualizar estado de sala del jugador
                    if (connectionInfo.PlayerId.HasValue)
                    {
                        UpdatePlayerRoomState(connectionInfo.PlayerId.Value, roomCode);
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

    /// <summary>
    /// NUEVO: Actualiza el estado de sala del jugador
    /// </summary>
    private void UpdatePlayerRoomState(Guid playerId, string roomCode)
    {
        try
        {
            if (_playerRoomStates.TryGetValue(playerId, out var existingState))
            {
                var updatedState = existingState with
                {
                    RoomCode = roomCode,
                    LastActivity = DateTime.UtcNow
                };
                _playerRoomStates[playerId] = updatedState;
            }
            else
            {
                var newState = new PlayerRoomState(
                    PlayerId: playerId,
                    RoomCode: roomCode,
                    TableId: null,
                    Groups: new List<string> { HubMethodNames.Groups.GetRoomGroup(roomCode) },
                    LastActivity: DateTime.UtcNow,
                    ConnectionLostAt: null
                );
                _playerRoomStates[playerId] = newState;
            }

            _logger.LogDebug("[ConnectionManager] Updated room state for player {PlayerId} - Room: {RoomCode}",
                playerId, roomCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error updating player room state: {Error}", ex.Message);
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

                    // NUEVO: Actualizar estado si es un grupo de sala
                    if (groupName.StartsWith("Room_") && connectionInfo.PlayerId.HasValue)
                    {
                        var roomCode = groupName.Replace("Room_", "");
                        UpdatePlayerRoomState(connectionInfo.PlayerId.Value, roomCode);
                    }
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

                    // NUEVO: Limpiar estado si sale de un grupo de sala
                    if (groupName.StartsWith("Room_") && connectionInfo.PlayerId.HasValue)
                    {
                        ClearPlayerRoomState(connectionInfo.PlayerId.Value);
                    }
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

    /// <summary>
    /// NUEVO: Limpia el estado de sala del jugador
    /// </summary>
    private void ClearPlayerRoomState(Guid playerId)
    {
        try
        {
            _playerRoomStates.TryRemove(playerId, out _);
            _logger.LogDebug("[ConnectionManager] Cleared room state for player {PlayerId}", playerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error clearing player room state: {Error}", ex.Message);
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

    #region Reconexión - MEJORADO

    public Task<ReconnectionInfo?> GetReconnectionInfoAsync(PlayerId playerId)
    {
        try
        {
            _logger.LogInformation("[ConnectionManager] === GETTING RECONNECTION INFO ===");
            _logger.LogInformation("[ConnectionManager] PlayerId: {PlayerId}", playerId);

            _reconnectionInfo.TryGetValue(playerId.Value, out var info);

            if (info != null)
            {
                _logger.LogInformation("[ConnectionManager] ✅ Found reconnection info - Room: {RoomCode}, LastSeen: {LastSeen}",
                    info.LastRoomCode, info.LastSeen);
            }
            else
            {
                _logger.LogInformation("[ConnectionManager] ❌ No reconnection info found for player {PlayerId}", playerId);
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
            _logger.LogInformation("[ConnectionManager] === SAVING RECONNECTION INFO ===");
            _logger.LogInformation("[ConnectionManager] PlayerId: {PlayerId}, RoomCode: {RoomCode}", playerId, roomCode);

            var info = new ReconnectionInfo(
                PlayerId: playerId.Value,
                LastRoomCode: roomCode,
                LastSeen: DateTime.UtcNow,
                WasInGame: !string.IsNullOrEmpty(roomCode)
            );

            _reconnectionInfo[playerId.Value] = info;

            _logger.LogInformation("[ConnectionManager] ✅ Saved reconnection info for player {PlayerId}, room: {RoomCode}",
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
            _logger.LogInformation("[ConnectionManager] === CLEARING RECONNECTION INFO ===");
            _logger.LogInformation("[ConnectionManager] PlayerId: {PlayerId}", playerId);

            var removed1 = _reconnectionInfo.TryRemove(playerId.Value, out _);
            var removed2 = _playerRoomStates.TryRemove(playerId.Value, out _);

            _logger.LogInformation("[ConnectionManager] ✅ Cleared reconnection info (removed: {Removed1}) and room state (removed: {Removed2}) for player {PlayerId}",
                removed1, removed2, playerId);

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
    /// NUEVO: Obtiene el estado detallado de sala del jugador
    /// </summary>
    public Task<PlayerRoomState?> GetPlayerRoomStateAsync(PlayerId playerId)
    {
        try
        {
            _playerRoomStates.TryGetValue(playerId.Value, out var state);
            return Task.FromResult(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionManager] Error getting player room state for {PlayerId}: {Error}",
                playerId, ex.Message);
            return Task.FromResult<PlayerRoomState?>(null);
        }
    }

    #endregion

    #region Limpieza y mantenimiento - MEJORADO

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
                _playerRoomStates.TryRemove(playerId, out _);
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
    /// NUEVO: Obtiene estadísticas detalladas del manager
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
                PlayersWithRoomState = _playerRoomStates.Count,
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