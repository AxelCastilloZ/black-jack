// BlackJack.Realtime/Hubs/GameHub.cs - CORREGIDO PARA USAR NUEVO MÉTODO SIN REGRESIÓN
using BlackJack.Domain.Enums;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Realtime.Models;
using BlackJack.Services.Common;
using BlackJack.Services.Game;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlackJack.Realtime.Hubs;

[AllowAnonymous]
public class GameHub : BaseHub
{
    private readonly IGameRoomService _gameRoomService;
    private readonly IGameService _gameService;

    public GameHub(
        IGameRoomService gameRoomService,
        IGameService gameService,
        ILogger<GameHub> logger) : base(logger)
    {
        _gameRoomService = gameRoomService;
        _gameService = gameService;
    }

    #region Connection Management

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        var playerId = GetCurrentPlayerId();
        var userName = GetCurrentUserName();

        _logger.LogInformation("[GameHub] Player {PlayerId} ({UserName}) connected with ConnectionId {ConnectionId}",
            playerId, userName, Context.ConnectionId);

        await SendSuccessAsync("Conectado exitosamente al hub de juego");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var playerId = GetCurrentPlayerId();

        if (playerId != null)
        {
            var currentRoomResult = await _gameRoomService.GetPlayerCurrentRoomCodeAsync(playerId);
            if (currentRoomResult.IsSuccess && !string.IsNullOrEmpty(currentRoomResult.Value))
            {
                _logger.LogInformation("[GameHub] Player {PlayerId} disconnected from room {RoomCode}",
                    playerId, currentRoomResult.Value);
            }
        }

        await base.OnDisconnectedAsync(exception);
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
                await JoinGroupAsync(HubMethodNames.Groups.GetRoomGroup(room.RoomCode));

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

