// BlackJack.Realtime/Hubs/GameControlHub.cs - FUSIONADO: Development + Cartas
//development

using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Realtime.Models;
using BlackJack.Services.Game;
using BlackJack.Realtime.Services;
using BlackJack.Data.Repositories.Game;
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
    // AGREGADO: Dependencias para cartas (del documento 5)
    private readonly IGameService _gameService;
    private readonly ITableRepository _tableRepository;
    private readonly IHandRepository _handRepository;

    public GameControlHub(
        IGameRoomService gameRoomService,
        IConnectionManager connectionManager,
        ISignalRNotificationService notificationService,
        // AGREGADO: Inyección de dependencias para cartas
        IGameService gameService,
        ITableRepository tableRepository,
        IHandRepository handRepository,
        ILogger<GameControlHub> logger) : base(logger)
    {
        _gameRoomService = gameRoomService;
        _connectionManager = connectionManager;
        _notificationService = notificationService;
        // AGREGADO: Asignación de dependencias para cartas
        _gameService = gameService;
        _tableRepository = tableRepository;
        _handRepository = handRepository;
    }

    #region Game Control

    public async Task StartGame(string roomCode)
    {
        try
        {
            _logger.LogInformation("[GameControlHub] ===== StartGame STARTED =====");
            _logger.LogInformation("[GameControlHub] RoomCode: {RoomCode}", roomCode);

            if (!IsAuthenticated())
            {
                await SendErrorAsync("Debes estar autenticado");
                return;
            }

            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

            if (!ValidateInput(roomCode, nameof(roomCode)))
            {
                await SendErrorAsync("Código de sala inválido");
                return;
            }

            _logger.LogInformation("[GameControlHub] Player {PlayerId} starting game in room {RoomCode}",
                playerId, roomCode);

            // FUSIONADO: Lógica mejorada del documento 5 para manejar cartas
            // 1. Obtener la sala y verificar el estado
            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync(roomResult.Error ?? "Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;

            // 2. Si hay mesa de blackjack, sentar jugadores y iniciar ronda de cartas
            if (room.BlackjackTableId.HasValue)
            {
                var tableId = room.BlackjackTableId.Value;

                int seatedCount = 0;
                foreach (var rp in room.Players)
                {
                    if (rp.SeatPosition.HasValue && rp.SeatPosition.Value >= 0 && rp.SeatPosition.Value <= 5)
                    {
                        try
                        {
                            _logger.LogInformation("[GameControlHub] Ensuring player {PlayerId} is seated at table {TableId} seat {Seat}", 
                                rp.PlayerId, tableId, rp.SeatPosition.Value);
                            var joinRes = await _gameService.JoinTableAsync(tableId, rp.PlayerId, rp.SeatPosition.Value);
                            if (joinRes.IsSuccess) seatedCount++;
                            else _logger.LogWarning("[GameControlHub] JoinTableAsync warning for player {PlayerId}: {Error}", 
                                rp.PlayerId, joinRes.Error);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[GameControlHub] JoinTableAsync threw for player {PlayerId}", rp.PlayerId);
                        }
                    }
                }

                // Fallback: si nadie tiene asiento válido, sentar al caller en asiento 0
                if (seatedCount == 0)
                {
                    try
                    {
                        _logger.LogInformation("[GameControlHub] Fallback seat host {PlayerId} at seat 0 for table {TableId}", 
                            playerId, tableId);
                        await _gameService.JoinTableAsync(tableId, playerId, 0);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[GameControlHub] Fallback seating failed for host {PlayerId}", playerId);
                    }
                }

                // Iniciar la ronda de blackjack (idempotente)
                _logger.LogInformation("[GameControlHub] Triggering StartRoundAsync for table {TableId}", tableId);
                var startRoundResult = await _gameService.StartRoundAsync(tableId);
                if (!startRoundResult.IsSuccess)
                {
                    // Si el error es por 0 asientos pero status ya es InProgress, tratar como OK
                    var tableStatusOk = startRoundResult.Error?.Contains("al menos 1 jugador") == true;
                    if (!tableStatusOk)
                    {
                        _logger.LogWarning("[GameControlHub] StartRoundAsync failed: {Error}", startRoundResult.Error);
                        await SendErrorAsync(startRoundResult.Error ?? "No se pudo iniciar la ronda");
                        return;
                    }
                }
            }

            // 3. Marcar sala como iniciada (para lobby/turnos) - DESARROLLO ORIGINAL
            var result = await _gameRoomService.StartGameAsync(roomCode, playerId);

            if (result.IsSuccess)
            {
                _logger.LogInformation("[GameControlHub] StartGame SUCCESS - Getting updated room info...");

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

                    _logger.LogInformation("[GameControlHub] Broadcasting GameStarted via NotificationService...");
                    await _notificationService.NotifyGameStartedAsync(roomCode, gameStartedEvent);

                    // FUSIONADO: Enviar estado inicial con cartas (del documento 5)
                    if (updatedRoom.BlackjackTableId.HasValue)
                    {
                        var tableAfterStart = await _tableRepository.GetTableWithPlayersAsync(updatedRoom.BlackjackTableId.Value);
                        if (tableAfterStart != null)
                        {
                            object? dealerPayload = null;
                            if (tableAfterStart.DealerHandId.HasValue)
                            {
                                var dealerHand = await _handRepository.GetByIdAsync(tableAfterStart.DealerHandId.Value);
                                if (dealerHand != null)
                                {
                                    dealerPayload = new
                                    {
                                        handId = dealerHand.Id,
                                        cards = dealerHand.Cards.Select(c => new { suit = c.Suit.ToString(), rank = c.Rank.ToString() }).ToList(),
                                        value = dealerHand.Value,
                                        status = dealerHand.Status.ToString()
                                    };
                                }
                            }

                            var playersPayload = new List<object>();
                            foreach (var seat in tableAfterStart.Seats.Where(s => s.IsOccupied && s.Player != null))
                            {
                                var player = seat.Player!;
                                Guid? firstHandId = player.HandIds.FirstOrDefault();
                                object? handPayload = null;
                                if (firstHandId.HasValue)
                                {
                                    var hand = await _handRepository.GetByIdAsync(firstHandId.Value);
                                    if (hand != null)
                                    {
                                        handPayload = new
                                        {
                                            handId = hand.Id,
                                            cards = hand.Cards.Select(c => new { suit = c.Suit.ToString(), rank = c.Rank.ToString() }).ToList(),
                                            value = hand.Value,
                                            status = hand.Status.ToString()
                                        };
                                    }
                                }

                                playersPayload.Add(new
                                {
                                    playerId = player.PlayerId.Value,
                                    name = player.Name,
                                    seat = seat.Position,
                                    hand = handPayload
                                });
                            }

                            var gameState = new
                            {
                                roomCode = roomCode,
                                status = "InProgress",
                                dealerHand = dealerPayload,
                                players = playersPayload
                            };

                            _logger.LogInformation("[GameControlHub] Sending GameStateUpdated with {PlayerCount} players", playersPayload.Count);
                            await _notificationService.NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.GameStateUpdated, gameState);
                        }
                    }

                    await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.Success,
                        new { Message = "Juego iniciado correctamente", GameInfo = gameStartedEvent });

                    _logger.LogInformation("[GameControlHub] Game started successfully by player {PlayerId}", playerId);
                }
                else
                {
                    _logger.LogError("[GameControlHub] Failed to get updated room info after game start: {Error}",
                        updatedRoomResult.Error);
                    await SendErrorAsync("Error obteniendo información actualizada del juego");
                }
            }
            else
            {
                _logger.LogWarning("[GameControlHub] StartGame FAILED for player {PlayerId}: {Error}",
                    playerId, result.Error);
                await SendErrorAsync(result.Error);
            }

            _logger.LogInformation("[GameControlHub] ===== StartGame COMPLETED =====");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameControlHub] CRITICAL EXCEPTION in StartGame for player {PlayerId}",
                GetCurrentPlayerId());
            await HandleExceptionAsync(ex, "StartGame");
        }
    }

    public async Task EndGame(string roomCode)
    {
        try
        {
            _logger.LogInformation("[GameControlHub] ===== EndGame STARTED =====");
            _logger.LogInformation("[GameControlHub] RoomCode: {RoomCode}", roomCode);

            if (!IsAuthenticated())
            {
                await SendErrorAsync("Debes estar autenticado");
                return;
            }

            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

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
                await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.Success,
                    new { Message = "Juego terminado correctamente" });

                _logger.LogInformation("[GameControlHub] Game ended successfully");
            }
            else
            {
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameControlHub] CRITICAL EXCEPTION in EndGame");
            await HandleExceptionAsync(ex, "EndGame");
        }
    }

    #endregion

    #region Auto-Betting - MANTENIDO INTACTO DE DEVELOPMENT

    public async Task ProcessRoundAutoBets(string roomCode, bool removePlayersWithoutFunds = true)
    {
        try
        {
            _logger.LogInformation("[GameControlHub] ===== ProcessRoundAutoBets STARTED =====");
            _logger.LogInformation("[GameControlHub] RoomCode: {RoomCode}, RemoveWithoutFunds: {RemoveFlag}",
                roomCode, removePlayersWithoutFunds);

            if (!IsAuthenticated())
            {
                await SendErrorAsync("Debes estar autenticado");
                return;
            }

            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

            if (!ValidateInput(roomCode, nameof(roomCode)))
            {
                await SendErrorAsync("Código de sala inválido");
                return;
            }

            _logger.LogInformation("[GameControlHub] Player {PlayerId} processing auto-bets for room {RoomCode}",
                playerId, roomCode);

            var processingStartedEvent = new AutoBetProcessingStartedEventModel(
                RoomCode: roomCode,
                SeatedPlayersCount: 0,
                MinBetPerRound: 0,
                TotalBetAmount: 0,
                StartedAt: DateTime.UtcNow
            );

            var statsResult = await _gameRoomService.CalculateAutoBetStatisticsAsync(roomCode);
            if (statsResult.IsSuccess)
            {
                var stats = statsResult.Value!;
                processingStartedEvent = processingStartedEvent with
                {
                    SeatedPlayersCount = stats.SeatedPlayersCount,
                    MinBetPerRound = stats.MinBetPerRound.Amount,
                    TotalBetAmount = stats.TotalBetPerRound.Amount
                };

                await _notificationService.NotifyAutoBetProcessingStartedAsync(roomCode, processingStartedEvent);
            }

            var result = await _gameRoomService.ProcessRoundAutoBetsAsync(roomCode, removePlayersWithoutFunds);

            if (result.IsSuccess)
            {
                var autoBetResult = result.Value!;

                _logger.LogInformation("[GameControlHub] ProcessRoundAutoBets SUCCESS - Processing notifications...");

                var autoBetEventModel = new AutoBetProcessedEventModel(
                    RoomCode: autoBetResult.RoomCode,
                    TotalPlayersProcessed: autoBetResult.TotalPlayersProcessed,
                    SuccessfulBets: autoBetResult.SuccessfulBets,
                    FailedBets: autoBetResult.FailedBets,
                    PlayersRemovedFromSeats: autoBetResult.PlayersRemovedFromSeats,
                    TotalAmountProcessed: autoBetResult.TotalAmountProcessed.Amount,
                    PlayerResults: autoBetResult.PlayerResults.Select(pr => new AutoBetPlayerResultModel(
                        PlayerId: pr.PlayerId.Value,
                        PlayerName: pr.PlayerName,
                        SeatPosition: pr.SeatPosition,
                        Status: pr.Status.ToString(),
                        OriginalBalance: pr.OriginalBalance.Amount,
                        NewBalance: pr.NewBalance.Amount,
                        BetAmount: pr.BetAmount.Amount,
                        ErrorMessage: pr.ErrorMessage
                    )).ToList(),
                    ProcessedAt: autoBetResult.ProcessedAt,
                    HasErrors: autoBetResult.HasErrors,
                    SuccessRate: autoBetResult.SuccessRate
                );

                await _notificationService.NotifyAutoBetProcessedAsync(roomCode, autoBetEventModel);

                // Notificar eventos específicos por jugador
                foreach (var playerResult in autoBetResult.PlayerResults)
                {
                    if (playerResult.Status == BetStatus.BetDeducted)
                    {
                        var balanceUpdateEvent = new PlayerBalanceUpdatedEventModel(
                            RoomCode: roomCode,
                            PlayerId: playerResult.PlayerId.Value,
                            PlayerName: playerResult.PlayerName,
                            PreviousBalance: playerResult.OriginalBalance.Amount,
                            NewBalance: playerResult.NewBalance.Amount,
                            AmountChanged: -playerResult.BetAmount.Amount,
                            ChangeReason: "AutoBet",
                            UpdatedAt: DateTime.UtcNow
                        );

                        await _notificationService.NotifyPlayerBalanceUpdatedAsync(roomCode, balanceUpdateEvent);
                    }

                    if (playerResult.Status == BetStatus.RemovedFromSeat)
                    {
                        var removedFromSeatEvent = new PlayerRemovedFromSeatEventModel(
                            RoomCode: roomCode,
                            PlayerId: playerResult.PlayerId.Value,
                            PlayerName: playerResult.PlayerName,
                            SeatPosition: playerResult.SeatPosition,
                            RequiredAmount: playerResult.BetAmount.Amount,
                            AvailableBalance: playerResult.OriginalBalance.Amount,
                            Reason: "Fondos insuficientes para apuesta automática",
                            RemovedAt: DateTime.UtcNow
                        );

                        await _notificationService.NotifyPlayerRemovedFromSeatAsync(roomCode, removedFromSeatEvent);
                    }

                    if (playerResult.Status == BetStatus.InsufficientFunds)
                    {
                        var warningEvent = new InsufficientFundsWarningEventModel(
                            RoomCode: roomCode,
                            PlayerId: playerResult.PlayerId.Value,
                            PlayerName: playerResult.PlayerName,
                            CurrentBalance: playerResult.OriginalBalance.Amount,
                            RequiredAmount: playerResult.BetAmount.Amount,
                            DeficitAmount: playerResult.BetAmount.Amount - playerResult.OriginalBalance.Amount,
                            RoundsRemaining: 0,
                            WillBeRemovedNextRound: removePlayersWithoutFunds,
                            WarningTime: DateTime.UtcNow
                        );

                        await _notificationService.NotifyInsufficientFundsWarningAsync(roomCode, warningEvent);
                    }
                }

                var roundSummaryEvent = new AutoBetRoundSummaryEventModel(
                    RoomCode: roomCode,
                    RoundNumber: 1,
                    RoundStartedAt: processingStartedEvent.StartedAt,
                    RoundCompletedAt: DateTime.UtcNow,
                    ProcessingDuration: DateTime.UtcNow - processingStartedEvent.StartedAt,
                    Results: autoBetEventModel,
                    Notifications: new List<string>
                    {
                        $"Procesadas {autoBetResult.SuccessfulBets} apuestas exitosas",
                        $"Total procesado: ${autoBetResult.TotalAmountProcessed.Amount:F2}",
                        autoBetResult.PlayersRemovedFromSeats > 0
                            ? $"{autoBetResult.PlayersRemovedFromSeats} jugadores removidos por fondos insuficientes"
                            : null
                    }.Where(n => n != null).Cast<string>().ToList()
                );

                await _notificationService.NotifyAutoBetRoundSummaryAsync(roomCode, roundSummaryEvent);

                await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.Success, new
                {
                    Message = $"Apuestas automáticas procesadas: {autoBetResult.SuccessfulBets} exitosas, {autoBetResult.FailedBets} fallidas",
                    Results = autoBetEventModel
                });

                _logger.LogInformation("[GameControlHub] Auto-betting processed successfully: {Success}/{Total} bets",
                    autoBetResult.SuccessfulBets, autoBetResult.TotalPlayersProcessed);
            }
            else
            {
                _logger.LogWarning("[GameControlHub] ProcessRoundAutoBets FAILED: {Error}", result.Error);

                var failedEvent = new AutoBetFailedEventModel(
                    RoomCode: roomCode,
                    ErrorMessage: result.Error,
                    ErrorCode: "AUTO_BET_PROCESSING_FAILED",
                    AffectedPlayersCount: 0,
                    AffectedPlayerIds: new List<Guid>(),
                    FailedAt: DateTime.UtcNow,
                    RequiresManualIntervention: true
                );

                await _notificationService.NotifyAutoBetFailedAsync(roomCode, failedEvent);
                await SendErrorAsync(result.Error);
            }

            _logger.LogInformation("[GameControlHub] ===== ProcessRoundAutoBets COMPLETED =====");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameControlHub] CRITICAL EXCEPTION in ProcessRoundAutoBets for room {RoomCode}",
                roomCode);

            var criticalFailedEvent = new AutoBetFailedEventModel(
                RoomCode: roomCode,
                ErrorMessage: "Error crítico en el procesamiento de apuestas automáticas",
                ErrorCode: "CRITICAL_AUTO_BET_ERROR",
                AffectedPlayersCount: 0,
                AffectedPlayerIds: new List<Guid>(),
                FailedAt: DateTime.UtcNow,
                RequiresManualIntervention: true
            );

            await _notificationService.NotifyAutoBetFailedAsync(roomCode, criticalFailedEvent);
            await HandleExceptionAsync(ex, "ProcessRoundAutoBets");
        }
    }

    public async Task GetAutoBetStatistics(string roomCode)
    {
        try
        {
            _logger.LogInformation("[GameControlHub] ===== GetAutoBetStatistics STARTED =====");
            _logger.LogInformation("[GameControlHub] RoomCode: {RoomCode}", roomCode);

            if (!IsAuthenticated())
            {
                await SendErrorAsync("Debes estar autenticado");
                return;
            }

            if (!ValidateInput(roomCode, nameof(roomCode)))
            {
                await SendErrorAsync("Código de sala inválido");
                return;
            }

            var result = await _gameRoomService.CalculateAutoBetStatisticsAsync(roomCode);

            if (result.IsSuccess)
            {
                var stats = result.Value!;

                var statsEventModel = new AutoBetStatisticsEventModel(
                    RoomCode: stats.RoomCode,
                    MinBetPerRound: stats.MinBetPerRound.Amount,
                    SeatedPlayersCount: stats.SeatedPlayersCount,
                    TotalBetPerRound: stats.TotalBetPerRound.Amount,
                    PlayersWithSufficientFunds: stats.PlayersWithSufficientFunds,
                    PlayersWithInsufficientFunds: stats.PlayersWithInsufficientFunds,
                    TotalAvailableFunds: stats.TotalAvailableFunds.Amount,
                    ExpectedSuccessfulBets: stats.ExpectedSuccessfulBets,
                    ExpectedTotalDeduction: stats.ExpectedTotalDeduction.Amount,
                    PlayerDetails: stats.PlayerDetails.Select(pd => new PlayerAutoBetDetailModel(
                        PlayerId: pd.PlayerId.Value,
                        PlayerName: pd.PlayerName,
                        SeatPosition: 0,
                        CurrentBalance: pd.CurrentBalance.Amount,
                        CanAffordBet: pd.CanAffordBet,
                        BalanceAfterBet: pd.BalanceAfterBet.Amount,
                        RoundsAffordable: pd.CanAffordBet ?
                            (int)(pd.CurrentBalance.Amount / stats.MinBetPerRound.Amount) : 0
                    )).ToList(),
                    CalculatedAt: stats.CalculatedAt
                );

                await _notificationService.NotifyAutoBetStatisticsAsync(roomCode, statsEventModel);
                await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.Success, new
                {
                    Message = "Estadísticas de auto-betting obtenidas",
                    Statistics = statsEventModel
                });

                _logger.LogInformation("[GameControlHub] Auto-bet statistics retrieved successfully");
            }
            else
            {
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameControlHub] CRITICAL EXCEPTION in GetAutoBetStatistics");
            await HandleExceptionAsync(ex, "GetAutoBetStatistics");
        }
    }

    #endregion

    #region Room Group Management

    public async Task JoinRoomForGameControl(string roomCode)
    {
        try
        {
            _logger.LogInformation("[GameControlHub] === JoinRoomForGameControl STARTED ===");
            _logger.LogInformation("[GameControlHub] RoomCode: {RoomCode}", roomCode);

            if (!IsAuthenticated())
            {
                await SendErrorAsync("Debes estar autenticado");
                return;
            }

            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

            if (!ValidateInput(roomCode, nameof(roomCode)))
            {
                await SendErrorAsync("Código de sala inválido");
                return;
            }

            // Verificar que la sala existe
            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync("Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;

            // Unirse a grupos necesarios para control de juego
            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(roomCode);
            await JoinGroupAsync(roomGroupName);
            await _connectionManager.AddToGroupAsync(Context.ConnectionId, roomGroupName);

            if (room.BlackjackTableId.HasValue)
            {
                var tableGroupName = $"Table_{room.BlackjackTableId.Value}";
                await JoinGroupAsync(tableGroupName);
                await _connectionManager.AddToGroupAsync(Context.ConnectionId, tableGroupName);
                _logger.LogInformation("[GameControlHub] Also joined table group: {TableGroupName}", tableGroupName);
            }

            await _notificationService.SendSuccessToConnectionAsync(Context.ConnectionId,
                $"Conectado al control de juego para sala {roomCode}");

            _logger.LogInformation("[GameControlHub] Player {PlayerId} joined room {RoomCode} for game control",
                playerId, roomCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameControlHub] EXCEPTION in JoinRoomForGameControl");
            await HandleExceptionAsync(ex, "JoinRoomForGameControl");
        }
    }

    public async Task LeaveRoomGameControl(string roomCode)
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

            if (!ValidateInput(roomCode, nameof(roomCode)))
            {
                await SendErrorAsync("Código de sala inválido");
                return;
            }

            _logger.LogInformation("[GameControlHub] Player {PlayerId} leaving game control for room {RoomCode}",
                playerId, roomCode);

            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            string? tableGroupName = null;
            if (roomResult.IsSuccess && roomResult.Value!.BlackjackTableId.HasValue)
            {
                tableGroupName = $"Table_{roomResult.Value.BlackjackTableId.Value}";
            }

            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(roomCode);
            await LeaveGroupAsync(roomGroupName);
            await _connectionManager.RemoveFromGroupAsync(Context.ConnectionId, roomGroupName);

            if (tableGroupName != null)
            {
                await LeaveGroupAsync(tableGroupName);
                await _connectionManager.RemoveFromGroupAsync(Context.ConnectionId, tableGroupName);
            }

            await _notificationService.SendSuccessToConnectionAsync(Context.ConnectionId,
                $"Desconectado del control de juego para sala {roomCode}");

            _logger.LogInformation("[GameControlHub] Player {PlayerId} left game control for room {RoomCode}",
                playerId, roomCode);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "LeaveRoomGameControl");
        }
    }

    #endregion

    #region Test Methods

    [AllowAnonymous]
    public async Task TestConnection()
    {
        _logger.LogInformation("[GameControlHub] TestConnection called");
        var response = new
        {
            message = "SignalR funcionando - GameControlHub avanzado",
            timestamp = DateTime.UtcNow,
            connectionId = Context.ConnectionId,
            playerId = GetCurrentPlayerId()?.Value,
            capabilities = new[] { "StartGame", "EndGame", "ProcessAutoBets", "GetAutoBetStats" }
        };

        await _notificationService.NotifyConnectionAsync(Context.ConnectionId, "TestResponse", response);
    }

    #endregion
}