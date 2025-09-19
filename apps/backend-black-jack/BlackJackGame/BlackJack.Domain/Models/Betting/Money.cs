namespace BlackJack.Domain.Models.Betting;

public class Money
{
    protected Money()
    {
        Amount = 0m;
    }

    public Money(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(amount));

        Amount = amount;
    }

    public decimal Amount { get; private set; }

    // AGREGADO: Conversión implícita a decimal para resolver errores CS1503
    public static implicit operator decimal(Money money) => money?.Amount ?? 0m;

    // AGREGADO: Conversión explícita de decimal a Money
    public static explicit operator Money(decimal amount) => new Money(amount);

    public Money Add(Money other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        return new Money(Amount + other.Amount);
    }

    public Money Subtract(Money other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        var result = Amount - other.Amount;
        if (result < 0)
            throw new InvalidOperationException("Cannot subtract more money than available");

        return new Money(result);
    }

    public Money Multiply(decimal multiplier)
    {
        if (multiplier < 0)
            throw new ArgumentException("Multiplier cannot be negative", nameof(multiplier));

        return new Money(Amount * multiplier);
    }

    public bool IsGreaterThan(Money? other)
    {
        return other != null && Amount > other.Amount;
    }

    public bool IsLessThan(Money? other)
    {
        return other != null && Amount < other.Amount;
    }

    public bool Equals(Money? other)
    {
        return other != null && Amount == other.Amount;
    }

    public override bool Equals(object? obj)
    {
        if (obj is Money money)
            return Equals(money);
        return false;
    }

    public override int GetHashCode()
    {
        return Amount.GetHashCode();
    }

    public override string ToString()
    {
        return $"${Amount:F2}";
    }

    // Static factory methods
    public static Money Zero => new Money(0m);
    public static Money Create(decimal amount) => new Money(amount);

    // Operators
    public static Money operator +(Money left, Money right) => left.Add(right);
    public static Money operator -(Money left, Money right) => left.Subtract(right);
    public static Money operator *(Money money, decimal multiplier) => money.Multiply(multiplier);
    public static bool operator >(Money? left, Money? right) => left?.IsGreaterThan(right) ?? false;
    public static bool operator <(Money? left, Money? right) => left?.IsLessThan(right) ?? false;
    public static bool operator ==(Money? left, Money? right) => left?.Equals(right) ?? right is null;
    public static bool operator !=(Money? left, Money? right) => !(left == right);
}