// GameRoomService.cs - CORREGIDO PARA RESOLVER REGRESIÓN DE SALAS SEPARADAS
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

    // Cache en memoria para posiciones de asientos (temporal, no persistente)
    private readonly ConcurrentDictionary<string, Dictionary<PlayerId, int>> _roomPlayerPositions = new();

    // CORREGIDO: Lock distribuido por TableId para evitar race conditions
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

    public async Task<Result<GameRoom>> CreateRoomAsync(string roomName, PlayerId hostPlayerId)
    {
        try
        {
            _logger.LogInformation("[GameRoomService] Creating room: {RoomName} for host: {HostId}", roomName, hostPlayerId);

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
            gameRoom.AddPlayer(hostPlayerId, $"Host-{hostPlayerId.Value.ToString()[..8]}");

            await _gameRoomRepository.AddAsync(gameRoom);

            // CORREGIDO: Inicializar posiciones de asientos en memoria inmediatamente
            _roomPlayerPositions[roomCode] = new Dictionary<PlayerId, int>();
            _logger.LogInformation("[GameRoomService] Initialized seat positions for new room {RoomCode}", roomCode);

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

            var existingRoomResult = await GetRoomByTableIdAsync(tableId);
            if (existingRoomResult.IsSuccess && existingRoomResult.Value != null)
            {
                return Result<GameRoom>.Failure("Ya existe una sala para esta mesa");
            }

            var createResult = await CreateRoomAsync(roomName, hostPlayerId);
            if (!createResult.IsSuccess)
                return createResult;

            var room = createResult.Value!;

            if (Guid.TryParse(tableId, out var tableGuid))
            {
                room.BlackjackTableId = tableGuid;
                await _gameRoomRepository.UpdateAsync(room);
            }

            _logger.LogInformation("[GameRoomService] Created room {RoomCode} for table {TableId}",
                room.RoomCode, tableId);

            return Result<GameRoom>.Success(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error creating room for table: {Error}", ex.Message);
            return Result<GameRoom>.Failure($"Error creating room for table: {ex.Message}");
        }
    }

    // CORREGIDO: Nuevo método principal para resolver la regresión de salas separadas
    public async Task<Result<GameRoom>> JoinOrCreateRoomForTableAsync(string tableId, PlayerId playerId, string playerName)
    {
        // Obtener o crear semáforo para esta tabla específica (evita race conditions)
        var tableLock = _tableLocks.GetOrAdd(tableId, _ => new SemaphoreSlim(1, 1));

        await tableLock.WaitAsync();
        try
        {
            _logger.LogInformation("[GameRoomService] === LOCKED ACCESS for table {TableId} ===", tableId);
            _logger.LogInformation("[GameRoomService] Player {PlayerId} ({PlayerName}) requesting room for table {TableId}",
                playerId, playerName, tableId);

            // STEP 1: Buscar sala existente (dentro del lock para evitar race conditions)
            _logger.LogInformation("[GameRoomService] Searching for existing room for table {TableId}...", tableId);
            var existingRoomResult = await GetRoomByTableIdAsync(tableId);

            if (existingRoomResult.IsSuccess && existingRoomResult.Value != null)
            {
                // EXISTING ROOM PATH
                var existingRoom = existingRoomResult.Value;
                _logger.LogInformation("[GameRoomService] Found existing room {RoomCode} for table {TableId}",
                    existingRoom.RoomCode, tableId);

                // Unirse a la sala existente
                var joinResult = await JoinRoomAsync(existingRoom.RoomCode, playerId, playerName);

                if (joinResult.IsSuccess)
                {
                    _logger.LogInformation("[GameRoomService] Player {PlayerId} successfully joined existing room {RoomCode} for table {TableId}",
                        playerId, existingRoom.RoomCode, tableId);
                    return Result<GameRoom>.Success(existingRoom);
                }
                else
                {
                    _logger.LogError("[GameRoomService] Failed to join existing room {RoomCode}: {Error}",
                        existingRoom.RoomCode, joinResult.Error);
                    return Result<GameRoom>.Failure(joinResult.Error);
                }
            }
            else
            {
                // NEW ROOM PATH
                _logger.LogInformation("[GameRoomService] No existing room found for table {TableId}, creating new room...", tableId);

                var roomName = $"Mesa {tableId.Substring(0, 8)}";
                var createResult = await CreateRoomForTableAsync(roomName, tableId, playerId);

                if (createResult.IsSuccess)
                {
                    _logger.LogInformation("[GameRoomService] Created new room {RoomCode} for table {TableId}",
                        createResult.Value!.RoomCode, tableId);
                }
                else
                {
                    _logger.LogError("[GameRoomService] Failed to create room for table {TableId}: {Error}",
                        tableId, createResult.Error);
                }

                return createResult;
            }
        }
        finally
        {
            tableLock.Release();
            _logger.LogInformation("[GameRoomService] === UNLOCKED ACCESS for table {TableId} ===", tableId);

            // Cleanup: remover semáforo si no hay waiters (optimización de memoria)
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

            // CORREGIDO: Incluir logs detallados para debugging
            _logger.LogInformation("[GameRoomService] Searching for room with tableId: {TableId} (parsed: {TableGuid})", tableId, tableGuid);

            var rooms = await _gameRoomRepository.GetActiveRoomsAsync();
            _logger.LogInformation("[GameRoomService] Found {RoomCount} active rooms total", rooms.Count);

            foreach (var room in rooms)
            {
                _logger.LogInformation("[GameRoomService] Room {RoomCode}: TableId={TableId}, Status={Status}",
                    room.RoomCode, room.BlackjackTableId?.ToString() ?? "NULL", room.Status);
            }

            var matchingRoom = rooms.FirstOrDefault(r => r.BlackjackTableId == tableGuid);

            if (matchingRoom != null)
            {
                _logger.LogInformation("[GameRoomService] Found matching room {RoomCode} for table {TableId}",
                    matchingRoom.RoomCode, tableId);
            }
            else
            {
                _logger.LogInformation("[GameRoomService] No room found for table {TableId}", tableId);
            }

            return Result<GameRoom?>.Success(matchingRoom);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error buscando sala por mesa {TableId}: {Error}", tableId, ex.Message);
            return Result<GameRoom?>.Failure($"Error buscando sala por mesa: {ex.Message}");
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

            // CORREGIDO: Asegurar que el diccionario de posiciones existe para la sala
            if (!_roomPlayerPositions.ContainsKey(roomCode))
            {
                _roomPlayerPositions[roomCode] = new Dictionary<PlayerId, int>();
                _logger.LogInformation("[GameRoomService] Initialized seat positions for existing room {RoomCode}", roomCode);
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

            // Asegurar inicialización de posiciones
            if (!_roomPlayerPositions.ContainsKey(room.RoomCode))
            {
                _roomPlayerPositions[room.RoomCode] = new Dictionary<PlayerId, int>();
                _logger.LogInformation("[GameRoomService] Initialized seat positions for room {RoomCode}", room.RoomCode);
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

            // CORREGIDO: Asegurar inicialización de posiciones para todas las salas activas
            foreach (var room in rooms)
            {
                if (!_roomPlayerPositions.ContainsKey(room.RoomCode))
                {
                    _roomPlayerPositions[room.RoomCode] = new Dictionary<PlayerId, int>();
                }
            }

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

    public async Task<r> JoinRoomAsync(string roomCode, PlayerId playerId, string playerName)
    {
        try
        {
            _logger.LogInformation("[GameRoomService] Player {PlayerId} ({PlayerName}) joining room {RoomCode}",
                playerId, playerName, roomCode);

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
                _logger.LogInformation("[GameRoomService] Player {PlayerId} already in room {RoomCode}", playerId, roomCode);
                return Result.Success(); // Ya está en la sala, no es error
            }

            room.AddPlayer(playerId, playerName);

            // CORREGIDO: Manejo mejorado de concurrencia con retry
            var retryCount = 0;
            const int maxRetries = 3;

            while (retryCount < maxRetries)
            {
                try
                {
                    await _gameRoomRepository.UpdateAsync(room);
                    break;
                }
                catch (DbUpdateConcurrencyException)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError("[GameRoomService] Max retries reached for player {PlayerId} joining room {RoomCode}",
                            playerId, roomCode);
                        return Result.Failure("Conflicto de concurrencia. Intenta de nuevo.");
                    }

                    // Recargar la entidad y reintentar
                    await Task.Delay(100 * retryCount); // Backoff exponencial
                    room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
                    if (room == null)
                    {
                        return Result.Failure("Sala no encontrada");
                    }

                    if (room.IsPlayerInRoom(playerId))
                    {
                        _logger.LogInformation("[GameRoomService] Player {PlayerId} already joined during retry", playerId);
                        return Result.Success(); // Ya está en la sala
                    }

                    room.AddPlayer(playerId, playerName);
                }
            }

            // CORREGIDO: Asegurar que el diccionario de posiciones existe
            if (!_roomPlayerPositions.ContainsKey(roomCode))
            {
                _roomPlayerPositions[roomCode] = new Dictionary<PlayerId, int>();
                _logger.LogInformation("[GameRoomService] Initialized seat positions for room {RoomCode} on player join", roomCode);
            }

            await _eventDispatcher.DispatchEventsAsync(room);

            _logger.LogInformation("[GameRoomService] Player {PlayerId} joined room {RoomCode} successfully. Total players: {PlayerCount}",
                playerId, roomCode, room.PlayerCount);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error joining room {RoomCode} for player {PlayerId}: {Error}",
                roomCode, playerId, ex.Message);
            return Result.Failure($"Error joining room: {ex.Message}");
        }
    }

    public async Task<r> LeaveRoomAsync(string roomCode, PlayerId playerId)
    {
        try
        {
            _logger.LogInformation("[GameRoomService] Player {PlayerId} leaving room {RoomCode}", playerId, roomCode);

            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                _logger.LogInformation("[GameRoomService] Room {RoomCode} not found for leaving player {PlayerId}", roomCode, playerId);
                return Result.Success();
            }

            if (!room.IsPlayerInRoom(playerId))
            {
                _logger.LogInformation("[GameRoomService] Player {PlayerId} not in room {RoomCode}", playerId, roomCode);
                return Result.Success();
            }

            room.RemovePlayer(playerId);

            // CORREGIDO: Limpiar posición de asiento de forma segura
            if (_roomPlayerPositions.TryGetValue(roomCode, out var positions))
            {
                var removed = positions.Remove(playerId);
                if (removed)
                {
                    _logger.LogInformation("[GameRoomService] Removed seat position for player {PlayerId} in room {RoomCode}",
                        playerId, roomCode);
                }
            }

            if (room.PlayerCount == 0)
            {
                await _gameRoomRepository.DeleteAsync(room);
                _roomPlayerPositions.TryRemove(roomCode, out _);
                _logger.LogInformation("[GameRoomService] Room {RoomCode} deleted - no players remaining", roomCode);
            }
            else
            {
                // Manejo de concurrencia para actualización
                try
                {
                    await _gameRoomRepository.UpdateAsync(room);
                    await _eventDispatcher.DispatchEventsAsync(room);
                    _logger.LogInformation("[GameRoomService] Room {RoomCode} updated after player {PlayerId} left. Remaining players: {PlayerCount}",
                        roomCode, playerId, room.PlayerCount);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning("[GameRoomService] Concurrency conflict when player {PlayerId} left room {RoomCode}",
                        playerId, roomCode);
                    // No fallar, el jugador ya salió lógicamente
                }
            }

            _logger.LogInformation("[GameRoomService] Player {PlayerId} left room {RoomCode} successfully", playerId, roomCode);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error leaving room {RoomCode} for player {PlayerId}: {Error}",
                roomCode, playerId, ex.Message);
            return Result.Failure($"Error leaving room: {ex.Message}");
        }
    }

    public async Task<r> AddSpectatorAsync(string roomCode, PlayerId playerId, string spectatorName)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result.Failure("Sala no encontrada");
            }

            room.AddSpectator(playerId, spectatorName);

            try
            {
                await _gameRoomRepository.UpdateAsync(room);
                await _eventDispatcher.DispatchEventsAsync(room);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure("Conflicto de concurrencia. Intenta de nuevo.");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error adding spectator: {Error}", ex.Message);
            return Result.Failure($"Error adding spectator: {ex.Message}");
        }
    }

    public async Task<r> RemoveSpectatorAsync(string roomCode, PlayerId playerId)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result.Success();
            }

            room.RemoveSpectator(playerId);

            try
            {
                await _gameRoomRepository.UpdateAsync(room);
                await _eventDispatcher.DispatchEventsAsync(room);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("[GameRoomService] Concurrency conflict when removing spectator");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error removing spectator: {Error}", ex.Message);
            return Result.Failure($"Error removing spectator: {ex.Message}");
        }
    }

    #endregion

    #region Gestión de Asientos (Solo en Memoria)

    public async Task<r> JoinSeatAsync(string roomCode, PlayerId playerId, int position)
    {
        try
        {
            if (position < 0 || position > 5)
            {
                return Result.Failure("Posición inválida. Debe estar entre 0 y 5.");
            }

            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result.Failure("Sala no encontrada");
            }

            if (room.Status == RoomStatus.InProgress)
            {
                return Result.Failure("No puedes cambiar de asiento durante la partida");
            }

            // CORREGIDO: Verificación más robusta de pertenencia a sala
            var isPlayerInRoom = room.IsPlayerInRoom(playerId);
            _logger.LogInformation("[GameRoomService] JoinSeat validation - Player {PlayerId} in room: {IsInRoom}, Position: {Position}",
                playerId, isPlayerInRoom, position);

            if (!isPlayerInRoom)
            {
                // Log adicional para debug
                _logger.LogWarning("[GameRoomService] Player {PlayerId} attempting to join seat but not in room {RoomCode}. Current players: {PlayerIds}",
                    playerId, roomCode, string.Join(", ", room.Players.Select(p => p.PlayerId.Value)));
                return Result.Failure("Debes estar en la sala para unirte a un asiento");
            }

            // Obtener o crear diccionario de posiciones para esta sala
            if (!_roomPlayerPositions.TryGetValue(roomCode, out var positions))
            {
                positions = new Dictionary<PlayerId, int>();
                _roomPlayerPositions[roomCode] = positions;
                _logger.LogInformation("[GameRoomService] Created new position dictionary for room {RoomCode}", roomCode);
            }

            // Verificar si la posición ya está ocupada
            if (positions.ContainsValue(position))
            {
                _logger.LogInformation("[GameRoomService] Position {Position} already occupied in room {RoomCode}", position, roomCode);
                return Result.Failure("La posición ya está ocupada");
            }

            // Remover posición anterior del jugador si tenía una
            if (positions.Remove(playerId))
            {
                _logger.LogInformation("[GameRoomService] Removed previous position for player {PlayerId} in room {RoomCode}",
                    playerId, roomCode);
            }

            // Asignar nueva posición
            positions[playerId] = position;

            _logger.LogInformation("[GameRoomService] Player {PlayerId} joined seat {Position} in room {RoomCode}. Current positions: {Positions}",
                playerId, position, roomCode, string.Join(", ", positions.Select(kvp => $"{kvp.Key.Value.ToString()[..8]}->P{kvp.Value}")));

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error joining seat {Position} in room {RoomCode} for player {PlayerId}: {Error}",
                position, roomCode, playerId, ex.Message);
            return Result.Failure($"Error joining seat: {ex.Message}");
        }
    }

    public async Task<r> LeaveSeatAsync(string roomCode, PlayerId playerId)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result.Failure("Sala no encontrada");
            }

            if (room.Status == RoomStatus.InProgress)
            {
                return Result.Failure("No puedes salir del asiento durante la partida");
            }

            // Remover posición del jugador
            if (_roomPlayerPositions.TryGetValue(roomCode, out var positions))
            {
                var removed = positions.Remove(playerId);
                if (!removed)
                {
                    return Result.Failure("No estás sentado en esta sala");
                }

                _logger.LogInformation("[GameRoomService] Player {PlayerId} left seat in room {RoomCode}. Remaining positions: {Positions}",
                    playerId, roomCode, string.Join(", ", positions.Select(kvp => $"{kvp.Key.Value.ToString()[..8]}->P{kvp.Value}")));
            }
            else
            {
                return Result.Failure("No estás sentado en esta sala");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error leaving seat in room {RoomCode} for player {PlayerId}: {Error}",
                roomCode, playerId, ex.Message);
            return Result.Failure($"Error leaving seat: {ex.Message}");
        }
    }

    #endregion

    #region Control de Juego

    public async Task<r> StartGameAsync(string roomCode, PlayerId hostPlayerId)
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

            if (!room.BlackjackTableId.HasValue)
            {
                var table = BlackjackTable.Create($"Table for Room {roomCode}");
                await _tableRepository.AddAsync(table);
                room.BlackjackTableId = table.Id;
            }

            room.StartGame(room.BlackjackTableId.Value);

            try
            {
                await _gameRoomRepository.UpdateAsync(room);
                await _eventDispatcher.DispatchEventsAsync(room);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure("Conflicto de concurrencia al iniciar juego. Intenta de nuevo.");
            }

            _logger.LogInformation("[GameRoomService] Game started in room {RoomCode} with table {TableId}",
                roomCode, room.BlackjackTableId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error starting game: {Error}", ex.Message);
            return Result.Failure($"Error starting game: {ex.Message}");
        }
    }

    public async Task<r> NextTurnAsync(string roomCode)
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

            try
            {
                await _gameRoomRepository.UpdateAsync(room);
                await _eventDispatcher.DispatchEventsAsync(room);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure("Conflicto de concurrencia al avanzar turno.");
            }

            _logger.LogInformation("[GameRoomService] Turn advanced in room {RoomCode}", roomCode);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error advancing turn: {Error}", ex.Message);
            return Result.Failure($"Error advancing turn: {ex.Message}");
        }
    }

    public async Task<r> EndGameAsync(string roomCode)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result.Failure("Sala no encontrada");
            }

            room.EndGame();

            // CORREGIDO: Limpiar posiciones de asientos al terminar la partida
            if (_roomPlayerPositions.TryGetValue(roomCode, out var positions))
            {
                var clearedPositions = positions.Count;
                positions.Clear();
                _logger.LogInformation("[GameRoomService] Cleared {PositionCount} seat positions in room {RoomCode} after game end",
                    clearedPositions, roomCode);
            }

            try
            {
                await _gameRoomRepository.UpdateAsync(room);
                await _eventDispatcher.DispatchEventsAsync(room);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("[GameRoomService] Concurrency conflict when ending game");
            }

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

    public async Task<Result<List<int>>> GetAvailablePositionsAsync(string roomCode)
    {
        try
        {
            if (!_roomPlayerPositions.TryGetValue(roomCode, out var positions))
            {
                return Result<List<int>>.Success(Enumerable.Range(0, 6).ToList());
            }

            var occupiedPositions = positions.Values.ToHashSet();
            var availablePositions = Enumerable.Range(0, 6)
                .Where(pos => !occupiedPositions.Contains(pos))
                .ToList();

            return Result<List<int>>.Success(availablePositions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error getting available positions: {Error}", ex.Message);
            return Result<List<int>>.Failure($"Error getting available positions: {ex.Message}");
        }
    }

    public async Task<Result<bool>> IsPositionOccupiedAsync(string roomCode, int position)
    {
        try
        {
            if (!_roomPlayerPositions.TryGetValue(roomCode, out var positions))
            {
                return Result<bool>.Success(false);
            }

            var isOccupied = positions.ContainsValue(position);
            return Result<bool>.Success(isOccupied);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error checking position: {Error}", ex.Message);
            return Result<bool>.Failure($"Error checking position: {ex.Message}");
        }
    }

    public async Task<Result<int?>> GetPlayerPositionAsync(string roomCode, PlayerId playerId)
    {
        try
        {
            if (!_roomPlayerPositions.TryGetValue(roomCode, out var positions))
            {
                return Result<int?>.Success(null);
            }

            if (positions.TryGetValue(playerId, out var position))
            {
                return Result<int?>.Success(position);
            }

            return Result<int?>.Success(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error getting player position: {Error}", ex.Message);
            return Result<int?>.Failure($"Error getting player position: {ex.Message}");
        }
    }

    public async Task<Result<Dictionary<Guid, int>>> GetRoomPositionsAsync(string roomCode)
    {
        try
        {
            if (!_roomPlayerPositions.TryGetValue(roomCode, out var positions))
            {
                // CORREGIDO: Inicializar si no existe y retornar vacío
                _roomPlayerPositions[roomCode] = new Dictionary<PlayerId, int>();
                return Result<Dictionary<Guid, int>>.Success(new Dictionary<Guid, int>());
            }

            var result = positions.ToDictionary(kvp => kvp.Key.Value, kvp => kvp.Value);
            return Result<Dictionary<Guid, int>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error getting room positions: {Error}", ex.Message);
            return Result<Dictionary<Guid, int>>.Failure($"Error getting room positions: {ex.Message}");
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