
using BlackJack.Domain.Common;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Common;

public class PlayerJoinedRoomEvent : BaseDomainEvent
{
    public string RoomCode { get; }
    public PlayerId PlayerId { get; }
    public string PlayerName { get; }
    public int Position { get; }
    public int TotalPlayers { get; }

    public PlayerJoinedRoomEvent(string roomCode, PlayerId playerId, string playerName, int position, int totalPlayers)
    {
        RoomCode = roomCode;
        PlayerId = playerId;
        PlayerName = playerName;
        Position = position;
        TotalPlayers = totalPlayers;
    }
}