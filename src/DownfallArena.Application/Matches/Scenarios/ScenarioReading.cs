using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// The rounds a recorded match can be cut at: each one the match reached, began and did not end in, in order,
/// as far as its journal rebuilds. A journal that stops rebuilding offers the rounds before the line it stopped
/// at, and says why in <paramref name="StoppedBy" />: an engine change can be reproduced on the rounds it has not
/// reached yet.
/// </summary>
/// <param name="Rounds">The rounds offered, each with where it begins in the journal.</param>
/// <param name="StoppedBy">The refusal that ended the reading before the journal did, or none.</param>
public sealed record ScenarioReading(IReadOnlyList<RoundCut> Rounds, DomainError? StoppedBy);
