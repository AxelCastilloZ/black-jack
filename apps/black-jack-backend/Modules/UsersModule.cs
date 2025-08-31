using black_jack_backend.Entities;
using black_jack_backend.Data;
using Microsoft.EntityFrameworkCore;

namespace black_jack_backend.Modules;

public static class UsersModule
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}

public interface IUserService
{
    Task<decimal> GetBalanceAsync(int userId);
    Task UpdateBalanceAsync(int userId, decimal newBalance);
}

public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;

    public UserService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetBalanceAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user?.Balance ?? 0;
    }

    public async Task UpdateBalanceAsync(int userId, decimal newBalance)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.Balance = newBalance;
            await _context.SaveChangesAsync();
        }
    }
}