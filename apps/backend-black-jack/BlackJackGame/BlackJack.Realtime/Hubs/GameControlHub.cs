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

    public GameControlHub(
        IGameRoomService gameRoomService,
        ILogger<GameControlHub> logger) : base(logger)
    {
        _gameRoomService = gameRoomService;
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