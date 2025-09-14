using Microsoft.EntityFrameworkCore;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Data.Context;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<BlackjackTable> BlackjackTables => Set<BlackjackTable>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Hand> Hands => Set<Hand>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Spectator> Spectators => Set<Spectator>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}