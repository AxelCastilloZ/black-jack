using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using BlackJack.Data.Context;
using BlackJack.Data.Identity;
using BlackJack.Services.Game;
using BlackJack.Services.User;
using BlackJack.Services.Betting;
using BlackJack.Services.Table;
using BlackJack.Services.Common;
using BlackJack.Data.Repositories.Game;
using BlackJack.Data.Repositories.Users;

namespace BlackJackGame.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Starting service registration...");

        // DbContexts
        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Configuring DbContexts...");
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Connection string present: {!string.IsNullOrEmpty(connectionString)}");

        services.AddDbContext<IdentityDbContext>(o =>
            o.UseSqlServer(connectionString));
        services.AddDbContext<ApplicationDbContext>(o =>
            o.UseSqlServer(connectionString));

        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Configuring Identity...");
        // Identity
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 6;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddDefaultTokenProviders();

        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Configuring JWT Bearer...");
        // JWT Bearer - CORREGIDO para SignalR
        var jwtKey = configuration["JwtSettings:Key"] ?? "default-key-for-development-only-not-secure";
        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] JWT Key present: {!string.IsNullOrEmpty(jwtKey)}");
        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] JWT Key length: {jwtKey.Length}");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Configuring JWT Bearer options...");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero,
                    ValidateLifetime = true
                };

                // CRÍTICO: Configuración para SignalR - DEBE ESTAR PRESENTE
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        Console.WriteLine($"[JWT-DEBUG] OnMessageReceived called");
                        Console.WriteLine($"[JWT-DEBUG] Path: {context.Request.Path}");

                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

                        Console.WriteLine($"[JWT-DEBUG] Query access_token present: {!string.IsNullOrEmpty(accessToken)}");
                        Console.WriteLine($"[JWT-DEBUG] Auth header present: {!string.IsNullOrEmpty(authHeader)}");

                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            Console.WriteLine($"[JWT-DEBUG] Query access_token length: {accessToken.ToString().Length}");
                            Console.WriteLine($"[JWT-DEBUG] Query access_token preview: {accessToken.ToString().Substring(0, Math.Min(50, accessToken.ToString().Length))}...");
                        }

                        // CRÍTICO: Para SignalR, obtener token del query string
                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/hubs") || path.StartsWithSegments("/hub")))
                        {
                            context.Token = accessToken;
                            Console.WriteLine($"[JWT-DEBUG] TOKEN SET FROM QUERY for SignalR path");
                            Console.WriteLine($"[JWT-DEBUG] This will enable Context.User in SignalR hubs");
                        }
                        else if (!string.IsNullOrEmpty(accessToken))
                        {
                            Console.WriteLine($"[JWT-DEBUG] Query token present but NOT a SignalR path");
                        }
                        else
                        {
                            Console.WriteLine($"[JWT-DEBUG] No query token found");
                        }

                        // Para requests normales, también intentar cookies como fallback
                        if (string.IsNullOrEmpty(context.Token) &&
                            context.Request.Cookies.TryGetValue("auth", out var cookieToken) &&
                            !string.IsNullOrWhiteSpace(cookieToken))
                        {
                            context.Token = cookieToken;
                            Console.WriteLine($"[JWT-DEBUG] Token set from cookie as fallback");
                        }

                        Console.WriteLine($"[JWT-DEBUG] Final context.Token present: {!string.IsNullOrEmpty(context.Token)}");
                        return Task.CompletedTask;
                    },

                    OnAuthenticationFailed = context =>
                    {
                        Console.WriteLine($"[JWT-DEBUG] OnAuthenticationFailed called");
                        Console.WriteLine($"[JWT-DEBUG] Path: {context.Request.Path}");
                        Console.WriteLine($"[JWT-DEBUG] Exception: {context.Exception?.Message}");
                        Console.WriteLine($"[JWT-DEBUG] Exception Type: {context.Exception?.GetType().Name}");

                        // Obtener tokens del request para debug
                        var queryToken = context.Request.Query["access_token"].FirstOrDefault();
                        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

                        if (!string.IsNullOrEmpty(queryToken))
                        {
                            Console.WriteLine($"[JWT-DEBUG] Query token that failed length: {queryToken.Length}");
                            Console.WriteLine($"[JWT-DEBUG] Query token that failed preview: {queryToken.Substring(0, Math.Min(100, queryToken.Length))}...");
                        }
                        else
                        {
                            Console.WriteLine($"[JWT-DEBUG] No query token was provided");
                        }

                        if (context.Request.Path.StartsWithSegments("/hubs"))
                        {
                            Console.WriteLine($"[JWT-DEBUG] *** SIGNALR AUTHENTICATION FAILED ***");
                            if (!string.IsNullOrEmpty(queryToken))
                            {
                                Console.WriteLine($"[JWT-DEBUG] SignalR query token was: {queryToken.Substring(0, Math.Min(50, queryToken.Length))}...");
                            }
                            else
                            {
                                Console.WriteLine($"[JWT-DEBUG] No query token found for SignalR authentication");
                            }
                        }

                        return Task.CompletedTask;
                    },

                    OnTokenValidated = context =>
                    {
                        Console.WriteLine($"[JWT-DEBUG] OnTokenValidated called - SUCCESS");
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
                            Console.WriteLine($"[JWT-DEBUG] SignalR Context.User will now have claims");
                        }

                        return Task.CompletedTask;
                    },

                    OnChallenge = context =>
                    {
                        Console.WriteLine($"[JWT-DEBUG] OnChallenge called");
                        Console.WriteLine($"[JWT-DEBUG] Path: {context.Request.Path}");
                        Console.WriteLine($"[JWT-DEBUG] Error: {context.Error ?? "NULL"}");
                        Console.WriteLine($"[JWT-DEBUG] AuthenticateFailure: {context.AuthenticateFailure?.Message ?? "NULL"}");

                        if (context.Request.Path.StartsWithSegments("/hubs"))
                        {
                            Console.WriteLine($"[JWT-DEBUG] *** SIGNALR CHALLENGE - AUTH REQUIRED ***");
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Adding Authorization...");
        services.AddAuthorization();
        services.AddHttpContextAccessor();

        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Registering Game Services...");
        // Game Services
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IGameRoomService, GameRoomService>();
        services.AddScoped<IDealerService, DealerService>();
        services.AddScoped<IHandEvaluationService, HandEvaluationService>();

        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Registering User Services...");
        // User Services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();

        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Registering Betting Services...");
        // Betting Services
        services.AddScoped<IBettingService, BettingService>();
        services.AddScoped<IPayoutService, PayoutService>();

        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Registering Table Services...");
        // Table Services
        services.AddScoped<ITableService, TableService>();

        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Registering Common Services...");
        // Common Services
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Registering Repositories...");
        // Repositories
        services.AddScoped<ITableRepository, TableRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IGameRoomRepository, GameRoomRepository>();
        services.AddScoped<IRoomPlayerRepository, RoomPlayerRepository>();
        services.AddScoped<IHandRepository, HandRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Registering Utility Services...");
        // Utility Services
        services.AddScoped<IDateTime, DateTimeService>();
        services.AddScoped<ICurrentUser, CurrentUserService>();

        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Service registration completed successfully");
        return services;
    }
}

