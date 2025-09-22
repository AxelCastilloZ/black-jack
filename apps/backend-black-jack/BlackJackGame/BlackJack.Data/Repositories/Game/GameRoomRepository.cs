// BlackJack.Data/Repositories/Game/GameRoomRepository.cs - SOLUCIÓN DEFINITIVA CON CONEXIÓN DIRECTA
using BlackJack.Data.Context;
using BlackJack.Data.Repositories.Common;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace BlackJack.Data.Repositories.Game;

public class GameRoomRepository : Repository<GameRoom>, IGameRoomRepository
{
    public GameRoomRepository(ApplicationDbContext context) : base(context)
    {
    }

    #region GameRoom Basic Operations

    // CORREGIDO: ThenInclude Player para obtener balance data
    public async Task<GameRoom?> GetByRoomCodeAsync(string roomCode)
    {
        return await _dbSet
            .Include(r => r.Players)
                .ThenInclude(p => p.Player) // ← NUEVO: JOIN con Player para balance
            .Include(r => r.Spectators)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode);
    }

    // CORREGIDO: ThenInclude Player para obtener balance data
    public async Task<GameRoom?> GetRoomWithPlayersAsync(string roomCode)
    {
        return await _dbSet
            .Include(r => r.Players)
                .ThenInclude(p => p.Player) // ← NUEVO: JOIN con Player para balance
            .Include(r => r.Spectators)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode);
    }

    // CORREGIDO: ThenInclude Player para obtener balance data
    public async Task<GameRoom?> GetRoomWithPlayersAsync(Guid roomId)
    {
        return await _dbSet
            .Include(r => r.Players)
                .ThenInclude(p => p.Player) // ← NUEVO: JOIN con Player para balance
            .Include(r => r.Spectators)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomId);
    }

    // CORREGIDO: ThenInclude Player para obtener balance data
    public async Task<GameRoom?> GetRoomWithPlayersReadOnlyAsync(string roomCode)
    {
        return await _dbSet
            .Include(r => r.Players)
                .ThenInclude(p => p.Player) // ← NUEVO: JOIN con Player para balance
            .Include(r => r.Spectators)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoomCode == roomCode);
    }

    // CORREGIDO: ThenInclude Player para obtener balance data
    public async Task<List<GameRoom>> GetActiveRoomsAsync()
    {
        await _context.SaveChangesAsync();

        return await _dbSet
            .Where(r => r.Status == RoomStatus.WaitingForPlayers || r.Status == RoomStatus.InProgress)
            .Include(r => r.Players)
                .ThenInclude(p => p.Player) // ← NUEVO: JOIN con Player para balance
            .Include(r => r.Spectators)
            .AsNoTracking()
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    // CORREGIDO: ThenInclude Player para obtener balance data
    public async Task<List<GameRoom>> GetActiveRoomsReadOnlyAsync()
    {
        await _context.SaveChangesAsync();

        return await _dbSet
            .Where(r => r.Status == RoomStatus.WaitingForPlayers || r.Status == RoomStatus.InProgress)
            .Include(r => r.Players)
                .ThenInclude(p => p.Player) // ← NUEVO: JOIN con Player para balance
            .Include(r => r.Spectators)
            .AsNoTracking()
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    // CORREGIDO: ThenInclude Player para obtener balance data
    public async Task<List<GameRoom>> GetRoomsByStatusAsync(RoomStatus status)
    {
        return await _dbSet
            .Where(r => r.Status == status)
            .Include(r => r.Players)
                .ThenInclude(p => p.Player) // ← NUEVO: JOIN con Player para balance
            .Include(r => r.Spectators)
            .AsNoTracking()
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> RoomCodeExistsAsync(string roomCode)
    {
        return await _dbSet.AnyAsync(r => r.RoomCode == roomCode);
    }

    // SOLUCIÓN DEFINITIVA: SQL directo para evitar completamente problemas de traducción de LINQ
    public async Task<GameRoom?> GetPlayerCurrentRoomAsync(PlayerId playerId)
    {
        await _context.SaveChangesAsync();

        // APPROACH DEFINITIVO: SQL directo para evitar problemas de value object translation
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

        // Cargar la GameRoom completa con todas las navegaciones usando EF
        return await _dbSet
            .Include(r => r.Players)
                .ThenInclude(p => p.Player) // ← NUEVO: JOIN con Player para balance
            .Include(r => r.Spectators)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomId);
    }

    // SOLUCIÓN FINAL: Conexión directa para evitar CUALQUIER problema de traducción
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

    // CORREGIDO: ThenInclude Player para obtener balance data
    public async Task<GameRoom?> GetRoomByTableIdAsync(Guid tableId)
    {
        await _context.SaveChangesAsync();

        return await _dbSet
            .Include(r => r.Players)
                .ThenInclude(p => p.Player) // ← NUEVO: JOIN con Player para balance
            .Include(r => r.Spectators)
            .AsNoTracking()
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

            // NUEVO: También recargar datos de Player para balance actualizado
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

    #region RoomPlayer and SeatPosition Operations

    // CORREGIDO: SQL directo para evitar problemas de traducción LINQ
    public async Task<RoomPlayer?> GetRoomPlayerAsync(string roomCode, PlayerId playerId)
    {
        await _context.SaveChangesAsync();

        // SOLUCIÓN: SQL directo para obtener el RoomPlayer ID
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

        // Cargar el RoomPlayer completo con navegaciones usando EF
        return await _context.Set<RoomPlayer>()
            .Include(rp => rp.Player)
            .AsNoTracking()
            .FirstOrDefaultAsync(rp => rp.Id == roomPlayerId);
    }

    // FIX DEFINITIVO: UpdateRoomPlayerAsync completamente aislado
    public async Task UpdateRoomPlayerAsync(RoomPlayer roomPlayer)
    {
        roomPlayer.UpdatedAt = DateTime.UtcNow;

        // CAMBIO CRÍTICO: Actualización completamente independiente sin tocar relaciones
        var existingEntry = _context.Entry(roomPlayer);

        // Si la entidad está detached, la adjuntamos y marcamos como modificada
        if (existingEntry.State == EntityState.Detached)
        {
            _context.Set<RoomPlayer>().Update(roomPlayer);
        }
        else
        {
            // Si ya está siendo tracked, solo marcamos como modificada
            existingEntry.State = EntityState.Modified;
        }

        await _context.SaveChangesAsync();
    }

    // SOLUCIÓN FINAL: Conexión directa para evitar CUALQUIER problema de traducción
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

    // CORREGIDO: SQL directo para evitar problemas de traducción LINQ
    public async Task<RoomPlayer?> GetPlayerInSeatAsync(string roomCode, int seatPosition)
    {
        await _context.SaveChangesAsync();

        // SOLUCIÓN: SQL directo para obtener el RoomPlayer ID
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

        // Cargar el RoomPlayer completo con navegaciones usando EF
        return await _context.Set<RoomPlayer>()
            .Include(rp => rp.Player)
            .AsNoTracking()
            .FirstOrDefaultAsync(rp => rp.Id == roomPlayerId);
    }

    // CORREGIDO: SQL directo para evitar problemas de traducción LINQ
    public async Task<Dictionary<Guid, int>> GetSeatPositionsAsync(string roomCode)
    {
        await _context.SaveChangesAsync();

        // SOLUCIÓN SIMPLE: SQL directo con múltiples consultas
        var sql = @"
            SELECT rp.PlayerId, rp.SeatPosition
            FROM RoomPlayers rp
            INNER JOIN GameRooms gr ON rp.GameRoomId = gr.Id
            WHERE gr.RoomCode = @RoomCode AND rp.SeatPosition IS NOT NULL";

        var parameter = new SqlParameter("@RoomCode", roomCode);

        // Ejecutar SQL y procesar resultados manualmente
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

    // SOLUCIÓN DEFINITIVA: FreeSeatAsync con SQL directo para evitar EF Collection issues
    public async Task<bool> FreeSeatAsync(string roomCode, PlayerId playerId)
    {
        // APPROACH RADICAL: SQL directo para evitar completamente EF change tracking
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

    // SOLUCIÓN FINAL: Conexión directa para evitar CUALQUIER problema de traducción
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