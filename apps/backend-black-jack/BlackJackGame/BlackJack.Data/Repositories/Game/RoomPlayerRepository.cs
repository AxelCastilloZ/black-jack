// RoomPlayerRepository.cs
using Microsoft.EntityFrameworkCore;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Data.Context;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Game;

public class RoomPlayerRepository : Repository<RoomPlayer>, IRoomPlayerRepository
{
    public RoomPlayerRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<RoomPlayer?> GetByPlayerIdAsync(PlayerId playerId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(rp => rp.PlayerId == playerId);
    }

    public async Task<List<RoomPlayer>> GetPlayersByRoomAsync(Guid roomId)
    {
        return await _dbSet
            .Where(rp => EF.Property<Guid>(rp, "GameRoomId") == roomId)
            .OrderBy(rp => rp.Position)
            .ToListAsync();
    }

    public async Task<RoomPlayer?> GetPlayerInRoomAsync(Guid roomId, PlayerId playerId)
    {
        return await _dbSet
            .Where(rp => EF.Property<Guid>(rp, "GameRoomId") == roomId)
            .FirstOrDefaultAsync(rp => rp.PlayerId == playerId);
    }

    public async Task<bool> IsPlayerInAnyRoomAsync(PlayerId playerId)
    {
        return await _dbSet.AnyAsync(rp => rp.PlayerId == playerId);
    }
}