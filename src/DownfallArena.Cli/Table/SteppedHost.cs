using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What a host hands every table stepped over its journal (ADR 0159): the engine's commands and reads, the
/// store the matches in memory are kept in, and the recorder of their feeds. A table makes its own stepper of
/// them, so what is kept of a match between two steps goes when the table does.
/// </summary>
internal sealed record SteppedHost(
    MatchCommandHandlers Commands,
    MatchQueryHandlers Queries,
    ICommandHandler<CreateMatch, Result<MatchId>> Create,
    ICommandHandler<JoinMatch, Result<PlayerSlot>> Join,
    ICommandHandler<Concede, Result> Concede,
    IMatchRepository Matches,
    MatchTraceRecorder Events)
{
    public static SteppedHost Of(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new SteppedHost(
            services.GetRequiredService<MatchCommandHandlers>(),
            services.GetRequiredService<MatchQueryHandlers>(),
            services.GetRequiredService<ICommandHandler<CreateMatch, Result<MatchId>>>(),
            services.GetRequiredService<ICommandHandler<JoinMatch, Result<PlayerSlot>>>(),
            services.GetRequiredService<ICommandHandler<Concede, Result>>(),
            services.GetRequiredService<IMatchRepository>(),
            services.GetRequiredService<MatchTraceRecorder>());
    }

    /// <summary>A table's own stepper over a journal.</summary>
    /// <param name="witness">What is shown every decision of the table's match, the replayed ones among them.</param>
    /// <param name="memory">What is told to forget the match when it is about to be rebuilt.</param>
    public JournaledStepper Stepper(IDecisionJournal journal, IDecisionWitness witness, IMatchMemory memory)
    {
        var stepper = new MatchStepper(Commands, Queries, [witness]);
        return new JournaledStepper(stepper, new MatchReplay(Create, Join, Concede, stepper, Matches, [memory]), journal, Matches, Concede);
    }
}
