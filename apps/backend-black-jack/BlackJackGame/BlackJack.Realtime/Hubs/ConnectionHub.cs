// BlackJack.Realtime/Hubs/ConnectionHub.cs - Hub especializado en gestión de conexiones y reconexión
using BlackJack.Domain.Models.Users;
using BlackJack.Realtime.Models;
using BlackJack.Realtime.Services;
using BlackJack.Services.Game;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlackJack.Realtime.Hubs;

[AllowAnonymous]
public class ConnectionHub : BaseHub
{
    private readonly IConnectionManager _connectionManager;
    private readonly IGameRoomService _gameRoomService;

    public ConnectionHub(
        IConnectionManager connectionManager,
        IGameRoomService gameRoomService,
        ILogger<ConnectionHub> logger) : base(logger)
    {
        _connectionManager = connectionManager;
        _gameRoomService = gameRoomService;
    }

    #region Connection Management

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        var playerId = GetCurrentPlayerId();
        var userName = GetCurrentUserName();

        _logger.LogInformation("[ConnectionHub] Player {PlayerId} ({UserName}) connected with ConnectionId {ConnectionId}",
            playerId, userName, Context.ConnectionId);

        // Registrar conexión en ConnectionManager
        if (playerId != null && userName != null)
        {
            await _connectionManager.AddConnectionAsync(Context.ConnectionId, playerId, userName);

            // Verificar reconexión automática
            await HandleAutoReconnectionAsync(playerId);
        }

        await SendSuccessAsync("Conectado exitosamente al hub de juego");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var playerId = GetCurrentPlayerId();

        _logger.LogInformation("[ConnectionHub] Player {PlayerId} disconnecting from ConnectionId {ConnectionId}",
            playerId, Context.ConnectionId);

        // Guardar información de reconexión antes de desconectar
        if (playerId != null)
        {
            await SaveReconnectionInfoAsync(playerId);
        }

