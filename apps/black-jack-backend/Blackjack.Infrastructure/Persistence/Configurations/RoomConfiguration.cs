using Blackjack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blackjack.Infrastructure.Persistence.Configurations;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(r => r.Id);
        
        builder.Property(r => r.Code)
            .IsRequired()
            .HasMaxLength(10);
            
        builder.HasIndex(r => r.Code)
            .IsUnique();
            
        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(r => r.MinBet)
            .HasColumnType("decimal(18,2)");
            
        builder.Property(r => r.MaxBet)
            .HasColumnType("decimal(18,2)");
            
        builder.Property(r => r.PayoutBlackjack)
            .HasColumnType("decimal(18,2)");
    }
}
