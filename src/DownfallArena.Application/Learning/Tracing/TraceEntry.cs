using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;

namespace DownfallArena.Application.Learning.Tracing;

/// <summary>
/// One domain event of a match and both players' boards once the command that raised it was applied.
/// </summary>
public sealed record TraceEntry
{
    public required int Sequence { get; init; }

    public int? Round { get; init; }

    public RoundSubPhase? SubPhase { get; init; }

    public required IMatchEvent Event { get; init; }

    public required PlayerBoardState Player1 { get; init; }

    public required PlayerBoardState Player2 { get; init; }
}
