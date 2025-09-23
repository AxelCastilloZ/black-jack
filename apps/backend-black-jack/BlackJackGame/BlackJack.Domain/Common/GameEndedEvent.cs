
using BlackJack.Domain.Models.Betting;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Common;

public class GameEndedEvent : BaseDomainEvent
{
    public string RoomCode { get; }
    public List<PlayerResult> Results { get; }
    public int DealerHandValue { get; }
    public PlayerId? WinnerId { get; }

    public GameEndedEvent(string roomCode, List<PlayerResult> results, int dealerHandValue, PlayerId? winnerId)
    {
        RoomCode = roomCode;
        Results = results;
        DealerHandValue = dealerHandValue;
        WinnerId = winnerId;
    }
}

public class PlayerResult
{
    public PlayerId PlayerId { get; }
    public string PlayerName { get; }
    public int HandValue { get; }
    public bool Won { get; }
    public Money Winnings { get; }
    public PayoutType PayoutType { get; }

    public PlayerResult(PlayerId playerId, string playerName, int handValue, bool won, Money winnings, PayoutType payoutType)
    {
        PlayerId = playerId;
        PlayerName = playerName;
        HandValue = handValue;
        Won = won;
        Winnings = winnings;
        PayoutType = payoutType;
    }
}