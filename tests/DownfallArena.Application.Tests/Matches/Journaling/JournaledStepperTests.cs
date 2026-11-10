using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using NSubstitute;

namespace DownfallArena.Application.Tests.Matches.Journaling;

/// <summary>
/// A match stepped over its journal (ADR 0159): the journal is the match, what a process holds of it is a
/// cache, and two processes cannot both write the same line.
/// </summary>
public sealed class JournaledStepperTests
{
    public static TheoryData<uint, CombatDeclaration> Matches
    {
        get
        {
            var matches = new TheoryData<uint, CombatDeclaration>();
            foreach (var seed in new uint[] { 1, 2, 3 })
            {
                matches.Add(seed, CombatDeclaration.HiddenIntent);
                matches.Add(seed, CombatDeclaration.AtActivation);
            }

            return matches;
        }
    }

    [Theory]
    [MemberData(nameof(Matches))]
    public async Task A_match_stepped_over_its_journal_is_the_driver_s_and_the_journal_holds_every_decision(uint seed, CombatDeclaration declaration)
    {
        var driven = await DrivenAsync(seed, declaration);
        var table = Table(seed, declaration);
        var journal = new JournalStore();
        var replica = new Replica(journal);

        var step = await replica.Journaled.AdvanceAsync(table, new MatchSeats(Agent(seed, PlayerSlot.Player1), Agent(seed, PlayerSlot.Player2)), TestContext.Current.CancellationToken);

        step.IsSuccess.ShouldBeTrue();
        step.Value.Step.Outcome.ShouldBe(driven.Outcome);
        replica.Events.Trace().ShouldBe(driven.Events);
        journal.Decisions.ShouldBe(step.Value.Step.Decided);
    }

    /// <summary>
    /// Two replicas take turns at one table, each step on the one that did not take the step before: neither
    /// ever holds the match as the journal has it, so every step is a rebuild. The match they play between them
    /// is the driver's, and a third process that never saw it reads it whole off the journal.
    /// </summary>
    [Theory]
    [MemberData(nameof(Matches))]
    public async Task Processes_that_share_only_the_journal_play_one_match_between_them(uint seed, CombatDeclaration declaration)
    {
        var driven = await DrivenAsync(seed, declaration);
        var table = Table(seed, declaration);
        var journal = new JournalStore();
        var replicas = new[] { new Replica(journal), new Replica(journal) };
        var person = Agent(seed, PlayerSlot.Player1);
        var seats = new MatchSeats(null, Agent(seed, PlayerSlot.Player2));

        var step = (await replicas[0].Journaled.AdvanceAsync(table, seats, TestContext.Current.CancellationToken)).Value;
        for (var taken = 1; !step.Step.IsOver; taken++)
        {
            taken.ShouldBeLessThan(5000);
            var replica = replicas[taken % 2];
            var view = (await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken)).Value.Player1.View;
            var next = await replica.Journaled.SubmitAsync(table, seats, new SeatDecision(PlayerSlot.Player1, Answer(person, view)), cancellationToken: TestContext.Current.CancellationToken);
            next.IsSuccess.ShouldBeTrue(next.IsFailure ? next.Error.Code : string.Empty);
            step = next.Value;
        }

