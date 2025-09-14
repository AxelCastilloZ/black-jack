using Microsoft.EntityFrameworkCore;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Data.Context;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Game;

public class TableRepository : Repository<BlackjackTable>, ITableRepository
{
    public TableRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<List<BlackjackTable>> GetAvailableTablesAsync()
    {
        return await _dbSet
            .Where(t => t.Status == Domain.Enums.GameStatus.WaitingForPlayers)
            .Include(t => t.Seats)
            .ToListAsync();
    }

    public async Task<BlackjackTable?> GetTableWithPlayersAsync(TableId tableId)
    {
        return await _dbSet
            .Include(t => t.Seats)
            .ThenInclude(s => s.Player)
            .Include(t => t.Spectators)
            .FirstOrDefaultAsync(t => t.Id == tableId.Value);
    }

    public async Task<List<BlackjackTable>> GetTablesByStatusAsync(Domain.Enums.GameStatus status)
    {
        return await _dbSet
            .Where(t => t.Status == status)
            .ToListAsync();
    }
}