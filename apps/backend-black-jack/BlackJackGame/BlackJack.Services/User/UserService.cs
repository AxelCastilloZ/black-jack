using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Betting;
using BlackJack.Services.Common;

namespace BlackJack.Services.User;

public class UserService : IUserService
{
    public async Task<Result<UserProfile>> CreateUserAsync(string displayName, string email)
    {
        try
        {
            var playerId = PlayerId.New();
            var profile = UserProfile.Create(playerId, displayName, email);
            return Result<UserProfile>.Success(profile);
        }
        catch (Exception ex)
        {
            return Result<UserProfile>.Failure($"Failed to create user: {ex.Message}");
        }
    }

    public async Task<Result<UserProfile>> GetUserAsync(PlayerId playerId)
    {
        await Task.CompletedTask;
        return Result<UserProfile>.Failure("Not implemented yet");
    }

    public async Task<Result> UpdateBalanceAsync(PlayerId playerId, Money newBalance)
    {
        await Task.CompletedTask;
        return Result.Failure("Not implemented yet");
    }

    public async Task<Result> RecordGameResultAsync(PlayerId playerId, bool won, Money winnings)
    {
        await Task.CompletedTask;
        return Result.Failure("Not implemented yet");
    }

    public async Task<Result<UserProfile>> UpdateProfileAsync(PlayerId playerId, string displayName)
    {
        await Task.CompletedTask;
        return Result<UserProfile>.Failure("Not implemented yet");
    }
}