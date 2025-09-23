// BlackJack.Realtime/Hubs/RoomHub.cs - CORREGIDO CON auto-betting integrado
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
public class RoomHub : BaseHub
{
    private readonly IGameRoomService _gameRoomService;
    private readonly IConnectionManager _connectionManager;
    private readonly ISignalRNotificationService _notificationService;

    public RoomHub(
        IGameRoomService gameRoomService,
        IConnectionManager connectionManager,
        ISignalRNotificationService notificationService,
        ILogger<RoomHub> logger) : base(logger)
    {
        _gameRoomService = gameRoomService;
        _connectionManager = connectionManager;
        _notificationService = notificationService;
    }

    #region Room Management

    public async Task CreateRoom(CreateRoomRequest request)
    {
        try
        {
            _logger.LogInformation("[RoomHub] === CreateRoom STARTED ===");
            _logger.LogInformation("[RoomHub] RoomName: {RoomName}", request.RoomName);

            if (!IsAuthenticated())
            {
                _logger.LogWarning("[RoomHub] CreateRoom - Authentication failed");
                await SendErrorAsync("Debes estar autenticado para crear una sala");
                return;
            }

            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                _logger.LogWarning("[RoomHub] CreateRoom - PlayerId is null");
                await SendErrorAsync("Error de autenticación");
                return;
            }

            if (!ValidateInput(request.RoomName, nameof(request.RoomName), 50))
            {
                _logger.LogWarning("[RoomHub] CreateRoom - Invalid room name");
                await SendErrorAsync("Nombre de sala inválido");
                return;
            }

            _logger.LogInformation("[RoomHub] Creating room {RoomName} for player {PlayerId}",
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

                // CORREGIDO: Usar Clients.Caller directamente para el creador
                await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.RoomCreated, roomInfo);

                _logger.LogInformation("[RoomHub] Room {RoomCode} created successfully", room.RoomCode);
            }
            else
            {
                _logger.LogError("[RoomHub] CreateRoom failed: {Error}", result.Error);
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoomHub] EXCEPTION in CreateRoom");
            await HandleExceptionAsync(ex, "CreateRoom");
        }
    }

    public async Task JoinOrCreateRoomForTable(string tableId, string playerName)
    {
        try
        {
            _logger.LogInformation("[RoomHub] ================================================");
            _logger.LogInformation("[RoomHub] === JoinOrCreateRoomForTable STARTED ===");
            _logger.LogInformation("[RoomHub] ================================================");
            _logger.LogInformation("[RoomHub] TableId: {TableId}", tableId);
            _logger.LogInformation("[RoomHub] PlayerName: {PlayerName}", playerName);
            _logger.LogInformation("[RoomHub] ConnectionId: {ConnectionId}", Context.ConnectionId);

            if (!IsAuthenticated())
            {
                _logger.LogWarning("[RoomHub] JoinOrCreateRoomForTable - Authentication failed");
                await SendErrorAsync("Debes estar autenticado");
                return;
            }

            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                _logger.LogWarning("[RoomHub] JoinOrCreateRoomForTable - PlayerId is null");
                await SendErrorAsync("Error de autenticación");
                return;
            }

            _logger.LogInformation("[RoomHub] PlayerId validated: {PlayerId}", playerId);

            if (!ValidateInput(tableId, nameof(tableId)) ||
                !ValidateInput(playerName, nameof(playerName), 30))
            {
                _logger.LogWarning("[RoomHub] JoinOrCreateRoomForTable - Invalid input data");
                await SendErrorAsync("Datos inválidos");
                return;
            }

            _logger.LogInformation("[RoomHub] Input validation passed");
            _logger.LogInformation("[RoomHub] Calling GameRoomService.JoinOrCreateRoomForTableAsync...");

            // CORREGIDO: Primero ejecutar la lógica de unión/creación
            var result = await _gameRoomService.JoinOrCreateRoomForTableAsync(tableId, playerId, playerName);

            if (!result.IsSuccess)
            {
                _logger.LogError("[RoomHub] JoinOrCreateRoomForTableAsync FAILED: {Error}", result.Error);
                await SendErrorAsync(result.Error);
                return;
            }

            var room = result.Value!;
            _logger.LogInformation("[RoomHub] Service SUCCESS - Room: {RoomCode} for table {TableId}",
                room.RoomCode, tableId);

            // CORREGIDO: Solo unirse a grupos de SignalR DESPUÉS de confirmar éxito
            var tableGroupName = $"Table_{tableId}";
            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(room.RoomCode);

            _logger.LogInformation("[RoomHub] Joining SignalR groups - Table: {TableGroup}, Room: {RoomGroup}",
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

                _logger.LogInformation("[RoomHub] Sending {EventMethod} event to caller (isNewRoom: {IsNewRoom})", eventMethod, isNewRoom);

                // CORREGIDO: Usar Clients.Caller directamente para respuesta al usuario
                await Clients.Caller.SendAsync(eventMethod, roomInfo);

                // Solo notificar si se unió a sala existente
                if (!isNewRoom)
                {
                    _logger.LogInformation("[RoomHub] Notifying other users about player join via NotificationService");

                    var playerJoinedEvent = new PlayerJoinedEventModel(
                        RoomCode: room.RoomCode,
                        PlayerId: playerId.Value,
                        PlayerName: playerName,
                        Position: -1,
                        TotalPlayers: roomInfo.PlayerCount,
                        Timestamp: DateTime.UtcNow
                    );

                    // CORREGIDO: Usar NotificationService en lugar de Clients directamente
                    // Pero excluir al usuario actual para evitar duplicación
                    await _notificationService.NotifyRoomExceptAsync(room.RoomCode, Context.ConnectionId,
                        HubMethodNames.ServerMethods.PlayerJoined, playerJoinedEvent);

                    await _notificationService.NotifyRoomExceptAsync(room.RoomCode, Context.ConnectionId,
                        HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);
                }

                _logger.LogInformation("[RoomHub] Successfully {Action} room {RoomCode} for table {TableId}. Total players: {PlayerCount}",
                    isNewRoom ? "created" : "joined", room.RoomCode, tableId, roomInfo.PlayerCount);
            }
            else
            {
                _logger.LogError("[RoomHub] Failed to get updated room info: {Error}", roomInfoResult.Error);

                // CORREGIDO: Si falla, remover de grupos de SignalR
                await LeaveGroupAsync(roomGroupName);
                await LeaveGroupAsync(tableGroupName);
                await _connectionManager.RemoveFromGroupAsync(Context.ConnectionId, roomGroupName);
                await _connectionManager.RemoveFromGroupAsync(Context.ConnectionId, tableGroupName);

                await SendErrorAsync("Error obteniendo información de la sala");
            }

            _logger.LogInformation("[RoomHub] ================================================");
            _logger.LogInformation("[RoomHub] === JoinOrCreateRoomForTable COMPLETED ===");
            _logger.LogInformation("[RoomHub] ================================================");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoomHub] CRITICAL EXCEPTION in JoinOrCreateRoomForTable");
            _logger.LogError(ex, "[RoomHub] Exception details - Message: {Message}, StackTrace: {StackTrace}",
                ex.Message, ex.StackTrace);
            await HandleExceptionAsync(ex, "JoinOrCreateRoomForTable");
        }
    }

    public async Task JoinRoom(JoinRoomRequest request)
    {
        try
        {
            _logger.LogInformation("[RoomHub] === JoinRoom STARTED ===");
            _logger.LogInformation("[RoomHub] RoomCode: {RoomCode}, PlayerName: {PlayerName}",
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

            _logger.LogInformation("[RoomHub] Player {PlayerId} joining room {RoomCode}",
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
                _logger.LogError("[RoomHub] JoinRoom failed: {Error}", joinResult.Error);
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
                _logger.LogInformation("[RoomHub] Also joined table group: {TableGroupName}", tableGroupName);
            }

            var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
            if (updatedRoomResult.IsSuccess)
            {
                var roomInfo = await MapToRoomInfoAsync(updatedRoomResult.Value!);

                // CORREGIDO: Usar Clients.Caller para respuesta directa
                await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.RoomJoined, roomInfo);

                var playerJoinedEvent = new PlayerJoinedEventModel(
                    RoomCode: request.RoomCode,
                    PlayerId: playerId.Value,
                    PlayerName: request.PlayerName,
                    Position: -1,
                    TotalPlayers: roomInfo.PlayerCount,
                    Timestamp: DateTime.UtcNow
                );

                // CORREGIDO: Usar NotificationService para notificar a otros, excluyendo al caller
                await _notificationService.NotifyRoomExceptAsync(request.RoomCode, Context.ConnectionId,
                    HubMethodNames.ServerMethods.PlayerJoined, playerJoinedEvent);

                await _notificationService.NotifyRoomExceptAsync(request.RoomCode, Context.ConnectionId,
                    HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);

                _logger.LogInformation("[RoomHub] Player {PlayerId} joined room {RoomCode} successfully",
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
            _logger.LogError(ex, "[RoomHub] EXCEPTION in JoinRoom");
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

            _logger.LogInformation("[RoomHub] EXPLICIT LeaveRoom called by player {PlayerId} for room {RoomCode}",
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
                // CORREGIDO: Usar Clients.Caller para respuesta directa
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

                // CORREGIDO: Usar NotificationService para notificar a todos en la sala
                await _notificationService.NotifyPlayerLeftAsync(roomCode, playerLeftEvent);

                // Limpiar información de reconexión (salida explícita)
                await _connectionManager.ClearReconnectionInfoAsync(playerId);

                _logger.LogInformation("[RoomHub] Player {PlayerId} EXPLICITLY left room {RoomCode} successfully",
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
                // CORREGIDO: Usar Clients.Caller para respuesta directa
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

    #region Utility Methods

    /// <summary>
    /// CORREGIDO: MapToRoomInfoAsync ahora incluye datos de auto-betting y balance de jugadores
    /// </summary>
    private async Task<RoomInfoModel> MapToRoomInfoAsync(BlackJack.Domain.Models.Game.GameRoom room)
    {
        try
        {
            _logger.LogInformation("[RoomHub] Mapping room {RoomCode} to RoomInfoModel with auto-betting data", room.RoomCode);

            // NUEVO: Calcular datos de auto-betting
            var seatedPlayersCount = room.Players.Count(p => p.IsSeated);
            var isAutoBettingActive = room.MinBetPerRound?.Amount > 0 && seatedPlayersCount > 0;

            _logger.LogInformation("[RoomHub] Auto-betting calculation: MinBetPerRound={MinBet}, SeatedPlayers={SeatedCount}, Active={Active}",
                room.MinBetPerRound?.Amount ?? 0, seatedPlayersCount, isAutoBettingActive);

            // Determinar host efectivo: si el Host actual no está sentado, usar el primer jugador sentado
            var seatedPlayers = room.Players.Where(p => p.IsSeated).ToList();
            var effectiveHostId = seatedPlayers.Any() && !seatedPlayers.Any(p => p.PlayerId.Value == room.HostPlayerId.Value)
                ? seatedPlayers.First().PlayerId
                : room.HostPlayerId;

            var roomInfo = new RoomInfoModel(
                RoomCode: room.RoomCode,
                Name: room.Name,
                Status: room.Status.ToString(),
                PlayerCount: seatedPlayersCount,
                MaxPlayers: room.MaxPlayers,

                // NUEVO: Datos de auto-betting incluidos
                MinBetPerRound: room.MinBetPerRound?.Amount ?? 0,
                AutoBettingActive: isAutoBettingActive,

                Players: seatedPlayers
                    .Select(p => new RoomPlayerModel(
                    PlayerId: p.PlayerId.Value,
                    Name: p.Name,
                    Position: p.GetSeatPosition(), // Usar SeatPosition para el layout
                    IsReady: p.IsReady,
                    IsHost: effectiveHostId.Value == p.PlayerId.Value,
                    HasPlayedTurn: p.HasPlayedTurn,

                    // NUEVO: Datos de balance y auto-betting del jugador
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
                CanStart: seatedPlayersCount >= 1,
                CreatedAt: room.CreatedAt
            );

            _logger.LogInformation("[RoomHub] RoomInfoModel created successfully for room {RoomCode} with {PlayerCount} players, MinBetPerRound={MinBet}",
                room.RoomCode, roomInfo.PlayerCount, roomInfo.MinBetPerRound);

            // NUEVO: Log detallado para debugging auto-betting
            var playerDetails = string.Join(", ", roomInfo.Players.Select(p =>
                $"{p.Name}(Pos:{p.Position}, Balance:${p.CurrentBalance}, CanAfford:{p.CanAffordBet})"));
            _logger.LogInformation("[RoomHub] Player balance details: {Players}", playerDetails);

            return roomInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoomHub] Error mapping room {RoomCode} to RoomInfoModel with auto-betting data", room.RoomCode);
            throw;
        }
    }

    #endregion
}