using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Enums;

namespace BlackJack.Services.Game;

public class HandEvaluationService : IHandEvaluationService
{
    public bool IsBlackjack(Hand hand)
    {
        return hand.Cards.Count == 2 && hand.Value == 21;
    }

    public bool IsBust(Hand hand)
    {
        return hand.Value > 21;
    }

    public HandResult CompareHands(Hand playerHand, Hand dealerHand)
    {
        // Check for player blackjack first
        if (IsBlackjack(playerHand) && !IsBlackjack(dealerHand))
            return HandResult.PlayerBlackjack;

        // Check for bust conditions
        if (IsBust(playerHand))
            return HandResult.DealerWins;

        if (IsBust(dealerHand))
            return HandResult.PlayerWins;

        // Check for both blackjack (push)
        if (IsBlackjack(playerHand) && IsBlackjack(dealerHand))
            return HandResult.Push;

        // Compare values
        if (playerHand.Value > dealerHand.Value)
            return HandResult.PlayerWins;
        else if (dealerHand.Value > playerHand.Value)
            return HandResult.DealerWins;
        else
            return HandResult.Push;
    }

    public int GetHandValue(Hand hand)
    {
        return hand.Value;
    }
}