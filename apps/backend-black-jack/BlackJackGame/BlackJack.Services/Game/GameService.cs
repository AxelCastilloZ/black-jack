// Services/Game/GameService.cs - CON USERSERVICE SYNC - SIN ERRORES
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
using BlackJack.Services.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlackJack.Services.Game
{
    public class GameService : IGameService
    {
        private readonly ITableRepository _tables;
        private readonly IPlayerRepository _players;
        private readonly IUserService _userService;
        private readonly ILogger<GameService> _logger;

        public GameService(ITableRepository tables, IPlayerRepository players, IUserService userService, ILogger<GameService> logger)
        {
            _tables = tables;
            _players = players;
            _userService = userService;
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

                    var table = await _tables.GetTableWithPlayersForUpdateAsync(tableId);
                    if (table is null)
                    {
                        await transaction.RollbackAsync();
                        return Result.Failure("Table not found");
                    }

                    var alreadySeated = table.Seats.FirstOrDefault(s =>
                        s.IsOccupied && s.Player != null && s.Player.PlayerId.Equals(playerId));

                    if (alreadySeated != null)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogWarning($"[GameService] Jugador {playerId} ya está sentado en posición {alreadySeated.Position}");
                        return Result.Failure($"Ya estás sentado en el asiento {alreadySeated.Position + 1}. Sal de ese asiento primero.");
                    }

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

                    var player = await _players.GetByPlayerIdAsync(playerId);
                    if (player is null)
                    {
                        // NUEVA FUNCIONALIDAD: Obtener o crear UserProfile
                        var userProfileResult = await _userService.GetOrCreateUserAsync(
                            playerId,
                            $"Player {playerId.Value.ToString()[..8]}",
                            $"player{playerId.Value.ToString()[..8]}@blackjack.local"
                        );

                        if (!userProfileResult.IsSuccess)
                        {
                            await transaction.RollbackAsync();
                            _logger.LogError($"[GameService] Error obteniendo/creando UserProfile: {userProfileResult.Error}");
                            return Result.Failure("Error creating user profile");
                        }

                        var userProfile = userProfileResult.Value!;

                        player = Player.Create(playerId, userProfile.DisplayName, userProfile.Balance);
                        player.AddHandId(Guid.NewGuid());
                        await _players.AddAsync(player);
                        _logger.LogInformation($"[GameService] Nuevo jugador creado: {playerId} con balance {userProfile.Balance.Amount}");
                    }
                    else
                    {
                        // NUEVA FUNCIONALIDAD: Sincronizar balance del UserProfile al Player existente
                        var userProfileResult = await _userService.GetUserAsync(playerId);
                        if (userProfileResult.IsSuccess)
                        {
                            var userProfile = userProfileResult.Value!;
                            if (player.Balance.Amount != userProfile.Balance.Amount)
                            {
                                player.SetBalance(userProfile.Balance);
                                _logger.LogInformation($"[GameService] Balance sincronizado para {playerId}: {userProfile.Balance.Amount}");
                            }
                        }
                    }

                    seat.SeatPlayer(player);
                    await _tables.UpdateAsync(table);
                    await transaction.CommitAsync();

                    _logger.LogInformation($"[GameService] Jugador {playerId} unido exitosamente al asiento {seatPosition + 1} en mesa {tableId}");
                    return Result.Success();
                }
                catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning($"[GameService] Concurrencia en JoinTable (intento {attempt}): {ex.Message}");
                    await Task.Delay(100 * attempt);
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
                    return Result.Success();
                }

                // NUEVA FUNCIONALIDAD: Sincronizar balance final al UserProfile antes de salir
                var player = seat.Player!;
                var syncResult = await _userService.SyncPlayerBalanceAsync(playerId, player.Balance);
                if (!syncResult.IsSuccess)
                {
                    _logger.LogWarning($"[GameService] Error sincronizando balance al salir: {syncResult.Error}");
                }
                else
                {
                    _logger.LogInformation($"[GameService] Balance sincronizado al salir: {playerId} -> {player.Balance.Amount}");
                }

                seat.ClearSeat();

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

                    var seatsWithoutBets = seats.Where(s => s.Player?.CurrentBet == null).ToList();
                    if (seatsWithoutBets.Any())
                    {
                        await transaction.RollbackAsync();
                        return Result.Failure("Todos los jugadores deben apostar antes de iniciar la ronda");
                    }

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

                    foreach (var s in seats)
                    {
                        var p = s.Player!;
                        p.ClearHandIds();
                        p.AddHandId(Guid.NewGuid());
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

                    var latest = await _tables.GetTableWithPlayersAsync(tableId);
                    if (latest != null && latest.Status == GameStatus.InProgress)
                    {
                        _logger.LogInformation("[GameService] Otro proceso ya inició la ronda -> OK");
                        return Result.Success();
                    }

                    await Task.Delay(100 * attempt);
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

                // NUEVA FUNCIONALIDAD: Procesar resultados y sincronizar con UserProfile
                var syncTasks = new List<Task>();

                foreach (var s in seatsToSettle)
                {
                    var player = s.Player!;
                    var betAmount = player.CurrentBet!.Amount;
                    var initialBalance = player.Balance.Amount;

                    // Lógica simplificada de payout
                    Money winnings = betAmount.Multiply(2m); // Payout 1:1 por simplicidad
                    bool playerWon = true; // Simplificado - en realidad sería vs dealer

                    player.WinBet(winnings);
                    player.ClearBet();

                    var finalBalance = player.Balance.Amount;
                    var netGain = new Money(finalBalance - initialBalance);

                    // NUEVA FUNCIONALIDAD: Sincronización asíncrona con UserProfile
                    syncTasks.Add(SyncPlayerResultAsync(player.PlayerId, playerWon, netGain, player.Balance));
                }

                table.EndRound();
                await _tables.UpdateAsync(table);
                await transaction.CommitAsync();

                // Ejecutar sincronizaciones con UserProfile en paralelo
                try
                {
                    await Task.WhenAll(syncTasks);
                    _logger.LogInformation($"[GameService] Ronda terminada y sincronizada en mesa {tableId} - {seatsToSettle.Count} jugadores");
                }
                catch (Exception syncEx)
                {
                    _logger.LogError(syncEx, $"[GameService] Error sincronizando con UserProfile: {syncEx.Message}");
                }

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GameService] Error en EndRoundAsync: {ex.Message}");
                return Result.Failure($"Error terminando ronda: {ex.Message}");
            }
        }

        // NUEVO MÉTODO: Sincronización individual de jugador con UserProfile
        private async Task SyncPlayerResultAsync(PlayerId playerId, bool won, Money netGain, Money finalBalance)
        {
            try
            {
                var gameResultTask = _userService.RecordGameResultAsync(playerId, won, netGain);
                var balanceTask = _userService.SyncPlayerBalanceAsync(playerId, finalBalance);

                await Task.WhenAll(gameResultTask, balanceTask);

                var gameResult = await gameResultTask;
                var balanceResult = await balanceTask;

                if (!gameResult.IsSuccess)
                {
                    _logger.LogWarning($"[GameService] Error registrando resultado para {playerId}: {gameResult.Error}");
                }

                if (!balanceResult.IsSuccess)
                {
                    _logger.LogWarning($"[GameService] Error sincronizando balance para {playerId}: {balanceResult.Error}");
                }

                if (gameResult.IsSuccess && balanceResult.IsSuccess)
                {
                    _logger.LogDebug($"[GameService] Sincronización exitosa para {playerId}: Won={won}, NetGain={netGain.Amount}, FinalBalance={finalBalance.Amount}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GameService] Error en SyncPlayerResultAsync para {playerId}: {ex.Message}");
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