using DesliderClaude.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DesliderClaude.Data;

public sealed class DesliderClaudeDbContext : DbContext
{
    public DesliderClaudeDbContext(DbContextOptions<DesliderClaudeDbContext> options)
        : base(options) { }

    public DbSet<GameNight> GameNights => Set<GameNight>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Voter> Voters => Set<Voter>();
    public DbSet<Swipe> Swipes => Set<Swipe>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DesliderClaudeDbContext).Assembly);
    }
}
