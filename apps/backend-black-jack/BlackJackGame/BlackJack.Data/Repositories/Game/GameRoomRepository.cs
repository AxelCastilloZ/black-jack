// BlackJack.Data/Repositories/Game/GameRoomRepository.cs - CORREGIDO PARA TRACKING CONFLICTS
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

    #region NUEVO: Limpieza de Datos - SOLUCIÓN AL PROBLEMA CRÍTICO

    /// <summary>
    /// MÉTODO CRÍTICO: Elimina completamente un RoomPlayer de la base de datos
    /// Resuelve el problema de "datos fantasma" que causaba "Ya estás en otra sala"
    /// </summary>
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

    /// <summary>
    /// MÉTODO DE LIMPIEZA COMPLETA: Elimina TODOS los registros de un jugador de TODAS las salas
    /// Método de emergencia para casos extremos
    /// </summary>
    public async Task<int> ForceCleanupPlayerFromAllRoomsAsync(PlayerId playerId)
    {
        var sql = @"
            DELETE FROM RoomPlayers 
            WHERE PlayerId = @PlayerId";

        var parameter = new SqlParameter("@PlayerId", playerId.Value);
        return await _context.Database.ExecuteSqlRawAsync(sql, parameter);
    }

    /// <summary>
    /// DIAGNÓSTICO: Obtiene todas las salas donde un jugador tiene registros huérfanos
    /// </summary>
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

    /// <summary>
    /// LIMPIEZA AUTOMÁTICA: Elimina salas vacías (sin jugadores)
    /// </summary>
    public async Task<int> CleanupEmptyRoomsAsync()
    {
        var sql = @"
            DELETE FROM GameRooms 
            WHERE Id NOT IN (
                SELECT DISTINCT GameRoomId 
                FROM RoomPlayers
            )
            AND Status IN (0, 2)"; // WaitingForPlayers o Finished

        return await _context.Database.ExecuteSqlRawAsync(sql);
    }

    #endregion

    #region Override Methods with Immediate Persistence

    public override async Task AddAsync(GameRoom entity)
    {
        await _dbSet.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    // *** FIX CRÍTICO: MÉTODO UpdateAsync CORREGIDO PARA EVITAR TRACKING CONFLICTS ***
    public override async Task UpdateAsync(GameRoom entity)
    {
        var maxRetries = 3;
        var retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                // PASO 1: Desconectar cualquier entidad con el mismo ID que esté siendo tracked
                var trackedEntries = _context.ChangeTracker.Entries<GameRoom>()
                    .Where(e => e.Entity.Id == entity.Id)
                    .ToList();

                foreach (var trackedEntry in trackedEntries)
                {
                    trackedEntry.State = EntityState.Detached;
                    Console.WriteLine($"[GameRoomRepository] Detached existing tracked entity for room {entity.RoomCode}");
                }

                // PASO 2: Usar Update() en lugar de Attach() para evitar conflictos
              
                _context.Update(entity);

                // PASO 3: Guardar cambios
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

                // Recargar la entidad desde la base de datos
                var entry = _context.Entry(entity);
                await entry.ReloadAsync();
                await Task.Delay(100 * retryCount); // Backoff exponencial

                Console.WriteLine($"[GameRoomRepository] Concurrency conflict updating room {entity.RoomCode}, retry {retryCount}/{maxRetries}");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("cannot be tracked"))
            {
                retryCount++;

                if (retryCount >= maxRetries)
                {
                    Console.WriteLine($"[GameRoomRepository] Max retries ({maxRetries}) reached for tracking conflict in room {entity.RoomCode}");

                    // FALLBACK RADICAL: Usar SQL directo
                    Console.WriteLine($"[GameRoomRepository] Falling back to SQL direct update for room {entity.RoomCode}");
                    await UpdateRoomStatusDirectAsync(entity.Id, entity.Status);
                    break;
                }

                // Limpiar completamente el ChangeTracker y reintentar
                _context.ChangeTracker.Clear();
                await Task.Delay(200 * retryCount);

                Console.WriteLine($"[GameRoomRepository] Tracking conflict for room {entity.RoomCode}, cleared ChangeTracker, retry {retryCount}/{maxRetries}");
            }
        }
    }

    /// <summary>
    /// FALLBACK: Actualización directa por SQL cuando EF falla completamente
    /// </summary>
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