using Blackjack.Domain.Entities;

namespace Blackjack.Application.Interfaces;

public interface IUserService
{
    Task<decimal> GetBalanceAsync(int userId);
    Task UpdateBalanceAsync(int userId, decimal newBalance);
}
