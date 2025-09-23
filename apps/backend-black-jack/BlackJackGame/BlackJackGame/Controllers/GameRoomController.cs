using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BlackJack.Services.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Enums;

namespace BlackJackGame.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Requiere autenticación para todas las acciones
public class GameRoomController : BaseController
{
    private readonly IGameRoomService _gameRoomService;
    private readonly IGameService _gameService;
    private readonly ILogger<GameRoomController> _logger;

    public GameRoomController(IGameRoomService gameRoomService, IGameService gameService, ILogger<GameRoomController> logger)
    {
        _gameRoomService = gameRoomService;
        _gameService = gameService;
        _logger = logger;
    }

    #region DTOs

    public record CreateRoomRequest(string RoomName, int MaxPlayers = 6);

    public record JoinRoomRequest(string RoomCode, string PlayerName);

    public record RoomPlaceBetRequest(decimal Amount);

    public record PlayerActionRequest(string Action);

    public record RoomInfoResponse(
        string RoomCode,
        string Name,
        string Status,
        int PlayerCount,
        int MaxPlayers,
        List<RoomPlayerResponse> Players,
        List<SpectatorResponse> Spectators,
        string? CurrentPlayerTurn,
        bool CanStart,
        DateTime CreatedAt
    );

    public record RoomPlayerResponse(
        string PlayerId,
        string Name,
        int Position,
        bool IsReady,
        bool IsHost,
        bool HasPlayedTurn
    );

    public record SpectatorResponse(
        string PlayerId,
        string Name,
        DateTime JoinedAt
    );

    public record ActiveRoomResponse(
        string RoomCode,
        string Name,
        int PlayerCount,
        int MaxPlayers,
        string Status,
        DateTime CreatedAt
    );

    #endregion

    #region Helper Methods

    private PlayerId GetCurrentPlayerId()
    {
        var playerIdClaim = HttpContext.User.FindFirst("playerId")?.Value;
        if (string.IsNullOrEmpty(playerIdClaim) || !Guid.TryParse(playerIdClaim, out var playerId))
        {
            throw new UnauthorizedAccessException("Invalid player ID in token");
        }
        return PlayerId.From(playerId);
    }

    private static RoomInfoResponse ToRoomInfoResponse(BlackJack.Domain.Models.Game.GameRoom room)
    {
        return new RoomInfoResponse(
            RoomCode: room.RoomCode,
            Name: room.Name,
            Status: room.Status.ToString(),
            PlayerCount: room.PlayerCount,
            MaxPlayers: room.MaxPlayers,
            Players: room.Players.Select(p => new RoomPlayerResponse(
                PlayerId: p.PlayerId.Value.ToString(),
                Name: p.Name,
                Position: p.Position,
                IsReady: p.IsReady,
                IsHost: room.HostPlayerId == p.PlayerId,
                HasPlayedTurn: p.HasPlayedTurn
            )).ToList(),
            Spectators: room.Spectators.Select(s => new SpectatorResponse(
                PlayerId: s.PlayerId.Value.ToString(),
                Name: s.Name,
                JoinedAt: s.JoinedAt
            )).ToList(),
            CurrentPlayerTurn: room.CurrentPlayer?.Name,
            CanStart: room.CanStart,
            CreatedAt: room.CreatedAt
        );
    }

    #endregion

    #region Room Management

