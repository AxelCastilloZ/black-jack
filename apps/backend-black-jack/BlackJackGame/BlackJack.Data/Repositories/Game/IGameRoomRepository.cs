// IGameRoomRepository.cs
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Game;

public interface IGameRoomRepository : IRepository<GameRoom>
{
    Task<GameRoom?> GetByRoomCodeAsync(string roomCode);
    Task<GameRoom?> GetRoomWithPlayersAsync(Guid roomId);
    Task<GameRoom?> GetRoomWithPlayersAsync(string roomCode);
    Task<List<GameRoom>> GetActiveRoomsAsync();
    Task<List<GameRoom>> GetRoomsByStatusAsync(RoomStatus status);
    Task<bool> RoomCodeExistsAsync(string roomCode);
    Task<GameRoom?> GetPlayerCurrentRoomAsync(PlayerId playerId);
}