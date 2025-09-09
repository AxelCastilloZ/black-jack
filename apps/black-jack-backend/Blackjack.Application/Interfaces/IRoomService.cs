using Blackjack.Domain.Entities;

namespace Blackjack.Application.Interfaces;

public interface IRoomService
{
    Task<Room> CreateRoomAsync(string name, int numDecks, decimal minBet, decimal maxBet);
    Task<List<Room>> ListRoomsAsync();
    Task<Room?> GetRoomDetailsAsync(int roomId);
    Task<bool> JoinRoomAsync(int roomId, int userId, string nickname, int seatIndex);
    Task<bool> LeaveRoomAsync(int roomId, int playerId);
    Task<bool> CloseRoomAsync(int roomId, int userId);
}
