using BlackJack.Domain.Models.Betting;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;

namespace BlackJack.Services.Betting;

public interface IBettingService
{
    Result<Bet> CreateBet(Money amount);
    Result ValidateBet(Bet bet, Money minBet, Money maxBet, Money playerBalance);
    Result ProcessBet(PlayerId playerId, Bet bet);
}