        step.Step.Outcome.ShouldBe(driven.Outcome);
        var late = new Replica(journal);
        var read = await late.Journaled.AdvanceAsync(table, MatchSeats.People, TestContext.Current.CancellationToken);
        read.Value.Step.Outcome.ShouldBe(driven.Outcome);
        read.Value.Step.Decided.ShouldBeEmpty();
        late.Events.Trace().ShouldBe(driven.Events);
    }

    [Fact]
    public async Task A_writer_that_lost_the_race_is_told_the_match_moved_and_writes_nothing()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        var theirs = new SeatDecision(PlayerSlot.Player2, PlayerDecision.Pass);
        var mine = new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass);
        journal.BeforeAppend = () => journal.WriteElsewhere(theirs);

        var lost = await replica.Journaled.SubmitAsync(table, MatchSeats.People, mine, cancellationToken: TestContext.Current.CancellationToken);

        lost.Error.ShouldBe(JournalErrors.Moved);
        journal.Decisions.ShouldBe([theirs]);
        var view = (await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken)).Value.Player1.View;
        view.Asked.ShouldBe(new SeatQuestion(PlayerOptionsKind.Evolution, null));

        var again = await replica.Journaled.SubmitAsync(table, MatchSeats.People, mine, cancellationToken: TestContext.Current.CancellationToken);

        again.IsSuccess.ShouldBeTrue();
        again.Value.Step.Held.IsEmpty.ShouldBeTrue();
        journal.Decisions.ShouldBe([theirs, mine]);
        replica.Events.All.OfType<EvolutionPassed>().Select(pass => pass.Slot).ShouldBe([PlayerSlot.Player1, PlayerSlot.Player2]);
    }

    [Fact]
    public async Task A_late_decision_is_refused_without_a_line_and_without_a_rebuild()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        var creature = (await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken)).Value.Player1.View.Board.Allies[0].Id;
        var early = new SeatDecision(PlayerSlot.Player1, PlayerDecision.ChooseSpeed(creature, Speed.Quick));

        var refused = await replica.Journaled.SubmitAsync(table, MatchSeats.People, early, cancellationToken: TestContext.Current.CancellationToken);
        await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);

        refused.Error.ShouldBe(DecisionErrors.NotPending);
        journal.Lines.ShouldBeEmpty();
        await replica.Store.Repository.Received(1).ForgetAsync(table.MatchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_journal_the_match_does_not_follow_is_refused_and_left_as_it_is()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        IReadOnlyList<SeatDecision> written = [new(PlayerSlot.Player1, PlayerDecision.Pass), new(PlayerSlot.Player1, PlayerDecision.Pass)];
        foreach (var line in written)
        {
            journal.WriteElsewhere(line);
        }

        var replica = new Replica(journal);

        var step = await replica.Journaled.AdvanceAsync(table, MatchSeats.People, TestContext.Current.CancellationToken);
        var view = await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);

        step.Error.Code.ShouldBe(JournalErrors.DivergedCode);
        view.Error.Code.ShouldBe(JournalErrors.DivergedCode);
        journal.Decisions.ShouldBe(written);
    }

    [Fact]
    public async Task Two_decisions_arriving_at_once_take_turns_and_both_are_written()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);

        var steps = await Task.WhenAll(
            Task.Run(() => replica.Journaled.SubmitAsync(table, MatchSeats.People, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), cancellationToken: TestContext.Current.CancellationToken), TestContext.Current.CancellationToken),
            Task.Run(() => replica.Journaled.SubmitAsync(table, MatchSeats.People, new SeatDecision(PlayerSlot.Player2, PlayerDecision.Pass), cancellationToken: TestContext.Current.CancellationToken), TestContext.Current.CancellationToken));

        steps.ShouldAllBe(step => step.IsSuccess);
        journal.Lines.Count.ShouldBe(2);
        journal.Decisions.Select(line => line.Slot).ShouldBe([PlayerSlot.Player1, PlayerSlot.Player2], ignoreOrder: true);
        steps.ShouldContain(step => step.Value.Step.Held.IsEmpty);
    }

    [Fact]
    public async Task A_match_let_go_of_keeps_its_journal_and_reads_the_same_when_asked_for_again()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        await replica.Journaled.SubmitAsync(table, MatchSeats.People, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), cancellationToken: TestContext.Current.CancellationToken);
        var before = JsonSerializer.Serialize((await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken)).Value.Player1.View.Board);

        await replica.Journaled.ForgetAsync(table.MatchId, TestContext.Current.CancellationToken);
        var after = (await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken)).Value.Player1.View;

        journal.Lines.Count.ShouldBe(1);
        JsonSerializer.Serialize(after.Board).ShouldBe(before);
        after.Board.HasPassedEvolution.ShouldBeTrue();
        await replica.Store.Repository.Received(3).ForgetAsync(table.MatchId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A step asked for while a match is being let go of waits behind that, and must then take its turn with
    /// the steps asked for after: let loose on its own it would share the match with them. What shows it here
    /// is that the step after it finds the match where the one that waited left it, and rebuilds nothing.
    /// </summary>
    [Fact]
    public async Task A_step_that_waited_behind_a_match_being_let_go_of_takes_its_turn_with_the_steps_after_it()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        journal.BeforeRead = async () =>
        {
            reading.SetResult();
            await release.Task;
        };
        var first = replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);
        await reading.Task;
        var letGo = replica.Journaled.ForgetAsync(table.MatchId, TestContext.Current.CancellationToken);
        var waited = replica.Journaled.SubmitAsync(table, MatchSeats.People, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), cancellationToken: TestContext.Current.CancellationToken);

        release.SetResult();
        await first;
        await letGo;
        (await waited).IsSuccess.ShouldBeTrue();
        var saved = replica.Store.Calls.Count;
        var next = await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);

        next.Value.Player1.View.Board.HasPassedEvolution.ShouldBeTrue();
        replica.Store.Calls.Count.ShouldBe(saved);
    }

    /// <summary>
    /// Two picks of one opportunity are asked with the same words. Each is an asking of its own, so an answer
    /// sent to the first cannot be taken for the second, and the other seat's answers do not move it.
    /// </summary>
    [Fact]
    public async Task Two_questions_of_one_shape_are_two_askings_and_an_answer_to_the_first_is_late_for_the_second()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        var opening = (await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken)).Value;
        var offered = opening.Player1.View.Options.Evolution.ShouldNotBeNull();
        var pick = new SeatDecision(PlayerSlot.Player1, PlayerDecision.Buy(offered.Creatures[0].Creature, offered.Creatures[0].AvailableTiers[0]));
        var pass = new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass);

        var picked = await replica.Journaled.SubmitAsync(table, MatchSeats.People, pick, opening.Player1.Asking, TestContext.Current.CancellationToken);
        var late = await replica.Journaled.SubmitAsync(table, MatchSeats.People, pass, opening.Player1.Asking, TestContext.Current.CancellationToken);
        var passed = await replica.Journaled.SubmitAsync(table, MatchSeats.People, pass, picked.Value.State.Player1.Asking, TestContext.Current.CancellationToken);

        opening.Player1.Asking.ShouldBe(1);
        picked.Value.State.Player1.Asking.ShouldBe(2);
        picked.Value.State.Player1.View.Asked.ShouldBe(opening.Player1.View.Asked);
        picked.Value.State.Player2.Asking.ShouldBe(1);
        late.Error.ShouldBe(DecisionErrors.NotPending);
        passed.IsSuccess.ShouldBeTrue();
        passed.Value.State.Player1.Asking.ShouldBe(3);
        passed.Value.State.Player2.Asking.ShouldBe(1);
        journal.Decisions.ShouldBe([pick, pass]);
        await replica.Store.Repository.Received(1).ForgetAsync(table.MatchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_step_answers_with_what_both_seats_read_once_it_is_taken()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var replica = new Replica(new JournalStore());

        var step = await replica.Journaled.SubmitAsync(table, MatchSeats.People, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), cancellationToken: TestContext.Current.CancellationToken);
        var read = await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);

        step.Value.State.Player1.View.Asked.ShouldBeNull();
        step.Value.State.Player1.View.Board.HasPassedEvolution.ShouldBeTrue();
        step.Value.State.Player2.View.Asked.ShouldBe(new SeatQuestion(PlayerOptionsKind.Evolution, null));
        step.Value.State.Outcome.ShouldBeNull();
        Normalized.Json(read.Value).ShouldBe(Normalized.Json(step.Value.State));
    }

    /// <summary>
    /// A concession ends the match and is a line of the journal, written in the same turn: a process that picks
    /// the match up later finds it conceded, where a match rebuilt from its decisions alone would be going on.
    /// </summary>
    [Fact]
    public async Task A_concession_is_a_line_of_the_journal_and_a_match_rebuilt_from_it_is_conceded()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        var pass = new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass);
        await replica.Journaled.SubmitAsync(table, MatchSeats.People, pass, cancellationToken: TestContext.Current.CancellationToken);

        var conceded = await replica.Journaled.ConcedeAsync(table, PlayerSlot.Player2, TestContext.Current.CancellationToken);
        var again = await replica.Journaled.ConcedeAsync(table, PlayerSlot.Player1, TestContext.Current.CancellationToken);
        var late = await replica.Journaled.SubmitAsync(table, MatchSeats.People, new SeatDecision(PlayerSlot.Player2, PlayerDecision.Pass), cancellationToken: TestContext.Current.CancellationToken);

        conceded.Value.Outcome.ShouldNotBeNull().Winner.ShouldBe(PlayerSlot.Player1);
        conceded.Value.Player1.View.Asked.ShouldBeNull();
        conceded.Value.Player1.View.Board.HasPassedEvolution.ShouldBeFalse("the answer held when the match ended is nobody's any more");
        again.Error.ShouldBe(MatchErrors.NotInProgress);
        late.Error.ShouldBe(DecisionErrors.NotPending);
        journal.Lines.ShouldBe([new DecisionEntry(pass), new ConcessionEntry(PlayerSlot.Player2)]);
        var elsewhere = await new Replica(journal).Journaled.ReadAsync(table, TestContext.Current.CancellationToken);
        elsewhere.Value.Outcome.ShouldBe(conceded.Value.Outcome);
    }

    /// <summary>
    /// The command that concedes changes the match in memory before it is saved and its events are handed on.
    /// When that fails, the match in memory is conceded and the journal does not say so: it is let go of, so
    /// the next reader finds the match the journal describes, still being played.
    /// </summary>
    [Fact]
    public async Task A_concession_that_fails_once_the_match_has_taken_it_is_not_kept_in_memory()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);
        replica.Store.Dispatcher.DispatchAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new IOException("The listener is down.")));

        await Should.ThrowAsync<IOException>(() => replica.Journaled.ConcedeAsync(table, PlayerSlot.Player2, TestContext.Current.CancellationToken));
        replica.Store.Dispatcher.DispatchAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var read = await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);

        journal.Lines.ShouldBeEmpty();
        read.Value.Outcome.ShouldBeNull();
        read.Value.Player1.View.Asked.ShouldBe(new SeatQuestion(PlayerOptionsKind.Evolution, null));
    }

    [Fact]
    public async Task A_seat_told_to_change_hands_is_a_line_the_next_reader_finds_and_the_match_is_as_it_was()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        var before = (await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken)).Value;
        var change = new SeatChangeEntry(PlayerSlot.Player2, "greedy", 2);

        var changed = await replica.Journaled.ChangeSeatAsync(table, change, TestContext.Current.CancellationToken);

        changed.Value.SeatChanges.ShouldBe([change]);
        changed.Value.Position.Lines.ShouldBe(1);
        Normalized.Json(changed.Value.Player1).ShouldBe(Normalized.Json(before.Player1));
        Normalized.Json(changed.Value.Player2).ShouldBe(Normalized.Json(before.Player2));
        journal.Lines.ShouldBe([change]);
        var elsewhere = (await new Replica(journal).Journaled.ReadAsync(table, TestContext.Current.CancellationToken)).Value;
        elsewhere.SeatChanges.ShouldBe([change]);
        elsewhere.Player2.Asking.ShouldBe(1);
        (await replica.Journaled.SubmitAsync(table, MatchSeats.People, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), cancellationToken: TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task A_seat_cannot_change_hands_from_a_round_the_match_has_reached_nor_once_it_is_over()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        var round = (await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken)).Value.Player1.View.Board.RoundNumber.ShouldNotBeNull();

        var reached = await replica.Journaled.ChangeSeatAsync(table, new SeatChangeEntry(PlayerSlot.Player1, "greedy", round), TestContext.Current.CancellationToken);
        await replica.Journaled.ConcedeAsync(table, PlayerSlot.Player1, TestContext.Current.CancellationToken);
        var over = await replica.Journaled.ChangeSeatAsync(table, new SeatChangeEntry(PlayerSlot.Player1, "greedy", round + 1), TestContext.Current.CancellationToken);

        reached.Error.ShouldBe(JournalErrors.RoundReached);
        over.Error.ShouldBe(MatchErrors.NotInProgress);
        journal.Lines.ShouldBe([new ConcessionEntry(PlayerSlot.Player1)]);
    }

    /// <summary>
    /// A match rebuilt from its journal is seen by a witness as it was seen being played: every decision, the
    /// agents' and the submitted ones, with the board and the options it was taken on. It is what lets a
    /// recording be rebuilt with the match rather than kept beside it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Matches))]
    public async Task A_match_being_rebuilt_is_seen_as_it_was_seen_being_played(uint seed, CombatDeclaration declaration)
    {
        var table = Table(seed, declaration);
        var journal = new JournalStore();
        var played = new Noting();
        var replica = new Replica(journal, [played]);
        var person = Agent(seed, PlayerSlot.Player1);
        var seats = new MatchSeats(null, Agent(seed, PlayerSlot.Player2));
        var step = (await replica.Journaled.AdvanceAsync(table, seats, TestContext.Current.CancellationToken)).Value;
        for (var taken = 1; !step.Step.IsOver; taken++)
        {
            taken.ShouldBeLessThan(5000);
            var answer = new SeatDecision(PlayerSlot.Player1, Answer(person, step.State.Player1.View));
            step = (await replica.Journaled.SubmitAsync(table, seats, answer, step.State.Player1.Asking, TestContext.Current.CancellationToken)).Value;
        }

        var rebuilt = new Noting();

        var read = await new Replica(journal, [rebuilt]).Journaled.ReadAsync(table, TestContext.Current.CancellationToken);

        read.Value.Outcome.ShouldBe(step.Step.Outcome);
        rebuilt.Of(PlayerSlot.Player1).ShouldBe(played.Of(PlayerSlot.Player1));
        rebuilt.Of(PlayerSlot.Player2).ShouldBe(played.Of(PlayerSlot.Player2));
        played.Of(PlayerSlot.Player1).ShouldNotBeEmpty();
    }

    private static JournaledMatch Table(uint seed, CombatDeclaration declaration)
    {
        var rules = MatchStore.TwoOnTwo().WithCombatDeclaration(declaration);
        return new JournaledMatch("run-1", MatchId.New(), rules, (int)seed, MatchStore.Roster(rules));
    }

    private static RandomAgent Agent(uint seed, PlayerSlot slot) => new(new TestRandom(seed + (slot == PlayerSlot.Player1 ? 1u : 2u)));

    /// <summary>The match the driver plays at a table from the same seed, created the way a journaled match is.</summary>
    private static async Task<(MatchOutcome? Outcome, IReadOnlyList<string> Events)> DrivenAsync(uint seed, CombatDeclaration declaration)
    {
        var table = Table(seed, declaration);
        var store = new MatchStore();
        var log = EventLog.Of(store);
        await new CreateMatchHandler(store.Workflow, TestContent.Resources, new TestRandomFactory()).HandleAsync(new CreateMatch(table.Rules, table.Seed, table.MatchId), TestContext.Current.CancellationToken);
        var join = new JoinMatchHandler(store.Workflow);
        await join.HandleAsync(new JoinMatch(table.MatchId, PlayerId.New(), table.Roster), TestContext.Current.CancellationToken);
        await join.HandleAsync(new JoinMatch(table.MatchId, PlayerId.New(), table.Roster), TestContext.Current.CancellationToken);
        var driver = Handlers.Driver(store.Workflow, together: true);

        var outcome = await driver.PlayAsync(table.MatchId, Agent(seed, PlayerSlot.Player1), Agent(seed, PlayerSlot.Player2), TestContext.Current.CancellationToken);

        outcome.IsSuccess.ShouldBeTrue();
        return (outcome.Value, log.Trace());
    }

    /// <summary>What an agent answers to the question a seat's view asks, on the board and the options the view shows.</summary>
    private static PlayerDecision Answer(RandomAgent agent, SeatView view)
    {
        var asked = view.Asked.ShouldNotBeNull();
        switch (asked.Kind)
        {
            case PlayerOptionsKind.Evolution:
                return agent.DecideEvolution(view.Board, view.Options.Evolution!).Choice is { } choice ? PlayerDecision.Buy(choice.Creature, choice.Tier) : PlayerDecision.Pass;
            case PlayerOptionsKind.Speed:
                return PlayerDecision.ChooseSpeed(asked.Creature!.Value, agent.DecideSpeed(view.Board, asked.Creature.Value));
            case PlayerOptionsKind.TieOrder:
                return PlayerDecision.OrderTies(agent.DecideTieOrder(view.Board, view.Options.TieOrder!));
            case PlayerOptionsKind.Intent:
                var option = view.Options.Intent!.Creatures.Single(offered => offered.Creature == asked.Creature);
                return PlayerDecision.DeclareIntent(option.Creature, agent.DecideIntent(view.Board, option));
            case PlayerOptionsKind.Target:
                return PlayerDecision.BindTargets(agent.DecideTargets(view.Board, view.Options.Target!));
            case PlayerOptionsKind.Action:
                var action = view.Options.Action!;
                var chosen = ((IPlayerAgent)agent).DecideAction(view.Board, action);
                return PlayerDecision.Act(action.Actor, chosen.Spell, chosen.Targets);
            default:
                throw new InvalidOperationException($"A seat is not asked {asked.Kind}.");
        }
    }
}
