using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Users;
using System.Text.Json;

namespace BlackJack.Data.Configurations.Users;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.HasKey(p => p.Id);

        // PlayerId como owned type
        builder.OwnsOne(p => p.PlayerId, playerId =>
        {
            playerId.Property(pid => pid.Value)
                .HasColumnName("PlayerId")
                .HasColumnType("uniqueidentifier")
                .IsRequired();
        });

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Configure Balance as Value Object
        builder.OwnsOne(p => p.Balance, money =>
        {
            money.Property(m => m.Amount)
                 .HasColumnName("Balance")
                 .HasColumnType("decimal(18,2)")
                 .IsRequired();
        });

        // SIMPLIFICADO: Ignorar CurrentBet por ahora
        builder.Ignore(p => p.CurrentBet);

        builder.Property(p => p.IsActive)
            .IsRequired();

        // ✅ FIX CRÍTICO: Configuración correcta para HandIds con JSON serialization
        builder.Property(p => p.HandIds)
            .HasColumnName("HandIds")
            .HasColumnType("nvarchar(max)")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => string.IsNullOrEmpty(v)
                    ? new List<Guid>()
                    : JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions)null!)
            );

        // Índices simples
        builder.HasIndex(p => p.Name);
    }
}