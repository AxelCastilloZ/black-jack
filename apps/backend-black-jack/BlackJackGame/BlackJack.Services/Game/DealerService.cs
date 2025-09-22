// BlackJack.Services.Game/DealerService.cs - IMPLEMENTED
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Cards;
using BlackJack.Data.Repositories.Game;
using Microsoft.Extensions.Logging;

namespace BlackJack.Services.Game;

public class DealerService : IDealerService
{
    private readonly IHandRepository _handRepository;
    private readonly ILogger<DealerService> _logger;

    public DealerService(IHandRepository handRepository, ILogger<DealerService> logger)
    {
        _handRepository = handRepository;
        _logger = logger;
    }

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

    public async Task DealInitialCardsAsync(BlackjackTable table)
    {
        _logger.LogInformation("[DealerService] Dealing initial cards for table {TableId}", table.Id);

        var occupiedSeats = table.Seats.Where(s => s.IsOccupied).ToList();

        // Create dealer hand
        var dealerHand = Hand.Create();
        await _handRepository.AddAsync(dealerHand);
        table.SetDealerHandId(dealerHand.Id);

        // Deal 2 cards to each player
        foreach (var seat in occupiedSeats)
        {
            if (seat.Player != null)
            {
                // Create player hand
                var playerHand = Hand.Create();
                await _handRepository.AddAsync(playerHand);
                seat.Player.AddHandId(playerHand.Id);

                // Deal 2 cards to player
                var card1 = table.DealCard();
                var card2 = table.DealCard();
                playerHand.AddCard(card1);
                playerHand.AddCard(card2);
                await _handRepository.UpdateAsync(playerHand);

                _logger.LogInformation("[DealerService] Dealt cards {Card1} and {Card2} to player {PlayerId}",
                    card1.GetDisplayName(), card2.GetDisplayName(), seat.Player.PlayerId);
            }
        }

        // Deal 2 cards to dealer
        var dealerCard1 = table.DealCard();
        var dealerCard2 = table.DealCard();
        dealerHand.AddCard(dealerCard1);
        dealerHand.AddCard(dealerCard2);
        await _handRepository.UpdateAsync(dealerHand);

        _logger.LogInformation("[DealerService] Dealt cards {Card1} and {Card2} to dealer",
            dealerCard1.GetDisplayName(), dealerCard2.GetDisplayName());

        _logger.LogInformation("[DealerService] Initial cards dealt successfully for table {TableId}", table.Id);
    }

    // Legacy method for backward compatibility
    public void DealInitialCards(BlackjackTable table)
    {
        throw new InvalidOperationException("Use DealInitialCardsAsync instead of DealInitialCards");
    }
}