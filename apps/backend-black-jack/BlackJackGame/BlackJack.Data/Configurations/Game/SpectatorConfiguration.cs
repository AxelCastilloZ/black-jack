using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Data.Configurations.Users;

public class SpectatorConfiguration : IEntityTypeConfiguration<Spectator>
{
    public void Configure(EntityTypeBuilder<Spectator> builder)
    {
        builder.HasKey(s => s.Id);

        // CORREGIDO: PlayerId como owned type
        builder.OwnsOne(s => s.PlayerId, playerId =>
        {
            playerId.Property(pid => pid.Value)
                .HasColumnName("PlayerId")
                .HasColumnType("uniqueidentifier")
                .IsRequired();
        });

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.JoinedAt)
            .IsRequired();

        // Índices simples
        builder.HasIndex(s => s.Name);
        builder.HasIndex(s => s.JoinedAt);
    }
}