using BlackJack.Domain.Common;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Models.Game;

public class Seat : BaseEntity
{
    // EF Core constructor
    protected Seat() : base()
    {
        Position = 0;
        Player = null;
    }

    // Constructor principal
    public Seat(int position, Guid? id = null) : base(id ?? Guid.NewGuid())
    {
        if (position < 0 || position >= 6)
            throw new ArgumentException("Position must be between 0 and 5", nameof(position));

        Position = position;
        Player = null;
    }

    // Propiedades
    public int Position { get; private set; }
    public Player? Player { get; private set; }

    // Propiedades calculadas
    public bool IsOccupied => Player != null;
    public bool IsEmpty => Player == null;
    public PlayerId? OccupiedBy => Player?.PlayerId;

    // Métodos principales
    public void SeatPlayer(Player player)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));

        if (IsOccupied)
            throw new InvalidOperationException($"Seat {Position} is already occupied");

        Player = player;
        UpdateTimestamp();
    }

    public void ClearSeat()
    {
        if (Player != null)
        {
            Player.SetActive(false);
            Player = null;
            UpdateTimestamp();
        }
    }

    // Métodos de validación
    public bool CanSeatPlayer(Player player)
    {
        return player != null && IsEmpty;
    }

    public bool IsOccupiedBy(PlayerId playerId)
    {
        return IsOccupied && Player!.PlayerId == playerId;
    }

    // Factory methods
    public static Seat Create(int position)
    {
        return new Seat(position);
    }

    public static Seat CreateWithPlayer(int position, Player player)
    {
        var seat = new Seat(position);
        seat.SeatPlayer(player);
        return seat;
    }

    // Métodos de información
    public string GetDisplayInfo()
    {
        if (IsEmpty)
            return $"Seat {Position}: Empty";

        return $"Seat {Position}: {Player!.Name} (Balance: {Player.Balance})";
    }

    public override string ToString()
    {
        return GetDisplayInfo();
    }

    // Métodos de comparación
    public override bool Equals(object? obj)
    {
        if (obj is Seat other)
        {
            return Position == other.Position && Id == other.Id;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Position);
    }
}