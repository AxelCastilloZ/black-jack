using Microsoft.AspNetCore.Mvc;
using Blackjack.Application.Interfaces;
using Blackjack.Application.DTOs;
using Blackjack.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Blackjack.WebApi.Controllers;

[ApiController]
[Route("game")]
public class BlackjackController : ControllerBase
{
    private readonly IGameService _gameService;

    public BlackjackController(IGameService gameService)
    {
        _gameService = gameService;
    }

    [HttpGet("{roomId}/state")]
    public async Task<IActionResult> GetGameState(int roomId)
    {
        var state = await _gameService.GetGameStateAsync(roomId);
        
        if (state == null)
            return NotFound("No active round found");

        return Ok(state);
    }

    [HttpPost("{roomId}/start-betting")]
    public async Task<IActionResult> StartBetting(int roomId)
    {
        var success = await _gameService.StartBettingAsync(roomId);
        
        if (!success)
            return NotFound("No waiting round found");

        return Ok(new { Message = "Betting phase started", Phase = "betting" });
    }

    [HttpPost("{roomId}/reset")]
    public async Task<IActionResult> ResetGame(int roomId)
    {
        var success = await _gameService.ResetGameAsync(roomId);
        
        if (!success)
            return NotFound("No round found");

        return Ok(new { Message = "Game reset successfully", Phase = "waiting" });
    }
}