using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Betting;
using BlackJack.Services.Common;

namespace BlackJack.Services.User;

public interface IUserService
{
    /// <summary>
    /// Crea un nuevo usuario con el perfil especificado
    /// </summary>
    Task<Result<UserProfile>> CreateUserAsync(string displayName, string email);

    /// <summary>
    /// Obtiene el perfil de usuario por PlayerId
    /// </summary>
    Task<Result<UserProfile>> GetUserAsync(PlayerId playerId);

    /// <summary>
    /// Obtiene un usuario existente o crea uno nuevo si no existe
    /// </summary>
    Task<Result<UserProfile>> GetOrCreateUserAsync(PlayerId playerId, string displayName, string email);

    /// <summary>
    /// Actualiza el balance del usuario
    /// </summary>
    Task<Result> UpdateBalanceAsync(PlayerId playerId, Money newBalance);

    /// <summary>
    /// Registra el resultado de un juego (victoria/derrota y ganancias)
    /// </summary>
    Task<Result> RecordGameResultAsync(PlayerId playerId, bool won, Money winnings);

    /// <summary>
    /// Actualiza el nombre de visualización del usuario
    /// </summary>
    Task<Result<UserProfile>> UpdateProfileAsync(PlayerId playerId, string displayName);

    /// <summary>
    /// Obtiene el ranking de usuarios ordenado por ganancias netas
    /// </summary>
    Task<Result<List<UserProfile>>> GetRankingAsync(int top = 10);

    /// <summary>
    /// Obtiene un usuario por su email
    /// </summary>
    Task<Result<UserProfile>> GetUserByEmailAsync(string email);

    /// <summary>
    /// Calcula las ganancias netas de un usuario (Balance actual - Balance inicial)
    /// </summary>
    Task<Result<decimal>> GetNetGainsAsync(PlayerId playerId);

    /// <summary>
    /// Sincroniza el balance de Player hacia UserProfile
    /// </summary>
    Task<Result> SyncPlayerBalanceAsync(PlayerId playerId, Money currentBalance);
}