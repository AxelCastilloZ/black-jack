// BlackJack.Realtime/Hubs/ConnectionHub.cs - CORREGIDO: Simplificado sin manejo de grupos
using BlackJack.Domain.Models.Users;
using BlackJack.Realtime.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlackJack.Realtime.Hubs;

[AllowAnonymous]
public class ConnectionHub : BaseHub
{
    private readonly IConnectionManager _connectionManager;
    private readonly ISignalRNotificationService _notificationService; // AGREGADO

    public ConnectionHub(
        IConnectionManager connectionManager,
        ISignalRNotificationService notificationService, // AGREGADO
        ILogger<ConnectionHub> logger) : base(logger)
    {
        _connectionManager = connectionManager;
        _notificationService = notificationService; // AGREGADO
    }

    #region Connection Management

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        var playerId = GetCurrentPlayerId();
        var userName = GetCurrentUserName();

        _logger.LogInformation("[ConnectionHub] Player {PlayerId} ({UserName}) connected with ConnectionId {ConnectionId}",
            playerId, userName, Context.ConnectionId);

        // SIMPLIFICADO: Solo registrar conexión básica
        if (playerId != null && userName != null)
        {
            await _connectionManager.AddConnectionAsync(Context.ConnectionId, playerId, userName);

            // REMOVIDO: HandleAutoReconnectionAsync - esto debe manejarse en otros hubs específicos
            // Ya no manejamos grupos aquí para evitar conflictos

            await _notificationService.SendSuccessToConnectionAsync(Context.ConnectionId, "Conectado exitosamente al hub de conexión");
        }
        else
        {
            await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, "Error generando ID de jugador");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var playerId = GetCurrentPlayerId();

        _logger.LogInformation("[ConnectionHub] Player {PlayerId} disconnecting from ConnectionId {ConnectionId}",
            playerId, Context.ConnectionId);

        // SIMPLIFICADO: Solo limpiar la conexión básica
        // La lógica de reconexión se maneja en otros hubs
        await _connectionManager.RemoveConnectionAsync(Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    #endregion

    #region Test Methods

    [AllowAnonymous]
    public async Task TestConnection()
    {
        _logger.LogInformation("[ConnectionHub] TestConnection called");

        var response = new
        {
            message = "SignalR funcionando",
            timestamp = DateTime.UtcNow,
            connectionId = Context.ConnectionId,
            playerId = GetCurrentPlayerId()?.Value
        };

        // CORREGIDO: Usar notificationService
        await _notificationService.NotifyConnectionAsync(Context.ConnectionId, "TestResponse", response);
    }

    #endregion
}