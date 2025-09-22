// BlackJack.Realtime/Hubs/GameHub.cs - Hub coordinador simple para compatibilidad
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlackJack.Realtime.Hubs;

[AllowAnonymous]
public class GameHub : BaseHub
{
    public GameHub(ILogger<GameHub> logger) : base(logger)
    {
    }

    #region Connection Management

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        var playerId = GetCurrentPlayerId();
        var userName = GetCurrentUserName();

        _logger.LogInformation("[GameHub] Player {PlayerId} ({UserName}) connected with ConnectionId {ConnectionId}",
            playerId, userName, Context.ConnectionId);

        await SendSuccessAsync("Conectado exitosamente al hub de juego");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var playerId = GetCurrentPlayerId();

        _logger.LogInformation("[GameHub] Player {PlayerId} disconnecting from ConnectionId {ConnectionId}",
            playerId, Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    #endregion

    #region Redirect Methods - Informan sobre los hubs especializados

    public async Task CreateRoom()
    {
        await SendErrorAsync("Este método se ha movido a RoomHub. Conecta a /hubs/room");
    }

    public async Task JoinRoom()
    {
        await SendErrorAsync("Este método se ha movido a RoomHub. Conecta a /hubs/room");
    }

    public async Task JoinSeat()
    {
        await SendErrorAsync("Este método se ha movido a SeatHub. Conecta a /hubs/seat");
    }

    public async Task JoinAsViewer()
    {
        await SendErrorAsync("Este método se ha movido a SpectatorHub. Conecta a /hubs/spectator");
    }

    public async Task StartGame()
    {
        await SendErrorAsync("Este método se ha movido a GameControlHub. Conecta a /hubs/game-control");
    }

    #endregion

    #region Test Methods

    [AllowAnonymous]
    public async Task TestConnection()
    {
        _logger.LogInformation("[GameHub] TestConnection called");
        await Clients.Caller.SendAsync("TestResponse", new
        {
            message = "SignalR funcionando - Hub coordinador",
            timestamp = DateTime.UtcNow,
            connectionId = Context.ConnectionId,
            playerId = GetCurrentPlayerId()?.Value,
            note = "Este es el hub coordinador. Para funcionalidad específica usa los hubs especializados."
        });
    }

    #endregion
}