using Microsoft.EntityFrameworkCore;
using Blackjack.Domain.Entities;
using Blackjack.Infrastructure.Persistence.Configurations;

namespace Blackjack.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // DbSet properties for your entities
    public DbSet<User> Users { get; set; }
    public DbSet<Room> Rooms { get; set; }
    public DbSet<Player> Players { get; set; }
    public DbSet<Round> Rounds { get; set; }
    public DbSet<Hand> Hands { get; set; }
    public DbSet<ActionLog> ActionLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RoomConfiguration());
        modelBuilder.ApplyConfiguration(new PlayerConfiguration());
        modelBuilder.ApplyConfiguration(new RoundConfiguration());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Additional configuration if needed
        base.OnConfiguring(optionsBuilder);
    }
}