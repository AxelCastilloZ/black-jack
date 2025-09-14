namespace BlackJack.Domain.Models.Betting;

public record Money(decimal Amount)
{
    public static Money Zero => new(0);

    public Money Add(Money other) => new(Amount + other.Amount);
    public Money Subtract(Money other) => new(Amount - other.Amount);
    public Money Multiply(decimal factor) => new(Amount * factor);

    public bool IsGreaterThan(Money other) => Amount > other.Amount;
    public bool IsGreaterOrEqual(Money other) => Amount >= other.Amount;
    public bool IsLessThan(Money other) => Amount < other.Amount;

    public static Money operator +(Money left, Money right) => left.Add(right);
    public static Money operator -(Money left, Money right) => left.Subtract(right);
    public static Money operator *(Money left, decimal right) => left.Multiply(right);

    public override string ToString() => $"${Amount:F2}";
}