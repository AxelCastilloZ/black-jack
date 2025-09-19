// IRoomPlayerRepository.cs
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Game;

public interface IRoomPlayerRepository : IRepository<RoomPlayer>
{
    Task<RoomPlayer?> GetByPlayerIdAsync(PlayerId playerId);
    Task<List<RoomPlayer>> GetPlayersByRoomAsync(Guid roomId);
    Task<RoomPlayer?> GetPlayerInRoomAsync(Guid roomId, PlayerId playerId);
    Task<bool> IsPlayerInAnyRoomAsync(PlayerId playerId);
}