using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Blackjack.Domain.ValueObjects;

namespace Blackjack.Domain.Entities;

public class Round
{
    public int Id { get; set; }
    
    [Required]
    public int RoomId { get; set; }
    
    [Required]
    public RoundPhase Phase { get; set; } = RoundPhase.Waiting; 
    
    [Required]
    public int ShoePosition { get; set; } = 0; // Ammount of cards you have used in the round
    
    [Required]
    public string DealerHandJSON { get; set; } = "[]"; // JSON array of dealer's cards
    
    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}