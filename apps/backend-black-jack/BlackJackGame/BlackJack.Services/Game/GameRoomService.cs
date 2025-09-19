// GameRoomService.cs - ACTUALIZADO con eventos
using Microsoft.Extensions.Logging;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;
using BlackJack.Data.Repositories.Game;

namespace BlackJack.Services.Game;

public class GameRoomService : IGameRoomService
{
    private readonly IGameRoomRepository _gameRoomRepository;
    private readonly IRoomPlayerRepository _roomPlayerRepository;
    private readonly ITableRepository _tableRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly ILogger<GameRoomService> _logger;

    public GameRoomService(
        IGameRoomRepository gameRoomRepository,
        IRoomPlayerRepository roomPlayerRepository,
        ITableRepository tableRepository,
        IDomainEventDispatcher eventDispatcher,
        ILogger<GameRoomService> logger)
    {
        _gameRoomRepository = gameRoomRepository;
        _roomPlayerRepository = roomPlayerRepository;
        _tableRepository = tableRepository;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }

    #region Gestión de Salas

    public async Task<Result<GameRoom>> CreateRoomAsync(string roomName, PlayerId hostPlayerId)
    {
        try
        {
            _logger.LogInformation("[GameRoomService] Creating room: {RoomName} for host: {HostId}", roomName, hostPlayerId);

            // Verificar que el host no esté en otra sala
            var existingRoom = await _gameRoomRepository.GetPlayerCurrentRoomAsync(hostPlayerId);
            if (existingRoom != null)
            {
                return Result<GameRoom>.Failure("Ya estás en otra sala. Sal de esa sala primero.");
            }

            // Generar código único
            string roomCode;
            do
            {
                roomCode = GenerateRoomCode();
            }
            while (await _gameRoomRepository.RoomCodeExistsAsync(roomCode));

            // Crear sala
            var gameRoom = GameRoom.Create(roomName, hostPlayerId, roomCode);

            // Agregar el host como primer jugador
            gameRoom.AddPlayer(hostPlayerId, $"Host-{hostPlayerId.Value.ToString()[..8]}");

            await _gameRoomRepository.AddAsync(gameRoom);

            // AGREGADO: Disparar eventos de dominio
            await _eventDispatcher.DispatchEventsAsync(gameRoom);

            _logger.LogInformation("[GameRoomService] Room created successfully: {RoomCode}", roomCode);
            return Result<GameRoom>.Success(gameRoom);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error creating room: {Error}", ex.Message);
            return Result<GameRoom>.Failure($"Error creating room: {ex.Message}");
        }
    }

    public async Task<Result<GameRoom>> GetRoomAsync(string roomCode)
    {
        try
        {
            var room = await _gameRoomRepository.GetByRoomCodeAsync(roomCode);
            if (room == null)
            {
                return Result<GameRoom>.Failure("Sala no encontrada");
            }

            return Result<GameRoom>.Success(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error getting room {RoomCode}: {Error}", roomCode, ex.Message);
            return Result<GameRoom>.Failure($"Error getting room: {ex.Message}");
        }
    }

    public async Task<Result<GameRoom>> GetRoomByIdAsync(Guid roomId)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomId);
            if (room == null)
            {
                return Result<GameRoom>.Failure("Sala no encontrada");
            }

