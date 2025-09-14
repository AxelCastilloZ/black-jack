using Microsoft.AspNetCore.Mvc;
using BlackJack.Services.Game;
using BlackJack.Services.Table;
using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Betting;

namespace BlackJackGame.Controllers;

public class GameController : BaseController
{
    private readonly IGameService _gameService;
    private readonly ITableService _tableService;

    public GameController(IGameService gameService, ITableService tableService)
    {
        _gameService = gameService;
        _tableService = tableService;
    }

    [HttpGet("tables")]
    public async Task<IActionResult> GetAvailableTables()
    {
        var result = await _tableService.GetAvailableTablesAsync();
        return HandleResult(result);
    }

    [HttpPost("tables")]
    public async Task<IActionResult> CreateTable([FromBody] CreateTableRequest request)
    {
        var result = await _tableService.CreateTableAsync(request.Name);
        return HandleResult(result);
    }

    [HttpGet("tables/{tableId}")]
    public async Task<IActionResult> GetTable(string tableId)
    {
        var result = await _gameService.GetTableAsync(TableId.From(Guid.Parse(tableId)));
        return HandleResult(result);
    }

    [HttpPost("tables/{tableId}/join")]
    public async Task<IActionResult> JoinTable(string tableId, [FromBody] JoinTableRequest request)
    {
        var playerId = PlayerId.New(); // TODO: Get from current user
        var result = await _gameService.JoinTableAsync(
            TableId.From(Guid.Parse(tableId)),
            playerId,
            request.SeatPosition);
        return HandleResult(result);
    }

    [HttpPost("tables/{tableId}/bet")]
    public async Task<IActionResult> PlaceBet(string tableId, [FromBody] PlaceBetRequest request)
    {
        var playerId = PlayerId.New(); // TODO: Get from current user
        var bet = Bet.Create(request.Amount);
        var result = await _gameService.PlaceBetAsync(
            TableId.From(Guid.Parse(tableId)),
            playerId,
            bet);
        return HandleResult(result);
    }
}

public class CreateTableRequest
{
    public string Name { get; set; } = string.Empty;
}

public class JoinTableRequest
{
    public int SeatPosition { get; set; }
}

public class PlaceBetRequest
{
    public decimal Amount { get; set; }
}