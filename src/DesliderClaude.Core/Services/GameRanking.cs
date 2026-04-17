namespace DesliderClaude.Core.Services;

public sealed record GameRanking(Guid GameId, string Name, int YesCount, int NoCount);
