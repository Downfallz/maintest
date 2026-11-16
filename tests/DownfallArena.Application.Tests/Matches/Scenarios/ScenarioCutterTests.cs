using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;
using NSubstitute;

namespace DownfallArena.Application.Tests.Matches.Scenarios;

/// <summary>
/// A match scenario: a recorded match cut at the top of a round, and a match of its own started from there by
/// replaying the journal before the cut from the source's seed (docs/tabletop/scenarios-plan.md, stage S1).
/// </summary>
public sealed class ScenarioCutterTests
{
    public static TheoryData<uint, CombatDeclaration> Matches
    {
        get
        {
            var matches = new TheoryData<uint, CombatDeclaration>();
            foreach (var seed in new uint[] { 1, 2 })
            {
                matches.Add(seed, CombatDeclaration.HiddenIntent);
                matches.Add(seed, CombatDeclaration.AtActivation);
            }

            return matches;
        }
    }

    [Theory]
    [MemberData(nameof(Matches))]
    public async Task Every_round_a_recorded_match_began_is_offered_in_order_from_the_first(uint seed, CombatDeclaration declaration)
    {
        var (source, entries, _) = await RecordedAsync(seed, declaration);

        var reading = await new Scenarios(new Replica(new JournalStore())).Rounds.ReadAsync(source, entries, TestContext.Current.CancellationToken);

        reading.StoppedBy.ShouldBeNull();
        reading.Rounds.Count.ShouldBeGreaterThan(1);
        reading.Rounds[0].Round.ShouldBe(1);
        reading.Rounds.Select(one => one.Round).ShouldBe(Enumerable.Range(1, reading.Rounds.Count));
        reading.Rounds.Select(one => one.CutAt).ShouldBeInOrder(SortDirection.Ascending);
        reading.Rounds[^1].CutAt.ShouldBeLessThan(entries.Count);
    }

    [Theory]
    [MemberData(nameof(Matches))]
    public async Task A_scenario_started_at_any_round_stands_on_the_boards_its_source_stood_on(uint seed, CombatDeclaration declaration)
    {
        var (source, entries, _) = await RecordedAsync(seed, declaration);
        var scenarios = new Scenarios(new Replica(new JournalStore()));
        var reading = await scenarios.Rounds.ReadAsync(source, entries, TestContext.Current.CancellationToken);

        foreach (var cut in reading.Rounds)
        {
            var scenario = await scenarios.Cutter.CutAsync(source, entries, cut.Round, TestContext.Current.CancellationToken);
            var started = await scenarios.Starter.StartAsync(scenario.Value, "scenario-1", MatchId.New(), TestContext.Current.CancellationToken);

            started.IsSuccess.ShouldBeTrue($"round {cut.Round}");
            started.Value.Match.MatchId.ShouldNotBe(source.MatchId);
            started.Value.Match.Seed.ShouldBe(source.Seed);
            scenario.Value.Checkpoint.Round.ShouldBe(cut.Round);
            scenario.Value.Before.Count.ShouldBe(cut.CutAt);
            (scenario.Value.Before.Count + scenario.Value.After.Count).ShouldBe(entries.Count);
        }
    }

    /// <summary>
    /// The random stream stands where it stood at the cut: the source's own decisions after the cut, handed to the
    /// scenario's match, end it exactly as the source ended, every event the same.
    /// </summary>
    [Theory]
    [MemberData(nameof(Matches))]
    public async Task A_scenario_played_on_as_its_source_was_ends_as_the_source_did(uint seed, CombatDeclaration declaration)
    {
        var (source, entries, outcome) = await RecordedAsync(seed, declaration);
        var replica = new Replica(new JournalStore());
        var scenarios = new Scenarios(replica);
        var reading = await scenarios.Rounds.ReadAsync(source, entries, TestContext.Current.CancellationToken);
        var middle = reading.Rounds[reading.Rounds.Count / 2].Round;
        var scenario = await scenarios.Cutter.CutAsync(source, entries, middle, TestContext.Current.CancellationToken);
        var started = await scenarios.Starter.StartAsync(scenario.Value, "scenario-1", MatchId.New(), TestContext.Current.CancellationToken);

        var played = await replica.Replay.ContinueAsync(started.Value.Match, HeldAnswers.None, scenario.Value.After, scenario.Value.Before.Count, TestContext.Current.CancellationToken);

        played.IsSuccess.ShouldBeTrue();
        var board = await replica.Stepper.SeatAsync(started.Value.Match.MatchId, PlayerSlot.Player1, played.Value, TestContext.Current.CancellationToken);
        board.Value.Board.State.ShouldBe(MatchState.Ended);
        board.Value.Board.Outcome.ShouldBe(outcome);
    }

