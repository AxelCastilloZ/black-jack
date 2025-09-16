using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;
using BlackJack.Data.Identity;
using BlackJack.Data.Repositories.Users;

namespace BlackJack.Services.User;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IUserRepository _userRepository;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IUserRepository userRepository)
    {
        _userManager = userManager;
        _configuration = configuration;
        _userRepository = userRepository;
    }

    public async Task<Result<string>> LoginAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, password))
        {
            return Result<string>.Failure("Email o contraseña incorrectos");
        }

        if (!user.IsActive)
        {
            return Result<string>.Failure("Cuenta desactivada");
        }

        // Actualizar último login
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var token = GenerateJwtToken(user);
        return Result<string>.Success(token);
    }

    public async Task<Result<UserProfile>> RegisterAsync(string displayName, string email, string password)
    {
        // Validaciones
        if (string.IsNullOrWhiteSpace(displayName))
            return Result<UserProfile>.Failure("El nombre es requerido");

        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            return Result<UserProfile>.Failure("Email inválido");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return Result<UserProfile>.Failure("La contraseña debe tener al menos 6 caracteres");

        // Verificar si ya existe
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            return Result<UserProfile>.Failure("El email ya está registrado");
        }

        // Crear ApplicationUser
        var playerId = PlayerId.New();
        var applicationUser = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            PlayerId = playerId,
            EmailConfirmed = true // Para simplificar
        };

        var result = await _userManager.CreateAsync(applicationUser, password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Result<UserProfile>.Failure($"Error al crear usuario: {errors}");
        }

        // Crear UserProfile
        var userProfile = UserProfile.Create(playerId, displayName, email);
        await _userRepository.AddAsync(userProfile);

        return Result<UserProfile>.Success(userProfile);
    }

    public async Task<Result<string>> RefreshTokenAsync(string refreshToken)
    {
        // Implementación básica - en producción usarías refresh tokens reales
        await Task.CompletedTask;
        return Result<string>.Failure("Refresh token no implementado");
    }

    public async Task<Result> LogoutAsync(PlayerId playerId)
    {
        // En una implementación completa, invalidarías el token
        await Task.CompletedTask;
        return Result.Success();
    }

    public async Task<Result<UserProfile>> ValidateTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:Key"] ?? "default-key-for-development-only-not-secure");

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userIdClaim = jwtToken.Claims.First(x => x.Type == "sub").Value;

            var user = await _userManager.FindByIdAsync(userIdClaim);
            if (user == null || !user.IsActive)
            {
                return Result<UserProfile>.Failure("Usuario no encontrado");
            }

            var userProfile = await _userRepository.GetByPlayerIdAsync(user.PlayerId);
            if (userProfile == null)
            {
                return Result<UserProfile>.Failure("Perfil de usuario no encontrado");
            }

            return Result<UserProfile>.Success(userProfile);
        }
        catch
        {
            return Result<UserProfile>.Failure("Token inválido");
        }
    }

    private string GenerateJwtToken(ApplicationUser user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:Key"] ?? "default-key-for-development-only-not-secure");

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", user.Id),
                new Claim("email", user.Email ?? ""),
                new Claim("name", user.DisplayName),
                new Claim("playerId", user.PlayerId.Value.ToString())
            }),
            Expires = DateTime.UtcNow.AddHours(24),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}