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

        // ✅ CORREGIDO: Usar HasConversion en lugar de OwnsOne
        builder.Property(rp => rp.PlayerId)
            .HasConversion(
                playerId => playerId.Value,       // Convertir PlayerId a Guid para DB
                value => PlayerId.From(value)     // Convertir Guid a PlayerId desde DB
            )
            .HasColumnName("PlayerId")
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(rp => rp.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(rp => rp.Position)
            .IsRequired();

        builder.Property(rp => rp.IsReady)
            .IsRequired();

        builder.Property(rp => rp.HasPlayedTurn)
            .IsRequired();

        builder.Property(rp => rp.JoinedAt)
            .IsRequired();

        builder.Property(rp => rp.LastActionAt)
            .IsRequired(false);

        // Índices
        builder.HasIndex(rp => rp.Position);

        // Crear índice en PlayerId para consultas rápidas
        builder.HasIndex(rp => rp.PlayerId);
    }
}