// BlackJack.Services.Table/TableService.cs - CORREGIDO CON GUID + COORDINACIÓN GAMEROOM
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Betting;
using BlackJack.Services.Common;
using BlackJack.Services.Game; // NUEVO: Para IGameRoomService
using BlackJack.Data.Repositories.Game;
using Microsoft.Extensions.Logging;

namespace BlackJack.Services.Table;

public class TableService : ITableService
{
    private readonly ITableRepository _tableRepository;
    private readonly IGameRoomService _gameRoomService; // NUEVO: Coordinación con GameRoom
    private readonly ILogger<TableService> _logger;

    // CONSTRUCTOR ACTUALIZADO: Inyección de IGameRoomService
    public TableService(
        ITableRepository tableRepository,
        IGameRoomService gameRoomService, // NUEVA dependencia
        ILogger<TableService> logger)
    {
        _tableRepository = tableRepository;
        _gameRoomService = gameRoomService; // NUEVA asignación
        _logger = logger;
    }

    // Eventos
    public event Action<BlackjackTable>? TableCreated;
    public event Action<Guid>? TableDeleted;
    public event Action<BlackjackTable>? TableUpdated;

    #region Métodos Originales (Sin cambios)

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

    #endregion

    #region NUEVO: Método Coordinado con GameRoomService

    /// <summary>
    /// MÉTODO COORDINADO: Obtiene mesas con información de auto-betting dinámica
    /// Combina datos de BlackjackTable (MinBet, MaxBet) + GameRoom (MinBetPerRound)
    /// </summary>
    public async Task<Result<List<EnhancedTableInfo>>> GetTablesWithAutoBettingInfoAsync()
    {
        try
        {
            _logger.LogInformation("[TableService] === COORDINACIÓN MESA + GAMEROOM START ===");
            _logger.LogInformation("[TableService] Obteniendo mesas con información de auto-betting...");

            // PASO 1: Obtener todas las mesas disponibles (BlackjackTable)
            var tablesResult = await GetAvailableTablesAsync();
            if (!tablesResult.IsSuccess)
            {
                return Result<List<EnhancedTableInfo>>.Failure(tablesResult.Error);
            }

            var blackjackTables = tablesResult.Value!;
            var enhancedTables = new List<EnhancedTableInfo>();

            _logger.LogInformation("[TableService] Procesando {Count} mesas para coordinar con GameRooms...",
                blackjackTables.Count);

            // PASO 2: Para cada BlackjackTable, buscar GameRoom asociado
            foreach (var table in blackjackTables)
            {
                try
                {
                    _logger.LogInformation("[TableService] Procesando mesa {TableId} - {TableName}...",
                        table.Id, table.Name);

                    // Buscar GameRoom asociado a esta mesa
                    var roomResult = await _gameRoomService.GetRoomByTableIdAsync(table.Id.ToString());

                    // PASO 3: Crear EnhancedTableInfo combinando ambas fuentes
                    var enhancedTable = new EnhancedTableInfo
                    {
                        // Información de BlackjackTable
                        Id = table.Id.ToString(),
                        Name = table.Name,
                        PlayerCount = table.Seats.Count(s => s.IsOccupied),
                        MaxPlayers = table.Seats.Count,
                        MinBet = table.MinBet.Amount,     // Límite de mesa
                        MaxBet = table.MaxBet.Amount,     // Límite de mesa  
                        Status = table.Status.ToString(),

                        // Información de GameRoom (si existe)
                        HasActiveRoom = roomResult.IsSuccess && roomResult.Value != null
                    };

                    if (enhancedTable.HasActiveRoom && roomResult.Value != null)
                    {
                        var gameRoom = roomResult.Value;

                        // CRÍTICO: Asignar MinBetPerRound dinámico desde GameRoom
                        enhancedTable.MinBetPerRound = gameRoom.MinBetPerRound?.Amount ?? table.MinBet.Amount;
                        enhancedTable.RoomCode = gameRoom.RoomCode;

                        _logger.LogInformation("[TableService] ✅ Mesa {TableId} tiene GameRoom: {RoomCode}, MinBetPerRound: {Amount}",
                            table.Id, gameRoom.RoomCode, enhancedTable.MinBetPerRound);
                    }
                    else
                    {
                        // Fallback: Si no hay GameRoom, usar MinBet de la mesa como MinBetPerRound
                        enhancedTable.MinBetPerRound = table.MinBet.Amount;
                        enhancedTable.RoomCode = null;

                        _logger.LogInformation("[TableService] Mesa {TableId} sin GameRoom - usando MinBet como MinBetPerRound: {Amount}",
                            table.Id, enhancedTable.MinBetPerRound);
                    }

                    enhancedTables.Add(enhancedTable);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TableService] Error procesando mesa {TableId}: {Error}",
                        table.Id, ex.Message);

                    // En caso de error, crear entrada básica sin información de GameRoom
                    var fallbackTable = new EnhancedTableInfo
                    {
                        Id = table.Id.ToString(),
                        Name = table.Name,
                        PlayerCount = table.Seats.Count(s => s.IsOccupied),
                        MaxPlayers = table.Seats.Count,
                        MinBet = table.MinBet.Amount,
                        MaxBet = table.MaxBet.Amount,
                        MinBetPerRound = table.MinBet.Amount, // Fallback
                        Status = table.Status.ToString(),
                        HasActiveRoom = false,
                        RoomCode = null
                    };

                    enhancedTables.Add(fallbackTable);
                }
            }

            _logger.LogInformation("[TableService] === COORDINACIÓN MESA + GAMEROOM COMPLETADA ===");
            _logger.LogInformation("[TableService] Devolviendo {Count} mesas con información coordinada",
                enhancedTables.Count);

            var tablesWithRooms = enhancedTables.Count(t => t.HasActiveRoom);
            _logger.LogInformation("[TableService] Mesas con GameRooms activos: {CountWithRooms}/{TotalCount}",
                tablesWithRooms, enhancedTables.Count);

            return Result<List<EnhancedTableInfo>>.Success(enhancedTables);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TableService] ERROR CRÍTICO en GetTablesWithAutoBettingInfoAsync: {Error}", ex.Message);
            return Result<List<EnhancedTableInfo>>.Failure($"Error obteniendo mesas coordinadas: {ex.Message}");
        }
    }

    #endregion
}