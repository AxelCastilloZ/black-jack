using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlackJack.Data.Identity;

public class IdentityDbContext : IdentityDbContext<ApplicationUser>
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure ApplicationUser
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.OwnsOne(u => u.PlayerId, playerId =>
            {
                playerId.Property(p => p.Value).HasColumnName("PlayerId");
            });
        });
    }
}