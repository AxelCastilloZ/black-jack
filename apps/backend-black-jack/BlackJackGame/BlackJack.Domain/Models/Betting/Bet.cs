namespace BlackJack.Domain.Models.Betting;

public record Bet(Money Amount)
{
    public static Bet Create(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Bet amount must be greater than zero");

        return new Bet(new Money(amount));
    }

    public Money CalculateBlackjackPayout() => Amount.Multiply(1.5m);
    public Money CalculateWinPayout() => Amount.Multiply(2m);  // Apuesta original + ganancia

    public override string ToString() => Amount.ToString();
}