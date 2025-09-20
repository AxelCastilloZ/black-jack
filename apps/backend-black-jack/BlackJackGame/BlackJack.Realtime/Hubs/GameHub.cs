// BlackJack.Realtime/Hubs/GameHub.cs - CORREGIDO: Grupos de SignalR DESPUÉS de verificar éxito
using BlackJack.Domain.Enums;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Realtime.Models;
using BlackJack.Services.Common;
using BlackJack.Services.Game;
using BlackJack.Realtime.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlackJack.Realtime.Hubs;

[AllowAnonymous]
public class GameHub : BaseHub
{
    private readonly IGameRoomService _gameRoomService;
    private readonly IGameService _gameService;
    private readonly IConnectionManager _connectionManager;

    public GameHub(
        IGameRoomService gameRoomService,
        IGameService gameService,
        IConnectionManager connectionManager,
        ILogger<GameHub> logger) : base(logger)
    {
        _gameRoomService = gameRoomService;
        _gameService = gameService;
        _connectionManager = connectionManager;
    }

    #region Connection Management

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        var playerId = GetCurrentPlayerId();
        var userName = GetCurrentUserName();

        _logger.LogInformation("[GameHub] Player {PlayerId} ({UserName}) connected with ConnectionId {ConnectionId}",
            playerId, userName, Context.ConnectionId);

        // NUEVO: Registrar conexión en ConnectionManager
        if (playerId != null && userName != null)
        {
            await _connectionManager.AddConnectionAsync(Context.ConnectionId, playerId, userName);

            // NUEVO: Verificar reconexión automática
            await HandleAutoReconnectionAsync(playerId);
        }

        await SendSuccessAsync("Conectado exitosamente al hub de juego");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var playerId = GetCurrentPlayerId();

        _logger.LogInformation("[GameHub] Player {PlayerId} disconnecting from ConnectionId {ConnectionId}",
            playerId, Context.ConnectionId);

        // NUEVO: Guardar información de reconexión antes de desconectar
        if (playerId != null)
        {
            await SaveReconnectionInfoAsync(playerId);
        }

        // Remover del ConnectionManager
        await _connectionManager.RemoveConnectionAsync(Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// NUEVO: Maneja la reconexión automática del jugador a su sala anterior
    /// </summary>
    private async Task HandleAutoReconnectionAsync(PlayerId playerId)
    {
        try
        {
            _logger.LogInformation("[GameHub] === AUTO RECONNECTION CHECK for player {PlayerId} ===", playerId);

            // Verificar si el jugador tiene información de reconexión
            var reconnectionInfo = await _connectionManager.GetReconnectionInfoAsync(playerId);
            if (reconnectionInfo != null && !string.IsNullOrEmpty(reconnectionInfo.LastRoomCode))
            {
                _logger.LogInformation("[GameHub] Found reconnection info for player {PlayerId} - Last room: {RoomCode}",
                    playerId, reconnectionInfo.LastRoomCode);

                // Verificar si la sala aún existe y el jugador sigue siendo miembro
                var roomResult = await _gameRoomService.GetRoomAsync(reconnectionInfo.LastRoomCode);
                if (roomResult.IsSuccess)
                {
                    var room = roomResult.Value!;

                    if (room.IsPlayerInRoom(playerId))
                    {
                        _logger.LogInformation("[GameHub] AUTO-RECONNECTING player {PlayerId} to room {RoomCode}",
                            playerId, reconnectionInfo.LastRoomCode);

                        // Unirse automáticamente a los grupos de SignalR
                        var roomGroupName = HubMethodNames.Groups.GetRoomGroup(reconnectionInfo.LastRoomCode);
                        await JoinGroupAsync(roomGroupName);
                        await _connectionManager.AddToGroupAsync(Context.ConnectionId, roomGroupName);

                        // También unirse al grupo de la tabla si existe
                        if (room.BlackjackTableId.HasValue)
                        {
                            var tableGroupName = $"Table_{room.BlackjackTableId.Value}";
                            await JoinGroupAsync(tableGroupName);
                            await _connectionManager.AddToGroupAsync(Context.ConnectionId, tableGroupName);
                        }

                        // Enviar estado actualizado de la sala al cliente reconectado
                        var roomInfo = await MapToRoomInfoAsync(room);
                        await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.RoomJoined, roomInfo);

                        // Limpiar información de reconexión (exitosa)
                        await _connectionManager.ClearReconnectionInfoAsync(playerId);

                        _logger.LogInformation("[GameHub] ✅ Successfully auto-reconnected player {PlayerId} to room {RoomCode}",
                            playerId, reconnectionInfo.LastRoomCode);
                    }
                    else
                    {
                        _logger.LogInformation("[GameHub] Player {PlayerId} no longer member of room {RoomCode} - clearing reconnection info",
                            playerId, reconnectionInfo.LastRoomCode);
                        await _connectionManager.ClearReconnectionInfoAsync(playerId);
                    }
                }
                else
                {
                    _logger.LogInformation("[GameHub] Room {RoomCode} no longer exists - clearing reconnection info for player {PlayerId}",
                        reconnectionInfo.LastRoomCode, playerId);
                    await _connectionManager.ClearReconnectionInfoAsync(playerId);
                }
            }
            else
            {
                _logger.LogInformation("[GameHub] No reconnection info found for player {PlayerId}", playerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameHub] Error during auto-reconnection for player {PlayerId}: {Error}",
                playerId, ex.Message);
        }
    }

