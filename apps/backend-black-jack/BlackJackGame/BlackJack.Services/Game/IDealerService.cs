using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Cards;

namespace BlackJack.Services.Game;

public interface IDealerService
{
    bool ShouldHit(Hand dealerHand);
    Hand PlayDealerHand(Hand dealerHand, Deck deck);
    void DealInitialCards(BlackjackTable table);
}