// Services/Game/IGameService.cs
using System.Threading.Tasks;
using BlackJack.Domain.Enums;
using BlackJack.Domain.Models.Betting;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;

namespace BlackJack.Services.Game;

public interface IGameService
{
    Task<Result<BlackjackTable>> CreateTableAsync(string name, Money minBet, Money maxBet);
    Task<Result<BlackjackTable>> GetTableAsync(TableId tableId);

    Task<Result> JoinTableAsync(TableId tableId, PlayerId playerId, int seatPosition);
    Task<Result> LeaveTableAsync(TableId tableId, PlayerId playerId);

    Task<Result> PlaceBetAsync(TableId tableId, PlayerId playerId, Bet bet);

    Task<Result> StartRoundAsync(TableId tableId);
    Task<Result> PlayerActionAsync(TableId tableId, PlayerId playerId, PlayerAction action);
    Task<Result> EndRoundAsync(TableId tableId);

    // Necesario para GameHub.ResetTable
    Task<Result> ResetTableAsync(TableId tableId);
}
