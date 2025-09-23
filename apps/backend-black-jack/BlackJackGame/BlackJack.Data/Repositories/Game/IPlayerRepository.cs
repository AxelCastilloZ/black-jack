
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Betting;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Game;

public interface IPlayerRepository : IRepository<Player>
{
    // Métodos de consulta existentes
    Task<Player?> GetByPlayerIdAsync(PlayerId playerId);
    Task<List<Player>> GetPlayersByTableAsync(Guid tableId);
    Task<List<Player>> GetPlayersByIdsAsync(List<PlayerId> playerIds);
    Task<Dictionary<Guid, Player>> GetPlayerDictionaryByIdsAsync(List<PlayerId> playerIds);

    // NUEVOS MÉTODOS: CONSULTAS FRESCAS SIN TRACKING PARA GAMECONTROLHUB
    Task<Player?> GetByIdFreshAsync(Guid id);
    Task<Player?> GetByPlayerIdFreshAsync(PlayerId playerId);

    // Métodos de balance
    Task<bool> HasSufficientFundsAsync(PlayerId playerId, Money amount);
    Task<Money?> GetPlayerBalanceAsync(PlayerId playerId);
    Task<bool> UpdatePlayerBalanceAsync(PlayerId playerId, Money newBalance);
    Task<bool> UpdateMultiplePlayerBalancesAsync(Dictionary<PlayerId, Money> balanceUpdates);

    // Métodos de apuestas
    Task<bool> PlaceBetAsync(PlayerId playerId, Bet bet);
    Task<bool> ClearPlayerBetAsync(PlayerId playerId);
    Task<bool> ClearMultiplePlayerBetsAsync(List<PlayerId> playerIds);

    // Métodos de consulta para apuestas automáticas
    Task<List<Player>> GetPlayersWithInsufficientFundsAsync(List<PlayerId> playerIds, Money requiredAmount);
    Task<List<PlayerId>> GetPlayersWithActiveBetsAsync(List<PlayerId> playerIds);
}