using black_jack_backend.Entities;
using black_jack_backend.Data;
using Microsoft.EntityFrameworkCore;

namespace black_jack_backend.Modules;

public static class RoomsModule
{
    public static IServiceCollection AddRoomsModule(this IServiceCollection services)
    {
        services.AddScoped<IRoomService, RoomService>();
        return services;
    }
}

public interface IRoomService
{
    Task<Room> CreateRoomAsync(string name, int numDecks, decimal minBet, decimal maxBet);
    Task<List<Room>> ListRoomsAsync();
    Task<Room?> GetRoomDetailsAsync(int roomId);
    Task<bool> JoinRoomAsync(int roomId, int userId, string nickname, int seatIndex);
    Task<bool> LeaveRoomAsync(int roomId, int playerId);
    Task<bool> CloseRoomAsync(int roomId, int userId);
}

public class RoomService : IRoomService
{
    private readonly ApplicationDbContext _context;

    public RoomService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Room> CreateRoomAsync(string name, int numDecks, decimal minBet, decimal maxBet)
    {
        var room = new Room
        {
            Code = GenerateRoomCode(),
            Name = name,
            NumDecks = numDecks,
            MinBet = minBet,
            MaxBet = maxBet,
            IsActive = true
        };

        _context.Rooms.Add(room);
        await _context.SaveChangesAsync();

        // Create initial Round with phase='waiting'
        var round = new Round
        {
            RoomId = room.Id,
            Phase = "waiting"
        };

        _context.Rounds.Add(round);
        await _context.SaveChangesAsync();

        return room;
    }

    public async Task<List<Room>> ListRoomsAsync()
    {
        return await _context.Rooms.Where(r => r.IsActive).ToListAsync();
    }

    public async Task<Room?> GetRoomDetailsAsync(int roomId)
    {
        return await _context.Rooms.FindAsync(roomId);
    }

    public async Task<bool> JoinRoomAsync(int roomId, int userId, string nickname, int seatIndex)
    {
        // Validate seatIndex is unique in the room
        var existingPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.RoomId == roomId && p.SeatIndex == seatIndex);

        if (existingPlayer != null)
            return false; // Seat already taken

        var player = new Player
        {
            UserId = userId,
            RoomId = roomId,
            Nickname = nickname,
            SeatIndex = seatIndex
        };

        _context.Players.Add(player);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> LeaveRoomAsync(int roomId, int playerId)
    {
        var player = await _context.Players.FindAsync(playerId);
        if (player?.RoomId == roomId)
        {
            _context.Players.Remove(player);
            await _context.SaveChangesAsync();
            return true;
        }
        return false;
    }

    public async Task<bool> CloseRoomAsync(int roomId, int userId)
    {
        var room = await _context.Rooms.FindAsync(roomId);
        if (room != null)
        {
            room.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }
        return false;
    }

    private string GenerateRoomCode()
    {
        // Generate a unique 6-character room code
        var random = new Random();
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}