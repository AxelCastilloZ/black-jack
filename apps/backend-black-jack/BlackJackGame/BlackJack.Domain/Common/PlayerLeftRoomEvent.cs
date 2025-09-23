
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Common;

public class PlayerLeftRoomEvent : BaseDomainEvent
{
    public string RoomCode { get; }
    public PlayerId PlayerId { get; }
    public string PlayerName { get; }
    public int RemainingPlayers { get; }

    public PlayerLeftRoomEvent(string roomCode, PlayerId playerId, string playerName, int remainingPlayers)
    {
        RoomCode = roomCode;
        PlayerId = playerId;
        PlayerName = playerName;
        RemainingPlayers = remainingPlayers;
    }
}