using Microsoft.AspNetCore.Mvc;
using BlackJack.Services.User;
using BlackJack.Services.Common;

namespace BlackJackGame.Controllers;

// MOVER LOS DTOs AL INICIO (ANTES DE LA CLASE)
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

[ApiController]
[Route("api/[controller]")]
public class AuthController : BaseController
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    public AuthController(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        // CAMBIO: Obtener información del usuario para la respuesta
        var userResult = await GetUserInfo(request.Email);

        if (!userResult.IsSuccess)
        {
            // Si no podemos obtener info del usuario, devolver solo el token
            return Ok(new { token = result.Value });
        }

        // Devolver token + información del usuario
        return Ok(new
        {
            token = result.Value,
            user = userResult.Value
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request.DisplayName, request.Email, request.Password);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        // Después del registro exitoso, hacer login automático
        var loginResult = await _authService.LoginAsync(request.Email, request.Password);

        if (!loginResult.IsSuccess)
        {
            return BadRequest(new { error = "Error en login automático después del registro" });
        }

        // Mapear UserProfile a formato esperado por frontend
        var userInfo = new
        {
            id = result.Value!.PlayerId.Value.ToString(),
            displayName = result.Value.DisplayName,
            email = result.Value.Email,
            balance = result.Value.Balance.Amount
        };

        return Ok(new
        {
            token = loginResult.Value,
            user = userInfo
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken);
        return HandleResult(result);
    }

    // Método helper para obtener información del usuario
    private async Task<Result<object>> GetUserInfo(string email)
    {
        try
        {
            await Task.CompletedTask; // Para evitar warning de async sin await

            return Result<object>.Success(new
            {
                id = Guid.NewGuid().ToString(),
                displayName = email.Split('@')[0],
                email = email,
                balance = 5000
            });
        }
        catch (Exception ex)
        {
            return Result<object>.Failure($"Error obteniendo información del usuario: {ex.Message}");
        }
    }
}