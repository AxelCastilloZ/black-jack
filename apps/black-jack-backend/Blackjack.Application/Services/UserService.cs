using Blackjack.Application.Interfaces;
using Blackjack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Blackjack.Application.Services;

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
