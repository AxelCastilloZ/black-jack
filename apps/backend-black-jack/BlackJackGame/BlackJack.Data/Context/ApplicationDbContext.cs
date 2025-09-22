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

        // 2. Ignorar SOLO Value Objects que no se usan en entities
        modelBuilder.Ignore<Card>();
        modelBuilder.Ignore<Deck>();

        // FIX CRÍTICO: NO ignorar PlayerId, Money, etc. porque SÍ se usan en entities
        // Estos se manejan con HasConversion en las configuraciones específicas
        // modelBuilder.Ignore<PlayerId>();  // REMOVIDO - causaba el conflicto
        // modelBuilder.Ignore<Money>();     // REMOVIDO - se usa en GameRoom.MinBetPerRound

        // Ignorar Value Objects que realmente no se persisten
        modelBuilder.Ignore<Bet>();
        modelBuilder.Ignore<Payout>();

        // 3. Ignorar interfaces
        modelBuilder.Ignore<IAggregateRoot>();
    }

    private static void ConfigureSpecialMappings(ModelBuilder modelBuilder)
    {
        // NUEVO: Configuración global para PlayerId si no está manejado por configuraciones específicas
        // Esto asegura consistencia en todo el proyecto

        // Configurar query splitting para mejorar performance con múltiples Includes
        // Esto resuelve el warning sobre QuerySplittingBehavior
        modelBuilder.HasDefaultSchema("dbo");

        // CRÍTICO: Configurar comportamiento de query splitting para evitar problemas de performance
        // con múltiples collections (Players y Spectators en GameRoom)
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

        // NUEVO: Configuración específica para prevenir ReadOnly collections
        // Asegurar que las navigation properties se manejen correctamente
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

        // NUEVO: Configuración para debugging y performance
        if (!optionsBuilder.IsConfigured)
        {
            // Solo para debugging - remover en producción
            optionsBuilder.EnableSensitiveDataLogging()
                         .EnableDetailedErrors();
        }

        // CRÍTICO: Configurar query splitting behavior para múltiples collections
        optionsBuilder.UseSqlServer(o =>
        {
            o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });
    }
}