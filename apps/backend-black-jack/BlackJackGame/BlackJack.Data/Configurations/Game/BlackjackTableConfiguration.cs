using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Game;

namespace BlackJack.Data.Configurations.Game;

public class BlackjackTableConfiguration : IEntityTypeConfiguration<BlackjackTable>
{
    public void Configure(EntityTypeBuilder<BlackjackTable> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        // Configure Money value objects
        builder.OwnsOne(t => t.MinBet, money =>
        {
            money.Property(m => m.Amount).HasColumnName("MinBet");
        });

        builder.OwnsOne(t => t.MaxBet, money =>
        {
            money.Property(m => m.Amount).HasColumnName("MaxBet");
        });

        // Configure relationships
        builder.HasMany(t => t.Seats)
            .WithOne()
            .HasForeignKey("TableId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Spectators)
            .WithOne()
            .HasForeignKey("TableId")
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore complex properties for now
        builder.Ignore(t => t.Deck);
        builder.Ignore(t => t.DealerHand);
    }
}