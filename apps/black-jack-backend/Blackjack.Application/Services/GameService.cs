using Blackjack.Application.Interfaces;
using Blackjack.Infrastructure.Persistence;
using Blackjack.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Blackjack.Application.Services;

public class GameService : IGameService
{
    private readonly ApplicationDbContext _context;

    public GameService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<object> GetGameStateAsync(int roomId)
    {
        var round = await _context.Rounds
            .FirstOrDefaultAsync(r => r.RoomId == roomId && r.Phase != RoundPhase.Finished);

        if (round == null)
            return null;

        var dealerHand = JsonSerializer.Deserialize<List<string>>(round.DealerHandJSON) ?? new List<string>();
        var visibleCards = dealerHand.Skip(1).ToList();

        return new
        {
            RoomId = roomId,
            Phase = round.Phase,
            DealerVisibleCards = visibleCards,
            DealerHiddenCardCount = dealerHand.Count > 0 ? 1 : 0,
            ShoePosition = round.ShoePosition,
            UpdatedAt = round.UpdatedAt
        };
    }

    public async Task<bool> StartBettingAsync(int roomId)
    {
        var round = await _context.Rounds
            .FirstOrDefaultAsync(r => r.RoomId == roomId && r.Phase == RoundPhase.Waiting);

        if (round == null)
            return false;

        round.Phase = RoundPhase.Betting;
        round.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResetGameAsync(int roomId)
    {
        var round = await _context.Rounds
            .FirstOrDefaultAsync(r => r.RoomId == roomId);

        if (round == null)
            return false;

        round.Phase = RoundPhase.Waiting;
        round.ShoePosition = 0;
        round.DealerHandJSON = "[]";
        round.UpdatedAt = DateTime.UtcNow;

        var hands = await _context.Hands.Where(h => h.RoundId == round.Id).ToListAsync();
        _context.Hands.RemoveRange(hands);

        await _context.SaveChangesAsync();
        return true;
    }
}
