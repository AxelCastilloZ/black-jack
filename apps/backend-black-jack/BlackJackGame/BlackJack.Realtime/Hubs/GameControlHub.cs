// BlackJack.Realtime/Hubs/GameControlHub.cs - Control de juego y auto-betting
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
    private readonly IGameService _gameService;
    private readonly ITableRepository _tableRepository;
    private readonly IHandRepository _handRepository;

    public GameControlHub(
        IGameRoomService gameRoomService,
        IConnectionManager connectionManager,
        ISignalRNotificationService notificationService,
        IGameService gameService,
        ITableRepository tableRepository,
        IHandRepository handRepository,
        ILogger<GameControlHub> logger) : base(logger)
    {
        _gameRoomService = gameRoomService;
        _connectionManager = connectionManager;
        _notificationService = notificationService;
        _gameService = gameService;
        _tableRepository = tableRepository;
        _handRepository = handRepository;
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

            // Obtener la sala y verificar el estado
            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync(roomResult.Error ?? "Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;

            // Si hay mesa de blackjack, inicializar cartas
            if (room.BlackjackTableId.HasValue)
            {
                var tableId = room.BlackjackTableId.Value;

                // Sentar jugadores en la mesa
                foreach (var rp in room.Players.Where(p => p.SeatPosition.HasValue))
                {
                    try
                    {
                        await _gameService.JoinTableAsync(tableId, rp.PlayerId, rp.SeatPosition!.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[GameControlHub] Error seating player {PlayerId}", rp.PlayerId);
                    }
                }

                // Iniciar la ronda de blackjack
                var startRoundResult = await _gameService.StartRoundAsync(tableId);
                if (!startRoundResult.IsSuccess)
                {
                    _logger.LogWarning("[GameControlHub] StartRoundAsync failed: {Error}", startRoundResult.Error);
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

                    // Enviar estado inicial del juego con cartas
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

    #region Auto-Betting

    public async Task ProcessRoundAutoBets(string roomCode, bool removePlayersWithoutFunds = true)
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

            _logger.LogInformation("[GameControlHub] Player {PlayerId} processing auto-bets for room {RoomCode}",
                playerId, roomCode);

            // Notificar inicio del procesamiento
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

            // Procesar auto-bets
            var result = await _gameRoomService.ProcessRoundAutoBetsAsync(roomCode, removePlayersWithoutFunds);

            if (result.IsSuccess)
            {
                var autoBetResult = result.Value!;

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
                }

                await SendSuccessAsync($"Auto-bets procesadas: {autoBetResult.SuccessfulBets} exitosas", autoBetEventModel);
            }
            else
            {
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
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "ProcessRoundAutoBets");
        }
    }

    public async Task GetAutoBetStatistics(string roomCode)
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
                await SendSuccessAsync("Estadísticas obtenidas", statsEventModel);
            }
            else
            {
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "GetAutoBetStatistics");
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

            // TODO: Implementar lógica de Hit en GameService
            await SendSuccessAsync("Hit realizado");
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

            // TODO: Implementar lógica de Stand en GameService
            await SendSuccessAsync("Stand realizado");
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "Stand");
        }
    }

    public async Task DoubleDown(string roomCode)
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

            // TODO: Implementar lógica de DoubleDown en GameService
            await SendSuccessAsync("Double Down realizado");
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "DoubleDown");
        }
    }

    public async Task Split(string roomCode)
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

            // TODO: Implementar lógica de Split en GameService
            await SendSuccessAsync("Split realizado");
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "Split");
        }
    }

    #endregion

    #region Room Group Management

    public async Task JoinRoomForGameControl(string roomCode)
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

            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync("Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;
            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(roomCode);

            await JoinGroupAsync(roomGroupName);
            await _connectionManager.AddToGroupAsync(Context.ConnectionId, roomGroupName);

            if (room.BlackjackTableId.HasValue)
            {
                var tableGroupName = HubMethodNames.Groups.GetTableGroup(room.BlackjackTableId.Value.ToString());
                await JoinGroupAsync(tableGroupName);
                await _connectionManager.AddToGroupAsync(Context.ConnectionId, tableGroupName);
            }

            await SendSuccessAsync($"Conectado al control de juego para sala {roomCode}");
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "JoinRoomForGameControl");
        }
    }

    public async Task LeaveRoomGameControl(string roomCode)
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

            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(roomCode);
            await LeaveGroupAsync(roomGroupName);
            await _connectionManager.RemoveFromGroupAsync(Context.ConnectionId, roomGroupName);

            await SendSuccessAsync($"Desconectado del control de juego para sala {roomCode}");
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

    #region Private Methods

    private async Task SendInitialGameState(string roomCode, Guid tableId)
    {
        try
        {
            var table = await _tableRepository.GetTableWithPlayersAsync(tableId);
            if (table == null) return;

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

            var playersPayload = new List<object>();
            foreach (var seat in table.Seats.Where(s => s.IsOccupied && s.Player != null))
            {
                object? handPayload = null;
                if (seat.Player!.HandIds.Any())
                {
                    var firstHandId = seat.Player.HandIds.First();
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
                    playerId = seat.Player.Id,
                    name = seat.Player.Name,
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

            await _notificationService.NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.GameStateUpdated, gameState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameControlHub] Error sending initial game state: {Error}", ex.Message);
        }
    }

    #endregion
}