// Interfaces de utilidad
public interface IDateTime
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
}

public interface ICurrentUser
{
    BlackJack.Domain.Models.Users.PlayerId? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
}

// Implementaciones de utilidad
public class DateTimeService : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now => DateTime.Now;
}

public class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(IHttpContextAccessor http) => _http = http;

    public BlackJack.Domain.Models.Users.PlayerId? UserId
    {
        get
        {
            Console.WriteLine($"[CURRENT-USER-DEBUG] Getting UserId...");
            var p = _http.HttpContext?.User;

            if (p == null)
            {
                Console.WriteLine($"[CURRENT-USER-DEBUG] HttpContext.User is null");
                return null;
            }

            Console.WriteLine($"[CURRENT-USER-DEBUG] User authenticated: {p.Identity?.IsAuthenticated ?? false}");
            Console.WriteLine($"[CURRENT-USER-DEBUG] User claims count: {p.Claims?.Count() ?? 0}");

            var playerIdClaim = p?.FindFirst("playerId")?.Value ?? p?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Console.WriteLine($"[CURRENT-USER-DEBUG] PlayerId claim: {playerIdClaim ?? "NULL"}");

            if (string.IsNullOrEmpty(playerIdClaim) || !Guid.TryParse(playerIdClaim, out var guidValue))
            {
                Console.WriteLine($"[CURRENT-USER-DEBUG] Invalid or missing playerId claim");
                return null;
            }

            try
            {
                var playerId = BlackJack.Domain.Models.Users.PlayerId.From(guidValue);
                Console.WriteLine($"[CURRENT-USER-DEBUG] PlayerId created successfully: {playerId}");
                return playerId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CURRENT-USER-DEBUG] Error creating PlayerId: {ex.Message}");
                return null;
            }
        }
    }

    public string? UserName
    {
        get
        {
            Console.WriteLine($"[CURRENT-USER-DEBUG] Getting UserName...");
            var p = _http.HttpContext?.User;

            if (p == null)
            {
                Console.WriteLine($"[CURRENT-USER-DEBUG] HttpContext.User is null for UserName");
                return null;
            }

            var userName = p?.FindFirst("name")?.Value
                ?? p?.FindFirst(ClaimTypes.Name)?.Value
                ?? p?.Identity?.Name;

            Console.WriteLine($"[CURRENT-USER-DEBUG] UserName resolved: {userName ?? "NULL"}");
            return userName;
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            var isAuth = _http.HttpContext?.User.Identity?.IsAuthenticated ?? false;
            Console.WriteLine($"[CURRENT-USER-DEBUG] IsAuthenticated: {isAuth}");
            return isAuth;
        }
    }
}