using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Users;

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

        // SIMPLIFICADO: HandIds como string simple
        builder.Property<string>("_handIdsJson")
               .HasColumnName("HandIds")
               .HasColumnType("nvarchar(max)");

        builder.Ignore(p => p.HandIds);

        // ELIMINADO: Player no hereda de AggregateRoot, así que NO tiene DomainEvents
        // builder.Ignore(p => p.DomainEvents); // ❌ Esta línea causa el error

        // Índices simples
        builder.HasIndex(p => p.Name);
    }
}