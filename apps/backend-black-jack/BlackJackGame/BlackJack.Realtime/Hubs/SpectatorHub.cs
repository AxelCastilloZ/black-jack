// BlackJack.Realtime/Hubs/SpectatorHub.cs - CORREGIDO: Usa SignalRNotificationService
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
public class SpectatorHub : BaseHub
{
    private readonly IGameRoomService _gameRoomService;
    private readonly IConnectionManager _connectionManager;
    private readonly ISignalRNotificationService _notificationService; // AGREGADO

    public SpectatorHub(
        IGameRoomService gameRoomService,
        IConnectionManager connectionManager,
        ISignalRNotificationService notificationService, // AGREGADO
        ILogger<SpectatorHub> logger) : base(logger)
    {
        _gameRoomService = gameRoomService;
        _connectionManager = connectionManager;
        _notificationService = notificationService; // AGREGADO
    }

    #region Spectator Management

    public async Task JoinAsViewer(JoinRoomRequest request)
    {
        try
        {
            _logger.LogInformation("[SpectatorHub] === JoinAsViewer STARTED ===");
            _logger.LogInformation("[SpectatorHub] RoomCode: {RoomCode}, PlayerName: {PlayerName}",
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

            _logger.LogInformation("[SpectatorHub] Player {PlayerId} joining room {RoomCode} as viewer",
                playerId, request.RoomCode);

            var roomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
            if (!roomResult.IsSuccess)
            {
                await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, "Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;

            // Join as viewer (isViewer = true)
            var joinResult = await _gameRoomService.JoinRoomAsync(request.RoomCode, playerId, request.PlayerName, true);

            if (!joinResult.IsSuccess)
            {
                _logger.LogError("[SpectatorHub] JoinAsViewer failed: {Error}", joinResult.Error);
                await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, joinResult.Error);
                return;
            }

            // Join SignalR groups (solo nativos, sin ConnectionManager)
            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(request.RoomCode);
            await JoinGroupAsync(roomGroupName);

            // Also join table group if exists
            if (room.BlackjackTableId.HasValue)
            {
                var tableGroupName = $"Table_{room.BlackjackTableId.Value}";
                await JoinGroupAsync(tableGroupName);
                _logger.LogInformation("[SpectatorHub] Also joined table group: {TableGroupName}", tableGroupName);
            }

            var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
            if (updatedRoomResult.IsSuccess)
            {
                var roomInfo = await MapToRoomInfoAsync(updatedRoomResult.Value!);

                // CORREGIDO: Usar notificationService
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

                // CORREGIDO: Usar notificationService para eventos coordinados
                await _notificationService.NotifyRoomExceptAsync(request.RoomCode, Context.ConnectionId,
                    HubMethodNames.ServerMethods.PlayerJoined, playerJoinedEvent);

                await _notificationService.NotifyRoomExceptAsync(request.RoomCode, Context.ConnectionId,
                    HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);

                _logger.LogInformation("[SpectatorHub] Player {PlayerId} joined room {RoomCode} as viewer successfully",
                    playerId, request.RoomCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SpectatorHub] Error in JoinAsViewer: {Error}", ex.Message);
            await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, "Error interno del servidor");
        }
    }

    public async Task JoinOrCreateRoomForTableAsViewer(string tableId, string playerName)
    {
        try
        {
            _logger.LogInformation("[SpectatorHub] === JoinOrCreateRoomForTableAsViewer STARTED ===");
            _logger.LogInformation("[SpectatorHub] TableId: {TableId}, PlayerName: {PlayerName}",
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
                _logger.LogWarning("[SpectatorHub] JoinOrCreateRoomForTableAsViewer - Invalid input data");
                await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, "Datos inválidos");
                return;
            }

            _logger.LogInformation("[SpectatorHub] Input validation passed");
            _logger.LogInformation("[SpectatorHub] Calling GameRoomService.JoinOrCreateRoomForTableAsync...");

            // Use the viewer-specific method
            var result = await _gameRoomService.JoinOrCreateRoomForTableAsViewerAsync(tableId, playerId, playerName);

            if (!result.IsSuccess)
            {
                _logger.LogError("[SpectatorHub] JoinOrCreateRoomForTableAsync FAILED: {Error}", result.Error);
                await _notificationService.SendErrorToConnectionAsync(Context.ConnectionId, result.Error);
                return;
            }

            var room = result.Value!;
            _logger.LogInformation("[SpectatorHub] Service SUCCESS - Room: {RoomCode} for table {TableId}",
                room.RoomCode, tableId);

            // Join SignalR groups (solo nativos)
            var tableGroupName = $"Table_{tableId}";
            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(room.RoomCode);

            _logger.LogInformation("[SpectatorHub] Joining SignalR groups - Table: {TableGroup}, Room: {RoomGroup}",
                tableGroupName, roomGroupName);

            await JoinGroupAsync(tableGroupName);
            await JoinGroupAsync(roomGroupName);

            // Get updated room info after join
            var roomInfoResult = await _gameRoomService.GetRoomAsync(room.RoomCode);
            if (roomInfoResult.IsSuccess)
            {
                var roomInfo = await MapToRoomInfoAsync(roomInfoResult.Value!);

                // Determine if the user created the room or joined an existing one
                var isNewRoom = room.HostPlayerId == playerId;
                var eventMethod = isNewRoom ? HubMethodNames.ServerMethods.RoomCreated : HubMethodNames.ServerMethods.RoomJoined;

                _logger.LogInformation("[SpectatorHub] Sending {EventMethod} event to caller (isNewRoom: {IsNewRoom})", eventMethod, isNewRoom);

                // CORREGIDO: Usar notificationService
                await _notificationService.NotifyConnectionAsync(Context.ConnectionId, eventMethod, roomInfo);

                // Only notify if joined existing room
                if (!isNewRoom)
                {
                    _logger.LogInformation("[SpectatorHub] Notifying other users about viewer join");

                    var playerJoinedEvent = new PlayerJoinedEventModel(
                        RoomCode: room.RoomCode,
                        PlayerId: playerId.Value,
                        PlayerName: playerName,
                        Position: -1,
                        TotalPlayers: roomInfo.PlayerCount,
                        Timestamp: DateTime.UtcNow
                    );

                    // CORREGIDO: Usar notificationService
                    await _notificationService.NotifyRoomExceptAsync(room.RoomCode, Context.ConnectionId,
                        HubMethodNames.ServerMethods.PlayerJoined, playerJoinedEvent);

                    await _notificationService.NotifyRoomExceptAsync(room.RoomCode, Context.ConnectionId,
                        HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);
                }

                _logger.LogInformation("[SpectatorHub] Player {PlayerId} joined/created room {RoomCode} as viewer successfully",
                    playerId, room.RoomCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SpectatorHub] EXCEPTION in JoinOrCreateRoomForTableAsViewer");
            await HandleExceptionAsync(ex, "JoinOrCreateRoomForTableAsViewer");
        }
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
            _logger.LogInformation("[SpectatorHub] Mapping room {RoomCode} to RoomInfoModel", room.RoomCode);

            _logger.LogInformation("[SpectatorHub] Using SeatPosition directly from database models");

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

            _logger.LogInformation("[SpectatorHub] RoomInfoModel created successfully for room {RoomCode} with {PlayerCount} players",
                room.RoomCode, roomInfo.PlayerCount);

            // NUEVO: Log detallado para debugging
            var playerDetails = string.Join(", ", roomInfo.Players.Select(p => $"{p.Name}(Pos:{p.Position})"));
            _logger.LogInformation("[SpectatorHub] Player details: {Players}", playerDetails);

            return roomInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SpectatorHub] Error mapping room {RoomCode} to RoomInfoModel", room.RoomCode);
            throw;
        }
    }

    #endregion
}