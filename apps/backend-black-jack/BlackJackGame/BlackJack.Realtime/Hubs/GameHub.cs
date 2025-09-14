using Microsoft.AspNetCore.SignalR;

namespace BlackJack.Realtime.Hubs;

public class GameHub : Hub
{
    public async Task JoinTable(string tableId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Table_{tableId}");
        await Clients.Group($"Table_{tableId}").SendAsync("PlayerJoined", Context.ConnectionId);
    }

    public async Task LeaveTable(string tableId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Table_{tableId}");
        await Clients.Group($"Table_{tableId}").SendAsync("PlayerLeft", Context.ConnectionId);
    }

    public async Task SendMessage(string tableId, string message)
    {
        await Clients.Group($"Table_{tableId}").SendAsync("ReceiveMessage", Context.ConnectionId, message);
    }
}