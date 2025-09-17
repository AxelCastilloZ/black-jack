// BlackJack.Realtime.Hubs/LobbyHub.cs - Versión corregida
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using BlackJack.Services.Table;
using System.Security.Claims;

namespace BlackJack.Realtime.Hubs;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class LobbyHub : Hub
{
    private readonly ITableService _tableService;

    public LobbyHub(ITableService tableService)
    {
        _tableService = tableService;
    }

    // Método helper para obtener el ID del usuario actual
    private string GetCurrentUserId()
    {
        var userIdClaim = Context.User?.FindFirst("sub")?.Value
                       ?? Context.User?.FindFirst("id")?.Value
                       ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return userIdClaim ?? Context.ConnectionId; // Fallback a ConnectionId
    }

    // ==== EVENTOS DE CONEXIÓN ====
    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = GetCurrentUserId();
            Console.WriteLine($"[LobbyHub] Usuario {userId} conectado. ConnectionId: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LobbyHub] Error en OnConnectedAsync: {ex.Message}");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var userId = GetCurrentUserId();
            Console.WriteLine($"[LobbyHub] Usuario {userId} desconectado. Razón: {exception?.Message ?? "Normal"}");
            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LobbyHub] Error en OnDisconnectedAsync: {ex.Message}");
        }
    }

    // ==== MÉTODOS INVOCADOS POR EL CLIENTE ====

    /// <summary>
    /// Unir usuario al grupo del lobby
    /// </summary>
    public async Task JoinLobby()
    {
        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "lobby");
            await Clients.Caller.SendAsync("JoinedLobby");

            Console.WriteLine($"[LobbyHub] Usuario {Context.ConnectionId} se unió al lobby");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LobbyHub] Error en JoinLobby: {ex.Message}");
            throw new HubException($"Error al unirse al lobby: {ex.Message}");
        }
    }

    /// <summary>
    /// Remover usuario del grupo del lobby
    /// </summary>
    public async Task LeaveLobby()
    {
        try
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "lobby");
            Console.WriteLine($"[LobbyHub] Usuario {Context.ConnectionId} dejó el lobby");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LobbyHub] Error en LeaveLobby: {ex.Message}");
            // No throw porque leave es menos crítico
        }
    }

    /// <summary>
    /// Crear nueva mesa de juego
    /// </summary>
    public async Task CreateTable(CreateTableRequest req)
    {
        try
        {
            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(req.Name))
                throw new HubException("El nombre de la mesa es requerido");

            if (req.Name.Length > 50)
                throw new HubException("El nombre de la mesa no puede exceder 50 caracteres");

            var userId = GetCurrentUserId();
            Console.WriteLine($"[LobbyHub] Usuario {userId} creando mesa: {req.Name}");

            // Llamar al servicio de tabla
            var result = await _tableService.CreateTableAsync(req.Name);

            if (!result.IsSuccess)
                throw new HubException(result.Error ?? "No se pudo crear la mesa");

            var table = result.Value;

            // Preparar data para enviar al frontend
            var tableData = new
            {
                table = new
                {
                    id = table.Id.ToString(),
                    name = table.Name,
                    playerCount = table.Seats.Count(s => s.IsOccupied),
                    maxPlayers = table.Seats.Count, // Normalmente 6
                    minBet = table.MinBet.Amount,
                    maxBet = table.MaxBet.Amount,
                    status = table.Status.ToString(),
                    createdBy = userId,
                    createdAt = DateTime.UtcNow
                }
            };

            // Notificar a todos en el lobby que se creó una mesa
            await Clients.Group("lobby").SendAsync("TableCreated", tableData);

            // También notificar al creador específicamente (por si necesita navegación automática)
            await Clients.Caller.SendAsync("TableCreatedByMe", tableData);

            Console.WriteLine($"[LobbyHub] Mesa {table.Id} creada exitosamente");
        }
        catch (HubException)
        {
            // Re-throw HubExceptions tal como están
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LobbyHub] Error inesperado en CreateTable: {ex.Message}");
            throw new HubException($"Error interno al crear la mesa: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtener lista de mesas disponibles (método opcional para refrescar)
    /// </summary>
    public async Task GetAvailableTables()
    {
        try
        {
            // Como no tienes GetAvailableTablesAsync, usa un método que sí existe
            // o comenta esta funcionalidad por ahora

            await Clients.Caller.SendAsync("AvailableTables", new object[0]); // Array vacío por ahora
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LobbyHub] Error en GetAvailableTables: {ex.Message}");
            throw new HubException($"Error al obtener mesas: {ex.Message}");
        }
    }

    // ==== MÉTODOS PRIVADOS DE UTILIDAD ====

    private async Task NotifyLobbyUpdate(string tableId, int playerCount, string status)
    {
        try
        {
            var update = new
            {
                tableId = tableId,
                playerCount = playerCount,
                status = status,
                timestamp = DateTime.UtcNow
            };

            await Clients.Group("lobby").SendAsync("TableUpdated", update);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LobbyHub] Error notificando actualización del lobby: {ex.Message}");
        }
    }
}

// ==== CLASES DE REQUEST ====

public class CreateTableRequest
{
    public string Name { get; set; } = string.Empty;

    // Propiedades opcionales para configuración futura
    public int MinBet { get; set; } = 10;
    public int MaxBet { get; set; } = 500;
    public int MaxPlayers { get; set; } = 6;
}