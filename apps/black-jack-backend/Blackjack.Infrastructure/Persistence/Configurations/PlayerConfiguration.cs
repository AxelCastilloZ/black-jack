using Blackjack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blackjack.Infrastructure.Persistence.Configurations;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.Nickname)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(p => p.BalanceShadow)
            .HasColumnType("decimal(18,2)");
            
        // Unique constraint: SeatIndex must be unique within a Room
        builder.HasIndex(p => new { p.RoomId, p.SeatIndex })
            .IsUnique();
            
        // Foreign key relationships
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.SetNull);
            
        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(p => p.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
