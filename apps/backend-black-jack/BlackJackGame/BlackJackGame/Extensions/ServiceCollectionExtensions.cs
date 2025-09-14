using Microsoft.EntityFrameworkCore;
using BlackJack.Data.Context;
using BlackJack.Services.Game;
using BlackJack.Services.User;
using BlackJack.Services.Betting;
using BlackJack.Services.Cards;
using BlackJack.Services.Table;
using BlackJack.Services.Common;
using BlackJack.Data.Repositories.Game;
using BlackJack.Data.Repositories.Users;
using BlackJack.Realtime.Hubs;

namespace BlackJackGame.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

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

        // Common services
        services.AddScoped<IDateTime, DateTimeService>();
        services.AddScoped<ICurrentUser, CurrentUserService>();

        // SignalR
        services.AddSignalR();

        return services;
    }
}

// Implementaciones temporales para IDateTime e ICurrentUser
public class DateTimeService : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now => DateTime.Now;
}

public class CurrentUserService : ICurrentUser
{
    public BlackJack.Domain.Models.Users.PlayerId? UserId => null;
    public string? UserName => null;
    public bool IsAuthenticated => false;
}