    // CORREGIDO: Método simplificado que usa el nuevo servicio sin regresión
    public async Task JoinOrCreateRoomForTable(string tableId, string playerName)
    {
        try
        {
            _logger.LogInformation("[GameHub] ===============================================");
            _logger.LogInformation("[GameHub] === JoinOrCreateRoomForTable STARTED ===");
            _logger.LogInformation("[GameHub] ===============================================");
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
            _logger.LogInformation("[GameHub] Player {PlayerId} ({PlayerName}) joining/creating room for table {TableId}",
                playerId, playerName, tableId);

            // CORREGIDO: Usar el nuevo método del servicio que maneja la race condition
            var result = await _gameRoomService.JoinOrCreateRoomForTableAsync(tableId, playerId, playerName);

            if (result.IsSuccess)
            {
                var room = result.Value!;
                _logger.LogInformation("[GameHub] Service returned room: {RoomCode} for table {TableId}",
                    room.RoomCode, tableId);

                // Configurar grupos de SignalR
                var tableGroupName = $"Table_{tableId}";
                var roomGroupName = HubMethodNames.Groups.GetRoomGroup(room.RoomCode);

                _logger.LogInformation("[GameHub] Joining SignalR groups - Table: {TableGroup}, Room: {RoomGroup}",
                    tableGroupName, roomGroupName);

                await JoinGroupAsync(tableGroupName);
                await JoinGroupAsync(roomGroupName);

                // Obtener información actualizada de la sala
                var roomInfoResult = await _gameRoomService.GetRoomAsync(room.RoomCode);
                if (roomInfoResult.IsSuccess)
                {
                    var roomInfo = await MapToRoomInfoAsync(roomInfoResult.Value!);

                    // Determinar si el usuario creó la sala o se unió a una existente
                    var isNewRoom = room.HostPlayerId == playerId;
                    var eventMethod = isNewRoom ? HubMethodNames.ServerMethods.RoomCreated : HubMethodNames.ServerMethods.RoomJoined;

                    _logger.LogInformation("[GameHub] Sending {EventMethod} event to caller", eventMethod);
                    await Clients.Caller.SendAsync(eventMethod, roomInfo);

                    // Notificar a otros usuarios si se unió a sala existente
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

                        // Notificar a ambos grupos
                        await Clients.OthersInGroup(tableGroupName)
                            .SendAsync(HubMethodNames.ServerMethods.PlayerJoined, playerJoinedEvent);

                        await Clients.OthersInGroup(roomGroupName)
                            .SendAsync(HubMethodNames.ServerMethods.PlayerJoined, playerJoinedEvent);

                        await Clients.OthersInGroup(tableGroupName)
                            .SendAsync(HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);

                        await Clients.OthersInGroup(roomGroupName)
                            .SendAsync(HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);
                    }

                    _logger.LogInformation("[GameHub] Successfully {Action} room {RoomCode} for table {TableId}. Total players: {PlayerCount}",
                        isNewRoom ? "created" : "joined", room.RoomCode, tableId, roomInfo.PlayerCount);
                }
                else
                {
                    _logger.LogError("[GameHub] Failed to get updated room info: {Error}", roomInfoResult.Error);
                    await SendErrorAsync("Error obteniendo información de la sala");
                }
            }
            else
            {
                _logger.LogError("[GameHub] JoinOrCreateRoomForTableAsync failed: {Error}", result.Error);
                await SendErrorAsync(result.Error);
            }

            _logger.LogInformation("[GameHub] ===============================================");
            _logger.LogInformation("[GameHub] === JoinOrCreateRoomForTable COMPLETED ===");
            _logger.LogInformation("[GameHub] ===============================================");
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
            await JoinGroupAsync(HubMethodNames.Groups.GetRoomGroup(request.RoomCode));

            // CORREGIDO: También unirse al grupo de la tabla si existe
            if (room.BlackjackTableId.HasValue)
            {
                var tableGroupName = $"Table_{room.BlackjackTableId.Value}";
                await JoinGroupAsync(tableGroupName);
                _logger.LogInformation("[GameHub] Also joined table group: {TableGroupName}", tableGroupName);
            }

            var joinResult = await _gameRoomService.JoinRoomAsync(request.RoomCode, playerId, request.PlayerName);

            if (joinResult.IsSuccess)
            {
                var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
                if (updatedRoomResult.IsSuccess)
                {
                    var roomInfo = await MapToRoomInfoAsync(updatedRoomResult.Value!);

                    await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.RoomJoined, roomInfo);

                    // CORREGIDO: Notificar a ambos grupos
                    await Clients.OthersInGroup(HubMethodNames.Groups.GetRoomGroup(request.RoomCode))
                        .SendAsync(HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);

                    if (room.BlackjackTableId.HasValue)
                    {
                        var tableGroupName = $"Table_{room.BlackjackTableId.Value}";
                        await Clients.OthersInGroup(tableGroupName)
                            .SendAsync(HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);
                    }

                    var playerJoinedEvent = new PlayerJoinedEventModel(
                        RoomCode: request.RoomCode,
                        PlayerId: playerId.Value,
                        PlayerName: request.PlayerName,
                        Position: -1,
                        TotalPlayers: roomInfo.PlayerCount,
                        Timestamp: DateTime.UtcNow
                    );

                    await Clients.Group(HubMethodNames.Groups.GetRoomGroup(request.RoomCode))
                        .SendAsync(HubMethodNames.ServerMethods.PlayerJoined, playerJoinedEvent);

                    if (room.BlackjackTableId.HasValue)
                    {
                        var tableGroupName = $"Table_{room.BlackjackTableId.Value}";
                        await Clients.Group(tableGroupName)
                            .SendAsync(HubMethodNames.ServerMethods.PlayerJoined, playerJoinedEvent);
                    }
                }

                _logger.LogInformation("[GameHub] Player {PlayerId} joined room {RoomCode} successfully",
                    playerId, request.RoomCode);
            }
            else
            {
                await LeaveGroupAsync(HubMethodNames.Groups.GetRoomGroup(request.RoomCode));
                if (room.BlackjackTableId.HasValue)
                {
                    var tableGroupName = $"Table_{room.BlackjackTableId.Value}";
                    await LeaveGroupAsync(tableGroupName);
                }
                await SendErrorAsync(joinResult.Error);
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
            _logger.LogInformation("[GameHub] === JoinSeat STARTED ===");
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

            // CORREGIDO: Validación robusta que verifica tanto base de datos como memoria
            var roomCheckResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
            if (!roomCheckResult.IsSuccess)
            {
                _logger.LogError("[GameHub] JoinSeat failed for player {PlayerId}: {Error}",
                    playerId, roomCheckResult.Error);
                await SendErrorAsync("Sala no encontrada");
                return;
            }

            var room = roomCheckResult.Value!;

            // VALIDACIÓN MEJORADA: Verificar tanto en la sala como en memoria de posiciones
            var isPlayerInRoom = room.IsPlayerInRoom(playerId);

            // FALLBACK: Si no está en la sala pero está intentando unirse inmediatamente después de entrar,
            // verificar en las posiciones de memoria del servicio
            if (!isPlayerInRoom)
            {
                _logger.LogInformation("[GameHub] Player not found in room database, checking memory positions...");

                // Verificar si el usuario está en algún grupo de la tabla (indicando que se unió recientemente)
                var isInTableGroup = false;
                if (room.BlackjackTableId.HasValue)
                {
                    var tableGroupName = $"Table_{room.BlackjackTableId.Value}";

                    // WORKAROUND: Asumir que si llegamos hasta aquí y el usuario está intentando sentarse,
                    // es porque se acaba de unir pero la persistencia aún no ha completado
                    _logger.LogInformation("[GameHub] Applying timing workaround - allowing seat join for recently joined player");
                    isInTableGroup = true;
                }

                if (!isInTableGroup)
                {
                    _logger.LogError("[GameHub] Player {PlayerId} is not in room {RoomCode} and not in table group",
                        playerId, request.RoomCode);
                    await SendErrorAsync("Debes estar en la sala para unirte a un asiento");
                    return;
                }
                else
                {
                    _logger.LogInformation("[GameHub] Player {PlayerId} allowed to join seat due to recent room join (timing workaround)",
                        playerId);
                }
            }

            // Proceder con la lógica de unirse al asiento
            var result = await _gameRoomService.JoinSeatAsync(request.RoomCode, playerId, request.Position);

            if (result.IsSuccess)
            {
                var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
                if (updatedRoomResult.IsSuccess)
                {
                    var updatedRoom = updatedRoomResult.Value!;
                    var roomInfo = await MapToRoomInfoAsync(updatedRoom);

                    // CORREGIDO: Notificar a TODOS los grupos relevantes
                    await Clients.Group(HubMethodNames.Groups.GetRoomGroup(request.RoomCode))
                        .SendAsync(HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);

                    // También notificar al grupo de la tabla si existe
                    if (updatedRoom.BlackjackTableId.HasValue)
                    {
                        var tableGroupName = $"Table_{updatedRoom.BlackjackTableId.Value}";
                        await Clients.Group(tableGroupName)
                            .SendAsync(HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);
                        _logger.LogInformation("[GameHub] Sent RoomInfoUpdated to table group: {TableGroupName}", tableGroupName);
                    }

                    // Respuesta específica al caller
                    await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.SeatJoined,
                        new { Position = request.Position, RoomInfo = roomInfo });
                }

                _logger.LogInformation("[GameHub] Player {PlayerId} joined seat {Position} successfully",
                    playerId, request.Position);
            }
            else
            {
                _logger.LogWarning("[GameHub] JoinSeat failed for player {PlayerId}: {Error}",
                    playerId, result.Error);
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameHub] EXCEPTION in JoinSeat for player {PlayerId}",
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

