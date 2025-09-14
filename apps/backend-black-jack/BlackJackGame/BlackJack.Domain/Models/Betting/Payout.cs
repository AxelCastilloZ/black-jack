using BlackJack.Domain.Models.Betting;

namespace BlackJack.Domain.Models.Betting;

public record Payout(Money Amount, PayoutType Type)
{
    public static Payout Win(Bet originalBet) => new(originalBet.Amount.Multiply(2m), PayoutType.Win);
    public static Payout Blackjack(Bet originalBet) => new(originalBet.Amount.Multiply(2.5m), PayoutType.Blackjack);
    public static Payout Push(Bet originalBet) => new(originalBet.Amount, PayoutType.Push);
    public static Payout Loss() => new(Money.Zero, PayoutType.Loss);

    public override string ToString() => $"{Type}: {Amount}";
}

public enum PayoutType
{
    Loss = 0,
    Push = 1,
    Win = 2,
    Blackjack = 3
}