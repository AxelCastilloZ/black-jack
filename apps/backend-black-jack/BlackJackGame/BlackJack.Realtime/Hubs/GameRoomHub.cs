// BlackJack.Realtime/Hubs/GameRoomHub.cs - Gestión de salas y asientos
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

        if (playerId != null && userName != null)
        {
            await _connectionManager.AddConnectionAsync(Context.ConnectionId, playerId, userName);

            // Limpieza automática de datos fantasma al conectar
            try
            {
                var cleanupResult = await _gameRoomService.ForceCleanupPlayerAsync(playerId);
                if (cleanupResult.IsSuccess && cleanupResult.Value > 0)
                {
                    _logger.LogInformation("[GameRoomHub] Cleaned up {Count} orphan records for player {PlayerId}",
                        cleanupResult.Value, playerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GameRoomHub] Error during cleanup for player {PlayerId}", playerId);
            }

            await SendSuccessAsync("Conectado al hub de sala");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _connectionManager.RemoveConnectionAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    #endregion

    #region Room Management

    public async Task CreateRoom(CreateRoomRequest request)
    {
        try
        {
            var playerId = await ValidateAuthenticationAsync();
            if (playerId == null) return;

            if (!ValidateInput(request.RoomName, nameof(request.RoomName), 50))
            {
                await SendErrorAsync("Nombre de sala inválido");
                return;
            }

            _logger.LogInformation("[GameRoomHub] Creating room {RoomName} for player {PlayerId}",
                request.RoomName, playerId);

            var result = await _gameRoomService.CreateRoomAsync(request.RoomName, playerId);

            if (result.IsSuccess)
            {
                var room = result.Value!;
                var roomGroupName = HubMethodNames.Groups.GetRoomGroup(room.RoomCode);

                await JoinGroupAsync(roomGroupName);
                await _connectionManager.AddToGroupAsync(Context.ConnectionId, roomGroupName);

                var roomInfo = await MapToRoomInfoAsync(room);
                await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.RoomCreated, roomInfo);

                _logger.LogInformation("[GameRoomHub] Room {RoomCode} created successfully", room.RoomCode);
            }
            else
            {
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "CreateRoom");
        }
    }

    public async Task JoinRoom(JoinRoomRequest request)
    {
        try
        {
            var playerId = await ValidateAuthenticationAsync();
            if (playerId == null) return;

            if (!ValidateInput(request.RoomCode, nameof(request.RoomCode)) ||
                !ValidateInput(request.PlayerName, nameof(request.PlayerName), 30))
            {
                await SendErrorAsync("Datos de entrada inválidos");
                return;
            }

            var joinResult = await _gameRoomService.JoinRoomAsync(request.RoomCode, playerId, request.PlayerName);

            if (!joinResult.IsSuccess)
            {
                await SendErrorAsync(joinResult.Error);
                return;
            }

            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(request.RoomCode);
            await JoinGroupAsync(roomGroupName);
            await _connectionManager.AddToGroupAsync(Context.ConnectionId, roomGroupName);

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
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "JoinRoom");
        }
    }

    public async Task JoinOrCreateRoomForTable(string tableId, string playerName)
    {
        try
        {
            var playerId = await ValidateAuthenticationAsync();
            if (playerId == null) return;

            if (!ValidateInput(tableId, nameof(tableId)) ||
                !ValidateInput(playerName, nameof(playerName), 30))
            {
                await SendErrorAsync("Datos inválidos");
                return;
            }

            _logger.LogInformation("[GameRoomHub] Player {PlayerId} joining/creating room for table {TableId}",
                playerId, tableId);

            var result = await _gameRoomService.JoinOrCreateRoomForTableAsync(tableId, playerId, playerName);

            if (!result.IsSuccess)
            {
                await SendErrorAsync(result.Error);
                return;
            }

            var room = result.Value!;
            var tableGroupName = HubMethodNames.Groups.GetTableGroup(tableId);
            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(room.RoomCode);

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

                await Clients.Caller.SendAsync(eventMethod, roomInfo);

                if (!isNewRoom)
                {
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

                _logger.LogInformation("[GameRoomHub] Successfully {Action} room {RoomCode} for table {TableId}",
                    isNewRoom ? "created" : "joined", room.RoomCode, tableId);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "JoinOrCreateRoomForTable");
        }
    }

    public async Task LeaveRoom(string roomCode)
    {
        try
        {
            var playerId = await ValidateAuthenticationAsync();
            if (playerId == null) return;

            if (!ValidateInput(roomCode, nameof(roomCode)))
            {
                await SendErrorAsync("Código de sala inválido");
                return;
            }

            // Salir de grupos de SignalR
            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(roomCode);
            await LeaveGroupAsync(roomGroupName);
            await _connectionManager.RemoveFromGroupAsync(Context.ConnectionId, roomGroupName);

            var result = await _gameRoomService.LeaveRoomAsync(roomCode, playerId);

            if (result.IsSuccess)
            {
                await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.RoomLeft,
                    new { message = "Has salido de la sala exitosamente" });

                var userName = GetCurrentUserName();
                var playerLeftEvent = new PlayerLeftEventModel(
                    RoomCode: roomCode,
                    PlayerId: playerId.Value,
                    PlayerName: userName,
                    RemainingPlayers: 0,
                    Timestamp: DateTime.UtcNow
                );

                await _notificationService.NotifyPlayerLeftAsync(roomCode, playerLeftEvent);
                await _connectionManager.ClearReconnectionInfoAsync(playerId);

                _logger.LogInformation("[GameRoomHub] Player {PlayerId} left room {RoomCode} successfully",
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
            var playerId = await ValidateAuthenticationAsync();
            if (playerId == null) return;

            if (!ValidateInput(request.RoomCode, nameof(request.RoomCode)) ||
                request.Position < 0 || request.Position > 5)
            {
                await SendErrorAsync("Datos inválidos");
                return;
            }

            _logger.LogInformation("[GameRoomHub] Player {PlayerId} joining seat {Position} in room {RoomCode}",
                playerId, request.Position, request.RoomCode);

            var result = await _gameRoomService.JoinSeatAsync(request.RoomCode, playerId, request.Position);

            if (result.IsSuccess)
            {
                var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
                if (updatedRoomResult.IsSuccess)
                {
                    var roomInfo = await MapToRoomInfoAsync(updatedRoomResult.Value!);

                    await _notificationService.NotifyRoomInfoUpdatedAsync(request.RoomCode, roomInfo);
                    await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.SeatJoined,
                        new { Position = request.Position, RoomInfo = roomInfo });

                    _logger.LogInformation("[GameRoomHub] Player {PlayerId} joined seat {Position} successfully",
                        playerId, request.Position);
                }
            }
            else
            {
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "JoinSeat");
        }
    }

    public async Task LeaveSeat(LeaveSeatRequest request)
    {
        try
        {
            var playerId = await ValidateAuthenticationAsync();
            if (playerId == null) return;

            if (!ValidateInput(request.RoomCode, nameof(request.RoomCode)))
            {
                await SendErrorAsync("Código de sala inválido");
                return;
            }

            var result = await _gameRoomService.LeaveSeatAsync(request.RoomCode, playerId);

            if (result.IsSuccess)
            {
                var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
                if (updatedRoomResult.IsSuccess)
                {
                    var roomInfo = await MapToRoomInfoAsync(updatedRoomResult.Value!);

                    await _notificationService.NotifyRoomInfoUpdatedAsync(request.RoomCode, roomInfo);
                    await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.SeatLeft, roomInfo);

                    _logger.LogInformation("[GameRoomHub] Player {PlayerId} left seat successfully", playerId);
                }
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

    #endregion

    #region Spectator Management

    public async Task JoinAsViewer(JoinRoomRequest request)
    {
        try
        {
            var playerId = await ValidateAuthenticationAsync();
            if (playerId == null) return;

            if (!ValidateInput(request.RoomCode, nameof(request.RoomCode)) ||
                !ValidateInput(request.PlayerName, nameof(request.PlayerName), 30))
            {
                await SendErrorAsync("Datos de entrada inválidos");
                return;
            }

            var joinResult = await _gameRoomService.JoinRoomAsync(request.RoomCode, playerId, request.PlayerName, true);

            if (!joinResult.IsSuccess)
            {
                await SendErrorAsync(joinResult.Error);
                return;
            }

            var roomGroupName = HubMethodNames.Groups.GetRoomGroup(request.RoomCode);
            await JoinGroupAsync(roomGroupName);

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

                _logger.LogInformation("[GameRoomHub] Player {PlayerId} joined as viewer successfully", playerId);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "JoinAsViewer");
        }
    }

    #endregion

    #region Test Methods

    [AllowAnonymous]
    public async Task TestConnection()
    {
        var response = new
        {
            message = "GameRoomHub funcionando",
            timestamp = DateTime.UtcNow,
            connectionId = Context.ConnectionId,
            playerId = GetCurrentPlayerId()?.Value
        };

        await Clients.Caller.SendAsync("TestResponse", response);
    }

    #endregion

    #region Utility Methods

    private async Task<RoomInfoModel> MapToRoomInfoAsync(BlackJack.Domain.Models.Game.GameRoom room)
    {
        try
        {
            var seatedPlayersCount = room.Players.Count(p => p.IsSeated);
            var isAutoBettingActive = room.MinBetPerRound?.Amount > 0 && seatedPlayersCount > 0;

            var seatedPlayers = room.Players.Where(p => p.IsSeated).ToList();
            var effectiveHostId = seatedPlayers.Any() && !seatedPlayers.Any(p => p.PlayerId.Value == room.HostPlayerId.Value)
                ? seatedPlayers.First().PlayerId
                : room.HostPlayerId;

            return new RoomInfoModel(
                RoomCode: room.RoomCode,
                Name: room.Name,
                Status: room.Status.ToString(),
                PlayerCount: seatedPlayersCount,
                MaxPlayers: room.MaxPlayers,
                MinBetPerRound: room.MinBetPerRound?.Amount ?? 0,
                AutoBettingActive: isAutoBettingActive,
                Players: seatedPlayers.Select(p => new RoomPlayerModel(
                    PlayerId: p.PlayerId.Value,
                    Name: p.Name,
                    Position: p.GetSeatPosition(),
                    IsReady: p.IsReady,
                    IsHost: effectiveHostId.Value == p.PlayerId.Value,
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
                CanStart: seatedPlayersCount >= 1,
                CreatedAt: room.CreatedAt
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomHub] Error mapping room {RoomCode} to RoomInfoModel", room.RoomCode);
            throw;
        }
    }

    #endregion
}