    [HttpGet]
    public async Task<IActionResult> GetActiveRooms()
    {
        try
        {
            _logger.LogInformation("[GameRoomController] Getting active rooms");

            var result = await _gameRoomService.GetActiveRoomsAsync();
            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error });
            }

            var activeRooms = result.Value!.Select(room => new ActiveRoomResponse(
                RoomCode: room.RoomCode,
                Name: room.Name,
                PlayerCount: room.PlayerCount,
                MaxPlayers: room.MaxPlayers,
                Status: room.Status.ToString(),
                CreatedAt: room.CreatedAt
            )).ToList();

            return Ok(activeRooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomController] Error getting active rooms: {Error}", ex.Message);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    [HttpGet("{roomCode}")]
    public async Task<IActionResult> GetRoom(string roomCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                return BadRequest(new { error = "Room code is required" });
            }

            var result = await _gameRoomService.GetRoomAsync(roomCode);
            if (!result.IsSuccess)
            {
                return NotFound(new { error = result.Error });
            }

            var roomInfo = ToRoomInfoResponse(result.Value!);
            return Ok(roomInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomController] Error getting room {RoomCode}: {Error}", roomCode, ex.Message);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RoomName))
            {
                return BadRequest(new { error = "Room name is required" });
            }

            if (request.MaxPlayers < 1 || request.MaxPlayers > 6)
            {
                return BadRequest(new { error = "Max players must be between 1 and 6" });
            }

            var playerId = GetCurrentPlayerId();
            _logger.LogInformation("[GameRoomController] Creating room {RoomName} for player {PlayerId}", request.RoomName, playerId);

            var result = await _gameRoomService.CreateRoomAsync(request.RoomName, playerId);
            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error });
            }

            var roomInfo = ToRoomInfoResponse(result.Value!);
            return CreatedAtAction(nameof(GetRoom), new { roomCode = roomInfo.RoomCode }, roomInfo);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Authentication required" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomController] Error creating room: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error creating room", message = ex.Message });
        }
    }

    #endregion

    #region Player Actions

    [HttpPost("{roomCode}/join")]
    public async Task<IActionResult> JoinRoom(string roomCode, [FromBody] JoinRoomRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomCode) || string.IsNullOrWhiteSpace(request.PlayerName))
            {
                return BadRequest(new { error = "Room code and player name are required" });
            }

            var playerId = GetCurrentPlayerId();
            _logger.LogInformation("[GameRoomController] Player {PlayerId} joining room {RoomCode}", playerId, roomCode);

            var result = await _gameRoomService.JoinRoomAsync(roomCode, playerId, request.PlayerName);
            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error });
            }

            // Obtener información actualizada de la sala
            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (roomResult.IsSuccess)
            {
                var roomInfo = ToRoomInfoResponse(roomResult.Value!);
                return Ok(roomInfo);
            }

            return Ok(new { message = "Successfully joined room", roomCode });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Authentication required" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomController] Error joining room: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error joining room", message = ex.Message });
        }
    }

    [HttpPost("{roomCode}/leave")]
    public async Task<IActionResult> LeaveRoom(string roomCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                return BadRequest(new { error = "Room code is required" });
            }

            var playerId = GetCurrentPlayerId();
            _logger.LogInformation("[GameRoomController] Player {PlayerId} leaving room {RoomCode}", playerId, roomCode);

            var result = await _gameRoomService.LeaveRoomAsync(roomCode, playerId);
            return HandleResult(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Authentication required" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomController] Error leaving room: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error leaving room", message = ex.Message });
        }
    }

    [HttpPost("{roomCode}/spectate")]
    public async Task<IActionResult> SpectateRoom(string roomCode, [FromBody] JoinRoomRequest request)
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            var result = await _gameRoomService.AddSpectatorAsync(roomCode, playerId, request.PlayerName);
            return HandleResult(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Authentication required" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomController] Error spectating room: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error spectating room", message = ex.Message });
        }
    }

    #endregion

    #region Game Control

    [HttpPost("{roomCode}/start")]
    public async Task<IActionResult> StartGame(string roomCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                return BadRequest(new { error = "Room code is required" });
            }

            var playerId = GetCurrentPlayerId();
            _logger.LogInformation("[GameRoomController] Starting game in room {RoomCode} by player {PlayerId}", roomCode, playerId);

            var result = await _gameRoomService.StartGameAsync(roomCode, playerId);
            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error });
            }

            return Ok(new { message = "Game started successfully", roomCode });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Authentication required" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomController] Error starting game: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error starting game", message = ex.Message });
        }
    }

    [HttpPost("{roomCode}/bet")]
    public async Task<IActionResult> PlaceBet(string roomCode, [FromBody] RoomPlaceBetRequest request)
    {
        try
        {
            if (request.Amount <= 0)
            {
                return BadRequest(new { error = "Bet amount must be greater than zero" });
            }

            var playerId = GetCurrentPlayerId();

            // Obtener la sala para verificar la mesa asociada
            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (!roomResult.IsSuccess || !roomResult.Value!.BlackjackTableId.HasValue)
            {
                return BadRequest(new { error = "Room or associated table not found" });
            }

            var tableId = roomResult.Value.BlackjackTableId.Value;
            var bet = BlackJack.Domain.Models.Betting.Bet.Create(request.Amount);

            var result = await _gameService.PlaceBetAsync(tableId, playerId, bet);
            return HandleResult(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Authentication required" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomController] Error placing bet: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error placing bet", message = ex.Message });
        }
    }

    [HttpPost("{roomCode}/action")]
    public async Task<IActionResult> PlayerAction(string roomCode, [FromBody] PlayerActionRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Action))
            {
                return BadRequest(new { error = "Action is required" });
            }

            if (!Enum.TryParse<PlayerAction>(request.Action, true, out var playerAction))
            {
                return BadRequest(new { error = "Invalid action" });
            }

            var playerId = GetCurrentPlayerId();

            // Obtener la sala para verificar la mesa asociada
            var roomResult = await _gameRoomService.GetRoomAsync(roomCode);
            if (!roomResult.IsSuccess || !roomResult.Value!.BlackjackTableId.HasValue)
            {
                return BadRequest(new { error = "Room or associated table not found" });
            }

            var tableId = roomResult.Value.BlackjackTableId.Value;

            var result = await _gameService.PlayerActionAsync(tableId, playerId, playerAction);
            return HandleResult(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Authentication required" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomController] Error performing player action: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error performing action", message = ex.Message });
        }
    }

    [HttpPost("{roomCode}/next-turn")]
    public async Task<IActionResult> NextTurn(string roomCode)
    {
        try
        {
            var result = await _gameRoomService.NextTurnAsync(roomCode);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomController] Error advancing turn: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error advancing turn", message = ex.Message });
        }
    }

    [HttpPost("{roomCode}/end")]
    public async Task<IActionResult> EndGame(string roomCode)
    {
        try
        {
            var result = await _gameRoomService.EndGameAsync(roomCode);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomController] Error ending game: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error ending game", message = ex.Message });
        }
    }

    // NUEVO: Endpoint de mantenimiento para limpiar una sala atascada (admin/dev)
    [HttpPost("{roomCode}/cleanup")]
    [AllowAnonymous]
    public async Task<IActionResult> ForceCleanupRoom(string roomCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                return BadRequest(new { error = "Room code is required" });
            }

            var result = await _gameRoomService.ForceCleanupRoomAsync(roomCode);
            return HandleResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomController] Error force-cleaning room: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error cleaning room", message = ex.Message });
        }
    }

    #endregion

    #region Player Status

    [HttpGet("my-room")]
    public async Task<IActionResult> GetMyCurrentRoom()
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            var result = await _gameRoomService.GetPlayerCurrentRoomCodeAsync(playerId);

            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error });
            }

            if (string.IsNullOrEmpty(result.Value))
            {
                return Ok(new { inRoom = false, roomCode = (string?)null });
            }

            // Obtener información completa de la sala
            var roomResult = await _gameRoomService.GetRoomAsync(result.Value);
            if (roomResult.IsSuccess)
            {
                var roomInfo = ToRoomInfoResponse(roomResult.Value!);
                return Ok(new { inRoom = true, room = roomInfo });
            }

            return Ok(new { inRoom = true, roomCode = result.Value });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Authentication required" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomController] Error getting current room: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error getting current room", message = ex.Message });
        }
    }

    [HttpGet("{roomCode}/my-turn")]
    public async Task<IActionResult> IsMyTurn(string roomCode)
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            var result = await _gameRoomService.IsPlayerTurnAsync(roomCode, playerId);

            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error });
            }

            return Ok(new { isMyTurn = result.Value, roomCode });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Authentication required" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GameRoomController] Error checking turn: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error checking turn", message = ex.Message });
        }
    }

    #endregion
}