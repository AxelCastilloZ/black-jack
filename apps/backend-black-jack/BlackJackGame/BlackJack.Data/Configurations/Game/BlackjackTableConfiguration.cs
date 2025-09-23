// BlackjackTableConfiguration.cs - LIMPIA (Spectators removidos)
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;

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

        builder.Property(t => t.RoundNumber)
            .IsRequired();

        // Money Value Objects  
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

        // El Deck se maneja en memoria, no se persiste
        builder.Ignore(t => t.Deck);

        // CORREGIDO: DealerHandId como Guid nullable
        builder.Property(t => t.DealerHandId)
            .HasColumnName("DealerHandId")
            .IsRequired(false);

        // LIMPIADO: Solo relación con Seats (Spectators removidos completamente)
        builder.HasMany(t => t.Seats)
            .WithOne()
            .HasForeignKey("TableId")
            .OnDelete(DeleteBehavior.Cascade);

        // REMOVIDO: Ya no existe t.Spectators en BlackjackTable
        // builder.HasMany(t => t.Spectators)  // ❌ REMOVIDO - causaba CS1061
        //     .WithOne()
        //     .HasForeignKey("TableId")
        //     .OnDelete(DeleteBehavior.Cascade);

        // Índices para performance
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.Name);
    }
}