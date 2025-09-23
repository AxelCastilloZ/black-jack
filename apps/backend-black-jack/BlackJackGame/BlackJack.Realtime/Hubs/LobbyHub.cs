// BlackJack.Realtime/Hubs/LobbyHub.cs - Lobby y lista de salas
using BlackJack.Realtime.Models;
using BlackJack.Realtime.Services;
using BlackJack.Services.Game;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlackJack.Realtime.Hubs;

[AllowAnonymous]
public class LobbyHub : BaseHub
{
    private readonly IGameRoomService _gameRoomService;
    private readonly IConnectionManager _connectionManager;
    private readonly ISignalRNotificationService _notificationService;

    public LobbyHub(
        IGameRoomService gameRoomService,
        IConnectionManager connectionManager,
        ISignalRNotificationService notificationService,
        ILogger<LobbyHub> logger) : base(logger)
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

            // Unirse al grupo del lobby automáticamente
            await JoinGroupAsync(HubMethodNames.Groups.LobbyGroup);
            await _connectionManager.AddToGroupAsync(Context.ConnectionId, HubMethodNames.Groups.LobbyGroup);

            await SendSuccessAsync("Conectado al lobby exitosamente");

            // Enviar lista de salas activas inmediatamente
            await SendActiveRoomsToClient();
        }
        else
        {
            await SendErrorAsync("Error de autenticación en lobby");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await LeaveGroupAsync(HubMethodNames.Groups.LobbyGroup);
        await _connectionManager.RemoveFromGroupAsync(Context.ConnectionId, HubMethodNames.Groups.LobbyGroup);
        await _connectionManager.RemoveConnectionAsync(Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    #endregion

    #region Lobby Methods

    public async Task JoinLobby()
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

            _logger.LogInformation("[LobbyHub] Player {PlayerId} joining lobby", playerId);

            await JoinGroupAsync(HubMethodNames.Groups.LobbyGroup);
            await _connectionManager.AddToGroupAsync(Context.ConnectionId, HubMethodNames.Groups.LobbyGroup);

            await SendSuccessAsync("Te has unido al lobby");
            await SendActiveRoomsToClient();
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "JoinLobby");
        }
    }

    public async Task LeaveLobby()
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            _logger.LogInformation("[LobbyHub] Player {PlayerId} leaving lobby", playerId);

            await LeaveGroupAsync(HubMethodNames.Groups.LobbyGroup);
            await _connectionManager.RemoveFromGroupAsync(Context.ConnectionId, HubMethodNames.Groups.LobbyGroup);

            await SendSuccessAsync("Has salido del lobby");
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "LeaveLobby");
        }
    }

    public async Task GetActiveRooms()
    {
        try
        {
            await SendActiveRoomsToClient();
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "GetActiveRooms");
        }
    }

    public async Task RefreshRooms()
    {
        try
        {
            _logger.LogInformation("[LobbyHub] Refreshing room list for connection {ConnectionId}",
                Context.ConnectionId);
            await SendActiveRoomsToClient();
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "RefreshRooms");
        }
    }

    #endregion

    #region Quick Join Methods

    public async Task QuickJoin(string? preferredRoomCode = null)
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            var userName = GetCurrentUserName();

            if (playerId == null || userName == null)
            {
                await SendErrorAsync("Error obteniendo información del jugador");
                return;
            }

            _logger.LogInformation("[LobbyHub] Player {PlayerId} attempting quick join", playerId);

            // Verificar si ya está en una sala
            var currentRoomResult = await _gameRoomService.GetPlayerCurrentRoomCodeAsync(playerId);
            if (currentRoomResult.IsSuccess && !string.IsNullOrEmpty(currentRoomResult.Value))
            {
                await SendErrorAsync("Ya estás en una sala. Sal de esa sala primero.");
                return;
            }

            // Intentar unirse a la sala preferida si se especifica
            if (!string.IsNullOrEmpty(preferredRoomCode))
            {
                var preferredRoomResult = await _gameRoomService.GetRoomAsync(preferredRoomCode);
                if (preferredRoomResult.IsSuccess && !preferredRoomResult.Value!.IsFull)
                {
                    await Clients.Caller.SendAsync("QuickJoinRedirect", new { roomCode = preferredRoomCode });
                    return;
                }
            }

            // Buscar una sala disponible
            var activeRoomsResult = await _gameRoomService.GetActiveRoomsAsync();
            if (activeRoomsResult.IsSuccess)
            {
                var availableRoom = activeRoomsResult.Value!
                    .Where(r => !r.IsFull && r.Status == BlackJack.Domain.Models.Game.RoomStatus.WaitingForPlayers)
                    .OrderBy(r => r.PlayerCount) // Preferir salas con menos jugadores
                    .FirstOrDefault();

                if (availableRoom != null)
                {
                    await Clients.Caller.SendAsync("QuickJoinRedirect", new { roomCode = availableRoom.RoomCode });
                }
                else
                {
                    await SendErrorAsync("No hay salas disponibles. Crea una nueva sala.");
                }
            }
            else
            {
                await SendErrorAsync("Error obteniendo salas disponibles");
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "QuickJoin");
        }
    }

    public async Task QuickJoinTable(string tableId)
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            var userName = GetCurrentUserName();

            if (playerId == null || userName == null)
            {
                await SendErrorAsync("Error obteniendo información del jugador");
                return;
            }

            if (!ValidateInput(tableId, nameof(tableId)))
            {
                await SendErrorAsync("ID de mesa inválido");
                return;
            }

            _logger.LogInformation("[LobbyHub] Player {PlayerId} attempting quick join to table {TableId}",
                playerId, tableId);

            // Verificar si ya está en una sala
            var currentRoomResult = await _gameRoomService.GetPlayerCurrentRoomCodeAsync(playerId);
            if (currentRoomResult.IsSuccess && !string.IsNullOrEmpty(currentRoomResult.Value))
            {
                await SendErrorAsync("Ya estás en una sala. Sal de esa sala primero.");
                return;
            }

            // Redirigir al GameRoomHub para manejar la lógica de unión/creación
            await Clients.Caller.SendAsync("QuickJoinTableRedirect", new
            {
                tableId = tableId,
                playerName = userName
            });

            _logger.LogInformation("[LobbyHub] Redirected player {PlayerId} to join table {TableId}",
                playerId, tableId);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "QuickJoinTable");
        }
    }

    #endregion

    #region Statistics Methods

    public async Task GetLobbyStats()
    {
        try
        {
            var activeRoomsResult = await _gameRoomService.GetActiveRoomsAsync();
            var onlinePlayerCount = await _connectionManager.GetOnlinePlayerCountAsync();
            var totalConnections = await _connectionManager.GetTotalConnectionCountAsync();

            var stats = new
            {
                OnlinePlayers = onlinePlayerCount,
                TotalConnections = totalConnections,
                ActiveRooms = activeRoomsResult.IsSuccess ? activeRoomsResult.Value!.Count : 0,
                PlayersInGame = activeRoomsResult.IsSuccess ?
                    activeRoomsResult.Value!
                        .Where(r => r.Status == BlackJack.Domain.Models.Game.RoomStatus.InProgress)
                        .Sum(r => r.PlayerCount) : 0,
                AvailableRooms = activeRoomsResult.IsSuccess ?
                    activeRoomsResult.Value!
                        .Count(r => !r.IsFull && r.Status == BlackJack.Domain.Models.Game.RoomStatus.WaitingForPlayers) : 0,
                Timestamp = DateTime.UtcNow
            };

            await Clients.Caller.SendAsync("LobbyStats", stats);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "GetLobbyStats");
        }
    }

    public async Task GetRoomDetails(string roomCode)
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
                var room = result.Value!;
                var detailedInfo = new
                {
                    RoomCode = room.RoomCode,
                    Name = room.Name,
                    Status = room.Status.ToString(),
                    PlayerCount = room.PlayerCount,
                    MaxPlayers = room.MaxPlayers,
                    HostPlayerId = room.HostPlayerId.Value,
                    CurrentPlayerTurn = room.CurrentPlayer?.Name,
                    CanStart = room.CanStart,
                    IsGameInProgress = room.IsGameInProgress,
                    BlackjackTableId = room.BlackjackTableId,
                    MinBetPerRound = room.MinBetPerRound?.Amount ?? 0,
                    CreatedAt = room.CreatedAt,
                    UpdatedAt = room.UpdatedAt,
                    Players = room.Players.Select(p => new
                    {
                        PlayerId = p.PlayerId.Value,
                        Name = p.Name,
                        Position = p.Position,
                        IsReady = p.IsReady,
                        IsSeated = p.IsSeated,
                        HasPlayedTurn = p.HasPlayedTurn,
                        JoinedAt = p.JoinedAt
                    }).ToList(),
                    Spectators = room.Spectators.Select(s => new
                    {
                        PlayerId = s.PlayerId.Value,
                        Name = s.Name,
                        JoinedAt = s.JoinedAt
                    }).ToList()
                };

                await Clients.Caller.SendAsync("DetailedRoomInfo", detailedInfo);
            }
            else
            {
                await SendErrorAsync(result.Error);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "GetRoomDetails");
        }
    }

    #endregion

    #region Test Methods

    public async Task TestConnection()
    {
        var response = new
        {
            message = "LobbyHub funcionando correctamente",
            timestamp = DateTime.UtcNow,
            connectionId = Context.ConnectionId,
            playerId = GetCurrentPlayerId()?.Value,
            capabilities = new[] { "QuickJoin", "GetActiveRooms", "GetLobbyStats" }
        };

        await Clients.Caller.SendAsync("TestResponse", response);
    }

    #endregion

    #region Private Helper Methods

    private async Task SendActiveRoomsToClient()
    {
        try
        {
            var result = await _gameRoomService.GetActiveRoomsAsync();

            if (result.IsSuccess)
            {
                var activeRooms = result.Value!.Select(room => new ActiveRoomModel(
                    RoomCode: room.RoomCode,
                    Name: room.Name,
                    PlayerCount: room.PlayerCount,
                    MaxPlayers: room.MaxPlayers,
                    Status: room.Status.ToString(),
                    CreatedAt: room.CreatedAt
                )).ToList();

                await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.ActiveRoomsUpdated, activeRooms);

                _logger.LogDebug("[LobbyHub] Sent {RoomCount} active rooms to connection {ConnectionId}",
                    activeRooms.Count, Context.ConnectionId);
            }
            else
            {
                await SendErrorAsync("Error obteniendo salas activas");
                _logger.LogWarning("[LobbyHub] Failed to get active rooms: {Error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LobbyHub] Error sending active rooms to client: {Error}", ex.Message);
            await SendErrorAsync("Error interno del servidor");
        }
    }

    #endregion
}