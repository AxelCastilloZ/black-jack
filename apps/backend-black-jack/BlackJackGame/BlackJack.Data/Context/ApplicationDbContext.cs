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

        // CRÍTICO: Ignorar todos los tipos que no deben persistirse
        IgnoreNonPersistentTypes(modelBuilder);

        // Aplicar configuraciones específicas
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
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

        // 2. Ignorar Value Objects
        modelBuilder.Ignore<Card>();
        modelBuilder.Ignore<Deck>();
        modelBuilder.Ignore<Money>();
        modelBuilder.Ignore<Bet>();
        modelBuilder.Ignore<Payout>();
        modelBuilder.Ignore<PlayerId>();

        // 3. CORREGIDO: NO ignorar enums - EF los maneja automáticamente
        // Los enums no necesitan ser ignorados explícitamente

        // 4. Ignorar interfaces
        modelBuilder.Ignore<IAggregateRoot>();
    }
}