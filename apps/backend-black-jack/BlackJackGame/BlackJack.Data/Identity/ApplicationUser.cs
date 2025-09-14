using Microsoft.AspNetCore.Identity;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Data.Identity;

public class ApplicationUser : IdentityUser
{
    public PlayerId PlayerId { get; set; } = PlayerId.New();
    public string DisplayName { get; set; } = string.Empty;
    public decimal Balance { get; set; } = 1000m;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}