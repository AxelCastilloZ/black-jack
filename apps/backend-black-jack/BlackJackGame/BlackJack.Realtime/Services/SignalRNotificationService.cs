// BlackJack.Realtime/Services/SignalRNotificationService.cs - Envío de mensajes
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using BlackJack.Domain.Models.Users;
using BlackJack.Realtime.Models;
using BlackJack.Realtime.Hubs;

namespace BlackJack.Realtime.Services;

public class SignalRNotificationService : ISignalRNotificationService
{
    private readonly IHubContext<GameRoomHub> _gameRoomHubContext;
    private readonly IHubContext<GameControlHub> _gameControlHubContext;
    private readonly IHubContext<LobbyHub> _lobbyHubContext;
    private readonly IConnectionManager _connectionManager;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        IHubContext<GameRoomHub> gameRoomHubContext,
        IHubContext<GameControlHub> gameControlHubContext,
        IHubContext<LobbyHub> lobbyHubContext,
        IConnectionManager connectionManager,
        ILogger<SignalRNotificationService> logger)
    {
        _gameRoomHubContext = gameRoomHubContext;
        _gameControlHubContext = gameControlHubContext;
        _lobbyHubContext = lobbyHubContext;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    #region Notificaciones de sala

    public async Task NotifyRoomAsync<T>(string roomCode, string methodName, T data)
    {
        try
        {
            var groupName = HubMethodNames.Groups.GetRoomGroup(roomCode);

            _logger.LogDebug("[SignalRNotification] Sending {MethodName} to room {RoomCode}",
                methodName, roomCode);

            // Enviar a ambos hubs de juego para cobertura completa
            var tasks = new List<Task>
            {
                _gameRoomHubContext.Clients.Group(groupName).SendAsync(methodName, data),
                _gameControlHubContext.Clients.Group(groupName).SendAsync(methodName, data)
            };

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SignalRNotification] Error sending {MethodName} to room {RoomCode}: {Error}",
                methodName, roomCode, ex.Message);
            throw;
        }
    }

    public async Task NotifyRoomAsync(string roomCode, string methodName, object data)
    {
        await NotifyRoomAsync<object>(roomCode, methodName, data);
    }

    public async Task NotifyRoomExceptAsync<T>(string roomCode, string excludeConnectionId, string methodName, T data)
    {
        try
        {
            var groupName = HubMethodNames.Groups.GetRoomGroup(roomCode);

            _logger.LogDebug("[SignalRNotification] Sending {MethodName} to room {RoomCode} except connection {ConnectionId}",
                methodName, roomCode, excludeConnectionId);

            // Enviar a ambos hubs de juego excepto la conexión especificada
            var tasks = new List<Task>
            {
                _gameRoomHubContext.Clients.GroupExcept(groupName, excludeConnectionId).SendAsync(methodName, data),
                _gameControlHubContext.Clients.GroupExcept(groupName, excludeConnectionId).SendAsync(methodName, data)
            };

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SignalRNotification] Error sending {MethodName} to room {RoomCode} except {ConnectionId}: {Error}",
                methodName, roomCode, excludeConnectionId, ex.Message);
            throw;
        }
    }

    public async Task NotifyRoomExceptAsync(string roomCode, PlayerId excludePlayerId, string methodName, object data)
    {
        try
        {
            var playerConnections = await _connectionManager.GetConnectionsForPlayerAsync(excludePlayerId);
            var groupName = HubMethodNames.Groups.GetRoomGroup(roomCode);

            _logger.LogDebug("[SignalRNotification] Sending {MethodName} to room {RoomCode} except player {PlayerId} ({ConnectionCount} connections)",
                methodName, roomCode, excludePlayerId, playerConnections.Count);

            // Enviar a ambos hubs de juego excepto las conexiones del jugador
            var tasks = new List<Task>
            {
                _gameRoomHubContext.Clients.GroupExcept(groupName, playerConnections).SendAsync(methodName, data),
                _gameControlHubContext.Clients.GroupExcept(groupName, playerConnections).SendAsync(methodName, data)
            };

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SignalRNotification] Error sending {MethodName} to room {RoomCode} except player {PlayerId}: {Error}",
                methodName, roomCode, excludePlayerId, ex.Message);
            throw;
        }
    }

    #endregion

    #region Notificaciones específicas de jugador

    public async Task NotifyPlayerAsync<T>(PlayerId playerId, string methodName, T data)
    {
        try
        {
            var connections = await _connectionManager.GetConnectionsForPlayerAsync(playerId);
            if (connections.Any())
            {
                _logger.LogDebug("[SignalRNotification] Sending {MethodName} to player {PlayerId} ({ConnectionCount} connections)",
                    methodName, playerId, connections.Count);

                // Enviar a todas las conexiones del jugador en todos los hubs
                var tasks = new List<Task>
                {
                    _gameRoomHubContext.Clients.Clients(connections).SendAsync(methodName, data),
                    _gameControlHubContext.Clients.Clients(connections).SendAsync(methodName, data),
                    _lobbyHubContext.Clients.Clients(connections).SendAsync(methodName, data)
                };

                await Task.WhenAll(tasks);
            }
            else
            {
                _logger.LogWarning("[SignalRNotification] No connections found for player {PlayerId}", playerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SignalRNotification] Error sending {MethodName} to player {PlayerId}: {Error}",
                methodName, playerId, ex.Message);
            throw;
        }
    }

    public async Task NotifyPlayerAsync(PlayerId playerId, string methodName, object data)
    {
        await NotifyPlayerAsync<object>(playerId, methodName, data);
    }

    public async Task NotifyConnectionAsync<T>(string connectionId, string methodName, T data)
    {
        try
        {
            _logger.LogDebug("[SignalRNotification] Sending {MethodName} to connection {ConnectionId}",
                methodName, connectionId);

            // Enviar a la conexión específica en todos los hubs
            var tasks = new List<Task>
            {
                _gameRoomHubContext.Clients.Client(connectionId).SendAsync(methodName, data),
                _gameControlHubContext.Clients.Client(connectionId).SendAsync(methodName, data),
                _lobbyHubContext.Clients.Client(connectionId).SendAsync(methodName, data)
            };

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SignalRNotification] Error sending {MethodName} to connection {ConnectionId}: {Error}",
                methodName, connectionId, ex.Message);
            throw;
        }
    }

    public async Task NotifyConnectionAsync(string connectionId, string methodName, object data)
    {
        await NotifyConnectionAsync<object>(connectionId, methodName, data);
    }

    #endregion

    #region Notificaciones grupales

    public async Task NotifyAllAsync<T>(string methodName, T data)
    {
        try
        {
            _logger.LogDebug("[SignalRNotification] Broadcasting {MethodName} to ALL clients", methodName);

            // Enviar a todos los hubs
            var tasks = new List<Task>
            {
                _gameRoomHubContext.Clients.All.SendAsync(methodName, data),
                _gameControlHubContext.Clients.All.SendAsync(methodName, data),
                _lobbyHubContext.Clients.All.SendAsync(methodName, data)
            };

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SignalRNotification] Error broadcasting {MethodName}: {Error}", methodName, ex.Message);
            throw;
        }
    }

    public async Task NotifyAllAsync(string methodName, object data)
    {
        await NotifyAllAsync<object>(methodName, data);
    }

    public async Task NotifyGroupAsync<T>(string groupName, string methodName, T data)
    {
        try
        {
            _logger.LogDebug("[SignalRNotification] Sending {MethodName} to group {GroupName}",
                methodName, groupName);

            if (groupName == HubMethodNames.Groups.LobbyGroup)
            {
                // Solo enviar al LobbyHub para grupo lobby
                await _lobbyHubContext.Clients.Group(groupName).SendAsync(methodName, data);
            }
            else
            {
                // Enviar a ambos hubs de juego para grupos de sala/mesa
                var tasks = new List<Task>
                {
                    _gameRoomHubContext.Clients.Group(groupName).SendAsync(methodName, data),
                    _gameControlHubContext.Clients.Group(groupName).SendAsync(methodName, data)
                };

                await Task.WhenAll(tasks);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SignalRNotification] Error sending {MethodName} to group {GroupName}: {Error}",
                methodName, groupName, ex.Message);
            throw;
        }
    }

    public async Task NotifyGroupAsync(string groupName, string methodName, object data)
    {
        await NotifyGroupAsync<object>(groupName, methodName, data);
    }

    #endregion

    #region Eventos específicos del juego

    public async Task NotifyPlayerJoinedAsync(string roomCode, PlayerJoinedEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] Player {PlayerName} joined room {RoomCode}",
            eventData.PlayerName, roomCode);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.PlayerJoined, eventData);
    }

    public async Task NotifyPlayerLeftAsync(string roomCode, PlayerLeftEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] Player {PlayerName} left room {RoomCode}",
            eventData.PlayerName, roomCode);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.PlayerLeft, eventData);
    }

    public async Task NotifySpectatorJoinedAsync(string roomCode, SpectatorModel spectator)
    {
        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.SpectatorJoined, spectator);
    }

    public async Task NotifySpectatorLeftAsync(string roomCode, SpectatorModel spectator)
    {
        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.SpectatorLeft, spectator);
    }

    public async Task NotifyGameStartedAsync(string roomCode, GameStartedEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] Game started in room {RoomCode}", roomCode);
        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.GameStarted, eventData);
    }

    public async Task NotifyGameEndedAsync(string roomCode, GameEndedEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] Game ended in room {RoomCode}", roomCode);
        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.GameEnded, eventData);
    }

    public async Task NotifyTurnChangedAsync(string roomCode, TurnChangedEventModel eventData)
    {
        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.TurnChanged, eventData);
    }

    public async Task NotifyCardDealtAsync(string roomCode, CardDealtEventModel eventData)
    {
        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.CardDealt, eventData);
    }

    public async Task NotifyPlayerActionAsync(string roomCode, PlayerActionEventModel eventData)
    {
        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.PlayerActionPerformed, eventData);
    }

    public async Task NotifyBetPlacedAsync(string roomCode, BetPlacedEventModel eventData)
    {
        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.BetPlaced, eventData);
    }

    public async Task NotifyGameStateUpdatedAsync(string roomCode, GameStateModel gameState)
    {
        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.GameStateUpdated, gameState);
    }

    public async Task NotifyRoomInfoUpdatedAsync(string roomCode, RoomInfoModel roomInfo)
    {
        _logger.LogInformation("[SignalRNotification] Room info updated for {RoomCode}, PlayerCount: {PlayerCount}",
            roomCode, roomInfo.PlayerCount);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);
    }

    #endregion

    #region Auto-Betting Events

    public async Task NotifyAutoBetProcessedAsync(string roomCode, AutoBetProcessedEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] Auto-bet processed in room {RoomCode}: {Successful} successful, {Failed} failed",
            roomCode, eventData.SuccessfulBets, eventData.FailedBets);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.AutoBetProcessed, eventData);
    }

    public async Task NotifyPlayerRemovedFromSeatAsync(string roomCode, PlayerRemovedFromSeatEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] Player {PlayerName} removed from seat in room {RoomCode}",
            eventData.PlayerName, roomCode);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.PlayerRemovedFromSeat, eventData);

        // También notificar individualmente al jugador removido
        var playerId = PlayerId.From(eventData.PlayerId);
        await NotifyPlayerAsync(playerId, HubMethodNames.ServerMethods.YouWereRemovedFromSeat, eventData);
    }

    public async Task NotifyPlayerBalanceUpdatedAsync(string roomCode, PlayerBalanceUpdatedEventModel eventData)
    {
        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.PlayerBalanceUpdated, eventData);

        // También notificar individualmente al jugador afectado
        var playerId = PlayerId.From(eventData.PlayerId);
        await NotifyPlayerAsync(playerId, HubMethodNames.ServerMethods.YourBalanceUpdated, eventData);
    }

    public async Task NotifyInsufficientFundsWarningAsync(string roomCode, InsufficientFundsWarningEventModel eventData)
    {
        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.InsufficientFundsWarning, eventData);

        // También notificar individualmente al jugador con la advertencia
        var playerId = PlayerId.From(eventData.PlayerId);
        await NotifyPlayerAsync(playerId, HubMethodNames.ServerMethods.InsufficientFundsWarningPersonal, eventData);
    }

    public async Task NotifyAutoBetStatisticsAsync(string roomCode, AutoBetStatisticsEventModel eventData)
    {
        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.AutoBetStatistics, eventData);
    }

    public async Task NotifyAutoBetProcessingStartedAsync(string roomCode, AutoBetProcessingStartedEventModel eventData)
    {
        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.AutoBetProcessingStarted, eventData);
    }

    public async Task NotifyAutoBetFailedAsync(string roomCode, AutoBetFailedEventModel eventData)
    {
        _logger.LogError("[SignalRNotification] Auto-bet failed in room {RoomCode}: {ErrorMessage}",
            roomCode, eventData.ErrorMessage);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.AutoBetFailed, eventData);

        // También notificar individualmente a los jugadores afectados
        foreach (var playerId in eventData.AffectedPlayerIds)
        {
            await NotifyPlayerAsync(PlayerId.From(playerId), HubMethodNames.ServerMethods.AutoBetFailedPersonal, eventData);
        }
    }

    public async Task NotifyMinBetPerRoundUpdatedAsync(string roomCode, MinBetPerRoundUpdatedEventModel eventData)
    {
        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.MinBetPerRoundUpdated, eventData);
    }

    public async Task NotifyAutoBetRoundSummaryAsync(string roomCode, AutoBetRoundSummaryEventModel eventData)
    {
        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.AutoBetRoundSummary, eventData);
    }

    #endregion

    #region Notificaciones de lobby

    public async Task NotifyActiveRoomsUpdatedAsync(List<ActiveRoomModel> activeRooms)
    {
        _logger.LogDebug("[SignalRNotification] Updating active rooms list with {RoomCount} rooms", activeRooms.Count);

        await _lobbyHubContext.Clients.Group(HubMethodNames.Groups.LobbyGroup)
            .SendAsync(HubMethodNames.ServerMethods.ActiveRoomsUpdated, activeRooms);
    }

    public async Task NotifyRoomCreatedAsync(ActiveRoomModel newRoom)
    {
        await _lobbyHubContext.Clients.Group(HubMethodNames.Groups.LobbyGroup)
            .SendAsync(HubMethodNames.ServerMethods.RoomListUpdated, new { action = "created", room = newRoom });
    }

    public async Task NotifyRoomClosedAsync(string roomCode)
    {
        await _lobbyHubContext.Clients.Group(HubMethodNames.Groups.LobbyGroup)
            .SendAsync(HubMethodNames.ServerMethods.RoomListUpdated, new { action = "closed", roomCode });
    }

    #endregion

    #region Notificaciones de error y éxito

    public async Task SendErrorToConnectionAsync(string connectionId, string message, string? code = null)
    {
        var errorModel = new ErrorModel(message, code, null, DateTime.UtcNow);
        await NotifyConnectionAsync(connectionId, HubMethodNames.ServerMethods.Error, errorModel);
    }

    public async Task SendSuccessToConnectionAsync(string connectionId, string message, object? data = null)
    {
        var successModel = new SuccessModel(message, data, DateTime.UtcNow);
        await NotifyConnectionAsync(connectionId, HubMethodNames.ServerMethods.Success, successModel);
    }

    public async Task SendErrorToPlayerAsync(PlayerId playerId, string message, string? code = null)
    {
        var errorModel = new ErrorModel(message, code, null, DateTime.UtcNow);
        await NotifyPlayerAsync(playerId, HubMethodNames.ServerMethods.Error, errorModel);
    }

    public async Task SendSuccessToPlayerAsync(PlayerId playerId, string message, object? data = null)
    {
        var successModel = new SuccessModel(message, data, DateTime.UtcNow);
        await NotifyPlayerAsync(playerId, HubMethodNames.ServerMethods.Success, successModel);
    }

    #endregion

    #region Chat (opcional)

    public async Task SendChatMessageAsync(string roomCode, ChatMessageModel message)
    {
        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.MessageReceived, message);
    }

    public async Task SendSystemMessageAsync(string roomCode, string message)
    {
        var systemMessage = new ChatMessageModel(
            PlayerId: Guid.Empty,
            PlayerName: "Sistema",
            Message: message,
            Timestamp: DateTime.UtcNow,
            Type: "system"
        );

        await SendChatMessageAsync(roomCode, systemMessage);
    }

    #endregion
}