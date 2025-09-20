using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BlackJack.Services.User;
using BlackJack.Services.Game;
using BlackJack.Domain.Models.Users;

namespace BlackJackGame.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlayerController : BaseController
{
    private readonly IUserService _userService;
    private readonly IGameRoomService _gameRoomService;
    private readonly ILogger<PlayerController> _logger;

    public PlayerController(IUserService userService, IGameRoomService gameRoomService, ILogger<PlayerController> logger)
    {
        _userService = userService;
        _gameRoomService = gameRoomService;
        _logger = logger;
    }

    #region DTOs

    public record PlayerProfileResponse(
        string PlayerId,
        string DisplayName,
        decimal Balance,
        int TotalGamesPlayed,
        int GamesWon,
        int GamesLost,
        decimal WinPercentage,
        decimal TotalWinnings,
        DateTime CreatedAt
    );

    public record PlayerStatusResponse(
        string PlayerId,
        string DisplayName,
        bool IsOnline,
        bool InRoom,
        string? CurrentRoomCode,
        DateTime LastSeen
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

    private string GetCurrentUserName()
    {
        return HttpContext.User.FindFirst("name")?.Value
            ?? HttpContext.User.Identity?.Name
            ?? "Unknown";
    }

    #endregion

    #region Player Profile

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            _logger.LogInformation("[PlayerController] Getting profile for player {PlayerId}", playerId);

            var result = await _userService.GetUserAsync(playerId);
            if (!result.IsSuccess)
            {
                // Si no existe el perfil, creamos uno básico
                if (result.Error.Contains("Not implemented"))
                {
                    return Ok(new PlayerProfileResponse(
                        PlayerId: playerId.Value.ToString(),
                        DisplayName: GetCurrentUserName(),
                        Balance: 1000m,
                        TotalGamesPlayed: 0,
                        GamesWon: 0,
                        GamesLost: 0,
                        WinPercentage: 0m,
                        TotalWinnings: 0m,
                        CreatedAt: DateTime.UtcNow
                    ));
                }
                return BadRequest(new { error = result.Error });
            }

            var profile = result.Value!;
            var response = new PlayerProfileResponse(
                PlayerId: profile.PlayerId.Value.ToString(),
                DisplayName: profile.DisplayName,
                Balance: profile.Balance.Amount,
                TotalGamesPlayed: profile.TotalGamesPlayed,
                GamesWon: profile.GamesWon,
                GamesLost: profile.GamesLost,
                WinPercentage: profile.WinPercentage,
                TotalWinnings: profile.TotalWinnings.Amount,
                CreatedAt: profile.CreatedAt
            );

            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Authentication required" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PlayerController] Error getting player profile: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error getting profile", message = ex.Message });
        }
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.DisplayName))
            {
                return BadRequest(new { error = "Display name is required" });
            }

            var playerId = GetCurrentPlayerId();
            var result = await _userService.UpdateProfileAsync(playerId, request.DisplayName);
            return HandleResult(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Authentication required" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PlayerController] Error updating profile: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error updating profile", message = ex.Message });
        }
    }

    #endregion

    #region Player Status

    [HttpGet("me/status")]
    public async Task<IActionResult> GetMyStatus()
    {
        try
        {
            var playerId = GetCurrentPlayerId();
            var displayName = GetCurrentUserName();

            // Verificar si está en una sala
            var roomResult = await _gameRoomService.GetPlayerCurrentRoomCodeAsync(playerId);
            var inRoom = roomResult.IsSuccess && !string.IsNullOrEmpty(roomResult.Value);
            var currentRoomCode = inRoom ? roomResult.Value : null;

            var response = new PlayerStatusResponse(
                PlayerId: playerId.Value.ToString(),
                DisplayName: displayName,
                IsOnline: true, // Si está haciendo la petición, está online
                InRoom: inRoom,
                CurrentRoomCode: currentRoomCode,
                LastSeen: DateTime.UtcNow
            );

            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Authentication required" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PlayerController] Error getting player status: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error getting status", message = ex.Message });
        }
    }

    [HttpGet("me/room")]
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

            return Ok(new { inRoom = true, roomCode = result.Value });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Authentication required" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PlayerController] Error getting current room: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error getting current room", message = ex.Message });
        }
    }

    #endregion

    #region Quick Actions

    [HttpGet("me/can-play")]
    public async Task<IActionResult> CanPlay()
    {
        try
        {
            var playerId = GetCurrentPlayerId();

            // CORREGIDO: Verificar si ya está en una sala usando GetPlayerCurrentRoomCodeAsync
            var currentRoomResult = await _gameRoomService.GetPlayerCurrentRoomCodeAsync(playerId);
            if (!currentRoomResult.IsSuccess)
            {
                return BadRequest(new { error = currentRoomResult.Error });
            }

            // El jugador está en una sala si currentRoomResult.Value no es null/empty
            var isInRoom = !string.IsNullOrEmpty(currentRoomResult.Value);
            var canPlay = !isInRoom; // Puede jugar si NO está en una sala
            var reason = isInRoom ? "Already in a room" : "Available to play";

            return Ok(new
            {
                canPlay,
                reason,
                playerId = playerId.Value.ToString(),
                currentRoomCode = currentRoomResult.Value // Incluir room code si está en una sala
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Authentication required" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PlayerController] Error checking can play: {Error}", ex.Message);
            return StatusCode(500, new { error = "Error checking availability", message = ex.Message });
        }
    }

    #endregion

    #region DTOs for Requests

    public record UpdateProfileRequest(string DisplayName);

    #endregion
}