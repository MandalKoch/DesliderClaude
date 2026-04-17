using DesliderClaude.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DesliderClaude.Data.Configurations;

internal sealed class VoterConfiguration : IEntityTypeConfiguration<Voter>
{
    public void Configure(EntityTypeBuilder<Voter> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.VoterToken).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.VoterToken).IsUnique();

        builder.HasMany(x => x.Swipes)
            .WithOne(s => s.Voter)
            .HasForeignKey(s => s.VoterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
