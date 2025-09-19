// IGameRoomService.cs - EN SERVICES/GAME/
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;

namespace BlackJack.Services.Game;

public interface IGameRoomService
{
    // Gestión de salas
    Task<Result<GameRoom>> CreateRoomAsync(string roomName, PlayerId hostPlayerId);
    Task<Result<GameRoom>> GetRoomAsync(string roomCode);
    Task<Result<GameRoom>> GetRoomByIdAsync(Guid roomId);
    Task<Result<List<GameRoom>>> GetActiveRoomsAsync();

    // Gestión de jugadores
    Task<Result> JoinRoomAsync(string roomCode, PlayerId playerId, string playerName);
    Task<Result> LeaveRoomAsync(string roomCode, PlayerId playerId);
    Task<Result> AddSpectatorAsync(string roomCode, PlayerId playerId, string spectatorName);
    Task<Result> RemoveSpectatorAsync(string roomCode, PlayerId playerId);

    // Control de juego
    Task<Result> StartGameAsync(string roomCode, PlayerId hostPlayerId);
    Task<Result> NextTurnAsync(string roomCode);
    Task<Result> EndGameAsync(string roomCode);

    // Consultas
    Task<Result<bool>> IsPlayerInRoomAsync(PlayerId playerId);
    Task<Result<string?>> GetPlayerCurrentRoomCodeAsync(PlayerId playerId);
    Task<Result<bool>> IsPlayerTurnAsync(string roomCode, PlayerId playerId);
}