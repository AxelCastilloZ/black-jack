namespace BlackJack.Domain.Models.Users;

public record TableId(Guid Value)
{
    public static TableId New() => new(Guid.NewGuid());
    public static TableId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}