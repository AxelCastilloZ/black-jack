using Microsoft.AspNetCore.Mvc;
using black_jack_backend.Modules;
using black_jack_backend.Entities;
using black_jack_backend.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace black_jack_backend.Controllers;

[ApiController]
[Route("game")]
public class BlackjackController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IRoomService _roomService;

    public BlackjackController(ApplicationDbContext context, IRoomService roomService)
    {
        _context = context;
        _roomService = roomService;
    }

    [HttpGet("{roomId}/state")]
    public async Task<IActionResult> GetGameState(int roomId)
    {
        var round = await _context.Rounds
            .FirstOrDefaultAsync(r => r.RoomId == roomId && r.Phase != "finished");

        if (round == null)
            return NotFound("No active round found");

        // Get dealer hand but don't reveal hidden card
        var dealerHand = JsonSerializer.Deserialize<List<string>>(round.DealerHandJSON) ?? new List<string>();
        var visibleCards = dealerHand.Skip(1).ToList(); // Skip first card (hidden)

        var state = new
        {
            RoomId = roomId,
            Phase = round.Phase,
            DealerVisibleCards = visibleCards,
            DealerHiddenCardCount = dealerHand.Count > 0 ? 1 : 0,
            ShoePosition = round.ShoePosition,
            UpdatedAt = round.UpdatedAt
        };

        return Ok(state);
    }

    [HttpPost("{roomId}/start-betting")]
    public async Task<IActionResult> StartBetting(int roomId)
    {
        var round = await _context.Rounds
            .FirstOrDefaultAsync(r => r.RoomId == roomId && r.Phase == "waiting");

        if (round == null)
            return NotFound("No waiting round found");

        round.Phase = "betting";
        round.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Betting phase started", Phase = "betting" });
    }

    [HttpPost("{roomId}/reset")]
    public async Task<IActionResult> ResetGame(int roomId)
    {
        var round = await _context.Rounds
            .FirstOrDefaultAsync(r => r.RoomId == roomId);

        if (round == null)
            return NotFound("No round found");

        // Reset round to waiting phase
        round.Phase = "waiting";
        round.ShoePosition = 0;
        round.DealerHandJSON = "[]";
        round.UpdatedAt = DateTime.UtcNow;

        // Clear all hands for this round
        var hands = await _context.Hands.Where(h => h.RoundId == round.Id).ToListAsync();
        _context.Hands.RemoveRange(hands);

        await _context.SaveChangesAsync();

        return Ok(new { Message = "Game reset successfully", Phase = "waiting" });
    }
}