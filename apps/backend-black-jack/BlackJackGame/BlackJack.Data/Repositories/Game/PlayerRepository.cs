using Microsoft.EntityFrameworkCore;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Data.Context;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Game;

public class PlayerRepository : Repository<Player>, IPlayerRepository
{
    public PlayerRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Player?> GetByPlayerIdAsync(PlayerId playerId)
    {
        return await _dbSet
            .Include(p => p.Hands)
            .FirstOrDefaultAsync(p => p.PlayerId == playerId);
    }

    public async Task<List<Player>> GetPlayersByTableAsync(TableId tableId)
    {
        return await _context.Seats
            .Where(s => s.IsOccupied)
            .Include(s => s.Player)
            .ThenInclude(p => p!.Hands)
            .Select(s => s.Player!)
            .ToListAsync();
    }
}