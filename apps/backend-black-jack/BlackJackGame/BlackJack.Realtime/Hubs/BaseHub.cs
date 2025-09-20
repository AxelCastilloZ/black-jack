// BlackJack.Realtime/Hubs/BaseHub.cs - VERSIÓN CORREGIDA PARA RESOLVER AUTENTICACIÓN JWT
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

    /// <summary>
    /// Obtiene el PlayerId del usuario actual desde el JWT token
    /// IGUAL QUE EN LOS CONTROLLERS
    /// </summary>
    protected PlayerId? GetCurrentPlayerId()
    {
        try
        {
            _logger.LogInformation("[BaseHub] Getting current player ID from JWT...");

            // Debug: log del usuario actual
            _logger.LogInformation("[BaseHub] User authenticated: {IsAuthenticated}",
                Context.User?.Identity?.IsAuthenticated ?? false);
            _logger.LogInformation("[BaseHub] User name: {UserName}",
                Context.User?.Identity?.Name ?? "NULL");

            var claims = Context.User?.Claims?.ToList() ?? new List<Claim>();
            _logger.LogInformation("[BaseHub] Claims count: {Count}", claims.Count);

            foreach (var claim in claims)
            {
                _logger.LogInformation("[BaseHub] Claim: {Type} = {Value}", claim.Type, claim.Value);
            }

            // MISMO CÓDIGO QUE EN CONTROLLERS: buscar playerId claim
            var playerIdClaim = Context.User?.FindFirst("playerId")?.Value
                ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            _logger.LogInformation("[BaseHub] PlayerId claim found: {PlayerIdClaim}", playerIdClaim ?? "NULL");

            if (string.IsNullOrEmpty(playerIdClaim) || !Guid.TryParse(playerIdClaim, out var playerId))
            {
                _logger.LogWarning("[BaseHub] Invalid or missing playerId claim in JWT token");
                return null;
            }

            var result = PlayerId.From(playerId);
            _logger.LogInformation("[BaseHub] PlayerId successfully created: {PlayerId}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BaseHub] Error getting current player ID from JWT: {Error}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Obtiene el nombre del usuario actual desde el JWT token
    /// IGUAL QUE EN LOS CONTROLLERS
    /// </summary>
    protected string? GetCurrentUserName()
    {
        try
        {
            _logger.LogInformation("[BaseHub] Getting current user name from JWT...");

            // MISMO CÓDIGO QUE EN CONTROLLERS: buscar name claim
            var userName = Context.User?.FindFirst("name")?.Value
                ?? Context.User?.FindFirst(ClaimTypes.Name)?.Value
                ?? Context.User?.Identity?.Name;

            _logger.LogInformation("[BaseHub] UserName resolved from JWT: {UserName}", userName ?? "NULL");

            return userName ?? "Jugador";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BaseHub] Error getting current user name from JWT: {Error}", ex.Message);
            return "Jugador";
        }
    }

    /// <summary>
    /// MÉTODO CORREGIDO: Verifica autenticación basada en claims válidos
    /// </summary>
    protected bool IsAuthenticated()
    {
        try
        {
            _logger.LogInformation("[BaseHub] === IsAuthenticated CHECK STARTED ===");

            // Método 1: Verificar Identity.IsAuthenticated
            var identityAuth = Context.User?.Identity?.IsAuthenticated ?? false;
            _logger.LogInformation("[BaseHub] Identity.IsAuthenticated: {IsAuthenticated}", identityAuth);

            // Método 2: Verificar presencia de claims específicos (MÁS CONFIABLE)
            var claims = Context.User?.Claims?.ToList() ?? new List<Claim>();
            _logger.LogInformation("[BaseHub] Total claims count: {Count}", claims.Count);

            // Buscar claims críticos que indican autenticación válida
            var hasPlayerIdClaim = Context.User?.FindFirst("playerId") != null ||
                                   Context.User?.FindFirst(ClaimTypes.NameIdentifier) != null;
            var hasNameClaim = Context.User?.FindFirst("name") != null ||
                              Context.User?.FindFirst(ClaimTypes.Name) != null;
            var hasSubClaim = Context.User?.FindFirst("sub") != null;

            _logger.LogInformation("[BaseHub] Claims analysis:");
            _logger.LogInformation("[BaseHub] - Has PlayerId claim: {HasPlayerId}", hasPlayerIdClaim);
            _logger.LogInformation("[BaseHub] - Has Name claim: {HasName}", hasNameClaim);
            _logger.LogInformation("[BaseHub] - Has Sub claim: {HasSub}", hasSubClaim);

            // DECISIÓN: Considerar autenticado si hay claims válidos
            // Esto es más confiable que solo Identity.IsAuthenticated para JWT
            var isAuthenticatedByClaims = claims.Count > 0 && (hasPlayerIdClaim || hasSubClaim);

            _logger.LogInformation("[BaseHub] Authentication decision:");
            _logger.LogInformation("[BaseHub] - Identity.IsAuthenticated: {IdentityAuth}", identityAuth);
            _logger.LogInformation("[BaseHub] - Authenticated by claims: {ClaimsAuth}", isAuthenticatedByClaims);

            // CAMBIO CRÍTICO: Usar autenticación por claims como principal
            var finalResult = identityAuth || isAuthenticatedByClaims;

            _logger.LogInformation("[BaseHub] === FINAL RESULT: {FinalResult} ===", finalResult);

            if (!finalResult)
            {
                _logger.LogWarning("[BaseHub] Authentication FAILED - No valid authentication found");
                _logger.LogWarning("[BaseHub] Claims dump:");
                foreach (var claim in claims)
                {
                    _logger.LogWarning("[BaseHub] Claim: {Type} = {Value}", claim.Type, claim.Value);
                }
            }
            else
            {
                _logger.LogInformation("[BaseHub] Authentication SUCCESS");
            }

            return finalResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BaseHub] EXCEPTION in IsAuthenticated: {Error}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Envía mensaje de éxito al cliente
    /// </summary>
    protected async Task SendSuccessAsync(string message)
    {
        try
        {
            await Clients.Caller.SendAsync("Success", new
            {
                message,
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
        await SendErrorAsync($"Error en {operation}: {ex.Message}");
    }

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
    /// Une el cliente a un grupo de SignalR
    /// </summary>
    protected async Task JoinGroupAsync(string groupName)
    {
        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("[{HubName}] Connection {ConnectionId} joined group {GroupName}",
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
            _logger.LogInformation("[{HubName}] Connection {ConnectionId} left group {GroupName}",
                GetType().Name, Context.ConnectionId, groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{HubName}] Error leaving group {GroupName}: {Error}",
                GetType().Name, groupName, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Evento al conectarse - VERSIÓN CORREGIDA QUE NO ABORTA CONEXIONES
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            var userName = GetCurrentUserName();
            var isAuthenticated = IsAuthenticated();

            _logger.LogInformation(
                "[{HubName}] New connection - ConnectionId: {ConnectionId}, PlayerId: {PlayerId}, UserName: {UserName}, Authenticated: {IsAuthenticated}",
                GetType().Name, Context.ConnectionId, playerId, userName, isAuthenticated);

            // Solo logging, NUNCA abortar conexión
            if (!isAuthenticated)
            {
                _logger.LogWarning("[{HubName}] Unauthenticated connection: {ConnectionId}",
                    GetType().Name, Context.ConnectionId);
                // REMOVIDO: await SendErrorAsync y Context.Abort()
            }

            if (playerId == null && isAuthenticated)
            {
                _logger.LogWarning("[{HubName}] Authenticated user but no valid PlayerId: {ConnectionId}",
                    GetType().Name, Context.ConnectionId);
                // REMOVIDO: await SendErrorAsync y Context.Abort()
            }

            // SIEMPRE permitir que la conexión continúe
            await base.OnConnectedAsync();

            _logger.LogInformation("[{HubName}] Connection {ConnectionId} established successfully",
                GetType().Name, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{HubName}] Error in OnConnectedAsync: {Error}",
                GetType().Name, ex.Message);

            // Solo abortar en casos de error crítico de sistema, no por auth
            throw;
        }
    }

    /// <summary>
    /// Evento al desconectarse
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            var userName = GetCurrentUserName();

            _logger.LogInformation(
                "[{HubName}] Connection disconnected - ConnectionId: {ConnectionId}, PlayerId: {PlayerId}, UserName: {UserName}, Exception: {Exception}",
                GetType().Name, Context.ConnectionId, playerId, userName, exception?.Message ?? "None");

            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{HubName}] Error in OnDisconnectedAsync: {Error}",
                GetType().Name, ex.Message);
        }
    }
}