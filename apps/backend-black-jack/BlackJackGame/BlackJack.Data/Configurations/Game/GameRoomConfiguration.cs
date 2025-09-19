// GameRoomConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Game;

namespace BlackJack.Data.Configurations.Game;

public class GameRoomConfiguration : IEntityTypeConfiguration<GameRoom>
{
    public void Configure(EntityTypeBuilder<GameRoom> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.RoomCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Configure HostPlayerId as Value Object
        builder.OwnsOne(g => g.HostPlayerId, hostId =>
        {
            hostId.Property(h => h.Value)
                .HasColumnName("HostPlayerId")
                .IsRequired();
        });

        builder.Property(g => g.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(g => g.MaxPlayers)
            .IsRequired();

        builder.Property(g => g.CurrentPlayerIndex)
            .IsRequired();

        builder.Property(g => g.BlackjackTableId)
            .IsRequired(false);

        // Relaciones
        builder.HasMany(g => g.Players)
            .WithOne()
            .HasForeignKey("GameRoomId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.Spectators)
            .WithOne()
            .HasForeignKey("GameRoomId")
            .OnDelete(DeleteBehavior.Cascade);

        // Índices
        builder.HasIndex(g => g.RoomCode)
            .IsUnique();

        builder.HasIndex(g => g.Status);
        builder.HasIndex(g => g.Name);
    }
}