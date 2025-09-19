// BaseHub.cs - En BlackJack.Realtime/Hubs/
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Realtime.Hubs;

public abstract class BaseHub : Hub
{
    protected readonly ILogger _logger;

    protected BaseHub(ILogger logger)
    {
        _logger = logger;
    }

    // Obtener el PlayerId del usuario actual desde el token JWT
    protected PlayerId? GetCurrentPlayerId()
    {
        try
        {
            var playerIdClaim = Context.User?.FindFirst("playerId")?.Value;
            if (string.IsNullOrEmpty(playerIdClaim))
            {
                _logger.LogWarning("[BaseHub] No playerId claim found for connection {ConnectionId}", Context.ConnectionId);
                return null;
            }

            if (Guid.TryParse(playerIdClaim, out var playerId))
            {
                return PlayerId.From(playerId);
            }

            _logger.LogWarning("[BaseHub] Invalid playerId format: {PlayerId} for connection {ConnectionId}",
                playerIdClaim, Context.ConnectionId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BaseHub] Error getting current player ID for connection {ConnectionId}: {Error}",
                Context.ConnectionId, ex.Message);
            return null;
        }
    }

    // Obtener el nombre del usuario actual
    protected string? GetCurrentUserName()
    {
        try
        {
            return Context.User?.FindFirst("name")?.Value ??
                   Context.User?.FindFirst(ClaimTypes.Name)?.Value ??
                   $"User-{Context.ConnectionId[..8]}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BaseHub] Error getting current user name for connection {ConnectionId}: {Error}",
                Context.ConnectionId, ex.Message);
            return $"User-{Context.ConnectionId[..8]}";
        }
    }

    // Verificar si el usuario está autenticado
    protected bool IsAuthenticated()
    {
        var isAuthenticated = Context.User?.Identity?.IsAuthenticated ?? false;
        if (!isAuthenticated)
        {
            _logger.LogWarning("[BaseHub] Unauthenticated connection attempt: {ConnectionId}", Context.ConnectionId);
        }
        return isAuthenticated;
    }

    // Enviar error al cliente actual
    protected async Task SendErrorAsync(string message)
    {
        _logger.LogWarning("[BaseHub] Sending error to {ConnectionId}: {Message}", Context.ConnectionId, message);
        await Clients.Caller.SendAsync("Error", new { message, timestamp = DateTime.UtcNow });
    }

    // Enviar éxito al cliente actual
    protected async Task SendSuccessAsync(string message, object? data = null)
    {
        _logger.LogInformation("[BaseHub] Sending success to {ConnectionId}: {Message}", Context.ConnectionId, message);
        await Clients.Caller.SendAsync("Success", new { message, data, timestamp = DateTime.UtcNow });
    }

    // Logging de eventos de conexión
    public override async Task OnConnectedAsync()
    {
        var playerId = GetCurrentPlayerId();
        var userName = GetCurrentUserName();

        _logger.LogInformation("[BaseHub] Client connected: {ConnectionId}, PlayerId: {PlayerId}, UserName: {UserName}",
            Context.ConnectionId, playerId, userName);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var playerId = GetCurrentPlayerId();
        var userName = GetCurrentUserName();

        if (exception != null)
        {
            _logger.LogWarning(exception, "[BaseHub] Client disconnected with error: {ConnectionId}, PlayerId: {PlayerId}, Error: {Error}",
                Context.ConnectionId, playerId, exception.Message);
        }
        else
        {
            _logger.LogInformation("[BaseHub] Client disconnected: {ConnectionId}, PlayerId: {PlayerId}, UserName: {UserName}",
                Context.ConnectionId, playerId, userName);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Métodos de utilidad para grupos
    protected async Task JoinGroupAsync(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("[BaseHub] Connection {ConnectionId} joined group {GroupName}",
            Context.ConnectionId, groupName);
    }

    protected async Task LeaveGroupAsync(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("[BaseHub] Connection {ConnectionId} left group {GroupName}",
            Context.ConnectionId, groupName);
    }

    // Validación de entrada
    protected bool ValidateInput(string input, string parameterName, int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            _logger.LogWarning("[BaseHub] Invalid input for {ParameterName}: null or empty", parameterName);
            return false;
        }

        if (maxLength.HasValue && input.Length > maxLength.Value)
        {
            _logger.LogWarning("[BaseHub] Invalid input for {ParameterName}: too long ({Length} > {MaxLength})",
                parameterName, input.Length, maxLength.Value);
            return false;
        }

        return true;
    }

    // Método para manejar excepciones comunes
    protected async Task HandleExceptionAsync(Exception ex, string operation)
    {
        _logger.LogError(ex, "[BaseHub] Error in {Operation} for connection {ConnectionId}: {Error}",
            operation, Context.ConnectionId, ex.Message);

        await SendErrorAsync($"Error en {operation}. Intenta de nuevo.");
    }
}