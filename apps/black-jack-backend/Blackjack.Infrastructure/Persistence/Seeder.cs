using Blackjack.Domain.Entities;
using Blackjack.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Blackjack.Infrastructure.Persistence;

public static class Seeder
{
    public static async Task SeedDemoDataAsync(ApplicationDbContext context)
    {
        // Check if demo data already exists
        if (await context.Rooms.AnyAsync())
            return; // Already seeded

        // Create demo room
        var demoRoom = new Room
        {
            Code = "DEMO01",
            Name = "Demo Room - Welcome to Blackjack!",
            IsActive = true,
            NumDecks = 6,
            MinBet = 500m,
            MaxBet = 10.000m,
            PayoutBlackjack = 1.5m,
            SurrenderEnabled = true
        };

        context.Rooms.Add(demoRoom);
        await context.SaveChangesAsync();

        // Create 2 demo users
        var demoUser1 = new User
        {
            DisplayName = "DemoUser1",
            Balance = 10000m
        };

        var demoUser2 = new User
        {
            DisplayName = "DemoUser2", 
            Balance = 10000m
        };

        context.Users.AddRange(demoUser1, demoUser2);
        await context.SaveChangesAsync();

        // Create initial round
        var demoRound = new Round
        {
            RoomId = demoRoom.Id,
            Phase = RoundPhase.Waiting,
            ShoePosition = 0,
            DealerHandJSON = "[]",
            UpdatedAt = DateTime.UtcNow
        };

        context.Rounds.Add(demoRound);
        await context.SaveChangesAsync();

        // Create 2 demo players
        var player1 = new Player
        {
            UserId = demoUser1.Id,
            RoomId = demoRoom.Id,
            Nickname = "DemoPlayer1",
            SeatIndex = 0,
            BalanceShadow = 10.000m,
            SocketId = null
        };

        var player2 = new Player
        {
            UserId = demoUser2.Id,
            RoomId = demoRoom.Id,
            Nickname = "DemoPlayer2",
            SeatIndex = 1,
            BalanceShadow = 10.000m,
            SocketId = null
        };

        context.Players.AddRange(player1, player2);
        await context.SaveChangesAsync();

        // Create action log entries
        var actions = new List<ActionLog>
        {
            new ActionLog
            {
                RoomId = demoRoom.Id,
                RoundId = demoRound.Id,
                PlayerId = null,
                ActionType = "room_created",
                PayloadJSON = "{\"message\": \"Demo room created\"}"
            },
            new ActionLog
            {
                RoomId = demoRoom.Id,
                RoundId = demoRound.Id,
                PlayerId = player1.Id,
                ActionType = "player_joined",
                PayloadJSON = "{\"seatIndex\": 0, \"nickname\": \"DemoPlayer1\"}"
            },
            new ActionLog
            {
                RoomId = demoRoom.Id,
                RoundId = demoRound.Id,
                PlayerId = player2.Id,
                ActionType = "player_joined",
                PayloadJSON = "{\"seatIndex\": 1, \"nickname\": \"DemoPlayer2\"}"
            }
        };

        context.ActionLogs.AddRange(actions);
        await context.SaveChangesAsync();
    }
}