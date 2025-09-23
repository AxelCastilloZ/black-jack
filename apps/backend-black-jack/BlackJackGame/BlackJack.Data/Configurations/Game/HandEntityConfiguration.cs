using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Cards;

namespace BlackJack.Data.Configurations.Game;

public class HandEntityConfiguration : IEntityTypeConfiguration<Hand>
{
    public void Configure(EntityTypeBuilder<Hand> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

     
        builder.Property<string>("CardsJson")
            .HasColumnName("Cards")
            .IsRequired(false);

  
        builder.Ignore(h => h.Value);
        builder.Ignore(h => h.IsSoft);
        builder.Ignore(h => h.IsBlackjack);
        builder.Ignore(h => h.IsBust);
        builder.Ignore(h => h.IsComplete);

        // NO ignorar Cards - EF Core la manejará automáticamente a través de CardsJson
        // La propiedad Cards se serializa/deserializa automáticamente

        // Índices para mejorar performance en consultas
        builder.HasIndex(h => h.Status)
            .HasDatabaseName("IX_Hands_Status");

        // Opcional: Agregar índice en CreatedAt para consultas temporales
        builder.HasIndex(h => h.CreatedAt)
            .HasDatabaseName("IX_Hands_CreatedAt");
    }
}