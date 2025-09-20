// GameRoomRepository.cs - CORREGIDO PARA PERSISTENCIA INMEDIATA Y CONCURRENCIA
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
            .AsNoTracking() // CORREGIDO: Evitar problemas de tracking
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode);
    }

    public async Task<GameRoom?> GetRoomWithPlayersAsync(Guid roomId)
    {
        return await _dbSet
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .AsNoTracking() // CORREGIDO: Evitar problemas de tracking
            .FirstOrDefaultAsync(r => r.Id == roomId);
    }

    public async Task<GameRoom?> GetRoomWithPlayersAsync(string roomCode)
    {
        return await _dbSet
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .AsNoTracking() // CORREGIDO: Evitar problemas de tracking para consultas
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode);
    }

    // NUEVO: Método específico para obtener sala con tracking para updates
    public async Task<GameRoom?> GetRoomWithPlayersForUpdateAsync(string roomCode)
    {
        return await _dbSet
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode);
    }

    // CORREGIDO: Método con forzado de persistencia inmediata
    public async Task<List<GameRoom>> GetActiveRoomsAsync()
    {
        // FORZAR persistencia de cambios pendientes antes de consultar
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
        // CORREGIDO: Forzar flush de cambios pendientes
        await _context.SaveChangesAsync();

        return await _dbSet
            .Include(r => r.Players)
            .Include(r => r.Spectators)
            .AsNoTracking()
            .Where(r => r.Players.Any(p => p.PlayerId == playerId))
            .FirstOrDefaultAsync();
    }

    // CORREGIDO: Override del método Add para persistencia inmediata
    public override async Task AddAsync(GameRoom entity)
    {
        await _dbSet.AddAsync(entity);
        // FORZAR persistencia inmediata para evitar race conditions
        await _context.SaveChangesAsync();
    }

    // CORREGIDO: Override del método Update para manejar concurrencia y persistencia inmediata
    public override async Task UpdateAsync(GameRoom entity)
    {
        try
        {
            // Detectar si la entidad ya está siendo tracked
            var existingEntry = _context.Entry(entity);

            if (existingEntry.State == EntityState.Detached)
            {
                // Si no está tracked, adjuntarla
                _dbSet.Attach(entity);
                existingEntry.State = EntityState.Modified;
            }

            // CORREGIDO: Persistencia inmediata para evitar timing issues
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Log del error de concurrencia
            Console.WriteLine($"[GameRoomRepository] Concurrency conflict updating room {entity.RoomCode}");
            throw; // Re-throw para que el servicio pueda manejar el retry
        }
    }

    // CORREGIDO: Override del método Delete para persistencia inmediata
    public override async Task DeleteAsync(GameRoom entity)
    {
        _dbSet.Remove(entity);
        // FORZAR persistencia inmediata
        await _context.SaveChangesAsync();
    }

    // NUEVO: Método para refrescar entidad desde base de datos
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

    // NUEVO: Método específico para verificar si un jugador está en una sala (con consistencia inmediata)
    public async Task<bool> IsPlayerInRoomAsync(PlayerId playerId, string roomCode)
    {
        // Forzar persistencia para asegurar consistencia
        await _context.SaveChangesAsync();

        return await _dbSet
            .Include(r => r.Players)
            .AsNoTracking()
            .Where(r => r.RoomCode == roomCode)
            .AnyAsync(r => r.Players.Any(p => p.PlayerId == playerId));
    }
}