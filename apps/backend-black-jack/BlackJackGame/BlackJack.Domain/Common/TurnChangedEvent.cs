
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Common;

public class TurnChangedEvent : BaseDomainEvent
{
    public string RoomCode { get; }
    public PlayerId CurrentPlayerId { get; }
    public string CurrentPlayerName { get; }
    public PlayerId? PreviousPlayerId { get; }
    public int TurnIndex { get; }

    public TurnChangedEvent(string roomCode, PlayerId currentPlayerId, string currentPlayerName,
                           PlayerId? previousPlayerId, int turnIndex)
    {
        RoomCode = roomCode;
        CurrentPlayerId = currentPlayerId;
        CurrentPlayerName = currentPlayerName;
        PreviousPlayerId = previousPlayerId;
        TurnIndex = turnIndex;
    }
}