            return Result<GameRoom>.Success(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error getting room {RoomId}: {Error}", roomId, ex.Message);
            return Result<GameRoom>.Failure($"Error getting room: {ex.Message}");
        }
    }

    public async Task<Result<List<GameRoom>>> GetActiveRoomsAsync()
    {
        try
        {
            var rooms = await _gameRoomRepository.GetActiveRoomsAsync();
            return Result<List<GameRoom>>.Success(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error getting active rooms: {Error}", ex.Message);
            return Result<List<GameRoom>>.Failure($"Error getting active rooms: {ex.Message}");
        }
    }

    #endregion

    #region Gestión de Jugadores

    public async Task<Result> JoinRoomAsync(string roomCode, PlayerId playerId, string playerName)
    {
        try
        {
            _logger.LogInformation("[GameRoomService] Player {PlayerId} joining room {RoomCode}", playerId, roomCode);

            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result.Failure("Sala no encontrada");
            }

            // Verificar que el jugador no esté en otra sala
            var existingRoom = await _gameRoomRepository.GetPlayerCurrentRoomAsync(playerId);
            if (existingRoom != null && existingRoom.Id != room.Id)
            {
                return Result.Failure("Ya estás en otra sala. Sal de esa sala primero.");
            }

            // Verificar si ya está en esta sala
            if (room.IsPlayerInRoom(playerId))
            {
                return Result.Failure("Ya estás en esta sala");
            }

            // Agregar jugador
            room.AddPlayer(playerId, playerName);
            await _gameRoomRepository.UpdateAsync(room);

            // AGREGADO: Disparar eventos de dominio
            await _eventDispatcher.DispatchEventsAsync(room);

            _logger.LogInformation("[GameRoomService] Player {PlayerId} joined room {RoomCode} successfully", playerId, roomCode);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error joining room: {Error}", ex.Message);
            return Result.Failure($"Error joining room: {ex.Message}");
        }
    }

    public async Task<Result> LeaveRoomAsync(string roomCode, PlayerId playerId)
    {
        try
        {
            _logger.LogInformation("[GameRoomService] Player {PlayerId} leaving room {RoomCode}", playerId, roomCode);

            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result.Success(); // No error if room doesn't exist
            }

            if (!room.IsPlayerInRoom(playerId))
            {
                return Result.Success(); // No error if player wasn't in room
            }

            room.RemovePlayer(playerId);

            // Si no quedan jugadores, eliminar la sala
            if (room.PlayerCount == 0)
            {
                await _gameRoomRepository.DeleteAsync(room);
                _logger.LogInformation("[GameRoomService] Room {RoomCode} deleted - no players remaining", roomCode);
            }
            else
            {
                await _gameRoomRepository.UpdateAsync(room);

                // AGREGADO: Disparar eventos de dominio
                await _eventDispatcher.DispatchEventsAsync(room);
            }

            _logger.LogInformation("[GameRoomService] Player {PlayerId} left room {RoomCode} successfully", playerId, roomCode);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error leaving room: {Error}", ex.Message);
            return Result.Failure($"Error leaving room: {ex.Message}");
        }
    }

    public async Task<Result> AddSpectatorAsync(string roomCode, PlayerId playerId, string spectatorName)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result.Failure("Sala no encontrada");
            }

            room.AddSpectator(playerId, spectatorName);
            await _gameRoomRepository.UpdateAsync(room);

            // AGREGADO: Disparar eventos de dominio
            await _eventDispatcher.DispatchEventsAsync(room);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error adding spectator: {Error}", ex.Message);
            return Result.Failure($"Error adding spectator: {ex.Message}");
        }
    }

    public async Task<Result> RemoveSpectatorAsync(string roomCode, PlayerId playerId)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result.Success(); // No error if room doesn't exist
            }

            room.RemoveSpectator(playerId);
            await _gameRoomRepository.UpdateAsync(room);

            // AGREGADO: Disparar eventos de dominio
            await _eventDispatcher.DispatchEventsAsync(room);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error removing spectator: {Error}", ex.Message);
            return Result.Failure($"Error removing spectator: {ex.Message}");
        }
    }

    #endregion

    #region Control de Juego

    public async Task<Result> StartGameAsync(string roomCode, PlayerId hostPlayerId)
    {
        try
        {
            _logger.LogInformation("[GameRoomService] Starting game in room {RoomCode} by host {HostId}", roomCode, hostPlayerId);

            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result.Failure("Sala no encontrada");
            }

            if (!room.IsHost(hostPlayerId))
            {
                return Result.Failure("Solo el host puede iniciar el juego");
            }

            if (!room.CanStart)
            {
                return Result.Failure("No se puede iniciar el juego. Verifica que haya al menos 1 jugador.");
            }

            // Crear mesa de Blackjack
            var table = BlackjackTable.Create($"Table for Room {roomCode}");
            await _tableRepository.AddAsync(table);

            // Iniciar juego en la sala
            room.StartGame(table.Id);
            await _gameRoomRepository.UpdateAsync(room);

            // AGREGADO: Disparar eventos de dominio
            await _eventDispatcher.DispatchEventsAsync(room);

            _logger.LogInformation("[GameRoomService] Game started in room {RoomCode} with table {TableId}", roomCode, table.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error starting game: {Error}", ex.Message);
            return Result.Failure($"Error starting game: {ex.Message}");
        }
    }

    public async Task<Result> NextTurnAsync(string roomCode)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result.Failure("Sala no encontrada");
            }

            if (!room.IsGameInProgress)
            {
                return Result.Failure("No hay juego en progreso");
            }

            room.NextTurn();
            await _gameRoomRepository.UpdateAsync(room);

            // AGREGADO: Disparar eventos de dominio
            await _eventDispatcher.DispatchEventsAsync(room);

            _logger.LogInformation("[GameRoomService] Turn advanced in room {RoomCode}", roomCode);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error advancing turn: {Error}", ex.Message);
            return Result.Failure($"Error advancing turn: {ex.Message}");
        }
    }

    public async Task<Result> EndGameAsync(string roomCode)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result.Failure("Sala no encontrada");
            }

            room.EndGame();
            await _gameRoomRepository.UpdateAsync(room);

            // AGREGADO: Disparar eventos de dominio  
            await _eventDispatcher.DispatchEventsAsync(room);

            _logger.LogInformation("[GameRoomService] Game ended in room {RoomCode}", roomCode);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error ending game: {Error}", ex.Message);
            return Result.Failure($"Error ending game: {ex.Message}");
        }
    }

    #endregion

    #region Consultas

    public async Task<Result<bool>> IsPlayerInRoomAsync(PlayerId playerId)
    {
        try
        {
            var room = await _gameRoomRepository.GetPlayerCurrentRoomAsync(playerId);
            return Result<bool>.Success(room != null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error checking if player is in room: {Error}", ex.Message);
            return Result<bool>.Failure($"Error checking player status: {ex.Message}");
        }
    }

    public async Task<Result<string?>> GetPlayerCurrentRoomCodeAsync(PlayerId playerId)
    {
        try
        {
            var room = await _gameRoomRepository.GetPlayerCurrentRoomAsync(playerId);
            return Result<string?>.Success(room?.RoomCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error getting player current room: {Error}", ex.Message);
            return Result<string?>.Failure($"Error getting current room: {ex.Message}");
        }
    }

    public async Task<Result<bool>> IsPlayerTurnAsync(string roomCode, PlayerId playerId)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result<bool>.Failure("Sala no encontrada");
            }

            var isPlayerTurn = room.IsPlayerTurn(playerId);
            return Result<bool>.Success(isPlayerTurn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error checking player turn: {Error}", ex.Message);
            return Result<bool>.Failure($"Error checking turn: {ex.Message}");
        }
    }

    #endregion

    #region Métodos Privados

    private static string GenerateRoomCode()
    {
        var random = new Random();
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    #endregion
}