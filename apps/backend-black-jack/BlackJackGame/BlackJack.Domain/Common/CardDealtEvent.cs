// CardDealtEvent.cs
using BlackJack.Domain.Models.Cards;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Common;

public class CardDealtEvent : BaseDomainEvent
{
    public string RoomCode { get; }
    public PlayerId? PlayerId { get; } // null si es para el dealer
    public Card Card { get; }
    public Guid HandId { get; }
    public bool IsVisible { get; } // para cartas ocultas del dealer

    public CardDealtEvent(string roomCode, PlayerId? playerId, Card card, Guid handId, bool isVisible = true)
    {
        RoomCode = roomCode;
        PlayerId = playerId;
        Card = card;
        HandId = handId;
        IsVisible = isVisible;
    }
}