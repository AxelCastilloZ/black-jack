using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Cards;

namespace BlackJack.Services.Game;

public class DealerService : IDealerService
{
    public bool ShouldHit(Hand dealerHand)
    {
       
        return dealerHand.Value < 17;
    }

    public Hand PlayDealerHand(Hand dealerHand, Deck deck)
    {
        while (ShouldHit(dealerHand))
        {
            var card = deck.DealCard();
            dealerHand.AddCard(card);
        }

        return dealerHand;
    }

    public void DealInitialCards(BlackjackTable table)
    {
        
        var occupiedSeats = table.Seats.Where(s => s.IsOccupied).ToList();

        
        foreach (var seat in occupiedSeats)
        {
            var card = table.Deck.DealCard();
            seat.Player!.Hands.First().AddCard(card);
        }

        
        var dealerCard1 = table.Deck.DealCard();
        table.DealerHand.AddCard(dealerCard1);

        
        foreach (var seat in occupiedSeats)
        {
            var card = table.Deck.DealCard();
            seat.Player!.Hands.First().AddCard(card);
        }

        
        var dealerCard2 = table.Deck.DealCard();
        table.DealerHand.AddCard(dealerCard2);
    }
}