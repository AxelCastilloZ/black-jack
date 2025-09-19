// BaseHub.cs - VERSIÓN CON MANEJO DE CLAIMS MEJORADO
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
            // DEBUG: Mostrar información completa del contexto
            _logger.LogInformation("[BaseHub] ===== DEBUGGING USER CONTEXT =====");
            _logger.LogInformation("[BaseHub] Connection ID: {ConnectionId}", Context.ConnectionId);
            _logger.LogInformation("[BaseHub] User exists: {UserExists}", Context.User != null);
            _logger.LogInformation("[BaseHub] User Identity exists: {IdentityExists}", Context.User?.Identity != null);
            _logger.LogInformation("[BaseHub] Is authenticated: {IsAuthenticated}", Context.User?.Identity?.IsAuthenticated);

            if (Context.User?.Claims != null)
            {
                var allClaims = Context.User.Claims.Select(c => $"{c.Type}={c.Value}").ToList();
                _logger.LogInformation("[BaseHub] All available claims: {Claims}", string.Join(", ", allClaims));
            }
            else
            {
                _logger.LogWarning("[BaseHub] No claims found in user context");
            }

            // Intentar múltiples nombres de claims comunes para playerId
            var playerIdClaim = Context.User?.FindFirst("playerId")?.Value ??
                               Context.User?.FindFirst("player_id")?.Value ??
                               Context.User?.FindFirst("PlayerId")?.Value ??
                               Context.User?.FindFirst("userId")?.Value ??
                               Context.User?.FindFirst("user_id")?.Value ??
                               Context.User?.FindFirst("UserId")?.Value ??
                               Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                               Context.User?.FindFirst("sub")?.Value; // "sub" es estándar JWT para subject/user ID

            if (string.IsNullOrEmpty(playerIdClaim))
            {
                _logger.LogWarning("[BaseHub] No playerId claim found for connection {ConnectionId}", Context.ConnectionId);

                // TEMPORAL: Si no hay claim, usar un ID basado en el email o nombre de usuario
                var emailClaim = Context.User?.FindFirst("email")?.Value ?? Context.User?.FindFirst(ClaimTypes.Email)?.Value;
                var nameClaim = Context.User?.FindFirst("name")?.Value ?? Context.User?.FindFirst(ClaimTypes.Name)?.Value;

                if (!string.IsNullOrEmpty(emailClaim))
                {
                    // Generar un GUID determinista basado en el email
                    var emailGuid = GenerateDeterministicGuid(emailClaim);
                    _logger.LogInformation("[BaseHub] Using email-based GUID: {EmailGuid} from email: {Email}", emailGuid, emailClaim);
                    return PlayerId.From(emailGuid);
                }

                if (!string.IsNullOrEmpty(nameClaim))
                {
                    // Generar un GUID determinista basado en el nombre
                    var nameGuid = GenerateDeterministicGuid(nameClaim);
                    _logger.LogInformation("[BaseHub] Using name-based GUID: {NameGuid} from name: {Name}", nameGuid, nameClaim);
                    return PlayerId.From(nameGuid);
                }

                return null;
            }

            if (Guid.TryParse(playerIdClaim, out var playerId))
            {
                _logger.LogInformation("[BaseHub] Successfully parsed playerId: {PlayerId} for connection {ConnectionId}",
                    playerId, Context.ConnectionId);
                return PlayerId.From(playerId);
            }

            // Si no es un GUID válido, intentar generar uno determinista
            _logger.LogWarning("[BaseHub] Invalid playerId format: {PlayerId}, generating deterministic GUID", playerIdClaim);
            var deterministicGuid = GenerateDeterministicGuid(playerIdClaim);
            _logger.LogInformation("[BaseHub] Generated deterministic GUID: {GUID} from claim: {Claim}", deterministicGuid, playerIdClaim);
            return PlayerId.From(deterministicGuid);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BaseHub] Error getting current player ID for connection {ConnectionId}: {Error}",
                Context.ConnectionId, ex.Message);
            return null;
        }
    }

    // Método para generar GUID determinista (siempre el mismo para el mismo input)
    private Guid GenerateDeterministicGuid(string input)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }

    // Obtener el nombre del usuario actual
    protected string? GetCurrentUserName()
    {
        try
        {
            return Context.User?.FindFirst("name")?.Value ??
                   Context.User?.FindFirst("username")?.Value ??
                   Context.User?.FindFirst("userName")?.Value ??
                   Context.User?.FindFirst(ClaimTypes.Name)?.Value ??
                   Context.User?.FindFirst("preferred_username")?.Value ??
                   Context.User?.FindFirst("email")?.Value ??
                   Context.User?.FindFirst(ClaimTypes.Email)?.Value ??
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

            // DEBUG: Si no está autenticado, mostrar información del contexto
            _logger.LogWarning("[BaseHub] User object: {User}", Context.User != null ? "EXISTS" : "NULL");
            _logger.LogWarning("[BaseHub] Identity object: {Identity}", Context.User?.Identity != null ? "EXISTS" : "NULL");
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