using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Game;

namespace BlackJack.Data.Configurations.Game;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(50);

        
        builder.OwnsOne(p => p.PlayerId, playerId =>
        {
            playerId.Property(pid => pid.Value).HasColumnName("PlayerId");
        });

        
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