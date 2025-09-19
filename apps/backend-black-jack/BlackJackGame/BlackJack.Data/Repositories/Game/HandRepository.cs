// HandRepository.cs
using Microsoft.EntityFrameworkCore;
using BlackJack.Domain.Models.Game;
using BlackJack.Data.Context;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Game;

public class HandRepository : Repository<Hand>, IHandRepository
{
    public HandRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<List<Hand>> GetPlayerHandsAsync(Guid playerId)
    {
        // Esto requerirá una relación específica entre Player y Hand
        // Por ahora, buscamos por los HandIds en Player
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.Id == playerId);

        if (player == null) return new List<Hand>();

        var handIds = player.HandIds;
        return await _dbSet
            .Where(h => handIds.Contains(h.Id))
            .ToListAsync();
    }

    public async Task<Hand?> GetDealerHandAsync(Guid tableId)
    {
        var table = await _context.BlackjackTables
            .FirstOrDefaultAsync(t => t.Id == tableId);

        if (table?.DealerHandId == null) return null;

        return await _dbSet
            .FirstOrDefaultAsync(h => h.Id == table.DealerHandId.Value);
    }

    public async Task<List<Hand>> GetHandsByTableAsync(Guid tableId)
    {
        // Obtener todas las manos relacionadas con una mesa
        var table = await _context.BlackjackTables
            .Include(t => t.Seats)
            .ThenInclude(s => s.Player)
            .FirstOrDefaultAsync(t => t.Id == tableId);

        if (table == null) return new List<Hand>();

        var allHandIds = new List<Guid>();

        // Agregar mano del dealer si existe
        if (table.DealerHandId.HasValue)
        {
            allHandIds.Add(table.DealerHandId.Value);
        }

        // Agregar manos de jugadores
        foreach (var seat in table.Seats.Where(s => s.IsOccupied))
        {
            allHandIds.AddRange(seat.Player!.HandIds);
        }

        return await _dbSet
            .Where(h => allHandIds.Contains(h.Id))
            .ToListAsync();
    }
}