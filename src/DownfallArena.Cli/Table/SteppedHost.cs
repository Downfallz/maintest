using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Matches;
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
    MatchTraceRecorder Events,
    TimeProvider Clock)
{
    /// <summary>
    /// Where the turns a table's bots owe are taken (ADR 0159, phase 5): in this process unless a host says
    /// otherwise.
    /// </summary>
    public IAgentTurns Turns { get; init; } = new InProcessAgentTurns(Clock);

    /// <summary>What times a match out (ADR 0169); a host made without it cannot, nor replay a journal that did.</summary>
    public ICommandHandler<TimeOut, Result>? TimeOut { get; init; }

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
            services.GetRequiredService<MatchTraceRecorder>(),
            services.GetRequiredService<TimeProvider>())
        {
            TimeOut = services.GetService<ICommandHandler<TimeOut, Result>>(),
        };
    }

    /// <summary>A stepper over a journal that nothing watches: what sets a match up before a table plays it.</summary>
    public JournaledStepper Stepper(IDecisionJournal journal)
    {
        var stepper = new MatchStepper(Commands, Queries);
        return new JournaledStepper(stepper, new MatchReplay(Create, Join, Concede, stepper, Matches, [], TimeOut), journal, Matches, Concede, timeOut: TimeOut);
    }

    /// <summary>A table's own stepper over a journal.</summary>
    /// <param name="witness">What is shown every decision of the table's match, the replayed ones among them.</param>
    /// <param name="memory">What is told to forget the match when it is about to be rebuilt.</param>
    /// <param name="inStep">What is told, in the match's turn, that the match stands where its journal ends.</param>
    public JournaledStepper Stepper(IDecisionJournal journal, IDecisionWitness witness, IMatchMemory memory, IMatchInStep inStep)
    {
        var stepper = new MatchStepper(Commands, Queries, [witness]);
        return new JournaledStepper(stepper, new MatchReplay(Create, Join, Concede, stepper, Matches, [memory], TimeOut), journal, Matches, Concede, [inStep], TimeOut);
    }
}
