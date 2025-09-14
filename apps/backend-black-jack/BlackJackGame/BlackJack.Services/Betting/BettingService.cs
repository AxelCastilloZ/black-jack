using BlackJack.Domain.Models.Betting;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;

namespace BlackJack.Services.Betting;

public class BettingService : IBettingService
{
    public Result<Bet> CreateBet(Money amount)
    {
        try
        {
            var bet = Bet.Create(amount.Amount);
            return Result<Bet>.Success(bet);
        }
        catch (Exception ex)
        {
            return Result<Bet>.Failure($"Invalid bet: {ex.Message}");
        }
    }

    public Result ValidateBet(Bet bet, Money minBet, Money maxBet, Money playerBalance)
    {
        if (bet.Amount.IsLessThan(minBet))
            return Result.Failure($"Bet amount {bet.Amount} is below minimum {minBet}");

        if (bet.Amount.IsGreaterThan(maxBet))
            return Result.Failure($"Bet amount {bet.Amount} exceeds maximum {maxBet}");

        if (playerBalance.IsLessThan(bet.Amount))
            return Result.Failure("Insufficient funds for this bet");

        return Result.Success();
    }

    public Result ProcessBet(PlayerId playerId, Bet bet)
    {
        // TODO: Implement bet processing logic with repository
        return Result.Success();
    }
}