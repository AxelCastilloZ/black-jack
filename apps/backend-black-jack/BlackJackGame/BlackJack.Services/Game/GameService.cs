// Services/Game/GameService.cs
using System.Linq;
using System.Threading.Tasks;
using BlackJack.Data.Repositories.Game;
using BlackJack.Domain.Enums;
using BlackJack.Domain.Models.Betting;
using BlackJack.Domain.Models.Cards;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;

namespace BlackJack.Services.Game
{
    public class GameService : IGameService
    {
        private readonly ITableRepository _tables;
        private readonly IPlayerRepository _players;
        private readonly IDateTime _clock;

        public GameService(
            ITableRepository tables,
            IPlayerRepository players,
            IDateTime clock
        )
        {
            _tables = tables;
            _players = players;
            _clock = clock;
        }

        // -------------------------------------------------------------
        // Crear mesa (si tu flujo real lo hace en TableService, ok; aquí igual está implementado)
        // -------------------------------------------------------------
        public async Task<Result<BlackjackTable>> CreateTableAsync(string name, Money minBet, Money maxBet)
        {
            try
            {
                var table = BlackjackTable.Create(name);
                table.SetBetLimits(minBet, maxBet);
                await _tables.AddAsync(table);
                return Result<BlackjackTable>.Success(table);
            }
            catch (System.Exception ex)
            {
                return Result<BlackjackTable>.Failure($"Failed to create table: {ex.Message}");
            }
        }

        // -------------------------------------------------------------
        public async Task<Result<BlackjackTable>> GetTableAsync(TableId tableId)
        {
            var table = await _tables.GetTableWithPlayersAsync(tableId);
            if (table is null)
                return Result<BlackjackTable>.Failure("Table not found");

            return Result<BlackjackTable>.Success(table);
        }

        // -------------------------------------------------------------
        public async Task<Result> JoinTableAsync(TableId tableId, PlayerId playerId, int seatPosition)
        {
            var table = await _tables.GetTableWithPlayersAsync(tableId);
            if (table is null) return Result.Failure("Table not found");

            var seat = table.Seats.FirstOrDefault(s => s.Position == seatPosition);
            if (seat is null) return Result.Failure("Seat not found");

            if (seat.IsOccupied) return Result.Failure("Seat already occupied");

            // Traer/crear jugador
            var player = await _players.GetByPlayerIdAsync(playerId);
            if (player is null)
            {
                // Nombre placeholder; en tu app real vendrá del perfil
                var name = $"Player {playerId.Value.ToString()[..6]}";
                player = Player.Create(playerId, name, new Money(1000m));
                player.AddHand(Hand.Empty());
                await _players.AddAsync(player);
            }

            seat.AssignPlayer(player);

            // Si la mesa estaba esperando y ya entra alguien, sigue esperando; el inicio de ronda lo hace StartRoundAsync.
            await _tables.UpdateAsync(table);
            return Result.Success();
        }

        // -------------------------------------------------------------
        public async Task<Result> LeaveTableAsync(TableId tableId, PlayerId playerId)
        {
            var table = await _tables.GetTableWithPlayersAsync(tableId);
            if (table is null) return Result.Failure("Table not found");

            var seat = table.Seats.FirstOrDefault(s =>
                s.IsOccupied && s.Player != null && s.Player.PlayerId == playerId);

            if (seat is null) return Result.Failure("Player not seated at this table");

            seat.RemovePlayer();

            // Si quedó vacía, la dejamos en espera de jugadores
            if (!table.Seats.Any(s => s.IsOccupied))
            {
                table.SetWaitingForPlayers();
            }

            await _tables.UpdateAsync(table);
            return Result.Success();
        }

        // -------------------------------------------------------------
        public async Task<Result> PlaceBetAsync(TableId tableId, PlayerId playerId, Bet bet)
        {
            var table = await _tables.GetTableWithPlayersAsync(tableId);
            if (table is null) return Result.Failure("Table not found");

            var seat = table.Seats.FirstOrDefault(s =>
                s.IsOccupied && s.Player != null && s.Player.PlayerId == playerId);

            if (seat is null || seat.Player is null)
                return Result.Failure("Player not seated at this table");

            // Validar límites
            if (bet.Amount.IsLessThan(table.MinBet) || bet.Amount.IsGreaterThan(table.MaxBet))
                return Result.Failure($"Bet must be between {table.MinBet} and {table.MaxBet}");

            try
            {
                seat.Player.PlaceBet(bet);
                await _players.UpdateAsync(seat.Player);
                return Result.Success();
            }
            catch (System.Exception ex)
            {
                return Result.Failure(ex.Message);
            }
        }

