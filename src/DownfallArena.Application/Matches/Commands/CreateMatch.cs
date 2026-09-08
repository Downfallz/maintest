using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Matches.Commands;

/// <summary>
/// Opens a match with the given rule set, waiting for two players.
/// </summary>
public sealed record CreateMatch(RuleSet RuleSet);
