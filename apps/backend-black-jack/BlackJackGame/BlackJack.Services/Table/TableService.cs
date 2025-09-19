// BlackJack.Services.Table/TableService.cs - CORREGIDO CON GUID
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Betting;
using BlackJack.Services.Common;
using BlackJack.Data.Repositories.Game;
using Microsoft.Extensions.Logging;

namespace BlackJack.Services.Table;

public class TableService : ITableService
{
    private readonly ITableRepository _tableRepository;
    private readonly ILogger<TableService> _logger;

    public TableService(ITableRepository tableRepository, ILogger<TableService> logger)
    {
        _tableRepository = tableRepository;
        _logger = logger;
    }

    // Eventos
    public event Action<BlackjackTable>? TableCreated;
    public event Action<Guid>? TableDeleted;
    public event Action<BlackjackTable>? TableUpdated;

    public async Task<Result<List<BlackjackTable>>> GetAvailableTablesAsync()
    {
        try
        {
            _logger.LogInformation("[TableService] Obteniendo mesas disponibles...");
            var tables = await _tableRepository.GetAvailableTablesAsync();
            _logger.LogInformation($"[TableService] {tables.Count} mesas encontradas");
            return Result<List<BlackjackTable>>.Success(tables);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TableService] Error obteniendo mesas disponibles: {Error}", ex.Message);
            return Result<List<BlackjackTable>>.Failure($"Error obteniendo mesas: {ex.Message}");
        }
    }

    public async Task<Result<BlackjackTable>> GetTableAsync(Guid tableId)
    {
        try
        {
            var table = await _tableRepository.GetTableWithPlayersAsync(tableId);
            if (table is null)
                return Result<BlackjackTable>.Failure("Mesa no encontrada");
            return Result<BlackjackTable>.Success(table);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TableService] Error obteniendo mesa {TableId}: {Error}", tableId, ex.Message);
            return Result<BlackjackTable>.Failure($"Error obteniendo mesa: {ex.Message}");
        }
    }

    public async Task<Result<BlackjackTable>> CreateTableAsync(string name)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result<BlackjackTable>.Failure("El nombre es requerido");

            _logger.LogInformation("[TableService] Creando mesa: {Name}", name);

            var table = BlackjackTable.Create(name);
            table.SetBetLimits(new Money(10m), new Money(500m));

            // CAMBIO: Usar el método específico para evitar conflictos
            if (_tableRepository is BlackJack.Data.Repositories.Game.TableRepository tableRepo)
            {
                table = await tableRepo.CreateAndSaveTableAsync(table);
            }
            else
            {
                await _tableRepository.AddAsync(table);
            }

            _logger.LogInformation("[TableService] Mesa {Name} creada con ID: {TableId}", name, table.Id);

            // Disparar evento
            try
            {
                TableCreated?.Invoke(table);
                _logger.LogInformation("[TableService] Evento TableCreated disparado para mesa {TableId}", table.Id);
            }
            catch (Exception eventEx)
            {
                _logger.LogWarning(eventEx, "[TableService] Error disparando evento TableCreated: {Error}", eventEx.Message);
            }

            return Result<BlackjackTable>.Success(table);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TableService] Error creando mesa '{Name}': {Error}", name, ex.Message);
            return Result<BlackjackTable>.Failure($"Error creando mesa: {ex.Message}");
        }
    }

    public async Task<Result> DeleteTableAsync(Guid tableId)
    {
        try
        {
            var existing = await _tableRepository.GetByIdAsync(tableId);
            if (existing is null)
                return Result.Failure("Mesa no encontrada");

            await _tableRepository.DeleteAsync(existing);

            try
            {
                TableDeleted?.Invoke(tableId);
                _logger.LogInformation("[TableService] Evento TableDeleted disparado para mesa {TableId}", tableId);
            }
            catch (Exception eventEx)
            {
                _logger.LogWarning(eventEx, "[TableService] Error disparando evento TableDeleted: {Error}", eventEx.Message);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TableService] Error eliminando mesa {TableId}: {Error}", tableId, ex.Message);
            return Result.Failure($"Error eliminando mesa: {ex.Message}");
        }
    }
}