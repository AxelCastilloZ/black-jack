using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Data.Configurations.Game;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(50);

        // CAMBIO AQUÍ: Usar HasConversion en lugar de OwnsOne para PlayerId
        builder.Property(p => p.PlayerId)
            .HasConversion(
                playerId => playerId.Value,        // Convertir PlayerId a Guid
                value => PlayerId.From(value)      // Convertir Guid a PlayerId
            )
            .HasColumnName("PlayerId");

        builder.OwnsOne(p => p.Balance, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Balance");
        });

        builder.HasMany(p => p.Hands)
            .WithOne()
            .HasForeignKey("PlayerId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(p => p.CurrentBet);
    }
}