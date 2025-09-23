using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Game;

namespace BlackJack.Data.Configurations.Users;

public class SpectatorConfiguration : IEntityTypeConfiguration<Spectator>
{
    public void Configure(EntityTypeBuilder<Spectator> builder)
    {
        builder.HasKey(s => s.Id);

  
        builder.Property(s => s.PlayerId)
            .HasConversion(
                playerId => playerId.Value,       
                value => PlayerId.From(value)     
            )
            .HasColumnName("PlayerId")
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.JoinedAt)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        // LIMPIADO: GameRoomId (requerido) - removido TableId completamente
        builder.Property(s => s.GameRoomId)
            .IsRequired()
            .HasColumnName("GameRoomId")
            .HasColumnType("uniqueidentifier");

        // Índices para optimización
        builder.HasIndex(s => s.PlayerId)
            .HasDatabaseName("IX_Spectators_PlayerId");

        builder.HasIndex(s => s.Name)
            .HasDatabaseName("IX_Spectators_Name");

        builder.HasIndex(s => s.JoinedAt)
            .HasDatabaseName("IX_Spectators_JoinedAt");

        // Índice para GameRoomId (performance crítico)
        builder.HasIndex(s => s.GameRoomId)
            .HasDatabaseName("IX_Spectators_GameRoomId");

        // LIMPIADO: Configuración de relación SOLO con GameRoom
        builder.HasOne(s => s.GameRoom)
            .WithMany(gr => gr.Spectators)
            .HasForeignKey(s => s.GameRoomId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_Spectators_GameRooms_GameRoomId");
    }
}