// BlackJack.Data/Configurations/Game/RoomPlayerConfiguration.cs - CORREGIDO CON PlayerEntityId FK
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Data.Configurations.Game;

public class RoomPlayerConfiguration : IEntityTypeConfiguration<RoomPlayer>
{
    public void Configure(EntityTypeBuilder<RoomPlayer> builder)
    {
        builder.HasKey(rp => rp.Id);

        // Configuración de PlayerId con conversión (DOMINIO - Value Object)
        builder.Property(rp => rp.PlayerId)
            .HasConversion(
                playerId => playerId.Value,       // Convertir PlayerId a Guid para DB
                value => PlayerId.From(value)     // Convertir Guid a PlayerId desde DB
            )
            .HasColumnName("PlayerId")
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        // NUEVO: Configuración de PlayerEntityId (ENTITY FRAMEWORK - Foreign Key)
        builder.Property(rp => rp.PlayerEntityId)
            .HasColumnName("PlayerEntityId")
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        // Configuración de campos básicos
        builder.Property(rp => rp.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(rp => rp.Position)
            .IsRequired();

        // Configuración para SeatPosition
        builder.Property(rp => rp.SeatPosition)
            .IsRequired(false)  // Nullable - null significa no sentado
            .HasColumnName("SeatPosition");

        builder.Property(rp => rp.IsReady)
            .IsRequired();

        builder.Property(rp => rp.HasPlayedTurn)
            .IsRequired();

        builder.Property(rp => rp.IsViewer)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(rp => rp.JoinedAt)
            .IsRequired();

        builder.Property(rp => rp.LastActionAt)
            .IsRequired(false);

        builder.Property(rp => rp.CreatedAt)
            .IsRequired();

        builder.Property(rp => rp.UpdatedAt)
            .IsRequired();

        // Índices para optimización
        builder.HasIndex(rp => rp.Position)
            .HasDatabaseName("IX_RoomPlayers_Position");

        builder.HasIndex(rp => rp.PlayerId)
            .HasDatabaseName("IX_RoomPlayers_PlayerId");

        // NUEVO: Índice para PlayerEntityId (FK performance)
        builder.HasIndex(rp => rp.PlayerEntityId)
            .HasDatabaseName("IX_RoomPlayers_PlayerEntityId");

        // Índice para SeatPosition
        builder.HasIndex(rp => rp.SeatPosition)
            .HasDatabaseName("IX_RoomPlayers_SeatPosition");

        // Índice compuesto para GameRoomId + SeatPosition
        builder.HasIndex(rp => new { rp.GameRoomId, rp.SeatPosition })
            .HasDatabaseName("IX_RoomPlayers_GameRoomId_SeatPosition")
            .IsUnique(false); // No único porque SeatPosition puede ser null

        // Configuración de relaciones
        builder.HasOne(rp => rp.GameRoom)
            .WithMany(gr => gr.Players)
            .HasForeignKey(rp => rp.GameRoomId)
            .OnDelete(DeleteBehavior.Cascade);

        // CORREGIDO: Navigation Property usando PlayerEntityId como FK
        builder.HasOne(rp => rp.Player)
            .WithMany() // Un Player puede tener múltiples RoomPlayers en diferentes salas
            .HasForeignKey(rp => rp.PlayerEntityId) // ← CORREGIDO: Usar PlayerEntityId
            .HasPrincipalKey(p => p.Id) // ← EXPLÍCITO: Relacionar con Player.Id
            .OnDelete(DeleteBehavior.Restrict); // No borrar Player si se borra RoomPlayer
    }
}