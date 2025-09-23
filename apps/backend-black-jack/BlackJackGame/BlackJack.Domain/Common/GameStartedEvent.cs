
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Common;

public class GameStartedEvent : BaseDomainEvent
{
    public string RoomCode { get; }
    public Guid GameTableId { get; }
    public List<string> PlayerNames { get; }
    public PlayerId FirstPlayerTurn { get; }

    public GameStartedEvent(string roomCode, Guid gameTableId, List<string> playerNames, PlayerId firstPlayerTurn)
    {
        RoomCode = roomCode;
        GameTableId = gameTableId;
        PlayerNames = playerNames;
        FirstPlayerTurn = firstPlayerTurn;
    }
}