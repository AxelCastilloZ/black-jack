using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
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

        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Configuring Identity WITHOUT Authentication...");
        // CAMBIO CRÍTICO: AddIdentityCore en lugar de AddIdentity
        // AddIdentityCore NO registra Authentication automáticamente
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 6;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole>() // Agregar roles manualmente
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddDefaultTokenProviders();

        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Identity configured WITHOUT overriding JWT Authentication");

        Console.WriteLine($"[SERVICE-EXTENSIONS-DEBUG] Adding HttpContextAccessor...");
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