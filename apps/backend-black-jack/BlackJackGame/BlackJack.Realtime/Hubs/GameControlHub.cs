// BlackJack.Realtime/Hubs/GameControlHub.cs - Control del juego
using BlackJack.Domain.Models.Users;
using BlackJack.Realtime.Models;
using BlackJack.Services.Game;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using BlackJack.Data.Repositories.Game;
using BlackJack.Realtime.Services;

namespace BlackJack.Realtime.Hubs;

[AllowAnonymous]
public class GameControlHub : BaseHub
{
    private readonly IGameRoomService _gameRoomService;
    private readonly IGameService _gameService;
    private readonly ITableRepository _tableRepository;
    private readonly IHandRepository _handRepository;
    private readonly ISignalRNotificationService _notificationService;

    public GameControlHub(
        IGameRoomService gameRoomService,
        IGameService gameService,
        ITableRepository tableRepository,
        IHandRepository handRepository,
        ISignalRNotificationService notificationService,
        ILogger<GameControlHub> logger) : base(logger)
    {
        _gameRoomService = gameRoomService;
        _gameService = gameService;
        _tableRepository = tableRepository;
        _handRepository = handRepository;
        _notificationService = notificationService;
    }

    #region Game Control

    public async Task StartGame(string roomCode)
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

            _logger.LogInformation("[GameControlHub] Starting game in room {RoomCode} by player {PlayerId}", roomCode, playerId);

            // First, get room and seat players on the actual table BEFORE changing room status
            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync(roomResult.Error ?? "Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;

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
                            _logger.LogInformation("[GameControlHub] Ensuring player {PlayerId} is seated at table {TableId} seat {Seat}", rp.PlayerId, tableId, rp.SeatPosition.Value);
                            var joinRes = await _gameService.JoinTableAsync(tableId, rp.PlayerId, rp.SeatPosition.Value);
                            if (joinRes.IsSuccess) seatedCount++;
                            else _logger.LogWarning("[GameControlHub] JoinTableAsync warning for player {PlayerId}: {Error}", rp.PlayerId, joinRes.Error);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[GameControlHub] JoinTableAsync threw for player {PlayerId}", rp.PlayerId);
                        }
                    }
                }

                // Fallback: if nobody had a valid seat, seat the caller at seat 0
                if (seatedCount == 0)
                {
                    try
                    {
                        _logger.LogInformation("[GameControlHub] Fallback seat host {PlayerId} at seat 0 for table {TableId}", playerId, tableId);
                        await _gameService.JoinTableAsync(tableId, playerId, 0);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[GameControlHub] Fallback seating failed for host {PlayerId}", playerId);
                    }
                }

                // Now, start the actual round (idempotent)
                _logger.LogInformation("[GameControlHub] Triggering StartRoundAsync for table {TableId}", tableId);
                var startRoundResult = await _gameService.StartRoundAsync(tableId);
                if (!startRoundResult.IsSuccess)
                {
                    // If error is due to 0 seats but status already InProgress, treat as OK
                    var tableStatusOk = startRoundResult.Error?.Contains("al menos 1 jugador") == true;
                    if (!tableStatusOk)
                    {
                        _logger.LogWarning("[GameControlHub] StartRoundAsync failed: {Error}", startRoundResult.Error);
                        await SendErrorAsync(startRoundResult.Error ?? "No se pudo iniciar la ronda");
                        return;
                    }
                }
            }

            // Finally, mark room as started (for lobby/turns) and notify
            var result = await _gameRoomService.StartGameAsync(roomCode, playerId);
            if (!result.IsSuccess)
            {
                await SendErrorAsync(result.Error);
                return;
            }

            _logger.LogInformation("[GameControlHub] Game started successfully in room {RoomCode}", roomCode);
            await _notificationService.NotifyRoomAsync(roomCode, HubMethodNames.ServerMethods.GameStarted,
                new { RoomCode = roomCode, Message = "Juego iniciado" });

            // Emit initial game state with dealt hands so frontend can render cards
            if (room.BlackjackTableId.HasValue)
            {
                var tableAfterStart = await _tableRepository.GetTableWithPlayersAsync(room.BlackjackTableId.Value);
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
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "StartGame");
        }
    }

    // Aquí se pueden agregar más métodos de control del juego como:
    // - PauseGame
    // - ResumeGame
    // - EndGame
    // - RestartGame
    // - etc.

    #endregion
}