    [Fact]
    public async Task A_round_the_match_never_began_cannot_be_cut()
    {
        var (source, entries, _) = await RecordedAsync(1, CombatDeclaration.AtActivation);

        var scenario = await new Scenarios(new Replica(new JournalStore())).Cutter.CutAsync(source, entries, 99, TestContext.Current.CancellationToken);

        scenario.IsFailure.ShouldBeTrue();
        scenario.Error.Code.ShouldBe("Scenario.RoundUnavailable");
    }

    [Fact]
    public async Task A_scenario_whose_match_no_longer_stands_on_its_recorded_boards_is_not_started()
    {
        var (source, entries, _) = await RecordedAsync(1, CombatDeclaration.AtActivation);
        var scenarios = new Scenarios(new Replica(new JournalStore()));
        var second = await scenarios.Cutter.CutAsync(source, entries, 2, TestContext.Current.CancellationToken);
        var third = await scenarios.Cutter.CutAsync(source, entries, 3, TestContext.Current.CancellationToken);
        // What an engine change looks like from here: the entries rebuild, to boards other than the recorded ones.
        var drifted = second.Value with { Checkpoint = third.Value.Checkpoint with { Round = 2 } };

        var started = await scenarios.Starter.StartAsync(drifted, "scenario-1", MatchId.New(), TestContext.Current.CancellationToken);

        started.IsFailure.ShouldBeTrue();
        started.Error.ShouldBe(ScenarioErrors.Diverged);
    }

