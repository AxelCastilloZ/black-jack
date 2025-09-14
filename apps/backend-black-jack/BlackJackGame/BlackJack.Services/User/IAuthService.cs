using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;

namespace BlackJack.Services.User;

public interface IAuthService
{
    Task<Result<string>> LoginAsync(string email, string password);
    Task<Result<UserProfile>> RegisterAsync(string displayName, string email, string password);
    Task<Result<string>> RefreshTokenAsync(string refreshToken);
    Task<Result> LogoutAsync(PlayerId playerId);
    Task<Result<UserProfile>> ValidateTokenAsync(string token);
}