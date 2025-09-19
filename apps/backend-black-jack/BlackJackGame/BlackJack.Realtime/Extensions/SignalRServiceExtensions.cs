// SignalRServiceExtensions.cs - En BlackJack.Realtime/Extensions/
using BlackJack.Domain.Common;
using BlackJack.Realtime.EventHandlers;
using BlackJack.Realtime.Hubs;
using BlackJack.Realtime.Services;
using BlackJack.Services.Common;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlackJack.Realtime.Extensions;

public static class SignalRServiceExtensions
{
    /// <summary>
    /// Registra todos los servicios de SignalR y componentes de tiempo real
    /// </summary>
    public static IServiceCollection AddBlackJackSignalR(this IServiceCollection services)
    {
        // Configurar SignalR con opciones optimizadas
        services.AddSignalR(options =>
        {
            // Configuraciones de producción
            options.EnableDetailedErrors = false; // Cambiar a true solo en desarrollo
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            options.HandshakeTimeout = TimeSpan.FromSeconds(15);
            options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
            options.StreamBufferCapacity = 10;
        })
        .AddJsonProtocol(options =>
        {
            // Configurar serialización JSON
            options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.PayloadSerializerOptions.WriteIndented = false;
        });

        // Registrar servicios de SignalR
        services.AddScoped<IConnectionManager, ConnectionManager>();
        services.AddScoped<ISignalRNotificationService, SignalRNotificationService>();

        // Registrar event handlers de dominio para SignalR
        RegisterDomainEventHandlers(services);

        return services;
    }

    /// <summary>
    /// Configuración adicional para desarrollo
    /// </summary>
    public static IServiceCollection AddBlackJackSignalRDevelopment(this IServiceCollection services)
    {
        services.AddSignalR(options =>
        {
            // Configuraciones más permisivas para desarrollo
            options.EnableDetailedErrors = true;
            options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
            options.HandshakeTimeout = TimeSpan.FromSeconds(30);
            options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB para desarrollo
        });

        services.AddScoped<IConnectionManager, ConnectionManager>();
        services.AddScoped<ISignalRNotificationService, SignalRNotificationService>();

        RegisterDomainEventHandlers(services);

        return services;
    }

    /// <summary>
    /// Configuración para producción con Redis (opcional)
    /// </summary>
    public static IServiceCollection AddBlackJackSignalRWithRedis(this IServiceCollection services, string redisConnectionString)
    {
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = false;
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        })
        .AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.PayloadSerializerOptions.WriteIndented = false;
        });
        // Redis se puede agregar más tarde: .AddStackExchangeRedis(redisConnectionString)

        services.AddScoped<IConnectionManager, ConnectionManager>();
        services.AddScoped<ISignalRNotificationService, SignalRNotificationService>();

        RegisterDomainEventHandlers(services);

        return services;
    }

    /// <summary>
    /// Registra todos los event handlers de dominio para SignalR
    /// </summary>
    private static void RegisterDomainEventHandlers(IServiceCollection services)
    {
        // Event handlers para eventos de GameRoom
        services.AddScoped<IDomainEventHandler<PlayerJoinedRoomEvent>, PlayerJoinedRoomEventHandler>();
        services.AddScoped<IDomainEventHandler<PlayerLeftRoomEvent>, PlayerLeftRoomEventHandler>();
        services.AddScoped<IDomainEventHandler<SpectatorJoinedEvent>, SpectatorJoinedEventHandler>();
        services.AddScoped<IDomainEventHandler<SpectatorLeftEvent>, SpectatorLeftEventHandler>();
        services.AddScoped<IDomainEventHandler<GameStartedEvent>, GameStartedEventHandler>();
        services.AddScoped<IDomainEventHandler<TurnChangedEvent>, TurnChangedEventHandler>();
        services.AddScoped<IDomainEventHandler<GameEndedEvent>, GameEndedEventHandler>();

        // Event handlers para eventos de juego (si los agregas después)
        // services.AddScoped<IDomainEventHandler<CardDealtEvent>, CardDealtEventHandler>();
        // services.AddScoped<IDomainEventHandler<PlayerActionEvent>, PlayerActionEventHandler>();
        // services.AddScoped<IDomainEventHandler<BetPlacedEvent>, BetPlacedEventHandler>();
    }

    /// <summary>
    /// Configurar grupos y políticas de autorización personalizadas
    /// </summary>
    public static IServiceCollection AddSignalRAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Política para acceso a GameHub
            options.AddPolicy("GameHubAccess", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("playerId"); // Debe tener PlayerId en el token
            });

            // Política para acceso a LobbyHub
            options.AddPolicy("LobbyHubAccess", policy =>
            {
                policy.RequireAuthenticatedUser();
            });

            // Política para administradores (opcional)
            options.AddPolicy("AdminAccess", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Admin");
            });
        });

        return services;
    }

    /// <summary>
    /// Configurar servicios de monitoreo y métricas para SignalR
    /// </summary>
    public static IServiceCollection AddSignalRMonitoring(this IServiceCollection services)
    {
        // Agregar métricas personalizadas
        services.AddSingleton<ISignalRMetrics, SignalRMetrics>();

        // Configurar logging específico para SignalR
        services.AddLogging(builder =>
        {
            builder.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Information);
            builder.AddFilter("BlackJack.Realtime", LogLevel.Information);
        });

        return services;
    }

    /// <summary>
    /// Configurar limpieza automática de conexiones obsoletas
    /// </summary>
    public static IServiceCollection AddConnectionCleanup(this IServiceCollection services)
    {
        services.AddHostedService<ConnectionCleanupService>();
        return services;
    }
}

