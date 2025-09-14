using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Data.Configurations.Users;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.HasKey(u => u.Id);

        // Configure PlayerId value object
        builder.OwnsOne(u => u.PlayerId, playerId =>
        {
            playerId.Property(pid => pid.Value).HasColumnName("PlayerId");
        });

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        // Configure Money value objects
        builder.OwnsOne(u => u.Balance, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Balance");
        });

        builder.OwnsOne(u => u.TotalWinnings, money =>
        {
            money.Property(m => m.Amount).HasColumnName("TotalWinnings");
        });

        // Add unique index on PlayerId
        builder.HasIndex("PlayerId").IsUnique();
    }
}