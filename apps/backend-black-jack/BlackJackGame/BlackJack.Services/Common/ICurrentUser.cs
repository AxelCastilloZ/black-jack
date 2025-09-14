using BlackJack.Domain.Models.Users;

namespace BlackJack.Services.Common;

public interface ICurrentUser
{
    PlayerId? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
}