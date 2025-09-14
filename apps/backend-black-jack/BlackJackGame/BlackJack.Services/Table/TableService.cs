using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;

namespace BlackJack.Services.Table;

public class TableService : ITableService
{
    public async Task<Result<List<BlackjackTable>>> GetAvailableTablesAsync()
    {
        await Task.CompletedTask;
        return Result<List<BlackjackTable>>.Failure("Not implemented yet");
    }

    public async Task<Result<BlackjackTable>> GetTableAsync(TableId tableId)
    {
        await Task.CompletedTask;
        return Result<BlackjackTable>.Failure("Not implemented yet");
    }

    public async Task<Result<BlackjackTable>> CreateTableAsync(string name)
    {
        try
        {
            var table = BlackjackTable.Create(name);
            return Result<BlackjackTable>.Success(table);
        }
        catch (Exception ex)
        {
            return Result<BlackjackTable>.Failure($"Failed to create table: {ex.Message}");
        }
    }

    public async Task<Result> DeleteTableAsync(TableId tableId)
    {
        await Task.CompletedTask;
        return Result.Failure("Not implemented yet");
    }
}