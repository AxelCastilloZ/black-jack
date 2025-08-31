namespace black_jack_backend.Common.DTOs;

public class CreateRoomDto
{
    public string Name { get; set; } = string.Empty;
    public int NumDecks { get; set; } = 6;
    public decimal MinBet { get; set; } = 500m;
    public decimal MaxBet { get; set; } = 10.000m;
    public decimal PayoutBlackjack { get; set; } = 1.5m;
    public bool SurrenderEnabled { get; set; } = true;
}