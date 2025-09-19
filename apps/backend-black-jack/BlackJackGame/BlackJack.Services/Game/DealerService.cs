// BlackJack.Services.Game/DealerService.cs - CORREGIDO
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

        // PROBLEMA: Tu modelo actual usa HandIds (List<Guid>) en lugar de objetos Hand
        // Esta implementación necesita ser adaptada según tu arquitectura actual

        // Opción 1: Si tienes un servicio para manejar manos por HandId
        foreach (var seat in occupiedSeats)
        {
            if (seat.Player != null && seat.Player.HandIds.Any())
            {
                var handId = seat.Player.HandIds.First();
                // TODO: Necesitas un servicio o repositorio para obtener/actualizar Hand por handId
                // var hand = await _handService.GetHandAsync(handId);
                // var card = table.Deck.DealCard();
                // hand.AddCard(card);
                // await _handService.UpdateHandAsync(hand);
            }
        }

        // PROBLEMA: BlackjackTable no tiene propiedad DealerHand
        // Necesitas agregar esta propiedad al modelo o manejar el dealer de otra forma

        // TODO: Agregar DealerHand a BlackjackTable o usar un approach diferente
        // var dealerCard1 = table.Deck.DealCard();
        // table.DealerHand.AddCard(dealerCard1);

        // Segunda ronda de cartas para jugadores
        foreach (var seat in occupiedSeats)
        {
            if (seat.Player != null && seat.Player.HandIds.Any())
            {
                var handId = seat.Player.HandIds.First();
                // TODO: Misma lógica que arriba
            }
        }

        // TODO: Segunda carta para el dealer
        // var dealerCard2 = table.Deck.DealCard();
        // table.DealerHand.AddCard(dealerCard2);
    }

    // MÉTODO ALTERNATIVO SIMPLIFICADO - Para que compile mientras defines la arquitectura
    public void DealInitialCardsSimplified(BlackjackTable table)
    {
        var occupiedSeats = table.Seats.Where(s => s.IsOccupied).ToList();

        // Por ahora, solo registra que las cartas fueron "repartidas"
        // Necesitarás implementar la lógica real según tu arquitectura de manos
        foreach (var seat in occupiedSeats)
        {
            if (seat.Player != null)
            {
                // Simulación: crear nuevos HandIds si no existen
                if (!seat.Player.HandIds.Any())
                {
                    seat.Player.AddHandId(Guid.NewGuid());
                }
            }
        }
    }
}