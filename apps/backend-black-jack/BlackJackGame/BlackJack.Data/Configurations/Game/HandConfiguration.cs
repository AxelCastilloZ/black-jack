using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Game;

namespace BlackJack.Data.Configurations.Game;

public class HandConfiguration : IEntityTypeConfiguration<Hand>
{
    public void Configure(EntityTypeBuilder<Hand> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(h => h.Value)
            .IsRequired();

        builder.Property(h => h.IsSoft)
            .IsRequired();

        // Store cards as JSON for now
        builder.Property(h => h.Cards)
            .HasConversion(
                cards => System.Text.Json.JsonSerializer.Serialize(cards, (System.Text.Json.JsonSerializerOptions?)null),
                json => System.Text.Json.JsonSerializer.Deserialize<List<BlackJack.Domain.Models.Cards.Card>>(json, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<BlackJack.Domain.Models.Cards.Card>()
            );
    }
}