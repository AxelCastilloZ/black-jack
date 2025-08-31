using System.ComponentModel.DataAnnotations;

namespace black_jack_backend.Entities;

public class User
{
    public int Id { get; set; }
    
    [Required]
    public string DisplayName { get; set; } = string.Empty;
    
    public string? PasswordHash { get; set; }
    
    [Required]
    public decimal Balance { get; set; } = 0;
}