
using BlackJack.Domain.Models.Betting;
using BlackJack.Domain.Models.Game;
using BlackJack.Services.Game;

namespace BlackJack.Services.Betting;

public class PayoutService : IPayoutService
{
    public Payout CalculatePayout(Bet originalBet, HandResult result)
    {
        return result switch
        {
            HandResult.PlayerBlackjack => Payout.Blackjack(originalBet),
            HandResult.PlayerWins => Payout.Win(originalBet),
            HandResult.Push => Payout.Push(originalBet),
            HandResult.DealerWins => Payout.Loss(),
            _ => Payout.Loss()
        };
    }

    public Money CalculateWinnings(Bet originalBet, HandResult result)
    {
        var payout = CalculatePayout(originalBet, result);
        return payout.Amount;
    }
}