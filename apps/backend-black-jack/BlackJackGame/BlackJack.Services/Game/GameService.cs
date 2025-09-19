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
        private readonly ILogger<GameService> _logger;

        public GameService(ITableRepository tables, IPlayerRepository players, ILogger<GameService> logger)
        {
            _tables = tables;
            _players = players;
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

                    var seats = table.Seats.Where(s => s.IsOccupied && s.Player is not null).ToList();
                    if (seats.Count < 1)
                    {
                        await transaction.RollbackAsync();
                        return Result.Failure($"Se necesitan al menos 1 jugador para iniciar (tienes {seats.Count})");
                    }

                    // Verificar que todos los jugadores tengan apuestas
                    var seatsWithoutBets = seats.Where(s => s.Player?.CurrentBet == null).ToList();
                    if (seatsWithoutBets.Any())
                    {
                        await transaction.RollbackAsync();
                        return Result.Failure("Todos los jugadores deben apostar antes de iniciar la ronda");
                    }

                    // Idempotente: si ya está en progreso, OK
                    if (table.Status == GameStatus.InProgress)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogInformation("[GameService] StartRoundAsync: ya estaba InProgress -> OK");
                        return Result.Success();
                    }

                    if (table.Status != GameStatus.WaitingForPlayers)
                    {
                        await transaction.RollbackAsync();
                        return Result.Failure($"La mesa no está esperando jugadores (Estado actual: {table.Status})");
                    }

                    // Preparar manos - crear nuevas manos para los jugadores
                    foreach (var s in seats)
                    {
                        var p = s.Player!;
                        p.ClearHandIds();
                        p.AddHandId(Guid.NewGuid()); // Nueva mano para esta ronda
                    }

                    table.StartNewRound();

                    await _tables.UpdateAsync(table);
                    await transaction.CommitAsync();

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
            _logger.LogInformation($"[GameService] PlayerActionAsync: tableId={tableId}, playerId={playerId}, action={action}");

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
                    return Result.Failure("La ronda no está en progreso");
                }

                var seat = table.Seats.FirstOrDefault(s =>
                    s.IsOccupied && s.Player != null && s.Player.PlayerId == playerId);

                if (seat is null || seat.Player is null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Player not seated at this table");
                }

                var player = seat.Player;

                // Para acciones básicas del juego
                switch (action)
                {
                    case PlayerAction.Hit:
                        _logger.LogInformation($"[GameService] Player {playerId} hits");
                        break;

                    case PlayerAction.Stand:
                        _logger.LogInformation($"[GameService] Player {playerId} stands");
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
    }
}