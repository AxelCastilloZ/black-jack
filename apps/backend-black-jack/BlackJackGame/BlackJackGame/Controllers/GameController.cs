using Microsoft.AspNetCore.Mvc;
using BlackJack.Services.Game;
using BlackJack.Services.Table;
using BlackJack.Domain.Models.Users;

namespace BlackJackGame.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase
{
    private readonly IGameService _gameService;
    private readonly ITableService _tableService;

    public GameController(IGameService gameService, ITableService tableService)
    {
        _gameService = gameService;
        _tableService = tableService;
    }

    public record TableSummaryDto(
        string id,
        string name,
        int playerCount,
        int maxPlayers,
        decimal minBet,
        decimal maxBet,
        string status
    );

    private static TableSummaryDto ToDto(BlackJack.Domain.Models.Game.BlackjackTable t) =>
        new(
            id: t.Id.ToString(),
            name: t.Name,
            playerCount: t.Seats.Count(s => s.IsOccupied),
            maxPlayers: t.Seats.Count,
            minBet: t.MinBet.Amount,
            maxBet: t.MaxBet.Amount,
            status: t.Status.ToString()
        );

    [HttpGet("tables")]
    public async Task<IActionResult> GetAvailableTables()
    {
        var result = await _tableService.GetAvailableTablesAsync();
        if (!result.IsSuccess)                       // <- aquí
            return BadRequest(new { error = result.Error });

        var dto = result.Value.Select(ToDto).ToList();
        return Ok(dto);
    }

    public class CreateTableRequest { public string Name { get; set; } = string.Empty; }

    [HttpPost("tables")]
    public async Task<IActionResult> CreateTable([FromBody] CreateTableRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "El nombre es requerido" });

        var result = await _tableService.CreateTableAsync(request.Name);
        if (!result.IsSuccess)                       // <- aquí
            return BadRequest(new { error = result.Error });

        var dto = ToDto(result.Value);
        return Ok(dto);
    }

    [HttpGet("tables/{tableId}")]
    public async Task<IActionResult> GetTable(string tableId)
    {
        if (!Guid.TryParse(tableId, out var guid))
            return BadRequest(new { error = "tableId inválido" });

        var result = await _gameService.GetTableAsync(TableId.From(guid));
        if (!result.IsSuccess)                       // <- aquí
            return BadRequest(new { error = result.Error });

        return Ok(ToDto(result.Value));
    }

    public class JoinTableRequest { public int SeatPosition { get; set; } }

    [HttpPost("tables/{tableId}/join")]
    public async Task<IActionResult> JoinTable(string tableId, [FromBody] JoinTableRequest request)
    {
        if (!Guid.TryParse(tableId, out var guid))
            return BadRequest(new { error = "tableId inválido" });

        var playerId = PlayerId.New();
        var result = await _gameService.JoinTableAsync(TableId.From(guid), playerId, request.SeatPosition);
        if (!result.IsSuccess)                       // <- aquí
            return BadRequest(new { error = result.Error });

        return Ok();
    }
}
