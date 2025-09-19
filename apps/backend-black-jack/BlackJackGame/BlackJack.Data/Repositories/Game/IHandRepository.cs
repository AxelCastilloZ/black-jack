// IHandRepository.cs
using BlackJack.Domain.Models.Game;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Game;

public interface IHandRepository : IRepository<Hand>
{
    Task<List<Hand>> GetPlayerHandsAsync(Guid playerId);
    Task<Hand?> GetDealerHandAsync(Guid tableId);
    Task<List<Hand>> GetHandsByTableAsync(Guid tableId);
}