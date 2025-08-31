using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace black_jack_backend.Entities;

public class Round
{
    public int Id { get; set; }
    
    [Required]
    public int RoomId { get; set; }
    
    [Required]
    public string Phase { get; set; } = "waiting"; // waiting, betting, playing, finished, we could add more 
    
    [Required]
    public int ShoePosition { get; set; } = 0; // Ammount of cards you have used in the round
    
    [Required]
    public string DealerHandJSON { get; set; } = "[]"; // JSON array of dealer's cards
    
    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}