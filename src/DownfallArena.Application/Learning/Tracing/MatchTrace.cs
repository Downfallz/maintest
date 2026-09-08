using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Learning.Tracing;

/// <summary>
/// The full record of one match: every domain event with both boards after it, enough for the viewer to replay
/// it without the engine, stamped with the run that produced it.
/// </summary>
public sealed record MatchTrace
{
    public required MatchId MatchId { get; init; }

    public int? Seed { get; init; }

    public required RunStamp Stamp { get; init; }

    public required IReadOnlyList<TraceEntry> Entries { get; init; }

    public MatchOutcome? Outcome { get; init; }
}
