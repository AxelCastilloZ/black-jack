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
        // DbContexts
        services.AddDbContext<IdentityDbContext>(o =>
            o.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
        services.AddDbContext<ApplicationDbContext>(o =>
            o.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

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

        // JWT Bearer - CORREGIDO para SignalR
        var jwtKey = configuration["JwtSettings:Key"] ?? "default-key-for-development-only-not-secure";
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };

                // CORREGIDO: Configuración para SignalR
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        // Para SignalR, obtener token del query string
                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/hubs") || path.StartsWithSegments("/hub")))
                        {
                            context.Token = accessToken;
                        }

                        // Para requests normales, también intentar cookies como fallback
                        if (string.IsNullOrEmpty(context.Token) &&
                            context.Request.Cookies.TryGetValue("auth", out var cookieToken) &&
                            !string.IsNullOrWhiteSpace(cookieToken))
                        {
                            context.Token = cookieToken;
                        }

                        return Task.CompletedTask;
                    },

                    OnAuthenticationFailed = context =>
                    {
                        Console.WriteLine($"JWT Authentication failed: {context.Exception?.Message}");
                        if (context.Request.Path.StartsWithSegments("/hubs"))
                        {
                            Console.WriteLine($"SignalR auth failed for path: {context.Request.Path}");
                            Console.WriteLine($"Query token present: {!string.IsNullOrEmpty(context.Request.Query["access_token"])}");
                        }
                        return Task.CompletedTask;
                    },

                    OnTokenValidated = context =>
                    {
                        Console.WriteLine($"JWT Token validated for: {context.Principal?.Identity?.Name}");
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        services.AddHttpContextAccessor();

        // Game Services
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IGameRoomService, GameRoomService>();
        services.AddScoped<IDealerService, DealerService>();
        services.AddScoped<IHandEvaluationService, HandEvaluationService>();

        // User Services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();

        // Betting Services
        services.AddScoped<IBettingService, BettingService>();
        services.AddScoped<IPayoutService, PayoutService>();

        // Table Services
        services.AddScoped<ITableService, TableService>();

        // Common Services
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        // Repositories
        services.AddScoped<ITableRepository, TableRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IGameRoomRepository, GameRoomRepository>();
        services.AddScoped<IRoomPlayerRepository, RoomPlayerRepository>();
        services.AddScoped<IHandRepository, HandRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // Utility Services
        services.AddScoped<IDateTime, DateTimeService>();
        services.AddScoped<ICurrentUser, CurrentUserService>();

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
            var p = _http.HttpContext?.User;
            var playerIdClaim = p?.FindFirst("playerId")?.Value ?? p?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(playerIdClaim) || !Guid.TryParse(playerIdClaim, out var guidValue))
            {
                return null;
            }

            try
            {
                return BlackJack.Domain.Models.Users.PlayerId.From(guidValue);
            }
            catch
            {
                return null;
            }
        }
    }

    public string? UserName
    {
        get
        {
            var p = _http.HttpContext?.User;
            return p?.FindFirst("name")?.Value
                ?? p?.FindFirst(ClaimTypes.Name)?.Value
                ?? p?.Identity?.Name;
        }
    }

    public bool IsAuthenticated => _http.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}