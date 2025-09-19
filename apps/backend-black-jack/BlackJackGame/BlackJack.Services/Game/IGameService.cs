// Services/Game/IGameService.cs - CORREGIDO CON GUID
using System.Threading.Tasks;
using BlackJack.Domain.Enums;
using BlackJack.Domain.Models.Betting;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;

namespace BlackJack.Services.Game;

public interface IGameService
{
    // Métodos de mesa
    Task<Result<BlackjackTable>> CreateTableAsync(string name, Money minBet, Money maxBet);
    Task<Result<BlackjackTable>> GetTableAsync(Guid tableId);

    // Métodos de jugadores
    Task<Result> JoinTableAsync(Guid tableId, PlayerId playerId, int seatPosition);
    Task<Result> LeaveTableAsync(Guid tableId, PlayerId playerId);

    // Métodos de apuestas
    Task<Result> PlaceBetAsync(Guid tableId, PlayerId playerId, Bet bet);

    // Métodos de juego
    Task<Result> StartRoundAsync(Guid tableId);
    Task<Result> PlayerActionAsync(Guid tableId, PlayerId playerId, PlayerAction action);
    Task<Result> EndRoundAsync(Guid tableId);

    // Métodos de administración
    Task<Result> ResetTableAsync(Guid tableId);
    Task<Result> PauseTableAsync(Guid tableId);
    Task<Result> ResumeTableAsync(Guid tableId);

    // Métodos de consulta adicionales
    Task<Result<List<BlackjackTable>>> GetAvailableTablesAsync();
    Task<Result<BlackjackTable>> GetTableDetailsAsync(Guid tableId);
    Task<Result<bool>> IsPlayerSeatedAsync(Guid tableId, PlayerId playerId);
    Task<Result<int?>> GetPlayerSeatPositionAsync(Guid tableId, PlayerId playerId);
}