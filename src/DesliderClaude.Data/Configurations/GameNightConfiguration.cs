using DesliderClaude.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DesliderClaude.Data.Configurations;

internal sealed class GameNightConfiguration : IEntityTypeConfiguration<GameNight>
{
    public void Configure(EntityTypeBuilder<GameNight> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ShareCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.HostToken).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.ShareCode).IsUnique();

        builder.HasMany(x => x.Games)
            .WithOne(g => g.GameNight)
            .HasForeignKey(g => g.GameNightId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Voters)
            .WithOne(v => v.GameNight)
            .HasForeignKey(v => v.GameNightId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
