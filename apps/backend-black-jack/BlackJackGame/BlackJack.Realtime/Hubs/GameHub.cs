// GameHub.cs - En BlackJack.Realtime/Hubs/ - CORREGIDO CON GUID
using BlackJack.Domain.Enums;
using BlackJack.Domain.Models.Game;
using BlackJack.Realtime.Models;
using BlackJack.Realtime.Services;
using BlackJack.Services.Common;
using BlackJack.Services.Game;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;

namespace BlackJack.Realtime.Hubs;
[Authorize]
public class GameHub : BaseHub
{
    private readonly IGameRoomService _gameRoomService;
    private readonly IGameService _gameService;
    private readonly IConnectionManager _connectionManager;
    private readonly ISignalRNotificationService _notificationService;

    public GameHub(
        IGameRoomService gameRoomService,
        IGameService gameService,
        IConnectionManager connectionManager,
        ISignalRNotificationService notificationService,
        ILogger<GameHub> logger) : base(logger)
    {
        _gameRoomService = gameRoomService;
        _gameService = gameService;
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
            await SendSuccessAsync("Conectado exitosamente al hub de juego");
        }
        else
        {
            await SendErrorAsync("Error de autenticación");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var playerId = GetCurrentPlayerId();

        if (playerId != null)
        {
            // Buscar la sala actual del jugador
            var currentRoomResult = await _gameRoomService.GetPlayerCurrentRoomCodeAsync(playerId);
            if (currentRoomResult.IsSuccess && !string.IsNullOrEmpty(currentRoomResult.Value))
            {
                // Opcional: manejar desconexión temporal vs permanente
                _logger.LogInformation("[GameHub] Player {PlayerId} disconnected from room {RoomCode}",
                    playerId, currentRoomResult.Value);

                // En una implementación completa, podrías implementar un timeout
                // antes de remover al jugador automáticamente
            }

            await _connectionManager.RemoveConnectionAsync(Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    #endregion

    #region Room Management

    public async Task CreateRoom(CreateRoomRequest request)
    {
        try
        {
            if (!IsAuthenticated())
            {
                await SendErrorAsync("Debes estar autenticado para crear una sala");
                return;
            }

            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

            if (!ValidateInput(request.RoomName, nameof(request.RoomName), 50))
            {
                await SendErrorAsync("Nombre de sala inválido");
                return;
            }

            _logger.LogInformation("[GameHub] Creating room {RoomName} for player {PlayerId}",
                request.RoomName, playerId);

            var result = await _gameRoomService.CreateRoomAsync(request.RoomName, playerId);

            if (result.IsSuccess)
            {
                var room = result.Value!;

                // Unirse al grupo de la sala
                await JoinGroupAsync(HubMethodNames.Groups.GetRoomGroup(room.RoomCode));

                var roomInfo = MapToRoomInfo(room);
                await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.RoomCreated,
                    SignalRResponse<RoomInfoModel>.Ok(roomInfo));

                _logger.LogInformation("[GameHub] Room {RoomCode} created successfully", room.RoomCode);
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

            // Verificar que la sala existe
            var roomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync("Sala no encontrada");
                return;
            }

            // Unirse al grupo antes de la lógica de negocio
            await JoinGroupAsync(HubMethodNames.Groups.GetRoomGroup(request.RoomCode));

            // Lógica de negocio
            var joinResult = await _gameRoomService.JoinRoomAsync(request.RoomCode, playerId, request.PlayerName);

            if (joinResult.IsSuccess)
            {
                // Obtener estado actualizado de la sala
                var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
                if (updatedRoomResult.IsSuccess)
                {
                    var roomInfo = MapToRoomInfo(updatedRoomResult.Value!);
                    await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.RoomJoined,
                        SignalRResponse<RoomInfoModel>.Ok(roomInfo));
                }

                _logger.LogInformation("[GameHub] Player {PlayerId} joined room {RoomCode} successfully",
                    playerId, request.RoomCode);
            }
            else
            {
                // Salir del grupo si falló
                await LeaveGroupAsync(HubMethodNames.Groups.GetRoomGroup(request.RoomCode));
                await SendErrorAsync(joinResult.Error);
            }
        }
        catch (Exception ex)
        {
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

            _logger.LogInformation("[GameHub] Player {PlayerId} leaving room {RoomCode}",
                playerId, roomCode);

            // Salir del grupo
            await LeaveGroupAsync(HubMethodNames.Groups.GetRoomGroup(roomCode));

            // Lógica de negocio
            var result = await _gameRoomService.LeaveRoomAsync(roomCode, playerId);

            if (result.IsSuccess)
            {
                await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.RoomLeft,
                    SignalRResponse.Ok("Has salido de la sala exitosamente"));

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
                var roomInfo = MapToRoomInfo(result.Value!);
                await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.RoomInfo,
                    SignalRResponse<RoomInfoModel>.Ok(roomInfo));
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
                // El evento GameStarted se enviará automáticamente por el event handler
                _logger.LogInformation("[GameHub] Game started successfully in room {RoomCode}", roomCode);
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

    public async Task PlaceBet(PlaceBetRequest request)
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

            if (!ValidateInput(request.RoomCode, nameof(request.RoomCode)) || request.Amount <= 0)
            {
                await SendErrorAsync("Datos de apuesta inválidos");
                return;
            }

            _logger.LogInformation("[GameHub] Player {PlayerId} placing bet {Amount} in room {RoomCode}",
                playerId, request.Amount, request.RoomCode);

            // Obtener la sala para verificar la mesa asociada
            var roomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
            if (!roomResult.IsSuccess || !roomResult.Value!.BlackjackTableId.HasValue)
            {
                await SendErrorAsync("Sala o mesa no encontrada");
                return;
            }

            var tableId = roomResult.Value.BlackjackTableId.Value;
            var bet = BlackJack.Domain.Models.Betting.Bet.Create(request.Amount);

            var result = await _gameService.PlaceBetAsync(tableId, playerId, bet);

            if (result.IsSuccess)
            {
                _logger.LogInformation("[GameHub] Bet placed successfully by {PlayerId}", playerId);
            }
            else
            {
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "PlaceBet");
        }
    }

