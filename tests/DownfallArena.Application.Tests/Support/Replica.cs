using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Queries;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// One process playing matches from a journal: its own memory of them, over a journal it may share with
/// another. Two of these over one <see cref="JournalStore" /> are two replicas of a host.
/// </summary>
internal sealed class Replica
{
    public Replica(JournalStore journal, params IMatchMemory[] memories)
        : this(journal, [], memories)
    {
    }

    /// <param name="inStep">What is told, in the match's turn, that the match stands where its journal ends.</param>
    public Replica(JournalStore journal, IMatchInStep inStep)
        : this(journal, [], [], inStep)
    {
    }

    /// <param name="witnesses">What sees every decision this process's stepper takes, a rebuilt match's included.</param>
    /// <param name="inStep">What is told, in the match's turn, that the match stands where its journal ends.</param>
    public Replica(JournalStore journal, IReadOnlyList<IDecisionWitness> witnesses, IMatchInStep inStep)
        : this(journal, witnesses, [], inStep)
    {
    }

    /// <param name="witnesses">What sees every decision this process's stepper takes, a rebuilt match's included.</param>
    public Replica(JournalStore journal, IReadOnlyList<IDecisionWitness> witnesses, params IMatchMemory[] memories)
        : this(journal, witnesses, memories, inStep: null)
    {
    }

    private Replica(JournalStore journal, IReadOnlyList<IDecisionWitness> witnesses, IMatchMemory[] memories, IMatchInStep? inStep)
    {
        Store = new MatchStore();
        Events = EventLog.Of(Store);
        Stepper = new MatchStepper(
            new MatchCommandHandlers(
                new SubmitEvolutionChoiceHandler(Store.Workflow),
                new PassEvolutionHandler(Store.Workflow),
                new SubmitSpeedChoiceHandler(Store.Workflow),
                new SubmitTieOrderHandler(Store.Workflow),
                new SubmitIntentHandler(Store.Workflow),
                new SubmitActionHandler(Store.Workflow)),
            new MatchQueryHandlers(
                new GetBoardStateForPlayerHandler(Store.Workflow),
                new GetPlayerOptionsHandler(Store.Workflow, TestContent.Resources)),
            witnesses);
        var concede = new ConcedeHandler(Store.Workflow);
        var timeOut = new TimeOutHandler(Store.Workflow);
        Replay = new MatchReplay(
            new CreateMatchHandler(Store.Workflow, TestContent.Resources, new TestRandomFactory()),
            new JoinMatchHandler(Store.Workflow),
            concede,
            Stepper,
            Store.Repository,
            memories,
            timeOut);
        Journaled = new JournaledStepper(Stepper, Replay, journal.Journal, Store.Repository, concede, inStep is null ? [] : [inStep], timeOut);
    }

    public MatchStore Store { get; }

    /// <summary>Every event raised in this process, a rebuilt match's included: rebuilding raises them again.</summary>
    public EventLog Events { get; }

    public MatchStepper Stepper { get; }

    public MatchReplay Replay { get; }

    public JournaledStepper Journaled { get; }
}