        // Remover del ConnectionManager
        await _connectionManager.RemoveConnectionAsync(Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    #endregion

    #region Auto Reconnection

    /// <summary>
    /// Maneja la reconexión automática del jugador a su sala anterior
    /// </summary>
    private async Task HandleAutoReconnectionAsync(PlayerId playerId)
    {
        try
        {
            _logger.LogInformation("[ConnectionHub] === AUTO RECONNECTION CHECK for player {PlayerId} ===", playerId);

            // Verificar si el jugador tiene información de reconexión
            var reconnectionInfo = await _connectionManager.GetReconnectionInfoAsync(playerId);
            if (reconnectionInfo != null && !string.IsNullOrEmpty(reconnectionInfo.LastRoomCode))
            {
                _logger.LogInformation("[ConnectionHub] Found reconnection info for player {PlayerId} - Last room: {RoomCode}",
                    playerId, reconnectionInfo.LastRoomCode);

                // Verificar si la sala aún existe y el jugador sigue siendo miembro
                var roomResult = await _gameRoomService.GetRoomAsync(reconnectionInfo.LastRoomCode);
                if (roomResult.IsSuccess)
                {
                    var room = roomResult.Value!;

                    if (room.IsPlayerInRoom(playerId))
                    {
                        _logger.LogInformation("[ConnectionHub] AUTO-RECONNECTING player {PlayerId} to room {RoomCode}",
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

                        _logger.LogInformation("[ConnectionHub] ✅ Successfully auto-reconnected player {PlayerId} to room {RoomCode}",
                            playerId, reconnectionInfo.LastRoomCode);
                    }
                    else
                    {
                        _logger.LogInformation("[ConnectionHub] Player {PlayerId} no longer member of room {RoomCode} - clearing reconnection info",
                            playerId, reconnectionInfo.LastRoomCode);
                        await _connectionManager.ClearReconnectionInfoAsync(playerId);
                    }
                }
                else
                {
                    _logger.LogInformation("[ConnectionHub] Room {RoomCode} no longer exists - clearing reconnection info for player {PlayerId}",
                        reconnectionInfo.LastRoomCode, playerId);
                    await _connectionManager.ClearReconnectionInfoAsync(playerId);
                }
            }
            else
            {
                _logger.LogInformation("[ConnectionHub] No reconnection info found for player {PlayerId}", playerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionHub] Error during auto-reconnection for player {PlayerId}: {Error}",
                playerId, ex.Message);
        }
    }

    /// <summary>
    /// Guarda información para reconexión posterior
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

                _logger.LogInformation("[ConnectionHub] Saved reconnection info for player {PlayerId} in room {RoomCode}",
                    playerId, currentRoomResult.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionHub] Error saving reconnection info for player {PlayerId}: {Error}",
                playerId, ex.Message);
        }
    }

    #endregion

    #region Connection Status Methods

    /// <summary>
    /// Obtiene el estado de conexión del jugador
    /// </summary>
    public async Task GetConnectionStatus()
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

            var connections = await _connectionManager.GetConnectionsForPlayerAsync(playerId);
            var groups = await _connectionManager.GetGroupsForConnectionAsync(Context.ConnectionId);
            var roomState = await _connectionManager.GetPlayerRoomStateAsync(playerId);

            var status = new
            {
                PlayerId = playerId.Value,
                ConnectionId = Context.ConnectionId,
                ActiveConnections = connections.Count,
                Groups = groups,
                RoomState = roomState,
                IsOnline = await _connectionManager.IsPlayerOnlineAsync(playerId),
                Timestamp = DateTime.UtcNow
            };

            await Clients.Caller.SendAsync("ConnectionStatus", status);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "GetConnectionStatus");
        }
    }

    /// <summary>
    /// Fuerza la limpieza de información de reconexión
    /// </summary>
    public async Task ClearReconnectionInfo()
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

            await _connectionManager.ClearReconnectionInfoAsync(playerId);
            await SendSuccessAsync("Información de reconexión limpiada");
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "ClearReconnectionInfo");
        }
    }

    #endregion

    #region Test Methods

    [AllowAnonymous]
    public async Task TestConnection()
    {
        _logger.LogInformation("[ConnectionHub] TestConnection called");
        await Clients.Caller.SendAsync("TestResponse", new
        {
            message = "SignalR funcionando",
            timestamp = DateTime.UtcNow,
            connectionId = Context.ConnectionId,
            playerId = GetCurrentPlayerId()?.Value
        });
    }

    /// <summary>
    /// Obtiene estadísticas del ConnectionManager (solo para debug)
    /// </summary>
    [AllowAnonymous]
    public async Task GetConnectionStats()
    {
        try
        {
            var stats = await _connectionManager.GetStatsAsync();
            await Clients.Caller.SendAsync("ConnectionStats", stats);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "GetConnectionStats");
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Mapea GameRoom a RoomInfoModel (copiado del GameHub original)
    /// </summary>
    private async Task<RoomInfoModel> MapToRoomInfoAsync(BlackJack.Domain.Models.Game.GameRoom room)
    {
        try
        {
            _logger.LogInformation("[ConnectionHub] Mapping room {RoomCode} to RoomInfoModel", room.RoomCode);

            var roomInfo = new RoomInfoModel(
                RoomCode: room.RoomCode,
                Name: room.Name,
                Status: room.Status.ToString(),
                PlayerCount: room.PlayerCount,
                MaxPlayers: room.MaxPlayers,
                Players: room.Players.Select(p => new RoomPlayerModel(
                    PlayerId: p.PlayerId.Value,
                    Name: p.Name,
                    Position: p.GetSeatPosition(),
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

            _logger.LogInformation("[ConnectionHub] RoomInfoModel created successfully for room {RoomCode} with {PlayerCount} players",
                room.RoomCode, roomInfo.PlayerCount);

            return roomInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConnectionHub] Error mapping room {RoomCode} to RoomInfoModel", room.RoomCode);
            throw;
        }
    }

    #endregion
}