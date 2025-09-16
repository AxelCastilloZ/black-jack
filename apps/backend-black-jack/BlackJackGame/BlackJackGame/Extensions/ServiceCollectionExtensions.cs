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
using BlackJack.Services.Cards;
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

        // ===== JWT Bearer =====
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

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        // 1) SignalR: ?access_token=... en /hubs/*
                        var accessToken = ctx.Request.Query["access_token"];
                        var path = ctx.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            ctx.Token = accessToken!;
                            return Task.CompletedTask;
                        }

                        // 2) Cookie "auth" (opcional)
                        if (string.IsNullOrEmpty(ctx.Token) &&
                            ctx.Request.Cookies.TryGetValue("auth", out var cookieToken) &&
                            !string.IsNullOrWhiteSpace(cookieToken))
                        {
                            ctx.Token = cookieToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        services.AddHttpContextAccessor();

        // Services
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IDealerService, DealerService>();
        services.AddScoped<IHandEvaluationService, HandEvaluationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBettingService, BettingService>();
        services.AddScoped<IPayoutService, PayoutService>();
        services.AddScoped<IShuffleService, ShuffleService>();
        services.AddScoped<ITableService, TableService>();

        // Repositories
        services.AddScoped<ITableRepository, TableRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // Common
        services.AddScoped<IDateTime, DateTimeService>();
        services.AddScoped<ICurrentUser, CurrentUserService>();

        // SignalR
        services.AddSignalR();

        return services;
    }
}

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
            return playerIdClaim != null && Guid.TryParse(playerIdClaim, out var g)
                ? BlackJack.Domain.Models.Users.PlayerId.From(g)
                : null;
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
