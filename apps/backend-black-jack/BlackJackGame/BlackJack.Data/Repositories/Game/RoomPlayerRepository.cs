// RoomPlayerRepository.cs - EXTENDIDO PARA APUESTAS AUTOMÁTICAS
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

    // Métodos existentes
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

    // NUEVOS: Métodos para apuestas automáticas
    public async Task<List<RoomPlayer>> GetSeatedPlayersByRoomCodeAsync(string roomCode)
    {
        return await _dbSet
            .Include(rp => rp.GameRoom)
            .Where(rp => rp.GameRoom.RoomCode == roomCode && rp.SeatPosition.HasValue)
            .OrderBy(rp => rp.SeatPosition)
            .ToListAsync();
    }

    public async Task<List<RoomPlayer>> GetSeatedPlayersByRoomAsync(Guid roomId)
    {
        return await _dbSet
            .Where(rp => EF.Property<Guid>(rp, "GameRoomId") == roomId && rp.SeatPosition.HasValue)
            .OrderBy(rp => rp.SeatPosition)
            .ToListAsync();
    }

    public async Task<bool> IsPlayerSeatedInRoomAsync(string roomCode, PlayerId playerId)
    {
        return await _dbSet
            .Include(rp => rp.GameRoom)
            .AnyAsync(rp => rp.GameRoom.RoomCode == roomCode &&
                           rp.PlayerId == playerId &&
                           rp.SeatPosition.HasValue);
    }

    public async Task<int?> GetPlayerSeatPositionAsync(string roomCode, PlayerId playerId)
    {
        var roomPlayer = await _dbSet
            .Include(rp => rp.GameRoom)
            .FirstOrDefaultAsync(rp => rp.GameRoom.RoomCode == roomCode && rp.PlayerId == playerId);

        return roomPlayer?.SeatPosition;
    }

    public async Task<int> GetSeatedPlayersCountAsync(string roomCode)
    {
        return await _dbSet
            .Include(rp => rp.GameRoom)
            .CountAsync(rp => rp.GameRoom.RoomCode == roomCode && rp.SeatPosition.HasValue);
    }

    public async Task<List<PlayerId>> GetSeatedPlayerIdsAsync(string roomCode)
    {
        return await _dbSet
            .Include(rp => rp.GameRoom)
            .Where(rp => rp.GameRoom.RoomCode == roomCode && rp.SeatPosition.HasValue)
            .Select(rp => rp.PlayerId)
            .ToListAsync();
    }

    public async Task<bool> HasSeatedPlayersAsync(string roomCode)
    {
        return await _dbSet
            .Include(rp => rp.GameRoom)
            .AnyAsync(rp => rp.GameRoom.RoomCode == roomCode && rp.SeatPosition.HasValue);
    }
}