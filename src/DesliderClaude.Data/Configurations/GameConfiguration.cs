using DesliderClaude.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DesliderClaude.Data.Configurations;

internal sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(512);
        builder.Property(x => x.ThumbnailUrl).HasMaxLength(512);

        builder.HasOne(x => x.BggGame)
            .WithMany()
            .HasForeignKey(x => x.BggGameId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Swipes)
            .WithOne(s => s.Game)
            .HasForeignKey(s => s.GameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
