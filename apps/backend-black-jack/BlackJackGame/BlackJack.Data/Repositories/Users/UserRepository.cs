using Microsoft.EntityFrameworkCore;
using BlackJack.Domain.Models.Users;
using BlackJack.Data.Context;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Users;

public class UserRepository : Repository<UserProfile>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<UserProfile?> GetByPlayerIdAsync(PlayerId playerId)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.PlayerId == playerId);
    }

    public async Task<UserProfile?> GetByEmailAsync(string email)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _dbSet.AnyAsync(u => u.Email == email);
    }
}