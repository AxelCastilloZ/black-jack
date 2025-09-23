using Microsoft.EntityFrameworkCore;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Common;
using BlackJack.Domain.Models.Cards;
using BlackJack.Domain.Models.Betting;

namespace BlackJack.Data.Context;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Entidades principales
    public DbSet<BlackjackTable> BlackjackTables => Set<BlackjackTable>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Spectator> Spectators => Set<Spectator>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<GameRoom> GameRooms => Set<GameRoom>();
    public DbSet<RoomPlayer> RoomPlayers => Set<RoomPlayer>();
    public DbSet<Hand> Hands => Set<Hand>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // CRÍTICO: Ignorar solo tipos que realmente no deben persistirse
        IgnoreNonPersistentTypes(modelBuilder);

        // Aplicar configuraciones específicas
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // NUEVO: Configuraciones adicionales para resolver conflictos de mapping
        ConfigureSpecialMappings(modelBuilder);
    }

    private static void IgnoreNonPersistentTypes(ModelBuilder modelBuilder)
    {
        // 1. Ignorar eventos de dominio
        modelBuilder.Ignore<BaseDomainEvent>();
        modelBuilder.Ignore<CardDealtEvent>();
        modelBuilder.Ignore<GameEndedEvent>();
        modelBuilder.Ignore<GameStartedEvent>();
        modelBuilder.Ignore<PlayerActionEvent>();
        modelBuilder.Ignore<PlayerJoinedRoomEvent>();
        modelBuilder.Ignore<PlayerLeftRoomEvent>();
        modelBuilder.Ignore<SpectatorJoinedEvent>();
        modelBuilder.Ignore<SpectatorLeftEvent>();
        modelBuilder.Ignore<TurnChangedEvent>();

        
        modelBuilder.Ignore<Card>();
        modelBuilder.Ignore<Deck>();

     
        modelBuilder.Ignore<Bet>();
        modelBuilder.Ignore<Payout>();

        // 3. Ignorar interfaces
        modelBuilder.Ignore<IAggregateRoot>();
    }

    private static void ConfigureSpecialMappings(ModelBuilder modelBuilder)
    {
       
        modelBuilder.HasDefaultSchema("dbo");

       
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Configurar tabla nombres consistentes
            if (entityType.ClrType == typeof(GameRoom))
            {
                entityType.SetTableName("GameRooms");
            }
            else if (entityType.ClrType == typeof(RoomPlayer))
            {
                entityType.SetTableName("RoomPlayers");
            }
            else if (entityType.ClrType == typeof(Spectator))
            {
                entityType.SetTableName("Spectators");
            }
        }

       
        modelBuilder.Entity<GameRoom>()
            .Navigation(e => e.Players)
            .EnableLazyLoading(false); // Evitar lazy loading problemático

        modelBuilder.Entity<GameRoom>()
            .Navigation(e => e.Spectators)
            .EnableLazyLoading(false); // Evitar lazy loading problemático
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

       
        if (!optionsBuilder.IsConfigured)
        {
            // Solo para debugging - remover en producción
            optionsBuilder.EnableSensitiveDataLogging()
                         .EnableDetailedErrors();
        }

       
        optionsBuilder.UseSqlServer(o =>
        {
            o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });
    }
}