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
using DownfallArena.SharedKernel.Primitives;
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
    /// A match of agents taken one of their turns at a time is the match taken in one step: the same events, the
    /// same journal. Between two of those turns the match is anybody's, which is what lets a seat be told to
    /// change hands while agents play it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Matches))]
    public async Task A_match_of_agents_taken_a_turn_at_a_time_is_the_match_taken_in_one_step(uint seed, CombatDeclaration declaration)
    {
        var driven = await DrivenAsync(seed, declaration);
        var table = Table(seed, declaration);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        var seats = new MatchSeats(Agent(seed, PlayerSlot.Player1), Agent(seed, PlayerSlot.Player2));
        var turns = 0;

        var step = (await replica.Journaled.AdvanceAsync(table, seats, once: true, TestContext.Current.CancellationToken)).Value;
        while (!step.Step.IsOver)
        {
            (++turns).ShouldBeLessThan(5000);
            step.Step.Owed.ShouldBeTrue("two agents never leave a person asked");
            step = (await replica.Journaled.AdvanceAsync(table, seats, once: true, TestContext.Current.CancellationToken)).Value;
        }

        turns.ShouldBeGreaterThan(10);
        step.Step.Outcome.ShouldBe(driven.Outcome);
        replica.Events.Trace().ShouldBe(driven.Events);
    }

    /// <summary>
    /// A turn at a time with a person in a seat: the step stops owing nothing as soon as the person is the one
    /// asked, and says so rather than taking the agent's next turn.
    /// </summary>
    [Fact]
    public async Task A_turn_at_a_time_stops_at_the_person_as_one_step_does()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var replica = new Replica(new JournalStore());
        var seats = new MatchSeats(null, Agent(3, PlayerSlot.Player2));

        var first = (await replica.Journaled.AdvanceAsync(table, seats, once: true, TestContext.Current.CancellationToken)).Value;
        var second = (await replica.Journaled.AdvanceAsync(table, seats, once: true, TestContext.Current.CancellationToken)).Value;

        first.Step.Asked.ShouldBe([PlayerSlot.Player1]);
        first.Step.Owed.ShouldBeFalse();
        first.Step.Decided.ShouldNotBeEmpty();
        second.Step.Asked.ShouldBe([PlayerSlot.Player1]);
        second.Step.Decided.ShouldBeEmpty();
    }

    /// <summary>
    /// Two replicas take turns at one table, each step on the one that did not take the step before: neither
    /// ever holds the match as the journal has it, so each takes the other's line before its own, and reads the
    /// journal whole only the first time (inventory question 13). The match they play between them is the
    /// driver's, and a third process that never saw it reads it whole off the journal.
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
        await journal.Journal.Received(2).ReadAsync(table.Run, Arg.Any<CancellationToken>());
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

    /// <summary>
    /// A process that stood where the journal ended takes the lines another wrote since, and only those: the
    /// match it holds then is the one a process that never saw it rebuilds from the whole journal, events and all.
    /// </summary>
    [Fact]
    public async Task Lines_another_process_wrote_are_taken_on_the_match_in_memory_without_building_it_again()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var mine = new Replica(journal);
        var theirs = new Replica(journal);
        await mine.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);
        await theirs.Journaled.SubmitAsync(table, MatchSeats.People, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), cancellationToken: TestContext.Current.CancellationToken);
        await theirs.Journaled.SubmitAsync(table, MatchSeats.People, new SeatDecision(PlayerSlot.Player2, PlayerDecision.Pass), cancellationToken: TestContext.Current.CancellationToken);

        var caught = (await mine.Journaled.ReadAsync(table, TestContext.Current.CancellationToken)).Value;

        var whole = new Replica(journal);
        var rebuilt = (await whole.Journaled.ReadAsync(table, TestContext.Current.CancellationToken)).Value;
        await mine.Store.Repository.Received(1).ForgetAsync(table.MatchId, Arg.Any<CancellationToken>());
        caught.Position.ShouldBe(rebuilt.Position);
        caught.Player1.Asking.ShouldBe(rebuilt.Player1.Asking);
        JsonSerializer.Serialize(caught.Player1.View).ShouldBe(JsonSerializer.Serialize(rebuilt.Player1.View));
        JsonSerializer.Serialize(caught.Player2.View).ShouldBe(JsonSerializer.Serialize(rebuilt.Player2.View));
        mine.Events.Trace().ShouldBe(whole.Events.Trace());
    }

    /// <summary>
    /// Lines another process wrote that the match in memory does not take leave it part of the way: it is
    /// rebuilt, and refused as a rebuild refuses the journal, naming the journal's own line.
    /// </summary>
    [Fact]
    public async Task Lines_another_process_wrote_that_the_match_does_not_take_are_refused_as_a_rebuild_refuses_them()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        await replica.Journaled.SubmitAsync(table, MatchSeats.People, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), cancellationToken: TestContext.Current.CancellationToken);
        journal.WriteElsewhere(new SeatDecision(PlayerSlot.Player2, PlayerDecision.Pass));
        journal.WriteElsewhere(new SeatDecision(PlayerSlot.Player2, PlayerDecision.Pass));

        var read = await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);
        var again = await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);

        read.Error.Code.ShouldBe(JournalErrors.DivergedCode);
        read.Error.Message.ShouldContain("Line 3");
        again.Error.ShouldBe(read.Error);
        journal.Lines.Count.ShouldBe(3);
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
    /// <summary>
    /// A timeout (ADR 0169) is a line of the journal, settled against the match as its turn finds it: the seat the
    /// caller names as late loses, and a match rebuilt from the journal ends the same way.
    /// </summary>
    [Fact]
    public async Task A_timeout_is_a_line_of_the_journal_and_a_match_rebuilt_from_it_ends_the_same()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        await replica.Journaled.SubmitAsync(table, MatchSeats.People, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), cancellationToken: TestContext.Current.CancellationToken);

        var since = (await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken)).Value.Position.Mark;

        var ended = await replica.Journaled.TimeOutAsync(table, since, state => state.Player2.View.Asked is null ? [] : [PlayerSlot.Player2], TestContext.Current.CancellationToken);

        ended.Value.Outcome.ShouldBe(new MatchOutcome(PlayerSlot.Player1, MatchEndReason.TimedOut));
        journal.Lines[^1].ShouldBe(new TimeoutEntry([PlayerSlot.Player2]));
        var rebuilt = await new Replica(journal).Journaled.ReadAsync(table, TestContext.Current.CancellationToken);
        rebuilt.Value.Outcome.ShouldBe(new MatchOutcome(PlayerSlot.Player1, MatchEndReason.TimedOut));
    }

    [Fact]
    public async Task Nobody_late_ends_nothing_and_writes_nothing()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);

        var ended = await replica.Journaled.TimeOutAsync(table, 0, _ => [], TestContext.Current.CancellationToken);

        ended.Error.ShouldBe(MatchErrors.NobodyTimedOut);
        journal.Lines.ShouldBeEmpty();
        (await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken)).Value.Outcome.ShouldBeNull();
    }

    /// <summary>
    /// The time is read before the timeout has its turn. A move made in between moved the journal's end, so whoever
    /// is asked now was asked just now: the timeout is refused, and nothing ends or is written.
    /// </summary>
    [Fact]
    public async Task A_timeout_seen_before_a_move_that_landed_since_ends_nothing()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        var since = (await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken)).Value.Position.Mark;
        await new Replica(journal).Journaled.SubmitAsync(table, MatchSeats.People, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), cancellationToken: TestContext.Current.CancellationToken);
        var lines = journal.Lines.Count;

        var ended = await replica.Journaled.TimeOutAsync(table, since, state => state.Player2.View.Asked is null ? [] : [PlayerSlot.Player2], TestContext.Current.CancellationToken);

        ended.Error.ShouldBe(JournalErrors.Moved);
        journal.Lines.Count.ShouldBe(lines);
        (await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken)).Value.Outcome.ShouldBeNull();
    }

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

    /// <summary>
    /// A round the match has left cannot be handed over; the one it is in can, to whoever knows the seat has
    /// decided nothing in it yet. Here seat 1 is played by an agent through round 1 and answered from outside
    /// from round 2, so the step stops at the top of round 2 with that seat still to decide.
    /// </summary>
    [Fact]
    public async Task A_seat_cannot_change_hands_from_a_round_the_match_has_left_nor_once_it_is_over()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        var seats = new MatchSeats(Agent(3, PlayerSlot.Player1), Agent(3, PlayerSlot.Player2)).From(PlayerSlot.Player1, 2, null);
        var step = (await replica.Journaled.AdvanceAsync(table, seats, TestContext.Current.CancellationToken)).Value;
        step.State.Player1.View.Board.RoundNumber.ShouldBe(2);
        var written = journal.Lines.Count;

        var left = await replica.Journaled.ChangeSeatAsync(table, new SeatChangeEntry(PlayerSlot.Player1, "greedy", 1), TestContext.Current.CancellationToken);
        var current = await replica.Journaled.ChangeSeatAsync(table, new SeatChangeEntry(PlayerSlot.Player1, "greedy", 2), TestContext.Current.CancellationToken);
        await replica.Journaled.ConcedeAsync(table, PlayerSlot.Player1, TestContext.Current.CancellationToken);
        var over = await replica.Journaled.ChangeSeatAsync(table, new SeatChangeEntry(PlayerSlot.Player1, "greedy", 3), TestContext.Current.CancellationToken);

        left.Error.ShouldBe(JournalErrors.RoundReached);
        current.IsSuccess.ShouldBeTrue();
        over.Error.ShouldBe(MatchErrors.NotInProgress);
        journal.Lines.Skip(written).ShouldBe([new SeatChangeEntry(PlayerSlot.Player1, "greedy", 2), new ConcessionEntry(PlayerSlot.Player1)]);
    }

    /// <summary>
    /// A step asked for while another is being taken waits for its turn without holding whoever asked: a tap
    /// made while a Player agent thinks is a request waiting, not a thread stopped for as long as the agent takes.
    /// </summary>
    [Fact]
    public async Task A_step_asked_for_while_another_is_being_taken_waits_its_turn_without_holding_its_caller()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var replica = new Replica(new JournalStore());
        var thinking = new Thinking(Agent(3, PlayerSlot.Player1));
        var first = Task.Run(() => replica.Journaled.AdvanceAsync(table, new MatchSeats(thinking, null), TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        await thinking.Asked.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var waiting = replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);
        var handedBack = clock.Elapsed;
        var answered = waiting.IsCompleted;
        thinking.Release();
        await first;
        var read = await waiting;

        answered.ShouldBeFalse("the agent is still thinking, and the match is its step's");
        handedBack.ShouldBeLessThan(TimeSpan.FromSeconds(1));
        read.IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// Whether a seat may give the match up is settled in the turn that ends it: the caller is shown the match
    /// as that turn finds it, and a concession refused there ends nothing and writes nothing.
    /// </summary>
    [Fact]
    public async Task A_concession_is_settled_against_the_match_as_its_turn_finds_it_and_one_refused_there_ends_nothing()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        await replica.Journaled.SubmitAsync(table, MatchSeats.People, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), cancellationToken: TestContext.Current.CancellationToken);
        var notTheirs = new DomainError("Seat.NotTheirs", "Somebody else holds the seat.");
        JournaledState? found = null;

        var refused = await replica.Journaled.ConcedeAsync(
            table,
            PlayerSlot.Player1,
            state =>
            {
                found = state;
                return notTheirs;
            },
            TestContext.Current.CancellationToken);
        var read = await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);
        var taken = await replica.Journaled.ConcedeAsync(table, PlayerSlot.Player1, _ => null, TestContext.Current.CancellationToken);

        refused.Error.ShouldBe(notTheirs);
        found.ShouldNotBeNull().Player1.Asking.ShouldBe(2);
        read.Value.Outcome.ShouldBeNull();
        taken.Value.Outcome.ShouldBe(new MatchOutcome(PlayerSlot.Player2, MatchEndReason.Concession));
        journal.Lines.OfType<ConcessionEntry>().ShouldHaveSingleItem();
    }

    /// <summary>
    /// Every state a stepper answers with is of a match that stands where its journal ends, and whatever listens
    /// is told so in the same turn, with the same position: what the match raised by then is the journal's.
    /// </summary>
    [Fact]
    public async Task Each_state_answered_is_told_in_its_turn_to_be_where_the_journal_ends()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var told = new List<JournalPosition>();
        var inStep = Substitute.For<IMatchInStep>();
        inStep.When(listener => listener.InStep(table.MatchId, Arg.Any<JournalPosition>())).Do(call => told.Add(call.Arg<JournalPosition>()));
        var replica = new Replica(journal, inStep);

        var read = await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);
        var passed = await replica.Journaled.SubmitAsync(table, MatchSeats.People, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), cancellationToken: TestContext.Current.CancellationToken);
        var late = await replica.Journaled.SubmitAsync(table, MatchSeats.People, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), asking: 1, TestContext.Current.CancellationToken);

        late.IsFailure.ShouldBeTrue();
        told.ShouldBe([read.Value.Position, passed.Value.State.Position]);
    }

    /// <summary>
    /// Lines another process wrote are taken on only once whatever listens is told every seat change through
    /// them: a decision after a seat change among those lines is seen when its seat is already named after
    /// whoever the change seats (inventory question 19).
    /// </summary>
    [Fact]
    public async Task Seat_changes_among_the_lines_another_process_wrote_are_told_before_the_lines_are_taken_on()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var seen = new Noting();
        var inStep = Substitute.For<IMatchInStep>();
        var change = new SeatChangeEntry(PlayerSlot.Player2, "greedy", 1, Reached: 0);
        int? seenWhenTold = null;
        inStep.When(listener => listener.Replaying(table.MatchId, Arg.Is<IReadOnlyList<SeatChangeEntry>>(changes => changes.SequenceEqual(new[] { change }))))
            .Do(_ => seenWhenTold = seen.Of(PlayerSlot.Player2).Count);
        var replica = new Replica(journal, [seen], inStep);
        await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);
        journal.WriteElsewhere(change);
        journal.WriteElsewhere(new SeatDecision(PlayerSlot.Player2, PlayerDecision.Pass));

        await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);

        seenWhenTold.ShouldBe(0);
        seen.Of(PlayerSlot.Player2).Count.ShouldBe(1);
    }

    /// <summary>
    /// A match rebuilt from its journal is rebuilt only once whatever listens is told every seat change the
    /// journal holds, the ones further down than the decisions replayed first included.
    /// </summary>
    [Fact]
    public async Task Seat_changes_of_a_journal_being_rebuilt_are_told_before_any_line_is_replayed()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var seen = new Noting();
        var inStep = Substitute.For<IMatchInStep>();
        var change = new SeatChangeEntry(PlayerSlot.Player2, "greedy", 1, Reached: 0);
        int? seenWhenTold = null;
        inStep.When(listener => listener.Replaying(table.MatchId, Arg.Any<IReadOnlyList<SeatChangeEntry>>()))
            .Do(call => seenWhenTold = call.Arg<IReadOnlyList<SeatChangeEntry>>().SequenceEqual(new[] { change }) ? seen.Of(PlayerSlot.Player1).Count : null);
        journal.WriteElsewhere(new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass));
        journal.WriteElsewhere(change);
        journal.WriteElsewhere(new SeatDecision(PlayerSlot.Player2, PlayerDecision.Pass));
        var replica = new Replica(journal, [seen], inStep);

        await replica.Journaled.ReadAsync(table, TestContext.Current.CancellationToken);

        seenWhenTold.ShouldBe(0);
        seen.Of(PlayerSlot.Player1).Count.ShouldBe(1);
    }

    /// <summary>
    /// What a seat change depends on is settled in the turn that writes it: the caller is shown the match as
    /// that turn finds it and says the line, or why there is none. Nothing of the seat's can land in between.
    /// </summary>
    [Fact]
    public async Task A_seat_change_is_settled_against_the_match_as_its_turn_finds_it_and_one_refused_there_writes_nothing()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        await replica.Journaled.SubmitAsync(table, MatchSeats.People, new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass), cancellationToken: TestContext.Current.CancellationToken);
        var change = new SeatChangeEntry(PlayerSlot.Player2, "greedy", 2, Reached: 1);
        JournaledState? found = null;

        var refused = await replica.Journaled.ChangeSeatAsync(
            table,
            state =>
            {
                found = state;
                return Result.Failure<SeatChangeEntry>(JournalErrors.RoundReached);
            },
            TestContext.Current.CancellationToken);
        var taken = await replica.Journaled.ChangeSeatAsync(table, _ => Result.Success(change), TestContext.Current.CancellationToken);

        refused.Error.ShouldBe(JournalErrors.RoundReached);
        found.ShouldNotBeNull().Player1.Asking.ShouldBe(2);
        taken.Value.SeatChanges.ShouldBe([change]);
        journal.Lines.OfType<SeatChangeEntry>().ShouldBe([change]);
    }

    /// <summary>
    /// Who sits where is a reading of the journal's seat changes, taken when the step has its turn: a seat told
    /// to change hands while the step waited for the match has changed hands by the time the step plays it.
    /// </summary>
    [Fact]
    public async Task A_step_s_seats_are_what_the_journal_s_seat_changes_make_them_when_it_has_its_turn()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        var change = new SeatChangeEntry(PlayerSlot.Player1, "random", 1);
        await replica.Journaled.ChangeSeatAsync(table, change, TestContext.Current.CancellationToken);
        IReadOnlyList<SeatChangeEntry>? read = null;

        var step = await replica.Journaled.AdvanceAsync(
            table,
            changes =>
            {
                read = changes;
                return new MatchSeats(changes.Count == 1 ? Agent(3, PlayerSlot.Player1) : null, null);
            },
            once: true,
            TestContext.Current.CancellationToken);

        read.ShouldBe([change]);
        step.Value.Step.Decided.ShouldNotBeEmpty();
        step.Value.Step.Decided.ShouldAllBe(decision => decision.Slot == PlayerSlot.Player1);
        step.Value.Step.Asked.ShouldBe([PlayerSlot.Player2]);
    }

    /// <summary>
    /// A decision is its submitter's to make only while the seat is: when the journal's seat changes have left
    /// the seat to somebody else by the time the decision has its turn, it is late, and nothing is written.
    /// </summary>
    [Fact]
    public async Task A_decision_the_seat_changes_leave_to_somebody_else_is_late_and_writes_nothing()
    {
        var table = Table(3, CombatDeclaration.HiddenIntent);
        var journal = new JournalStore();
        var replica = new Replica(journal);
        var pass = new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass);

        var late = await replica.Journaled.SubmitAsync(table, _ => null, pass, cancellationToken: TestContext.Current.CancellationToken);
        var taken = await replica.Journaled.SubmitAsync(table, _ => MatchSeats.People, pass, cancellationToken: TestContext.Current.CancellationToken);

        late.Error.ShouldBe(DecisionErrors.NotPending);
        taken.IsSuccess.ShouldBeTrue();
        journal.Decisions.ShouldBe([pass]);
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
    private static PlayerDecision Answer(RandomAgent agent, SeatView view) => SeatAnswer.Of(agent, view);
}
