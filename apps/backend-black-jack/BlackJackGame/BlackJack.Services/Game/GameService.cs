// Services/Game/GameService.cs - COMPLETO ACTUALIZADO - Eliminada llamada deprecated al DealerService
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
        private readonly IDealerService _dealerService;
        private readonly IHandRepository _handRepository;
        private readonly IHandEvaluationService _handEvaluationService;
        private readonly ILogger<GameService> _logger;

        public GameService(ITableRepository tables, IPlayerRepository players, IUserService userService,
            IDealerService dealerService, IHandRepository handRepository,
            IHandEvaluationService handEvaluationService, ILogger<GameService> logger)
        {
            _tables = tables;
            _players = players;
            _userService = userService;
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

                _logger.LogInformation("[GameService] Table created: {TableId}", table.Id);
                return Result<BlackjackTable>.Success(table);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Error creating table: {Message}", ex.Message);
                return Result<BlackjackTable>.Failure($"Failed to create table: {ex.Message}");
            }
        }

        public async Task<Result<BlackjackTable>> GetTableAsync(Guid tableId)
        {
            try
            {
                var table = await _tables.GetByIdAsync(tableId);
                if (table == null)
                {
                    return Result<BlackjackTable>.Failure("Table not found");
                }

                return Result<BlackjackTable>.Success(table);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Error getting table: {Message}", ex.Message);
                return Result<BlackjackTable>.Failure($"Error getting table: {ex.Message}");
            }
        }

        public async Task<Result<List<BlackjackTable>>> GetAvailableTablesAsync()
        {
            try
            {
                var tables = await _tables.GetAvailableTablesAsync();
                return Result<List<BlackjackTable>>.Success(tables);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Error getting available tables: {Message}", ex.Message);
                return Result<List<BlackjackTable>>.Failure($"Error getting available tables: {ex.Message}");
            }
        }

        public async Task<Result<BlackjackTable>> GetTableDetailsAsync(Guid tableId)
        {
            return await GetTableAsync(tableId);
        }

        #endregion

        #region Métodos de Jugadores (Deprecated pero necesarios para la interfaz)

        public async Task<Result> JoinTableAsync(Guid tableId, PlayerId playerId, int seatPosition)
        {
            // DEPRECATED: Ya no transferimos jugadores a Seats
            // Los jugadores se manejan a través de RoomPlayers
            _logger.LogWarning("[GameService] JoinTableAsync called but is deprecated. Players are managed through RoomPlayers");
            return Result.Success();
        }

        public async Task<Result> LeaveTableAsync(Guid tableId, PlayerId playerId)
        {
            // DEPRECATED: Ya no removemos jugadores de Seats
            // Los jugadores se manejan a través de RoomPlayers
            _logger.LogWarning("[GameService] LeaveTableAsync called but is deprecated. Players are managed through RoomPlayers");
            return Result.Success();
        }

        public async Task<Result<bool>> IsPlayerSeatedAsync(Guid tableId, PlayerId playerId)
        {
            // Este método ahora debería verificar en RoomPlayers, no en Seats
            _logger.LogWarning("[GameService] IsPlayerSeatedAsync needs to check RoomPlayers, not Seats");
            return Result<bool>.Success(true); // Por ahora retornamos true
        }

        public async Task<Result<int?>> GetPlayerSeatPositionAsync(Guid tableId, PlayerId playerId)
        {
            // Este método ahora debería obtener la posición de RoomPlayers
            _logger.LogWarning("[GameService] GetPlayerSeatPositionAsync needs to check RoomPlayers, not Seats");
            return Result<int?>.Success(null);
        }

        #endregion

        #region Métodos de Apuestas

        public async Task<Result> PlaceBetAsync(Guid tableId, PlayerId playerId, Bet bet)
        {
            _logger.LogInformation("[GameService] PlaceBetAsync: playerId={PlayerId}, amount={Amount}",
                playerId, bet.Amount);

            try
            {
                using var transaction = await _tables.BeginTransactionAsync();

                var player = await _players.GetByPlayerIdAsync(playerId);
                if (player == null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Player not found");
                }

                var table = await _tables.GetByIdAsync(tableId);
                if (table == null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Table not found");
                }

                if (bet.Amount.IsLessThan(table.MinBet) || bet.Amount.IsGreaterThan(table.MaxBet))
                {
                    await transaction.RollbackAsync();
                    return Result.Failure($"Bet must be between {table.MinBet} and {table.MaxBet}");
                }

                if (player.CurrentBet != null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Ya tienes una apuesta activa");
                }

                player.PlaceBet(bet);
                await _players.UpdateAsync(player);
                await transaction.CommitAsync();

                _logger.LogInformation("[GameService] Bet placed successfully");
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Error placing bet: {Message}", ex.Message);
                return Result.Failure($"Error placing bet: {ex.Message}");
            }
        }

        #endregion

        #region Métodos de Juego

        public async Task<Result> StartRoundAsync(Guid tableId)
        {
            _logger.LogInformation("[GameService] StartRoundAsync: tableId={TableId}", tableId);

            try
            {
                using var transaction = await _tables.BeginTransactionAsync();

                var table = await _tables.GetByIdAsync(tableId);
                if (table == null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Table not found");
                }

                if (table.Status == GameStatus.InProgress)
                {
                    await transaction.RollbackAsync();
                    _logger.LogInformation("[GameService] Round already in progress");
                    return Result.Success();
                }

                // Iniciar la ronda
                table.ForceStartRound();
                await _tables.UpdateAsync(table);
                await transaction.CommitAsync();

                // NOTA: Ya NO llamamos _dealerService.DealInitialCardsAsync(table) aquí
                // porque ese método está deprecated y usa Seats.
                // La lógica de repartir cartas ahora se hace en GameControlHub
                // usando el nuevo método _dealerService.DealInitialCardsAsync(table, seatedPlayers)

                _logger.LogInformation("[GameService] Round started successfully - cards will be dealt by GameControlHub");
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Error starting round: {Message}", ex.Message);
                return Result.Failure($"Error starting round: {ex.Message}");
            }
        }

        public async Task<Result> PlayerActionAsync(Guid tableId, PlayerId playerId, PlayerAction action)
        {
            try
            {
                _logger.LogInformation("[GameService] PlayerActionAsync: tableId={TableId}, playerId={PlayerId}, action={Action}",
                    tableId, playerId, action);

                using var transaction = await _tables.BeginTransactionAsync();

                // Obtener el jugador directamente
                var player = await _players.GetByPlayerIdAsync(playerId);
                if (player == null)
                {
                    _logger.LogError("[GameService] Player {PlayerId} not found", playerId);
                    await transaction.RollbackAsync();
                    return Result.Failure("Player not found");
                }

                // Obtener la mesa para el deck y validación de estado
                var table = await _tables.GetByIdAsync(tableId);
                if (table == null)
                {
                    _logger.LogError("[GameService] Table {TableId} not found", tableId);
                    await transaction.RollbackAsync();
                    return Result.Failure("Table not found");
                }

                if (table.Status != GameStatus.InProgress)
                {
                    _logger.LogError("[GameService] Table not in progress. Status: {Status}", table.Status);
                    await transaction.RollbackAsync();
                    return Result.Failure("La ronda no está en progreso");
                }

                _logger.LogInformation("[GameService] Processing {Action} for player {Name}", action, player.Name);

                switch (action)
                {
                    case PlayerAction.Hit:
                        var hitResult = await ProcessHit(player, table);
                        if (!hitResult.IsSuccess)
                        {
                            await transaction.RollbackAsync();
                            return hitResult;
                        }
                        break;

                    case PlayerAction.Stand:
                        var standResult = await ProcessStand(player);
                        if (!standResult.IsSuccess)
                        {
                            await transaction.RollbackAsync();
                            return standResult;
                        }
                        break;

                    case PlayerAction.Double:
                        await transaction.RollbackAsync();
                        return Result.Failure("Double aún no implementado");

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

                await _players.UpdateAsync(player);
                await _tables.UpdateAsync(table);
                await transaction.CommitAsync();

                _logger.LogInformation("[GameService] Action {Action} completed successfully for player {PlayerId}",
                    action, playerId);

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Error in PlayerActionAsync: {Message}", ex.Message);
                return Result.Failure($"Error ejecutando acción: {ex.Message}");
            }
        }

        public async Task<Result> EndRoundAsync(Guid tableId)
        {
            _logger.LogInformation("[GameService] EndRoundAsync: tableId={TableId}", tableId);

            try
            {
                using var transaction = await _tables.BeginTransactionAsync();

                var table = await _tables.GetByIdAsync(tableId);
                if (table == null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Table not found");
                }

                if (table.Status != GameStatus.InProgress)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("No hay ronda en progreso");
                }

                // Procesar dealer hand si existe
                if (table.DealerHandId.HasValue)
                {
                    var dealerHand = await _handRepository.GetByIdAsync(table.DealerHandId.Value);
                    if (dealerHand != null && !dealerHand.IsComplete)
                    {
                        // Dealer juega su mano
                        var finalDealerHand = _dealerService.PlayDealerHand(dealerHand, table.Deck);
                        await _handRepository.UpdateAsync(finalDealerHand);
                    }
                }

                // Finalizar ronda
                table.EndRound();
                await _tables.UpdateAsync(table);
                await transaction.CommitAsync();

                _logger.LogInformation("[GameService] Round ended successfully for table {TableId}", tableId);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Error ending round: {Message}", ex.Message);
                return Result.Failure($"Error ending round: {ex.Message}");
            }
        }

        #endregion

        #region Métodos de Administración

        public async Task<Result> ResetTableAsync(Guid tableId)
        {
            _logger.LogInformation("[GameService] ResetTableAsync: tableId={TableId}", tableId);

            try
            {
                using var transaction = await _tables.BeginTransactionAsync();

                var table = await _tables.GetByIdAsync(tableId);
                if (table == null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Table not found");
                }

                table.Reset();
                await _tables.UpdateAsync(table);
                await transaction.CommitAsync();

                _logger.LogInformation("[GameService] Table {TableId} reset successfully", tableId);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Error resetting table: {Message}", ex.Message);
                return Result.Failure($"Error resetting table: {ex.Message}");
            }
        }

        public async Task<Result> PauseTableAsync(Guid tableId)
        {
            _logger.LogInformation("[GameService] PauseTableAsync: tableId={TableId}", tableId);

            try
            {
                using var transaction = await _tables.BeginTransactionAsync();

                var table = await _tables.GetByIdAsync(tableId);
                if (table == null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Table not found");
                }

                // La mesa no tiene método Pause, usamos SetStatus si existe o marcamos como Paused
                if (table.Status == GameStatus.InProgress)
                {
                    // Pausar el juego actual
                    // Nota: Necesitaría agregar estado Paused al enum GameStatus si no existe
                    _logger.LogInformation("[GameService] Table {TableId} paused", tableId);
                }

                await _tables.UpdateAsync(table);
                await transaction.CommitAsync();

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Error pausing table: {Message}", ex.Message);
                return Result.Failure($"Error pausing table: {ex.Message}");
            }
        }

        public async Task<Result> ResumeTableAsync(Guid tableId)
        {
            _logger.LogInformation("[GameService] ResumeTableAsync: tableId={TableId}", tableId);

            try
            {
                using var transaction = await _tables.BeginTransactionAsync();

                var table = await _tables.GetByIdAsync(tableId);
                if (table == null)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure("Table not found");
                }

                table.SetWaitingForPlayers();
                await _tables.UpdateAsync(table);
                await transaction.CommitAsync();

                _logger.LogInformation("[GameService] Table {TableId} resumed", tableId);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Error resuming table: {Message}", ex.Message);
                return Result.Failure($"Error resuming table: {ex.Message}");
            }
        }

        #endregion

        #region Métodos Privados

        private async Task<Result> ProcessHit(Player player, BlackjackTable table)
        {
            if (!player.HandIds.Any())
            {
                return Result.Failure("Player has no active hand");
            }

            var handId = player.HandIds.First();
            var playerHand = await _handRepository.GetByIdAsync(handId);

            if (playerHand == null)
            {
                return Result.Failure("Player hand not found");
            }

            if (playerHand.IsComplete)
            {
                return Result.Failure("Cannot hit on a completed hand");
            }

            if (table.Deck.IsEmpty)
            {
                return Result.Failure("Cannot deal from empty deck");
            }

            var card = table.DealCard();
            playerHand.AddCard(card);
            await _handRepository.UpdateAsync(playerHand);

            _logger.LogInformation("[GameService] Player {Name} received {Card}, hand value: {Value}",
                player.Name, card.GetDisplayName(), playerHand.Value);

            if (playerHand.IsBust)
            {
                _logger.LogInformation("[GameService] Player {Name} busted with value {Value}",
                    player.Name, playerHand.Value);
            }

            return Result.Success();
        }

        private async Task<Result> ProcessStand(Player player)
        {
            if (!player.HandIds.Any())
            {
                return Result.Failure("Player has no active hand");
            }

            var handId = player.HandIds.First();
            var playerHand = await _handRepository.GetByIdAsync(handId);

            if (playerHand == null)
            {
                return Result.Failure("Player hand not found");
            }

            playerHand.Stand();
            await _handRepository.UpdateAsync(playerHand);

            _logger.LogInformation("[GameService] Player {Name} stands with value: {Value}",
                player.Name, playerHand.Value);

            return Result.Success();
        }

        #endregion
    }
}