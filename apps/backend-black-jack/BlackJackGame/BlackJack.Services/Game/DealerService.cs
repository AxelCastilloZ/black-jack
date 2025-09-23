// BlackJack.Services.Game/DealerService.cs - REFACTORIZADO para usar RoomPlayers
using BlackJack.Data.Repositories.Game;
using BlackJack.Domain.Models.Cards;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Services;
using Microsoft.Extensions.Logging;

namespace BlackJack.Services.Game;

public class DealerService : IDealerService
{
    private readonly IHandRepository _handRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly ILogger<DealerService> _logger;

    public DealerService(
        IHandRepository handRepository,
        IPlayerRepository playerRepository,
        ILogger<DealerService> logger)
    {
        _handRepository = handRepository;
        _playerRepository = playerRepository;
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

    // NUEVO MÉTODO: Usar RoomPlayers en lugar de Seats
    public async Task DealInitialCardsAsync(BlackjackTable table, List<RoomPlayer> seatedPlayers)
    {
        _logger.LogInformation("[DealerService] Dealing initial cards for table {TableId} with {PlayerCount} seated players",
            table.Id, seatedPlayers.Count);

        if (!seatedPlayers.Any())
        {
            _logger.LogWarning("[DealerService] No seated players found for table {TableId}", table.Id);
            return;
        }

        try
        {
            // 1. Crear dealer hand
            var dealerHand = Hand.Create();
            await _handRepository.AddAsync(dealerHand);
            table.SetDealerHandId(dealerHand.Id);

            _logger.LogInformation("[DealerService] Created dealer hand {HandId}", dealerHand.Id);

            // 2. Para cada RoomPlayer sentado, crear Hand real y reemplazar HandId ficticio
            foreach (var roomPlayer in seatedPlayers.Where(p => p.SeatPosition.HasValue))
            {
                _logger.LogInformation("[DealerService] Processing player {Name} at seat {Seat}",
                    roomPlayer.Name, roomPlayer.SeatPosition);

                // Obtener Player entity
                Player? player = null;
                if (roomPlayer.PlayerEntityId != Guid.Empty)
                {
                    player = await _playerRepository.GetByIdAsync(roomPlayer.PlayerEntityId);
                }

                if (player == null)
                {
                    player = await _playerRepository.GetByPlayerIdAsync(roomPlayer.PlayerId);
                }

                if (player == null)
                {
                    _logger.LogError("[DealerService] Player entity not found for {Name} ({PlayerId})",
                        roomPlayer.Name, roomPlayer.PlayerId);
                    continue;
                }

                // Crear Hand real
                var playerHand = Hand.Create();
                await _handRepository.AddAsync(playerHand);

                // CRÍTICO: Reemplazar HandIds ficticios con HandId real
                player.ClearHandIds();
                player.AddHandId(playerHand.Id);
                await _playerRepository.UpdateAsync(player);

                // Repartir 2 cartas al jugador
                if (table.Deck.IsEmpty)
                {
                    _logger.LogError("[DealerService] Deck is empty, cannot deal cards");
                    return;
                }

                var card1 = table.DealCard();
                var card2 = table.DealCard();

                playerHand.AddCard(card1);
                playerHand.AddCard(card2);
                await _handRepository.UpdateAsync(playerHand);

                _logger.LogInformation("[DealerService] Dealt cards {Card1} and {Card2} to player {Name} (Hand: {HandId})",
                    card1.GetDisplayName(), card2.GetDisplayName(), roomPlayer.Name, playerHand.Id);
            }

            // 3. Repartir 2 cartas al dealer
            if (table.Deck.Count < 2)
            {
                _logger.LogError("[DealerService] Not enough cards in deck for dealer");
                return;
            }

            var dealerCard1 = table.DealCard();
            var dealerCard2 = table.DealCard();

            dealerHand.AddCard(dealerCard1);
            dealerHand.AddCard(dealerCard2);
            await _handRepository.UpdateAsync(dealerHand);

            _logger.LogInformation("[DealerService] Dealt cards {Card1} and {Card2} to dealer (Hand: {HandId})",
                dealerCard1.GetDisplayName(), dealerCard2.GetDisplayName(), dealerHand.Id);

            _logger.LogInformation("[DealerService] Initial cards dealt successfully for table {TableId}", table.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DealerService] Error dealing initial cards: {Message}", ex.Message);
            throw;
        }
    }

    // MÉTODO ORIGINAL - DEPRECATED pero mantenido por compatibilidad
    public async Task DealInitialCardsAsync(BlackjackTable table)
    {
        _logger.LogError("[DealerService] DealInitialCardsAsync(table) is DEPRECATED - use DealInitialCardsAsync(table, seatedPlayers) instead");

        // Buscar en Seats (arquitectura antigua) - probablemente vacío
        var occupiedSeats = table.Seats.Where(s => s.IsOccupied).ToList();

        if (!occupiedSeats.Any())
        {
            _logger.LogError("[DealerService] No occupied seats found - this method is deprecated and should use RoomPlayers");
            return;
        }

        // Código original como fallback
        _logger.LogInformation("[DealerService] [DEPRECATED] Dealing initial cards for table {TableId}", table.Id);

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

                _logger.LogInformation("[DealerService] [DEPRECATED] Dealt cards {Card1} and {Card2} to player {PlayerId}",
                    card1.GetDisplayName(), card2.GetDisplayName(), seat.Player.PlayerId);
            }
        }

        // Deal 2 cards to dealer
        var dealerCard1 = table.DealCard();
        var dealerCard2 = table.DealCard();
        dealerHand.AddCard(dealerCard1);
        dealerHand.AddCard(dealerCard2);
        await _handRepository.UpdateAsync(dealerHand);

        _logger.LogInformation("[DealerService] [DEPRECATED] Dealt cards {Card1} and {Card2} to dealer",
            dealerCard1.GetDisplayName(), dealerCard2.GetDisplayName());
    }

    // Legacy method for backward compatibility
    public void DealInitialCards(BlackjackTable table)
    {
        throw new InvalidOperationException("Use DealInitialCardsAsync instead of DealInitialCards");
    }
}