    /// <summary>
    /// NUEVO: Guarda información para reconexión posterior
    /// </summary>
    private async Task SaveReconnectionInfoAsync(PlayerId playerId)
    {
        try
        {
            // Obtener la sala actual del jugador
            var currentRoomResult = await _gameRoomService.GetPlayerCurrentRoomCodeAsync(playerId);
            if (currentRoomResult.IsSuccess && !string.IsNullOrEmpty(currentRoomResult.Value))
            {
                await _connectionManager.SaveReconnectionInfoAsync(playerId, currentRoomResult.Value);

                _logger.LogInformation("[GameHub] Saved reconnection info for player {PlayerId} in room {RoomCode}",
                    playerId, currentRoomResult.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameHub] Error saving reconnection info for player {PlayerId}: {Error}",
                playerId, ex.Message);
        }
    }

    #endregion

    #region Room Management

    public async Task CreateRoom(CreateRoomRequest request)
    {
        try
        {
            _logger.LogInformation("[GameHub] === CreateRoom STARTED ===");
            _logger.LogInformation("[GameHub] RoomName: {RoomName}", request.RoomName);

            if (!IsAuthenticated())
            {
                _logger.LogWarning("[GameHub] CreateRoom - Authentication failed");
                await SendErrorAsync("Debes estar autenticado para crear una sala");
                return;
            }

            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                _logger.LogWarning("[GameHub] CreateRoom - PlayerId is null");
                await SendErrorAsync("Error de autenticación");
                return;
            }

            if (!ValidateInput(request.RoomName, nameof(request.RoomName), 50))
            {
                _logger.LogWarning("[GameHub] CreateRoom - Invalid room name");
                await SendErrorAsync("Nombre de sala inválido");
                return;
            }

            _logger.LogInformation("[GameHub] Creating room {RoomName} for player {PlayerId}",
                request.RoomName, playerId);

            var result = await _gameRoomService.CreateRoomAsync(request.RoomName, playerId);

            if (result.IsSuccess)
            {
                var room = result.Value!;

                // Unirse a grupos de SignalR
                var roomGroupName = HubMethodNames.Groups.GetRoomGroup(room.RoomCode);
                await JoinGroupAsync(roomGroupName);
                await _connectionManager.AddToGroupAsync(Context.ConnectionId, roomGroupName);

                var roomInfo = await MapToRoomInfoAsync(room);
                await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.RoomCreated, roomInfo);

                _logger.LogInformation("[GameHub] Room {RoomCode} created successfully", room.RoomCode);
            }
            else
            {
                _logger.LogError("[GameHub] CreateRoom failed: {Error}", result.Error);
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameHub] EXCEPTION in CreateRoom");
            await HandleExceptionAsync(ex, "CreateRoom");
        }
    }

    public async Task JoinOrCreateRoomForTable(string tableId, string playerName)
    {
        try
        {
            _logger.LogInformation("[GameHub] ================================================");
            _logger.LogInformation("[GameHub] === JoinOrCreateRoomForTable STARTED ===");
            _logger.LogInformation("[GameHub] ================================================");
            _logger.LogInformation("[GameHub] TableId: {TableId}", tableId);
            _logger.LogInformation("[GameHub] PlayerName: {PlayerName}", playerName);
            _logger.LogInformation("[GameHub] ConnectionId: {ConnectionId}", Context.ConnectionId);

            if (!IsAuthenticated())
            {
                _logger.LogWarning("[GameHub] JoinOrCreateRoomForTable - Authentication failed");
                await SendErrorAsync("Debes estar autenticado");
                return;
            }

            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                _logger.LogWarning("[GameHub] JoinOrCreateRoomForTable - PlayerId is null");
                await SendErrorAsync("Error de autenticación");
                return;
            }

            _logger.LogInformation("[GameHub] PlayerId validated: {PlayerId}", playerId);

            if (!ValidateInput(tableId, nameof(tableId)) ||
                !ValidateInput(playerName, nameof(playerName), 30))
            {
                _logger.LogWarning("[GameHub] JoinOrCreateRoomForTable - Invalid input data");
                await SendErrorAsync("Datos inválidos");
                return;
            }

            _logger.LogInformation("[GameHub] Input validation passed");
            _logger.LogInformation("[GameHub] Calling GameRoomService.JoinOrCreateRoomForTableAsync...");

            // CORREGIDO: Primero ejecutar la lógica de unión/creación
            var result = await _gameRoomService.JoinOrCreateRoomForTableAsync(tableId, playerId, playerName);

            if (!result.IsSuccess)
            {
                _logger.LogError("[GameHub] JoinOrCreateRoomForTableAsync FAILED: {Error}", result.Error);
                await SendErrorAsync(result.Error);
                return;
            }

            var room = result.Value!;
            _logger.LogInformation("[GameHub] Service SUCCESS - Room: {RoomCode} for table {TableId}",
                room.RoomCode, tableId);

            // CORREGIDO: Solo unirse a grupos de SignalR DESPUÉS de confirmar éxito
            var tableGroupName = $"Table_{tableId}";
            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(room.RoomCode);

            _logger.LogInformation("[GameHub] Joining SignalR groups - Table: {TableGroup}, Room: {RoomGroup}",
                tableGroupName, roomGroupName);

            await JoinGroupAsync(tableGroupName);
            await JoinGroupAsync(roomGroupName);

            // NUEVO: Registrar en ConnectionManager para reconexión
            await _connectionManager.AddToGroupAsync(Context.ConnectionId, tableGroupName);
            await _connectionManager.AddToGroupAsync(Context.ConnectionId, roomGroupName);

            // Obtener información ACTUALIZADA de la sala después de join
            var roomInfoResult = await _gameRoomService.GetRoomAsync(room.RoomCode);
            if (roomInfoResult.IsSuccess)
            {
                var roomInfo = await MapToRoomInfoAsync(roomInfoResult.Value!);

                // Determinar si el usuario creó la sala o se unió a una existente
                var isNewRoom = room.HostPlayerId == playerId;
                var eventMethod = isNewRoom ? HubMethodNames.ServerMethods.RoomCreated : HubMethodNames.ServerMethods.RoomJoined;

                _logger.LogInformation("[GameHub] Sending {EventMethod} event to caller (isNewRoom: {IsNewRoom})", eventMethod, isNewRoom);
                await Clients.Caller.SendAsync(eventMethod, roomInfo);

                // Solo notificar si se unió a sala existente
                if (!isNewRoom)
                {
                    _logger.LogInformation("[GameHub] Notifying other users about player join");

                    var playerJoinedEvent = new PlayerJoinedEventModel(
                        RoomCode: room.RoomCode,
                        PlayerId: playerId.Value,
                        PlayerName: playerName,
                        Position: -1,
                        TotalPlayers: roomInfo.PlayerCount,
                        Timestamp: DateTime.UtcNow
                    );

                    await Clients.OthersInGroup(roomGroupName)
                        .SendAsync(HubMethodNames.ServerMethods.PlayerJoined, playerJoinedEvent);

                    await Clients.OthersInGroup(roomGroupName)
                        .SendAsync(HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);
                }

                _logger.LogInformation("[GameHub] Successfully {Action} room {RoomCode} for table {TableId}. Total players: {PlayerCount}",
                    isNewRoom ? "created" : "joined", room.RoomCode, tableId, roomInfo.PlayerCount);
            }
            else
            {
                _logger.LogError("[GameHub] Failed to get updated room info: {Error}", roomInfoResult.Error);

                // CORREGIDO: Si falla, remover de grupos de SignalR
                await LeaveGroupAsync(roomGroupName);
                await LeaveGroupAsync(tableGroupName);
                await _connectionManager.RemoveFromGroupAsync(Context.ConnectionId, roomGroupName);
                await _connectionManager.RemoveFromGroupAsync(Context.ConnectionId, tableGroupName);

                await SendErrorAsync("Error obteniendo información de la sala");
            }

            _logger.LogInformation("[GameHub] ================================================");
            _logger.LogInformation("[GameHub] === JoinOrCreateRoomForTable COMPLETED ===");
            _logger.LogInformation("[GameHub] ================================================");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameHub] CRITICAL EXCEPTION in JoinOrCreateRoomForTable");
            _logger.LogError(ex, "[GameHub] Exception details - Message: {Message}, StackTrace: {StackTrace}",
                ex.Message, ex.StackTrace);
            await HandleExceptionAsync(ex, "JoinOrCreateRoomForTable");
        }
    }

    public async Task JoinRoom(JoinRoomRequest request)
    {
        try
        {
            _logger.LogInformation("[GameHub] === JoinRoom STARTED ===");
            _logger.LogInformation("[GameHub] RoomCode: {RoomCode}, PlayerName: {PlayerName}",
                request.RoomCode, request.PlayerName);

            if (!IsAuthenticated())
            {
                await SendErrorAsync("Debes estar autenticado para unirte a una sala");
                return;
            }

            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

            if (!ValidateInput(request.RoomCode, nameof(request.RoomCode)) ||
                !ValidateInput(request.PlayerName, nameof(request.PlayerName), 30))
            {
                await SendErrorAsync("Datos de entrada inválidos");
                return;
            }

            _logger.LogInformation("[GameHub] Player {PlayerId} joining room {RoomCode}",
                playerId, request.RoomCode);

            var roomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync("Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;

            // CORREGIDO: Primero ejecutar JoinRoom, DESPUÉS unirse a grupos
            var joinResult = await _gameRoomService.JoinRoomAsync(request.RoomCode, playerId, request.PlayerName);

            if (!joinResult.IsSuccess)
            {
                _logger.LogError("[GameHub] JoinRoom failed: {Error}", joinResult.Error);
                await SendErrorAsync(joinResult.Error);
                return;
            }

            // Solo después de éxito, unirse a grupos de SignalR
            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(request.RoomCode);
            await JoinGroupAsync(roomGroupName);
            await _connectionManager.AddToGroupAsync(Context.ConnectionId, roomGroupName);

            // También unirse al grupo de la tabla si existe
            if (room.BlackjackTableId.HasValue)
            {
                var tableGroupName = $"Table_{room.BlackjackTableId.Value}";
                await JoinGroupAsync(tableGroupName);
                await _connectionManager.AddToGroupAsync(Context.ConnectionId, tableGroupName);
                _logger.LogInformation("[GameHub] Also joined table group: {TableGroupName}", tableGroupName);
            }

            var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
            if (updatedRoomResult.IsSuccess)
            {
                var roomInfo = await MapToRoomInfoAsync(updatedRoomResult.Value!);

                await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.RoomJoined, roomInfo);

                await Clients.OthersInGroup(roomGroupName)
                    .SendAsync(HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);

                var playerJoinedEvent = new PlayerJoinedEventModel(
                    RoomCode: request.RoomCode,
                    PlayerId: playerId.Value,
                    PlayerName: request.PlayerName,
                    Position: -1,
                    TotalPlayers: roomInfo.PlayerCount,
                    Timestamp: DateTime.UtcNow
                );

                await Clients.OthersInGroup(roomGroupName)
                    .SendAsync(HubMethodNames.ServerMethods.PlayerJoined, playerJoinedEvent);

                _logger.LogInformation("[GameHub] Player {PlayerId} joined room {RoomCode} successfully",
                    playerId, request.RoomCode);
            }
            else
            {
                // Si falla obtener info actualizada, remover de grupos
                await LeaveGroupAsync(roomGroupName);
                await _connectionManager.RemoveFromGroupAsync(Context.ConnectionId, roomGroupName);

                if (room.BlackjackTableId.HasValue)
                {
                    var tableGroupName = $"Table_{room.BlackjackTableId.Value}";
                    await LeaveGroupAsync(tableGroupName);
                    await _connectionManager.RemoveFromGroupAsync(Context.ConnectionId, tableGroupName);
                }

                await SendErrorAsync("Error obteniendo información actualizada de la sala");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameHub] EXCEPTION in JoinRoom");
            await HandleExceptionAsync(ex, "JoinRoom");
        }
    }

    public async Task JoinSeat(JoinSeatRequest request)
    {
        try
        {
            _logger.LogInformation("[GameHub] ===== JoinSeat STARTED =====");
            _logger.LogInformation("[GameHub] RoomCode: {RoomCode}, Position: {Position}",
                request.RoomCode, request.Position);

            if (!IsAuthenticated())
            {
                await SendErrorAsync("Debes estar autenticado");
                return;
            }

            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

            if (!ValidateInput(request.RoomCode, nameof(request.RoomCode)) ||
                request.Position < 0 || request.Position > 5)
            {
                await SendErrorAsync("Datos inválidos");
                return;
            }

            _logger.LogInformation("[GameHub] Player {PlayerId} attempting to join seat {Position} in room {RoomCode}",
                playerId, request.Position, request.RoomCode);

            var result = await _gameRoomService.JoinSeatAsync(request.RoomCode, playerId, request.Position);

            if (result.IsSuccess)
            {
                _logger.LogInformation("[GameHub] JoinSeat SUCCESS - Getting updated room info...");

                var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
                if (updatedRoomResult.IsSuccess)
                {
                    var updatedRoom = updatedRoomResult.Value!;
                    var roomInfo = await MapToRoomInfoAsync(updatedRoom);

                    _logger.LogInformation("[GameHub] Broadcasting RoomInfoUpdated to room group...");

                    await Clients.Group(HubMethodNames.Groups.GetRoomGroup(request.RoomCode))
                        .SendAsync(HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);

                    await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.SeatJoined,
                        new { Position = request.Position, RoomInfo = roomInfo });

                    _logger.LogInformation("[GameHub] Player {PlayerId} joined seat {Position} successfully",
                        playerId, request.Position);
                }
                else
                {
                    _logger.LogError("[GameHub] Failed to get updated room info after seat join: {Error}",
                        updatedRoomResult.Error);
                    await SendErrorAsync("Error obteniendo información actualizada de la sala");
                }
            }
            else
            {
                _logger.LogWarning("[GameHub] JoinSeat FAILED for player {PlayerId}: {Error}",
                    playerId, result.Error);
                await SendErrorAsync(result.Error);
            }

            _logger.LogInformation("[GameHub] ===== JoinSeat COMPLETED =====");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameHub] CRITICAL EXCEPTION in JoinSeat for player {PlayerId}",
                GetCurrentPlayerId());
            await HandleExceptionAsync(ex, "JoinSeat");
        }
    }

    public async Task LeaveSeat(LeaveSeatRequest request)
    {
        try
        {
            if (!IsAuthenticated())
            {
                await SendErrorAsync("Debes estar autenticado");
                return;
            }

            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

            if (!ValidateInput(request.RoomCode, nameof(request.RoomCode)))
            {
                await SendErrorAsync("Código de sala inválido");
                return;
            }

            _logger.LogInformation("[GameHub] Player {PlayerId} leaving seat in room {RoomCode}",
                playerId, request.RoomCode);

            var result = await _gameRoomService.LeaveSeatAsync(request.RoomCode, playerId);

            if (result.IsSuccess)
            {
                var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
                if (updatedRoomResult.IsSuccess)
                {
                    var room = updatedRoomResult.Value!;
                    var roomInfo = await MapToRoomInfoAsync(room);

                    await Clients.Group(HubMethodNames.Groups.GetRoomGroup(request.RoomCode))
                        .SendAsync(HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);

                    await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.SeatLeft, roomInfo);
                }

                _logger.LogInformation("[GameHub] Player {PlayerId} left seat successfully", playerId);
            }
            else
            {
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "LeaveSeat");
        }
    }

    public async Task LeaveRoom(string roomCode)
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

            if (!ValidateInput(roomCode, nameof(roomCode)))
            {
                await SendErrorAsync("Código de sala inválido");
                return;
            }

            _logger.LogInformation("[GameHub] EXPLICIT LeaveRoom called by player {PlayerId} for room {RoomCode}",
                playerId, roomCode);

            // Obtener info de la sala para el tableId antes de salir
            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            string? tableGroupName = null;
            if (roomResult.IsSuccess && roomResult.Value!.BlackjackTableId.HasValue)
            {
                tableGroupName = $"Table_{roomResult.Value.BlackjackTableId.Value}";
            }

            // Salir de grupos de SignalR
            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(roomCode);
            await LeaveGroupAsync(roomGroupName);
            await _connectionManager.RemoveFromGroupAsync(Context.ConnectionId, roomGroupName);

            if (tableGroupName != null)
            {
                await LeaveGroupAsync(tableGroupName);
                await _connectionManager.RemoveFromGroupAsync(Context.ConnectionId, tableGroupName);
            }

            var result = await _gameRoomService.LeaveRoomAsync(roomCode, playerId);

            if (result.IsSuccess)
            {
                await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.RoomLeft,
                    new { message = "Has salido de la sala exitosamente" });

                var userName = GetCurrentUserName() ?? "Jugador";
                var playerLeftEvent = new PlayerLeftEventModel(
                    RoomCode: roomCode,
                    PlayerId: playerId.Value,
                    PlayerName: userName,
                    RemainingPlayers: 0,
                    Timestamp: DateTime.UtcNow
                );

                await Clients.Group(roomGroupName)
                    .SendAsync(HubMethodNames.ServerMethods.PlayerLeft, playerLeftEvent);

                // Limpiar información de reconexión (salida explícita)
                await _connectionManager.ClearReconnectionInfoAsync(playerId);

                _logger.LogInformation("[GameHub] Player {PlayerId} EXPLICITLY left room {RoomCode} successfully",
                    playerId, roomCode);
            }
            else
            {
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "LeaveRoom");
        }
    }

    public async Task GetRoomInfo(string roomCode)
    {
        try
        {
            if (!ValidateInput(roomCode, nameof(roomCode)))
            {
                await SendErrorAsync("Código de sala inválido");
                return;
            }

            var result = await _gameRoomService.GetRoomAsync(roomCode);

            if (result.IsSuccess)
            {
                var roomInfo = await MapToRoomInfoAsync(result.Value!);
                await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.RoomInfo, roomInfo);
            }
            else
            {
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "GetRoomInfo");
        }
    }

    #endregion

    #region Game Control

    public async Task StartGame(string roomCode)
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

            if (!ValidateInput(roomCode, nameof(roomCode)))
            {
                await SendErrorAsync("Código de sala inválido");
                return;
            }

            _logger.LogInformation("[GameHub] Starting game in room {RoomCode} by player {PlayerId}",
                roomCode, playerId);

            var result = await _gameRoomService.StartGameAsync(roomCode, playerId);

            if (result.IsSuccess)
            {
                _logger.LogInformation("[GameHub] Game started successfully in room {RoomCode}", roomCode);

                var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
                if (roomResult.IsSuccess)
                {
                    var room = roomResult.Value!;
                    var gameStartedEvent = new { RoomCode = roomCode, Message = "Juego iniciado" };

                    await Clients.Group(HubMethodNames.Groups.GetRoomGroup(roomCode))
                        .SendAsync(HubMethodNames.ServerMethods.GameStarted, gameStartedEvent);
                }
            }
            else
            {
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "StartGame");
        }
    }

    #endregion

    #region Test Methods

    [AllowAnonymous]
    public async Task TestConnection()
    {
        _logger.LogInformation("[GameHub] TestConnection called");
        await Clients.Caller.SendAsync("TestResponse", new
        {
            message = "SignalR funcionando",
            timestamp = DateTime.UtcNow,
            connectionId = Context.ConnectionId,
            playerId = GetCurrentPlayerId()?.Value
        });
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// CORREGIDO: MapToRoomInfoAsync ahora usa SeatPosition directamente del modelo BD
    /// </summary>
    private async Task<RoomInfoModel> MapToRoomInfoAsync(BlackJack.Domain.Models.Game.GameRoom room)
    {
        try
        {
            _logger.LogInformation("[GameHub] Mapping room {RoomCode} to RoomInfoModel", room.RoomCode);

            // ELIMINADO: Ya no necesitamos obtener posiciones de memoria
            // var positionsResult = await _gameRoomService.GetRoomPositionsAsync(room.RoomCode);
            // var positions = positionsResult.IsSuccess ? positionsResult.Value : new Dictionary<Guid, int>();

            _logger.LogInformation("[GameHub] Using SeatPosition directly from database models");

            var roomInfo = new RoomInfoModel(
                RoomCode: room.RoomCode,
                Name: room.Name,
                Status: room.Status.ToString(),
                PlayerCount: room.PlayerCount, // Este viene correcto de BD
                MaxPlayers: room.MaxPlayers,
                Players: room.Players.Select(p => new RoomPlayerModel(
                    PlayerId: p.PlayerId.Value,
                    Name: p.Name,
                    // CORREGIDO: Usar SeatPosition directamente del modelo RoomPlayer
                    Position: p.GetSeatPosition(), // Método que devuelve SeatPosition ?? -1
                    IsReady: p.IsReady,
                    IsHost: room.HostPlayerId == p.PlayerId,
                    HasPlayedTurn: p.HasPlayedTurn
                )).ToList(),
                Spectators: room.Spectators.Select(s => new SpectatorModel(
                    PlayerId: s.PlayerId.Value,
                    Name: s.Name,
                    JoinedAt: s.JoinedAt
                )).ToList(),
                CurrentPlayerTurn: room.CurrentPlayer?.Name,
                CanStart: room.CanStart,
                CreatedAt: room.CreatedAt
            );

            _logger.LogInformation("[GameHub] RoomInfoModel created successfully for room {RoomCode} with {PlayerCount} players",
                room.RoomCode, roomInfo.PlayerCount);

            // NUEVO: Log detallado para debugging
            var playerDetails = string.Join(", ", roomInfo.Players.Select(p => $"{p.Name}(Pos:{p.Position})"));
            _logger.LogInformation("[GameHub] Player details: {Players}", playerDetails);

            return roomInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameHub] Error mapping room {RoomCode} to RoomInfoModel", room.RoomCode);
            throw;
        }
    }

    #endregion
}