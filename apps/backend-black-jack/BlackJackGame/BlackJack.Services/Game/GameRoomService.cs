


//development




using BlackJack.Data.Repositories.Game;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Betting;
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
    private readonly IPlayerRepository _playerRepository; // NUEVO: Para auto-betting
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly ILogger<GameRoomService> _logger;

    // Lock distribuido por TableId más robusto
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _tableLocks = new();

    public GameRoomService(
        IGameRoomRepository gameRoomRepository,
        IRoomPlayerRepository roomPlayerRepository,
        ITableRepository tableRepository,
        IPlayerRepository playerRepository, // NUEVO: Inyección de dependencia
        IDomainEventDispatcher eventDispatcher,
        ILogger<GameRoomService> logger)
    {
        _gameRoomRepository = gameRoomRepository;
        _roomPlayerRepository = roomPlayerRepository;
        _tableRepository = tableRepository;
        _playerRepository = playerRepository; // NUEVO
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }

    #region Gestión de Salas

    // CORREGIDO: CreateRoomAsync ahora crea Player entity antes de AddPlayer
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

            // PASO 1: Crear/obtener Player entity ANTES de crear GameRoom
            var player = await EnsurePlayerExistsAsync(hostPlayerId, $"Host-{hostPlayerId.Value.ToString()[..8]}");
            if (player == null)
            {
                return Result<GameRoom>.Failure("Error creando jugador");
            }

            // PASO 2: Crear GameRoom (SIN AddPlayer todavía)
            var gameRoom = GameRoom.Create(roomName, hostPlayerId, roomCode);

            if (blackjackTableId.HasValue)
            {
                gameRoom.BlackjackTableId = blackjackTableId.Value;
                _logger.LogInformation("[GameRoomService] Assigned BlackjackTableId {TableId} to room {RoomCode}",
                    blackjackTableId.Value, roomCode);
            }

            // PASO 3: Agregar player con PlayerEntityId correcto
            await AddPlayerToRoomAsync(gameRoom, hostPlayerId, player.Id, $"Host-{hostPlayerId.Value.ToString()[..8]}", false);

            // PASO 4: Guardar en base de datos
            // PASO 4: Guardar en base de datos
            await _gameRoomRepository.AddAsync(gameRoom);

            // TEMPORAL: Comentar para diagnosticar
            // await _eventDispatcher.DispatchEventsAsync(gameRoom);

            _logger.LogInformation("[GameRoomService] Room created successfully: {RoomCode}", roomCode);
            // await _eventDispatcher.DispatchEventsAsync(gameRoom);

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

    // CORREGIDO: JoinRoomAsync ahora crea Player entity antes de AddPlayer
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

            // NUEVO: Crear/obtener Player entity antes de AddPlayer
            var player = await EnsurePlayerExistsAsync(playerId, playerName);
            if (player == null)
            {
                return Result.Failure("Error creando jugador");
            }

            // NUEVO: Usar AddPlayerToRoomAsync en lugar de room.AddPlayer() directamente
            await AddPlayerToRoomAsync(room, playerId, player.Id, playerName, isViewer);

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

    // SOLUCIÓN CRÍTICA: LeaveRoomAsync completamente corregido para eliminar datos fantasma
    public async Task<r> LeaveRoomAsync(string roomCode, PlayerId playerId)
    {
        try
        {
            _logger.LogInformation("[GameRoomService] === LEAVE ROOM START ===");
            _logger.LogInformation("[GameRoomService] Player {PlayerId} leaving room {RoomCode}", playerId, roomCode);

            // PASO 1: CRÍTICO - Eliminar RoomPlayer de la base de datos PRIMERO
            _logger.LogInformation("[GameRoomService] Step 1: Removing RoomPlayer from database...");
            var roomPlayerRemoved = await _gameRoomRepository.RemoveRoomPlayerAsync(roomCode, playerId);

            if (roomPlayerRemoved)
            {
                _logger.LogInformation("[GameRoomService] ✅ RoomPlayer removed from database successfully");
            }
            else
            {
                _logger.LogWarning("[GameRoomService] ⚠️ RoomPlayer was not found in database or already removed");
            }

            // PASO 2: Liberar asiento si estaba sentado
            _logger.LogInformation("[GameRoomService] Step 2: Freeing seat if occupied...");
            var seatFreed = await _gameRoomRepository.FreeSeatAsync(roomCode, playerId);
            if (seatFreed)
            {
                _logger.LogInformation("[GameRoomService] ✅ Seat freed successfully");
            }

            // PASO 3: Obtener sala y verificar si existe
            _logger.LogInformation("[GameRoomService] Step 3: Loading room for domain operations...");
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                _logger.LogInformation("[GameRoomService] Room {RoomCode} no longer exists, cleanup complete", roomCode);
                return Result.Success();
            }

            // PASO 4: Operación de dominio (ahora segura porque ya eliminamos de BD)
            _logger.LogInformation("[GameRoomService] Step 4: Calling domain RemovePlayer...");
            var wasPlayerInRoom = room.IsPlayerInRoom(playerId);
            if (wasPlayerInRoom)
            {
                room.RemovePlayer(playerId);
                _logger.LogInformation("[GameRoomService] ✅ Player removed from domain room object");
            }
            else
            {
                _logger.LogInformation("[GameRoomService] Player was not in room domain object");
            }

            // PASO 5: Gestionar sala vacía o actualizar
            if (room.PlayerCount == 0)
            {
                _logger.LogInformation("[GameRoomService] Step 5: Room is now empty, deleting...");
                await _gameRoomRepository.DeleteAsync(room);
                _logger.LogInformation("[GameRoomService] ✅ Empty room deleted");
            }
            else
            {
                _logger.LogInformation("[GameRoomService] Step 5: Room still has {PlayerCount} players, updating...", room.PlayerCount);
                await _gameRoomRepository.UpdateAsync(room);
                await _eventDispatcher.DispatchEventsAsync(room);
                _logger.LogInformation("[GameRoomService] ✅ Room updated successfully");
            }

            _logger.LogInformation("[GameRoomService] === LEAVE ROOM COMPLETED SUCCESSFULLY ===");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] CRITICAL ERROR in LeaveRoomAsync - Player: {PlayerId}, Room: {RoomCode}",
                playerId, roomCode);

            // FALLBACK: En caso de error, intentar limpieza forzada
            try
            {
                _logger.LogWarning("[GameRoomService] Attempting forced cleanup for player {PlayerId}", playerId);
                await _gameRoomRepository.ForceCleanupPlayerFromAllRoomsAsync(playerId);
                _logger.LogInformation("[GameRoomService] ✅ Forced cleanup completed");
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "[GameRoomService] Even forced cleanup failed for player {PlayerId}", playerId);
            }

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

            var spectator = Spectator.Create(playerId, spectatorName, room.Id);
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

    #region NUEVOS: Métodos auxiliares para manejo de Player entities

    /// <summary>
    /// Crea o obtiene un Player entity existente para un PlayerId dado
    /// </summary>
    private async Task<Player?> EnsurePlayerExistsAsync(PlayerId playerId, string playerName, decimal initialBalance = 1000m)
    {
        try
        {
            _logger.LogInformation("[GameRoomService] Ensuring Player entity exists for PlayerId: {PlayerId}", playerId);

            // 1. Intentar obtener Player existente
            var existingPlayer = await _playerRepository.GetByPlayerIdAsync(playerId);
            if (existingPlayer != null)
            {
                _logger.LogInformation("[GameRoomService] Found existing Player entity with Id: {PlayerId}", existingPlayer.Id);
                return existingPlayer;
            }

            // 2. Crear nuevo Player si no existe
            _logger.LogInformation("[GameRoomService] Creating new Player entity for PlayerId: {PlayerId}", playerId);
            var newPlayer = Player.Create(playerId, playerName, initialBalance);
            await _playerRepository.AddAsync(newPlayer);

            _logger.LogInformation("[GameRoomService] Created new Player entity with Id: {PlayerId}", newPlayer.Id);
            return newPlayer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error ensuring Player entity exists for PlayerId: {PlayerId}", playerId);
            return null;
        }
    }

    /// <summary>
    /// Agrega un jugador a una sala con PlayerEntityId correcto
    /// </summary>
    private async Task AddPlayerToRoomAsync(GameRoom gameRoom, PlayerId playerId, Guid playerEntityId, string playerName, bool isViewer = false)
    {
        try
        {
            _logger.LogInformation("[GameRoomService] Adding player to room - PlayerId: {PlayerId}, PlayerEntityId: {PlayerEntityId}",
                playerId, playerEntityId);

            // 1. Llamar al método de dominio para agregar el jugador (esto crea RoomPlayer con PlayerEntityId = Guid.Empty)
            gameRoom.AddPlayer(playerId, playerName, isViewer);

            // 2. Obtener el RoomPlayer recién creado y asignar el PlayerEntityId correcto
            var roomPlayer = gameRoom.GetPlayer(playerId);
            if (roomPlayer != null)
            {
                // Asignar PlayerEntityId correcto directamente
                roomPlayer.PlayerEntityId = playerEntityId;
                roomPlayer.UpdatedAt = DateTime.UtcNow; // CORREGIDO: Usar UpdatedAt directamente

                _logger.LogInformation("[GameRoomService] Assigned PlayerEntityId {PlayerEntityId} to RoomPlayer for PlayerId: {PlayerId}",
                    playerEntityId, playerId);
            }
            else
            {
                _logger.LogError("[GameRoomService] Failed to find RoomPlayer after adding to GameRoom for PlayerId: {PlayerId}", playerId);
                throw new InvalidOperationException("Failed to add player to room");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error adding player to room - PlayerId: {PlayerId}", playerId);
            throw;
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

            // FIX: Persist host to first seated player or if current host isn't seated
            try
            {
                var seatPositions = await _gameRoomRepository.GetSeatPositionsAsync(roomCode);
                var seatedPlayerIds = seatPositions.Keys.ToHashSet();

                var roomEntity = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
                if (roomEntity != null)
                {
                    var hostGuid = roomEntity.HostPlayerId.Value;
                    var hostIsSeated = seatedPlayerIds.Contains(hostGuid);

                    if (!hostIsSeated || seatedPlayerIds.Count == 1)
                    {
                        typeof(GameRoom).GetProperty("HostPlayerId")!.SetValue(roomEntity, playerId);
                        await _gameRoomRepository.UpdateAsync(roomEntity);
                        _logger.LogInformation("[GameRoomService] Host set to player {PlayerId} for room {RoomCode}", playerId, roomCode);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GameRoomService] Host assignment after JoinSeat failed for room {RoomCode}", roomCode);
            }

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] CRITICAL ERROR in JoinSeatAsync for player {PlayerId} in room {RoomCode}",
                playerId, roomCode);

            // TEMPORAL: Mostrar error específico en lugar de genérico
            return Result<bool>.Failure($"Error: {ex.Message} - {ex.InnerException?.Message}");
        }
    }

    public async Task<Result<bool>> LeaveSeatAsync(string roomCode, PlayerId playerId)
    {
        try
        {
            // SOLUCIÓN: Verificación directa de sala existencia sin cargar Players
            var roomExists = await _gameRoomRepository.RoomCodeExistsAsync(roomCode);
            if (!roomExists)
            {
                return Result<bool>.Failure("Sala no encontrada");
            }

            // SOLUCIÓN: Verificación de membership sin cargar GameRoom completo
            var isPlayerInRoom = await _gameRoomRepository.IsPlayerInRoomAsync(playerId, roomCode);
            if (!isPlayerInRoom)
            {
                return Result<bool>.Failure("No estás en esta sala");
            }

            // OPERACIÓN PRINCIPAL: Liberar asiento (usa SQL directo - ya funciona)
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
                // Permitir al jugador iniciar si está sentado aunque HostPlayerId no esté actualizado aún
                var callerIsSeated = room.Players.Any(p => p.IsSeated && p.PlayerId.Value == hostPlayerId.Value);
                if (!callerIsSeated)
                {
                    return Result<bool>.Failure("Solo el host puede iniciar el juego");
                }
            }

            if (!room.CanStart)
            {
                return Result<bool>.Failure("La sala no puede iniciar el juego aún");
            }

            room.StartGame();
            try
            {
                await _gameRoomRepository.UpdateAsync(room);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GameRoomService] UpdateAsync failed; proceeding as started for room {RoomCode}", roomCode);
                // Proceed without failing; table round will be in progress and hubs will broadcast state
            }

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

    #region Auto-Betting

    /// <summary>
    /// Procesa las apuestas automáticas para todos los jugadores sentados en una sala
    /// </summary>
    public async Task<Result<AutoBetResult>> ProcessRoundAutoBetsAsync(string roomCode, bool removePlayersWithoutFunds = true)
    {
        try
        {
            _logger.LogInformation("[AutoBetting] === PROCESANDO APUESTAS AUTOMÁTICAS ===");
            _logger.LogInformation("[AutoBetting] Room: {RoomCode}, RemoveWithoutFunds: {RemoveFlag}",
                roomCode, removePlayersWithoutFunds);

            // 1. Validar sala
            var validationResult = await ValidateRoomForAutoBettingAsync(roomCode);
            if (!validationResult.IsSuccess)
            {
                return Result<AutoBetResult>.Failure(validationResult.Error);
            }

            var validation = validationResult.Value!;
            if (!validation.IsValid)
            {
                var errorMessages = string.Join(", ", validation.ErrorMessages);
                return Result<AutoBetResult>.Failure($"Validación fallida: {errorMessages}");
            }

            // 2. Obtener sala y jugadores sentados
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            var seatedPlayers = await _roomPlayerRepository.GetSeatedPlayersByRoomCodeAsync(roomCode);
            var seatedPlayerIds = seatedPlayers.Select(rp => rp.PlayerId).ToList();

            if (!seatedPlayerIds.Any())
            {
                _logger.LogInformation("[AutoBetting] No hay jugadores sentados en la sala {RoomCode}", roomCode);
                return Result<AutoBetResult>.Success(new AutoBetResult
                {
                    RoomCode = roomCode,
                    TotalPlayersProcessed = 0,
                    SuccessfulBets = 0,
                    FailedBets = 0,
                    PlayersRemovedFromSeats = 0,
                    TotalAmountProcessed = Money.Zero,
                    PlayerResults = new List<PlayerBetResult>(),
                    ProcessedAt = DateTime.UtcNow
                });
            }

            // 3. Obtener jugadores reales con sus balances
            var players = await _playerRepository.GetPlayersByIdsAsync(seatedPlayerIds);
            var playerDict = players.ToDictionary(p => p.PlayerId.Value, p => p);

            // 4. Procesar apuestas automáticas
            var playerResults = new List<PlayerBetResult>();
            var playersToRemove = new List<PlayerId>();
            var totalAmountProcessed = Money.Zero;
            int successfulBets = 0;
            int failedBets = 0;

            foreach (var seatedPlayer in seatedPlayers)
            {
                var playerId = seatedPlayer.PlayerId;

                if (!playerDict.TryGetValue(playerId.Value, out var player))
                {
                    _logger.LogWarning("[AutoBetting] Jugador {PlayerId} sentado pero no encontrado en Player table", playerId);
                    playerResults.Add(new PlayerBetResult
                    {
                        PlayerId = playerId,
                        PlayerName = seatedPlayer.Name,
                        SeatPosition = seatedPlayer.GetSeatPosition(),
                        Status = BetStatus.Failed,
                        OriginalBalance = Money.Zero,
                        NewBalance = Money.Zero,
                        BetAmount = Money.Zero,
                        ErrorMessage = "Jugador no encontrado"
                    });
                    continue;
                }

                var betAmount = room!.MinBetPerRound;
                var originalBalance = player.Balance;

                // Verificar fondos suficientes
                if (!player.CanAffordBet(betAmount))
                {
                    _logger.LogWarning("[AutoBetting] Jugador {PlayerId} sin fondos suficientes. Balance: {Balance}, Requerido: {Required}",
                        playerId, originalBalance, betAmount);

                    var result = new PlayerBetResult
                    {
                        PlayerId = playerId,
                        PlayerName = seatedPlayer.Name,
                        SeatPosition = seatedPlayer.GetSeatPosition(),
                        Status = BetStatus.InsufficientFunds,
                        OriginalBalance = originalBalance,
                        NewBalance = originalBalance,
                        BetAmount = betAmount,
                        ErrorMessage = "Fondos insuficientes"
                    };

                    if (removePlayersWithoutFunds)
                    {
                        result.Status = BetStatus.RemovedFromSeat;
                        playersToRemove.Add(playerId);
                    }

                    playerResults.Add(result);
                    failedBets++;
                    continue;
                }

                // Intentar colocar apuesta
                var bet = Bet.Create(betAmount);
                var betPlaced = await _playerRepository.PlaceBetAsync(playerId, bet);

                if (betPlaced)
                {
                    var newBalance = originalBalance.Subtract(betAmount);
                    totalAmountProcessed = totalAmountProcessed.Add(betAmount);

                    playerResults.Add(new PlayerBetResult
                    {
                        PlayerId = playerId,
                        PlayerName = seatedPlayer.Name,
                        SeatPosition = seatedPlayer.GetSeatPosition(),
                        Status = BetStatus.BetDeducted,
                        OriginalBalance = originalBalance,
                        NewBalance = newBalance,
                        BetAmount = betAmount,
                        ErrorMessage = null
                    });

                    successfulBets++;
                    _logger.LogInformation("[AutoBetting] ✅ Apuesta colocada para {PlayerId}: {Amount}",
                        playerId, betAmount);
                }
                else
                {
                    playerResults.Add(new PlayerBetResult
                    {
                        PlayerId = playerId,
                        PlayerName = seatedPlayer.Name,
                        SeatPosition = seatedPlayer.GetSeatPosition(),
                        Status = BetStatus.Failed,
                        OriginalBalance = originalBalance,
                        NewBalance = originalBalance,
                        BetAmount = betAmount,
                        ErrorMessage = "Error al procesar apuesta"
                    });

                    failedBets++;
                    _logger.LogError("[AutoBetting] ❌ Error al colocar apuesta para {PlayerId}", playerId);
                }
            }

            // 5. Remover jugadores sin fondos de sus asientos
            int playersRemoved = 0;
            if (removePlayersWithoutFunds && playersToRemove.Any())
            {
                foreach (var playerId in playersToRemove)
                {
                    var removeResult = await LeaveSeatAsync(roomCode, playerId);
                    if (removeResult.IsSuccess)
                    {
                        playersRemoved++;
                        _logger.LogInformation("[AutoBetting] Jugador {PlayerId} removido del asiento por fondos insuficientes", playerId);
                    }
                }
            }

            // 6. Crear resultado final
            var autoBetResult = new AutoBetResult
            {
                RoomCode = roomCode,
                TotalPlayersProcessed = seatedPlayers.Count,
                SuccessfulBets = successfulBets,
                FailedBets = failedBets,
                PlayersRemovedFromSeats = playersRemoved,
                TotalAmountProcessed = totalAmountProcessed,
                PlayerResults = playerResults,
                ProcessedAt = DateTime.UtcNow
            };

            _logger.LogInformation("[AutoBetting] === PROCESAMIENTO COMPLETADO ===");
            _logger.LogInformation("[AutoBetting] Exitosas: {Success}, Fallidas: {Failed}, Removidos: {Removed}, Total: {Total}",
                successfulBets, failedBets, playersRemoved, totalAmountProcessed);

            return Result<AutoBetResult>.Success(autoBetResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AutoBetting] Error crítico procesando apuestas automáticas en sala {RoomCode}", roomCode);
            return Result<AutoBetResult>.Failure($"Error procesando apuestas automáticas: {ex.Message}");
        }
    }

    /// <summary>
    /// Valida si una sala puede procesar apuestas automáticas
    /// </summary>
    public async Task<Result<AutoBetValidation>> ValidateRoomForAutoBettingAsync(string roomCode)
    {
        try
        {
            var validation = new AutoBetValidation
            {
                RoomCode = roomCode,
                IsValid = true,
                ErrorMessages = new List<string>()
            };

            // 1. Verificar que la sala existe
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                validation.IsValid = false;
                validation.ErrorMessages.Add("Sala no encontrada");
                return Result<AutoBetValidation>.Success(validation);
            }

            // 2. Verificar MinBetPerRound configurado
            if (room.MinBetPerRound == null || room.MinBetPerRound.Amount <= 0)
            {
                validation.IsValid = false;
                validation.ErrorMessages.Add("Apuesta mínima por ronda no configurada");
            }

            // 3. Verificar que hay jugadores sentados
            var seatedPlayersCount = await _roomPlayerRepository.GetSeatedPlayersCountAsync(roomCode);
            if (seatedPlayersCount == 0)
            {
                validation.IsValid = false;
                validation.ErrorMessages.Add("No hay jugadores sentados en la sala");
            }

            // 4. Verificar estado de la sala (opcional - puede procesarse en cualquier estado)
            validation.RoomStatus = room.Status;
            validation.SeatedPlayersCount = seatedPlayersCount;
            validation.MinBetPerRound = room.MinBetPerRound;

            return Result<AutoBetValidation>.Success(validation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AutoBetting] Error validando sala {RoomCode} para auto-betting", roomCode);
            return Result<AutoBetValidation>.Failure($"Error en validación: {ex.Message}");
        }
    }

    /// <summary>
    /// Calcula estadísticas de apuestas automáticas para una sala
    /// </summary>
    public async Task<Result<AutoBetStatistics>> CalculateAutoBetStatisticsAsync(string roomCode)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result<AutoBetStatistics>.Failure("Sala no encontrada");
            }

            var seatedPlayers = await _roomPlayerRepository.GetSeatedPlayersByRoomCodeAsync(roomCode);
            var seatedPlayerIds = seatedPlayers.Select(rp => rp.PlayerId).ToList();

            var statistics = new AutoBetStatistics
            {
                RoomCode = roomCode,
                MinBetPerRound = room.MinBetPerRound,
                SeatedPlayersCount = seatedPlayers.Count,
                TotalBetPerRound = new Money(room.MinBetPerRound.Amount * seatedPlayers.Count),
                CalculatedAt = DateTime.UtcNow
            };

            if (seatedPlayerIds.Any())
            {
                var players = await _playerRepository.GetPlayersByIdsAsync(seatedPlayerIds);
                var playersWithSufficientFunds = players.Where(p => p.CanAffordBet(room.MinBetPerRound)).ToList();
                var playersWithInsufficientFunds = players.Where(p => !p.CanAffordBet(room.MinBetPerRound)).ToList();

                statistics.PlayersWithSufficientFunds = playersWithSufficientFunds.Count;
                statistics.PlayersWithInsufficientFunds = playersWithInsufficientFunds.Count;
                statistics.TotalAvailableFunds = new Money(players.Sum(p => p.Balance.Amount));
                statistics.ExpectedSuccessfulBets = playersWithSufficientFunds.Count;
                statistics.ExpectedTotalDeduction = new Money(room.MinBetPerRound.Amount * playersWithSufficientFunds.Count);

                // Detalles por jugador
                statistics.PlayerDetails = players.Select(p => new PlayerAutoBetDetail
                {
                    PlayerId = p.PlayerId,
                    PlayerName = p.Name,
                    CurrentBalance = p.Balance,
                    CanAffordBet = p.CanAffordBet(room.MinBetPerRound),
                    BalanceAfterBet = p.CanAffordBet(room.MinBetPerRound)
                        ? p.Balance.Subtract(room.MinBetPerRound)
                        : p.Balance
                }).ToList();
            }

            return Result<AutoBetStatistics>.Success(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AutoBetting] Error calculando estadísticas para sala {RoomCode}", roomCode);
            return Result<AutoBetStatistics>.Failure($"Error calculando estadísticas: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifica si un jugador puede costear la apuesta automática de una sala
    /// </summary>
    public async Task<Result<bool>> CanPlayerAffordAutoBetAsync(string roomCode, PlayerId playerId)
    {
        try
        {
            var room = await _gameRoomRepository.GetRoomWithPlayersAsync(roomCode);
            if (room == null)
            {
                return Result<bool>.Failure("Sala no encontrada");
            }

            var player = await _playerRepository.GetByPlayerIdAsync(playerId);
            if (player == null)
            {
                return Result<bool>.Failure("Jugador no encontrado");
            }

            var canAfford = player.CanAffordBet(room.MinBetPerRound);
            return Result<bool>.Success(canAfford);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AutoBetting] Error verificando fondos del jugador {PlayerId} en sala {RoomCode}",
                playerId, roomCode);
            return Result<bool>.Failure($"Error verificando fondos: {ex.Message}");
        }
    }

    #endregion

    #region NUEVO: Métodos de Diagnóstico y Limpieza

    /// <summary>
    /// MÉTODO DE DIAGNÓSTICO: Obtiene información sobre registros huérfanos de un jugador
    /// </summary>
    public async Task<Result<List<string>>> DiagnosePlayerOrphanRoomsAsync(PlayerId playerId)
    {
        try
        {
            var orphanRooms = await _gameRoomRepository.GetPlayerOrphanRoomsAsync(playerId);
            _logger.LogInformation("[GameRoomService] Player {PlayerId} has orphan records in {Count} rooms: {Rooms}",
                playerId, orphanRooms.Count, string.Join(", ", orphanRooms));

            return Result<List<string>>.Success(orphanRooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error diagnosing orphan rooms for player {PlayerId}", playerId);
            return Result<List<string>>.Failure($"Error en diagnóstico: {ex.Message}");
        }
    }

    /// <summary>
    /// MÉTODO DE EMERGENCIA: Limpia completamente un jugador de TODAS las salas
    /// </summary>
    public async Task<Result<int>> ForceCleanupPlayerAsync(PlayerId playerId)
    {
        try
        {
            _logger.LogWarning("[GameRoomService] === FORCE CLEANUP INITIATED ===");
            _logger.LogWarning("[GameRoomService] Forcing cleanup for player {PlayerId}", playerId);

            var affectedRows = await _gameRoomRepository.ForceCleanupPlayerFromAllRoomsAsync(playerId);

            _logger.LogInformation("[GameRoomService] Force cleanup completed: {AffectedRows} records removed for player {PlayerId}",
                affectedRows, playerId);

            return Result<int>.Success(affectedRows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error in force cleanup for player {PlayerId}", playerId);
            return Result<int>.Failure($"Error en limpieza forzada: {ex.Message}");
        }
    }

    /// <summary>
    /// MÉTODO DE MANTENIMIENTO: Limpia salas vacías automáticamente
    /// </summary>
    public async Task<Result<int>> CleanupEmptyRoomsAsync()
    {
        try
        {
            var removedRooms = await _gameRoomRepository.CleanupEmptyRoomsAsync();
            _logger.LogInformation("[GameRoomService] Cleanup completed: {RemovedRooms} empty rooms removed", removedRooms);

            return Result<int>.Success(removedRooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomService] Error cleaning up empty rooms");
            return Result<int>.Failure($"Error en limpieza de salas vacías: {ex.Message}");
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

#region Auto-Betting Models

/// <summary>
/// Resultado del procesamiento de apuestas automáticas
/// </summary>
public class AutoBetResult
{
    public string RoomCode { get; set; } = string.Empty;
    public int TotalPlayersProcessed { get; set; }
    public int SuccessfulBets { get; set; }
    public int FailedBets { get; set; }
    public int PlayersRemovedFromSeats { get; set; }
    public Money TotalAmountProcessed { get; set; } = Money.Zero;
    public List<PlayerBetResult> PlayerResults { get; set; } = new();
    public DateTime ProcessedAt { get; set; }

    public bool HasErrors => FailedBets > 0;
    public decimal SuccessRate => TotalPlayersProcessed > 0 ? (decimal)SuccessfulBets / TotalPlayersProcessed : 0;
}

/// <summary>
/// Resultado de apuesta para un jugador individual
/// </summary>
public class PlayerBetResult
{
    public PlayerId PlayerId { get; set; } = default!;
    public string PlayerName { get; set; } = string.Empty;
    public int SeatPosition { get; set; }
    public BetStatus Status { get; set; }
    public Money OriginalBalance { get; set; } = Money.Zero;
    public Money NewBalance { get; set; } = Money.Zero;
    public Money BetAmount { get; set; } = Money.Zero;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Estado del resultado de una apuesta
/// </summary>
public enum BetStatus
{
    BetDeducted,        // Apuesta colocada exitosamente
    InsufficientFunds,  // Fondos insuficientes
    RemovedFromSeat,    // Removido del asiento por fondos insuficientes  
    Failed              // Error general
}

/// <summary>
/// Validación de sala para auto-betting
/// </summary>
public class AutoBetValidation
{
    public string RoomCode { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public RoomStatus RoomStatus { get; set; }
    public int SeatedPlayersCount { get; set; }
    public Money MinBetPerRound { get; set; } = Money.Zero;
}

/// <summary>
/// Estadísticas de auto-betting para una sala
/// </summary>
public class AutoBetStatistics
{
    public string RoomCode { get; set; } = string.Empty;
    public Money MinBetPerRound { get; set; } = Money.Zero;
    public int SeatedPlayersCount { get; set; }
    public Money TotalBetPerRound { get; set; } = Money.Zero;
    public int PlayersWithSufficientFunds { get; set; }
    public int PlayersWithInsufficientFunds { get; set; }
    public Money TotalAvailableFunds { get; set; } = Money.Zero;
    public int ExpectedSuccessfulBets { get; set; }
    public Money ExpectedTotalDeduction { get; set; } = Money.Zero;
    public List<PlayerAutoBetDetail> PlayerDetails { get; set; } = new();
    public DateTime CalculatedAt { get; set; }
}

/// <summary>
/// Detalle de auto-betting por jugador
/// </summary>
public class PlayerAutoBetDetail
{
    public PlayerId PlayerId { get; set; } = default!;
    public string PlayerName { get; set; } = string.Empty;
    public Money CurrentBalance { get; set; } = Money.Zero;
    public bool CanAffordBet { get; set; }
    public Money BalanceAfterBet { get; set; } = Money.Zero;
}

#endregion

#region Existing Models

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

#endregion