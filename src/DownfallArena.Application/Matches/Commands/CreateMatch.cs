using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Commands;

/// <summary>
/// Opens a match with the given rule set, waiting for two players. The seed drives every random roll of the
/// match; the same seed, content, and decisions replay the same match.
/// </summary>
/// <param name="Id">
/// The id the match is given, or none for a new one. A host that rebuilds a match from its seed and its
/// recorded decisions names it as it was, so what was written about the match -- its trace, its notes --
/// still names it (ADR 0091).
/// </param>
public sealed record CreateMatch(RuleSet RuleSet, int Seed, MatchId? Id = null);
