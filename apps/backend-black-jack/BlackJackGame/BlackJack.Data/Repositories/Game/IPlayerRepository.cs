// BlackJack.Data.Repositories.Game/IPlayerRepository.cs - CORREGIDO CON GUID
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Game;

public interface IPlayerRepository : IRepository<Player>
{
    Task<Player?> GetByPlayerIdAsync(PlayerId playerId);
    Task<List<Player>> GetPlayersByTableAsync(Guid tableId);
}