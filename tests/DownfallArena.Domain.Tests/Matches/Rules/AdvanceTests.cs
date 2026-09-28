using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Events;
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
    /// combat, the fizzles they cause when a slot comes up, energy paid, damage through a buff, conditions counting down across two
    /// cleanups, one of them fresh and one not -- and then plays on to the elimination, so the last round's
    /// cleanup, with an outcome and no start of round after it, is compared too.
    /// </summary>
    [Fact]
    public void A_board_advanced_through_every_step_of_a_match_lands_where_the_match_does()
    {
        var match = Table.Started();
        // The script needs Slam in round 1, and a creature buys one package a round (ADR 0066): it comes into
        // the match already owning Slam's prerequisite, and the match's one pick for it buys Slam.
        Table.CreatureNumber(match, 1).BuyTier(Arena.Resources.GetTier(Arena.GuardPack)).IsSuccess.ShouldBeTrue();
        match.SubmitEvolutionChoice(PlayerSlot.Player1, new EvolutionChoice(One, Arena.SlamPack)).IsSuccess.ShouldBeTrue();
        match.PassEvolution(PlayerSlot.Player1).IsSuccess.ShouldBeTrue();
        match.SubmitEvolutionChoice(PlayerSlot.Player2, new EvolutionChoice(Three, Arena.GuardPack)).IsSuccess.ShouldBeTrue();
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
            Compared(match, () => Table.DeclareStrikes(match));
            ActivateEachSlot(match, slot => [Table.Living(match, Enemy(slot.Owner)).First().Id]);
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
        var round = match.CurrentRound.ShouldNotBeNull();
        for (var activated = 0; activated < 3; activated++)
        {
            var slot = round.NextSlot.ShouldNotBeNull();
            match.SubmitAction(slot.Owner, FirstLivingEnemy(match, round)).IsSuccess.ShouldBeTrue();
        }

        var before = match.Snapshots();
        var action = FirstLivingEnemy(match, round);
        var owner = round.NextSlot.ShouldNotBeNull().Owner;

        var advanced = Advance.Action(action, before, Arena.Resources, match.RuleSet, new FixedRandom(0.99), Speed.Standard);
        match.SubmitAction(owner, action).IsSuccess.ShouldBeTrue();

        match.State.ShouldBe(MatchState.Ended);
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
    public void A_board_with_a_team_wiped_is_an_elimination_whatever_the_round()
    {
        var creatures = Arena.FourCreatures();
        Arena.Find(creatures, Arena.Ghoul).TakeDamage(99);
        Arena.Find(creatures, Arena.Wraith).TakeDamage(99);

        var outcome = Advance.Elimination(Arena.Snapshots(creatures), Arena.Resources);

        outcome.ShouldBe(new MatchOutcome(PlayerSlot.Player1, MatchEndReason.Elimination));
        Advance.Elimination(Arena.Snapshots(Arena.FourCreatures()), Arena.Resources).ShouldBeNull();
    }

    [Fact]
    public void Advancing_a_board_leaves_the_match_it_was_taken_from_where_it_was()
    {
        var match = Table.Started();
        Table.PassEvolution(match);
        Table.ChooseStandard(match);
        Table.DeclareStrikes(match);
        var before = match.Snapshots();

        var advanced = Advance.Action(FirstLivingEnemy(match, match.CurrentRound.ShouldNotBeNull()), before, Arena.Resources, match.RuleSet, new FixedRandom(0.99), Speed.Standard);

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
    /// Standard speeds, the planned intent per creature on the timeline, and its planned targets when its slot
    /// comes up, every action compared with what <see cref="Advance"/> makes of it.
    /// </summary>
    private static void PlayCombat(Match match, Dictionary<CreatureId, (SpellId Spell, CreatureId[] Targets)> plan)
    {
        Table.ChooseStandard(match);
        var round = match.CurrentRound.ShouldNotBeNull();
        Compared(match, () =>
        {
            foreach (var slot in round.Timeline.Slots)
            {
                match.SubmitIntent(slot.Owner, new CombatIntent(slot.Creature, plan[slot.Creature].Spell)).IsSuccess.ShouldBeTrue();
            }
        });
        ActivateEachSlot(match, slot => plan[slot.Creature].Targets);
    }

    /// <summary>Binds the targets of every slot a player is asked for, comparing each activation.</summary>
    private static void ActivateEachSlot(Match match, Func<ActivationSlot, CreatureId[]> targets)
    {
        var round = match.CurrentRound.ShouldNotBeNull();
        while (match.State == MatchState.InProgress && round.SubPhase == RoundSubPhase.Activation && round.NextSlot is { } slot)
        {
            var action = CombatAction.Bind(round.IntentOf(slot.Creature).ShouldNotBeNull(), targets(slot));
            Compared(match, () => match.SubmitAction(slot.Owner, action).IsSuccess.ShouldBeTrue());
        }
    }

    /// <summary>
    /// Runs a command and walks what the match did in it: every action resolved, the fizzles it activated on
    /// its own included, each from the board the one before it left; then, when the round ended, the cleanup,
    /// the outcome, and the start of the next round unless that outcome ended the match; or the elimination
    /// the match stopped on (ADR 0083).
    /// </summary>
    private static void Compared(Match match, Action command)
    {
        var seen = match.DomainEvents.Count;
        var round = match.CurrentRound.ShouldNotBeNull().Number;
        var expected = match.Snapshots();

        command();

        var events = match.DomainEvents.Skip(seen).ToList();
        foreach (var resolved in events.OfType<CombatActionResolved>())
        {
            var advanced = Advance.Action(resolved.Resolution.Action, expected, Arena.Resources, match.RuleSet, new FixedRandom(0.99), Speed.Standard);
            advanced.Resolution.Fizzled.ShouldBe(resolved.Resolution.Fizzled);
            advanced.Resolution.FizzleReason.ShouldBe(resolved.Resolution.FizzleReason);
            advanced.Resolution.Outcomes.ShouldBe(resolved.Resolution.Outcomes);
            advanced.AppliedOutcomes.ShouldBe(resolved.AppliedOutcomes);
            ShouldMatch(resolved.Frame.ShouldNotBeNull().After, advanced.Board);
            expected = advanced.Board;
        }

        if (events.OfType<ConditionsExpired>().Any())
        {
            expected = Advance.Cleanup(expected, Arena.Resources);
            var outcome = Advance.Outcome(expected, Arena.Resources, round, match.RuleSet);
            if (outcome is null)
            {
                expected = Advance.StartOfRound(expected, Arena.Resources, match.RuleSet);
                outcome = Advance.Elimination(expected, Arena.Resources);
            }

            outcome.ShouldBe(match.Outcome);
        }
        else
        {
            Advance.Elimination(expected, Arena.Resources).ShouldBe(match.Outcome);
        }

        ShouldMatch(match.Snapshots(), expected);
    }

    private static CombatAction FirstLivingEnemy(Match match, Round round)
    {
        var slot = round.NextSlot.ShouldNotBeNull();
        return CombatAction.Bind(round.IntentOf(slot.Creature).ShouldNotBeNull(), [Table.Living(match, Enemy(slot.Owner)).First().Id]);
    }

    private static PlayerSlot Enemy(PlayerSlot slot) => slot == PlayerSlot.Player1 ? PlayerSlot.Player2 : PlayerSlot.Player1;

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
            creature.StunImmunityRounds.ShouldBe(other.StunImmunityRounds, $"creature {creature.Id}");
            creature.KnownSpells.ShouldBe(other.KnownSpells, ignoreOrder: true);
            creature.Conditions.ShouldBe(other.Conditions, $"creature {creature.Id}");
        }
    }
}
