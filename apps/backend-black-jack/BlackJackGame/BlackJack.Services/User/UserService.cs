using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Betting;
using BlackJack.Services.Common;
using BlackJack.Data.Repositories.Users;
using Microsoft.Extensions.Logging;

namespace BlackJack.Services.User;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository userRepository, ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<Result<UserProfile>> CreateUserAsync(string displayName, string email)
    {
        try
        {
            _logger.LogInformation("[UserService] Creating user: {DisplayName}, {Email}", displayName, email);

            var emailExists = await _userRepository.EmailExistsAsync(email);
            if (emailExists)
            {
                _logger.LogWarning("[UserService] Email already exists: {Email}", email);
                return Result<UserProfile>.Failure("Email already registered");
            }

            var playerId = PlayerId.New();
            var profile = UserProfile.Create(playerId, displayName, email);
            await _userRepository.AddAsync(profile);

            _logger.LogInformation("[UserService] User created successfully: {PlayerId}", playerId);
            return Result<UserProfile>.Success(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserService] Error creating user: {Error}", ex.Message);
            return Result<UserProfile>.Failure($"Failed to create user: {ex.Message}");
        }
    }

    public async Task<Result<UserProfile>> GetUserAsync(PlayerId playerId)
    {
        try
        {
            _logger.LogDebug("[UserService] Getting user: {PlayerId}", playerId);

            var profile = await _userRepository.GetByPlayerIdAsync(playerId);
            if (profile == null)
            {
                _logger.LogWarning("[UserService] User not found: {PlayerId}", playerId);
                return Result<UserProfile>.Failure("User not found");
            }

            _logger.LogDebug("[UserService] User retrieved successfully: {PlayerId}", playerId);
            return Result<UserProfile>.Success(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserService] Error getting user {PlayerId}: {Error}", playerId, ex.Message);
            return Result<UserProfile>.Failure($"Error getting user: {ex.Message}");
        }
    }

    public async Task<Result<UserProfile>> GetOrCreateUserAsync(PlayerId playerId, string displayName, string email)
    {
        try
        {
            _logger.LogDebug("[UserService] Getting or creating user: {PlayerId}", playerId);

            var existingProfile = await _userRepository.GetByPlayerIdAsync(playerId);
            if (existingProfile != null)
            {
                _logger.LogDebug("[UserService] User found: {PlayerId}", playerId);
                return Result<UserProfile>.Success(existingProfile);
            }

            _logger.LogInformation("[UserService] User not found, creating new user: {PlayerId}", playerId);
            return await CreateUserAsync(displayName, email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserService] Error in GetOrCreateUserAsync: {Error}", ex.Message);
            return Result<UserProfile>.Failure($"Error getting or creating user: {ex.Message}");
        }
    }

    public async Task<Result> UpdateBalanceAsync(PlayerId playerId, Money newBalance)
    {
        try
        {
            _logger.LogInformation("[UserService] Updating balance for {PlayerId}: {NewBalance}", playerId, newBalance.Amount);

            var profile = await _userRepository.GetByPlayerIdAsync(playerId);
            if (profile == null)
            {
                _logger.LogWarning("[UserService] User not found for balance update: {PlayerId}", playerId);
                return Result.Failure("User not found");
            }

            profile.UpdateBalance(newBalance);
            await _userRepository.UpdateAsync(profile);

            _logger.LogInformation("[UserService] Balance updated successfully for {PlayerId}", playerId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserService] Error updating balance for {PlayerId}: {Error}", playerId, ex.Message);
            return Result.Failure($"Error updating balance: {ex.Message}");
        }
    }

    public async Task<Result> RecordGameResultAsync(PlayerId playerId, bool won, Money winnings)
    {
        try
        {
            _logger.LogInformation("[UserService] Recording game result for {PlayerId}: Won={Won}, Winnings={Winnings}",
                playerId, won, winnings.Amount);

            var profile = await _userRepository.GetByPlayerIdAsync(playerId);
            if (profile == null)
            {
                _logger.LogWarning("[UserService] User not found for game result: {PlayerId}", playerId);
                return Result.Failure("User not found");
            }

            profile.RecordGameResult(won, winnings);
            await _userRepository.UpdateAsync(profile);

            _logger.LogInformation("[UserService] Game result recorded for {PlayerId}: Total games={Total}, Win%={WinRate}%",
                playerId, profile.TotalGamesPlayed, profile.WinPercentage);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserService] Error recording game result for {PlayerId}: {Error}", playerId, ex.Message);
            return Result.Failure($"Error recording game result: {ex.Message}");
        }
    }

    public async Task<Result<UserProfile>> UpdateProfileAsync(PlayerId playerId, string displayName)
    {
        try
        {
            _logger.LogInformation("[UserService] Update profile requested for {PlayerId}: {DisplayName}", playerId, displayName);

            if (string.IsNullOrWhiteSpace(displayName))
            {
                return Result<UserProfile>.Failure("Display name cannot be empty");
            }

            var profile = await _userRepository.GetByPlayerIdAsync(playerId);
            if (profile == null)
            {
                _logger.LogWarning("[UserService] User not found for profile update: {PlayerId}", playerId);
                return Result<UserProfile>.Failure("User not found");
            }

            _logger.LogWarning("[UserService] DisplayName update requested but UserProfile.DisplayName is immutable");
            return Result<UserProfile>.Failure("Display name cannot be updated after user creation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserService] Error updating profile for {PlayerId}: {Error}", playerId, ex.Message);
            return Result<UserProfile>.Failure($"Error updating profile: {ex.Message}");
        }
    }

    public async Task<Result<List<UserProfile>>> GetRankingAsync(int top = 10)
    {
        try
        {
            _logger.LogInformation("[UserService] Getting ranking - top {Top} players", top);

            var allUsers = await _userRepository.GetAllAsync();

            var ranking = allUsers
                .Where(u => u.IsActive)
                .OrderByDescending(u => u.Balance.Amount - 1000m) // Ganancias netas
                .ThenByDescending(u => u.WinPercentage)
                .ThenByDescending(u => u.TotalGamesPlayed)
                .Take(top)
                .ToList();

            _logger.LogInformation("[UserService] Ranking retrieved: {Count} players", ranking.Count);
            return Result<List<UserProfile>>.Success(ranking);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserService] Error getting ranking: {Error}", ex.Message);
            return Result<List<UserProfile>>.Failure($"Error getting ranking: {ex.Message}");
        }
    }

    public async Task<Result<UserProfile>> GetUserByEmailAsync(string email)
    {
        try
        {
            _logger.LogDebug("[UserService] Getting user by email: {Email}", email);

            var profile = await _userRepository.GetByEmailAsync(email);
            if (profile == null)
            {
                _logger.LogWarning("[UserService] User not found by email: {Email}", email);
                return Result<UserProfile>.Failure("User not found");
            }

            _logger.LogDebug("[UserService] User found by email: {PlayerId}", profile.PlayerId);
            return Result<UserProfile>.Success(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserService] Error getting user by email {Email}: {Error}", email, ex.Message);
            return Result<UserProfile>.Failure($"Error getting user: {ex.Message}");
        }
    }

    public async Task<Result<decimal>> GetNetGainsAsync(PlayerId playerId)
    {
        try
        {
            var profileResult = await GetUserAsync(playerId);
            if (!profileResult.IsSuccess)
            {
                return Result<decimal>.Failure(profileResult.Error);
            }

            var profile = profileResult.Value!;
            var netGains = profile.Balance.Amount - 1000m;

            _logger.LogDebug("[UserService] Net gains for {PlayerId}: {NetGains}", playerId, netGains);
            return Result<decimal>.Success(netGains);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserService] Error calculating net gains for {PlayerId}: {Error}", playerId, ex.Message);
            return Result<decimal>.Failure($"Error calculating net gains: {ex.Message}");
        }
    }

    public async Task<Result> SyncPlayerBalanceAsync(PlayerId playerId, Money currentBalance)
    {
        try
        {
            _logger.LogInformation("[UserService] Syncing player balance: {PlayerId} -> {Balance}", playerId, currentBalance.Amount);

            var profile = await _userRepository.GetByPlayerIdAsync(playerId);
            if (profile == null)
            {
                _logger.LogWarning("[UserService] User not found for balance sync: {PlayerId}", playerId);
                return Result.Failure("User not found");
            }

            if (profile.Balance.Amount != currentBalance.Amount)
            {
                profile.UpdateBalance(currentBalance);
                await _userRepository.UpdateAsync(profile);

                _logger.LogInformation("[UserService] Balance synchronized for {PlayerId}: {OldBalance} -> {NewBalance}",
                    playerId, profile.Balance.Amount, currentBalance.Amount);
            }
            else
            {
                _logger.LogDebug("[UserService] Balance already synchronized for {PlayerId}", playerId);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserService] Error syncing balance for {PlayerId}: {Error}", playerId, ex.Message);
            return Result.Failure($"Error syncing balance: {ex.Message}");
        }
    }
}