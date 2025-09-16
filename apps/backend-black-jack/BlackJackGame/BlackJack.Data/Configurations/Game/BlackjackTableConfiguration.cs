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
            .HasConversion<string>()   // enum a string
            .HasMaxLength(50);

        // --- Money (Value Objects) ---
        builder.OwnsOne(t => t.MinBet, money =>
        {
            money.Property(m => m.Amount)
                 .HasColumnName("MinBet")
                 .HasColumnType("decimal(18,2)")
                 .IsRequired();
        });

        builder.OwnsOne(t => t.MaxBet, money =>
        {
            money.Property(m => m.Amount)
                 .HasColumnName("MaxBet")
                 .HasColumnType("decimal(18,2)")
                 .IsRequired();
        });

        // --- Relaciones ---
        builder.HasMany(t => t.Seats)
            .WithOne()
            .HasForeignKey("TableId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Spectators)
            .WithOne()
            .HasForeignKey("TableId")
            .OnDelete(DeleteBehavior.Cascade);

        // --- Propiedades complejas (no mapeadas por ahora) ---
        builder.Ignore(t => t.Deck);
        builder.Ignore(t => t.DealerHand);
    }
}
