// BlackJack.Services/Game/IGameRoomService.cs - INTERFAZ FINAL CORREGIDA
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;

namespace BlackJack.Services.Game;

public interface IGameRoomService
{
    // Métodos básicos de sala
    Task<Result<GameRoom>> CreateRoomAsync(string roomName, PlayerId hostPlayerId, Guid? blackjackTableId = null);
    Task<Result<GameRoom>> CreateRoomForTableAsync(string roomName, string tableId, PlayerId hostPlayerId);
    Task<Result<GameRoom>> JoinOrCreateRoomForTableAsync(string tableId, PlayerId playerId, string playerName);
    Task<Result> JoinRoomAsync(string roomCode, PlayerId playerId, string playerName);
    Task<Result> LeaveRoomAsync(string roomCode, PlayerId playerId);
    Task<Result<GameRoom>> GetRoomAsync(string roomCode);
    Task<Result<GameRoom>> GetRoomByIdAsync(Guid roomId);
    Task<Result<List<GameRoom>>> GetActiveRoomsAsync();
    Task<Result<string?>> GetPlayerCurrentRoomCodeAsync(PlayerId playerId);
    Task<Result<GameRoom?>> GetRoomByTableIdAsync(string tableId);

    // Métodos de espectadores
    Task<Result<bool>> AddSpectatorAsync(string roomCode, PlayerId playerId, string spectatorName);
    Task<Result<bool>> RemoveSpectatorAsync(string roomCode, PlayerId playerId);

    // Métodos de control de juego
    Task<Result<bool>> StartGameAsync(string roomCode, PlayerId hostPlayerId);
    Task<Result<bool>> EndGameAsync(string roomCode);
    Task<Result<bool>> NextTurnAsync(string roomCode);
    Task<Result<bool>> IsPlayerTurnAsync(string roomCode, PlayerId playerId);

    // Métodos de asientos (actualizados para usar BD)
    Task<Result<bool>> JoinSeatAsync(string roomCode, PlayerId playerId, int position);
    Task<Result<bool>> LeaveSeatAsync(string roomCode, PlayerId playerId);
    Task<Result<Dictionary<Guid, int>>> GetRoomPositionsAsync(string roomCode);
    Task<Result<List<SeatInfo>>> GetSeatInfoAsync(string roomCode);

    // Métodos de gestión de jugadores
    Task<Result<bool>> IsPlayerInRoomAsync(PlayerId playerId, string roomCode);
    Task<Result<bool>> IsPositionOccupiedAsync(string roomCode, int position);

    // Métodos de estado de sala
    Task<Result<List<GameRoom>>> GetRoomsByStatusAsync(RoomStatus status);
    Task<Result<bool>> GetAvailablePositionsAsync(string roomCode);
    Task<Result<GameRoomStats>> GetRoomStatsAsync(string roomCode);
}