// SpectatorJoinedEvent.cs
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Common;

public class SpectatorJoinedEvent : BaseDomainEvent
{
    public string RoomCode { get; }
    public PlayerId SpectatorId { get; }
    public string SpectatorName { get; }

    public SpectatorJoinedEvent(string roomCode, PlayerId spectatorId, string spectatorName)
    {
        RoomCode = roomCode;
        SpectatorId = spectatorId;
        SpectatorName = spectatorName;
    }
}