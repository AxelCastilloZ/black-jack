// BlackJack.Realtime/Hubs/BaseHub.cs - Funcionalidad base compartida
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using BlackJack.Domain.Models.Users;
using System.Security.Claims;

namespace BlackJack.Realtime.Hubs;

public abstract class BaseHub : Hub
{
    protected readonly ILogger _logger;

    protected BaseHub(ILogger logger)
    {
        _logger = logger;
    }

    #region Autenticación JWT

    /// <summary>
    /// Obtiene el PlayerId del usuario actual desde el JWT token
    /// </summary>
    protected PlayerId? GetCurrentPlayerId()
    {
        try
        {
            var playerIdClaim = Context.User?.FindFirst("playerId")?.Value
                ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(playerIdClaim) || !Guid.TryParse(playerIdClaim, out var playerId))
            {
                return null;
            }

            return PlayerId.From(playerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BaseHub] Error getting current player ID: {Error}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Obtiene el nombre del usuario actual desde el JWT token
    /// </summary>
    protected string GetCurrentUserName()
    {
        try
        {
            var userName = Context.User?.FindFirst("name")?.Value
                ?? Context.User?.FindFirst(ClaimTypes.Name)?.Value
                ?? Context.User?.Identity?.Name;

            return userName ?? "Jugador";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BaseHub] Error getting current user name: {Error}", ex.Message);
            return "Jugador";
        }
    }

    /// <summary>
    /// Verifica si el usuario está autenticado
    /// </summary>
    protected bool IsAuthenticated()
    {
        try
        {
            var identityAuth = Context.User?.Identity?.IsAuthenticated ?? false;
            var claims = Context.User?.Claims?.ToList() ?? new List<Claim>();

            var hasPlayerIdClaim = Context.User?.FindFirst("playerId") != null ||
                                   Context.User?.FindFirst(ClaimTypes.NameIdentifier) != null;

            return identityAuth || (claims.Count > 0 && hasPlayerIdClaim);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BaseHub] Error checking authentication: {Error}", ex.Message);
            return false;
        }
    }

    #endregion

    #region Mensajes al cliente

    /// <summary>
    /// Envía mensaje de éxito al cliente
    /// </summary>
    protected async Task SendSuccessAsync(string message, object? data = null)
    {
        try
        {
            await Clients.Caller.SendAsync("Success", new
            {
                message,
                data,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BaseHub] Error sending success message: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Envía mensaje de error al cliente
    /// </summary>
    protected async Task SendErrorAsync(string message)
    {
        try
        {
            await Clients.Caller.SendAsync("Error", new
            {
                message,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BaseHub] Error sending error message: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Maneja excepciones y envía error al cliente
    /// </summary>
    protected async Task HandleExceptionAsync(Exception ex, string operation)
    {
        _logger.LogError(ex, "[{HubName}] Error in {Operation}: {Error}",
            GetType().Name, operation, ex.Message);
        await SendErrorAsync($"Error en {operation}");
    }

    #endregion

    #region Validaciones

    /// <summary>
    /// Valida entrada de texto
    /// </summary>
    protected bool ValidateInput(string? input, string paramName, int maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            _logger.LogWarning("[{HubName}] Invalid input: {ParamName} is null or empty",
                GetType().Name, paramName);
            return false;
        }

        if (input.Length > maxLength)
        {
            _logger.LogWarning("[{HubName}] Invalid input: {ParamName} exceeds max length {MaxLength}",
                GetType().Name, paramName, maxLength);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Valida autenticación y obtiene PlayerId
    /// </summary>
    protected async Task<PlayerId?> ValidateAuthenticationAsync()
    {
        if (!IsAuthenticated())
        {
            await SendErrorAsync("Debes estar autenticado");
            return null;
        }

        var playerId = GetCurrentPlayerId();
        if (playerId == null)
        {
            await SendErrorAsync("Error de autenticación");
            return null;
        }

        return playerId;
    }

    #endregion

    #region Gestión de grupos

    /// <summary>
    /// Une el cliente a un grupo de SignalR
    /// </summary>
    protected async Task JoinGroupAsync(string groupName)
    {
        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogDebug("[{HubName}] Connection {ConnectionId} joined group {GroupName}",
                GetType().Name, Context.ConnectionId, groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{HubName}] Error joining group {GroupName}: {Error}",
                GetType().Name, groupName, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Remueve el cliente de un grupo de SignalR
    /// </summary>
    protected async Task LeaveGroupAsync(string groupName)
    {
        try
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogDebug("[{HubName}] Connection {ConnectionId} left group {GroupName}",
                GetType().Name, Context.ConnectionId, groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{HubName}] Error leaving group {GroupName}: {Error}",
                GetType().Name, groupName, ex.Message);
            throw;
        }
    }

    #endregion

    #region Eventos de conexión

    public override async Task OnConnectedAsync()
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            var userName = GetCurrentUserName();
            var isAuthenticated = IsAuthenticated();

            _logger.LogInformation(
                "[{HubName}] Connection established - ConnectionId: {ConnectionId}, PlayerId: {PlayerId}, UserName: {UserName}, Authenticated: {IsAuthenticated}",
                GetType().Name, Context.ConnectionId, playerId, userName, isAuthenticated);

            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{HubName}] Error in OnConnectedAsync: {Error}",
                GetType().Name, ex.Message);
            throw;
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var playerId = GetCurrentPlayerId();

            _logger.LogInformation(
                "[{HubName}] Connection disconnected - ConnectionId: {ConnectionId}, PlayerId: {PlayerId}, Exception: {Exception}",
                GetType().Name, Context.ConnectionId, playerId, exception?.Message ?? "None");

            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{HubName}] Error in OnDisconnectedAsync: {Error}",
                GetType().Name, ex.Message);
        }
    }

    #endregion
}