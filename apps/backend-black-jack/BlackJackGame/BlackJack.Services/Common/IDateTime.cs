namespace BlackJack.Services.Common;

public interface IDateTime
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
}