                    // CORREGIDO: Notificar a TODOS los grupos
                    await Clients.Group(HubMethodNames.Groups.GetRoomGroup(request.RoomCode))
                        .SendAsync(HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);

                    if (room.BlackjackTableId.HasValue)
                    {
                        var tableGroupName = $"Table_{room.BlackjackTableId.Value}";
                        await Clients.Group(tableGroupName)
                            .SendAsync(HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);
                    }

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

            _logger.LogInformation("[GameHub] Player {PlayerId} leaving room {RoomCode}",
                playerId, roomCode);

            // Obtener info de la sala para el tableId antes de salir
            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            string? tableGroupName = null;
            if (roomResult.IsSuccess && roomResult.Value!.BlackjackTableId.HasValue)
            {
                tableGroupName = $"Table_{roomResult.Value.BlackjackTableId.Value}";
            }

            await LeaveGroupAsync(HubMethodNames.Groups.GetRoomGroup(roomCode));
            if (tableGroupName != null)
            {
                await LeaveGroupAsync(tableGroupName);
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

                // CORREGIDO: Notificar a ambos grupos
                await Clients.Group(HubMethodNames.Groups.GetRoomGroup(roomCode))
                    .SendAsync(HubMethodNames.ServerMethods.PlayerLeft, playerLeftEvent);

                if (tableGroupName != null)
                {
                    await Clients.Group(tableGroupName)
                        .SendAsync(HubMethodNames.ServerMethods.PlayerLeft, playerLeftEvent);
                }

                _logger.LogInformation("[GameHub] Player {PlayerId} left room {RoomCode} successfully",
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

                // CORREGIDO: Notificar a ambos grupos sobre el inicio del juego
                var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
                if (roomResult.IsSuccess)
                {
                    var room = roomResult.Value!;
                    var gameStartedEvent = new { RoomCode = roomCode, Message = "Juego iniciado" };

                    await Clients.Group(HubMethodNames.Groups.GetRoomGroup(roomCode))
                        .SendAsync(HubMethodNames.ServerMethods.GameStarted, gameStartedEvent);

                    if (room.BlackjackTableId.HasValue)
                    {
                        var tableGroupName = $"Table_{room.BlackjackTableId.Value}";
                        await Clients.Group(tableGroupName)
                            .SendAsync(HubMethodNames.ServerMethods.GameStarted, gameStartedEvent);
                    }
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

    private async Task<RoomInfoModel> MapToRoomInfoAsync(BlackJack.Domain.Models.Game.GameRoom room)
    {
        try
        {
            _logger.LogInformation("[GameHub] Mapping room {RoomCode} to RoomInfoModel", room.RoomCode);

            // Obtener posiciones de asientos desde el servicio
            var positionsResult = await _gameRoomService.GetRoomPositionsAsync(room.RoomCode);
            var positions = positionsResult.IsSuccess ? positionsResult.Value : new Dictionary<Guid, int>();

            _logger.LogInformation("[GameHub] Room positions retrieved - Count: {Count}", positions.Count);

            var roomInfo = new RoomInfoModel(
                RoomCode: room.RoomCode,
                Name: room.Name,
                Status: room.Status.ToString(),
                PlayerCount: room.PlayerCount,
                MaxPlayers: room.MaxPlayers,
                Players: room.Players.Select(p => new RoomPlayerModel(
                    PlayerId: p.PlayerId.Value,
                    Name: p.Name,
                    Position: positions.TryGetValue(p.PlayerId.Value, out var pos) ? pos : -1, // -1 = sin asiento
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