using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A table a test builds by hand the way the composer builds every ordinary one (ADR 0159): its match stepped
/// over a journal, nothing waiting on a person. The tests of the routes' own behaviour are written against it,
/// since it is what the routes serve; <see cref="Driven" /> is the blocking model practice tables keep.
/// </summary>
internal static class Stepped
{
    /// <summary>
    /// Opens a table's match over its journal, with nobody asked anything yet: the test begins it, or a request
    /// carrying a person's token does.
    /// </summary>
    /// <param name="journal">The journal the table is stepped over; one in memory when none is given, which goes with the table.</param>
    /// <param name="run">The recording, whose id is the name the journal is kept under; none for a table that keeps nothing.</param>
    public static async Task<SteppedSession> OpenAsync(
        IServiceProvider services,
        RuleSet rules,
        int seed,
        SteppedSeats seats,
        CancellationToken stopping,
        IDecisionJournal? journal = null,
        PlaytestRun? run = null)
    {
        var resources = services.GetRequiredService<IGameResources>();
        List<CreatureDefinitionId> roster = [.. Enumerable.Repeat(resources.Creatures.First().Id, rules.TeamSize)];
        var opened = await SteppedSession.OpenAsync(
            SteppedHost.Of(services),
            journal ?? new InMemoryDecisionJournal(),
            new JournaledMatch(run?.SessionId ?? "table-1", MatchId.New(), rules, seed, roster),
            seats,
            run,
            stopping: stopping);
        opened.IsSuccess.ShouldBeTrue(opened.IsFailure ? opened.Error.Message : string.Empty);

        // The composer's first checkpoint: the trace file exists before anybody has tapped anything.
        if (run is not null)
        {
            await run.CheckpointAsync(opened.Value.MatchId, stopping);
        }

        return opened.Value;
    }
}
