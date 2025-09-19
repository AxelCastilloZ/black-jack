using BlackJack.Domain.Models.Betting;

namespace BlackJack.Domain.Models.Betting;

public class Bet
{
    // Constructor sin parámetros para EF Core
    protected Bet()
    {
        Amount = new Money(0m);
    }

    public Bet(Money amount)
    {
        if (amount == null)
            throw new ArgumentNullException(nameof(amount));

        if (amount.Amount <= 0)
            throw new ArgumentException("Bet amount must be greater than zero", nameof(amount));

        Amount = amount;
    }

    public Money Amount { get; private set; } = default!;

    public bool IsValid()
    {
        return Amount != null && Amount.Amount > 0;
    }

    public bool IsWithinLimits(Money minBet, Money maxBet)
    {
        if (minBet == null || maxBet == null)
            return false;

        return Amount.Amount >= minBet.Amount && Amount.Amount <= maxBet.Amount;
    }

    public override bool Equals(object? obj)
    {
        if (obj is Bet other)
            return Amount.Equals(other.Amount);
        return false;
    }

    public override int GetHashCode()
    {
        return Amount?.GetHashCode() ?? 0;
    }

    public override string ToString()
    {
        return $"Bet: {Amount}";
    }

    // Factory method
    public static Bet Create(decimal amount)
    {
        return new Bet(new Money(amount));
    }

    public static Bet Create(Money amount)
    {
        return new Bet(amount);
    }
}