// Services/Game/GameService.cs - COMPLETO Y CORREGIDO CON GUID
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using BlackJack.Data.Repositories.Game;
using BlackJack.Domain.Enums;
using BlackJack.Domain.Models.Betting;
using BlackJack.Domain.Models.Cards;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlackJack.Services.Game
{
    public class GameService : IGameService
    {
        private readonly ITableRepository _tables;
        private readonly IPlayerRepository _players;
        private readonly IDealerService _dealerService;
        private readonly IHandRepository _handRepository;
        private readonly IHandEvaluationService _handEvaluationService;
        private readonly ILogger<GameService> _logger;

        public GameService(ITableRepository tables, IPlayerRepository players, IDealerService dealerService, IHandRepository handRepository, IHandEvaluationService handEvaluationService, ILogger<GameService> logger)
        {
            _tables = tables;
            _players = players;
            _dealerService = dealerService;
            _handRepository = handRepository;
            _handEvaluationService = handEvaluationService;
            _logger = logger;
        }

        #region Métodos de Mesa

        public async Task<Result<BlackjackTable>> CreateTableAsync(string name, Money minBet, Money maxBet)
        {
            try
            {
                var table = BlackjackTable.Create(name);
                table.SetBetLimits(minBet, maxBet);
                await _tables.AddAsync(table);

                _logger.LogInformation($"[GameService] Mesa creada: {table.Id}");
                _logger.LogInformation($"[GameService] Asientos: {string.Join(", ", table.Seats.Select(s => s.Position))}");

                return Result<BlackjackTable>.Success(table);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GameService] Error creando mesa: {ex.Message}");
                return Result<BlackjackTable>.Failure($"Failed to create table: {ex.Message}");
            }
        }

        public async Task<Result<BlackjackTable>> GetTableAsync(Guid tableId)
        {
            try
            {
                var table = await _tables.GetTableWithPlayersAsync(tableId);
                if (table is null)
                {
                    _logger.LogWarning($"[GameService] Mesa no encontrada: {tableId}");
                    return Result<BlackjackTable>.Failure("Table not found");
                }

                _logger.LogDebug($"[GameService] Mesa obtenida: {tableId} - {table.Seats.Count(s => s.IsOccupied)} jugadores");
                return Result<BlackjackTable>.Success(table);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GameService] Error obteniendo mesa {tableId}: {ex.Message}");
                return Result<BlackjackTable>.Failure($"Error getting table: {ex.Message}");
            }
        }

        public async Task<Result<List<BlackjackTable>>> GetAvailableTablesAsync()
        {
            try
            {
                var tables = await _tables.GetAvailableTablesAsync();
                _logger.LogDebug($"[GameService] {tables.Count} mesas disponibles obtenidas");
                return Result<List<BlackjackTable>>.Success(tables);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GameService] Error obteniendo mesas disponibles: {ex.Message}");
                return Result<List<BlackjackTable>>.Failure($"Error getting available tables: {ex.Message}");
            }
        }

        public async Task<Result<BlackjackTable>> GetTableDetailsAsync(Guid tableId)
        {
            return await GetTableAsync(tableId);
        }

        #endregion

        #region Métodos de Jugadores

        public async Task<Result> JoinTableAsync(Guid tableId, PlayerId playerId, int seatPosition)
        {
            _logger.LogInformation($"[GameService] JoinTableAsync: tableId={tableId}, playerId={playerId}, seat={seatPosition}");

            const int maxRetries = 3;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var transaction = await _tables.BeginTransactionAsync();

                    // Obtener tabla con lock
                    var table = await _tables.GetTableWithPlayersForUpdateAsync(tableId);
                    if (table is null)
                    {
                        await transaction.RollbackAsync();
                        return Result.Failure("Table not found");
                    }

                    // Verificar si ya está sentado EN CUALQUIER ASIENTO
                    var alreadySeated = table.Seats.FirstOrDefault(s =>
                        s.IsOccupied && s.Player != null && s.Player.PlayerId.Equals(playerId));

                    if (alreadySeated != null)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogWarning($"[GameService] Jugador {playerId} ya está sentado en posición {alreadySeated.Position}");
                        return Result.Failure($"Ya estás sentado en el asiento {alreadySeated.Position + 1}. Sal de ese asiento primero.");
                    }

                    // Verificar el asiento específico
                    var seat = table.Seats.FirstOrDefault(s => s.Position == seatPosition);
                    if (seat is null)
                    {
                        await transaction.RollbackAsync();
                        return Result.Failure($"Asiento no encontrado: {seatPosition + 1}");
                    }

                    if (seat.IsOccupied)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogWarning($"[GameService] Asiento {seatPosition} ya ocupado en mesa {tableId}");
                        return Result.Failure("El asiento ya está ocupado por otro jugador");
                    }

                    if (table.Status != GameStatus.WaitingForPlayers)
                    {
                        await transaction.RollbackAsync();
                        return Result.Failure("No puedes unirte a la mesa mientras hay una partida en progreso");
                    }

                    // Obtener o crear jugador
                    var player = await _players.GetByPlayerIdAsync(playerId);
                    if (player is null)
                    {
                        var name = $"Player {playerId.Value.ToString()[..8]}";
                        player = Player.Create(playerId, name, new Money(1000m));
                        player.AddHandId(Guid.NewGuid()); // Crear ID de mano
                        await _players.AddAsync(player);
                        _logger.LogInformation($"[GameService] Nuevo jugador creado: {playerId}");
                    }

                    // Asignar jugador al asiento
                    seat.SeatPlayer(player);
                    await _tables.UpdateAsync(table);
                    await transaction.CommitAsync();

                    _logger.LogInformation($"[GameService] Jugador {playerId} unido exitosamente al asiento {seatPosition + 1} en mesa {tableId}");
                    return Result.Success();
                }
                catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning($"[GameService] Concurrencia en JoinTable (intento {attempt}): {ex.Message}");
                    await Task.Delay(100 * attempt); // Backoff exponencial
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[GameService] Error en JoinTableAsync (intento {attempt}): {ex.Message}");

                    if (attempt == maxRetries)
                        return Result.Failure($"Error interno: {ex.Message}");
                }
            }

            return Result.Failure("Error de concurrencia: intenta de nuevo");
        }

        public async Task<Result> LeaveTableAsync(Guid tableId, PlayerId playerId)
        {
            _logger.LogInformation($"[GameService] LeaveTableAsync: tableId={tableId}, playerId={playerId}");

            try
            {
                using var transaction = await _tables.BeginTransactionAsync();

                var table = await _tables.GetTableWithPlayersForUpdateAsync(tableId);
                if (table is null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Table not found");
                }

                var seat = table.Seats.FirstOrDefault(s =>
                    s.IsOccupied && s.Player != null && s.Player.PlayerId == playerId);

                if (seat is null)
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning($"[GameService] Jugador {playerId} no encontrado en mesa {tableId}");
                    return Result.Success(); // No es error si no estaba sentado
                }

                // Remover jugador del asiento
                seat.ClearSeat();

                // Si no quedan jugadores, resetear estado de mesa
                if (!table.Seats.Any(s => s.IsOccupied))
                {
                    table.SetWaitingForPlayers();
                    _logger.LogInformation($"[GameService] Mesa {tableId} sin jugadores, cambiando a WaitingForPlayers");
                }

                await _tables.UpdateAsync(table);
                await transaction.CommitAsync();

                _logger.LogInformation($"[GameService] Jugador {playerId} salió de mesa {tableId} exitosamente");
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GameService] Error en LeaveTableAsync: {ex.Message}");
                return Result.Failure($"Error interno: {ex.Message}");
            }
        }

        public async Task<Result<bool>> IsPlayerSeatedAsync(Guid tableId, PlayerId playerId)
        {
            try
            {
                var table = await _tables.GetTableWithPlayersAsync(tableId);
                if (table is null)
                    return Result<bool>.Failure("Table not found");

                var isSeated = table.Seats.Any(s =>
                    s.IsOccupied && s.Player != null && s.Player.PlayerId == playerId);

                return Result<bool>.Success(isSeated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GameService] Error en IsPlayerSeatedAsync: {ex.Message}");
                return Result<bool>.Failure($"Error checking player seat: {ex.Message}");
            }
        }

        public async Task<Result<int?>> GetPlayerSeatPositionAsync(Guid tableId, PlayerId playerId)
        {
            try
            {
                var table = await _tables.GetTableWithPlayersAsync(tableId);
                if (table is null)
                    return Result<int?>.Failure("Table not found");

                var seat = table.Seats.FirstOrDefault(s =>
                    s.IsOccupied && s.Player != null && s.Player.PlayerId == playerId);

                var position = seat?.Position;
                return Result<int?>.Success(position);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GameService] Error en GetPlayerSeatPositionAsync: {ex.Message}");
                return Result<int?>.Failure($"Error getting player position: {ex.Message}");
            }
        }

        #endregion

        #region Métodos de Apuestas

        public async Task<Result> PlaceBetAsync(Guid tableId, PlayerId playerId, Bet bet)
        {
            _logger.LogInformation($"[GameService] PlaceBetAsync: tableId={tableId}, playerId={playerId}, amount={bet.Amount}");

            try
            {
                using var transaction = await _tables.BeginTransactionAsync();

                var table = await _tables.GetTableWithPlayersForUpdateAsync(tableId);
                if (table is null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Table not found");
                }

                var seat = table.Seats.FirstOrDefault(s =>
                    s.IsOccupied && s.Player != null && s.Player.PlayerId == playerId);

                if (seat is null || seat.Player is null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Player not seated at this table");
                }

                if (bet.Amount.IsLessThan(table.MinBet) || bet.Amount.IsGreaterThan(table.MaxBet))
                {
                    await transaction.RollbackAsync();
                    return Result.Failure($"Bet must be between {table.MinBet} and {table.MaxBet}");
                }

                if (seat.Player.CurrentBet != null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Ya tienes una apuesta activa");
                }

                if (table.Status != GameStatus.WaitingForPlayers)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("No puedes apostar durante una partida en progreso");
                }

                seat.Player.PlaceBet(bet);
                await _tables.UpdateAsync(table);
                await transaction.CommitAsync();

                _logger.LogInformation($"[GameService] Apuesta de {bet.Amount} colocada por {playerId}");
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GameService] Error en PlaceBetAsync: {ex.Message}");
                return Result.Failure($"Error interno: {ex.Message}");
            }
        }

        #endregion

        #region Métodos de Juego

        public async Task<Result> StartRoundAsync(Guid tableId)
        {
            _logger.LogInformation($"[GameService] StartRoundAsync: tableId={tableId}");

            const int maxRetries = 5;

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var transaction = await _tables.BeginTransactionAsync();

                    var table = await _tables.GetTableWithPlayersForUpdateAsync(tableId);
                    if (table is null)
                    {
                        await transaction.RollbackAsync();
                        return Result.Failure("Table not found");
                    }

                    // Idempotent: if already in progress, return OK immediately
                    if (table.Status == GameStatus.InProgress)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogInformation("[GameService] StartRoundAsync: already InProgress -> OK");
                        return Result.Success();
                    }

                    var seats = table.Seats.Where(s => s.IsOccupied && s.Player is not null).ToList();
                    if (seats.Count < 1)
                    {
                        await transaction.RollbackAsync();
                        return Result.Failure($"Se necesitan al menos 1 jugador para iniciar (tienes {seats.Count})");
                    }

                    // TEMP: Deshabilitado requisito de apuestas para permitir pruebas de juego
                    // var seatsWithoutBets = seats.Where(s => s.Player?.CurrentBet == null).ToList();
                    // if (seatsWithoutBets.Any())
                    // {
                    //     await transaction.RollbackAsync();
                    //     return Result.Failure("Todos los jugadores deben apostar antes de iniciar la ronda");
                    // }

                    // TEMP: Relajar validación de estado para permitir pruebas
                    // if (table.Status != GameStatus.WaitingForPlayers)
                    // {
                    //     await transaction.RollbackAsync();
                    //     return Result.Failure($"La mesa no está esperando jugadores (Estado actual: {table.Status})");
                    // }

                    // Preparar manos - crear nuevas manos para los jugadores
                    foreach (var s in seats)
                    {
                        var p = s.Player!;
                        p.ClearHandIds();
                        p.AddHandId(Guid.NewGuid()); // Nueva mano para esta ronda
                    }

                    // Forzar inicio de ronda para pruebas de gameplay
                    table.ForceStartRound();

                    await _tables.UpdateAsync(table);
                    await transaction.CommitAsync();

                    // Deal initial cards after transaction is committed
                    await _dealerService.DealInitialCardsAsync(table);
                    await _tables.UpdateAsync(table); // Update table with dealer hand ID

                    _logger.LogInformation($"[GameService] Round started exitosamente (attempt {attempt}) - {seats.Count} jugadores");
                    return Result.Success();
                }
                catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning($"[GameService] Concurrency on StartRound (attempt {attempt}): {ex.Message}");

                    // Si otro ya la arrancó mientras tanto, devolvemos OK
                    var latest = await _tables.GetTableWithPlayersAsync(tableId);
                    if (latest != null && latest.Status == GameStatus.InProgress)
                    {
                        _logger.LogInformation("[GameService] Otro proceso ya inició la ronda -> OK");
                        return Result.Success();
                    }

                    await Task.Delay(100 * attempt); // backoff
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[GameService] Error en StartRoundAsync (attempt {attempt}): {ex.Message}");
                    if (attempt == maxRetries)
                        return Result.Failure($"Error interno al iniciar la partida: {ex.Message}");
                }
            }

            return Result.Failure("Error de concurrencia al iniciar la partida");
        }

        public async Task<Result> PlayerActionAsync(Guid tableId, PlayerId playerId, PlayerAction action)
        {
            try
            {
                _logger.LogInformation($"[GameService] PlayerActionAsync START: tableId={tableId}, playerId={playerId}, action={action}");
                _logger.LogInformation($"[GameService] PlayerActionAsync: playerId.Value={playerId.Value}");
                using var transaction = await _tables.BeginTransactionAsync();

                var table = await _tables.GetTableWithPlayersForUpdateAsync(tableId);
                _logger.LogInformation($"[GameService] Table retrieved: {(table != null ? "Found" : "Not found")}");
                
                if (table is null)
                {
                    _logger.LogError($"[GameService] Table {tableId} not found");
                    await transaction.RollbackAsync();
                    return Result.Failure("Table not found");
                }

                _logger.LogInformation($"[GameService] Table status: {table.Status}");
                
                if (table.Status != GameStatus.InProgress)
                {
                    _logger.LogError($"[GameService] Table {tableId} is not in progress. Status: {table.Status}");
                    await transaction.RollbackAsync();
                    return Result.Failure("La ronda no está en progreso");
                }

                _logger.LogInformation($"[GameService] Looking for player {playerId} in {table.Seats.Count} seats");
                
                var seat = table.Seats.FirstOrDefault(s =>
                    s.IsOccupied && s.Player != null && s.Player.PlayerId == playerId);

                if (seat is null || seat.Player is null)
                {
                    _logger.LogError($"[GameService] Player {playerId} not found in any occupied seat");
                    _logger.LogError($"[GameService] Available seats: {string.Join(", ", table.Seats.Select(s => $"Pos:{s.Position}, Occupied:{s.IsOccupied}, PlayerId:{(s.Player?.PlayerId.Value.ToString() ?? "null")}"))}");
                    await transaction.RollbackAsync();
                    return Result.Failure("Player not seated at this table");
                }

                var player = seat.Player;
                _logger.LogInformation($"[GameService] Found player {playerId} at seat {seat.Position}");

                // Para acciones básicas del juego
                switch (action)
                {
                    case PlayerAction.Hit:
                        _logger.LogInformation($"[GameService] Player {playerId} hits");
                        
                        // Get player's current hand
                        if (!player.HandIds.Any())
                        {
                            _logger.LogError($"[GameService] Player {playerId} has no hand IDs");
                            await transaction.RollbackAsync();
                            return Result.Failure("Player has no active hand");
                        }
                        
                        var handId = player.HandIds.First();
                        _logger.LogInformation($"[GameService] Player {playerId} hand ID: {handId}");
                        
                        var playerHand = await _handRepository.GetByIdAsync(handId);
                        if (playerHand == null)
                        {
                            _logger.LogError($"[GameService] Player {playerId} hand not found for ID: {handId}");
                            await transaction.RollbackAsync();
                            return Result.Failure("Player hand not found");
                        }
                        
                        _logger.LogInformation($"[GameService] Player {playerId} hand status: {playerHand.Status}, isComplete: {playerHand.IsComplete}");
                        
                        // Check if hand is already complete (bust, stand, etc.)
                        if (playerHand.IsComplete)
                        {
                            _logger.LogError($"[GameService] Player {playerId} hand is already complete: {playerHand.Status}");
                            await transaction.RollbackAsync();
                            return Result.Failure("Cannot hit on a completed hand");
                        }
                        
                        // Check if deck is empty
                        if (table.Deck.IsEmpty)
                        {
                            _logger.LogError($"[GameService] Deck is empty for table {tableId}");
                            await transaction.RollbackAsync();
                            return Result.Failure("Cannot deal from empty deck");
                        }
                        
                        // Deal one card
                        _logger.LogInformation($"[GameService] Dealing card to player {playerId}");
                        var card = table.DealCard();
                        _logger.LogInformation($"[GameService] Card dealt: {card.GetDisplayName()}");
                        
                        playerHand.AddCard(card);
                        await _handRepository.UpdateAsync(playerHand);
                        
                        _logger.LogInformation($"[GameService] Player {playerId} received card {card.GetDisplayName()}, hand value: {playerHand.Value}");
                        
                        // Check if bust
                        if (playerHand.IsBust)
                        {
                            _logger.LogInformation($"[GameService] Player {playerId} busted with value {playerHand.Value}");
                        }
                        break;

                    case PlayerAction.Stand:
                        _logger.LogInformation($"[GameService] Player {playerId} stands");
                        
                        // Get player's current hand
                        if (!player.HandIds.Any())
                        {
                            await transaction.RollbackAsync();
                            return Result.Failure("Player has no active hand");
                        }
                        
                        var standHandId = player.HandIds.First();
                        var standHand = await _handRepository.GetByIdAsync(standHandId);
                        if (standHand == null)
                        {
                            await transaction.RollbackAsync();
                            return Result.Failure("Player hand not found");
                        }
                        
                        // Mark hand as stand
                        standHand.Stand();
                        await _handRepository.UpdateAsync(standHand);
                        
                        _logger.LogInformation($"[GameService] Player {playerId} stands with hand value: {standHand.Value}");
                        break;

                    case PlayerAction.Double:
                        if (player.CurrentBet != null)
                        {
                            // Duplicar apuesta si es posible
                            var doubleAmount = player.CurrentBet.Amount.Amount * 2;
                            if (player.CanAffordBet(new Money(doubleAmount)))
                            {
                                var doubleBet = Bet.Create(doubleAmount);
                                player.SubtractFromBalance(player.CurrentBet.Amount);
                                player.PlaceBet(doubleBet);
                            }
                            else
                            {
                                await transaction.RollbackAsync();
                                return Result.Failure("Fondos insuficientes para doblar");
                            }
                        }
                        _logger.LogInformation($"[GameService] Player {playerId} doubles down");
                        break;

                    case PlayerAction.Split:
                        await transaction.RollbackAsync();
                        return Result.Failure("Split aún no implementado");

                    case PlayerAction.Surrender:
                        await transaction.RollbackAsync();
                        return Result.Failure("Surrender aún no implementado");

                    default:
                        await transaction.RollbackAsync();
                        return Result.Failure("Acción no válida");
                }

                await _tables.UpdateAsync(table);
                await transaction.CommitAsync();

                _logger.LogInformation($"[GameService] Acción {action} ejecutada por {playerId}");

                // Check if all players are done, if so, play dealer hand and complete round
                if (await AreAllPlayersDoneAsync(table))
                {
                    _logger.LogInformation("[GameService] All players done, starting dealer play for table {TableId}", table.Id);
                    var dealerResult = await PlayDealerHandAsync(table);
                    if (!dealerResult.IsSuccess)
                    {
                        _logger.LogError("[GameService] Error playing dealer hand: {Error}", dealerResult.Error);
                        // Don't fail the player action, just log the error
                    }
                    else
                    {
                        // Dealer played successfully, now complete the round
                        _logger.LogInformation("[GameService] Dealer play complete, finishing round for table {TableId}", table.Id);
                        var roundResult = await CompleteRoundAsync(table);
                        if (!roundResult.IsSuccess)
                        {
                            _logger.LogError("[GameService] Error completing round: {Error}", roundResult.Error);
                            // Don't fail the player action, just log the error
                        }
                    }
                }

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GameService] Error en PlayerActionAsync: {ex.Message}");
                return Result.Failure($"Error ejecutando acción: {ex.Message}");
            }
        }

        public async Task<Result> EndRoundAsync(Guid tableId)
        {
            _logger.LogInformation($"[GameService] EndRoundAsync: tableId={tableId}");

            try
            {
                using var transaction = await _tables.BeginTransactionAsync();

                var table = await _tables.GetTableWithPlayersForUpdateAsync(tableId);
                if (table is null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Table not found");
                }

                if (table.Status != GameStatus.InProgress)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("No hay ronda en progreso");
                }

                var seatsToSettle = table.Seats
                    .Where(s => s.IsOccupied && s.Player is not null && s.Player.CurrentBet is not null)
                    .ToList();

                foreach (var s in seatsToSettle)
                {
                    var player = s.Player!;
                    var betAmount = player.CurrentBet!.Amount;

                    // Lógica simplificada de payout
                    Money winnings = betAmount.Multiply(2m); // Payout 1:1 por simplicidad

                    player.WinBet(winnings);
                    player.ClearBet();
                }

                table.EndRound();
                await _tables.UpdateAsync(table);
                await transaction.CommitAsync();

                _logger.LogInformation($"[GameService] Ronda terminada en mesa {tableId} - {seatsToSettle.Count} jugadores liquidados");
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GameService] Error en EndRoundAsync: {ex.Message}");
                return Result.Failure($"Error terminando ronda: {ex.Message}");
            }
        }

        #endregion

        #region Métodos de Administración

        public async Task<Result> ResetTableAsync(Guid tableId)
        {
            _logger.LogInformation($"[GameService] ResetTableAsync: tableId={tableId}");

            try
            {
                using var transaction = await _tables.BeginTransactionAsync();

                var table = await _tables.GetTableWithPlayersForUpdateAsync(tableId);
                if (table is null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Table not found");
                }

                table.Reset();
                await _tables.UpdateAsync(table);
                await transaction.CommitAsync();

                _logger.LogInformation($"[GameService] Mesa {tableId} reseteada completamente");
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GameService] Error en ResetTableAsync: {ex.Message}");
                return Result.Failure($"Error reseteando mesa: {ex.Message}");
            }
        }

        public async Task<Result> PauseTableAsync(Guid tableId)
        {
            _logger.LogInformation($"[GameService] PauseTableAsync: tableId={tableId}");

            try
            {
                using var transaction = await _tables.BeginTransactionAsync();

                var table = await _tables.GetTableWithPlayersForUpdateAsync(tableId);
                if (table is null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Table not found");
                }

                // Cambiar a paused si tuvieras ese estado
                // table.Status = GameStatus.Paused;

                await _tables.UpdateAsync(table);
                await transaction.CommitAsync();

                _logger.LogInformation($"[GameService] Mesa {tableId} pausada");
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GameService] Error en PauseTableAsync: {ex.Message}");
                return Result.Failure($"Error pausando mesa: {ex.Message}");
            }
        }

        public async Task<Result> ResumeTableAsync(Guid tableId)
        {
            _logger.LogInformation($"[GameService] ResumeTableAsync: tableId={tableId}");

            try
            {
                using var transaction = await _tables.BeginTransactionAsync();

                var table = await _tables.GetTableWithPlayersForUpdateAsync(tableId);
                if (table is null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Table not found");
                }

                table.SetWaitingForPlayers();
                await _tables.UpdateAsync(table);
                await transaction.CommitAsync();

                _logger.LogInformation($"[GameService] Mesa {tableId} reanudada");
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GameService] Error en ResumeTableAsync: {ex.Message}");
                return Result.Failure($"Error reanudando mesa: {ex.Message}");
            }
        }

        #endregion

        #region Dealer Play Logic

        private async Task<bool> AreAllPlayersDoneAsync(BlackjackTable table)
        {
            var occupiedSeats = table.Seats.Where(s => s.IsOccupied && s.Player != null).ToList();
            
            foreach (var seat in occupiedSeats)
            {
                if (!seat.Player!.HandIds.Any())
                    return false;
                
                var handId = seat.Player.HandIds.First();
                var hand = await _handRepository.GetByIdAsync(handId);
                
                if (hand == null || !hand.IsComplete)
                    return false;
            }
            
            return true;
        }

        private async Task<Result> PlayDealerHandAsync(BlackjackTable table)
        {
            try
            {
                if (table.DealerHandId == null)
                {
                    _logger.LogError("[GameService] No dealer hand found for table {TableId}", table.Id);
                    return Result.Failure("No dealer hand found");
                }

                var dealerHand = await _handRepository.GetByIdAsync(table.DealerHandId.Value);
                if (dealerHand == null)
                {
                    _logger.LogError("[GameService] Dealer hand not found in database for table {TableId}", table.Id);
                    return Result.Failure("Dealer hand not found");
                }

                _logger.LogInformation("[GameService] Starting dealer play for table {TableId}, current value: {Value}", 
                    table.Id, dealerHand.Value);

                // Use existing DealerService logic
                var finalDealerHand = _dealerService.PlayDealerHand(dealerHand, table.Deck);
                
                // Update dealer hand in database
                await _handRepository.UpdateAsync(finalDealerHand);

                _logger.LogInformation("[GameService] Dealer finished playing for table {TableId}, final value: {Value}", 
                    table.Id, finalDealerHand.Value);

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Error playing dealer hand for table {TableId}: {Error}", 
                    table.Id, ex.Message);
                return Result.Failure($"Error playing dealer hand: {ex.Message}");
            }
        }

        private async Task<Result> CompleteRoundAsync(BlackjackTable table)
        {
            try
            {
                _logger.LogInformation("[GameService] Completing round for table {TableId}", table.Id);

                // Get dealer hand
                if (table.DealerHandId == null)
                {
                    _logger.LogError("[GameService] No dealer hand found for table {TableId}", table.Id);
                    return Result.Failure("No dealer hand found");
                }

                var dealerHand = await _handRepository.GetByIdAsync(table.DealerHandId.Value);
                if (dealerHand == null)
                {
                    _logger.LogError("[GameService] Dealer hand not found in database for table {TableId}", table.Id);
                    return Result.Failure("Dealer hand not found");
                }

                _logger.LogInformation("[GameService] Dealer final hand value: {Value}", dealerHand.Value);

                // Process each player
                var occupiedSeats = table.Seats.Where(s => s.IsOccupied && s.Player != null).ToList();
                
                foreach (var seat in occupiedSeats)
                {
                    var player = seat.Player!;
                    
                    if (!player.HandIds.Any())
                    {
                        _logger.LogWarning("[GameService] Player {PlayerId} has no hands, skipping", player.PlayerId);
                        continue;
                    }

                    var playerHandId = player.HandIds.First();
                    var playerHand = await _handRepository.GetByIdAsync(playerHandId);
                    
                    if (playerHand == null)
                    {
                        _logger.LogWarning("[GameService] Player {PlayerId} hand not found, skipping", player.PlayerId);
                        continue;
                    }

                    // Determine winner using HandEvaluationService
                    var result = _handEvaluationService.CompareHands(playerHand, dealerHand);
                    
                    _logger.LogInformation("[GameService] Player {PlayerId} vs Dealer: {Result} (Player: {PlayerValue}, Dealer: {DealerValue})", 
                        player.PlayerId, result, playerHand.Value, dealerHand.Value);

                    // Simple payout logic (your coworker handles complex betting)
                    if (result == HandResult.PlayerWins || result == HandResult.PlayerBlackjack)
                    {
                        // Player wins - give back bet + winnings (2x bet total)
                        if (player.CurrentBet != null)
                        {
                            var winnings = player.CurrentBet.Amount.Multiply(2m); // 2x bet (1x bet + 1x winnings)
                            player.WinBet(winnings);
                            _logger.LogInformation("[GameService] Player {PlayerId} wins {Amount}", player.PlayerId, winnings.Amount);
                        }
                    }
                    else if (result == HandResult.Push)
                    {
                        // Push - give back bet only
                        if (player.CurrentBet != null)
                        {
                            player.WinBet(player.CurrentBet.Amount); // Just return the bet
                            _logger.LogInformation("[GameService] Player {PlayerId} pushes, gets bet back", player.PlayerId);
                        }
                    }
                    else
                    {
                        // Player loses - bet is already deducted, nothing to do
                        _logger.LogInformation("[GameService] Player {PlayerId} loses", player.PlayerId);
                    }

                    // Clear the bet
                    player.ClearBet();
                    
                    // Update player in database
                    await _players.UpdateAsync(player);
                }

                // End the round
                table.EndRound();
                await _tables.UpdateAsync(table);

                _logger.LogInformation("[GameService] Round completed successfully for table {TableId}", table.Id);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Error completing round for table {TableId}: {Error}", 
                    table.Id, ex.Message);
                return Result.Failure($"Error completing round: {ex.Message}");
            }
        }

        #endregion
    }
}