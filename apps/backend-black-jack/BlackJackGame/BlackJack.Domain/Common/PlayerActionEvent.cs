
using BlackJack.Domain.Enums;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Common;

public class PlayerActionEvent : BaseDomainEvent
{
    public string RoomCode { get; }
    public PlayerId PlayerId { get; }
    public string PlayerName { get; }
    public PlayerAction Action { get; }
    public Guid HandId { get; }
    public int HandValue { get; }

    public PlayerActionEvent(string roomCode, PlayerId playerId, string playerName,
                           PlayerAction action, Guid handId, int handValue)
    {
        RoomCode = roomCode;
        PlayerId = playerId;
        PlayerName = playerName;
        Action = action;
        HandId = handId;
        HandValue = handValue;
    }
}