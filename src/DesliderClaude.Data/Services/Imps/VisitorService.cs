using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DesliderClaude.Data.Services.Imps;

internal sealed class VisitorService : IVisitorService
{
    private readonly DesliderClaudeDbContext _db;

    public VisitorService(DesliderClaudeDbContext db) => _db = db;

    public Task<Visitor?> GetByTokenAsync(string token, CancellationToken ct = default)
        => _db.Visitors.FirstOrDefaultAsync(v => v.Token == token, ct);

    public async Task<Visitor> CreateAsync(string token, string displayName, CancellationToken ct = default)
    {
        var visitor = new Visitor
        {
            Token = token,
            DisplayName = displayName,
        };
        _db.Visitors.Add(visitor);
        await _db.SaveChangesAsync(ct);
        return visitor;
    }

    public async Task<Visitor?> UpdateDisplayNameAsync(string token, string displayName, CancellationToken ct = default)
    {
        var visitor = await _db.Visitors.FirstOrDefaultAsync(v => v.Token == token, ct);
        if (visitor is null) return null;
        visitor.DisplayName = displayName;
        visitor.LastSeenAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return visitor;
    }

    public async Task TouchAsync(string token, CancellationToken ct = default)
    {
        var visitor = await _db.Visitors.FirstOrDefaultAsync(v => v.Token == token, ct);
        if (visitor is null) return;
        visitor.LastSeenAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
