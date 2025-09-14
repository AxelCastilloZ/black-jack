namespace BlackJack.Realtime.Models.Requests;

public class PlayerActionRequest
{
    public string TableId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; 
}