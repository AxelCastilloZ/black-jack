// BlackJack.Realtime.Hubs/GameHub.cs - Versión mínima actualizada
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using BlackJack.Services.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Game;
using System.Security.Claims;

namespace BlackJack.Realtime.Hubs;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GameHub : Hub
{
    private readonly IGameService _gameService;

    public GameHub(IGameService gameService)
    {
        _gameService = gameService;
    }

    private PlayerId GetCurrentPlayerId()
    {
        // Intentar obtener el ID del usuario desde los claims
        var userIdClaim = Context.User?.FindFirst("sub")?.Value
                       ?? Context.User?.FindFirst("id")?.Value
                       ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userIdClaim != null && Guid.TryParse(userIdClaim, out var userId))
        {
            return PlayerId.From(userId);
        }

        // Fallback: usar un GUID basado en el ConnectionId para consistencia
        var connectionGuid = Guid.NewGuid();
        return PlayerId.From(connectionGuid);
    }

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

    // NUEVO: Método para unirse a un asiento específico
    public async Task JoinSeat(string tableId, int seatPosition)
    {
        try
        {
            if (!Guid.TryParse(tableId, out var tableGuid))
                throw new HubException("ID de mesa inválido");

            if (seatPosition < 0 || seatPosition >= 6)
                throw new HubException("Posición de asiento inválida (0-5)");

            var playerId = GetCurrentPlayerId();
            var result = await _gameService.JoinTableAsync(TableId.From(tableGuid), playerId, seatPosition);

            if (!result.IsSuccess)
                throw new HubException(result.Error ?? "No se pudo unir al asiento");

            // Asegurar que está en el grupo de la mesa
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Table_{tableId}");

            // Notificar que se unió al asiento específico
            await Clients.Group($"Table_{tableId}").SendAsync("PlayerJoinedSeat", Context.ConnectionId, seatPosition, playerId.ToString());

            // Opcional: Enviar estado actualizado del juego
            await NotifyGameState(tableId);
        }
        catch (Exception ex)
        {
            throw new HubException($"Error al unirse al asiento: {ex.Message}");
        }
    }

    // Opcional: Método para enviar el estado del juego
    private async Task NotifyGameState(string tableId)
    {
        try
        {
            var tableResult = await _gameService.GetTableAsync(TableId.From(Guid.Parse(tableId)));
            if (tableResult.IsSuccess)
            {
                var table = tableResult.Value;
                var gameState = new
                {
                    id = table.Id.ToString(),
                    status = table.Status.ToString(),
                    players = table.Seats.Where(s => s.IsOccupied && s.Player != null)
                                        .Select(s => new
                                        {
                                            id = s.Player.PlayerId.ToString(),
                                            displayName = s.Player.Name,
                                            balance = s.Player.Balance.Amount,
                                            currentBet = s.Player.CurrentBet?.Amount.Amount ?? 0,
                                            position = s.Position,
                                            isActive = s.Player.IsActive
                                        }).ToArray(),
                    minBet = table.MinBet.Amount,
                    maxBet = table.MaxBet.Amount
                };

                await Clients.Group($"Table_{tableId}").SendAsync("GameStateUpdated", gameState);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error enviando estado del juego: {ex.Message}");
        }
    }
}