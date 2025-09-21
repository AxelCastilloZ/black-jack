// BlackJack.Realtime/Hubs/SeatHub.cs - Hub especializado en gestión de asientos
using BlackJack.Domain.Models.Users;
using BlackJack.Realtime.Models;
using BlackJack.Realtime.Services;
using BlackJack.Services.Game;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlackJack.Realtime.Hubs;

[AllowAnonymous]
public class SeatHub : BaseHub
{
    private readonly IGameRoomService _gameRoomService;
    private readonly IConnectionManager _connectionManager;

    public SeatHub(
        IGameRoomService gameRoomService,
        IConnectionManager connectionManager,
        ILogger<SeatHub> logger) : base(logger)
    {
        _gameRoomService = gameRoomService;
        _connectionManager = connectionManager;
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

                    _logger.LogInformation("[SeatHub] Broadcasting RoomInfoUpdated to room group...");

                    await Clients.Group(HubMethodNames.Groups.GetRoomGroup(request.RoomCode))
                        .SendAsync(HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);

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
                var updatedRoomResult = await _gameRoomService.GetRoomAsync(request.RoomCode);
                if (updatedRoomResult.IsSuccess)
                {
                    var room = updatedRoomResult.Value!;
                    var roomInfo = await MapToRoomInfoAsync(room);

                    await Clients.Group(HubMethodNames.Groups.GetRoomGroup(request.RoomCode))
                        .SendAsync(HubMethodNames.ServerMethods.RoomInfoUpdated, roomInfo);

                    await Clients.Caller.SendAsync(HubMethodNames.ServerMethods.SeatLeft, roomInfo);
                }

                _logger.LogInformation("[SeatHub] Player {PlayerId} left seat successfully", playerId);
            }
            else
            {
                await SendErrorAsync(result.Error);
            }

