// GameRoomRepository.cs
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

    public async Task<GameRoom?> GetByRoomCodeAsync(string roomCode)
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

    public async Task<GameRoom?> GetRoomWithPlayersAsync(string roomCode)
    {
        return await _dbSet
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode);
    }

    public async Task<List<GameRoom>> GetActiveRoomsAsync()
    {
        return await _dbSet
            .Where(r => r.Status == RoomStatus.WaitingForPlayers || r.Status == RoomStatus.InProgress)
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<GameRoom>> GetRoomsByStatusAsync(RoomStatus status)
    {
        return await _dbSet
            .Where(r => r.Status == status)
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> RoomCodeExistsAsync(string roomCode)
    {
        return await _dbSet.AnyAsync(r => r.RoomCode == roomCode);
    }

    public async Task<GameRoom?> GetPlayerCurrentRoomAsync(PlayerId playerId)
    {
        return await _dbSet
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .Where(r => r.Players.Any(p => p.PlayerId == playerId))
            .FirstOrDefaultAsync();
    }
}