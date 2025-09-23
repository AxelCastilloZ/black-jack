
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Cards;


namespace BlackJack.Services.Game;

public interface IDealerService
{
    /// <summary>
    /// Determina si el dealer debe pedir otra carta (valor < 17)
    /// </summary>
    bool ShouldHit(Hand dealerHand);

    /// <summary>
    /// Juega la mano del dealer siguiendo las reglas estándar
    /// </summary>
    Hand PlayDealerHand(Hand dealerHand, Deck deck);

    /// <summary>
    /// NUEVO: Reparte cartas iniciales usando RoomPlayers (arquitectura nueva)
    /// </summary>
    Task DealInitialCardsAsync(BlackjackTable table, List<RoomPlayer> seatedPlayers);

    /// <summary>
    /// DEPRECATED: Reparte cartas iniciales usando Seats (arquitectura antigua)
    /// </summary>
    [Obsolete("Use DealInitialCardsAsync(table, seatedPlayers) instead")]
    Task DealInitialCardsAsync(BlackjackTable table);

    /// <summary>
    /// LEGACY: Método síncrono deprecated
    /// </summary>
    [Obsolete("Use DealInitialCardsAsync instead")]
    void DealInitialCards(BlackjackTable table);
}