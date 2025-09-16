using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;
using BlackJack.Data.Repositories.Game;   // <- usa tu repo EF
using Microsoft.Extensions.Logging;

namespace BlackJack.Services.Table;

public class TableService : ITableService
{
    private readonly ITableRepository _tableRepository;
    private readonly ILogger<TableService>? _logger;

    public TableService(ITableRepository tableRepository, ILogger<TableService>? logger = null)
    {
        _tableRepository = tableRepository;
        _logger = logger;
    }

    public async Task<Result<List<BlackjackTable>>> GetAvailableTablesAsync()
    {
        try
        {
            var tables = await _tableRepository.GetAvailableTablesAsync();
            // Devolvemos éxito incluso si la lista está vacía
            return Result<List<BlackjackTable>>.Success(tables);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error obteniendo mesas disponibles");
            return Result<List<BlackjackTable>>.Failure("No se pudieron obtener las mesas disponibles");
        }
    }

    public async Task<Result<BlackjackTable>> GetTableAsync(TableId tableId)
    {
        try
        {
            // Carga con relaciones (seats, players, spectators)
            var table = await _tableRepository.GetTableWithPlayersAsync(tableId);
            if (table is null)
                return Result<BlackjackTable>.Failure("Mesa no encontrada");

            return Result<BlackjackTable>.Success(table);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error obteniendo la mesa {TableId}", tableId);
            return Result<BlackjackTable>.Failure("No se pudo obtener la mesa");
        }
    }

    public async Task<Result<BlackjackTable>> CreateTableAsync(string name)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result<BlackjackTable>.Failure("El nombre es requerido");

            var table = BlackjackTable.Create(name);

            // Si quieres fijar límites personalizados en el futuro:
            // table.SetBetLimits(new Money(10), new Money(500));

            await _tableRepository.AddAsync(table);
            return Result<BlackjackTable>.Success(table);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creando mesa");
            return Result<BlackjackTable>.Failure($"Error al crear la mesa: {ex.Message}");
        }
    }

    public async Task<Result> DeleteTableAsync(TableId tableId)
    {
        try
        {
            var existing = await _tableRepository.GetByIdAsync(tableId.Value);
            if (existing is null)
                return Result.Failure("Mesa no encontrada");

            await _tableRepository.DeleteAsync(existing);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error eliminando mesa {TableId}", tableId);
            return Result.Failure("No se pudo eliminar la mesa");
        }
    }
}
