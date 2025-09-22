// SignalRNotificationService.cs - CORREGIDO COMPLETAMENTE PARA ENVIAR A MÚLTIPLES HUBS + AUTO-BETTING
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using BlackJack.Domain.Models.Users;
using BlackJack.Realtime.Models;
using BlackJack.Realtime.Hubs;

namespace BlackJack.Realtime.Services;

public class SignalRNotificationService : ISignalRNotificationService
{
    private readonly IHubContext<GameHub> _gameHubContext;
    private readonly IHubContext<RoomHub> _roomHubContext;
    private readonly IHubContext<SeatHub> _seatHubContext;
    private readonly IHubContext<SpectatorHub> _spectatorHubContext;
    private readonly IHubContext<ConnectionHub> _connectionHubContext;
    private readonly IHubContext<GameControlHub> _gameControlHubContext;
    private readonly IHubContext<LobbyHub> _lobbyHubContext;
    private readonly IConnectionManager _connectionManager;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        IHubContext<GameHub> gameHubContext,
        IHubContext<RoomHub> roomHubContext,
        IHubContext<SeatHub> seatHubContext,
        IHubContext<SpectatorHub> spectatorHubContext,
        IHubContext<ConnectionHub> connectionHubContext,
        IHubContext<GameControlHub> gameControlHubContext,
        IHubContext<LobbyHub> lobbyHubContext,
        IConnectionManager connectionManager,
        ILogger<SignalRNotificationService> logger)
    {
        _gameHubContext = gameHubContext;
        _roomHubContext = roomHubContext;
        _seatHubContext = seatHubContext;
        _spectatorHubContext = spectatorHubContext;
        _connectionHubContext = connectionHubContext;
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

            _logger.LogInformation("[SignalRNotification] === SENDING EVENT TO ALL HUBS ===");
            _logger.LogInformation("[SignalRNotification] Method: {MethodName}, RoomCode: {RoomCode}, Group: {GroupName}",
                methodName, roomCode, groupName);

            // CRÍTICO: Enviar a TODOS los hubs donde pueden estar los usuarios
            var tasks = new List<Task>
            {
                _gameHubContext.Clients.Group(groupName).SendAsync(methodName, data),
                _roomHubContext.Clients.Group(groupName).SendAsync(methodName, data),
                _seatHubContext.Clients.Group(groupName).SendAsync(methodName, data),
                _spectatorHubContext.Clients.Group(groupName).SendAsync(methodName, data),
                _connectionHubContext.Clients.Group(groupName).SendAsync(methodName, data),
                _gameControlHubContext.Clients.Group(groupName).SendAsync(methodName, data)
            };

            await Task.WhenAll(tasks);

            _logger.LogInformation("[SignalRNotification] ✅ Successfully sent {MethodName} to room {RoomCode} via ALL HUBS",
                methodName, roomCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SignalRNotification] ERROR sending {MethodName} to room {RoomCode}: {Error}",
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

            _logger.LogInformation("[SignalRNotification] Sending {MethodName} to room {RoomCode} except connection {ConnectionId} via ALL HUBS",
                methodName, roomCode, excludeConnectionId);

            // CRÍTICO: Enviar a TODOS los hubs excluyendo conexión específica
            var tasks = new List<Task>
            {
                _gameHubContext.Clients.GroupExcept(groupName, excludeConnectionId).SendAsync(methodName, data),
                _roomHubContext.Clients.GroupExcept(groupName, excludeConnectionId).SendAsync(methodName, data),
                _seatHubContext.Clients.GroupExcept(groupName, excludeConnectionId).SendAsync(methodName, data),
                _spectatorHubContext.Clients.GroupExcept(groupName, excludeConnectionId).SendAsync(methodName, data),
                _connectionHubContext.Clients.GroupExcept(groupName, excludeConnectionId).SendAsync(methodName, data),
                _gameControlHubContext.Clients.GroupExcept(groupName, excludeConnectionId).SendAsync(methodName, data)
            };

            await Task.WhenAll(tasks);

            _logger.LogDebug("[SignalRNotification] Sent {MethodName} to room {RoomCode} except {ConnectionId} via ALL HUBS",
                methodName, roomCode, excludeConnectionId);
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

            _logger.LogInformation("[SignalRNotification] Sending {MethodName} to room {RoomCode} except player {PlayerId} ({ConnectionCount} connections) via ALL HUBS",
                methodName, roomCode, excludePlayerId, playerConnections.Count);

            // CRÍTICO: Enviar a TODOS los hubs excluyendo conexiones del jugador
            var tasks = new List<Task>
            {
                _gameHubContext.Clients.GroupExcept(groupName, playerConnections).SendAsync(methodName, data),
                _roomHubContext.Clients.GroupExcept(groupName, playerConnections).SendAsync(methodName, data),
                _seatHubContext.Clients.GroupExcept(groupName, playerConnections).SendAsync(methodName, data),
                _spectatorHubContext.Clients.GroupExcept(groupName, playerConnections).SendAsync(methodName, data),
                _connectionHubContext.Clients.GroupExcept(groupName, playerConnections).SendAsync(methodName, data),
                _gameControlHubContext.Clients.GroupExcept(groupName, playerConnections).SendAsync(methodName, data)
            };

            await Task.WhenAll(tasks);

            _logger.LogDebug("[SignalRNotification] Sent {MethodName} to room {RoomCode} except player {PlayerId} via ALL HUBS",
                methodName, roomCode, excludePlayerId);
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
                _logger.LogInformation("[SignalRNotification] Sending {MethodName} to player {PlayerId} ({ConnectionCount} connections) via ALL HUBS",
                    methodName, playerId, connections.Count);

                // CRÍTICO: Enviar a TODOS los hubs donde puede estar el jugador
                var tasks = new List<Task>
                {
                    _gameHubContext.Clients.Clients(connections).SendAsync(methodName, data),
                    _roomHubContext.Clients.Clients(connections).SendAsync(methodName, data),
                    _seatHubContext.Clients.Clients(connections).SendAsync(methodName, data),
                    _spectatorHubContext.Clients.Clients(connections).SendAsync(methodName, data),
                    _connectionHubContext.Clients.Clients(connections).SendAsync(methodName, data),
                    _gameControlHubContext.Clients.Clients(connections).SendAsync(methodName, data)
                };

                await Task.WhenAll(tasks);

                _logger.LogDebug("[SignalRNotification] Sent {MethodName} to player {PlayerId} via ALL HUBS", methodName, playerId);
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
            _logger.LogInformation("[SignalRNotification] Sending {MethodName} to connection {ConnectionId} via ALL HUBS",
                methodName, connectionId);

            // CRÍTICO: Enviar a TODOS los hubs para asegurar entrega
            var tasks = new List<Task>
            {
                _gameHubContext.Clients.Client(connectionId).SendAsync(methodName, data),
                _roomHubContext.Clients.Client(connectionId).SendAsync(methodName, data),
                _seatHubContext.Clients.Client(connectionId).SendAsync(methodName, data),
                _spectatorHubContext.Clients.Client(connectionId).SendAsync(methodName, data),
                _connectionHubContext.Clients.Client(connectionId).SendAsync(methodName, data),
                _gameControlHubContext.Clients.Client(connectionId).SendAsync(methodName, data)
            };

            await Task.WhenAll(tasks);

            _logger.LogDebug("[SignalRNotification] Sent {MethodName} to connection {ConnectionId} via ALL HUBS",
                methodName, connectionId);
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
            _logger.LogInformation("[SignalRNotification] Broadcasting {MethodName} to ALL clients via ALL HUBS", methodName);

            // CRÍTICO: Broadcast a TODOS los hubs
            var tasks = new List<Task>
            {
                _gameHubContext.Clients.All.SendAsync(methodName, data),
                _roomHubContext.Clients.All.SendAsync(methodName, data),
                _seatHubContext.Clients.All.SendAsync(methodName, data),
                _spectatorHubContext.Clients.All.SendAsync(methodName, data),
                _connectionHubContext.Clients.All.SendAsync(methodName, data),
                _gameControlHubContext.Clients.All.SendAsync(methodName, data),
                _lobbyHubContext.Clients.All.SendAsync(methodName, data)
            };

            await Task.WhenAll(tasks);

            _logger.LogDebug("[SignalRNotification] Broadcast {MethodName} to all clients via ALL HUBS", methodName);
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
            _logger.LogInformation("[SignalRNotification] Sending {MethodName} to group {GroupName} via ALL HUBS",
                methodName, groupName);

            // CRÍTICO: Enviar a TODOS los hubs para el grupo específico
            var tasks = new List<Task>
            {
                _gameHubContext.Clients.Group(groupName).SendAsync(methodName, data),
                _roomHubContext.Clients.Group(groupName).SendAsync(methodName, data),
                _seatHubContext.Clients.Group(groupName).SendAsync(methodName, data),
                _spectatorHubContext.Clients.Group(groupName).SendAsync(methodName, data),
                _connectionHubContext.Clients.Group(groupName).SendAsync(methodName, data),
                _gameControlHubContext.Clients.Group(groupName).SendAsync(methodName, data),
                _lobbyHubContext.Clients.Group(groupName).SendAsync(methodName, data)
            };

            await Task.WhenAll(tasks);

            _logger.LogDebug("[SignalRNotification] Sent {MethodName} to group {GroupName} via ALL HUBS", methodName, groupName);
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
        _logger.LogInformation("[SignalRNotification] === PLAYER JOINED EVENT ===");
        _logger.LogInformation("[SignalRNotification] RoomCode: {RoomCode}, Player: {PlayerName}, Position: {Position}",
            roomCode, eventData.PlayerName, eventData.Position);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.PlayerJoined, eventData);
    }

    public async Task NotifyPlayerLeftAsync(string roomCode, PlayerLeftEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] === PLAYER LEFT EVENT ===");
        _logger.LogInformation("[SignalRNotification] RoomCode: {RoomCode}, Player: {PlayerName}",
            roomCode, eventData.PlayerName);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.PlayerLeft, eventData);
    }

    public async Task NotifySpectatorJoinedAsync(string roomCode, SpectatorModel spectator)
    {
        _logger.LogInformation("[SignalRNotification] === SPECTATOR JOINED EVENT ===");
        _logger.LogInformation("[SignalRNotification] RoomCode: {RoomCode}, Spectator: {SpectatorName}",
            roomCode, spectator.Name);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.SpectatorJoined, spectator);
    }

    public async Task NotifySpectatorLeftAsync(string roomCode, SpectatorModel spectator)
    {
        _logger.LogInformation("[SignalRNotification] === SPECTATOR LEFT EVENT ===");
        _logger.LogInformation("[SignalRNotification] RoomCode: {RoomCode}, Spectator: {SpectatorName}",
            roomCode, spectator.Name);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.SpectatorLeft, spectator);
    }

    public async Task NotifyGameStartedAsync(string roomCode, GameStartedEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] === GAME STARTED EVENT ===");
        _logger.LogInformation("[SignalRNotification] RoomCode: {RoomCode}", roomCode);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.GameStarted, eventData);
    }

    public async Task NotifyGameEndedAsync(string roomCode, GameEndedEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] === GAME ENDED EVENT ===");
        _logger.LogInformation("[SignalRNotification] RoomCode: {RoomCode}", roomCode);

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

    // CRÍTICO: El evento más importante - RoomInfoUpdated
    public async Task NotifyRoomInfoUpdatedAsync(string roomCode, RoomInfoModel roomInfo)
    {
        _logger.LogInformation("[SignalRNotification] === ROOM INFO UPDATED EVENT ===");
        _logger.LogInformation("[SignalRNotification] RoomCode: {RoomCode}, PlayerCount: {PlayerCount}",
            roomCode, roomInfo.PlayerCount);
        _logger.LogInformation("[SignalRNotification] Players: {Players}",
            string.Join(", ", roomInfo.Players.Select(p => $"{p.Name}(Pos:{p.Position})")));

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);
    }

    #endregion

    #region Auto-Betting Events

    /// <summary>
    /// Notifica el resultado completo del procesamiento de apuestas automáticas
    /// </summary>
    public async Task NotifyAutoBetProcessedAsync(string roomCode, AutoBetProcessedEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] === AUTO-BET PROCESSED EVENT ===");
        _logger.LogInformation("[SignalRNotification] RoomCode: {RoomCode}, Successful: {Successful}, Failed: {Failed}, Removed: {Removed}",
            roomCode, eventData.SuccessfulBets, eventData.FailedBets, eventData.PlayersRemovedFromSeats);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.AutoBetProcessed, eventData);
    }

    /// <summary>
    /// Notifica cuando un jugador es removido de su asiento por fondos insuficientes
    /// </summary>
    public async Task NotifyPlayerRemovedFromSeatAsync(string roomCode, PlayerRemovedFromSeatEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] === PLAYER REMOVED FROM SEAT EVENT ===");
        _logger.LogInformation("[SignalRNotification] RoomCode: {RoomCode}, Player: {PlayerName}, Seat: {SeatPosition}, Reason: {Reason}",
            roomCode, eventData.PlayerName, eventData.SeatPosition, eventData.Reason);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.PlayerRemovedFromSeat, eventData);

        // También notificar individualmente al jugador removido
        var playerId = PlayerId.From(eventData.PlayerId);
        await NotifyPlayerAsync(playerId, HubMethodNames.ServerMethods.YouWereRemovedFromSeat, eventData);
    }

    /// <summary>
    /// Notifica actualizaciones de balance de jugadores después de apuestas automáticas
    /// </summary>
    public async Task NotifyPlayerBalanceUpdatedAsync(string roomCode, PlayerBalanceUpdatedEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] === PLAYER BALANCE UPDATED EVENT ===");
        _logger.LogInformation("[SignalRNotification] RoomCode: {RoomCode}, Player: {PlayerName}, Previous: {Previous}, New: {New}, Reason: {Reason}",
            roomCode, eventData.PlayerName, eventData.PreviousBalance, eventData.NewBalance, eventData.ChangeReason);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.PlayerBalanceUpdated, eventData);

        // También notificar individualmente al jugador afectado
        var playerId = PlayerId.From(eventData.PlayerId);
        await NotifyPlayerAsync(playerId, HubMethodNames.ServerMethods.YourBalanceUpdated, eventData);
    }

    /// <summary>
    /// Notifica advertencias de fondos insuficientes antes de remover jugadores
    /// </summary>
    public async Task NotifyInsufficientFundsWarningAsync(string roomCode, InsufficientFundsWarningEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] === INSUFFICIENT FUNDS WARNING EVENT ===");
        _logger.LogInformation("[SignalRNotification] RoomCode: {RoomCode}, Player: {PlayerName}, Balance: {Balance}, Required: {Required}",
            roomCode, eventData.PlayerName, eventData.CurrentBalance, eventData.RequiredAmount);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.InsufficientFundsWarning, eventData);

        // También notificar individualmente al jugador con la advertencia
        var playerId = PlayerId.From(eventData.PlayerId);
        await NotifyPlayerAsync(playerId, HubMethodNames.ServerMethods.InsufficientFundsWarningPersonal, eventData);
    }

    /// <summary>
    /// Notifica estadísticas de auto-betting de una sala
    /// </summary>
    public async Task NotifyAutoBetStatisticsAsync(string roomCode, AutoBetStatisticsEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] === AUTO-BET STATISTICS EVENT ===");
        _logger.LogInformation("[SignalRNotification] RoomCode: {RoomCode}, SeatedPlayers: {SeatedPlayers}, WithFunds: {WithFunds}, WithoutFunds: {WithoutFunds}",
            roomCode, eventData.SeatedPlayersCount, eventData.PlayersWithSufficientFunds, eventData.PlayersWithInsufficientFunds);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.AutoBetStatistics, eventData);
    }

    /// <summary>
    /// Notifica el inicio del procesamiento de apuestas automáticas
    /// </summary>
    public async Task NotifyAutoBetProcessingStartedAsync(string roomCode, AutoBetProcessingStartedEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] === AUTO-BET PROCESSING STARTED EVENT ===");
        _logger.LogInformation("[SignalRNotification] RoomCode: {RoomCode}, SeatedPlayers: {SeatedPlayers}, TotalBet: {TotalBet}",
            roomCode, eventData.SeatedPlayersCount, eventData.TotalBetAmount);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.AutoBetProcessingStarted, eventData);
    }

    /// <summary>
    /// Notifica fallos en el procesamiento de auto-betting
    /// </summary>
    public async Task NotifyAutoBetFailedAsync(string roomCode, AutoBetFailedEventModel eventData)
    {
        _logger.LogError("[SignalRNotification] === AUTO-BET FAILED EVENT ===");
        _logger.LogError("[SignalRNotification] RoomCode: {RoomCode}, Error: {ErrorMessage}, AffectedPlayers: {AffectedPlayers}",
            roomCode, eventData.ErrorMessage, eventData.AffectedPlayersCount);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.AutoBetFailed, eventData);

        // También notificar individualmente a los jugadores afectados
        foreach (var playerId in eventData.AffectedPlayerIds)
        {
            await NotifyPlayerAsync(PlayerId.From(playerId), HubMethodNames.ServerMethods.AutoBetFailedPersonal, eventData);
        }
    }

    /// <summary>
    /// Notifica cambios en la apuesta mínima por ronda
    /// </summary>
    public async Task NotifyMinBetPerRoundUpdatedAsync(string roomCode, MinBetPerRoundUpdatedEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] === MIN BET PER ROUND UPDATED EVENT ===");
        _logger.LogInformation("[SignalRNotification] RoomCode: {RoomCode}, Previous: {Previous}, New: {New}, UpdatedBy: {UpdatedBy}",
            roomCode, eventData.PreviousMinBet, eventData.NewMinBet, eventData.UpdatedByPlayerName);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.MinBetPerRoundUpdated, eventData);
    }

    /// <summary>
    /// Notifica el resumen de una ronda de apuestas automáticas
    /// </summary>
    public async Task NotifyAutoBetRoundSummaryAsync(string roomCode, AutoBetRoundSummaryEventModel eventData)
    {
        _logger.LogInformation("[SignalRNotification] === AUTO-BET ROUND SUMMARY EVENT ===");
        _logger.LogInformation("[SignalRNotification] RoomCode: {RoomCode}, Round: {RoundNumber}, Duration: {Duration}ms",
            roomCode, eventData.RoundNumber, eventData.ProcessingDuration.TotalMilliseconds);

        await NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.AutoBetRoundSummary, eventData);
    }

    #endregion

    #region Notificaciones de lobby

    public async Task NotifyActiveRoomsUpdatedAsync(List<ActiveRoomModel> activeRooms)
    {
        _logger.LogInformation("[SignalRNotification] === ACTIVE ROOMS UPDATED ===");
        _logger.LogInformation("[SignalRNotification] Room count: {RoomCount}", activeRooms.Count);

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