// BlackJack.Realtime/Hubs/GameControlHub.cs - ARCHIVO COMPLETO REFACTORIZADO
using BlackJack.Data.Repositories.Game;
using BlackJack.Domain.Enums;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Realtime.Models;
using BlackJack.Realtime.Services;
using BlackJack.Services.Game;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlackJack.Realtime.Hubs;

[AllowAnonymous]
public class GameControlHub : BaseHub
{
    private readonly IGameRoomService _gameRoomService;
    private readonly IConnectionManager _connectionManager;
    private readonly ISignalRNotificationService _notificationService;
    private readonly IGameService _gameService;
    private readonly ITableRepository _tableRepository;
    private readonly IHandRepository _handRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IDealerService _dealerService;

    public GameControlHub(
        IGameRoomService gameRoomService,
        IConnectionManager connectionManager,
        ISignalRNotificationService notificationService,
        IGameService gameService,
        ITableRepository tableRepository,
        IHandRepository handRepository,
        IPlayerRepository playerRepository,
        IDealerService dealerService,
        ILogger<GameControlHub> logger) : base(logger)
    {
        _gameRoomService = gameRoomService;
        _connectionManager = connectionManager;
        _notificationService = notificationService;
        _gameService = gameService;
        _tableRepository = tableRepository;
        _handRepository = handRepository;
        _playerRepository = playerRepository;
        _dealerService = dealerService;
    }

    #region Connection Management

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        var playerId = GetCurrentPlayerId();
        var userName = GetCurrentUserName();

