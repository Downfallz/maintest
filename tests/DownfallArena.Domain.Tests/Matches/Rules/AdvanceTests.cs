using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Domain.Tests.Matches.Support;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Tests.Matches.Rules;

public sealed class AdvanceTests
{
    private static readonly CreatureId One = CreatureId.From(1);
    private static readonly CreatureId Two = CreatureId.From(2);
    private static readonly CreatureId Three = CreatureId.From(3);
    private static readonly CreatureId Four = CreatureId.From(4);

    /// <summary>
    /// The test ADR 0047 asks for: a board cloned and advanced by the same rules must land where the match
    /// lands. The script exercises everything the applier carries -- a stun and a defense buff applied in
    /// combat, the fizzles they cause, energy paid, damage through a buff, conditions counting down across two
    /// cleanups, one of them fresh and one not -- and then plays on to the elimination, so the last round's
    /// cleanup, with an outcome and no start of round after it, is compared too.
    /// </summary>
    [Fact]
    public void A_board_advanced_through_every_step_of_a_match_lands_where_the_match_does()
    {
        var match = Table.Started();
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(One, Arena.Guard)).IsSuccess.ShouldBeTrue();
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(One, Arena.Slam)).IsSuccess.ShouldBeTrue();
        match.SubmitEvolutionChoice(PlayerSlot.Player2, new EvolutionChoice(Three, Arena.Guard)).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player2).IsSuccess.ShouldBeTrue();
        PlayCombat(match, new()
        {
            [One] = (Arena.Slam, [Three, Four]),
            [Two] = (Arena.Strike, [Three]),
            [Three] = (Arena.Guard, [Three]),
            [Four] = (Arena.Strike, [One]),
        });
        Table.CreatureNumber(match, 3).IsStunned.ShouldBeTrue();

        // Three and Four are stunned and sit this round out; One guards itself, which is the fresh condition
        // the next cleanup must not count down while the stuns, past their first countdown, expire.
        Table.PassEvolution(match);
        PlayCombat(match, new()
        {
            [One] = (Arena.Guard, [One]),
            [Two] = (Arena.Strike, [Three]),
        });
        Table.CreatureNumber(match, 3).IsStunned.ShouldBeFalse();
        Table.CreatureNumber(match, 1).TotalDefense.ShouldBe(Defense.Of(2));

        Table.PassEvolution(match);
        PlayCombat(match, new()
        {
            [One] = (Arena.Strike, [Three]),
            [Two] = (Arena.Strike, [Four]),
            [Three] = (Arena.Guard, [Three]),
            [Four] = (Arena.Strike, [One]),
        });
        Table.CreatureNumber(match, 1).TotalDefense.ShouldBe(Defense.Of(0));

        while (match.State == MatchState.InProgress)
        {
            Table.PassEvolution(match);
            Table.ChooseStandard(match);
            Table.DeclareStrikes(match);
            Table.HitFirstLivingEnemy(match);
            ResolveAndCompare(match);
        }

        match.Outcome.ShouldNotBeNull().Reason.ShouldBe(MatchEndReason.Elimination);
    }

    [Fact]
    public void The_cleanup_that_ends_the_match_has_no_start_of_round_after_it()
    {
        var match = Table.Started(Table.TwoOnTwo(roundCap: 1));
        Table.PassEvolution(match);
        Table.ChooseStandard(match);
        Table.DeclareStrikes(match);
        Table.HitFirstLivingEnemy(match);
        for (var resolved = 0; resolved < 3; resolved++)
        {
            match.ResolveNextAction().Value.RoundCompleted.ShouldBeFalse();
        }

        var before = match.Snapshots();
        var action = match.CurrentRound.ShouldNotBeNull().NextActionToResolve();

        var advanced = Advance.Action(action, before, Arena.Resources, match.RuleSet, new FixedRandom(0.99), Speed.Standard);
        var step = match.ResolveNextAction().Value;

        step.MatchCompleted.ShouldBeTrue();
        ShouldMatch(match.Snapshots(), Advance.Cleanup(advanced.Board, Arena.Resources));
        match.Creatures.ShouldAllBe(creature => creature.Energy == Energy.Of(2));
    }

    [Fact]
    public void The_outcome_of_a_board_at_the_round_cap_is_the_matchs()
    {
        var match = Table.Started(Table.TwoOnTwo(roundCap: 1));
        Table.PlayRound(match);

        var outcome = Advance.Outcome(match.Snapshots(), Arena.Resources, completedRound: 1, match.RuleSet);

        match.Outcome.ShouldNotBeNull().Reason.ShouldBe(MatchEndReason.RoundCap);
        outcome.ShouldBe(match.Outcome);
    }

    [Fact]
    public void A_board_with_both_teams_standing_before_the_cap_has_no_outcome()
    {
        var board = Arena.Snapshots(Arena.FourCreatures());

        Advance.Outcome(board, Arena.Resources, completedRound: 1, Table.TwoOnTwo()).ShouldBeNull();
    }

    [Fact]
    public void Advancing_a_board_leaves_the_match_it_was_taken_from_where_it_was()
    {
        var match = Table.Started();
        Table.PassEvolution(match);
        Table.ChooseStandard(match);
        Table.DeclareStrikes(match);
        Table.HitFirstLivingEnemy(match);
        var before = match.Snapshots();

        var advanced = Advance.Action(match.CurrentRound.ShouldNotBeNull().NextActionToResolve(), before, Arena.Resources, match.RuleSet, new FixedRandom(0.99), Speed.Standard);

        advanced.AppliedOutcomes.ShouldNotBeEmpty();
        ShouldMatch(match.Snapshots(), before);
        match.Creatures.ShouldAllBe(creature => creature.Health == creature.MaxHealth);
    }

    [Fact]
    public void A_fizzled_action_changes_nothing_and_says_why()
    {
        var board = Arena.Snapshots(Arena.FourCreatures());
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Slam), [Arena.Ghoul]);

        var advanced = Advance.Action(action, board, Arena.Resources, Table.TwoOnTwo(), new FixedRandom(0.99), Speed.Standard);

        advanced.Resolution.Fizzled.ShouldBeTrue();
        advanced.Resolution.FizzleReason.ShouldBe(CombatErrors.SpellNotKnown);
        advanced.AppliedOutcomes.ShouldBeEmpty();
        ShouldMatch(advanced.Board, board);
    }

    [Fact]
    public void The_random_source_decides_the_critical_roll()
    {
        var board = Arena.Snapshots(Arena.FourCreatures());
        var action = CombatAction.Bind(new CombatIntent(Arena.Knight, Arena.Strike), [Arena.Ghoul]);

        var plain = Advance.Action(action, board, Arena.Resources, Table.TwoOnTwo(), new FixedRandom(0.99), Speed.Standard);
        var critical = Advance.Action(action, board, Arena.Resources, Table.TwoOnTwo(), new FixedRandom(0.0), Speed.Standard);

        plain.Board.Single(creature => creature.Id == Arena.Ghoul).Health.ShouldBe(Health.Of(17));
        critical.Resolution.IsCritical.ShouldBeTrue();
        critical.Board.Single(creature => creature.Id == Arena.Ghoul).Health.ShouldBe(Health.Of(14));
    }

    [Fact]
    public void Cleanup_counts_a_condition_down_only_past_its_first_countdown()
    {
        var creatures = Arena.FourCreatures();
        Arena.Find(creatures, Arena.Ghoul).Apply(Stun.For(1)).ShouldNotBeNull();
        var wraith = Arena.Find(creatures, Arena.Wraith);
        wraith.Apply(Stun.For(1)).ShouldNotBeNull();
        wraith.TickConditions().ShouldBeEmpty();

        var after = Advance.Cleanup(Arena.Snapshots(creatures), Arena.Resources);

        after.Single(creature => creature.Id == Arena.Ghoul).Conditions.ShouldBe([new ConditionSnapshot(Stun.For(1), 1)]);
        after.Single(creature => creature.Id == Arena.Wraith).Conditions.ShouldBeEmpty();
        after.Single(creature => creature.Id == Arena.Wraith).IsStunned.ShouldBeFalse();
    }

    [Fact]
    public void The_start_of_a_round_gives_its_energy_and_ticks_the_ongoing_effects()
    {
        var creatures = Arena.FourCreatures();
        Arena.Find(creatures, Arena.Ghoul).Apply(Bleed.Of(4, rounds: 2)).ShouldNotBeNull();

        var after = Advance.StartOfRound(Arena.Snapshots(creatures), Arena.Resources, Table.TwoOnTwo());

        after.Single(creature => creature.Id == Arena.Ghoul).Health.ShouldBe(Health.Of(16));
        after.Single(creature => creature.Id == Arena.Ghoul).Energy.ShouldBe(Energy.Of(2));
        after.Single(creature => creature.Id == Arena.Knight).Energy.ShouldBe(Energy.Of(2));
    }

    [Fact]
    public void The_start_of_a_round_gives_a_dead_creature_nothing()
    {
        var creatures = Arena.FourCreatures();
        Arena.Find(creatures, Arena.Wraith).TakeDamage(20);

        var after = Advance.StartOfRound(Arena.Snapshots(creatures), Arena.Resources, Table.TwoOnTwo());

        after.Single(creature => creature.Id == Arena.Wraith).Energy.ShouldBe(Energy.Of(0));
    }

    /// <summary>
    /// Standard speeds, the planned intent per creature on the timeline, its planned targets in reveal order,
    /// then every action resolved through the match and through <see cref="Advance"/> side by side.
    /// </summary>
    private static void PlayCombat(Match match, Dictionary<CreatureId, (SpellId Spell, CreatureId[] Targets)> plan)
    {
        Table.ChooseStandard(match);
        var round = match.CurrentRound.ShouldNotBeNull();
        foreach (var slot in round.Timeline.Slots)
        {
            match.SubmitIntent(slot.Owner, new CombatIntent(slot.Creature, plan[slot.Creature].Spell)).IsSuccess.ShouldBeTrue();
        }

        while (round.NextSlotToReveal is { } slot)
        {
            var intent = round.IntentOf(slot.Creature).ShouldNotBeNull();
            match.SubmitAction(slot.Owner, CombatAction.Bind(intent, plan[slot.Creature].Targets)).IsSuccess.ShouldBeTrue();
        }

        ResolveAndCompare(match);
    }

    /// <summary>
    /// Walks the match's own steps: an action, and when it was the round's last, the cleanup, the outcome, and
    /// the start of the next round unless that outcome ended the match.
    /// </summary>
    private static void ResolveAndCompare(Match match)
    {
        while (match.State == MatchState.InProgress && match.CurrentRound.ShouldNotBeNull().SubPhase == RoundSubPhase.ActionResolution)
        {
            var before = match.Snapshots();
            var round = match.CurrentRound.Number;
            var action = match.CurrentRound.NextActionToResolve();
            var advanced = Advance.Action(action, before, Arena.Resources, match.RuleSet, new FixedRandom(0.99), Speed.Standard);

            var step = match.ResolveNextAction().Value;

            advanced.Resolution.Fizzled.ShouldBe(step.Resolution.Fizzled);
            advanced.Resolution.FizzleReason.ShouldBe(step.Resolution.FizzleReason);
            advanced.Resolution.Outcomes.ShouldBe(step.Resolution.Outcomes);
            advanced.AppliedOutcomes.ShouldBe(step.AppliedOutcomes);
            var expected = advanced.Board;
            if (step.RoundCompleted)
            {
                expected = Advance.Cleanup(expected, Arena.Resources);
                var outcome = Advance.Outcome(expected, Arena.Resources, round, match.RuleSet);
                outcome.ShouldBe(match.Outcome);
                if (outcome is null)
                {
                    expected = Advance.StartOfRound(expected, Arena.Resources, match.RuleSet);
                }
            }

            ShouldMatch(match.Snapshots(), expected);
        }
    }

    /// <summary>
    /// Field by field: a snapshot is a record, but its known spells and conditions are collections, which a
    /// record compares by reference.
    /// </summary>
    private static void ShouldMatch(IReadOnlyList<CreatureSnapshot> actual, IReadOnlyList<CreatureSnapshot> expected)
    {
        actual.Count.ShouldBe(expected.Count);
        foreach (var (creature, other) in actual.Zip(expected))
        {
            creature.Id.ShouldBe(other.Id);
            creature.Owner.ShouldBe(other.Owner);
            creature.DefinitionId.ShouldBe(other.DefinitionId);
            creature.Name.ShouldBe(other.Name);
            creature.TalentTree.ShouldBe(other.TalentTree);
            creature.Health.ShouldBe(other.Health, $"creature {creature.Id}");
            creature.MaxHealth.ShouldBe(other.MaxHealth);
            creature.Energy.ShouldBe(other.Energy, $"creature {creature.Id}");
            creature.TotalDefense.ShouldBe(other.TotalDefense, $"creature {creature.Id}");
            creature.BaseInitiative.ShouldBe(other.BaseInitiative);
            creature.CurrentInitiative.ShouldBe(other.CurrentInitiative);
            creature.CriticalChance.ShouldBe(other.CriticalChance);
            creature.IsStunned.ShouldBe(other.IsStunned, $"creature {creature.Id}");
            creature.KnownSpells.ShouldBe(other.KnownSpells, ignoreOrder: true);
            creature.Conditions.ShouldBe(other.Conditions, $"creature {creature.Id}");
        }
    }
}
