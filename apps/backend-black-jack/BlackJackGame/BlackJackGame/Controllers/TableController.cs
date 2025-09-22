using Microsoft.AspNetCore.Mvc;
using BlackJack.Services.Table;
using BlackJack.Services.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Betting;

namespace BlackJackGame.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TableController : BaseController
{
    private readonly ITableService _tableService;
    private readonly IGameService _gameService;
    private readonly IGameRoomService _gameRoomService; // NUEVA dependencia
    private readonly ILogger<TableController> _logger;

    public TableController(
        ITableService tableService,
        IGameService gameService,
        IGameRoomService gameRoomService, // NUEVA dependencia
        ILogger<TableController> logger)
    {
        _tableService = tableService;
        _gameService = gameService;
        _gameRoomService = gameRoomService; // NUEVA asignación
        _logger = logger;
    }

    #region DTOs

    // DTO ACTUALIZADO: Incluye MinBetPerRound dinámico
    public record TableSummaryDto(
        string Id,
        string Name,
        int PlayerCount,
        int MaxPlayers,
        decimal MinBet,           // Límite mínimo de mesa
        decimal MaxBet,           // Límite máximo de mesa
        decimal MinBetPerRound,   // NUEVO: Auto-betting dinámico desde GameRoom
        string Status,
        bool HasActiveRoom,       // NUEVO: Si tiene GameRoom asociado
        string? RoomCode          // NUEVO: Código de sala si existe
    );

    // DTO ACTUALIZADO: Incluye MinBetPerRound para auto-betting
    public record CreateTableRequest(string Name, decimal MinBetPerRound = 10m);

    public record JoinTableRequest(int SeatPosition);

    public record TablePlaceBetRequest(decimal Amount);

    #endregion

    #region Helper Methods

    // MÉTODO HELPER ACTUALIZADO: Mapea desde EnhancedTableInfo
    private static TableSummaryDto ToDto(EnhancedTableInfo table) =>
        new(
            Id: table.Id,
            Name: table.Name,
            PlayerCount: table.PlayerCount,
            MaxPlayers: table.MaxPlayers,
            MinBet: table.MinBet,
            MaxBet: table.MaxBet,
            MinBetPerRound: table.MinBetPerRound,    // CRÍTICO: Campo dinámico
            Status: table.Status,
            HasActiveRoom: table.HasActiveRoom,      // Info adicional
            RoomCode: table.RoomCode                 // Info adicional
        );

    // Método helper para BlackjackTable (usado en GetTable individual)
    private static TableSummaryDto ToDtoFromBlackjackTable(BlackJack.Domain.Models.Game.BlackjackTable table) =>
        new(
            Id: table.Id.ToString(),
            Name: table.Name,
            PlayerCount: table.Seats.Count(s => s.IsOccupied),
            MaxPlayers: table.Seats.Count,
            MinBet: table.MinBet.Amount,
            MaxBet: table.MaxBet.Amount,
            MinBetPerRound: table.MinBet.Amount, // Fallback cuando no hay GameRoom
            Status: table.Status.ToString(),
            HasActiveRoom: false,                // No coordinado
            RoomCode: null
        );

    #endregion

    #region Table Management

