using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace black_jack_backend.Entities;

public class Hand
{
    public int Id { get; set; }
    
    [Required]
    public int RoundId { get; set; }
    
    [Required]
    public int PlayerId { get; set; }
    
    [Required]
    public string CardsJSON { get; set; } = "[]"; // JSON array of cards in this hand
    
    [Required]
    public decimal BetAmount { get; set; } = 0;
    
    public bool IsStanding { get; set; } = false; // Player chose to stand aka retire from the round
    
    public bool IsBusted { get; set; } = false; // Hand value > 21 it goes automatically to lose
    
    public bool IsDoubled { get; set; } = false; // Player doubled down the bet and take one more card
    
    public bool IsBlackjack { get; set; } = false; // Natural blackjack (For example 10 + J)
    
    public string? Result { get; set; } // "win", "lose", "push" 
    
    public decimal Payout { get; set; } = 0; // Amount won/lost
}