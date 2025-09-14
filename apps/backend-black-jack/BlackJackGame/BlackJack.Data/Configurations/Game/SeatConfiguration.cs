using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Game;

namespace BlackJack.Data.Configurations.Game;

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Position)
            .IsRequired();

        builder.Property(s => s.IsOccupied)
            .IsRequired();

        // Configure relationship with Player
        builder.HasOne(s => s.Player)
            .WithMany()
            .HasForeignKey("PlayerId")
            .OnDelete(DeleteBehavior.SetNull);
    }
}