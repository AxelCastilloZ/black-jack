using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlackJack.Domain.Models.Game;

namespace BlackJack.Data.Configurations.Game
{
    public class SpectatorConfiguration : IEntityTypeConfiguration<Spectator>
    {
        public void Configure(EntityTypeBuilder<Spectator> builder)
        {
            builder.HasKey(x => x.Id);

            builder.OwnsOne(x => x.PlayerId, pid =>
            {
                pid.Property(p => p.Value)
                   .HasColumnName("PlayerId")
                   .IsRequired();
            });

            // otras props/relaciones si aplican...
        }
    }
}
