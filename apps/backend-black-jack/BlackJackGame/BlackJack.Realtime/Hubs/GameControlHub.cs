// BlackJack.Realtime/Hubs/GameControlHub.cs - Control del juego
using BlackJack.Domain.Models.Users;
using BlackJack.Realtime.Models;
using BlackJack.Services.Game;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlackJack.Realtime.Hubs;

[AllowAnonymous]
public class GameControlHub : BaseHub
{
    private readonly IGameRoomService _gameRoomService;
    private readonly IGameService _gameService;

    public GameControlHub(
        IGameRoomService gameRoomService,
        IGameService gameService,
        ILogger<GameControlHub> logger) : base(logger)
    {
        _gameRoomService = gameRoomService;
        _gameService = gameService;
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

            _logger.LogInformation("[GameControlHub] Starting game in room {RoomCode} by player {PlayerId}",
                roomCode, playerId);

            var result = await _gameRoomService.StartGameAsync(roomCode, playerId);

            if (result.IsSuccess)
            {
                _logger.LogInformation("[GameControlHub] Game started successfully in room {RoomCode}", roomCode);

                var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
                if (roomResult.IsSuccess)
                {
                    var room = roomResult.Value!;

                    // If the room is associated with a Blackjack table, sync seats and start the actual round
                    if (room.BlackjackTableId.HasValue)
                    {
                        var tableId = room.BlackjackTableId.Value;

                        // Sync seated players from room to table (idempotent per player/seat)
                        foreach (var rp in room.Players)
                        {
                            if (rp.SeatPosition.HasValue && rp.SeatPosition.Value >= 0 && rp.SeatPosition.Value <= 5)
                            {
                                try
                                {
                                    _logger.LogInformation("[GameControlHub] Ensuring player {PlayerId} is seated at table {TableId} seat {Seat}", rp.PlayerId, tableId, rp.SeatPosition.Value);
                                    var joinRes = await _gameService.JoinTableAsync(tableId, rp.PlayerId, rp.SeatPosition.Value);
                                    if (!joinRes.IsSuccess)
                                    {
                                        _logger.LogWarning("[GameControlHub] JoinTableAsync warning for player {PlayerId}: {Error}", rp.PlayerId, joinRes.Error);
                                        // Continue seating others; table may already reflect seating
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "[GameControlHub] JoinTableAsync threw for player {PlayerId}", rp.PlayerId);
                                }
                            }
                        }

                        _logger.LogInformation("[GameControlHub] Triggering StartRoundAsync for table {TableId}", tableId);
                        var startRoundResult = await _gameService.StartRoundAsync(tableId);
                        if (!startRoundResult.IsSuccess)
                        {
                            _logger.LogWarning("[GameControlHub] StartRoundAsync failed: {Error}", startRoundResult.Error);
                            await SendErrorAsync(startRoundResult.Error ?? "No se pudo iniciar la ronda");
                            return;
                        }
                    }

                    var gameStartedEvent = new { RoomCode = roomCode, Message = "Juego iniciado" };

                    await Clients.Group(HubMethodNames.Groups.GetRoomGroup(roomCode))
                        .SendAsync(HubMethodNames.ServerMethods.GameStarted, gameStartedEvent);
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

    // Aquí se pueden agregar más métodos de control del juego como:
    // - PauseGame
    // - ResumeGame
    // - EndGame
    // - RestartGame
    // - etc.

    #endregion
}