    [Fact]
    public async Task A_journal_that_stops_rebuilding_still_offers_the_round_it_stops_in_and_says_why()
    {
        var (source, entries, _) = await RecordedAsync(1, CombatDeclaration.AtActivation);
        var scenarios = new Scenarios(new Replica(new JournalStore()));
        var third = (await scenarios.Rounds.ReadAsync(source, entries, TestContext.Current.CancellationToken)).Rounds.Single(one => one.Round == 3);
        // A line of the third round no longer answers the match: the content, the rules or the engine changed.
        var broken = entries.ToList();
        broken[third.CutAt + 1] = new DecisionEntry(new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass));
        broken[third.CutAt] = new DecisionEntry(new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass));

        var reading = await scenarios.Rounds.ReadAsync(source, broken, TestContext.Current.CancellationToken);

        reading.StoppedBy.ShouldNotBeNull();
        reading.StoppedBy.Code.ShouldBe(JournalErrors.DivergedCode);
        reading.Rounds.Select(one => one.Round).ShouldBe([1, 2, 3]);
        (await scenarios.Cutter.CutAsync(source, broken, 3, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// A host cuts scenarios with the services that hold its tables: cutting one leaves the source's match in
    /// memory where its journal ends, so the table it belongs to reads and plays on as it did.
    /// </summary>
    [Fact]
    public async Task Cutting_a_scenario_leaves_the_source_s_match_in_memory_as_its_journal_leaves_it()
    {
        var replica = new Replica(new JournalStore());
        var (source, entries, outcome) = await RecordedAsync(1, CombatDeclaration.AtActivation, replica);
        var scenarios = new Scenarios(replica);

        await scenarios.Cutter.CutAsync(source, entries, 2, TestContext.Current.CancellationToken);
        await scenarios.Rounds.ReadAsync(source, [.. entries.Take(3)], TestContext.Current.CancellationToken);

        var seat = await replica.Stepper.SeatAsync(source.MatchId, PlayerSlot.Player1, HeldAnswers.None, TestContext.Current.CancellationToken);
        seat.Value.Board.State.ShouldBe(MatchState.Ended);
        seat.Value.Board.Outcome.ShouldBe(outcome);
    }

    /// <summary>
    /// The matches a cut is read on are set aside under ids of their own, and what kept their events is told to
    /// let them go; the source's own match is never forgotten, which would end a table a host is playing.
    /// </summary>
    [Fact]
    public async Task What_kept_the_matches_a_cut_was_read_on_lets_them_go_and_never_the_source()
    {
        var replica = new Replica(new JournalStore());
        var (source, entries, _) = await RecordedAsync(1, CombatDeclaration.AtActivation, replica);
        var memory = Substitute.For<IMatchMemory>();
        var rounds = new ScenarioRounds(replica.Replay, replica.Stepper, replica.Store.Repository, [memory]);
        var cutter = new ScenarioCutter(rounds, replica.Replay, replica.Stepper, replica.Store.Repository, [memory]);

        await cutter.CutAsync(source, entries, 2, TestContext.Current.CancellationToken);

        memory.ReceivedCalls().Count().ShouldBe(2);
        memory.DidNotReceive().Forget(source.MatchId);
    }

    [Fact]
    public async Task A_scenario_s_journal_keeps_the_match_s_entries_and_leaves_the_source_s_seat_changes_behind()
    {
        var (source, entries, _) = await RecordedAsync(1, CombatDeclaration.AtActivation);
        var scenarios = new Scenarios(new Replica(new JournalStore()));
        var cut = (await scenarios.Cutter.CutAsync(source, entries, 2, TestContext.Current.CancellationToken)).Value;
        var reseated = cut with { Before = [new SeatChangeEntry(PlayerSlot.Player2, "greedy", 2), .. cut.Before], Cut = cut.Cut with { CutAt = cut.Cut.CutAt + 1 } };

        var started = await scenarios.Starter.StartAsync(reseated, "scenario-1", MatchId.New(), TestContext.Current.CancellationToken);

        started.IsSuccess.ShouldBeTrue();
        started.Value.Entries.ShouldBe(cut.Before);
    }

    private sealed record Scenarios(ScenarioRounds Rounds, ScenarioCutter Cutter, ScenarioStarter Starter)
    {
        public Scenarios(Replica replica)
            : this(new ScenarioRounds(replica.Replay, replica.Stepper, replica.Store.Repository, []), replica)
        {
        }

        private Scenarios(ScenarioRounds rounds, Replica replica)
            : this(rounds, new ScenarioCutter(rounds, replica.Replay, replica.Stepper, replica.Store.Repository, []), new ScenarioStarter(replica.Replay, replica.Stepper))
        {
        }
    }

    /// <summary>A match two agents played over its journal to its end: how it was created, its journal, its outcome.</summary>
    private static async Task<(JournaledMatch Source, IReadOnlyList<IJournalEntry> Entries, MatchOutcome? Outcome)> RecordedAsync(uint seed, CombatDeclaration declaration, Replica? replica = null)
    {
        var rules = MatchStore.TwoOnTwo().WithCombatDeclaration(declaration);
        var source = new JournaledMatch("run-1", MatchId.New(), rules, (int)seed, MatchStore.Roster(rules));
        var journal = new JournalStore();
        replica ??= new Replica(journal);
        var seats = new MatchSeats(new RandomAgent(new TestRandom(seed + 1)), new RandomAgent(new TestRandom(seed + 2)));
        var step = await replica.Journaled.AdvanceAsync(source, seats, TestContext.Current.CancellationToken);
        step.IsSuccess.ShouldBeTrue();
        step.Value.Step.Outcome.ShouldNotBeNull();
        return (source, step.Value.Step.Decided.Select(decision => (IJournalEntry)new DecisionEntry(decision)).ToArray(), step.Value.Step.Outcome);
    }
}
