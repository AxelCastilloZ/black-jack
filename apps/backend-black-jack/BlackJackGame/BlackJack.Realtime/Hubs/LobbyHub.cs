using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using BlackJack.Services.Table;
using BlackJack.Services.Common;

namespace BlackJack.Realtime.Hubs;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class LobbyHub : Hub
{
    private readonly ITableService _tableService;
    private readonly ICurrentUser _currentUser;

    public LobbyHub(ITableService tableService, ICurrentUser currentUser)
    {
        _tableService = tableService;
        _currentUser = currentUser;
    }

    // ==== Métodos invocados por el CLIENTE ====

    public async Task JoinLobby()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "lobby");
        await Clients.Caller.SendAsync("JoinedLobby");
    }

    public async Task LeaveLobby()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "lobby");
    }

    public class CreateTableRequest
    {
        public string Name { get; set; } = string.Empty;
        // Si luego quieres, agrega min/max bet, maxPlayers, etc.
    }

    // El nombre DEBE coincidir con el que invocas desde el cliente: "CreateTable"
    public async Task CreateTable(CreateTableRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new HubException("El nombre es requerido");

        var result = await _tableService.CreateTableAsync(req.Name);
        if (!result.IsSuccess)
            throw new HubException(result.Error ?? "No se pudo crear la mesa");

        var t = result.Value;

        // Notifica a todos en el lobby
        await Clients.Group("lobby").SendAsync("TableCreated", new
        {
            table = new
            {
                id = t.Id.ToString(),
                name = t.Name,
                playerCount = t.Seats.Count(s => s.IsOccupied),
                maxPlayers = t.Seats.Count,
                minBet = t.MinBet.Amount,
                maxBet = t.MaxBet.Amount,
                status = t.Status.ToString()
            }
        });
    }
}
