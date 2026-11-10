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
                new GetPlayerOptionsHandler(Store.Workflow, TestContent.Resources)));
        Replay = new MatchReplay(
            new CreateMatchHandler(Store.Workflow, TestContent.Resources, new TestRandomFactory()),
            new JoinMatchHandler(Store.Workflow),
            Stepper,
            Store.Repository,
            memories);
        Journaled = new JournaledStepper(Stepper, Replay, journal.Journal, Store.Repository);
    }

    public MatchStore Store { get; }

    /// <summary>Every event raised in this process, a rebuilt match's included: rebuilding raises them again.</summary>
    public EventLog Events { get; }

    public MatchStepper Stepper { get; }

    public MatchReplay Replay { get; }

    public JournaledStepper Journaled { get; }
}
