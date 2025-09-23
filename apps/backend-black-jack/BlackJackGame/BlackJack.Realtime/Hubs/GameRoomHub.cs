// BlackJack.Realtime/Hubs/GameRoomHub.cs - Hub básico para manejo de salas
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Realtime.Models;
using BlackJack.Services.Game;
using BlackJack.Realtime.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlackJack.Realtime.Hubs;

[AllowAnonymous]
public class GameRoomHub : BaseHub
{
    private readonly IGameRoomService _gameRoomService;
    private readonly IConnectionManager _connectionManager;
    private readonly ISignalRNotificationService _notificationService;

    public GameRoomHub(
        IGameRoomService gameRoomService,
        IConnectionManager connectionManager,
        ISignalRNotificationService notificationService,
        ILogger<GameRoomHub> logger) : base(logger)
    {
        _gameRoomService = gameRoomService;
        _connectionManager = connectionManager;
        _notificationService = notificationService;
    }

    #region Connection Management

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        var playerId = GetCurrentPlayerId();
        var userName = GetCurrentUserName();

        _logger.LogInformation("[GameRoomHub] Player {PlayerId} ({UserName}) connected with ConnectionId {ConnectionId}",
            playerId, userName, Context.ConnectionId);

        // LIMPIEZA AUTOMÁTICA DE DATOS FANTASMA AL CONECTAR
        if (playerId != null)
        {
            try
            {
                _logger.LogInformation("[GameRoomHub] === AUTOMATIC CLEANUP ON RECONNECT START ===");
                _logger.LogInformation("[GameRoomHub] Performing automatic cleanup for player {PlayerId}", playerId);

                var cleanupResult = await _gameRoomService.ForceCleanupPlayerAsync(playerId);

                if (cleanupResult.IsSuccess)
                {
                    var affectedRows = cleanupResult.Value;
                    if (affectedRows > 0)
                    {
                        _logger.LogInformation("[GameRoomHub] ✅ Automatic cleanup completed: {AffectedRows} orphan records removed for player {PlayerId}",
                            affectedRows, playerId);
                    }
                    else
                    {
                        _logger.LogInformation("[GameRoomHub] ✅ Automatic cleanup completed: No orphan records found for player {PlayerId}", playerId);
                    }
                }
                else
                {
                    _logger.LogWarning("[GameRoomHub] ⚠️ Automatic cleanup failed for player {PlayerId}: {Error}",
                        playerId, cleanupResult.Error);
                }

                _logger.LogInformation("[GameRoomHub] === AUTOMATIC CLEANUP ON RECONNECT END ===");
            }
            catch (Exception ex)
            {
                // IMPORTANTE: No abortar conexión por errores de limpieza
                _logger.LogError(ex, "[GameRoomHub] Error during automatic cleanup for player {PlayerId}: {Error}",
                    playerId, ex.Message);
            }
        }

        // Registrar conexión básica
        if (playerId != null && userName != null)
        {
            await _connectionManager.AddConnectionAsync(Context.ConnectionId, playerId, userName);
            await _notificationService.SendSuccessToConnectionAsync(Context.ConnectionId, "Conectado exitosamente al hub de sala");
        }
        else
        {
            await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, "Error generando ID de jugador");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var playerId = GetCurrentPlayerId();
        _logger.LogInformation("[GameRoomHub] Player {PlayerId} disconnecting from ConnectionId {ConnectionId}",
            playerId, Context.ConnectionId);

        await _connectionManager.RemoveConnectionAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    #endregion

    #region Room Management

