using BlackJack.Domain.Enums;
using BlackJack.Domain.Models.Betting;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;
using BlackJack.Services.Game;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

namespace BlackJack.Realtime.Hubs;

public class GameHub : Hub
{
    private readonly IGameService _gameService;
    private readonly ICurrentUser _currentUser;

    public GameHub(IGameService gameService, ICurrentUser currentUser)
    {
        _gameService = gameService;
        _currentUser = currentUser;
    }

    public async Task JoinTable(string tableId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Table_{tableId}");
        await Clients.Group($"Table_{tableId}").SendAsync("PlayerConnected", Context.ConnectionId);
    }

    public async Task LeaveTable(string tableId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Table_{tableId}");
        await Clients.Group($"Table_{tableId}").SendAsync("PlayerDisconnected", Context.ConnectionId);
    }

    public async Task PlaceBet(string tableId, decimal amount)
    {
        if (_currentUser.UserId == null)
        {
            await Clients.Caller.SendAsync("Error", "User not authenticated");
            return;
        }

        var bet = Bet.Create(amount);
        var result = await _gameService.PlaceBetAsync(TableId.From(Guid.Parse(tableId)), _currentUser.UserId, bet);

        if (result.IsSuccess)
        {
            await Clients.Group($"Table_{tableId}").SendAsync("BetPlaced", Context.ConnectionId, amount);
        }
        else
        {
            await Clients.Caller.SendAsync("Error", result.Error);
        }
    }

    public async Task PlayerAction(string tableId, string action)
    {
        if (_currentUser.UserId == null)
        {
            await Clients.Caller.SendAsync("Error", "User not authenticated");
            return;
        }

        if (Enum.TryParse<PlayerAction>(action, out var playerAction))
        {
            var result = await _gameService.PlayerActionAsync(
                TableId.From(Guid.Parse(tableId)),
                _currentUser.UserId,
                playerAction);

            if (result.IsSuccess)
            {
                await Clients.Group($"Table_{tableId}").SendAsync("PlayerAction", Context.ConnectionId, action);
            }
            else
            {
                await Clients.Caller.SendAsync("Error", result.Error);
            }
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Handle cleanup when user disconnects
        await base.OnDisconnectedAsync(exception);
    }
}