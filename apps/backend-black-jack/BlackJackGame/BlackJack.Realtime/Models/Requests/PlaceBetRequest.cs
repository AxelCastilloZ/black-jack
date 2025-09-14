namespace BlackJack.Realtime.Models.Requests;

public class PlaceBetRequest
{
    public string TableId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}