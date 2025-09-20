// IGameRoomService.cs - CORREGIDO PARA INCLUIR NUEVO MÉTODO SIN REGRESIÓN
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;

namespace BlackJack.Services.Game;

public interface IGameRoomService
{
    #region Gestión de Salas

    Task<Result<GameRoom>> CreateRoomAsync(string roomName, PlayerId hostPlayerId);
    Task<Result<GameRoom>> CreateRoomForTableAsync(string roomName, string tableId, PlayerId hostPlayerId);

    // NUEVO: Método principal para resolver regresión de salas separadas
    Task<Result<GameRoom>> JoinOrCreateRoomForTableAsync(string tableId, PlayerId playerId, string playerName);

    Task<Result<GameRoom?>> GetRoomByTableIdAsync(string tableId);
    Task<Result<GameRoom>> GetRoomAsync(string roomCode);
    Task<Result<GameRoom>> GetRoomByIdAsync(Guid roomId);
    Task<Result<List<GameRoom>>> GetActiveRoomsAsync();

    #endregion

    #region Gestión de Jugadores

    Task<Result> JoinRoomAsync(string roomCode, PlayerId playerId, string playerName);
    Task<Result> LeaveRoomAsync(string roomCode, PlayerId playerId);
    Task<Result> AddSpectatorAsync(string roomCode, PlayerId playerId, string spectatorName);
    Task<Result> RemoveSpectatorAsync(string roomCode, PlayerId playerId);

    #endregion

    #region Gestión de Asientos

    Task<Result> JoinSeatAsync(string roomCode, PlayerId playerId, int position);
    Task<Result> LeaveSeatAsync(string roomCode, PlayerId playerId);

    #endregion

    #region Control de Juego

    Task<Result> StartGameAsync(string roomCode, PlayerId hostPlayerId);
    Task<Result> NextTurnAsync(string roomCode);
    Task<Result> EndGameAsync(string roomCode);

    #endregion

    #region Consultas

    Task<Result<bool>> IsPlayerInRoomAsync(PlayerId playerId);
    Task<Result<string?>> GetPlayerCurrentRoomCodeAsync(PlayerId playerId);
    Task<Result<bool>> IsPlayerTurnAsync(string roomCode, PlayerId playerId);
    Task<Result<List<int>>> GetAvailablePositionsAsync(string roomCode);
    Task<Result<bool>> IsPositionOccupiedAsync(string roomCode, int position);
    Task<Result<int?>> GetPlayerPositionAsync(string roomCode, PlayerId playerId);
    Task<Result<Dictionary<Guid, int>>> GetRoomPositionsAsync(string roomCode);

    #endregion
}