// BlackJack.Data/Repositories/Game/GameRoomRepository.cs - IMPLEMENTACIÓN COMPLETA
using Microsoft.EntityFrameworkCore;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Data.Context;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Game;

public class GameRoomRepository : Repository<GameRoom>, IGameRoomRepository
{
    public GameRoomRepository(ApplicationDbContext context) : base(context)
    {
    }

    #region GameRoom Basic Operations

    public async Task<GameRoom?> GetByRoomCodeAsync(string roomCode)
    {
        return await _dbSet
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode);
    }

    public async Task<GameRoom?> GetRoomWithPlayersAsync(string roomCode)
    {
        return await _dbSet
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode);
    }

    public async Task<GameRoom?> GetRoomWithPlayersAsync(Guid roomId)
    {
        return await _dbSet
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .FirstOrDefaultAsync(r => r.Id == roomId);
    }

    public async Task<GameRoom?> GetRoomWithPlayersReadOnlyAsync(string roomCode)
    {
        return await _dbSet
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode);
    }

    public async Task<List<GameRoom>> GetActiveRoomsAsync()
    {
        await _context.SaveChangesAsync();

        return await _dbSet
            .Where(r => r.Status == RoomStatus.WaitingForPlayers || r.Status == RoomStatus.InProgress)
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<GameRoom>> GetActiveRoomsReadOnlyAsync()
    {
        await _context.SaveChangesAsync();

        return await _dbSet
            .Where(r => r.Status == RoomStatus.WaitingForPlayers || r.Status == RoomStatus.InProgress)
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .AsNoTracking()
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<GameRoom>> GetRoomsByStatusAsync(RoomStatus status)
    {
        return await _dbSet
            .Where(r => r.Status == status)
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .AsNoTracking()
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> RoomCodeExistsAsync(string roomCode)
    {
        return await _dbSet.AnyAsync(r => r.RoomCode == roomCode);
    }

    public async Task<GameRoom?> GetPlayerCurrentRoomAsync(PlayerId playerId)
    {
        await _context.SaveChangesAsync();

        return await _dbSet
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .Where(r => r.Players.Any(p => p.PlayerId == playerId))
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsPlayerInRoomAsync(PlayerId playerId, string roomCode)
    {
        await _context.SaveChangesAsync();

        return await _dbSet
            .Include(r => r.Players)
            .Where(r => r.RoomCode == roomCode)
            .AnyAsync(r => r.Players.Any(p => p.PlayerId == playerId));
    }

    public async Task<GameRoom?> GetRoomByTableIdAsync(Guid tableId)
    {
        await _context.SaveChangesAsync();

        return await _dbSet
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .Where(r => r.BlackjackTableId == tableId)
            .FirstOrDefaultAsync();
    }

    public async Task<GameRoom?> RefreshRoomAsync(GameRoom room)
    {
        var entry = _context.Entry(room);
        if (entry.State != EntityState.Detached)
        {
            await entry.ReloadAsync();
            await entry.Collection(r => r.Players).LoadAsync();
            await entry.Collection(r => r.Spectators).LoadAsync();
        }
        return room;
    }

    public async Task FlushChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    #endregion

    #region RoomPlayer and SeatPosition Operations

    public async Task<RoomPlayer?> GetRoomPlayerAsync(string roomCode, PlayerId playerId)
    {
        await _context.SaveChangesAsync();

        return await _context.Set<RoomPlayer>()
            .Include(rp => rp.GameRoom)
            .Where(rp => rp.GameRoom.RoomCode == roomCode && rp.PlayerId == playerId)
            .FirstOrDefaultAsync();
    }

    public async Task UpdateRoomPlayerAsync(RoomPlayer roomPlayer)
    {
        roomPlayer.UpdatedAt = DateTime.UtcNow;
        _context.Set<RoomPlayer>().Update(roomPlayer);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsSeatOccupiedAsync(string roomCode, int seatPosition)
    {
        await _context.SaveChangesAsync();

        return await _context.Set<RoomPlayer>()
            .Include(rp => rp.GameRoom)
            .AnyAsync(rp => rp.GameRoom.RoomCode == roomCode && rp.SeatPosition == seatPosition);
    }

    public async Task<RoomPlayer?> GetPlayerInSeatAsync(string roomCode, int seatPosition)
    {
        await _context.SaveChangesAsync();

        return await _context.Set<RoomPlayer>()
            .Include(rp => rp.GameRoom)
            .Where(rp => rp.GameRoom.RoomCode == roomCode && rp.SeatPosition == seatPosition)
            .FirstOrDefaultAsync();
    }

    public async Task<Dictionary<Guid, int>> GetSeatPositionsAsync(string roomCode)
    {
        await _context.SaveChangesAsync();

        var seatData = await _context.Set<RoomPlayer>()
            .Include(rp => rp.GameRoom)
            .Where(rp => rp.GameRoom.RoomCode == roomCode && rp.SeatPosition.HasValue)
            .Select(rp => new { PlayerId = rp.PlayerId.Value, SeatPosition = rp.SeatPosition.Value })
            .ToListAsync();

        return seatData.ToDictionary(x => x.PlayerId, x => x.SeatPosition);
    }

    public async Task<bool> FreeSeatAsync(string roomCode, PlayerId playerId)
    {
        var roomPlayer = await GetRoomPlayerAsync(roomCode, playerId);
        if (roomPlayer != null && roomPlayer.SeatPosition.HasValue)
        {
            roomPlayer.LeaveSeat();
            await UpdateRoomPlayerAsync(roomPlayer);
            return true;
        }
        return false;
    }

    public async Task<bool> IsPlayerSeatedAsync(string roomCode, PlayerId playerId)
    {
        await _context.SaveChangesAsync();

        return await _context.Set<RoomPlayer>()
            .Include(rp => rp.GameRoom)
            .AnyAsync(rp => rp.GameRoom.RoomCode == roomCode &&
                           rp.PlayerId == playerId &&
                           rp.SeatPosition.HasValue);
    }

    #endregion

    #region Override Methods with Immediate Persistence

    public override async Task AddAsync(GameRoom entity)
    {
        await _dbSet.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public override async Task UpdateAsync(GameRoom entity)
    {
        var maxRetries = 3;
        var retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                var existingEntry = _context.Entry(entity);

                if (existingEntry.State == EntityState.Detached)
                {
                    _dbSet.Attach(entity);
                    existingEntry.State = EntityState.Modified;
                }

                await _context.SaveChangesAsync();
                break;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                retryCount++;

                if (retryCount >= maxRetries)
                {
                    Console.WriteLine($"[GameRoomRepository] Max retries ({maxRetries}) reached for room {entity.RoomCode}");
                    throw;
                }

                var entry = _context.Entry(entity);
                await entry.ReloadAsync();
                await Task.Delay(100 * retryCount);

                Console.WriteLine($"[GameRoomRepository] Concurrency conflict updating room {entity.RoomCode}, retry {retryCount}/{maxRetries}");
            }
        }
    }

    public override async Task DeleteAsync(GameRoom entity)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync();
    }

    #endregion
}