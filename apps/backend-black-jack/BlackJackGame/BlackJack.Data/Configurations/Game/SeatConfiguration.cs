// SeatConfiguration.cs - CORREGIDA
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Game;

namespace BlackJack.Data.Configurations.Game;

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        // CORREGIDO: Usar Id heredado de BaseEntity como clave primaria
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Position)
            .IsRequired();

        // Relationship with Player (nullable)
        builder.HasOne(s => s.Player)
            .WithMany()
            .HasForeignKey("PlayerId")
            .OnDelete(DeleteBehavior.SetNull);

        // Index for performance
        builder.HasIndex(s => s.Position);
    }
}