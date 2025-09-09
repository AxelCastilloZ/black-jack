using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Blackjack.Domain.Entities;

public class Player
{
    public int Id { get; set; }
    
    public int? UserId { get; set; } // in case we had a guest player
    
    [Required]
    public int RoomId { get; set; }
    
    [Required]
    public string Nickname { get; set; } = string.Empty;
    
    [Required]
    public int SeatIndex { get; set; } // Position at the table (0, 1, 2, 3...)
    
    public decimal? BalanceShadow { get; set; } // Ammout of money the player has 
    
    public string? SocketId { get; set; } // For SignalR connection tracking
}