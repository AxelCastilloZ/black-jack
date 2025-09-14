namespace BlackJack.Domain.Models.Users;

public record SeatPosition(int Position)
{
    public const int MinPosition = 1;
    public const int MaxPosition = 6;

    public static SeatPosition Create(int position)
    {
        if (position < MinPosition || position > MaxPosition)
            throw new ArgumentException($"Seat position must be between {MinPosition} and {MaxPosition}");

        return new SeatPosition(position);
    }

    public override string ToString() => $"Seat {Position}";
}