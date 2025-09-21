// GameRoomService.cs - CORREGIDO FINAL para eliminar errores de compilación
using BlackJack.Data.Repositories.Game;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

using r = BlackJack.Services.Common.Result;

namespace BlackJack.Services.Game;

public class GameRoomService : IGameRoomService
{
    private readonly IGameRoomRepository _gameRoomRepository;
    private readonly IRoomPlayerRepository _roomPlayerRepository;
    private readonly ITableRepository _tableRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly ILogger<GameRoomService> _logger;

    // Lock distribuido por TableId más robusto
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _tableLocks = new();

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

    // CORREGIDO: Método CreateRoomAsync ahora acepta BlackjackTableId opcional
    public async Task<Result<GameRoom>> CreateRoomAsync(string roomName, PlayerId hostPlayerId, Guid? blackjackTableId = null)
    {
        try
        {
            _logger.LogInformation("[GameRoomService] Creating room: {RoomName} for host: {HostId}, TableId: {TableId}",
                roomName, hostPlayerId, blackjackTableId);

            var existingRoom = await _gameRoomRepository.GetPlayerCurrentRoomAsync(hostPlayerId);
            if (existingRoom != null)
            {
                return Result<GameRoom>.Failure("Ya estás en otra sala. Sal de esa sala primero.");
            }

            string roomCode;
            do
            {
                roomCode = GenerateRoomCode();
            }
            while (await _gameRoomRepository.RoomCodeExistsAsync(roomCode));

            var gameRoom = GameRoom.Create(roomName, hostPlayerId, roomCode);

            if (blackjackTableId.HasValue)
            {
                gameRoom.BlackjackTableId = blackjackTableId.Value;
                _logger.LogInformation("[GameRoomService] Assigned BlackjackTableId {TableId} to room {RoomCode}",
                    blackjackTableId.Value, roomCode);
            }

            gameRoom.AddPlayer(hostPlayerId, $"Host-{hostPlayerId.Value.ToString()[..8]}", false);
            await _gameRoomRepository.AddAsync(gameRoom);
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

    public async Task<Result<GameRoom>> CreateRoomForTableAsync(string roomName, string tableId, PlayerId hostPlayerId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tableId))
                return Result<GameRoom>.Failure("El ID de la mesa es requerido");

            if (!Guid.TryParse(tableId, out var tableGuid))
return Result<GameRoom>.Failure("ID de mesa inválido");

            var existingRoomResult = await GetRoomByTableIdAsync(tableId);
            if (existingRoomResult.IsSuccess && existingRoomResult.Value != null)
            {
                return Result<GameRoom>.Failure("Ya existe una sala para esta mesa");
            }

            var createResult = await CreateRoomAsync(roomName, hostPlayerId, tableGuid);
            return createResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error creating room for table: {Error}", ex.Message);
            return Result<GameRoom>.Failure($"Error creating room for table: {ex.Message}");
        }
    }

    public async Task<Result<GameRoom>> CreateRoomForTableAsViewerAsync(string roomName, string tableId, PlayerId hostPlayerId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tableId))
                return Result<GameRoom>.Failure("El ID de la mesa es requerido");

            if (!Guid.TryParse(tableId, out var tableGuid))
                return Result<GameRoom>.Failure("ID de mesa inválido");

            var existingRoomResult = await GetRoomByTableIdAsync(tableId);
            if (existingRoomResult.IsSuccess && existingRoomResult.Value != null)
            {
                return Result<GameRoom>.Failure("Ya existe una sala para esta mesa");
            }

            // For viewers, we allow them to create rooms even if they're in another room
            // First, leave any existing room they might be in
            var currentRoomResult = await GetPlayerCurrentRoomCodeAsync(hostPlayerId);
            if (currentRoomResult.IsSuccess && !string.IsNullOrEmpty(currentRoomResult.Value))
            {
                _logger.LogInformation("[GameRoomService] Viewer {PlayerId} leaving current room {CurrentRoom} to create new room for table {TableId}", 
                    hostPlayerId, currentRoomResult.Value, tableId);
                await LeaveRoomAsync(currentRoomResult.Value, hostPlayerId);
            }

            var createResult = await CreateRoomAsync(roomName, hostPlayerId, tableGuid);
            return createResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error creating room for table as viewer: {Error}", ex.Message);
            return Result<GameRoom>.Failure($"Error creating room for table as viewer: {ex.Message}");
        }
    }

    public async Task<Result<GameRoom>> JoinOrCreateRoomForTableAsync(string tableId, PlayerId playerId, string playerName)
    {
        var tableLock = _tableLocks.GetOrAdd(tableId, _ => new SemaphoreSlim(1, 1));
        await tableLock.WaitAsync();

        try
        {
            _logger.LogInformation("[GameRoomService] === ATOMIC OPERATION START ===");
            _logger.LogInformation("[GameRoomService] Player {PlayerId} requesting room for table {TableId}",
                playerId, tableId);

            if (!Guid.TryParse(tableId, out var tableGuid))
            {
                return Result<GameRoom>.Failure("ID de mesa inválido");
            }

            await _gameRoomRepository.FlushChangesAsync();
            var existingRoom = await _gameRoomRepository.GetRoomByTableIdAsync(tableGuid);

            if (existingRoom != null)
            {
                if (existingRoom.IsPlayerInRoom(playerId))
                {
                    return Result<GameRoom>.Success(existingRoom);
                }

                var joinResult = await JoinRoomAsync(existingRoom.RoomCode, playerId, playerName);
                if (joinResult.IsSuccess)
                {
                    var updatedRoom = await _gameRoomRepository.GetRoomWithPlayersAsync(existingRoom.RoomCode);
                    return Result<GameRoom>.Success(updatedRoom ?? existingRoom);
                }
                else
                {
                    return Result<GameRoom>.Failure(joinResult.Error);
                }
            }
            else
            {
                var roomName = $"Mesa {tableId.Substring(0, Math.Min(8, tableId.Length))}";
                var createResult = await CreateRoomForTableAsync(roomName, tableId, playerId);
                return createResult;
            }
        }
        finally
        {
            tableLock.Release();
            if (tableLock.CurrentCount == 1)
            {
                _tableLocks.TryRemove(tableId, out _);
            }
        }
    }

    public async Task<Result<GameRoom>> JoinOrCreateRoomForTableAsViewerAsync(string tableId, PlayerId playerId, string playerName)
    {
        var tableLock = _tableLocks.GetOrAdd(tableId, _ => new SemaphoreSlim(1, 1));
        await tableLock.WaitAsync();

        try
        {
            _logger.LogInformation("[GameRoomService] === ATOMIC OPERATION START (VIEWER) ===");
            _logger.LogInformation("[GameRoomService] Viewer {PlayerId} requesting room for table {TableId}",
                playerId, tableId);

            if (!Guid.TryParse(tableId, out var tableGuid))
            {
                return Result<GameRoom>.Failure("ID de mesa inválido");
            }

            await _gameRoomRepository.FlushChangesAsync();
            var existingRoom = await _gameRoomRepository.GetRoomByTableIdAsync(tableGuid);

            if (existingRoom != null)
            {
                // Check if player is already in this specific room
                if (existingRoom.IsPlayerInRoom(playerId))
                {
                    _logger.LogInformation("[GameRoomService] Viewer {PlayerId} already in room {RoomCode}", playerId, existingRoom.RoomCode);
                    return Result<GameRoom>.Success(existingRoom);
                }

                // For viewers, we allow them to join even if they're in another room
                // First, leave any existing room they might be in
                var currentRoomResult = await GetPlayerCurrentRoomCodeAsync(playerId);
                if (currentRoomResult.IsSuccess && !string.IsNullOrEmpty(currentRoomResult.Value))
                {
                    _logger.LogInformation("[GameRoomService] Viewer {PlayerId} leaving current room {CurrentRoom} to join {NewRoom}", 
                        playerId, currentRoomResult.Value, existingRoom.RoomCode);
                    await LeaveRoomAsync(currentRoomResult.Value, playerId);
                }

                var joinResult = await JoinRoomAsync(existingRoom.RoomCode, playerId, playerName, true); // isViewer = true
                if (joinResult.IsSuccess)
                {
                    var updatedRoom = await _gameRoomRepository.GetRoomWithPlayersAsync(existingRoom.RoomCode);
                    return Result<GameRoom>.Success(updatedRoom ?? existingRoom);
                }
                else
                {
                    return Result<GameRoom>.Failure(joinResult.Error);
                }
            }
            else
            {
                // For new rooms, use the viewer-specific creation method
                var roomName = $"Mesa {tableId.Substring(0, Math.Min(8, tableId.Length))}";
                var createResult = await CreateRoomForTableAsViewerAsync(roomName, tableId, playerId);
                return createResult;
            }
        }
        finally
        {
            tableLock.Release();
            if (tableLock.CurrentCount == 1)
            {
                _tableLocks.TryRemove(tableId, out _);
            }
        }
    }

    public async Task<Result<GameRoom?>> GetRoomByTableIdAsync(string tableId)
    {
        try
        {
            if (!Guid.TryParse(tableId, out var tableGuid))
                return Result<GameRoom?>.Failure("ID de mesa inválido");

            var room = await _gameRoomRepository.GetRoomByTableIdAsync(tableGuid);
            return Result<GameRoom?>.Success(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error searching room by table {TableId}: {Error}", tableId, ex.Message);
            return Result<GameRoom?>.Failure($"Error searching room by table: {ex.Message}");
        }
    }

    public async Task<Result<GameRoom>> GetRoomAsync(string roomCode)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
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

    public async Task<r> JoinRoomAsync(string roomCode, PlayerId playerId, string playerName, bool isViewer = false)
    {
        try
        {
            _logger.LogInformation("[GameRoomService] Player {PlayerId} joining room {RoomCode} as {Role}",
                playerId, roomCode, isViewer ? "viewer" : "player");

            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result.Failure("Sala no encontrada");
            }

            var existingRoom = await _gameRoomRepository.GetPlayerCurrentRoomAsync(playerId);
            if (existingRoom != null && existingRoom.Id != room.Id)
            {
                return Result.Failure("Ya estás en otra sala. Sal de esa sala primero.");
            }

            if (room.IsPlayerInRoom(playerId))
            {
                return Result.Success();
            }

            room.AddPlayer(playerId, playerName, isViewer);
            await _gameRoomRepository.UpdateAsync(room);
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

    public async Task<r> LeaveRoomAsync(string roomCode, PlayerId playerId)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result.Success();
            }

            if (!room.IsPlayerInRoom(playerId))
            {
                return Result.Success();
            }

            room.RemovePlayer(playerId);
            await _gameRoomRepository.FreeSeatAsync(roomCode, playerId);

            if (room.PlayerCount == 0)
            {
                await _gameRoomRepository.DeleteAsync(room);
            }
            else
            {
                await _gameRoomRepository.UpdateAsync(room);
                await _eventDispatcher.DispatchEventsAsync(room);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error leaving room: {Error}", ex.Message);
            return Result.Failure($"Error leaving room: {ex.Message}");
        }
    }

    public async Task<Result<bool>> AddSpectatorAsync(string roomCode, PlayerId playerId, string spectatorName)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result<bool>.Failure("Sala no encontrada");
            }

            if (room.IsPlayerInRoom(playerId))
            {
                return Result<bool>.Failure("Ya estás en esta sala como jugador");
            }

            if (room.Spectators.Any(s => s.PlayerId == playerId))
            {
                return Result<bool>.Failure("Ya eres espectador de esta sala");
            }

            var spectator = new Spectator(playerId, spectatorName, room.BlackjackTableId ?? Guid.Empty);
            room.AddSpectator(spectator);
            await _gameRoomRepository.UpdateAsync(room);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error adding spectator: {Error}", ex.Message);
            return Result<bool>.Failure("Error agregando espectador");
        }
    }

    public async Task<Result<bool>> RemoveSpectatorAsync(string roomCode, PlayerId playerId)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result<bool>.Failure("Sala no encontrada");
            }

            var spectator = room.Spectators.FirstOrDefault(s => s.PlayerId == playerId);
            if (spectator == null)
            {
                return Result<bool>.Failure("No eres espectador de esta sala");
            }

            room.RemoveSpectator(spectator);
            await _gameRoomRepository.UpdateAsync(room);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error removing spectator: {Error}", ex.Message);
            return Result<bool>.Failure("Error removiendo espectador");
        }
    }

    #endregion

    #region Gestión de Asientos

    public async Task<Result<bool>> JoinSeatAsync(string roomCode, PlayerId playerId, int position)
    {
        try
        {
            _logger.LogInformation("[GameRoomService] === JoinSeat VALIDATION START ===");
            _logger.LogInformation("[GameRoomService] Player: {PlayerId}, Room: {RoomCode}, Position: {Position}",
                playerId, roomCode, position);

            if (position < 0 || position > 5)
            {
                return Result<bool>.Failure("Posición de asiento inválida. Debe estar entre 0 y 5.");
            }

            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result<bool>.Failure("Sala no encontrada");
            }

            // Validación robusta de membership
            _logger.LogInformation("[GameRoomService] === ROBUST MEMBERSHIP VALIDATION ===");

            var isPlayerInRoom = room.IsPlayerInRoom(playerId);
            _logger.LogInformation("[GameRoomService] Membership check 1 (room instance): {IsValid}", isPlayerInRoom);

            var isPlayerInRoomDb = await _gameRoomRepository.IsPlayerInRoomAsync(playerId, roomCode);
            _logger.LogInformation("[GameRoomService] Membership check 2 (database direct): {IsValid}", isPlayerInRoomDb);

            var playerCurrentRoomResult = await GetPlayerCurrentRoomCodeAsync(playerId);
            var isInThisRoom = playerCurrentRoomResult.IsSuccess && playerCurrentRoomResult.Value == roomCode;
            _logger.LogInformation("[GameRoomService] Membership check 3 (player current room): {IsValid}", isInThisRoom);

            var isMembershipValid = isPlayerInRoom || isPlayerInRoomDb || isInThisRoom;

            _logger.LogInformation("[GameRoomService] === MEMBERSHIP VALIDATION SUMMARY ===");
            _logger.LogInformation("[GameRoomService] Room instance check: {Check1}", isPlayerInRoom);
            _logger.LogInformation("[GameRoomService] Database direct check: {Check2}", isPlayerInRoomDb);
            _logger.LogInformation("[GameRoomService] Current room check: {Check3}", isInThisRoom);
            _logger.LogInformation("[GameRoomService] FINAL MEMBERSHIP VALID: {IsValid}", isMembershipValid);

            if (!isMembershipValid)
            {
                var currentPlayers = string.Join(", ", room.Players.Select(p => p.PlayerId.Value));
                _logger.LogError("[GameRoomService] Player {PlayerId} FAILED all membership validations for room {RoomCode}",
                    playerId, roomCode);
                _logger.LogError("[GameRoomService] Current players in room: {Players}", currentPlayers);
                return Result<bool>.Failure("Debes estar en la sala para unirte a un asiento");
            }

            _logger.LogInformation("[GameRoomService] ✅ Player {PlayerId} passed membership validation for room {RoomCode}",
                playerId, roomCode);

            // Verificar si el asiento está ocupado
            var isSeatOccupied = await _gameRoomRepository.IsSeatOccupiedAsync(roomCode, position);
            if (isSeatOccupied)
            {
                return Result<bool>.Failure($"El asiento {position} ya está ocupado");
            }

            // Verificar si el jugador ya está sentado
            var isPlayerSeated = await _gameRoomRepository.IsPlayerSeatedAsync(roomCode, playerId);
            if (isPlayerSeated)
            {
                return Result<bool>.Failure("Ya estás sentado en un asiento. Debes salir primero del asiento actual");
            }

            // Actualizar posición en BD
            var roomPlayer = await _gameRoomRepository.GetRoomPlayerAsync(roomCode, playerId);
            if (roomPlayer == null)
            {
                return Result<bool>.Failure("Error: jugador no encontrado en la sala");
            }

            roomPlayer.JoinSeat(position);
            await _gameRoomRepository.UpdateRoomPlayerAsync(roomPlayer);

            _logger.LogInformation("[GameRoomService] Player {PlayerId} joined seat {Position} in room {RoomCode}",
                playerId, position, roomCode);
            _logger.LogInformation("[GameRoomService] === JoinSeat VALIDATION END - SUCCESS ===");

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] CRITICAL ERROR in JoinSeatAsync for player {PlayerId} in room {RoomCode}",
                playerId, roomCode);
            return Result<bool>.Failure("Error interno del servidor");
        }
    }

    public async Task<Result<bool>> LeaveSeatAsync(string roomCode, PlayerId playerId)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result<bool>.Failure("Sala no encontrada");
            }

            if (!room.IsPlayerInRoom(playerId))
            {
                return Result<bool>.Failure("No estás en esta sala");
            }

            var seatFreed = await _gameRoomRepository.FreeSeatAsync(roomCode, playerId);
            if (!seatFreed)
            {
                return Result<bool>.Failure("No estás sentado en ningún asiento");
            }

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error in LeaveSeatAsync: {Error}", ex.Message);
            return Result<bool>.Failure("Error interno del servidor");
        }
    }

    public async Task<Result<Dictionary<Guid, int>>> GetRoomPositionsAsync(string roomCode)
    {
        try
        {
            var positions = await _gameRoomRepository.GetSeatPositionsAsync(roomCode);
            _logger.LogInformation("[GameRoomService] Retrieved {Count} seat positions from database for room {RoomCode}",
                positions.Count, roomCode);
            return Result<Dictionary<Guid, int>>.Success(positions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error getting room positions: {Error}", ex.Message);
            return Result<Dictionary<Guid, int>>.Failure("Error obteniendo posiciones de la sala");
        }
    }

    public async Task<Result<List<SeatInfo>>> GetSeatInfoAsync(string roomCode)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result<List<SeatInfo>>.Failure("Sala no encontrada");
            }

            var seatInfoList = new List<SeatInfo>();

            for (int i = 0; i <= 5; i++)
            {
                var playerInSeat = await _gameRoomRepository.GetPlayerInSeatAsync(roomCode, i);

                var seatInfo = new SeatInfo
                {
                    Position = i,
                    IsOccupied = playerInSeat != null,
                    PlayerId = playerInSeat?.PlayerId.Value,
                    PlayerName = playerInSeat?.Name
                };

                seatInfoList.Add(seatInfo);
            }

            return Result<List<SeatInfo>>.Success(seatInfoList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error getting seat info: {Error}", ex.Message);
            return Result<List<SeatInfo>>.Failure("Error obteniendo información de asientos");
        }
    }

    #endregion

    #region Control de Juego

    public async Task<Result<bool>> StartGameAsync(string roomCode, PlayerId hostPlayerId)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result<bool>.Failure("Sala no encontrada");
            }

            if (room.HostPlayerId != hostPlayerId)
            {
                return Result<bool>.Failure("Solo el host puede iniciar el juego");
            }

            if (!room.CanStart)
            {
                return Result<bool>.Failure("La sala no puede iniciar el juego aún");
            }

            room.StartGame();
            await _gameRoomRepository.UpdateAsync(room);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error starting game: {Error}", ex.Message);
            return Result<bool>.Failure("Error iniciando el juego");
        }
    }

    public async Task<Result<bool>> EndGameAsync(string roomCode)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result<bool>.Failure("Sala no encontrada");
            }

            room.EndGame();

            // Limpiar posiciones de asientos
            var seatedPlayers = await _gameRoomRepository.GetSeatPositionsAsync(roomCode);
            foreach (var playerId in seatedPlayers.Keys)
            {
                await _gameRoomRepository.FreeSeatAsync(roomCode, PlayerId.From(playerId));
            }

            await _gameRoomRepository.UpdateAsync(room);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error ending game: {Error}", ex.Message);
            return Result<bool>.Failure("Error terminando el juego");
        }
    }

    public async Task<Result<bool>> NextTurnAsync(string roomCode)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result<bool>.Failure("Sala no encontrada");
            }

            room.NextTurn();
            await _gameRoomRepository.UpdateAsync(room);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error advancing turn: {Error}", ex.Message);
            return Result<bool>.Failure("Error avanzando el turno");
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

            var isPlayerTurn = room.CurrentPlayer?.PlayerId == playerId;
            return Result<bool>.Success(isPlayerTurn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error checking player turn: {Error}", ex.Message);
            return Result<bool>.Failure("Error verificando turno");
        }
    }

    #endregion

    #region Consultas

    public async Task<Result<bool>> IsPlayerInRoomAsync(PlayerId playerId, string roomCode)
    {
        try
        {
            var isInRoom = await _gameRoomRepository.IsPlayerInRoomAsync(playerId, roomCode);
            return Result<bool>.Success(isInRoom);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error checking player in room: {Error}", ex.Message);
            return Result<bool>.Failure("Error verificando membresía");
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
            _logger.LogError(ex, "[GameRoomService] Error getting current room: {Error}", ex.Message);
            return Result<string?>.Failure("Error obteniendo sala actual");
        }
    }

    public async Task<Result<bool>> IsPositionOccupiedAsync(string roomCode, int position)
    {
        try
        {
            var isOccupied = await _gameRoomRepository.IsSeatOccupiedAsync(roomCode, position);
            return Result<bool>.Success(isOccupied);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error checking position: {Error}", ex.Message);
            return Result<bool>.Failure("Error verificando posición");
        }
    }

    public async Task<Result<bool>> GetAvailablePositionsAsync(string roomCode)
    {
        try
        {
            var seatInfo = await GetSeatInfoAsync(roomCode);
            if (!seatInfo.IsSuccess)
            {
                return Result<bool>.Failure(seatInfo.Error);
            }

            var hasAvailableSeats = seatInfo.Value!.Any(seat => !seat.IsOccupied);
            return Result<bool>.Success(hasAvailableSeats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error getting available positions: {Error}", ex.Message);
            return Result<bool>.Failure("Error obteniendo posiciones disponibles");
        }
    }

    public async Task<Result<List<GameRoom>>> GetRoomsByStatusAsync(RoomStatus status)
    {
        try
        {
            var rooms = await _gameRoomRepository.GetRoomsByStatusAsync(status);
            return Result<List<GameRoom>>.Success(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error getting rooms by status: {Error}", ex.Message);
            return Result<List<GameRoom>>.Failure("Error obteniendo salas por estado");
        }
    }

    public async Task<Result<GameRoomStats>> GetRoomStatsAsync(string roomCode)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result<GameRoomStats>.Failure("Sala no encontrada");
            }

            var seatInfoResult = await GetSeatInfoAsync(roomCode);
            var seatInfo = seatInfoResult.IsSuccess ? seatInfoResult.Value! : new List<SeatInfo>();

            var stats = new GameRoomStats
            {
                RoomCode = room.RoomCode,
                TotalPlayers = room.PlayerCount,
                SeatedPlayers = seatInfo.Count(s => s.IsOccupied),
                Spectators = room.Spectators.Count,
                OccupiedSeats = seatInfo.Where(s => s.IsOccupied).Select(s => s.Position).ToList(),
                AvailableSeats = seatInfo.Where(s => !s.IsOccupied).Select(s => s.Position).ToList(),
                Status = room.Status,
                CreatedAt = room.CreatedAt,
                LastActivity = room.UpdatedAt
            };

            return Result<GameRoomStats>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error getting room stats: {Error}", ex.Message);
            return Result<GameRoomStats>.Failure("Error obteniendo estadísticas");
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

/// <summary>
/// Modelo para información de asientos
/// </summary>
public class SeatInfo
{
    public int Position { get; set; }
    public bool IsOccupied { get; set; }
    public Guid? PlayerId { get; set; }
    public string? PlayerName { get; set; }
}

/// <summary>
/// Modelo para estadísticas de sala (renombrado para evitar conflictos)
/// </summary>
public class GameRoomStats
{
    public string RoomCode { get; set; } = string.Empty;
    public int TotalPlayers { get; set; }
    public int SeatedPlayers { get; set; }
    public int Spectators { get; set; }
    public List<int> OccupiedSeats { get; set; } = new();
    public List<int> AvailableSeats { get; set; } = new();
    public RoomStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivity { get; set; }
}