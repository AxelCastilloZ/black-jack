using Blackjack.Domain.ValueObjects;

namespace Blackjack.Application.DTOs;

public class GameStateDto
{
    public int RoomId { get; set; }
    public RoundPhase Phase { get; set; }
    public List<string> DealerVisibleCards { get; set; } = new();
    public int DealerHiddenCardCount { get; set; }
    public int ShoePosition { get; set; }
    public DateTime UpdatedAt { get; set; }
}
