using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Enums;

namespace BlackJack.Services.Game;

public interface IHandEvaluationService
{
    bool IsBlackjack(Hand hand);
    bool IsBust(Hand hand);
    HandResult CompareHands(Hand playerHand, Hand dealerHand);
    int GetHandValue(Hand hand);
}

