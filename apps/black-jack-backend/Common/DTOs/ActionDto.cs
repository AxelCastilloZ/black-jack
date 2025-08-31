namespace black_jack_backend.Common.DTOs;

public class ActionDto
{
    public string ActionType { get; set; } = string.Empty; // "hit", "stand", "double"
    public int SeatIndex { get; set; }
    public decimal? BetAmount { get; set; } // For double down
}