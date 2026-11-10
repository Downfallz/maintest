using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The table's API over a session a test started itself and a driver plays: the blocking model, which practice
/// tables keep. The tests of the routes' own behaviour are written against <see cref="Stepped" />; what is left
/// here is what only that model does, the reads it makes again while the match moves under a poll.
/// </summary>
internal static class Driven
{
    /// <param name="reads">The read side the routes ask, which a test may slow down or move under them.</param>
    public static TableApi Api(
        TableSession session,
        MatchQueryHandlers reads,
        IReadOnlyList<TableSeat> seats,
        CatalogueView catalogue,
        MatchTraceRecorder events,
        PlaytestRun? run = null,
        TablePilot? pilot = null) =>
        new(new DrivenSession(session, seats, run, reads), seats, catalogue, events, run, pilot);
}
