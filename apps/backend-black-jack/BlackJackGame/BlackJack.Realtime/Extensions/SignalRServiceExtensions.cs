// SignalRServiceExtensions.cs - CONFIGURACIÓN DE TIMEOUTS CORREGIDA
using BlackJack.Domain.Common;
using BlackJack.Realtime.EventHandlers;
using BlackJack.Realtime.Hubs;
using BlackJack.Realtime.Services;
using BlackJack.Services.Common;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;

namespace BlackJack.Realtime.Extensions;

public static class SignalRServiceExtensions
{
    /// <summary>
    /// Registra SignalR con configuración JWT completa para producción
    /// </summary>
    public static IServiceCollection AddBlackJackSignalR(this IServiceCollection services, IConfiguration configuration)
    {
        Console.WriteLine($"[SIGNALR-EXTENSIONS-DEBUG] Adding BlackJack SignalR for PRODUCTION...");

        // PASO 1: Configurar JWT Bearer Authentication (CRÍTICO PARA SIGNALR)
        AddJwtAuthenticationForSignalR(services, configuration);

        // PASO 2: Configurar SignalR con opciones optimizadas para producción
        services.AddSignalR(options =>
        {
            Console.WriteLine($"[SIGNALR-EXTENSIONS-DEBUG] Configuring SignalR options for production...");
            options.EnableDetailedErrors = false;

            // CORREGIDO: Timeouts optimizados para producción
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);      // Ping cada 15s
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);  // Timeout después de 60s sin respuesta
            options.HandshakeTimeout = TimeSpan.FromSeconds(15);       // Timeout de handshake

            options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
            options.StreamBufferCapacity = 10;
        })
        .AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.PayloadSerializerOptions.WriteIndented = false;
        });

        // PASO 3: Registrar servicios de SignalR
        RegisterSignalRServices(services);

        Console.WriteLine($"[SIGNALR-EXTENSIONS-DEBUG] BlackJack SignalR configured successfully for PRODUCTION");
        return services;
    }

    /// <summary>
    /// Registra SignalR con configuración JWT completa para desarrollo
    /// CORREGIDO: Timeouts apropiados que evitan desconexiones frecuentes
    /// </summary>
    public static IServiceCollection AddBlackJackSignalRDevelopment(this IServiceCollection services, IConfiguration configuration)
    {
        Console.WriteLine($"[SIGNALR-EXTENSIONS-DEBUG] Adding BlackJack SignalR for DEVELOPMENT...");

        // PASO 1: Configurar JWT Bearer Authentication (CRÍTICO PARA SIGNALR)
        AddJwtAuthenticationForSignalR(services, configuration);

        // PASO 2: Configurar SignalR con opciones CORREGIDAS para desarrollo
        services.AddSignalR(options =>
        {
            Console.WriteLine($"[SIGNALR-EXTENSIONS-DEBUG] Configuring SignalR options for development...");
            options.EnableDetailedErrors = true; // Más detalles en desarrollo

            // CORREGIDO: Timeouts que eliminan desconexiones frecuentes
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);      // Era 30s - CAUSABA EL PROBLEMA
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);  // Era 2min - Reducido pero estable
            options.HandshakeTimeout = TimeSpan.FromSeconds(30);       // Más generoso para desarrollo

            options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB para desarrollo
        })
        .AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.PayloadSerializerOptions.WriteIndented = true; // Formato legible en desarrollo
        });

        // PASO 3: Registrar servicios de SignalR
        RegisterSignalRServices(services);

        Console.WriteLine($"[SIGNALR-EXTENSIONS-DEBUG] BlackJack SignalR configured successfully for DEVELOPMENT");
        Console.WriteLine($"[SIGNALR-EXTENSIONS-DEBUG] KeepAlive: 15s, ClientTimeout: 60s (FIXED TIMEOUT ISSUES)");
        return services;
    }

    /// <summary>
    /// NUEVO: Configura JWT Authentication específicamente para SignalR
    /// Esta configuración es CRÍTICA para que los claims lleguen a los hubs
    /// </summary>
    private static void AddJwtAuthenticationForSignalR(IServiceCollection services, IConfiguration configuration)
    {
        Console.WriteLine($"[JWT-SIGNALR-DEBUG] === CONFIGURING JWT FOR SIGNALR ===");

        var jwtKey = configuration["JwtSettings:Key"] ?? "default-key-for-development-only-not-secure";
        Console.WriteLine($"[JWT-SIGNALR-DEBUG] JWT Key present: {!string.IsNullOrEmpty(jwtKey)}");
        Console.WriteLine($"[JWT-SIGNALR-DEBUG] JWT Key length: {jwtKey.Length}");

        // Configurar Authentication con JWT Bearer
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                Console.WriteLine($"[JWT-SIGNALR-DEBUG] Configuring JWT Bearer options for SignalR...");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero,
                    ValidateLifetime = true
                };

                // CONFIGURACIÓN CRÍTICA: Events para SignalR
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        Console.WriteLine($"[JWT-DEBUG] === OnMessageReceived STARTED ===");
                        Console.WriteLine($"[JWT-DEBUG] Request Path: {context.Request.Path}");
                        Console.WriteLine($"[JWT-DEBUG] Request Method: {context.Request.Method}");

                        try
                        {
                            var path = context.HttpContext.Request.Path;
                            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                            var accessToken = context.Request.Query["access_token"].FirstOrDefault();

                            Console.WriteLine($"[JWT-DEBUG] Auth header present: {!string.IsNullOrEmpty(authHeader)}");
                            Console.WriteLine($"[JWT-DEBUG] Query access_token present: {!string.IsNullOrEmpty(accessToken)}");

                            if (!string.IsNullOrEmpty(accessToken))
                            {
                                Console.WriteLine($"[JWT-DEBUG] Query access_token length: {accessToken.Length}");
                                Console.WriteLine($"[JWT-DEBUG] Query access_token preview: {accessToken.Substring(0, Math.Min(50, accessToken.Length))}...");
                            }

                            // Verificación específica de rutas SignalR
                            var isSignalRPath = path.StartsWithSegments("/hubs") ||
                                               path.StartsWithSegments("/hub") ||
                                               path.ToString().Contains("/hubs/");

                            Console.WriteLine($"[JWT-DEBUG] Is SignalR path: {isSignalRPath}");

                            // Procesar token para SignalR cuando esté presente
                            if (!string.IsNullOrEmpty(accessToken) && isSignalRPath)
                            {
                                var cleanToken = accessToken.Trim();
                                if (cleanToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                                {
                                    cleanToken = cleanToken.Substring(7).Trim();
                                }

                                context.Token = cleanToken;
                                Console.WriteLine($"[JWT-DEBUG] ✅ TOKEN SET FROM QUERY for SignalR");
                                Console.WriteLine($"[JWT-DEBUG] Clean token length: {cleanToken.Length}");
                            }
                            else if (!string.IsNullOrEmpty(accessToken))
                            {
                                Console.WriteLine($"[JWT-DEBUG] Query token present but NOT a SignalR path");
                            }
                            else if (isSignalRPath)
                            {
                                Console.WriteLine($"[JWT-DEBUG] SignalR path but NO query token found");
                            }
                            else
                            {
                                Console.WriteLine($"[JWT-DEBUG] Regular HTTP request - no special handling needed");
                            }

                            // Fallback para cookies si es necesario
                            if (string.IsNullOrEmpty(context.Token) &&
                                context.Request.Cookies.TryGetValue("auth", out var cookieToken) &&
                                !string.IsNullOrWhiteSpace(cookieToken))
                            {
                                context.Token = cookieToken;
                                Console.WriteLine($"[JWT-DEBUG] Token set from cookie as fallback");
                            }

                            Console.WriteLine($"[JWT-DEBUG] Final context.Token assigned: {!string.IsNullOrEmpty(context.Token)}");
                            Console.WriteLine($"[JWT-DEBUG] === OnMessageReceived COMPLETED ===");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[JWT-DEBUG] EXCEPTION in OnMessageReceived: {ex.Message}");
                            Console.WriteLine($"[JWT-DEBUG] Exception StackTrace: {ex.StackTrace}");
                        }

                        return Task.CompletedTask;
                    },

                    OnAuthenticationFailed = context =>
                    {
                        Console.WriteLine($"[JWT-DEBUG] === OnAuthenticationFailed STARTED ===");
                        Console.WriteLine($"[JWT-DEBUG] Path: {context.Request.Path}");
                        Console.WriteLine($"[JWT-DEBUG] Exception: {context.Exception?.Message ?? "NULL"}");
                        Console.WriteLine($"[JWT-DEBUG] Exception Type: {context.Exception?.GetType().Name ?? "NULL"}");

                        if (context.Request.Path.StartsWithSegments("/hubs"))
                        {
                            Console.WriteLine($"[JWT-DEBUG] *** SIGNALR AUTHENTICATION FAILED ***");
                            Console.WriteLine($"[JWT-DEBUG] This is CRITICAL - SignalR won't have authenticated user");

                            var queryToken = context.Request.Query["access_token"].FirstOrDefault();
                            if (!string.IsNullOrEmpty(queryToken))
                            {
                                Console.WriteLine($"[JWT-DEBUG] SignalR had query token but validation failed");
                                Console.WriteLine($"[JWT-DEBUG] Check token format and expiration");
                                Console.WriteLine($"[JWT-DEBUG] Failed token preview: {queryToken.Substring(0, Math.Min(100, queryToken.Length))}...");
                            }
                            else
                            {
                                Console.WriteLine($"[JWT-DEBUG] SignalR had no query token");
                            }
                        }

                        Console.WriteLine($"[JWT-DEBUG] === OnAuthenticationFailed COMPLETED ===");
                        return Task.CompletedTask;
                    },

                    OnTokenValidated = context =>
                    {
                        Console.WriteLine($"[JWT-DEBUG] === OnTokenValidated STARTED - SUCCESS ===");
                        Console.WriteLine($"[JWT-DEBUG] Path: {context.Request.Path}");
                        Console.WriteLine($"[JWT-DEBUG] User authenticated: {context.Principal?.Identity?.IsAuthenticated ?? false}");
                        Console.WriteLine($"[JWT-DEBUG] User name: {context.Principal?.Identity?.Name ?? "NULL"}");

                        var claims = context.Principal?.Claims?.ToList() ?? new List<Claim>();
                        Console.WriteLine($"[JWT-DEBUG] Claims count: {claims.Count}");

                        foreach (var claim in claims)
                        {
                            Console.WriteLine($"[JWT-DEBUG] Claim: {claim.Type} = {claim.Value}");
                        }

                        var playerId = context.Principal?.FindFirst("playerId")?.Value ??
                                       context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                                       "NOT_FOUND";
                        Console.WriteLine($"[JWT-DEBUG] PlayerId extracted: {playerId}");

                        if (context.Request.Path.StartsWithSegments("/hubs"))
                        {
                            Console.WriteLine($"[JWT-DEBUG] *** SIGNALR AUTHENTICATION SUCCESS ***");
                            Console.WriteLine($"[JWT-DEBUG] SignalR Context.User will now have {claims.Count} claims");
                            Console.WriteLine($"[JWT-DEBUG] BaseHub.GetCurrentPlayerId() should work now");
                        }

                        Console.WriteLine($"[JWT-DEBUG] === OnTokenValidated COMPLETED ===");
                        return Task.CompletedTask;
                    },

                    OnChallenge = context =>
                    {
                        Console.WriteLine($"[JWT-DEBUG] === OnChallenge STARTED ===");
                        Console.WriteLine($"[JWT-DEBUG] Path: {context.Request.Path}");
                        Console.WriteLine($"[JWT-DEBUG] Error: {context.Error ?? "NULL"}");
                        Console.WriteLine($"[JWT-DEBUG] AuthenticateFailure: {context.AuthenticateFailure?.Message ?? "NULL"}");

                        if (context.Request.Path.StartsWithSegments("/hubs"))
                        {
                            Console.WriteLine($"[JWT-DEBUG] *** SIGNALR CHALLENGE - AUTH REQUIRED ***");
                            Console.WriteLine($"[JWT-DEBUG] SignalR authentication was challenged");
                        }

                        Console.WriteLine($"[JWT-DEBUG] === OnChallenge COMPLETED ===");
                        return Task.CompletedTask;
                    }
                };
            });

        // Agregar Authorization
        services.AddAuthorization();

        Console.WriteLine($"[JWT-SIGNALR-DEBUG] === JWT FOR SIGNALR CONFIGURED SUCCESSFULLY ===");
    }

    /// <summary>
    /// Registra todos los servicios de SignalR y componentes de tiempo real
    /// </summary>
    private static void RegisterSignalRServices(IServiceCollection services)
    {
        Console.WriteLine($"[SIGNALR-EXTENSIONS-DEBUG] Registering SignalR services...");

        // Servicios de SignalR
        services.AddScoped<IConnectionManager, ConnectionManager>();
        services.AddScoped<ISignalRNotificationService, SignalRNotificationService>();

        // Event handlers de dominio para SignalR
        RegisterDomainEventHandlers(services);

        Console.WriteLine($"[SIGNALR-EXTENSIONS-DEBUG] SignalR services registered successfully");
    }

    /// <summary>
    /// Configuración para producción con Redis (opcional)
    /// </summary>
    public static IServiceCollection AddBlackJackSignalRWithRedis(this IServiceCollection services, IConfiguration configuration, string redisConnectionString)
    {
        Console.WriteLine($"[SIGNALR-EXTENSIONS-DEBUG] Adding BlackJack SignalR with Redis...");

        // JWT Authentication primero
        AddJwtAuthenticationForSignalR(services, configuration);

        // SignalR con Redis
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = false;
            // CORREGIDO: Mismos timeouts para Redis
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        })
        .AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.PayloadSerializerOptions.WriteIndented = false;
        });
        // Redis se puede agregar más tarde: .AddStackExchangeRedis(redisConnectionString)

        RegisterSignalRServices(services);
        return services;
    }

    /// <summary>
    /// Registra todos los event handlers de dominio para SignalR
    /// </summary>
    private static void RegisterDomainEventHandlers(IServiceCollection services)
    {
        Console.WriteLine($"[SIGNALR-EXTENSIONS-DEBUG] Registering domain event handlers...");

        // Event handlers para eventos de GameRoom
        services.AddScoped<IDomainEventHandler<PlayerJoinedRoomEvent>, PlayerJoinedRoomEventHandler>();
        services.AddScoped<IDomainEventHandler<PlayerLeftRoomEvent>, PlayerLeftRoomEventHandler>();
        services.AddScoped<IDomainEventHandler<SpectatorJoinedEvent>, SpectatorJoinedEventHandler>();
        services.AddScoped<IDomainEventHandler<SpectatorLeftEvent>, SpectatorLeftEventHandler>();
        services.AddScoped<IDomainEventHandler<GameStartedEvent>, GameStartedEventHandler>();
        services.AddScoped<IDomainEventHandler<TurnChangedEvent>, TurnChangedEventHandler>();
        services.AddScoped<IDomainEventHandler<GameEndedEvent>, GameEndedEventHandler>();

        Console.WriteLine($"[SIGNALR-EXTENSIONS-DEBUG] Domain event handlers registered successfully");
    }

    /// <summary>
    /// Configurar grupos y políticas de autorización personalizadas
    /// </summary>
    public static IServiceCollection AddSignalRAuthorization(this IServiceCollection services)
    {
        Console.WriteLine($"[SIGNALR-EXTENSIONS-DEBUG] Adding SignalR authorization policies...");

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

        Console.WriteLine($"[SIGNALR-EXTENSIONS-DEBUG] SignalR authorization policies configured successfully");
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