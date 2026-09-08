using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Commands;

/// <summary>
/// Resolves the next combat action of the timeline. Any host may drive resolution; it is not a player decision.
/// </summary>
public sealed record ResolveNextAction(MatchId MatchId);
