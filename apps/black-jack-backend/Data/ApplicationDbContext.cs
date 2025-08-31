using Microsoft.EntityFrameworkCore;
using black_jack_backend.Entities;

namespace black_jack_backend.Data;

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

        // Configure entity relationships and constraints here
        // This will be expanded as we create the entities
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Additional configuration if needed
        base.OnConfiguring(optionsBuilder);
    }
}