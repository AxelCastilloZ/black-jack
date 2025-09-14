using BlackJack.Domain.Models.Users;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Users;

public interface IUserRepository : IRepository<UserProfile>
{
    Task<UserProfile?> GetByPlayerIdAsync(PlayerId playerId);
    Task<UserProfile?> GetByEmailAsync(string email);
    Task<bool> EmailExistsAsync(string email);
}