    public async Task PlayerAction(PlayerActionRequest request)
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

            if (!ValidateInput(request.RoomCode, nameof(request.RoomCode)) ||
                !ValidateInput(request.Action, nameof(request.Action)))
            {
                await SendErrorAsync("Datos de acción inválidos");
                return;
            }

            if (!Enum.TryParse<PlayerAction>(request.Action, true, out var playerAction))
            {
                await SendErrorAsync("Acción no válida");
                return;
            }

            _logger.LogInformation("[GameHub] Player {PlayerId} performing action {Action} in room {RoomCode}",
                playerId, request.Action, request.RoomCode);

            // Obtener la sala para verificar la mesa asociada
            var roomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
            if (!roomResult.IsSuccess || !roomResult.Value!.BlackjackTableId.HasValue)
            {
                await SendErrorAsync("Sala o mesa no encontrada");
                return;
            }

            var tableId = roomResult.Value.BlackjackTableId.Value;

            var result = await _gameService.PlayerActionAsync(tableId, playerId, playerAction);

            if (result.IsSuccess)
            {
                _logger.LogInformation("[GameHub] Action {Action} performed successfully by {PlayerId}",
                    request.Action, playerId);
            }
            else
            {
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "PlayerAction");
        }
    }

    #endregion

    #region Utility Methods

    private static RoomInfoModel MapToRoomInfo(BlackJack.Domain.Models.Game.GameRoom room)
    {
        return new RoomInfoModel(
            RoomCode: room.RoomCode,
            Name: room.Name,
            Status: room.Status.ToString(),
            PlayerCount: room.PlayerCount,
            MaxPlayers: room.MaxPlayers,
            Players: room.Players.Select(p => new RoomPlayerModel(
                PlayerId: p.PlayerId.Value,
                Name: p.Name,
                Position: p.Position,
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
    }

    #endregion
}