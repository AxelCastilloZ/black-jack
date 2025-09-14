using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;

namespace BlackJack.Services.User;

public class AuthService : IAuthService
{
    public async Task<Result<string>> LoginAsync(string email, string password)
    {
        await Task.CompletedTask;
        return Result<string>.Failure("Not implemented yet");
    }

    public async Task<Result<UserProfile>> RegisterAsync(string displayName, string email, string password)
    {
        await Task.CompletedTask;
        return Result<UserProfile>.Failure("Not implemented yet");
    }

    public async Task<Result<string>> RefreshTokenAsync(string refreshToken)
    {
        await Task.CompletedTask;
        return Result<string>.Failure("Not implemented yet");
    }

    public async Task<Result> LogoutAsync(PlayerId playerId)
    {
        await Task.CompletedTask;
        return Result.Failure("Not implemented yet");
    }

    public async Task<Result<UserProfile>> ValidateTokenAsync(string token)
    {
        await Task.CompletedTask;
        return Result<UserProfile>.Failure("Not implemented yet");
    }
}