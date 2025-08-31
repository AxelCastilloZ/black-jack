using System.ComponentModel.DataAnnotations;

namespace black_jack_backend.Entities;

public class Room
{
    public int Id { get; set; }
    
    [Required]
    public string Code { get; set; } = string.Empty; // The code of the room, it is unique
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    [Required]
    public int NumDecks { get; set; } = 1; // Number of decks (Mazos) of cards in the room, default is 1
    
    [Required]
    public decimal MinBet { get; set; } = 500m; // 5 Tejas as minimum bet
    
    [Required]
    public decimal MaxBet { get; set; } = 10.000m; // 10 Rojos as maximum bet
    
    [Required]
    public decimal PayoutBlackjack { get; set; } = 1.5m; // Payout for blackjack, default is 1.5
    
    public bool SurrenderEnabled { get; set; } = true; //Surrender in case the player doesn't want to play anymore
    
}