#region Servicios auxiliares

/// <summary>
/// Interfaz para métricas de SignalR (opcional)
/// </summary>
public interface ISignalRMetrics
{
    void IncrementConnectionCount();
    void DecrementConnectionCount();
    void RecordRoomJoin(string roomCode);
    void RecordRoomLeave(string roomCode);
    void RecordMessageSent(string hubName, string methodName);
}

/// <summary>
/// Implementación básica de métricas
/// </summary>
public class SignalRMetrics : ISignalRMetrics
{
    private readonly ILogger<SignalRMetrics> _logger;
    private long _connectionCount = 0;

    public SignalRMetrics(ILogger<SignalRMetrics> logger)
    {
        _logger = logger;
    }

    public void IncrementConnectionCount()
    {
        Interlocked.Increment(ref _connectionCount);
        _logger.LogInformation("[Metrics] Connection count: {Count}", _connectionCount);
    }

    public void DecrementConnectionCount()
    {
        Interlocked.Decrement(ref _connectionCount);
        _logger.LogInformation("[Metrics] Connection count: {Count}", _connectionCount);
    }

    public void RecordRoomJoin(string roomCode)
    {
        _logger.LogInformation("[Metrics] Player joined room: {RoomCode}", roomCode);
    }

    public void RecordRoomLeave(string roomCode)
    {
        _logger.LogInformation("[Metrics] Player left room: {RoomCode}", roomCode);
    }

    public void RecordMessageSent(string hubName, string methodName)
    {
        _logger.LogDebug("[Metrics] Message sent - Hub: {HubName}, Method: {MethodName}", hubName, methodName);
    }
}

/// <summary>
/// Servicio de limpieza de conexiones obsoletas
/// </summary>
public class ConnectionCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ConnectionCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    public ConnectionCleanupService(IServiceProvider serviceProvider, ILogger<ConnectionCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var connectionManager = scope.ServiceProvider.GetRequiredService<IConnectionManager>();

                await connectionManager.CleanupStaleConnectionsAsync();

                _logger.LogDebug("[ConnectionCleanup] Cleanup cycle completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ConnectionCleanup] Error during cleanup cycle: {Error}", ex.Message);
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }
}

#endregion