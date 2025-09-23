// BlackJack.Realtime/Hubs/SeatHub.cs - CORREGIDO CON auto-betting integrado
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
public class SeatHub : BaseHub
{
    private readonly IGameRoomService _gameRoomService;
    private readonly ISignalRNotificationService _notificationService;

    public SeatHub(
        IGameRoomService gameRoomService,
        ISignalRNotificationService notificationService,
        ILogger<SeatHub> logger) : base(logger)
    {
        _gameRoomService = gameRoomService;
        _notificationService = notificationService;
    }

    #region Seat Management

    public async Task JoinSeat(JoinSeatRequest request)
    {
        try
        {
            _logger.LogInformation("[SeatHub] ===== JoinSeat STARTED =====");
            _logger.LogInformation("[SeatHub] RoomCode: {RoomCode}, Position: {Position}",
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

            _logger.LogInformation("[SeatHub] Player {PlayerId} attempting to join seat {Position} in room {RoomCode}",
                playerId, request.Position, request.RoomCode);

            var result = await _gameRoomService.JoinSeatAsync(request.RoomCode, playerId, request.Position);

            if (result.IsSuccess)
            {
                _logger.LogInformation("[SeatHub] JoinSeat SUCCESS - Getting updated room info...");

                var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
                if (updatedRoomResult.IsSuccess)
                {
                    var updatedRoom = updatedRoomResult.Value!;
                    var roomInfo = await MapToRoomInfoAsync(updatedRoom);

                    _logger.LogInformation("[SeatHub] Broadcasting RoomInfoUpdated via NotificationService...");

                    // CORREGIDO: Usar NotificationService en lugar de Clients.Group directamente
                    await _notificationService.NotifyRoomInfoUpdatedAsync(request.RoomCode, roomInfo);

                    // CORREGIDO: Usar Clients.Caller para respuesta directa al usuario que se sentó
                    await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.SeatJoined,
                        new { Position = request.Position, RoomInfo = roomInfo });

                    _logger.LogInformation("[SeatHub] Player {PlayerId} joined seat {Position} successfully",
                        playerId, request.Position);
                }
                else
                {
                    _logger.LogError("[SeatHub] Failed to get updated room info after seat join: {Error}",
                        updatedRoomResult.Error);
                    await SendErrorAsync("Error obteniendo información actualizada de la sala");
                }
            }
            else
            {
                _logger.LogWarning("[SeatHub] JoinSeat FAILED for player {PlayerId}: {Error}",
                    playerId, result.Error);
                await SendErrorAsync(result.Error);
            }

            _logger.LogInformation("[SeatHub] ===== JoinSeat COMPLETED =====");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SeatHub] CRITICAL EXCEPTION in JoinSeat for player {PlayerId}",
                GetCurrentPlayerId());
            await HandleExceptionAsync(ex, "JoinSeat");
        }
    }

    public async Task LeaveSeat(LeaveSeatRequest request)
    {
        try
        {
            _logger.LogInformation("[SeatHub] ===== LeaveSeat STARTED =====");
            _logger.LogInformation("[SeatHub] RoomCode: {RoomCode}", request.RoomCode);

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

            _logger.LogInformation("[SeatHub] Player {PlayerId} leaving seat in room {RoomCode}",
                playerId, request.RoomCode);

            var result = await _gameRoomService.LeaveSeatAsync(request.RoomCode, playerId);

            if (result.IsSuccess)
            {
                _logger.LogInformation("[SeatHub] LeaveSeat SUCCESS - Getting updated room info...");

                var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
                if (updatedRoomResult.IsSuccess)
                {
                    var room = updatedRoomResult.Value!;
                    var roomInfo = await MapToRoomInfoAsync(room);

                    _logger.LogInformation("[SeatHub] Broadcasting RoomInfoUpdated via NotificationService...");

                    // CORREGIDO: Usar NotificationService en lugar de Clients.Group directamente
                    await _notificationService.NotifyRoomInfoUpdatedAsync(request.RoomCode, roomInfo);

                    // CORREGIDO: Usar Clients.Caller para respuesta directa al usuario que salió del asiento
                    await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.SeatLeft, roomInfo);

                    _logger.LogInformation("[SeatHub] Player {PlayerId} left seat successfully", playerId);
                }
                else
                {
                    _logger.LogError("[SeatHub] Failed to get updated room info after seat leave: {Error}",
                        updatedRoomResult.Error);
                    await SendErrorAsync("Error obteniendo información actualizada de la sala");
                }
            }
            else
            {
                _logger.LogWarning("[SeatHub] LeaveSeat FAILED for player {PlayerId}: {Error}",
                    playerId, result.Error);
                await SendErrorAsync(result.Error);
            }

            _logger.LogInformation("[SeatHub] ===== LeaveSeat COMPLETED =====");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SeatHub] CRITICAL EXCEPTION in LeaveSeat for player {PlayerId}",
                GetCurrentPlayerId());
            await HandleExceptionAsync(ex, "LeaveSeat");
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
            _logger.LogInformation("[SeatHub] Mapping room {RoomCode} to RoomInfoModel with auto-betting data", room.RoomCode);

            // NUEVO: Calcular datos de auto-betting
            var seatedPlayersCount = room.Players.Count(p => p.IsSeated);
            var isAutoBettingActive = room.MinBetPerRound?.Amount > 0 && seatedPlayersCount > 0;

            _logger.LogInformation("[SeatHub] Auto-betting calculation: MinBetPerRound={MinBet}, SeatedPlayers={SeatedCount}, Active={Active}",
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
                PlayerCount: seatedPlayersCount, // count only seated players (exclude host with Position -1)
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
                CanStart: seatedPlayersCount >= 1, // start allowed if at least one seated player
                CreatedAt: room.CreatedAt
            );

            _logger.LogInformation("[SeatHub] RoomInfoModel created successfully for room {RoomCode} with {PlayerCount} players, MinBetPerRound={MinBet}",
                room.RoomCode, roomInfo.PlayerCount, roomInfo.MinBetPerRound);

            // NUEVO: Log detallado para debugging auto-betting
            var playerDetails = string.Join(", ", roomInfo.Players.Select(p =>
                $"{p.Name}(Pos:{p.Position}, Balance:${p.CurrentBalance}, CanAfford:{p.CanAffordBet})"));
            _logger.LogInformation("[SeatHub] Player balance details: {Players}", playerDetails);

            return roomInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SeatHub] Error mapping room {RoomCode} to RoomInfoModel with auto-betting data", room.RoomCode);
            throw;
        }
    }

    #endregion
}