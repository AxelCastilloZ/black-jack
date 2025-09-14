using Microsoft.AspNetCore.SignalR;

namespace BlackJack.Realtime.Hubs;

public class LobbyHub : Hub
{
    public async Task JoinLobby()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Lobby");
        await Clients.Caller.SendAsync("JoinedLobby");
    }

    public async Task LeaveLobby()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Lobby");
    }
}