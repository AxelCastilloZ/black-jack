using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Game;

namespace BlackJack.Data.Configurations.Game;

public class RoomPlayerConfiguration : IEntityTypeConfiguration<RoomPlayer>
{
    public void Configure(EntityTypeBuilder<RoomPlayer> builder)
    {
        builder.HasKey(rp => rp.Id);

        // CORREGIDO: Configurar PlayerId como owned type correctamente
        builder.OwnsOne(rp => rp.PlayerId, playerId =>
        {
            playerId.Property(pid => pid.Value)
                .HasColumnName("PlayerId")
                .HasColumnType("uniqueidentifier")
                .IsRequired();
        });

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

        // ELIMINADO: Índices problemáticos en owned types
        // Solo crear índices simples
        builder.HasIndex(rp => rp.Position);
    }
}