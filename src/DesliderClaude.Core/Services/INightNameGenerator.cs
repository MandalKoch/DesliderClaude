namespace DesliderClaude.Core.Services;

/// <summary>
/// Produces a random, slightly-silly default name for a new Game Night
/// (e.g. "Feral Gambit", "Caffeinated Showdown"). Pure; safe to call anywhere.
/// </summary>
public interface INightNameGenerator
{
    string Generate();
}
