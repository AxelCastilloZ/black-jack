using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Data.Configurations.Users;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.HasKey(u => u.Id);

        
        builder.OwnsOne(u => u.PlayerId, pid =>
        {
            pid.Property(p => p.Value)
               .HasColumnName("PlayerId")
               .IsRequired();

          
        });

        builder.Property(u => u.DisplayName)
               .IsRequired()
               .HasMaxLength(50);

        builder.Property(u => u.Email)
               .IsRequired()
               .HasMaxLength(255);

        builder.OwnsOne(u => u.Balance, money =>
        {
            money.Property(m => m.Amount)
                 .HasColumnName("Balance")
                 .HasColumnType("decimal(18,2)")
                 .IsRequired();
        });

        builder.OwnsOne(u => u.TotalWinnings, money =>
        {
            money.Property(m => m.Amount)
                 .HasColumnName("TotalWinnings")
                 .HasColumnType("decimal(18,2)")
                 .IsRequired();
        });
    }
}
