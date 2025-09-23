// Services/Game/GameService.cs - COMPLETO CORREGIDO SIN ERRORES TIPOGRÁFICOS
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
        private readonly IGameRoomRepository _gameRoomRepository;
        private readonly ILogger<GameService> _logger;

        // NUEVO: Constantes del juego
        private const int MAX_ROUNDS_PER_GAME = 5;
        private const int MIN_PLAYERS_TO_START = 1;

        public GameService(ITableRepository tables, IPlayerRepository players, IUserService userService,
            IDealerService dealerService, IHandRepository handRepository,
            IHandEvaluationService handEvaluationService, IGameRoomRepository gameRoomRepository,
            ILogger<GameService> logger)
        {
            _tables = tables;
            _players = players;
            _userService = userService;
            _dealerService = dealerService;
            _handRepository = handRepository;
            _handEvaluationService = handEvaluationService;
            _gameRoomRepository = gameRoomRepository;
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
            _logger.LogWarning("[GameService] JoinTableAsync called but is deprecated. Players are managed through RoomPlayers");
            return Result.Success();
        }

        public async Task<Result> LeaveTableAsync(Guid tableId, PlayerId playerId)
        {
            _logger.LogWarning("[GameService] LeaveTableAsync called but is deprecated. Players are managed through RoomPlayers");
            return Result.Success();
        }

        public async Task<Result<bool>> IsPlayerSeatedAsync(Guid tableId, PlayerId playerId)
        {
            _logger.LogWarning("[GameService] IsPlayerSeatedAsync needs to check RoomPlayers, not Seats");
            return Result<bool>.Success(true);
        }

        public async Task<Result<int?>> GetPlayerSeatPositionAsync(Guid tableId, PlayerId playerId)
        {
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

                // NUEVO: Verificar límite de rondas
                if (table.RoundNumber >= MAX_ROUNDS_PER_GAME)
                {
                    await transaction.RollbackAsync();
                    _logger.LogInformation("[GameService] Game completed - reached maximum {MaxRounds} rounds", MAX_ROUNDS_PER_GAME);
                    return Result.Failure($"Juego completado - máximo {MAX_ROUNDS_PER_GAME} rondas alcanzadas");
                }

                if (table.Status == GameStatus.InProgress)
                {
                    await transaction.RollbackAsync();
                    _logger.LogInformation("[GameService] Round already in progress");
                    return Result.Success();
                }

                // ✅ FIX CRÍTICO: Resetear turnos usando solo jugadores sentados
                var room = await _gameRoomRepository.GetByTableIdAsync(tableId);
                if (room != null)
                {
                    foreach (var player in room.SeatedPlayers)
                    {
                        player.ResetTurn();
                    }
                    // ✅ CAMBIO CRÍTICO: Establecer el primer jugador SENTADO como actual
                    room.SetFirstSeatedPlayerAsCurrent();
                    await _gameRoomRepository.UpdateAsync(room);

                    _logger.LogInformation("[GameService] Round initialized with first seated player: {PlayerName}",
                        room.CurrentPlayer?.Name ?? "None");
                }

                // Iniciar la ronda
                table.ForceStartRound();
                await _tables.UpdateAsync(table);
                await transaction.CommitAsync();

                _logger.LogInformation("[GameService] Round {RoundNumber}/{MaxRounds} started successfully - cards will be dealt by GameControlHub",
                    table.RoundNumber, MAX_ROUNDS_PER_GAME);
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

                // ✅ FIX CRÍTICO: Obtener la sala para validación de turnos con jugadores sentados
                var room = await _gameRoomRepository.GetByTableIdAsync(tableId);
                if (room == null)
                {
                    _logger.LogError("[GameService] Room not found for table {TableId}", tableId);
                    await transaction.RollbackAsync();
                    return Result.Failure("Sala no encontrada");
                }

                // ✅ VALIDACIÓN CRÍTICA: Verificar turno usando el GameRoom corregido
                if (!room.IsPlayerTurn(playerId))
                {
                    var currentPlayer = room.CurrentPlayer;
                    _logger.LogWarning("[GameService] Player {PlayerId} attempted action but it's {CurrentPlayer}'s turn",
                        playerId, currentPlayer?.Name ?? "unknown");
                    await transaction.RollbackAsync();
                    return Result.Failure($"No es tu turno. Es el turno de {currentPlayer?.Name ?? "otro jugador"}");
                }

                // ✅ NUEVA VALIDACIÓN: Verificar que el jugador esté sentado
                if (!room.IsPlayerSeated(playerId))
                {
                    _logger.LogWarning("[GameService] Player {PlayerId} is not seated", playerId);
                    await transaction.RollbackAsync();
                    return Result.Failure("Debes estar sentado para jugar");
                }

                // Obtener el jugador directamente
                var player = await _players.GetByPlayerIdAsync(playerId);
                if (player == null)
                {
                    _logger.LogError("[GameService] Player {PlayerId} not found", playerId);
                    await transaction.RollbackAsync();
                    return Result.Failure("Player not found");
                }

                // Obtener RoomPlayer para marcar turno jugado
                var roomPlayer = room.GetPlayer(playerId);
                if (roomPlayer == null)
                {
                    _logger.LogError("[GameService] RoomPlayer {PlayerId} not found in room", playerId);
                    await transaction.RollbackAsync();
                    return Result.Failure("Jugador no encontrado en la sala");
                }

                _logger.LogInformation("[GameService] Processing {Action} for player {Name} (turn: {CurrentIndex})",
                    action, player.Name, room.CurrentPlayerIndex);

                bool shouldAdvanceTurn = false;

                switch (action)
                {
                    case PlayerAction.Hit:
                        var hitResult = await ProcessHit(player, table);
                        if (!hitResult.IsSuccess)
                        {
                            await transaction.RollbackAsync();
                            return hitResult;
                        }

                        // Verificar si el jugador se pasó (bust) - si es así, avanzar turno
                        var playerHand = await GetPlayerActiveHand(player);
                        if (playerHand != null && playerHand.IsBust)
                        {
                            shouldAdvanceTurn = true;
                            roomPlayer.MarkTurnPlayed();
                            _logger.LogInformation("[GameService] Player {Name} busted, advancing turn", player.Name);
                        }
                        break;

                    case PlayerAction.Stand:
                        var standResult = await ProcessStand(player);
                        if (!standResult.IsSuccess)
                        {
                            await transaction.RollbackAsync();
                            return standResult;
                        }

                        // Stand siempre avanza el turno
                        shouldAdvanceTurn = true;
                        roomPlayer.MarkTurnPlayed();
                        _logger.LogInformation("[GameService] Player {Name} stands, advancing turn", player.Name);
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

                // Actualizar entidades
                await _players.UpdateAsync(player);
                await _tables.UpdateAsync(table);

                // ✅ FIX CRÍTICO: Avanzar turno usando solo jugadores sentados
                if (shouldAdvanceTurn)
                {
                    var advanceResult = await AdvanceToNextTurnSeated(room, table);
                    if (!advanceResult.IsSuccess)
                    {
                        // Log pero no fallar - el juego puede continuar
                        _logger.LogWarning("[GameService] Failed to advance turn: {Error}", advanceResult.Error);
                    }
                }

                await _gameRoomRepository.UpdateAsync(room);
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
                        var finalDealerHand = _dealerService.PlayDealerHand(dealerHand, table.Deck);
                        await _handRepository.UpdateAsync(finalDealerHand);
                    }
                }

                // ✅ NUEVO: Verificar si es la última ronda
                var isLastRound = table.RoundNumber >= MAX_ROUNDS_PER_GAME;

                if (isLastRound)
                {
                    _logger.LogInformation("[GameService] Final round ({RoundNumber}/{MaxRounds}) completed, determining winner",
                        table.RoundNumber, MAX_ROUNDS_PER_GAME);

                    // Determinar ganador del juego completo
                    var winnerResult = await DetermineGameWinner(tableId);
                    if (winnerResult.IsSuccess)
                    {
                        _logger.LogInformation("[GameService] Game winner determined: {Winner}", winnerResult.Value ?? "Empate");
                    }
                }

                // Finalizar ronda
                table.EndRound();
                await _tables.UpdateAsync(table);
                await transaction.CommitAsync();

                _logger.LogInformation("[GameService] Round {RoundNumber} ended successfully for table {TableId}{GameStatus}",
                    table.RoundNumber, tableId, isLastRound ? " (GAME COMPLETED)" : "");

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

                // NUEVO: Reset también la sala asociada
                var room = await _gameRoomRepository.GetByTableIdAsync(tableId);
                if (room != null)
                {
                    room.ResetForNewGame();
                    await _gameRoomRepository.UpdateAsync(room);
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

                if (table.Status == GameStatus.InProgress)
                {
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

        private async Task<Hand?> GetPlayerActiveHand(Player player)
        {
            if (!player.HandIds.Any())
                return null;

            var handId = player.HandIds.First();
            return await _handRepository.GetByIdAsync(handId);
        }

        // ✅ NUEVO MÉTODO: Avanzar turno solo entre jugadores SENTADOS
        private async Task<Result> AdvanceToNextTurnSeated(GameRoom room, BlackjackTable table)
        {
            try
            {
                var seatedPlayers = room.SeatedPlayers.ToList();
                var currentPlayerIndex = room.CurrentPlayerIndex;

                _logger.LogInformation("[GameService] Advancing turn from seated player {CurrentIndex}/{Total}",
                    currentPlayerIndex, seatedPlayers.Count);

                // Verificar si todos los jugadores SENTADOS han jugado
                var allSeatedPlayersPlayed = seatedPlayers.All(p => p.HasPlayedTurn);

                if (allSeatedPlayersPlayed)
                {
                    _logger.LogInformation("[GameService] All seated players have played their turn, ending round");

                    // Todos han jugado, terminar ronda automáticamente
                    await EndRoundAsync(table.Id);

                    // Resetear turnos para siguiente ronda si no es la última
                    if (table.RoundNumber < MAX_ROUNDS_PER_GAME)
                    {
                        foreach (var player in seatedPlayers)
                        {
                            player.ResetTurn();
                        }
                        room.SetFirstSeatedPlayerAsCurrent();
                        _logger.LogInformation("[GameService] Turns reset for next round");
                    }
                    else
                    {
                        _logger.LogInformation("[GameService] Game completed after {MaxRounds} rounds", MAX_ROUNDS_PER_GAME);
                    }
                }
                else
                {
                    // ✅ USAR EL MÉTODO CORREGIDO DE GameRoom que solo maneja jugadores sentados
                    room.NextTurn();
                    _logger.LogInformation("[GameService] Turn advanced to seated player {NewIndex} ({PlayerName})",
                        room.CurrentPlayerIndex, room.CurrentPlayer?.Name ?? "unknown");
                }

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Error advancing turn: {Message}", ex.Message);
                return Result.Failure($"Error advancing turn: {ex.Message}");
            }
        }

        // ✅ NUEVO: Determinar ganador del juego completo
        private async Task<Result<string?>> DetermineGameWinner(Guid tableId)
        {
            try
            {
                var room = await _gameRoomRepository.GetByTableIdAsync(tableId);
                if (room == null)
                {
                    return Result<string?>.Failure("Room not found");
                }

                var seatedPlayers = room.SeatedPlayers.ToList();
                var playerScores = new Dictionary<string, int>();

                // Calcular puntuación de cada jugador (simplificado: balance final)
                foreach (var roomPlayer in seatedPlayers)
                {
                    var player = await _players.GetByPlayerIdAsync(roomPlayer.PlayerId);
                    if (player != null)
                    {
                        // Por ahora, usamos balance como indicador de rendimiento
                        // En un sistema completo, rastrearías victorias/derrotas por ronda
                        var score = (int)player.Balance.Amount;
                        playerScores[player.Name] = score;

                        _logger.LogInformation("[GameService] Player {Name} final score: {Score}",
                            player.Name, score);
                    }
                }

                if (!playerScores.Any())
                {
                    return Result<string?>.Success(null);
                }

                // Encontrar el jugador con mayor puntuación
                var winner = playerScores.OrderByDescending(kvp => kvp.Value).First();
                var hasMultipleWinners = playerScores.Values.Count(score => score == winner.Value) > 1;

                if (hasMultipleWinners)
                {
                    var winners = playerScores.Where(kvp => kvp.Value == winner.Value).Select(kvp => kvp.Key);
                    _logger.LogInformation("[GameService] Game ended in tie between: {Winners}", string.Join(", ", winners));
                    return Result<string?>.Success(null); // Empate
                }
                else
                {
                    _logger.LogInformation("[GameService] Game winner: {Winner} with score {Score}", winner.Key, winner.Value);
                    return Result<string?>.Success(winner.Key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Error determining game winner: {Message}", ex.Message);
                return Result<string?>.Failure($"Error determining winner: {ex.Message}");
            }
        }

        #endregion
    }
}