// GameRoomConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;

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

        // ✅ CORREGIDO: Usar HasConversion en lugar de OwnsOne
        builder.Property(g => g.HostPlayerId)
            .HasConversion(
                playerId => playerId.Value,       // Convertir PlayerId a Guid para DB
                value => PlayerId.From(value)     // Convertir Guid a PlayerId desde DB
            )
            .HasColumnName("HostPlayerId")
            .HasColumnType("uniqueidentifier")
            .IsRequired();

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

        // Índice en HostPlayerId para consultas rápidas
        builder.HasIndex(g => g.HostPlayerId);
    }
}