// BlackJack.Realtime/Hubs/ConnectionHub.cs - CORREGIDO: Con limpieza automática al reconectar
using BlackJack.Domain.Models.Users;
using BlackJack.Realtime.Services;
using BlackJack.Services.Game; // NUEVO: Para IGameRoomService
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlackJack.Realtime.Hubs;

[AllowAnonymous]
public class ConnectionHub : BaseHub
{
    private readonly IConnectionManager _connectionManager;
    private readonly ISignalRNotificationService _notificationService;
    private readonly IGameRoomService _gameRoomService; // NUEVO: Para limpieza automática

    public ConnectionHub(
        IConnectionManager connectionManager,
        ISignalRNotificationService notificationService,
        IGameRoomService gameRoomService, // NUEVO: Inyección de dependencia
        ILogger<ConnectionHub> logger) : base(logger)
    {
        _connectionManager = connectionManager;
        _notificationService = notificationService;
        _gameRoomService = gameRoomService; // NUEVO
    }

    #region Connection Management

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        var playerId = GetCurrentPlayerId();
        var userName = GetCurrentUserName();

        _logger.LogInformation("[ConnectionHub] Player {PlayerId} ({UserName}) connected with ConnectionId {ConnectionId}",
            playerId, userName, Context.ConnectionId);

        // NUEVO: LIMPIEZA AUTOMÁTICA DE DATOS FANTASMA AL CONECTAR
        if (playerId != null)
        {
            try
            {
                _logger.LogInformation("[ConnectionHub] === AUTOMATIC CLEANUP ON RECONNECT START ===");
                _logger.LogInformation("[ConnectionHub] Performing automatic cleanup for player {PlayerId}", playerId);

                var cleanupResult = await _gameRoomService.ForceCleanupPlayerAsync(playerId);

                if (cleanupResult.IsSuccess)
                {
                    var affectedRows = cleanupResult.Value;
                    if (affectedRows > 0)
                    {
                        _logger.LogInformation("[ConnectionHub] ✅ Automatic cleanup completed: {AffectedRows} orphan records removed for player {PlayerId}",
                            affectedRows, playerId);
                    }
                    else
                    {
                        _logger.LogInformation("[ConnectionHub] ✅ Automatic cleanup completed: No orphan records found for player {PlayerId}", playerId);
                    }
                }
                else
                {
                    _logger.LogWarning("[ConnectionHub] ⚠️ Automatic cleanup failed for player {PlayerId}: {Error}",
                        playerId, cleanupResult.Error);
                }

                _logger.LogInformation("[ConnectionHub] === AUTOMATIC CLEANUP ON RECONNECT END ===");
            }
            catch (Exception ex)
            {
                // IMPORTANTE: No abortar conexión por errores de limpieza
                _logger.LogError(ex, "[ConnectionHub] Error during automatic cleanup for player {PlayerId}: {Error}",
                    playerId, ex.Message);
            }
        }

        // CONTINUAR CON LÓGICA ORIGINAL: Registrar conexión básica
        if (playerId != null && userName != null)
        {
            await _connectionManager.AddConnectionAsync(Context.ConnectionId, playerId, userName);
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

        await _notificationService.NotifyConnectionAsync(Context.ConnectionId, "TestResponse", response);
    }

    #endregion
}