        // -------------------------------------------------------------
        public async Task<Result> StartRoundAsync(TableId tableId)
        {
            var table = await _tables.GetTableWithPlayersAsync(tableId);
            if (table is null) return Result.Failure("Table not found");

            var activeSeats = table.Seats.Where(s => s.IsOccupied && s.Player is not null).ToList();
            if (!activeSeats.Any()) return Result.Failure("No players at the table");

            // Cambia estado a InProgress y limpia manos
            table.StartNewRound();

            // Reparte 2 cartas a cada jugador
            for (int i = 0; i < 2; i++)
            {
                foreach (var s in activeSeats)
                {
                    var hand = s.Player!.Hands.FirstOrDefault();
                    if (hand is null)
                    {
                        hand = Hand.Empty();
                        s.Player.AddHand(hand);
                    }

                    hand.AddCard(table.Deck.DealCard());
                }
            }

            // Reparte 2 cartas al dealer
            table.DealerHand.AddCard(table.Deck.DealCard());
            table.DealerHand.AddCard(table.Deck.DealCard());

            await _tables.UpdateAsync(table);
            foreach (var s in activeSeats)
            {
                await _players.UpdateAsync(s.Player!);
            }

            return Result.Success();
        }

        // -------------------------------------------------------------
        public async Task<Result> PlayerActionAsync(TableId tableId, PlayerId playerId, PlayerAction action)
        {
            var table = await _tables.GetTableWithPlayersAsync(tableId);
            if (table is null) return Result.Failure("Table not found");

            var seat = table.Seats.FirstOrDefault(s =>
                s.IsOccupied && s.Player != null && s.Player.PlayerId == playerId);

            if (seat is null || seat.Player is null)
                return Result.Failure("Player not seated at this table");

            var hand = seat.Player.Hands.FirstOrDefault();
            if (hand is null) return Result.Failure("Player has no active hand");

            try
            {
                switch (action)
                {
                    case PlayerAction.Hit:
                        hand.AddCard(table.Deck.DealCard());
                        break;

                    case PlayerAction.Stand:
                        // Si quieres marcar "Stand", agrega un método en Hand (p.ej. hand.Stand())
                        // De momento, no tocamos Status directamente para evitar CS0272.
                        break;

                    case PlayerAction.Double:
                        // Requiere lógica de apuesta adicional (doblar, robar 1 carta, plantarse)
                        // Implementable cuando agregues métodos en dominio para ello.
                        break;

                    case PlayerAction.Split:
                        // Requiere lógica de manos múltiples; omitir por ahora.
                        break;

                    case PlayerAction.Surrender:
                        // Requiere método en Hand para rendirse y cálculo de devolución de mitad de apuesta.
                        break;
                }

                await _tables.UpdateAsync(table);
                await _players.UpdateAsync(seat.Player);
                return Result.Success();
            }
            catch (System.Exception ex)
            {
                return Result.Failure(ex.Message);
            }
        }

        // -------------------------------------------------------------
        public async Task<Result> EndRoundAsync(TableId tableId)
        {
            var table = await _tables.GetTableWithPlayersAsync(tableId);
            if (table is null) return Result.Failure("Table not found");

            // Dealer roba hasta 17 (17 suave se planta en esta versión simple)
            while (table.DealerHand.Value < 17)
            {
                table.DealerHand.AddCard(table.Deck.DealCard());
            }

            var seatsToSettle = table.Seats
                .Where(s => s.IsOccupied && s.Player is not null && s.Player.CurrentBet is not null)
                .ToList();

            foreach (var s in seatsToSettle)
            {
                var player = s.Player!;
                var hand = player.Hands.FirstOrDefault();
                if (hand is null) continue;

                var betAmount = player.CurrentBet!.Amount;
                Money winnings = Money.Zero;

                bool playerBlackjack = hand.Status == HandStatus.Blackjack && hand.Cards.Count == 2;
                bool dealerBust = table.DealerHand.Status == HandStatus.Bust || table.DealerHand.Value > 21;
                bool playerBust = hand.Status == HandStatus.Bust || hand.Value > 21;

                if (playerBust)
                {
                    winnings = Money.Zero;
                }
                else if (playerBlackjack)
                {
                    // 3:2 → recupera apuesta + 1.5x de ganancia = 2.5x total
                    winnings = betAmount.Multiply(2.5m);
                }
                else if (dealerBust)
                {
                    winnings = betAmount.Multiply(2m);
                }
                else
                {
                    if (hand.Value > table.DealerHand.Value)
                        winnings = betAmount.Multiply(2m);
                    else if (hand.Value == table.DealerHand.Value)
                        winnings = betAmount; // push
                    else
                        winnings = Money.Zero;
                }

                player.WinBet(winnings);

                // Si CurrentBet tiene setter privado, agrega en Player un método public void ClearBet()
                // y úsalo aquí. Asumimos que ya lo agregaste:
                player.ClearBet();

                await _players.UpdateAsync(player);
            }

            // Cerrar ronda SIN tocar setters privados
            table.EndRound();
            await _tables.UpdateAsync(table);

            return Result.Success();
        }
    }
}
