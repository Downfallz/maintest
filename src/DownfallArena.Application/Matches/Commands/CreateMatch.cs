using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Matches.Commands;

/// <summary>
/// Opens a match with the given rule set, waiting for two players. The seed drives every random roll of the
/// match; the same seed, content, and decisions replay the same match.
/// </summary>
public sealed record CreateMatch(RuleSet RuleSet, int Seed);
