// BlackJack.Data.Repositories.Game/PlayerRepository.cs - CORREGIDO CON GUID
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
        // Compare by underlying GUID to avoid EF Core owned-type member access issues
        return await _dbSet
            .FirstOrDefaultAsync(p => p.PlayerId.Value == playerId.Value);
    }

    public async Task<List<Player>> GetPlayersByTableAsync(Guid tableId)
    {
        // Como Seat no tiene BlackjackTableId directo, necesitamos hacer el query desde BlackjackTable
        var table = await _context.Set<BlackjackTable>()
            .Include(t => t.Seats)
            .ThenInclude(s => s.Player)
            .FirstOrDefaultAsync(t => t.Id == tableId);

        if (table == null)
            return new List<Player>();

        return table.Seats
            .Where(s => s.IsOccupied && s.Player != null)
            .Select(s => s.Player!)
            .ToList();
    }
}