// Services/Game/GameService.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using BlackJack.Data.Repositories.Game;
using BlackJack.Domain.Enums;
using BlackJack.Domain.Models.Betting;
using BlackJack.Domain.Models.Cards;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;
using Microsoft.EntityFrameworkCore;

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

        public async Task<Result<BlackjackTable>> CreateTableAsync(string name, Money minBet, Money maxBet)
        {
            try
            {
                var table = BlackjackTable.Create(name);
                table.SetBetLimits(minBet, maxBet);
                await _tables.AddAsync(table);

                Console.WriteLine($"[GameService] Mesa creada: {table.Id}");
                Console.WriteLine($"[GameService] Asientos creados: {string.Join(", ", table.Seats.Select(s => s.Position))}");

                return Result<BlackjackTable>.Success(table);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameService] Error creando mesa: {ex.Message}");
                return Result<BlackjackTable>.Failure($"Failed to create table: {ex.Message}");
            }
        }

        public async Task<Result<BlackjackTable>> GetTableAsync(TableId tableId)
        {
            var table = await _tables.GetTableWithPlayersAsync(tableId);
            if (table is null)
                return Result<BlackjackTable>.Failure("Table not found");

            return Result<BlackjackTable>.Success(table);
        }

        public async Task<Result> JoinTableAsync(TableId tableId, PlayerId playerId, int seatPosition)
        {
            Console.WriteLine($"[GameService] JoinTableAsync called: tableId={tableId}, playerId={playerId}, seatPosition={seatPosition}");

            var table = await _tables.GetTableWithPlayersAsync(tableId);
            if (table is null)
            {
                Console.WriteLine($"[GameService] Table not found: {tableId}");
                return Result.Failure("Table not found");
            }

            // Jugador ya sentado en esta mesa
            var existingSeat = table.Seats.FirstOrDefault(s =>
                s.IsOccupied && s.Player != null && s.Player.PlayerId.Equals(playerId));

            if (existingSeat != null)
            {
                Console.WriteLine($"[GameService] Jugador {playerId} ya está en posición {existingSeat.Position}");
                return Result.Failure($"Ya estás sentado en el asiento {existingSeat.Position + 1}. Debes salir de ese asiento primero.");
            }

            var seat = table.Seats.FirstOrDefault(s => s.Position == seatPosition);
            if (seat is null)
            {
                var availablePositions = table.Seats.Select(s => s.Position).ToList();
                Console.WriteLine($"[GameService] Seat not found at position {seatPosition}");
                return Result.Failure($"Asiento no encontrado. Posición solicitada: {seatPosition}, Posiciones disponibles: [{string.Join(", ", availablePositions)}]");
            }

            if (seat.IsOccupied)
            {
                Console.WriteLine($"[GameService] Seat {seatPosition} ocupado por {seat.Player?.PlayerId}");
                return Result.Failure("El asiento ya está ocupado por otro jugador");
            }

            if (table.Status != GameStatus.WaitingForPlayers)
            {
                Console.WriteLine($"[GameService] No se puede unir en estado: {table.Status}");
                return Result.Failure("No puedes unirte a la mesa mientras hay una partida en progreso");
            }

            // Obtener o crear jugador
            var player = await _players.GetByPlayerIdAsync(playerId);
            if (player is null)
            {
                var name = $"Player {playerId.Value.ToString()[..8]}";
                player = Player.Create(playerId, name, new Money(1000m));
                player.AddHand(Hand.Empty());
                await _players.AddAsync(player);
                Console.WriteLine($"[GameService] Created new player: {name} (ID: {playerId})");
            }

            try
            {
                seat.AssignPlayer(player);
                Console.WriteLine($"[GameService] Player {player.Name} -> seat {seatPosition}");

                await _tables.UpdateAsync(table);
                Console.WriteLine($"[GameService] Table updated. Players: {table.Seats.Count(s => s.IsOccupied)}");

                return Result.Success();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameService] Error assigning player to seat: {ex.Message}");
                return Result.Failure($"Error interno al asignar jugador al asiento: {ex.Message}");
            }
        }

        public async Task<Result> LeaveTableAsync(TableId tableId, PlayerId playerId)
        {
            Console.WriteLine($"[GameService] LeaveTableAsync: tableId={tableId}, playerId={playerId}");

            var table = await _tables.GetTableWithPlayersAsync(tableId);
            if (table is null) return Result.Failure("Table not found");

            var seat = table.Seats.FirstOrDefault(s =>
                s.IsOccupied && s.Player != null && s.Player.PlayerId == playerId);

            if (seat is null)
            {
                Console.WriteLine($"[GameService] Player {playerId} not found at table {tableId}");
                return Result.Failure("Player not seated at this table");
            }

            Console.WriteLine($"[GameService] Removing player {playerId} from seat {seat.Position}");
            seat.RemovePlayer();

            if (!table.Seats.Any(s => s.IsOccupied))
            {
                table.SetWaitingForPlayers();
                Console.WriteLine($"[GameService] Table {tableId} -> WaitingForPlayers");
            }

            await _tables.UpdateAsync(table);
            Console.WriteLine($"[GameService] LeaveTableAsync OK");
            return Result.Success();
        }

        public async Task<Result> PlaceBetAsync(TableId tableId, PlayerId playerId, Bet bet)
        {
            Console.WriteLine($"[GameService] PlaceBetAsync: tableId={tableId}, playerId={playerId}, amount={bet.Amount}");

            var table = await _tables.GetTableWithPlayersAsync(tableId);
            if (table is null) return Result.Failure("Table not found");

            var seat = table.Seats.FirstOrDefault(s =>
                s.IsOccupied && s.Player != null && s.Player.PlayerId == playerId);

            if (seat is null || seat.Player is null)
                return Result.Failure("Player not seated at this table");

            // Validar límites
            if (bet.Amount.IsLessThan(table.MinBet) || bet.Amount.IsGreaterThan(table.MaxBet))
            {
                Console.WriteLine($"[GameService] Bet {bet.Amount} fuera de límites: {table.MinBet} - {table.MaxBet}");
                return Result.Failure($"Bet must be between {table.MinBet} and {table.MaxBet}");
            }

            try
            {
                seat.Player.PlaceBet(bet);
                await _players.UpdateAsync(seat.Player);
                Console.WriteLine($"[GameService] Bet OK: {seat.Player.Name} bet {bet.Amount}");
                return Result.Success();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameService] Error placing bet: {ex.Message}");
                return Result.Failure(ex.Message);
            }
        }

        public async Task<Result> ResetTableAsync(TableId tableId)
        {
            Console.WriteLine($"[GameService] ResetTableAsync: tableId={tableId}");

            var table = await _tables.GetTableWithPlayersAsync(tableId);
            if (table is null) return Result.Failure("Table not found");

            try
            {
                table.SetWaitingForPlayers();

                var activeSeats = table.Seats.Where(s => s.IsOccupied && s.Player != null).ToList();
                foreach (var seat in activeSeats)
                {
                    seat.Player!.ClearHands();
                    seat.Player.ClearBet();
                }

                await _tables.UpdateAsync(table);

                foreach (var seat in activeSeats)
                {
                    await _players.UpdateAsync(seat.Player!);
                }

                Console.WriteLine($"[GameService] Table {tableId} reset OK");
                return Result.Success();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameService] Error resetting table: {ex.Message}");
                return Result.Failure($"Error reseteando mesa: {ex.Message}");
            }
        }

        public async Task<Result> StartRoundAsync(TableId tableId)
        {
            const int maxRetries = 3;
            var retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    Console.WriteLine($"[GameService] StartRoundAsync attempt {retryCount + 1}: {tableId}");

                    var table = await _tables.GetTableWithPlayersAsync(tableId);
                    if (table is null)
                        return Result.Failure("Table not found");

                    var activeSeats = table.Seats.Where(s => s.IsOccupied && s.Player is not null).ToList();
                    if (activeSeats.Count < 1) // cambiar a 2 en prod
                    {
                        Console.WriteLine($"[GameService] Not enough players: {activeSeats.Count}");
                        return Result.Failure($"Se necesitan al menos 1 jugador para iniciar (tienes {activeSeats.Count})");
                    }

                    if (table.Status != GameStatus.WaitingForPlayers)
                    {
                        Console.WriteLine($"[GameService] Estado inválido para iniciar: {table.Status}");
                        return Result.Failure($"La mesa no está esperando jugadores (Estado actual: {table.Status})");
                    }

                    Console.WriteLine($"[GameService] Start round {table.RoundNumber + 1} players={activeSeats.Count}");

                    foreach (var s in activeSeats)
                    {
                        s.Player!.ClearHands();
                        s.Player.AddHand(Hand.Empty());
                    }

                    table.StartNewRound();

                    // 2 a cada jugador
                    for (int i = 0; i < 2; i++)
                    {
                        foreach (var s in activeSeats)
                        {
                            var hand = s.Player!.Hands.FirstOrDefault();
                            if (hand != null)
                            {
                                hand.AddCard(table.Deck.DealCard());
                                Console.WriteLine($"[GameService] Dealt card {i + 1} -> {s.Player.Name}");
                            }
                        }
                    }

                    // 2 al dealer
                    table.DealerHand.AddCard(table.Deck.DealCard());
                    table.DealerHand.AddCard(table.Deck.DealCard());
                    Console.WriteLine($"[GameService] Dealer recibe 2 cartas");

                    await _tables.UpdateAsync(table);
                    foreach (var s in activeSeats)
                    {
                        await _players.UpdateAsync(s.Player!);
                    }

                    Console.WriteLine($"[GameService] Round started OK (attempt {retryCount + 1})");
                    return Result.Success();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    retryCount++;
                    Console.WriteLine($"[GameService] Concurrency attempt {retryCount}: {ex.Message}");

                    if (retryCount >= maxRetries)
                    {
                        Console.WriteLine($"[GameService] Max retries reached.");
                        return Result.Failure("Error de concurrencia: Otro jugador modificó la mesa al mismo tiempo. Inténtalo de nuevo.");
                    }

                    await Task.Delay(100 * retryCount);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameService] Error inesperado StartRoundAsync: {ex.Message}");
                    Console.WriteLine($"[GameService] Stack: {ex.StackTrace}");
                    return Result.Failure($"Error interno al iniciar la partida: {ex.Message}");
                }
            }

            return Result.Failure("Error desconocido al iniciar la partida");
        }

        public async Task<Result> PlayerActionAsync(TableId tableId, PlayerId playerId, PlayerAction action)
        {
            Console.WriteLine($"[GameService] PlayerActionAsync: tableId={tableId}, playerId={playerId}, action={action}");

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
                        Console.WriteLine($"[GameService] {seat.Player.Name} HIT (hand: {hand.Value})");
                        break;

                    case PlayerAction.Stand:
                        Console.WriteLine($"[GameService] {seat.Player.Name} STAND (hand: {hand.Value})");
                        break;

                    case PlayerAction.Double:
                        Console.WriteLine($"[GameService] {seat.Player.Name} DOUBLE");
                        break;

                    case PlayerAction.Split:
                        Console.WriteLine($"[GameService] {seat.Player.Name} SPLIT");
                        break;

                    case PlayerAction.Surrender:
                        Console.WriteLine($"[GameService] {seat.Player.Name} SURRENDER");
                        break;
                }

                await _tables.UpdateAsync(table);
                await _players.UpdateAsync(seat.Player);
                return Result.Success();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameService] Error player action: {ex.Message}");
                return Result.Failure(ex.Message);
            }
        }

        public async Task<Result> EndRoundAsync(TableId tableId)
        {
            Console.WriteLine($"[GameService] EndRoundAsync: tableId={tableId}");

            var table = await _tables.GetTableWithPlayersAsync(tableId);
            if (table is null) return Result.Failure("Table not found");

            // Dealer roba hasta 17
            Console.WriteLine($"[GameService] Dealer initial: {table.DealerHand.Value}");
            while (table.DealerHand.Value < 17)
            {
                table.DealerHand.AddCard(table.Deck.DealCard());
                Console.WriteLine($"[GameService] Dealer draws -> {table.DealerHand.Value}");
            }

            var seatsToSettle = table.Seats
                .Where(s => s.IsOccupied && s.Player is not null && s.Player.CurrentBet is not null)
                .ToList();

            Console.WriteLine($"[GameService] Settling {seatsToSettle.Count} players");

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
                    Console.WriteLine($"[GameService] {player.Name}: BUST (pierde apuesta)");
                }
                else if (playerBlackjack)
                {
                    winnings = betAmount.Multiply(2.5m);
                    Console.WriteLine($"[GameService] {player.Name}: BLACKJACK (gana {winnings})");
                }
                else if (dealerBust)
                {
                    winnings = betAmount.Multiply(2m);
                    Console.WriteLine($"[GameService] {player.Name}: Dealer BUST (gana {winnings})");
                }
                else
                {
                    if (hand.Value > table.DealerHand.Value)
                    {
                        winnings = betAmount.Multiply(2m);
                        Console.WriteLine($"[GameService] {player.Name}: > dealer ({hand.Value} vs {table.DealerHand.Value}) gana {winnings}");
                    }
                    else if (hand.Value == table.DealerHand.Value)
                    {
                        winnings = betAmount;
                        Console.WriteLine($"[GameService] {player.Name}: PUSH ({hand.Value}) recupera apuesta");
                    }
                    else
                    {
                        winnings = Money.Zero;
                        Console.WriteLine($"[GameService] {player.Name}: < dealer ({hand.Value} vs {table.DealerHand.Value}) pierde");
                    }
                }

                player.WinBet(winnings);
                player.ClearBet();
                await _players.UpdateAsync(player);
            }

            table.EndRound();
            await _tables.UpdateAsync(table);

            Console.WriteLine($"[GameService] EndRound OK");
            return Result.Success();
        }
    }
}
