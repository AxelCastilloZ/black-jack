// BlackJack.Data.Repositories.Game/IPlayerRepository.cs - EXTENDIDO PARA APUESTAS
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Betting;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Game;

public interface IPlayerRepository : IRepository<Player>
{
    // Métodos existentes
    Task<Player?> GetByPlayerIdAsync(PlayerId playerId);
    Task<List<Player>> GetPlayersByTableAsync(Guid tableId);

    // NUEVOS: Métodos para apuestas automáticas
    /// <summary>
    /// Obtiene múltiples jugadores por sus PlayerId
    /// </summary>
    Task<List<Player>> GetPlayersByIdsAsync(List<PlayerId> playerIds);

    /// <summary>
    /// Obtiene un diccionario de jugadores indexado por PlayerId para consultas rápidas
    /// </summary>
    Task<Dictionary<Guid, Player>> GetPlayerDictionaryByIdsAsync(List<PlayerId> playerIds);

    /// <summary>
    /// Verifica si un jugador tiene fondos suficientes para una apuesta
    /// </summary>
    Task<bool> HasSufficientFundsAsync(PlayerId playerId, Money amount);

    /// <summary>
    /// Obtiene solo el balance de un jugador sin cargar toda la entidad
    /// </summary>
    Task<Money?> GetPlayerBalanceAsync(PlayerId playerId);

    /// <summary>
    /// Actualiza el balance de un jugador de forma atómica
    /// </summary>
    Task<bool> UpdatePlayerBalanceAsync(PlayerId playerId, Money newBalance);

    /// <summary>
    /// Actualiza múltiples balances de jugadores en una sola transacción
    /// </summary>
    Task<bool> UpdateMultiplePlayerBalancesAsync(Dictionary<PlayerId, Money> balanceUpdates);

    /// <summary>
    /// Coloca una apuesta para un jugador y actualiza su balance atómicamente
    /// </summary>
    Task<bool> PlaceBetAsync(PlayerId playerId, Bet bet);

    /// <summary>
    /// Limpia la apuesta actual de un jugador
    /// </summary>
    Task<bool> ClearPlayerBetAsync(PlayerId playerId);

    /// <summary>
    /// Limpia las apuestas de múltiples jugadores
    /// </summary>
    Task<bool> ClearMultiplePlayerBetsAsync(List<PlayerId> playerIds);

    /// <summary>
    /// Obtiene jugadores con fondos insuficientes para una cantidad específica
    /// </summary>
    Task<List<Player>> GetPlayersWithInsufficientFundsAsync(List<PlayerId> playerIds, Money requiredAmount);

    /// <summary>
    /// Verifica qué jugadores tienen apuestas activas
    /// </summary>
    Task<List<PlayerId>> GetPlayersWithActiveBetsAsync(List<PlayerId> playerIds);
}