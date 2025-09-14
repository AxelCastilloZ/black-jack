using BlackJack.Services.Common;
using BlackJack.Services.Table;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

namespace BlackJack.Realtime.Hubs;

public class LobbyHub : Hub
{
    private readonly ITableService _tableService;
    private readonly ICurrentUser _currentUser;

    public LobbyHub(ITableService tableService, ICurrentUser currentUser)
    {
        _tableService = tableService;
        _currentUser = currentUser;
    }

    public async Task JoinLobby()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Lobby");

        // Send current available tables to the user
        var tablesResult = await _tableService.GetAvailableTablesAsync();
        if (tablesResult.IsSuccess)
        {
            await Clients.Caller.SendAsync("AvailableTables", tablesResult.Value);
        }
    }

    public async Task LeaveLobby()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Lobby");
    }

    public async Task CreateTable(string tableName)
    {
        if (_currentUser.UserId == null)
        {
            await Clients.Caller.SendAsync("Error", "User not authenticated");
            return;
        }

        var result = await _tableService.CreateTableAsync(tableName);

        if (result.IsSuccess)
        {
            // Notify all lobby users of new table
            await Clients.Group("Lobby").SendAsync("TableCreated", result.Value);
        }
        else
        {
            await Clients.Caller.SendAsync("Error", result.Error);
        }
    }

    public override async Task OnConnectedAsync()
    {
        await JoinLobby();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await LeaveLobby();
        await base.OnDisconnectedAsync(exception);
    }
}