        if (playerId != null && userName != null)
        {
            await _connectionManager.AddConnectionAsync(Context.ConnectionId, playerId, userName);
            await SendSuccessAsync("Conectado al hub de control de juego");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _connectionManager.RemoveConnectionAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    #endregion

    #region Game Control

    public async Task StartGame(string roomCode)
    {
        try
        {
            var playerId = await ValidateAuthenticationAsync();
            if (playerId == null) return;

            if (!ValidateInput(roomCode, nameof(roomCode)))
            {
                await SendErrorAsync("Código de sala inválido");
                return;
            }

            _logger.LogInformation("[GameControlHub] Player {PlayerId} starting game in room {RoomCode}",
                playerId, roomCode);

            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync(roomResult.Error ?? "Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;

            // Verificar si el juego ya está en progreso
            if (room.Status == RoomStatus.InProgress)
            {
                _logger.LogInformation("[GameControlHub] Game already in progress for room {RoomCode}", roomCode);

                if (room.BlackjackTableId.HasValue)
                {
                    await SendInitialGameState(roomCode, room.BlackjackTableId.Value);
                }

                await SendSuccessAsync("El juego ya está en progreso");
                return;
            }

            // Verificar estado de la mesa
            if (room.BlackjackTableId.HasValue)
            {
                var tableResult = await _gameService.GetTableAsync(room.BlackjackTableId.Value);
                if (tableResult.IsSuccess && tableResult.Value!.Status == GameStatus.InProgress)
                {
                    _logger.LogInformation("[GameControlHub] Table already in progress for room {RoomCode}", roomCode);

                    await SendInitialGameState(roomCode, room.BlackjackTableId.Value);
                    await SendSuccessAsync("El juego ya está en progreso");
                    return;
                }
            }

            // REFACTORIZADO: Usar nuevo DealerService con RoomPlayers
            if (room.BlackjackTableId.HasValue)
            {
                var tableId = room.BlackjackTableId.Value;
                var seatedPlayers = room.Players.Where(p => p.SeatPosition.HasValue).ToList();

                _logger.LogInformation("[GameControlHub] Starting round for {Count} seated players", seatedPlayers.Count);

                if (seatedPlayers.Any())
                {
                    // Iniciar la ronda (sin repartir cartas aún)
                    var startRoundResult = await _gameService.StartRoundAsync(tableId);
                    if (!startRoundResult.IsSuccess)
                    {
                        _logger.LogError("[GameControlHub] StartRoundAsync failed: {Error}", startRoundResult.Error);
                        await SendErrorAsync(startRoundResult.Error);
                        return;
                    }

                    // Obtener la mesa actualizada
                    var table = await _tableRepository.GetByIdAsync(tableId);
                    if (table == null)
                    {
                        _logger.LogError("[GameControlHub] Table not found after StartRoundAsync");
                        await SendErrorAsync("Error obteniendo mesa después de iniciar ronda");
                        return;
                    }

                    // NUEVO: Usar DealerService refactorizado con RoomPlayers
                    await _dealerService.DealInitialCardsAsync(table, seatedPlayers);

                    // Actualizar la mesa con los cambios
                    await _tableRepository.UpdateAsync(table);

                    _logger.LogInformation("[GameControlHub] Initial cards dealt successfully for {Count} players", seatedPlayers.Count);
                }
                else
                {
                    _logger.LogWarning("[GameControlHub] No seated players found, cannot start game");
                    await SendErrorAsync("No hay jugadores sentados para iniciar el juego");
                    return;
                }
            }

            // Marcar sala como iniciada
            var result = await _gameRoomService.StartGameAsync(roomCode, playerId);

            if (result.IsSuccess)
            {
                var updatedRoomResult = await _gameRoomService.GetRoomAsync(roomCode);
                if (updatedRoomResult.IsSuccess)
                {
                    var updatedRoom = updatedRoomResult.Value!;
                    var gameStartedEvent = new GameStartedEventModel(
                        RoomCode: roomCode,
                        GameTableId: updatedRoom.BlackjackTableId ?? Guid.Empty,
                        PlayerNames: updatedRoom.Players.Select(p => p.Name).ToList(),
                        FirstPlayerTurn: updatedRoom.CurrentPlayer?.PlayerId.Value ?? Guid.Empty,
                        Timestamp: DateTime.UtcNow
                    );

                    await _notificationService.NotifyGameStartedAsync(roomCode, gameStartedEvent);

                    if (updatedRoom.BlackjackTableId.HasValue)
                    {
                        await SendInitialGameState(roomCode, updatedRoom.BlackjackTableId.Value);
                    }

                    await SendSuccessAsync("Juego iniciado correctamente", gameStartedEvent);
                    _logger.LogInformation("[GameControlHub] Game started successfully");
                }
            }
            else
            {
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "StartGame");
        }
    }

    public async Task EndGame(string roomCode)
    {
        try
        {
            var playerId = await ValidateAuthenticationAsync();
            if (playerId == null) return;

            if (!ValidateInput(roomCode, nameof(roomCode)))
            {
                await SendErrorAsync("Código de sala inválido");
                return;
            }

            var result = await _gameRoomService.EndGameAsync(roomCode);

            if (result.IsSuccess)
            {
                var gameEndedEvent = new GameEndedEventModel(
                    RoomCode: roomCode,
                    Results: new List<PlayerResultModel>(),
                    DealerHandValue: 0,
                    WinnerId: null,
                    Timestamp: DateTime.UtcNow
                );

                await _notificationService.NotifyGameEndedAsync(roomCode, gameEndedEvent);
                await SendSuccessAsync("Juego terminado correctamente");
            }
            else
            {
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "EndGame");
        }
    }

    #endregion

    #region Player Actions

    public async Task Hit(string roomCode)
    {
        try
        {
            var playerId = await ValidateAuthenticationAsync();
            if (playerId == null) return;

            if (!ValidateInput(roomCode, nameof(roomCode)))
            {
                await SendErrorAsync("Código de sala inválido");
                return;
            }

            _logger.LogInformation("[GameControlHub] Player {PlayerId} hitting in room {RoomCode}",
                playerId, roomCode);

            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync(roomResult.Error ?? "Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;

            if (!room.BlackjackTableId.HasValue)
            {
                await SendErrorAsync("La sala no tiene una mesa de blackjack asociada");
                return;
            }

            var tableId = room.BlackjackTableId.Value;

            if (!room.IsPlayerInRoom(playerId))
            {
                await SendErrorAsync("No estás en esta sala");
                return;
            }

            var actionResult = await _gameService.PlayerActionAsync(tableId, playerId, PlayerAction.Hit);

            if (actionResult.IsSuccess)
            {
                _logger.LogInformation("[GameControlHub] Hit action successful for player {PlayerId}", playerId);
                await SendUpdatedGameState(roomCode, tableId);
                await SendSuccessAsync("Hit ejecutado correctamente");
            }
            else
            {
                _logger.LogError("[GameControlHub] Hit action failed: {Error}", actionResult.Error);
                await SendErrorAsync(actionResult.Error ?? "Error ejecutando Hit");
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "Hit");
        }
    }

    public async Task Stand(string roomCode)
    {
        try
        {
            var playerId = await ValidateAuthenticationAsync();
            if (playerId == null) return;

            if (!ValidateInput(roomCode, nameof(roomCode)))
            {
                await SendErrorAsync("Código de sala inválido");
                return;
            }

            _logger.LogInformation("[GameControlHub] Player {PlayerId} standing in room {RoomCode}",
                playerId, roomCode);

            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync(roomResult.Error ?? "Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;

            if (!room.BlackjackTableId.HasValue)
            {
                await SendErrorAsync("La sala no tiene una mesa de blackjack asociada");
                return;
            }

            var tableId = room.BlackjackTableId.Value;

            if (!room.IsPlayerInRoom(playerId))
            {
                await SendErrorAsync("No estás en esta sala");
                return;
            }

            var actionResult = await _gameService.PlayerActionAsync(tableId, playerId, PlayerAction.Stand);

            if (actionResult.IsSuccess)
            {
                _logger.LogInformation("[GameControlHub] Stand action successful for player {PlayerId}", playerId);
                await SendUpdatedGameState(roomCode, tableId);
                await SendSuccessAsync("Stand ejecutado correctamente");
            }
            else
            {
                _logger.LogError("[GameControlHub] Stand action failed: {Error}", actionResult.Error);
                await SendErrorAsync(actionResult.Error ?? "Error ejecutando Stand");
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "Stand");
        }
    }

    private async Task SendUpdatedGameState(string roomCode, Guid tableId)
    {
        try
        {
            var table = await _tableRepository.GetByIdAsync(tableId);
            if (table == null)
            {
                _logger.LogWarning("[GameControlHub] Cannot send updated game state - table not found");
                return;
            }

            var gameState = await BuildGameStatePayload(roomCode, table);

            _logger.LogInformation("[GameControlHub] Sending updated game state to room {RoomCode}", roomCode);
            await _notificationService.NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.GameStateUpdated, gameState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameControlHub] Error sending updated game state: {Error}", ex.Message);
        }
    }

    #endregion

    #region Private Methods

    private async Task SendInitialGameState(string roomCode, Guid tableId)
    {
        try
        {
            var table = await _tableRepository.GetByIdAsync(tableId);
            if (table == null) return;

            var gameState = await BuildGameStatePayload(roomCode, table);

            _logger.LogInformation("[GameControlHub] Sending initial game state to room {RoomCode}", roomCode);
            await _notificationService.NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.GameStateUpdated, gameState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameControlHub] Error sending initial game state: {Error}", ex.Message);
        }
    }

    private async Task SendInitialGameStateToConnection(string connectionId, string roomCode, Guid tableId)
    {
        try
        {
            var table = await _tableRepository.GetByIdAsync(tableId);
            if (table == null) return;

            var gameState = await BuildGameStatePayload(roomCode, table);

            _logger.LogInformation("[GameControlHub] Sending game state to connection {ConnectionId}", connectionId);

            await Clients.Client(connectionId).SendAsync(HubMethodNames.ServerMethods.GameStateUpdated, gameState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameControlHub] Error sending game state to connection: {Error}", ex.Message);
        }
    }

    private async Task<object> BuildGameStatePayload(string roomCode, BlackjackTable table)
    {
        _logger.LogInformation("[GameControlHub] Building game state payload for room {RoomCode}", roomCode);

        // Obtener la sala con sus jugadores
        var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
        if (!roomResult.IsSuccess || roomResult.Value == null)
        {
            _logger.LogError("[GameControlHub] Room not found for {RoomCode}", roomCode);
            return new { roomCode, status = "Error", players = new List<object>() };
        }

        var room = roomResult.Value;

        // Construir dealer payload
        object? dealerPayload = null;
        if (table.DealerHandId.HasValue)
        {
            var dealerHand = await _handRepository.GetByIdAsync(table.DealerHandId.Value);
            if (dealerHand != null)
            {
                dealerPayload = new
                {
                    handId = dealerHand.Id,
                    cards = dealerHand.Cards.Select(c => new {
                        suit = c.Suit.ToString(),
                        rank = c.Rank.ToString()
                    }).ToList(),
                    value = dealerHand.Value,
                    status = dealerHand.Status.ToString()
                };
            }
        }

        // REFACTORIZADO: Usar RoomPlayers en lugar de Seats
        var playersPayload = new List<object>();
        var seatedPlayers = room.Players.Where(p => p.SeatPosition.HasValue).ToList();

        _logger.LogInformation("[GameControlHub] Processing {Count} seated players", seatedPlayers.Count);

        foreach (var roomPlayer in seatedPlayers)
        {
            _logger.LogInformation("[GameControlHub] Processing player {Name} at seat {Seat}",
                roomPlayer.Name, roomPlayer.SeatPosition);

            // Obtener el Player entity
            Player? player = null;
            if (roomPlayer.PlayerEntityId != Guid.Empty)
            {
                player = await _playerRepository.GetByIdAsync(roomPlayer.PlayerEntityId);
            }

            if (player == null)
            {
                // Si no hay Player entity, intentar obtenerlo por PlayerId
                player = await _playerRepository.GetByPlayerIdAsync(roomPlayer.PlayerId);
            }

            if (player == null)
            {
                _logger.LogWarning("[GameControlHub] Player entity not found for {Name}", roomPlayer.Name);
                continue;
            }

            // Construir hand payload
            object? handPayload = null;
            if (player.HandIds.Any())
            {
                var firstHandId = player.HandIds.First();
                var hand = await _handRepository.GetByIdAsync(firstHandId);
                if (hand != null)
                {
                    handPayload = new
                    {
                        handId = hand.Id,
                        cards = hand.Cards.Select(c => new {
                            suit = c.Suit.ToString(),
                            rank = c.Rank.ToString()
                        }).ToList(),
                        value = hand.Value,
                        status = hand.Status.ToString()
                    };
                }
            }

            playersPayload.Add(new
            {
                playerId = roomPlayer.PlayerId.Value,
                name = roomPlayer.Name,
                seat = roomPlayer.SeatPosition!.Value,
                hand = handPayload
            });
        }

        _logger.LogInformation("[GameControlHub] Built game state with {Count} players", playersPayload.Count);

        return new
        {
            roomCode = roomCode,
            status = table.Status.ToString(),
            dealerHand = dealerPayload,
            players = playersPayload
        };
    }

    #endregion

    public async Task JoinRoomForGameControl(string roomCode)
    {
        try
        {
            var playerId = await ValidateAuthenticationAsync();
            if (playerId == null) return;

            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync("Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;
            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(roomCode);

            await Groups.AddToGroupAsync(Context.ConnectionId, roomGroupName);
            await _connectionManager.AddToGroupAsync(Context.ConnectionId, roomGroupName);

            if (room.BlackjackTableId.HasValue)
            {
                var tableGroupName = HubMethodNames.Groups.GetTableGroup(room.BlackjackTableId.Value.ToString());
                await Groups.AddToGroupAsync(Context.ConnectionId, tableGroupName);
                await _connectionManager.AddToGroupAsync(Context.ConnectionId, tableGroupName);
            }

            await SendSuccessAsync($"Conectado al control de juego para sala {roomCode}");

            // Enviar estado actual si el juego está en progreso
            if (room.BlackjackTableId.HasValue && room.Status == RoomStatus.InProgress)
            {
                await SendInitialGameStateToConnection(Context.ConnectionId, roomCode, room.BlackjackTableId.Value);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "JoinRoomForGameControl");
        }
    }

    public async Task GetAutoBetStatistics(string roomCode)
    {
        try
        {
            var playerId = await ValidateAuthenticationAsync();
            if (playerId == null) return;

            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync("Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;
            var seatedPlayers = room.Players.Where(p => p.SeatPosition.HasValue).ToList();

            var stats = new
            {
                roomCode = roomCode,
                minBetPerRound = 20m,
                seatedPlayersCount = seatedPlayers.Count,
                totalBetPerRound = 0m,
                playersWithSufficientFunds = 0,
                playerDetails = new List<object>()
            };

            await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.AutoBetStatistics, stats);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "GetAutoBetStatistics");
        }
    }

    #region Test Methods

    [AllowAnonymous]
    public async Task TestConnection()
    {
        var response = new
        {
            message = "GameControlHub funcionando",
            timestamp = DateTime.UtcNow,
            connectionId = Context.ConnectionId,
            playerId = GetCurrentPlayerId()?.Value,
            capabilities = new[] { "StartGame", "EndGame", "ProcessAutoBets", "PlayerActions" }
        };

        await Clients.Caller.SendAsync("TestResponse", response);
    }

    #endregion
}