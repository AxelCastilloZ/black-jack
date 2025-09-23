
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Data.Context;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Game;

public class TableRepository : Repository<BlackjackTable>, ITableRepository
{
    private readonly ApplicationDbContext _context;

    public TableRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<List<BlackjackTable>> GetAvailableTablesAsync()
    {
        try
        {
          
            var tables = await _dbSet
                .Include(t => t.Seats)
                    .ThenInclude(s => s.Player)
                .ToListAsync();

            // Log para debugging
            Console.WriteLine($"[TableRepository] Found {tables.Count} tables in database");

            return tables;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TableRepository] Error in GetAvailableTablesAsync: {ex.Message}");
            Console.WriteLine($"[TableRepository] Stack trace: {ex.StackTrace}");
            throw; // Re-throw para que TableService maneje el error
        }
    }

    public async Task<BlackjackTable?> GetTableWithPlayersAsync(Guid tableId)
    {
        try
        {
            // LIMPIADO: Removido .Include(t => t.Spectators) porque ya no existe
            var table = await _dbSet
                .Include(t => t.Seats)
                    .ThenInclude(s => s.Player)
                .FirstOrDefaultAsync(t => t.Id == tableId);

            Console.WriteLine($"[TableRepository] GetTableWithPlayersAsync for {tableId}: {(table != null ? "Found" : "Not found")}");
            return table;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TableRepository] Error in GetTableWithPlayersAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<BlackjackTable?> GetTableWithPlayersForUpdateAsync(Guid tableId)
    {
        try
        {
            // LIMPIADO: Removido .Include(t => t.Spectators) porque ya no existe
            var table = await _dbSet
                .Include(t => t.Seats)
                    .ThenInclude(s => s.Player)
                .FirstOrDefaultAsync(t => t.Id == tableId);

            Console.WriteLine($"[TableRepository] GetTableWithPlayersForUpdateAsync for {tableId}: {(table != null ? "Found" : "Not found")}");
            return table;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TableRepository] Error in GetTableWithPlayersForUpdateAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<List<BlackjackTable>> GetTablesByStatusAsync(Domain.Enums.GameStatus status)
    {
        try
        {
            // LIMPIO: Solo Seats, no Spectators
            var tables = await _dbSet
                .Where(t => t.Status == status)
                .Include(t => t.Seats)
                    .ThenInclude(s => s.Player)
                .ToListAsync();

            Console.WriteLine($"[TableRepository] Found {tables.Count} tables with status {status}");
            return tables;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TableRepository] Error in GetTablesByStatusAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync()
    {
        return await _context.Database.BeginTransactionAsync();
    }

    // NUEVO: Método para verificar conectividad de BD
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await _context.Database.CanConnectAsync();
            Console.WriteLine("[TableRepository] Database connection test: SUCCESS");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TableRepository] Database connection test: FAILED - {ex.Message}");
            return false;
        }
    }

    // NUEVO: Método específico para agregar tablas (evita conflicto con base)
    public async Task<BlackjackTable> CreateAndSaveTableAsync(BlackjackTable entity)
    {
        try
        {
            Console.WriteLine($"[TableRepository] Adding new table: {entity.Name} (ID: {entity.Id})");
            await AddAsync(entity); // Usar el método base heredado
            Console.WriteLine($"[TableRepository] Table {entity.Name} saved successfully");
            return entity;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TableRepository] Error adding table {entity.Name}: {ex.Message}");
            throw;
        }
    }
}