// BlackJack.Services.Table/ITableService.cs - CORREGIDO CON GUID
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;

namespace BlackJack.Services.Table;

public interface ITableService
{
    Task<Result<List<BlackjackTable>>> GetAvailableTablesAsync();
    Task<Result<BlackjackTable>> GetTableAsync(Guid tableId);
    Task<Result<BlackjackTable>> CreateTableAsync(string name);
    Task<Result> DeleteTableAsync(Guid tableId);
}