    public async Task CreateRoom(CreateRoomRequest request)
    {
        try
        {
            _logger.LogInformation("[GameRoomHub] === CreateRoom STARTED ===");
            _logger.LogInformation("[GameRoomHub] RoomName: {RoomName}", request.RoomName);

            if (!IsAuthenticated())
            {
                _logger.LogWarning("[GameRoomHub] CreateRoom - Authentication failed");
                await SendErrorAsync("Debes estar autenticado para crear una sala");
                return;
            }

            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                _logger.LogWarning("[GameRoomHub] CreateRoom - PlayerId is null");
                await SendErrorAsync("Error de autenticación");
                return;
            }

            if (!ValidateInput(request.RoomName, nameof(request.RoomName), 50))
            {
                _logger.LogWarning("[GameRoomHub] CreateRoom - Invalid room name");
                await SendErrorAsync("Nombre de sala inválido");
                return;
            }

            _logger.LogInformation("[GameRoomHub] Creating room {RoomName} for player {PlayerId}",
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

                _logger.LogInformation("[GameRoomHub] Room {RoomCode} created successfully", room.RoomCode);
            }
            else
            {
                _logger.LogError("[GameRoomHub] CreateRoom failed: {Error}", result.Error);
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomHub] EXCEPTION in CreateRoom");
            await HandleExceptionAsync(ex, "CreateRoom");
        }
    }

    public async Task JoinOrCreateRoomForTable(string tableId, string playerName)
    {
        try
        {
            _logger.LogInformation("[GameRoomHub] ================================================");
            _logger.LogInformation("[GameRoomHub] === JoinOrCreateRoomForTable STARTED ===");
            _logger.LogInformation("[GameRoomHub] ================================================");
            _logger.LogInformation("[GameRoomHub] TableId: {TableId}", tableId);
            _logger.LogInformation("[GameRoomHub] PlayerName: {PlayerName}", playerName);
            _logger.LogInformation("[GameRoomHub] ConnectionId: {ConnectionId}", Context.ConnectionId);

            if (!IsAuthenticated())
            {
                _logger.LogWarning("[GameRoomHub] JoinOrCreateRoomForTable - Authentication failed");
                await SendErrorAsync("Debes estar autenticado");
                return;
            }

            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                _logger.LogWarning("[GameRoomHub] JoinOrCreateRoomForTable - PlayerId is null");
                await SendErrorAsync("Error de autenticación");
                return;
            }

            _logger.LogInformation("[GameRoomHub] PlayerId validated: {PlayerId}", playerId);

            if (!ValidateInput(tableId, nameof(tableId)) ||
                !ValidateInput(playerName, nameof(playerName), 30))
            {
                _logger.LogWarning("[GameRoomHub] JoinOrCreateRoomForTable - Invalid input data");
                await SendErrorAsync("Datos inválidos");
                return;
            }

            _logger.LogInformation("[GameRoomHub] Input validation passed");
            _logger.LogInformation("[GameRoomHub] Calling GameRoomService.JoinOrCreateRoomForTableAsync...");

            var result = await _gameRoomService.JoinOrCreateRoomForTableAsync(tableId, playerId, playerName);

            if (!result.IsSuccess)
            {
                _logger.LogError("[GameRoomHub] JoinOrCreateRoomForTableAsync FAILED: {Error}", result.Error);
                await SendErrorAsync(result.Error);
                return;
            }

            var room = result.Value!;
            _logger.LogInformation("[GameRoomHub] Service SUCCESS - Room: {RoomCode} for table {TableId}",
                room.RoomCode, tableId);

            var tableGroupName = $"Table_{tableId}";
            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(room.RoomCode);

            _logger.LogInformation("[GameRoomHub] Joining SignalR groups - Table: {TableGroup}, Room: {RoomGroup}",
                tableGroupName, roomGroupName);

            await JoinGroupAsync(tableGroupName);
            await JoinGroupAsync(roomGroupName);
            await _connectionManager.AddToGroupAsync(Context.ConnectionId, tableGroupName);
            await _connectionManager.AddToGroupAsync(Context.ConnectionId, roomGroupName);

            var roomInfoResult = await _gameRoomService.GetRoomAsync(room.RoomCode);
            if (roomInfoResult.IsSuccess)
            {
                var roomInfo = await MapToRoomInfoAsync(roomInfoResult.Value!);

                var isNewRoom = room.HostPlayerId == playerId;
                var eventMethod = isNewRoom ? HubMethodNames.ServerMethods.RoomCreated : HubMethodNames.ServerMethods.RoomJoined;

                _logger.LogInformation("[GameRoomHub] Sending {EventMethod} event to caller (isNewRoom: {IsNewRoom})", eventMethod, isNewRoom);

                await Clients.Caller.SendAsync(eventMethod, roomInfo);

                if (!isNewRoom)
                {
                    _logger.LogInformation("[GameRoomHub] Notifying other users about player join via NotificationService");

                    var playerJoinedEvent = new PlayerJoinedEventModel(
                        RoomCode: room.RoomCode,
                        PlayerId: playerId.Value,
                        PlayerName: playerName,
                        Position: -1,
                        TotalPlayers: roomInfo.PlayerCount,
                        Timestamp: DateTime.UtcNow
                    );

                    await _notificationService.NotifyRoomExceptAsync(room.RoomCode, Context.ConnectionId,
                        HubMethodNames.ServerMethods.PlayerJoined, playerJoinedEvent);

                    await _notificationService.NotifyRoomExceptAsync(room.RoomCode, Context.ConnectionId,
                        HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);
                }

                _logger.LogInformation("[GameRoomHub] Successfully {Action} room {RoomCode} for table {TableId}. Total players: {PlayerCount}",
                    isNewRoom ? "created" : "joined", room.RoomCode, tableId, roomInfo.PlayerCount);
            }
            else
            {
                _logger.LogError("[GameRoomHub] Failed to get updated room info: {Error}", roomInfoResult.Error);

                await LeaveGroupAsync(roomGroupName);
                await LeaveGroupAsync(tableGroupName);
                await _connectionManager.RemoveFromGroupAsync(Context.ConnectionId, roomGroupName);
                await _connectionManager.RemoveFromGroupAsync(Context.ConnectionId, tableGroupName);

                await SendErrorAsync("Error obteniendo información de la sala");
            }

            _logger.LogInformation("[GameRoomHub] ================================================");
            _logger.LogInformation("[GameRoomHub] === JoinOrCreateRoomForTable COMPLETED ===");
            _logger.LogInformation("[GameRoomHub] ================================================");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomHub] CRITICAL EXCEPTION in JoinOrCreateRoomForTable");
            _logger.LogError(ex, "[GameRoomHub] Exception details - Message: {Message}, StackTrace: {StackTrace}",
                ex.Message, ex.StackTrace);
            await HandleExceptionAsync(ex, "JoinOrCreateRoomForTable");
        }
    }

    public async Task JoinRoom(JoinRoomRequest request)
    {
        try
        {
            _logger.LogInformation("[GameRoomHub] === JoinRoom STARTED ===");
            _logger.LogInformation("[GameRoomHub] RoomCode: {RoomCode}, PlayerName: {PlayerName}",
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

            _logger.LogInformation("[GameRoomHub] Player {PlayerId} joining room {RoomCode}",
                playerId, request.RoomCode);

            var roomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync("Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;

            var joinResult = await _gameRoomService.JoinRoomAsync(request.RoomCode, playerId, request.PlayerName);

            if (!joinResult.IsSuccess)
            {
                _logger.LogError("[GameRoomHub] JoinRoom failed: {Error}", joinResult.Error);
                await SendErrorAsync(joinResult.Error);
                return;
            }

            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(request.RoomCode);
            await JoinGroupAsync(roomGroupName);
            await _connectionManager.AddToGroupAsync(Context.ConnectionId, roomGroupName);

            if (room.BlackjackTableId.HasValue)
            {
                var tableGroupName = $"Table_{room.BlackjackTableId.Value}";
                await JoinGroupAsync(tableGroupName);
                await _connectionManager.AddToGroupAsync(Context.ConnectionId, tableGroupName);
                _logger.LogInformation("[GameRoomHub] Also joined table group: {TableGroupName}", tableGroupName);
            }

            var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
            if (updatedRoomResult.IsSuccess)
            {
                var roomInfo = await MapToRoomInfoAsync(updatedRoomResult.Value!);

                await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.RoomJoined, roomInfo);

                var playerJoinedEvent = new PlayerJoinedEventModel(
                    RoomCode: request.RoomCode,
                    PlayerId: playerId.Value,
                    PlayerName: request.PlayerName,
                    Position: -1,
                    TotalPlayers: roomInfo.PlayerCount,
                    Timestamp: DateTime.UtcNow
                );

                await _notificationService.NotifyRoomExceptAsync(request.RoomCode, Context.ConnectionId,
                    HubMethodNames.ServerMethods.PlayerJoined, playerJoinedEvent);

                await _notificationService.NotifyRoomExceptAsync(request.RoomCode, Context.ConnectionId,
                    HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);

                _logger.LogInformation("[GameRoomHub] Player {PlayerId} joined room {RoomCode} successfully",
                    playerId, request.RoomCode);
            }
            else
            {
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
            _logger.LogError(ex, "[GameRoomHub] EXCEPTION in JoinRoom");
            await HandleExceptionAsync(ex, "JoinRoom");
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

            _logger.LogInformation("[GameRoomHub] EXPLICIT LeaveRoom called by player {PlayerId} for room {RoomCode}",
                playerId, roomCode);

            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            string? tableGroupName = null;
            if (roomResult.IsSuccess && roomResult.Value!.BlackjackTableId.HasValue)
            {
                tableGroupName = $"Table_{roomResult.Value.BlackjackTableId.Value}";
            }

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

                await _notificationService.NotifyPlayerLeftAsync(roomCode, playerLeftEvent);
                await _connectionManager.ClearReconnectionInfoAsync(playerId);

                _logger.LogInformation("[GameRoomHub] Player {PlayerId} EXPLICITLY left room {RoomCode} successfully",
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

    #region Seat Management

    public async Task JoinSeat(JoinSeatRequest request)
    {
        try
        {
            _logger.LogInformation("[GameRoomHub] ===== JoinSeat STARTED =====");
            _logger.LogInformation("[GameRoomHub] RoomCode: {RoomCode}, Position: {Position}",
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

            _logger.LogInformation("[GameRoomHub] Player {PlayerId} attempting to join seat {Position} in room {RoomCode}",
                playerId, request.Position, request.RoomCode);

            var result = await _gameRoomService.JoinSeatAsync(request.RoomCode, playerId, request.Position);

            if (result.IsSuccess)
            {
                _logger.LogInformation("[GameRoomHub] JoinSeat SUCCESS - Getting updated room info...");

                var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
                if (updatedRoomResult.IsSuccess)
                {
                    var updatedRoom = updatedRoomResult.Value!;
                    var roomInfo = await MapToRoomInfoAsync(updatedRoom);

                    _logger.LogInformation("[GameRoomHub] Broadcasting RoomInfoUpdated via NotificationService...");

                    await _notificationService.NotifyRoomInfoUpdatedAsync(request.RoomCode, roomInfo);

                    await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.SeatJoined,
                        new { Position = request.Position, RoomInfo = roomInfo });

                    _logger.LogInformation("[GameRoomHub] Player {PlayerId} joined seat {Position} successfully",
                        playerId, request.Position);
                }
                else
                {
                    _logger.LogError("[GameRoomHub] Failed to get updated room info after seat join: {Error}",
                        updatedRoomResult.Error);
                    await SendErrorAsync("Error obteniendo información actualizada de la sala");
                }
            }
            else
            {
                _logger.LogWarning("[GameRoomHub] JoinSeat FAILED for player {PlayerId}: {Error}",
                    playerId, result.Error);
                await SendErrorAsync(result.Error);
            }

            _logger.LogInformation("[GameRoomHub] ===== JoinSeat COMPLETED =====");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomHub] CRITICAL EXCEPTION in JoinSeat for player {PlayerId}",
                GetCurrentPlayerId());
            await HandleExceptionAsync(ex, "JoinSeat");
        }
    }

    public async Task LeaveSeat(LeaveSeatRequest request)
    {
        try
        {
            _logger.LogInformation("[GameRoomHub] ===== LeaveSeat STARTED =====");
            _logger.LogInformation("[GameRoomHub] RoomCode: {RoomCode}", request.RoomCode);

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

            _logger.LogInformation("[GameRoomHub] Player {PlayerId} leaving seat in room {RoomCode}",
                playerId, request.RoomCode);

            var result = await _gameRoomService.LeaveSeatAsync(request.RoomCode, playerId);

            if (result.IsSuccess)
            {
                _logger.LogInformation("[GameRoomHub] LeaveSeat SUCCESS - Getting updated room info...");

                var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
                if (updatedRoomResult.IsSuccess)
                {
                    var room = updatedRoomResult.Value!;
                    var roomInfo = await MapToRoomInfoAsync(room);

                    _logger.LogInformation("[GameRoomHub] Broadcasting RoomInfoUpdated via NotificationService...");

                    await _notificationService.NotifyRoomInfoUpdatedAsync(request.RoomCode, roomInfo);

                    await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.SeatLeft, roomInfo);

                    _logger.LogInformation("[GameRoomHub] Player {PlayerId} left seat successfully", playerId);
                }
                else
                {
                    _logger.LogError("[GameRoomHub] Failed to get updated room info after seat leave: {Error}",
                        updatedRoomResult.Error);
                    await SendErrorAsync("Error obteniendo información actualizada de la sala");
                }
            }
            else
            {
                _logger.LogWarning("[GameRoomHub] LeaveSeat FAILED for player {PlayerId}: {Error}",
                    playerId, result.Error);
                await SendErrorAsync(result.Error);
            }

            _logger.LogInformation("[GameRoomHub] ===== LeaveSeat COMPLETED =====");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomHub] CRITICAL EXCEPTION in LeaveSeat for player {PlayerId}",
                GetCurrentPlayerId());
            await HandleExceptionAsync(ex, "LeaveSeat");
        }
    }

    #endregion

    #region Spectator Management

    public async Task JoinAsViewer(JoinRoomRequest request)
    {
        try
        {
            _logger.LogInformation("[GameRoomHub] === JoinAsViewer STARTED ===");
            _logger.LogInformation("[GameRoomHub] RoomCode: {RoomCode}, PlayerName: {PlayerName}",
                request.RoomCode, request.PlayerName);

            if (!IsAuthenticated())
            {
                await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, "Debes estar autenticado para unirte como viewer");
                return;
            }

            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, "Error de autenticación");
                return;
            }

            if (!ValidateInput(request.RoomCode, nameof(request.RoomCode)) ||
                !ValidateInput(request.PlayerName, nameof(request.PlayerName), 30))
            {
                await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, "Datos de entrada inválidos");
                return;
            }

            _logger.LogInformation("[GameRoomHub] Player {PlayerId} joining room {RoomCode} as viewer",
                playerId, request.RoomCode);

            var roomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
            if (!roomResult.IsSuccess)
            {
                await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, "Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;

            var joinResult = await _gameRoomService.JoinRoomAsync(request.RoomCode, playerId, request.PlayerName, true);

            if (!joinResult.IsSuccess)
            {
                _logger.LogError("[GameRoomHub] JoinAsViewer failed: {Error}", joinResult.Error);
                await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, joinResult.Error);
                return;
            }

            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(request.RoomCode);
            await JoinGroupAsync(roomGroupName);

            if (room.BlackjackTableId.HasValue)
            {
                var tableGroupName = $"Table_{room.BlackjackTableId.Value}";
                await JoinGroupAsync(tableGroupName);
                _logger.LogInformation("[GameRoomHub] Also joined table group: {TableGroupName}", tableGroupName);
            }

            var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
            if (updatedRoomResult.IsSuccess)
            {
                var roomInfo = await MapToRoomInfoAsync(updatedRoomResult.Value!);

                await _notificationService.NotifyConnectionAsync(Context.ConnectionId,
                    HubMethodNames.ServerMethods.RoomJoined, roomInfo);

                var playerJoinedEvent = new PlayerJoinedEventModel(
                    RoomCode: request.RoomCode,
                    PlayerId: playerId.Value,
                    PlayerName: request.PlayerName,
                    Position: -1,
                    TotalPlayers: roomInfo.PlayerCount,
                    Timestamp: DateTime.UtcNow
                );

                await _notificationService.NotifyRoomExceptAsync(request.RoomCode, Context.ConnectionId,
                    HubMethodNames.ServerMethods.PlayerJoined, playerJoinedEvent);

                await _notificationService.NotifyRoomExceptAsync(request.RoomCode, Context.ConnectionId,
                    HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);

                _logger.LogInformation("[GameRoomHub] Player {PlayerId} joined room {RoomCode} as viewer successfully",
                    playerId, request.RoomCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomHub] Error in JoinAsViewer: {Error}", ex.Message);
            await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, "Error interno del servidor");
        }
    }

    public async Task JoinOrCreateRoomForTableAsViewer(string tableId, string playerName)
    {
        try
        {
            _logger.LogInformation("[GameRoomHub] === JoinOrCreateRoomForTableAsViewer STARTED ===");
            _logger.LogInformation("[GameRoomHub] TableId: {TableId}, PlayerName: {PlayerName}",
                tableId, playerName);

            if (!IsAuthenticated())
            {
                await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, "Debes estar autenticado para unirte como viewer");
                return;
            }

            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, "Error de autenticación");
                return;
            }

            if (!ValidateInput(tableId, nameof(tableId)) ||
                !ValidateInput(playerName, nameof(playerName), 30))
            {
                _logger.LogWarning("[GameRoomHub] JoinOrCreateRoomForTableAsViewer - Invalid input data");
                await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, "Datos inválidos");
                return;
            }

            _logger.LogInformation("[GameRoomHub] Input validation passed");
            _logger.LogInformation("[GameRoomHub] Calling GameRoomService.JoinOrCreateRoomForTableAsViewerAsync...");

            var result = await _gameRoomService.JoinOrCreateRoomForTableAsViewerAsync(tableId, playerId, playerName);

            if (!result.IsSuccess)
            {
                _logger.LogError("[GameRoomHub] JoinOrCreateRoomForTableAsync FAILED: {Error}", result.Error);
                await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, result.Error);
                return;
            }

            var room = result.Value!;
            _logger.LogInformation("[GameRoomHub] Service SUCCESS - Room: {RoomCode} for table {TableId}",
                room.RoomCode, tableId);

            var tableGroupName = $"Table_{tableId}";
            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(room.RoomCode);

            _logger.LogInformation("[GameRoomHub] Joining SignalR groups - Table: {TableGroup}, Room: {RoomGroup}",
                tableGroupName, roomGroupName);

            await JoinGroupAsync(tableGroupName);
            await JoinGroupAsync(roomGroupName);

            var roomInfoResult = await _gameRoomService.GetRoomAsync(room.RoomCode);
            if (roomInfoResult.IsSuccess)
            {
                var roomInfo = await MapToRoomInfoAsync(roomInfoResult.Value!);

                var isNewRoom = room.HostPlayerId == playerId;
                var eventMethod = isNewRoom ? HubMethodNames.ServerMethods.RoomCreated : HubMethodNames.ServerMethods.RoomJoined;

                _logger.LogInformation("[GameRoomHub] Sending {EventMethod} event to caller (isNewRoom: {IsNewRoom})", eventMethod, isNewRoom);

                await _notificationService.NotifyConnectionAsync(Context.ConnectionId, eventMethod, roomInfo);

                if (!isNewRoom)
                {
                    _logger.LogInformation("[GameRoomHub] Notifying other users about viewer join");

                    var playerJoinedEvent = new PlayerJoinedEventModel(
                        RoomCode: room.RoomCode,
                        PlayerId: playerId.Value,
                        PlayerName: playerName,
                        Position: -1,
                        TotalPlayers: roomInfo.PlayerCount,
                        Timestamp: DateTime.UtcNow
                    );

                    await _notificationService.NotifyRoomExceptAsync(room.RoomCode, Context.ConnectionId,
                        HubMethodNames.ServerMethods.PlayerJoined, playerJoinedEvent);

                    await _notificationService.NotifyRoomExceptAsync(room.RoomCode, Context.ConnectionId,
                        HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);
                }

                _logger.LogInformation("[GameRoomHub] Player {PlayerId} joined/created room {RoomCode} as viewer successfully",
                    playerId, room.RoomCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomHub] EXCEPTION in JoinOrCreateRoomForTableAsViewer");
            await HandleExceptionAsync(ex, "JoinOrCreateRoomForTableAsViewer");
        }
    }

    #endregion

    #region Test Methods

    [AllowAnonymous]
    public async Task TestConnection()
    {
        _logger.LogInformation("[GameRoomHub] TestConnection called");
        var response = new
        {
            message = "SignalR funcionando - GameRoomHub básico",
            timestamp = DateTime.UtcNow,
            connectionId = Context.ConnectionId,
            playerId = GetCurrentPlayerId()?.Value
        };

        await _notificationService.NotifyConnectionAsync(Context.ConnectionId, "TestResponse", response);
    }

    #endregion

    #region Utility Methods

    private async Task<RoomInfoModel> MapToRoomInfoAsync(BlackJack.Domain.Models.Game.GameRoom room)
    {
        try
        {
            _logger.LogInformation("[GameRoomHub] Mapping room {RoomCode} to RoomInfoModel with basic data", room.RoomCode);

            var seatedPlayersCount = room.Players.Count(p => p.IsSeated);
            var isAutoBettingActive = room.MinBetPerRound?.Amount > 0 && seatedPlayersCount > 0;

            _logger.LogInformation("[GameRoomHub] Basic calculation: MinBetPerRound={MinBet}, SeatedPlayers={SeatedCount}, Active={Active}",
                room.MinBetPerRound?.Amount ?? 0, seatedPlayersCount, isAutoBettingActive);

            var roomInfo = new RoomInfoModel(
                RoomCode: room.RoomCode,
                Name: room.Name,
                Status: room.Status.ToString(),
                PlayerCount: room.PlayerCount,
                MaxPlayers: room.MaxPlayers,
                MinBetPerRound: room.MinBetPerRound?.Amount ?? 0,
                AutoBettingActive: isAutoBettingActive,
                Players: room.Players.Select(p => new RoomPlayerModel(
                    PlayerId: p.PlayerId.Value,
                    Name: p.Name,
                    Position: p.GetSeatPosition(),
                    IsReady: p.IsReady,
                    IsHost: room.HostPlayerId == p.PlayerId,
                    HasPlayedTurn: p.HasPlayedTurn,
                    CurrentBalance: p.Player?.Balance?.Amount ?? 0,
                    TotalBetThisSession: p.TotalBetThisSession,
                    CanAffordBet: p.Player?.Balance != null && room.MinBetPerRound != null
                        ? p.Player.Balance.Amount >= room.MinBetPerRound.Amount
                        : false
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

            _logger.LogInformation("[GameRoomHub] RoomInfoModel created successfully for room {RoomCode} with {PlayerCount} players",
                room.RoomCode, roomInfo.PlayerCount);

            return roomInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomHub] Error mapping room {RoomCode} to RoomInfoModel", room.RoomCode);
            throw;
        }
    }

    #endregion
}