    // MÉTODO PRINCIPAL ACTUALIZADO: Usa coordinación con GameRoomService
    [HttpGet]
    public async Task<IActionResult> GetAvailableTables()
    {
        try
        {
            _logger.LogInformation("[TableController] Getting available tables with auto-betting info");

            // CAMBIO CRÍTICO: Usar método coordinado en lugar del original
            var result = await _tableService.GetTablesWithAutoBettingInfoAsync();
            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error, timestamp = DateTime.UtcNow });
            }

            // Mapear a DTOs con MinBetPerRound dinámico incluido
            var tables = result.Value!.Select(ToDto).ToList();

            _logger.LogInformation("[TableController] Returning {Count} tables with coordinated auto-betting info",
                tables.Count);

            // Log para debugging - mostrar valores dinámicos vs fijos
            foreach (var table in tables)
            {
                _logger.LogInformation("[TableController] Mesa {Name}: MinBet={MinBet}, MinBetPerRound={MinBetPerRound}, HasRoom={HasRoom}",
                    table.Name, table.MinBet, table.MinBetPerRound, table.HasActiveRoom);
            }

            return Ok(tables);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TableController] Error in GetAvailableTables: {Error}", ex.Message);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message, timestamp = DateTime.UtcNow });
        }
    }

    [HttpGet("{tableId}")]
    public async Task<IActionResult> GetTable(string tableId)
    {
        try
        {
            if (!Guid.TryParse(tableId, out var guid))
            {
                return BadRequest(new { error = "Invalid table ID format" });
            }

            var result = await _gameService.GetTableAsync(guid);
            if (!result.IsSuccess)
            {
                return NotFound(new { error = result.Error });
            }

            // Para mesa individual, usar método de fallback
            // TODO: En futuras mejoras, también coordinar aquí con GameRoomService
            var dto = ToDtoFromBlackjackTable(result.Value!);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TableController] Error getting table {TableId}: {Error}", tableId, ex.Message);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    // MÉTODO COMPLETAMENTE ACTUALIZADO: Crea BlackjackTable + GameRoom coordinado
    [HttpPost]
    public async Task<IActionResult> CreateTable([FromBody] CreateTableRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { error = "Table name is required" });
            }

            // NUEVA VALIDACIÓN: MinBetPerRound
            if (request.MinBetPerRound <= 0)
            {
                return BadRequest(new { error = "MinBetPerRound must be greater than 0" });
            }

            _logger.LogInformation("[TableController] Creating table: {Name} with MinBetPerRound: {MinBetPerRound}",
                request.Name, request.MinBetPerRound);

            // PASO 1: Crear BlackjackTable (original)
            var result = await _tableService.CreateTableAsync(request.Name);
            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error });
            }

            var blackjackTable = result.Value!;
            _logger.LogInformation("[TableController] BlackjackTable created: {TableId}", blackjackTable.Id);

            // PASO 2: NUEVO - Crear GameRoom asociado con auto-betting
            try
            {
                // TODO: Obtener PlayerId del usuario autenticado
                var hostPlayerId = PlayerId.New(); // Temporal - reemplazar con auth real
                var roomName = $"Sala de {request.Name}";

                _logger.LogInformation("[TableController] Creating GameRoom for table {TableId} with MinBetPerRound: {MinBetPerRound}",
                    blackjackTable.Id, request.MinBetPerRound);

                var roomResult = await _gameRoomService.CreateRoomForTableAsync(
                    roomName,
                    blackjackTable.Id.ToString(),
                    hostPlayerId
                );

                if (roomResult.IsSuccess && roomResult.Value != null)
                {
                    var gameRoom = roomResult.Value;

                    // CRÍTICO: Usar método público SetMinBetPerRound en lugar de asignación directa
                    gameRoom.SetMinBetPerRound(request.MinBetPerRound);

                    _logger.LogInformation("[TableController] ✅ GameRoom created successfully: {RoomCode} with MinBetPerRound: {Amount}",
                        gameRoom.RoomCode, gameRoom.MinBetPerRound.Amount);
                }
                else
                {
                    _logger.LogWarning("[TableController] ⚠️ Failed to create GameRoom for table {TableId}: {Error}",
                        blackjackTable.Id, roomResult.Error);
                    // Continuar sin GameRoom - será creado cuando alguien se una
                }
            }
            catch (Exception gameRoomEx)
            {
                _logger.LogError(gameRoomEx, "[TableController] Error creating GameRoom for table {TableId}",
                    blackjackTable.Id);
                // Continuar sin GameRoom - no es crítico para la creación de la mesa
            }

            // PASO 3: Crear respuesta con información coordinada
            try
            {
                // Intentar obtener información coordinada (con GameRoom si se creó)
                var coordinatedResult = await _tableService.GetTablesWithAutoBettingInfoAsync();
                if (coordinatedResult.IsSuccess)
                {
                    var createdTable = coordinatedResult.Value!
                        .FirstOrDefault(t => t.Id == blackjackTable.Id.ToString());

                    if (createdTable != null)
                    {
                        var coordinatedDto = ToDto(createdTable);
                        _logger.LogInformation("[TableController] Table created with coordinated info: MinBetPerRound={MinBetPerRound}, HasRoom={HasRoom}",
                            coordinatedDto.MinBetPerRound, coordinatedDto.HasActiveRoom);

                        return CreatedAtAction(nameof(GetTable), new { tableId = coordinatedDto.Id }, coordinatedDto);
                    }
                }
            }
            catch (Exception coordEx)
            {
                _logger.LogWarning(coordEx, "[TableController] Error getting coordinated info for created table, using fallback");
            }

            // FALLBACK: Usar información básica de BlackjackTable
            var fallbackDto = new TableSummaryDto(
                Id: blackjackTable.Id.ToString(),
                Name: blackjackTable.Name,
                PlayerCount: 0, // Nueva mesa
                MaxPlayers: blackjackTable.Seats.Count,
                MinBet: blackjackTable.MinBet.Amount,
                MaxBet: blackjackTable.MaxBet.Amount,
                MinBetPerRound: request.MinBetPerRound, // USAR VALOR DEL REQUEST
                Status: blackjackTable.Status.ToString(),
                HasActiveRoom: false, // Se determinará en próxima carga
                RoomCode: null
            );

            _logger.LogInformation("[TableController] Table created successfully (fallback response): {TableId}", fallbackDto.Id);
            return CreatedAtAction(nameof(GetTable), new { tableId = fallbackDto.Id }, fallbackDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TableController] Error creating table: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error creating table", message = ex.Message });
        }
    }

    [HttpDelete("{tableId}")]
    public async Task<IActionResult> DeleteTable(string tableId)
    {
        try
        {
            if (!Guid.TryParse(tableId, out var guid))
            {
                return BadRequest(new { error = "Invalid table ID format" });
            }

            var result = await _tableService.DeleteTableAsync(guid);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TableController] Error deleting table {TableId}: {Error}", tableId, ex.Message);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    #endregion

    #region Player Actions (Básicas)

    [HttpPost("{tableId}/join")]
    public async Task<IActionResult> JoinTable(string tableId, [FromBody] JoinTableRequest request)
    {
        try
        {
            if (!Guid.TryParse(tableId, out var guid))
            {
                return BadRequest(new { error = "Invalid table ID format" });
            }

            var playerId = PlayerId.New();

            // NUEVO: Limpieza preventiva antes de unirse
            await _gameRoomService.ForceCleanupPlayerAsync(playerId);

            var result = await _gameService.JoinTableAsync(guid, playerId, request.SeatPosition);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TableController] Error joining table: {Error}", ex.Message);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    [HttpPost("{tableId}/leave")]
    public async Task<IActionResult> LeaveTable(string tableId)
    {
        try
        {
            if (!Guid.TryParse(tableId, out var guid))
            {
                return BadRequest(new { error = "Invalid table ID format" });
            }

            // TODO: Obtener PlayerId del usuario autenticado
            var playerId = PlayerId.New();

            var result = await _gameService.LeaveTableAsync(guid, playerId);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TableController] Error leaving table: {Error}", ex.Message);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    [HttpPost("{tableId}/bet")]
    public async Task<IActionResult> PlaceBet(string tableId, [FromBody] TablePlaceBetRequest request)
    {
        try
        {
            if (!Guid.TryParse(tableId, out var guid))
            {
                return BadRequest(new { error = "Invalid table ID format" });
            }

            if (request.Amount <= 0)
            {
                return BadRequest(new { error = "Bet amount must be greater than zero" });
            }

            // TODO: Obtener PlayerId del usuario autenticado
            var playerId = PlayerId.New();
            var bet = Bet.Create(request.Amount);

            var result = await _gameService.PlaceBetAsync(guid, playerId, bet);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TableController] Error placing bet: {Error}", ex.Message);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    #endregion

    #region Debug Endpoints (Temporal)

    [HttpGet("debug")]
    public async Task<IActionResult> DebugTables()
    {
        try
        {
            var debugInfo = new
            {
                timestamp = DateTime.UtcNow,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                tableServiceType = _tableService.GetType().Name,
                gameServiceType = _gameService.GetType().Name,
                gameRoomServiceType = _gameRoomService.GetType().Name // NUEVO
            };

            // ACTUALIZADO: Usar método coordinado en debug también
            var result = await _tableService.GetTablesWithAutoBettingInfoAsync();

            return Ok(new
            {
                debugInfo,
                serviceCallSuccess = result.IsSuccess,
                serviceError = result.IsSuccess ? null : result.Error,
                tableCount = result.IsSuccess ? result.Value!.Count : 0,
                coordinatedTables = result.IsSuccess ? result.Value!.Select(t => new {
                    id = t.Id,
                    name = t.Name,
                    status = t.Status,
                    minBet = t.MinBet,
                    maxBet = t.MaxBet,
                    minBetPerRound = t.MinBetPerRound,  // NUEVO: Mostrar valor dinámico
                    hasActiveRoom = t.HasActiveRoom,    // NUEVO: Estado de GameRoom
                    roomCode = t.RoomCode               // NUEVO: Código de sala
                }).ToList() : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TableController] Debug endpoint error");
            return StatusCode(500, new
            {
                error = ex.Message,
                stackTrace = ex.StackTrace,
                innerException = ex.InnerException?.Message
            });
        }
    }

    #endregion
}