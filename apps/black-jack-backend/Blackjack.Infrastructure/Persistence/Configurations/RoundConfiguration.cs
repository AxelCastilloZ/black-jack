using Blackjack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blackjack.Infrastructure.Persistence.Configurations;

public class RoundConfiguration : IEntityTypeConfiguration<Round>
{
    public void Configure(EntityTypeBuilder<Round> builder)
    {
        builder.HasKey(r => r.Id);
        
        builder.Property(r => r.DealerHandJSON)
            .IsRequired()
            .HasMaxLength(1000);
            
        // Foreign key relationship
        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(r => r.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
