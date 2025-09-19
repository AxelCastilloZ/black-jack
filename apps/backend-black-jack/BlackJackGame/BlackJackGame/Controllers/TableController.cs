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
    private readonly ILogger<TableController> _logger;

    public TableController(ITableService tableService, IGameService gameService, ILogger<TableController> logger)
    {
        _tableService = tableService;
        _gameService = gameService;
        _logger = logger;
    }

    #region DTOs

    public record TableSummaryDto(
        string Id,
        string Name,
        int PlayerCount,
        int MaxPlayers,
        decimal MinBet,
        decimal MaxBet,
        string Status
    );

    public record CreateTableRequest(string Name);

    public record JoinTableRequest(int SeatPosition);

    public record TablePlaceBetRequest(decimal Amount);

    #endregion

    #region Helper Methods

    private static TableSummaryDto ToDto(BlackJack.Domain.Models.Game.BlackjackTable table) =>
        new(
            Id: table.Id.ToString(),
            Name: table.Name,
            PlayerCount: table.Seats.Count(s => s.IsOccupied),
            MaxPlayers: table.Seats.Count,
            MinBet: table.MinBet.Amount,
            MaxBet: table.MaxBet.Amount,
            Status: table.Status.ToString()
        );

    #endregion

    #region Table Management

    [HttpGet]
    public async Task<IActionResult> GetAvailableTables()
    {
        try
        {
            _logger.LogInformation("[TableController] Getting available tables");

            var result = await _tableService.GetAvailableTablesAsync();
            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error, timestamp = DateTime.UtcNow });
            }

            var tables = result.Value!.Select(ToDto).ToList();
            _logger.LogInformation("[TableController] Returning {Count} tables", tables.Count);

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

            var dto = ToDto(result.Value!);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TableController] Error getting table {TableId}: {Error}", tableId, ex.Message);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateTable([FromBody] CreateTableRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { error = "Table name is required" });
            }

            _logger.LogInformation("[TableController] Creating table: {Name}", request.Name);

            var result = await _tableService.CreateTableAsync(request.Name);
            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error });
            }

            var dto = ToDto(result.Value!);
            _logger.LogInformation("[TableController] Table created successfully: {TableId}", dto.Id);

            return CreatedAtAction(nameof(GetTable), new { tableId = dto.Id }, dto);
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

            // TODO: Obtener PlayerId del usuario autenticado
            // Por ahora usamos uno temporal - esto se arreglará con la autenticación
            var playerId = PlayerId.New();

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
                gameServiceType = _gameService.GetType().Name
            };

            var result = await _tableService.GetAvailableTablesAsync();

            return Ok(new
            {
                debugInfo,
                serviceCallSuccess = result.IsSuccess,
                serviceError = result.IsSuccess ? null : result.Error,
                tableCount = result.IsSuccess ? result.Value!.Count : 0,
                tables = result.IsSuccess ? result.Value!.Select(t => new {
                    id = t.Id.ToString(),
                    name = t.Name,
                    status = t.Status.ToString(),
                    seatCount = t.Seats?.Count ?? 0
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