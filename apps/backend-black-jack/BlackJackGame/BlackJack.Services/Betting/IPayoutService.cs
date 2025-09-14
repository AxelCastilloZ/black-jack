using BlackJack.Domain.Models.Betting;
using BlackJack.Domain.Models.Game;
using BlackJack.Services.Game;

namespace BlackJack.Services.Betting;

public interface IPayoutService
{
    Payout CalculatePayout(Bet originalBet, HandResult result);
    Money CalculateWinnings(Bet originalBet, HandResult result);
}