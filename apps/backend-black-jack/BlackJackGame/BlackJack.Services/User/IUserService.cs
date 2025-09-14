using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Betting;
using BlackJack.Services.Common;

namespace BlackJack.Services.User;

public interface IUserService
{
    Task<Result<UserProfile>> CreateUserAsync(string displayName, string email);
    Task<Result<UserProfile>> GetUserAsync(PlayerId playerId);
    Task<Result> UpdateBalanceAsync(PlayerId playerId, Money newBalance);
    Task<Result> RecordGameResultAsync(PlayerId playerId, bool won, Money winnings);
    Task<Result<UserProfile>> UpdateProfileAsync(PlayerId playerId, string displayName);
}