            _logger.LogInformation("[SeatHub] ===== LeaveSeat COMPLETED =====");
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "LeaveSeat");
        }
    }

    #endregion

    #region Seat Information

    /// <summary>
    /// Obtiene información detallada de los asientos en una sala
    /// </summary>
    public async Task GetSeatInfo(string roomCode)
    {
        try
        {
            if (!ValidateInput(roomCode, nameof(roomCode)))
            {
                await SendErrorAsync("Código de sala inválido");
                return;
            }

            _logger.LogInformation("[SeatHub] Getting seat info for room {RoomCode}", roomCode);

            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync("Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;
            var seatInfo = BuildSeatInfo(room);

            await Clients.Caller.SendAsync("SeatInfo", seatInfo);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "GetSeatInfo");
        }
    }

    /// <summary>
    /// Verifica si un asiento específico está disponible
    /// </summary>
    public async Task CheckSeatAvailability(string roomCode, int position)
    {
        try
        {
            if (!ValidateInput(roomCode, nameof(roomCode)) ||
                position < 0 || position > 5)
            {
                await SendErrorAsync("Datos inválidos");
                return;
            }

            _logger.LogInformation("[SeatHub] Checking seat {Position} availability in room {RoomCode}",
                position, roomCode);

            // Aquí podrías implementar un método específico en el service si es necesario
            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync("Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;
            var isOccupied = room.Players.Any(p => p.GetSeatPosition() == position);

            var availability = new
            {
                RoomCode = roomCode,
                Position = position,
                IsAvailable = !isOccupied,
                OccupiedBy = isOccupied ? room.Players.FirstOrDefault(p => p.GetSeatPosition() == position)?.Name : null,
                Timestamp = DateTime.UtcNow
            };

            await Clients.Caller.SendAsync("SeatAvailability", availability);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "CheckSeatAvailability");
        }
    }

    /// <summary>
    /// Obtiene información del asiento actual del jugador
    /// </summary>
    public async Task GetMySeatInfo(string roomCode)
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

            _logger.LogInformation("[SeatHub] Getting seat info for player {PlayerId} in room {RoomCode}",
                playerId, roomCode);

            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync("Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;
            var player = room.Players.FirstOrDefault(p => p.PlayerId == playerId);

            var mySeatInfo = new
            {
                RoomCode = roomCode,
                PlayerId = playerId.Value,
                IsSeated = player?.IsSeated ?? false,
                Position = player?.GetSeatPosition() ?? -1,
                CanJoinSeat = player != null && !player.IsSeated,
                CanLeaveSeat = player?.IsSeated ?? false,
                Timestamp = DateTime.UtcNow
            };

            await Clients.Caller.SendAsync("MySeatInfo", mySeatInfo);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "GetMySeatInfo");
        }
    }

    #endregion

    #region Seat Actions

    /// <summary>
    /// Intercambia asientos con otro jugador (si ambos están de acuerdo)
    /// </summary>
    public async Task RequestSeatSwap(string roomCode, int targetPosition)
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            if (playerId == null)
            {
                await SendErrorAsync("Error de autenticación");
                return;
            }

            if (!ValidateInput(roomCode, nameof(roomCode)) ||
                targetPosition < 0 || targetPosition > 5)
            {
                await SendErrorAsync("Datos inválidos");
                return;
            }

            _logger.LogInformation("[SeatHub] Player {PlayerId} requesting seat swap to position {Position} in room {RoomCode}",
                playerId, targetPosition, roomCode);

            // Implementación básica - en el futuro podrías agregar un sistema de confirmación
            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (!roomResult.IsSuccess)
            {
                await SendErrorAsync("Sala no encontrada");
                return;
            }

            var room = roomResult.Value!;
            var currentPlayer = room.Players.FirstOrDefault(p => p.PlayerId == playerId);
            var targetPlayer = room.Players.FirstOrDefault(p => p.GetSeatPosition() == targetPosition);

            if (currentPlayer == null || !currentPlayer.IsSeated)
            {
                await SendErrorAsync("Debes estar sentado para intercambiar asientos");
                return;
            }

            if (targetPlayer == null)
            {
                await SendErrorAsync("No hay jugador en esa posición");
                return;
            }

            // Por ahora solo notificar la solicitud
            var swapRequest = new
            {
                RoomCode = roomCode,
                RequesterId = playerId.Value,
                RequesterName = currentPlayer.Name,
                RequesterPosition = currentPlayer.GetSeatPosition(),
                TargetPlayerId = targetPlayer.PlayerId.Value,
                TargetPlayerName = targetPlayer.Name,
                TargetPosition = targetPosition,
                Timestamp = DateTime.UtcNow
            };

            await Clients.Group(HubMethodNames.Groups.GetRoomGroup(roomCode))
                .SendAsync("SeatSwapRequested", swapRequest);

            _logger.LogInformation("[SeatHub] Seat swap request sent from {RequesterName} to {TargetName}",
                currentPlayer.Name, targetPlayer.Name);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "RequestSeatSwap");
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Construye información detallada de todos los asientos
    /// </summary>
    private object BuildSeatInfo(BlackJack.Domain.Models.Game.GameRoom room)
    {
        var seats = new List<object>();

        for (int position = 0; position < room.MaxPlayers; position++)
        {
            var player = room.Players.FirstOrDefault(p => p.GetSeatPosition() == position);

            seats.Add(new
            {
                Position = position,
                IsOccupied = player != null,
                PlayerId = player?.PlayerId.Value,
                PlayerName = player?.Name,
                IsReady = player?.IsReady ?? false,
                IsHost = player != null && room.HostPlayerId == player.PlayerId,
                HasPlayedTurn = player?.HasPlayedTurn ?? false
            });
        }

        return new
        {
            RoomCode = room.RoomCode,
            MaxSeats = room.MaxPlayers,
            OccupiedSeats = room.Players.Count(p => p.IsSeated),
            AvailableSeats = room.MaxPlayers - room.Players.Count(p => p.IsSeated),
            Seats = seats,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Mapea GameRoom a RoomInfoModel
    /// </summary>
    private async Task<RoomInfoModel> MapToRoomInfoAsync(BlackJack.Domain.Models.Game.GameRoom room)
    {
        try
        {
            _logger.LogInformation("[SeatHub] Mapping room {RoomCode} to RoomInfoModel", room.RoomCode);

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

            _logger.LogInformation("[SeatHub] RoomInfoModel created successfully for room {RoomCode} with {PlayerCount} players",
                room.RoomCode, roomInfo.PlayerCount);

            return roomInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SeatHub] Error mapping room {RoomCode} to RoomInfoModel", room.RoomCode);
            throw;
        }
    }

    #endregion
}