namespace BlackJack.Realtime.Models.Requests;

public class JoinTableRequest
{
    public string TableId { get; set; } = string.Empty;
    public int SeatPosition { get; set; }
}