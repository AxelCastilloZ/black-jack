using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace black_jack_backend.Entities;

public class ActionLog
{
    public int Id { get; set; }
    
    [Required]
    public int RoomId { get; set; }
    
    public int? RoundId { get; set; } // Links to the round if the action happened in a round
    
    public int? PlayerId { get; set; } // Links to the player if the action happened to a player
    
    [Required]
    public string ActionType { get; set; } = string.Empty; // "join_room", "place_bet", "hit", "stand", "game_start", etc.
    
    [Required]
    public string PayloadJSON { get; set; } = "{}"; // JSON with action-specific data
}