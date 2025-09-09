using Blackjack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blackjack.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        
        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(u => u.Balance)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0);
            
        builder.Property(u => u.PasswordHash)
            .HasMaxLength(255);
    }
}
