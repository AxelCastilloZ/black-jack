
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Common;

public class SpectatorLeftEvent : BaseDomainEvent
{
    public string RoomCode { get; }
    public PlayerId SpectatorId { get; }
    public string SpectatorName { get; }

    public SpectatorLeftEvent(string roomCode, PlayerId spectatorId, string spectatorName)
    {
        RoomCode = roomCode;
        SpectatorId = spectatorId;
        SpectatorName = spectatorName;
    }
}