namespace Blackjack.Application.DTOs;

public class JoinRoomDto
{
    public string RoomCode { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public int SeatIndex { get; set; }
}