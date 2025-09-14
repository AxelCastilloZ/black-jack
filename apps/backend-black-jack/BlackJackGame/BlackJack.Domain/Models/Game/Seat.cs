using BlackJack.Domain.Common;
using System.Numerics;

namespace BlackJack.Domain.Models.Game;

public class Seat : BaseEntity
{
    public int Position { get; private set; }
    public bool IsOccupied { get; private set; }
    public Player? Player { get; private set; }

    private Seat() { } // EF Constructor

    public static Seat Create(int position)
    {
        return new Seat
        {
            Position = position,
            IsOccupied = false
        };
    }

    public void AssignPlayer(Player player)
    {
        Player = player;
        IsOccupied = true;
        UpdateTimestamp();
    }

    public void RemovePlayer()
    {
        Player = null;
        IsOccupied = false;
        UpdateTimestamp();
    }
}