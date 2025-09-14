using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Game;

public interface ITableRepository : IRepository<BlackjackTable>
{
    Task<List<BlackjackTable>> GetAvailableTablesAsync();
    Task<BlackjackTable?> GetTableWithPlayersAsync(TableId tableId);
    Task<List<BlackjackTable>> GetTablesByStatusAsync(Domain.Enums.GameStatus status);
}