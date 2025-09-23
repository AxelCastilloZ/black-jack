// BlackJack.Data/Repositories/Game/GameRoomRepository.cs - COMPLETO CON TODOS LOS MÉTODOS
using BlackJack.Data.Context;
using BlackJack.Data.Repositories.Common;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace BlackJack.Data.Repositories.Game;

public class GameRoomRepository : Repository<GameRoom>, IGameRoomRepository
{
    public GameRoomRepository(ApplicationDbContext context) : base(context)
    {
    }

    #region Basic CRUD Operations

    public async Task<GameRoom?> GetByRoomCodeAsync(string roomCode)
    {
        return await _dbSet
            .Include(r => r.Players)
                .ThenInclude(p => p.Player)
            .Include(r => r.Spectators)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode);
    }

    // ✅ NUEVO: Método requerido por IGameRoomRepository y GameService
    public async Task<GameRoom?> GetByTableIdAsync(Guid tableId)
    {
        await _context.SaveChangesAsync();

        return await _dbSet
            .Include(r => r.Players)
                .ThenInclude(p => p.Player)
            .Include(r => r.Spectators)
            .AsNoTracking()
            .Where(r => r.BlackjackTableId == tableId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<GameRoom>> GetAllAsync()
    {
        return await _dbSet
            .Include(r => r.Players)
                .ThenInclude(p => p.Player)
            .Include(r => r.Spectators)
            .AsNoTracking()
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    // ✅ NUEVO: Método requerido por IGameRoomRepository
    public async Task<List<GameRoom>> GetAvailableRoomsAsync()
    {
        return await GetActiveRoomsAsync(); // Reutiliza la implementación existente
    }

    public async Task<List<GameRoom>> GetRoomsByHostAsync(PlayerId hostPlayerId)
    {
        var sql = @"
            SELECT r.Id
            FROM GameRooms r
            WHERE r.HostPlayerId = @HostPlayerId";

        var parameter = new SqlParameter("@HostPlayerId", hostPlayerId.Value);
        var roomIds = await _context.Database.SqlQueryRaw<Guid>(sql, parameter).ToListAsync();

        if (!roomIds.Any())
            return new List<GameRoom>();

        return await _dbSet
            .Where(r => roomIds.Contains(r.Id))
            .Include(r => r.Players)
                .ThenInclude(p => p.Player)
            .Include(r => r.Spectators)
            .AsNoTracking()
            .ToListAsync();
    }

    // ✅ NUEVO: Método requerido por IGameRoomRepository
    public async Task<List<GameRoom>> GetRoomsInProgressAsync()
    {
        return await GetRoomsByStatusAsync(RoomStatus.InProgress);
    }

    public async Task<List<GameRoom>> GetRoomsByStatusAsync(RoomStatus status)
    {
        return await _dbSet
            .Where(r => r.Status == status)
            .Include(r => r.Players)
                .ThenInclude(p => p.Player)
            .Include(r => r.Spectators)
            .AsNoTracking()
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> RoomCodeExistsAsync(string roomCode)
    {
        return await _dbSet.AnyAsync(r => r.RoomCode == roomCode);
    }

    // ✅ NUEVO: Método requerido por IGameRoomRepository
    public async Task<bool> IsPlayerInAnyRoomAsync(PlayerId playerId)
    {
        var sql = @"
            SELECT COUNT(1)
            FROM RoomPlayers
            WHERE PlayerId = @PlayerId";

        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqlParameter("@PlayerId", playerId.Value));

        if (_context.Database.GetDbConnection().State != ConnectionState.Open)
            await _context.Database.OpenConnectionAsync();

        var result = (int)await command.ExecuteScalarAsync();
        return result > 0;
    }

    public async Task<GameRoom?> GetPlayerCurrentRoomAsync(PlayerId playerId)
    {
        await _context.SaveChangesAsync();

        var sql = @"
            SELECT TOP(1) gr.Id 
            FROM GameRooms gr
            INNER JOIN RoomPlayers rp ON gr.Id = rp.GameRoomId
            WHERE rp.PlayerId = @PlayerId";

        var parameter = new SqlParameter("@PlayerId", playerId.Value);

        var roomIds = await _context.Database
            .SqlQueryRaw<Guid>(sql, parameter)
            .ToListAsync();

        if (!roomIds.Any())
            return null;

        var roomId = roomIds.First();

        return await _dbSet
            .Include(r => r.Players)
                .ThenInclude(p => p.Player)
            .Include(r => r.Spectators)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomId);
    }

    #endregion

    #region Pagination and Search Methods

    // ✅ NUEVO: Método requerido por IGameRoomRepository
    public async Task<(List<GameRoom> rooms, int totalCount)> GetRoomsPagedAsync(
        int page,
        int pageSize,
        string? searchTerm = null,
        RoomStatus? status = null)
    {
        var query = _dbSet
            .Include(r => r.Players)
                .ThenInclude(p => p.Player)
            .Include(r => r.Spectators)
            .AsNoTracking()
            .AsQueryable();

        // Aplicar filtros
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(r => r.Name.Contains(searchTerm) || r.RoomCode.Contains(searchTerm));
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var totalCount = await query.CountAsync();

        var rooms = await query
            .OrderBy(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (rooms, totalCount);
    }

    #endregion

    #region Statistics Methods

    // ✅ NUEVO: Método requerido por IGameRoomRepository
    public async Task<int> GetTotalRoomsCountAsync()
    {
        return await _dbSet.CountAsync();
    }

    // ✅ NUEVO: Método requerido por IGameRoomRepository
    public async Task<int> GetActiveRoomsCountAsync()
    {
        return await _dbSet
            .Where(r => r.Status == RoomStatus.WaitingForPlayers || r.Status == RoomStatus.InProgress)
            .CountAsync();
    }

    // ✅ NUEVO: Método requerido por IGameRoomRepository
    public async Task<Dictionary<RoomStatus, int>> GetRoomStatisticsAsync()
    {
        var stats = await _dbSet
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        // Asegurar que todos los estados estén representados
        foreach (RoomStatus status in Enum.GetValues<RoomStatus>())
        {
            if (!stats.ContainsKey(status))
                stats[status] = 0;
        }

        return stats;
    }

    #endregion

    #region Game-Specific Methods

    // ✅ NUEVO: Método requerido por IGameRoomRepository
    public async Task<List<GameRoom>> GetRoomsWithPlayersAsync()
    {
        return await _dbSet
            .Where(r => r.Players.Any())
            .Include(r => r.Players)
                .ThenInclude(p => p.Player)
            .Include(r => r.Spectators)
            .AsNoTracking()
            .ToListAsync();
    }

    // ✅ NUEVO: Método requerido por IGameRoomRepository
    public async Task<bool> HasActiveGameAsync(Guid tableId)
    {
        return await _dbSet.AnyAsync(r => r.BlackjackTableId == tableId && r.Status == RoomStatus.InProgress);
    }

    #endregion

    #region Transaction Methods

    // ✅ NUEVO: Método requerido por IGameRoomRepository
    public async Task<IDbContextTransaction> BeginTransactionAsync()
    {
        return await _context.Database.BeginTransactionAsync();
    }

    // ✅ NUEVO: Método requerido por IGameRoomRepository
    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    #endregion

    #region Existing Methods (mantenidos del código original)

    public async Task<GameRoom?> GetRoomWithPlayersAsync(string roomCode)
    {
        return await _dbSet
            .Include(r => r.Players)
                .ThenInclude(p => p.Player)
            .Include(r => r.Spectators)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode);
    }

    public async Task<GameRoom?> GetRoomWithPlayersAsync(Guid roomId)
    {
        return await _dbSet
            .Include(r => r.Players)
                .ThenInclude(p => p.Player)
            .Include(r => r.Spectators)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomId);
    }

    public async Task<GameRoom?> GetRoomWithPlayersReadOnlyAsync(string roomCode)
    {
        return await _dbSet
            .Include(r => r.Players)
                .ThenInclude(p => p.Player)
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
                .ThenInclude(p => p.Player)
            .Include(r => r.Spectators)
            .AsNoTracking()
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<GameRoom>> GetActiveRoomsReadOnlyAsync()
    {
        await _context.SaveChangesAsync();

        return await _dbSet
            .Where(r => r.Status == RoomStatus.WaitingForPlayers || r.Status == RoomStatus.InProgress)
            .Include(r => r.Players)
                .ThenInclude(p => p.Player)
            .Include(r => r.Spectators)
            .AsNoTracking()
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    // DEPRECATED: Usar GetByTableIdAsync en su lugar
    public async Task<GameRoom?> GetRoomByTableIdAsync(Guid tableId)
    {
        return await GetByTableIdAsync(tableId); // Redirige al método estándar
    }

    public async Task<GameRoom?> RefreshRoomAsync(GameRoom room)
    {
        var entry = _context.Entry(room);
        if (entry.State != EntityState.Detached)
        {
            await entry.ReloadAsync();
            await entry.Collection(r => r.Players).LoadAsync();
            await entry.Collection(r => r.Spectators).LoadAsync();

            foreach (var player in room.Players)
            {
                await _context.Entry(player).Reference(rp => rp.Player).LoadAsync();
            }
        }
        return room;
    }

    public async Task FlushChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    #endregion

    #region RoomPlayer Operations

    public async Task<RoomPlayer?> GetRoomPlayerAsync(string roomCode, PlayerId playerId)
    {
        await _context.SaveChangesAsync();

        var sql = @"
            SELECT rp.Id
            FROM RoomPlayers rp
            INNER JOIN GameRooms gr ON rp.GameRoomId = gr.Id
            WHERE gr.RoomCode = @RoomCode AND rp.PlayerId = @PlayerId";

        var parameters = new[]
        {
            new SqlParameter("@RoomCode", roomCode),
            new SqlParameter("@PlayerId", playerId.Value)
        };

        var roomPlayerIds = await _context.Database
            .SqlQueryRaw<Guid>(sql, parameters)
            .ToListAsync();

        if (!roomPlayerIds.Any())
            return null;

        var roomPlayerId = roomPlayerIds.First();

        return await _context.Set<RoomPlayer>()
            .Include(rp => rp.Player)
            .AsNoTracking()
            .FirstOrDefaultAsync(rp => rp.Id == roomPlayerId);
    }

    public async Task UpdateRoomPlayerAsync(RoomPlayer roomPlayer)
    {
        roomPlayer.UpdatedAt = DateTime.UtcNow;

        var existingEntry = _context.Entry(roomPlayer);

        if (existingEntry.State == EntityState.Detached)
        {
            _context.Set<RoomPlayer>().Update(roomPlayer);
        }
        else
        {
            existingEntry.State = EntityState.Modified;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsPlayerInRoomAsync(PlayerId playerId, string roomCode)
    {
        var sql = @"
            SELECT COUNT(1) 
            FROM RoomPlayers rp
            INNER JOIN GameRooms gr ON rp.GameRoomId = gr.Id
            WHERE gr.RoomCode = @RoomCode AND rp.PlayerId = @PlayerId";

        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqlParameter("@RoomCode", roomCode));
        command.Parameters.Add(new SqlParameter("@PlayerId", playerId.Value));

        if (_context.Database.GetDbConnection().State != ConnectionState.Open)
            await _context.Database.OpenConnectionAsync();

        var result = (int)await command.ExecuteScalarAsync();
        return result > 0;
    }

    public async Task<bool> IsSeatOccupiedAsync(string roomCode, int seatPosition)
    {
        var sql = @"
            SELECT COUNT(1)
            FROM RoomPlayers rp
            INNER JOIN GameRooms gr ON rp.GameRoomId = gr.Id
            WHERE gr.RoomCode = @RoomCode AND rp.SeatPosition = @SeatPosition";

        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqlParameter("@RoomCode", roomCode));
        command.Parameters.Add(new SqlParameter("@SeatPosition", seatPosition));

        if (_context.Database.GetDbConnection().State != ConnectionState.Open)
            await _context.Database.OpenConnectionAsync();

        var result = (int)await command.ExecuteScalarAsync();
        return result > 0;
    }

    public async Task<RoomPlayer?> GetPlayerInSeatAsync(string roomCode, int seatPosition)
    {
        await _context.SaveChangesAsync();

        var sql = @"
            SELECT rp.Id
            FROM RoomPlayers rp
            INNER JOIN GameRooms gr ON rp.GameRoomId = gr.Id
            WHERE gr.RoomCode = @RoomCode AND rp.SeatPosition = @SeatPosition";

        var parameters = new[]
        {
            new SqlParameter("@RoomCode", roomCode),
            new SqlParameter("@SeatPosition", seatPosition)
        };

        var roomPlayerIds = await _context.Database
            .SqlQueryRaw<Guid>(sql, parameters)
            .ToListAsync();

        if (!roomPlayerIds.Any())
            return null;

        var roomPlayerId = roomPlayerIds.First();

        return await _context.Set<RoomPlayer>()
            .Include(rp => rp.Player)
            .AsNoTracking()
            .FirstOrDefaultAsync(rp => rp.Id == roomPlayerId);
    }

    public async Task<Dictionary<Guid, int>> GetSeatPositionsAsync(string roomCode)
    {
        await _context.SaveChangesAsync();

        var sql = @"
            SELECT rp.PlayerId, rp.SeatPosition
            FROM RoomPlayers rp
            INNER JOIN GameRooms gr ON rp.GameRoomId = gr.Id
            WHERE gr.RoomCode = @RoomCode AND rp.SeatPosition IS NOT NULL";

        var parameter = new SqlParameter("@RoomCode", roomCode);

        var connection = _context.Database.GetDbConnection();
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(parameter);

        var result = new Dictionary<Guid, int>();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        try
        {
            var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var playerId = reader.GetGuid("PlayerId");
                var seatPosition = reader.GetInt32("SeatPosition");
                result[playerId] = seatPosition;
            }
            await reader.CloseAsync();
        }
        finally
        {
            if (connection.State == ConnectionState.Open)
                await connection.CloseAsync();
        }

        return result;
    }

    public async Task<bool> FreeSeatAsync(string roomCode, PlayerId playerId)
    {
        var sql = @"
            UPDATE RoomPlayers 
            SET SeatPosition = NULL, UpdatedAt = @UpdatedAt
            WHERE GameRoomId = (SELECT Id FROM GameRooms WHERE RoomCode = @RoomCode)
              AND PlayerId = @PlayerId
              AND SeatPosition IS NOT NULL";

        var parameters = new[]
        {
            new SqlParameter("@RoomCode", roomCode),
            new SqlParameter("@PlayerId", playerId.Value),
            new SqlParameter("@UpdatedAt", DateTime.UtcNow)
        };

        var affectedRows = await _context.Database.ExecuteSqlRawAsync(sql, parameters);
        return affectedRows > 0;
    }

    public async Task<bool> IsPlayerSeatedAsync(string roomCode, PlayerId playerId)
    {
        var sql = @"
            SELECT COUNT(1)
            FROM RoomPlayers rp
            INNER JOIN GameRooms gr ON rp.GameRoomId = gr.Id
            WHERE gr.RoomCode = @RoomCode AND rp.PlayerId = @PlayerId AND rp.SeatPosition IS NOT NULL";

        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqlParameter("@RoomCode", roomCode));
        command.Parameters.Add(new SqlParameter("@PlayerId", playerId.Value));

        if (_context.Database.GetDbConnection().State != ConnectionState.Open)
            await _context.Database.OpenConnectionAsync();

        var result = (int)await command.ExecuteScalarAsync();
        return result > 0;
    }

    #endregion

    #region Data Cleanup Methods

    public async Task<bool> RemoveRoomPlayerAsync(string roomCode, PlayerId playerId)
    {
        var sql = @"
            DELETE FROM RoomPlayers 
            WHERE GameRoomId = (SELECT Id FROM GameRooms WHERE RoomCode = @RoomCode)
              AND PlayerId = @PlayerId";

        var parameters = new[]
        {
            new SqlParameter("@RoomCode", roomCode),
            new SqlParameter("@PlayerId", playerId.Value)
        };

        var affectedRows = await _context.Database.ExecuteSqlRawAsync(sql, parameters);
        return affectedRows > 0;
    }

    public async Task<int> ForceCleanupPlayerFromAllRoomsAsync(PlayerId playerId)
    {
        var sql = @"
            DELETE FROM RoomPlayers 
            WHERE PlayerId = @PlayerId";

        var parameter = new SqlParameter("@PlayerId", playerId.Value);
        return await _context.Database.ExecuteSqlRawAsync(sql, parameter);
    }

    public async Task<List<string>> GetPlayerOrphanRoomsAsync(PlayerId playerId)
    {
        var sql = @"
            SELECT gr.RoomCode
            FROM RoomPlayers rp
            INNER JOIN GameRooms gr ON rp.GameRoomId = gr.Id
            WHERE rp.PlayerId = @PlayerId";

        var parameter = new SqlParameter("@PlayerId", playerId.Value);

        return await _context.Database
            .SqlQueryRaw<string>(sql, parameter)
            .ToListAsync();
    }

    public async Task<int> CleanupEmptyRoomsAsync()
    {
        var sql = @"
            DELETE FROM GameRooms 
            WHERE Id NOT IN (
                SELECT DISTINCT GameRoomId 
                FROM RoomPlayers
            )
            AND Status IN (0, 2)";

        return await _context.Database.ExecuteSqlRawAsync(sql);
    }

    #endregion

    #region Override Methods with Improved Tracking Handling

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
                var trackedEntries = _context.ChangeTracker.Entries<GameRoom>()
                    .Where(e => e.Entity.Id == entity.Id)
                    .ToList();

                foreach (var trackedEntry in trackedEntries)
                {
                    trackedEntry.State = EntityState.Detached;
                    Console.WriteLine($"[GameRoomRepository] Detached existing tracked entity for room {entity.RoomCode}");
                }

                _context.Update(entity);
                await _context.SaveChangesAsync();

                Console.WriteLine($"[GameRoomRepository] Successfully updated room {entity.RoomCode} to status {entity.Status}");
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
            catch (InvalidOperationException ex) when (ex.Message.Contains("cannot be tracked"))
            {
                retryCount++;

                if (retryCount >= maxRetries)
                {
                    Console.WriteLine($"[GameRoomRepository] Max retries ({maxRetries}) reached for tracking conflict in room {entity.RoomCode}");

                    Console.WriteLine($"[GameRoomRepository] Falling back to SQL direct update for room {entity.RoomCode}");
                    await UpdateRoomStatusDirectAsync(entity.Id, entity.Status);
                    break;
                }

                _context.ChangeTracker.Clear();
                await Task.Delay(200 * retryCount);

                Console.WriteLine($"[GameRoomRepository] Tracking conflict for room {entity.RoomCode}, cleared ChangeTracker, retry {retryCount}/{maxRetries}");
            }
        }
    }

    private async Task UpdateRoomStatusDirectAsync(Guid roomId, RoomStatus status)
    {
        var sql = @"
            UPDATE GameRooms 
            SET Status = @Status, UpdatedAt = @UpdatedAt 
            WHERE Id = @RoomId";

        var parameters = new[]
        {
            new SqlParameter("@Status", (int)status),
            new SqlParameter("@UpdatedAt", DateTime.UtcNow),
            new SqlParameter("@RoomId", roomId)
        };

        var affectedRows = await _context.Database.ExecuteSqlRawAsync(sql, parameters);
        Console.WriteLine($"[GameRoomRepository] Direct SQL update affected {affectedRows} rows for room {roomId}");
    }

    public override async Task DeleteAsync(GameRoom entity)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync();
    }

    #endregion
}